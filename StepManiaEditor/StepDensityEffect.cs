using System;
using System.Threading;
using System.Threading.Tasks;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.StepDensity;
using static StepManiaEditor.PreferencesDensityGraph;
using ColorUtils = MonoGameExtensions.ColorUtils;

namespace StepManiaEditor;

/// <summary>
/// StepDensityEffect renders a density graph for an EditorChart's StepDensity.
/// It also asynchronously computes aggregate data over all StepDensity data like Peak NPS.
/// Expected Usage:
///  Call SetStepDensity to update the StepDensity to render.
///  Call UpdateBounds to set the bounds of the effect.
///  Call Draw once each frame to render the effect.
///  Call ResetBufferCapacities to reset capacities for internal rendering buffers.
///  For input processing call MouseDown, MouseMove, and MouseUp.
///  To get the position from the MiniMap call GetTimeFromScrollBar.
/// </summary>
internal sealed class StepDensityEffect : Fumen.IObserver<StepDensity>, Fumen.IObserver<PreferencesDensityGraph>, IDisposable
{
	private const int MinNumMeasures = 256;
	private const int MinNumVertices = 2048;
	private const int MinNumIndices = 6288;
	private const float RimW = 1.0f;
	private const float TextPadding = 3.0f;
	private static readonly Color RimColor = Color.White;
	private static readonly Color TimeMarkerColor = new(0.8f, 0.8f, 0.8f, 1.0f);
	private static readonly Color TimeRegionColor = new(1.0f, 1.0f, 1.0f, 0.122f);
	private static readonly Color TimeRegionHoveredColor = new(1.0f, 1.0f, 1.0f, 0.155f);
	private static readonly Color TimeRegionSelectedColor = new(1.0f, 1.0f, 1.0f, 0.188f);

	/// <summary>
	/// Orientation of the effect.
	/// </summary>
	public enum Orientation
	{
		Vertical,
		Horizontal,
	}

	/// <summary>
	/// Data for communicating state from calls to update the measures to the long-running
	/// thread which processes measures and turns them into primitives. For abusively long
	/// content the number of measures can be in the multiple tens of thousands. We do not
	/// want to loop over that on the main thread every time a change occurs.
	/// </summary>
	internal sealed class CreatePrimitivesState
	{
		private readonly object Lock = new();

		private readonly DynamicArray<Measure> EnqueuedMeasures = new(MinNumMeasures);
		private double FinalTime;
		private float Width;
		private float Height;
		private Color LowColor;
		private Color HighColor;
		private Color BackgroundColor;
		private DensityGraphColorMode ColorMode;
		private StepAccumulationType AccumulationMode;

		private bool HasNewEnqueuedData;
		private bool ShouldResetCapacities;
		private bool Shutdown;

		private readonly DynamicArray<Measure> WorkingMeasures = new(MinNumMeasures);
		private readonly DynamicArray<VertexPositionColor> WorkingVertices = new(MinNumVertices);
		private readonly DynamicArray<int> WorkingIndices = new(MinNumIndices);

		public void EnqueueData(
			IReadOnlyDynamicArray<Measure> measures,
			double finalTime,
			float width,
			float height,
			Color lowColor,
			Color highColor,
			Color backgroundColor,
			DensityGraphColorMode colorMode,
			StepAccumulationType accumulationMode)
		{
			lock (Lock)
			{
				HasNewEnqueuedData = true;
				if (ShouldResetCapacities)
					EnqueuedMeasures.UpdateCapacity(MinNumMeasures);
				if (measures != null)
					EnqueuedMeasures.CopyFrom(measures);
				else
					EnqueuedMeasures.Clear();

				FinalTime = finalTime;
				Width = width;
				Height = height;
				LowColor = lowColor;
				HighColor = highColor;
				BackgroundColor = backgroundColor;
				ColorMode = colorMode;
				AccumulationMode = accumulationMode;
			}
		}

		public bool TryPopEnqueuedData(
			ref DynamicArray<Measure> measures,
			ref DynamicArray<VertexPositionColor> vertices,
			ref DynamicArray<int> indices,
			ref double finalTime,
			ref float width,
			ref float height,
			ref Color lowColor,
			ref Color highColor,
			ref Color backgroundColor,
			ref DensityGraphColorMode colorMode,
			ref StepAccumulationType accumulationMode)
		{
			lock (Lock)
			{
				if (ShouldResetCapacities)
				{
					WorkingMeasures.UpdateCapacity(MinNumMeasures);
					WorkingVertices.UpdateCapacity(MinNumVertices);
					WorkingIndices.UpdateCapacity(MinNumIndices);
					ShouldResetCapacities = false;
				}

				if (!HasNewEnqueuedData)
					return false;

				WorkingMeasures.Clear();
				WorkingVertices.Clear();
				WorkingIndices.Clear();

				WorkingMeasures.CopyFrom(EnqueuedMeasures);

				measures = WorkingMeasures;
				vertices = WorkingVertices;
				indices = WorkingIndices;
				finalTime = FinalTime;
				width = Width;
				height = Height;
				lowColor = LowColor;
				highColor = HighColor;
				backgroundColor = BackgroundColor;
				colorMode = ColorMode;
				accumulationMode = AccumulationMode;

				HasNewEnqueuedData = false;
				return true;
			}
		}

		public void ResetCapacities()
		{
			lock (Lock)
			{
				ShouldResetCapacities = true;
			}
		}

		public void SetShouldShutdown()
		{
			Shutdown = true;
		}

		public bool ShouldShutdown()
		{
			return Shutdown;
		}
	}

	/// <summary>
	/// State for communicating updates to the primitives thread.
	/// </summary>
	private readonly CreatePrimitivesState State = new();

	/// <summary>
	/// The GraphicsDevice used for rendering the density graph.
	/// </summary>
	private readonly GraphicsDevice GraphicsDevice;

	/// <summary>
	/// The BasicEffect used for rendering the density graph.
	/// </summary>
	private readonly BasicEffect DensityEffect;

	/// <summary>
	/// SpriteBatch for rendering stream text.
	/// </summary>
	private readonly SpriteBatch SpriteBatch;

	/// <summary>
	/// Font for rendering stream text.
	/// </summary>
	private readonly SpriteFont Font;

	/// <summary>
	/// RasterizerState for rendering text with a scissor rect.
	/// </summary>
	private readonly RasterizerState TextRasterizerState;

	/// <summary>
	/// Lock for data computed from the StepDensity measure data.
	/// </summary>
	private readonly object DataLock = new();

	/// <summary>
	/// Primitive vertex array.
	/// </summary>
	private readonly DynamicArray<VertexPositionColor> Vertices = new(MinNumVertices);

	/// <summary>
	/// Primitive index array.
	/// </summary>
	private readonly DynamicArray<int> Indices = new(MinNumIndices);

	/// <summary>
	/// Number of primitives.
	/// </summary>
	private int NumPrimitives;

	/// <summary>
	/// Peak notes per second value.
	/// </summary>
	private double PeakNps;

	/// <summary>
	/// Peak rows per second value.
	/// </summary>
	private double PeakRps;

	/// <summary>
	/// Long-running task for updating primitive data.
	/// </summary>
	private readonly Task UpdateDataTask;

	/// <summary>
	/// Primitive vertex array for the scroll bar.
	/// </summary>
	private readonly VertexPositionColor[] ScrollBarVertices = new VertexPositionColor[8];

	/// <summary>
	/// Primitive index array for the scroll bar.
	/// </summary>
	private readonly int[] ScrollBarIndices = new int[12];

	/// <summary>
	/// Whether or not the scroll bar is visible.
	/// </summary>
	private bool ScrollBarVisible;

	/// <summary>
	/// Flag for not rendering the scroll bar due to it having invalid bounds even it should be visible.
	/// </summary>
	private bool ScrollBarInvalidBounds;

	/// <summary>
	/// Scroll bar start time.
	/// </summary>
	private double ScrollBarStartTime;

	/// <summary>
	/// Scroll bar end time.
	/// </summary>
	private double ScrollBarEndTime;

	/// <summary>
	/// Scroll bar current time.
	/// </summary>
	private double ScrollBarCurrentTime;

	/// <summary>
	/// Current StepDensity to render.
	/// </summary>
	private StepDensity StepDensity;

	/// <summary>
	/// Screen space bounds of the density graph.
	/// </summary>
	private Rectangle Bounds;

	/// <summary>
	/// Screen space bounds of the scroll bar within the density graph.
	/// </summary>
	private Rectangle ScrollBarBounds;

	/// <summary>
	/// Whether or not the mouse is currently over the scroll bar.
	/// </summary>
	private bool MouseOverScrollBar;

	/// <summary>
	/// Orientation of the density graph.
	/// </summary>
	private Orientation EffectOrientation = Orientation.Horizontal;

	/// <summary>
	/// Whether or not the scroll bar is currently being grabbed for movement.
	/// </summary>
	private bool Grabbed;

	/// <summary>
	/// When grabbing the scroll bar, this stores where within the scroll bar area
	/// the user clicked so that when they scroll the editor doesn't jump to center
	/// on the selected area.
	/// </summary>
	private double GrabbedPositionAsPercentageOfScrollBar;

	/// <summary>
	/// Disposed flag for IDisposable interface.
	/// </summary>
	private bool Disposed;

	#region Initialization

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="graphicsDevice">GraphicsDevice to use for the effect.</param>
	/// <param name="font">Font for rendering stream text.</param>
	public StepDensityEffect(GraphicsDevice graphicsDevice, SpriteFont font)
	{
		// Set up the Effect for rendering.
		GraphicsDevice = graphicsDevice;
		DensityEffect = new BasicEffect(GraphicsDevice);
		DensityEffect.VertexColorEnabled = true;
		DensityEffect.World = Matrix.Identity;

		TextRasterizerState = new RasterizerState
		{
			CullMode = CullMode.None,
			DepthBias = 0,
			FillMode = FillMode.Solid,
			MultiSampleAntiAlias = false,
			ScissorTestEnable = true,
			SlopeScaleDepthBias = 0,
		};

		SpriteBatch = new SpriteBatch(GraphicsDevice);
		Font = font;

		// Observe relevant preferences so the effect can be updated accordingly.
		Preferences.Instance.PreferencesDensityGraph.AddObserver(this);

		InitializeScrollBar();

		// Start a long-running task to process updates to data derived from the StepDensity.
		UpdateDataTask = Task.Factory.StartNew(
			UpdateData,
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);
	}

	private void InitializeScrollBar()
	{
		ScrollBarIndices[0] = 1;
		ScrollBarIndices[1] = 0;
		ScrollBarIndices[2] = 2;
		ScrollBarIndices[3] = 0;
		ScrollBarIndices[4] = 3;
		ScrollBarIndices[5] = 2;
		ScrollBarIndices[6] = 5;
		ScrollBarIndices[7] = 4;
		ScrollBarIndices[8] = 6;
		ScrollBarIndices[9] = 4;
		ScrollBarIndices[10] = 7;
		ScrollBarIndices[11] = 6;
	}

	#endregion Initialization

	#region IDisposable

	~StepDensityEffect()
	{
		Dispose(false);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (Disposed)
			return;
		if (disposing)
		{
			Preferences.Instance.PreferencesDensityGraph.RemoveObserver(this);
			StepDensity?.RemoveObserver(this);
			State.SetShouldShutdown();
			UpdateDataTask.Wait();
		}

		Disposed = true;
	}

	#endregion IDisposable

	/// <summary>
	/// Set the StepDensity to use for rendering the density effect.
	/// </summary>
	/// <param name="stepDensity">StepDensity to use for rendering the density effect.</param>
	public void SetStepDensity(StepDensity stepDensity)
	{
		StepDensity?.RemoveObserver(this);
		StepDensity = stepDensity;
		StepDensity?.AddObserver(this);
		RefreshData();
	}

	/// <summary>
	/// Reset buffer capacities to default values.
	/// Useful when unloading one (large) song and loading another.
	/// </summary>
	public void ResetBufferCapacities()
	{
		State.ResetCapacities();
		lock (DataLock)
		{
			Vertices.UpdateCapacity(MinNumVertices);
			Indices.UpdateCapacity(MinNumIndices);
		}
	}

	#region Accessors

	public double GetPeakNps()
	{
		return PeakNps;
	}

	public double GetPeakRps()
	{
		return PeakRps;
	}

	#endregion Accessors

	#region Mouse Input

	public bool IsInDensityGraphArea(int screenX, int screenY)
	{
		return Bounds.Contains(screenX, screenY);
	}

	private void UpdateTrackingMouseOverScrollBar(int screenX, int screenY)
	{
		MouseOverScrollBar = ScrollBarVisible && !ScrollBarInvalidBounds && ScrollBarBounds.Contains(screenX, screenY);
	}

	private double GetScreenSpacePositionFromTime(double time)
	{
		if (StepDensity == null)
			return 0.0;
		if (EffectOrientation == Orientation.Horizontal)
			return Bounds.X + RimW + (Bounds.Width - RimW * 2) * (time / StepDensity.GetLastMeasurePlusOneTime());
		return Bounds.Y + RimW + (Bounds.Height - RimW * 2) * (time / StepDensity.GetLastMeasurePlusOneTime());
	}

	/// <summary>
	/// Called when the mouse button is pressed.
	/// </summary>
	/// <param name="screenX">X mouse position in screen space.</param>
	/// <param name="screenY">Y mouse position in screen space.</param>
	/// <returns>Whether or not the StepDensityEffect has captured this input.</returns>
	public bool MouseDown(int screenX, int screenY)
	{
		UpdateTrackingMouseOverScrollBar(screenX, screenY);

		if (!Bounds.Contains(screenX, screenY))
			return false;

		Grabbed = true;

		// Grabbed the scroll bar.
		if (MouseOverScrollBar)
		{
			var screenPos = EffectOrientation == Orientation.Horizontal ? screenX : screenY;
			var endTimeScreenSpace = GetScreenSpacePositionFromTime(ScrollBarEndTime);
			var startTimeScreenSpace = GetScreenSpacePositionFromTime(ScrollBarStartTime);
			GrabbedPositionAsPercentageOfScrollBar =
				(screenPos - startTimeScreenSpace) / (endTimeScreenSpace - startTimeScreenSpace);
		}
		// Grabbed outside the scroll bar.
		else
		{
			GrabbedPositionAsPercentageOfScrollBar =
				(ScrollBarCurrentTime - ScrollBarStartTime) / (ScrollBarEndTime - ScrollBarStartTime);
		}

		MouseMove(screenX, screenY);
		return true;
	}

	/// <summary>
	/// Called when the mouse moves.
	/// </summary>
	/// <param name="screenX">Mouse X position in screen space.</param>
	/// <param name="screenY">Mouse Y position in screen space.</param>
	public void MouseMove(int screenX, int screenY)
	{
		UpdateTrackingMouseOverScrollBar(screenX, screenY);
		if (!Grabbed)
			return;

		MoveScrollBar(screenX, screenY);
	}

	/// <summary>
	/// Called when the mouse button is released.
	/// </summary>
	/// <param name="screenX">Mouse X position in screen space.</param>
	/// <param name="screenY">Mouse Y position in screen space.</param>
	public void MouseUp(int screenX, int screenY)
	{
		UpdateTrackingMouseOverScrollBar(screenX, screenY);
		Grabbed = false;
	}

	/// <summary>
	/// Returns whether or not the StepDensityEffect wants to be processing mouse input, which is
	/// equivalent to if the StepDensityEffect's scroll bar region is being grabbed.
	/// </summary>
	/// <returns>Whether or not the StepDensityEffect wants to be processing mouse input.</returns>
	public bool WantsMouse()
	{
		return Grabbed;
	}

	/// <summary>
	/// Move the scrollbar based on the mouse's screen position.
	/// </summary>
	/// <param name="screenX">Mouse X position in screen space.</param>
	/// <param name="screenY">Mouse Y position in screen space.</param>
	private void MoveScrollBar(int screenX, int screenY)
	{
		if (StepDensity == null)
			return;

		var finalTime = StepDensity.GetLastMeasurePlusOneTime();
		var mouseX = EffectOrientation == Orientation.Horizontal ? screenX : screenY;
		var startX = EffectOrientation == Orientation.Horizontal ? Bounds.X + RimW : Bounds.Y + RimW;
		var totalW = EffectOrientation == Orientation.Horizontal ? Bounds.Width - RimW * 2 : Bounds.Height - RimW * 2;

		var scrollBarDuration = ScrollBarEndTime - ScrollBarStartTime;
		var scrollBarDurationToCurrentTime = ScrollBarCurrentTime - ScrollBarStartTime;
		var newTime = (mouseX - startX) / totalW * finalTime;
		ScrollBarStartTime = newTime - GrabbedPositionAsPercentageOfScrollBar * scrollBarDuration;
		ScrollBarEndTime = ScrollBarStartTime + scrollBarDuration;
		ScrollBarCurrentTime = ScrollBarStartTime + scrollBarDurationToCurrentTime;

		UpdateScrollBarPrimitives();
	}

	/// <summary>
	/// Gets the time from the scroll bar.
	/// </summary>
	/// <returns>Time from the scroll bar.</returns>
	public double GetTimeFromScrollBar()
	{
		return ScrollBarCurrentTime;
	}

	#endregion Mouse Input

	#region Update

	/// <summary>
	/// Update the bounds of the effect.
	/// </summary>
	/// <param name="bounds">Bound in screen space for the effect to occupy.</param>
	/// <param name="orientation">The orientation of the effect.</param>
	public void UpdateBounds(Rectangle bounds, Orientation orientation)
	{
		var dimensionsDirty = bounds.Width != Bounds.Width
		                      || bounds.Height != Bounds.Height
		                      || orientation != EffectOrientation;
		Bounds = bounds;
		EffectOrientation = orientation;
		if (!dimensionsDirty)
			return;
		RefreshData();
	}

	/// <summary>
	/// Updates the StepDensityEffect.
	/// </summary>
	/// <param name="timeRangeStart">Current visible time range start of the chart within the Editor.</param>
	/// <param name="timeRangeEnd">Current visible time range end of the chart within the Editor.</param>
	/// <param name="currentTime">Current time of the chart.</param>
	public void Update(double timeRangeStart, double timeRangeEnd, double currentTime)
	{
		ScrollBarStartTime = timeRangeStart;
		ScrollBarEndTime = timeRangeEnd;
		ScrollBarCurrentTime = currentTime;

		// Update the scroll bar.
		if (StepDensity == null)
		{
			ScrollBarVisible = false;
			return;
		}

		ScrollBarVisible = true;
		UpdateScrollBarPrimitives();
	}

	#endregion Update

	#region Draw

	/// <summary>
	/// Draw the density graph.
	/// </summary>
	public void Draw()
	{
		if (!Preferences.Instance.PreferencesDensityGraph.ShowDensityGraph)
			return;

		var viewportW = GraphicsDevice.Viewport.Width;
		var viewportH = GraphicsDevice.Viewport.Height;
		var x = (int)(viewportW * 0.5 - Bounds.X + 0.5);
		DensityEffect.Projection = Matrix.CreateOrthographic(viewportW, viewportH, -10, 10);

		// The primitives are always generated horizontally. For vertical orientation we rotate the view.
		if (EffectOrientation == Orientation.Vertical)
		{
			var y = (int)(viewportH * 0.5) - Bounds.Y;
			DensityEffect.View = Matrix.CreateLookAt(new Vector3(y, x, 2), new Vector3(y, x, 0), Vector3.Left);
		}
		else
		{
			var y = Bounds.Y + Bounds.Height - (int)(viewportH * 0.5);
			DensityEffect.View = Matrix.CreateLookAt(new Vector3(x, y, 2), new Vector3(x, y, 0), Vector3.Up);
		}

		// Draw primitives.
		lock (DataLock)
		{
			foreach (var pass in DensityEffect.CurrentTechnique.Passes)
			{
				pass.Apply();
				if (NumPrimitives > 0)
				{
					GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, Vertices.GetArray(), 0,
						Vertices.GetSize(),
						Indices.GetArray(), 0, NumPrimitives);
				}

				if (ScrollBarVisible && !ScrollBarInvalidBounds)
				{
					GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, ScrollBarVertices, 0,
						ScrollBarVertices.Length,
						ScrollBarIndices, 0, 4);
				}
			}
		}

		// Draw stream breakdown text.
		if (StepDensity != null && Font != null && Preferences.Instance.PreferencesDensityGraph.ShowStream)
		{
			var stream = StepDensity.GetStreamBreakdown();
			var textSize = Font.MeasureString(stream);

			var minHeightForText = EffectOrientation == Orientation.Vertical ? Bounds.Width : Bounds.Height;
			if (textSize.Y + TextPadding * 2 <= minHeightForText && Bounds.Width > 0 && Bounds.Height > 0)
			{
				var previousScissorRect = GraphicsDevice.ScissorRectangle;
				GraphicsDevice.ScissorRectangle = Bounds;

				SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, null, null, TextRasterizerState);

				float rotation;
				Vector2 position;
				if (EffectOrientation == Orientation.Vertical)
				{
					position = new Vector2(Bounds.X + TextPadding + textSize.Y, Bounds.Y + TextPadding);
					rotation = (float)(Math.PI * 0.5);
				}
				else
				{
					position = new Vector2(Bounds.X + TextPadding, Bounds.Y + Bounds.Height - TextPadding - textSize.Y);
					rotation = 0.0f;
				}

				SpriteBatch.DrawString(
					Font,
					stream,
					position,
					Color.White,
					rotation,
					Vector2.Zero,
					1.0f,
					SpriteEffects.None,
					1.0f);

				SpriteBatch.End();

				GraphicsDevice.ScissorRectangle = previousScissorRect;
			}
		}
	}

	#endregion Draw

	#region Primitive Generation

	/// <summary>
	/// Update the scroll bar primitives.
	/// The scroll bar can frequently change every frame, and we want it to be responsive so
	/// we update these primitives directly rather than enqueueing actions for them.
	/// </summary>
	private void UpdateScrollBarPrimitives()
	{
		var w = EffectOrientation == Orientation.Vertical ? Bounds.Height : Bounds.Width;
		var h = EffectOrientation == Orientation.Vertical ? Bounds.Width : Bounds.Height;

		var xRange = w - 2 * RimW;
		var yRange = h - 2 * RimW;
		if (xRange <= 0 || yRange <= 0)
		{
			ScrollBarInvalidBounds = true;
			return;
		}

		ScrollBarInvalidBounds = false;

		var minX = RimW;
		var maxX = w - RimW;

		var finalTime = StepDensity.GetLastMeasurePlusOneTime();
		var markerStartX = (float)(RimW + ScrollBarCurrentTime / finalTime * xRange);
		var markerEndX = markerStartX + 1;
		var regionStartX = markerStartX - (int)((ScrollBarCurrentTime - ScrollBarStartTime) / finalTime * xRange);
		var regionEndX = markerStartX + (int)((ScrollBarEndTime - ScrollBarCurrentTime) / finalTime * xRange);

		markerStartX = Math.Clamp(markerStartX, minX, maxX);
		markerEndX = Math.Clamp(markerEndX, minX, maxX);
		regionStartX = Math.Clamp(regionStartX, minX, maxX);
		regionEndX = Math.Clamp(regionEndX, minX, maxX);

		var topY = h - RimW;
		var bottomY = RimW;

		if (EffectOrientation == Orientation.Vertical)
			ScrollBarBounds = new Rectangle(Bounds.X + (int)bottomY, Bounds.Y + (int)regionStartX, (int)(topY - bottomY),
				(int)(regionEndX - regionStartX));
		else
			ScrollBarBounds = new Rectangle(Bounds.X + (int)regionStartX, Bounds.Y + (int)bottomY,
				(int)(regionEndX - regionStartX), (int)(topY - bottomY));

		var barColor = Grabbed ? TimeRegionSelectedColor : MouseOverScrollBar ? TimeRegionHoveredColor : TimeRegionColor;

		ScrollBarVertices[0] = new VertexPositionColor(new Vector3(regionStartX, topY, 0.0f), barColor);
		ScrollBarVertices[1] = new VertexPositionColor(new Vector3(regionStartX, bottomY, 0.0f), barColor);
		ScrollBarVertices[2] = new VertexPositionColor(new Vector3(regionEndX, bottomY, 0.0f), barColor);
		ScrollBarVertices[3] = new VertexPositionColor(new Vector3(regionEndX, topY, 0.0f), barColor);
		ScrollBarVertices[4] = new VertexPositionColor(new Vector3(markerStartX, topY, 0.0f), TimeMarkerColor);
		ScrollBarVertices[5] = new VertexPositionColor(new Vector3(markerStartX, bottomY, 0.0f), TimeMarkerColor);
		ScrollBarVertices[6] = new VertexPositionColor(new Vector3(markerEndX, bottomY, 0.0f), TimeMarkerColor);
		ScrollBarVertices[7] = new VertexPositionColor(new Vector3(markerEndX, topY, 0.0f), TimeMarkerColor);
	}

	/// <summary>
	/// Long-running task to update the primitives used for rendering.
	/// </summary>
	private async Task UpdateData()
	{
		// Local state.
		DynamicArray<Measure> measures = null;
		DynamicArray<VertexPositionColor> vertices = null;
		DynamicArray<int> indices = null;
		var finalTime = 0.0;
		var width = 0.0f;
		var height = 0.0f;
		var lowColor = Color.White;
		var highColor = Color.White;
		var backgroundColor = Color.Black;
		var colorMode = DensityGraphColorMode.ColorByDensity;
		var accumulationMode = StepAccumulationType.Step;

		// Loop continuously, yielding when there is no work.
		while (true)
		{
			try
			{
				// Check for work.
				while (true)
				{
					// Return if we should be shutting down.
					if (State.ShouldShutdown())
						return;

					// Try to pop any enqueued data. If there is data to process then break out and process it below.
					if (State.TryPopEnqueuedData(ref measures, ref vertices, ref indices, ref finalTime, ref width, ref height,
						    ref lowColor, ref highColor, ref backgroundColor, ref colorMode, ref accumulationMode))
						break;
					await Task.Delay(1);
				}

				// Begin processing new data.
				var numPrimitives = 0;
				var peakNps = 0.0;
				var peakRps = 0.0;

				// Early out on invalid bounds.
				if (height < 0.0f || width < 0.0f)
				{
					UpdateData(vertices, indices, numPrimitives, peakNps, peakRps);
					continue;
				}

				// Add the background primitives.
				AddBackground(vertices, indices, ref numPrimitives, width, height, ref backgroundColor);

				// Early out due to no measures or not enough area to render measures
				if (measures.GetSize() == 0 || height <= RimW * 2 || width <= RimW * 2)
				{
					// Add the rim primitives.
					AddRim(vertices, indices, ref numPrimitives, width, height);

					UpdateData(vertices, indices, numPrimitives, peakNps, peakRps);
					continue;
				}

				// Determine the greatest number of steps per measure.
				for (var i = 0; i < measures.GetSize(); i++)
				{
					double measureTime;
					if (i + 1 < measures.GetSize())
						measureTime = measures[i + 1].StartTime - measures[i].StartTime;
					else
						measureTime = finalTime - measures[i].StartTime;
					if (measureTime > 0.0)
					{
						peakNps = Math.Max(measures[i].Steps / measureTime, peakNps);
						peakRps = Math.Max(measures[i].RowsWithSteps / measureTime, peakRps);
					}
				}

				var peakSteps = accumulationMode == StepAccumulationType.Step ? peakNps : peakRps;

				var previousMeasureHighIndex = 0;
				var previousMeasureLowIndex = 0;
				var previousMeasureStepsPerSecond = 0.0;
				var previousPreviousMeasureStepsPerSecond = 0.0;
				var minX = RimW;
				var minY = RimW;
				var stepHeight = height - RimW * 2;
				var stepWidth = width - RimW * 2;
				for (var i = 0; i < measures.GetSize(); i++)
				{
					double measureTime;
					if (i + 1 < measures.GetSize())
						measureTime = measures[i + 1].StartTime - measures[i].StartTime;
					else
						measureTime = finalTime - measures[i].StartTime;
					var stepsPerSecond = 0.0;
					if (measures[i].Steps > 0 && !measureTime.DoubleEquals(0.0))
					{
						stepsPerSecond = accumulationMode == StepAccumulationType.Step
							? measures[i].Steps / measureTime
							: measures[i].RowsWithSteps / measureTime;
					}

					var yPercent = stepsPerSecond / peakSteps;
					var y = minY + (float)(yPercent * stepHeight);
					var x = minX + (float)(measures[i].StartTime / finalTime * stepWidth);

					// Special Case: No Steps.
					if (measures[i].Steps == 0)
					{
						// If the previous measure also had no steps there is nothing we need to do. No new triangles are needed.
						if (i == 0 || (i >= 1 && measures[i - 1].Steps == 0))
						{
							continue;
						}

						// If the previous measure had steps then we need one triangle to connect down to the bottom of the graph.
						vertices.Add(new VertexPositionColor(new Vector3(x, y, 0.0f), lowColor));
						indices.Add(previousMeasureLowIndex);
						indices.Add(previousMeasureHighIndex);
						indices.Add(vertices.GetSize() - 1);
						previousMeasureLowIndex = vertices.GetSize() - 1;
						previousMeasureHighIndex = vertices.GetSize() - 1;
						numPrimitives++;
						previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
						previousMeasureStepsPerSecond = stepsPerSecond;
						continue;
					}

					// Special Case: This measure has the same number of steps per second as the previous two measures and should extend the previous quad.
					if (i >= 2 && measures[i - 1].Steps != 0 && measures[i - 2].Steps != 0 &&
					    stepsPerSecond.DoubleEquals(previousMeasureStepsPerSecond, 0.0001) &&
					    stepsPerSecond.DoubleEquals(previousPreviousMeasureStepsPerSecond, 0.0001))
					{
						vertices[previousMeasureLowIndex] = new VertexPositionColor(
							new Vector3(x, vertices[previousMeasureLowIndex].Position.Y, 0.0f),
							vertices[previousMeasureLowIndex].Color);
						vertices[previousMeasureHighIndex] = new VertexPositionColor(
							new Vector3(x, vertices[previousMeasureHighIndex].Position.Y, 0.0f),
							vertices[previousMeasureHighIndex].Color);
						previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
						previousMeasureStepsPerSecond = stepsPerSecond;
						continue;
					}

					// Special Case: Previous measure has no steps.
					Color c;
					if (i == 0 || measures[i - 1].Steps == 0)
					{
						// We need to record two new vertices for this measure.
						c = ColorUtils.Interpolate(lowColor, highColor, (float)yPercent);
						vertices.Add(new VertexPositionColor(new Vector3(x, minY, 0.0f),
							colorMode == DensityGraphColorMode.ColorByDensity ? c : lowColor));
						previousMeasureLowIndex = vertices.GetSize() - 1;
						vertices.Add(new VertexPositionColor(new Vector3(x, y, 0.0f), c));
						previousMeasureHighIndex = vertices.GetSize() - 1;
						previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
						previousMeasureStepsPerSecond = stepsPerSecond;
						continue;
					}

					// Normal case: The previous measure had steps and this measure has a different number of steps.
					// Add two vertices and two triangles.
					c = ColorUtils.Interpolate(lowColor, highColor, (float)yPercent);
					vertices.Add(new VertexPositionColor(new Vector3(x, minY, 0.0f),
						colorMode == DensityGraphColorMode.ColorByDensity ? c : lowColor));
					vertices.Add(new VertexPositionColor(new Vector3(x, y, 0.0f), c));
					indices.Add(previousMeasureLowIndex);
					indices.Add(previousMeasureHighIndex);
					indices.Add(vertices.GetSize() - 2);
					indices.Add(previousMeasureHighIndex);
					indices.Add(vertices.GetSize() - 1);
					indices.Add(vertices.GetSize() - 2);
					numPrimitives += 2;
					previousMeasureLowIndex = vertices.GetSize() - 2;
					previousMeasureHighIndex = vertices.GetSize() - 1;
					previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
					previousMeasureStepsPerSecond = stepsPerSecond;
				}

				// If we made it to the end and there was a measure that ends with height, we need to 
				// add one more triangle to bring it down to 0.
				if (measures[measures.GetSize() - 1].Steps > 0)
				{
					vertices.Add(new VertexPositionColor(new Vector3(stepWidth, minY, 0.0f), lowColor));
					indices.Add(previousMeasureLowIndex);
					indices.Add(previousMeasureHighIndex);
					indices.Add(vertices.GetSize() - 1);
					numPrimitives++;
				}

				// Add the rim primitives.
				AddRim(vertices, indices, ref numPrimitives, width, height);

				// Save results.
				UpdateData(vertices, indices, numPrimitives, peakNps, peakRps);
			}
			catch (Exception)
			{
				// Ignored.
				// Logging here would likely result in log spam.
			}
		}
	}

	private static void AddBackground(
		DynamicArray<VertexPositionColor> vertices,
		DynamicArray<int> indices,
		ref int numPrimitives,
		float width,
		float height,
		ref Color backgroundColor)
	{
		const float backgroundZ = -1.0f;

		var rimIndexStart = vertices.GetSize();
		vertices.Add(new VertexPositionColor(new Vector3(0.0f, height, backgroundZ), backgroundColor));
		vertices.Add(new VertexPositionColor(new Vector3(0.0f, 0.0f, backgroundZ), backgroundColor));
		vertices.Add(new VertexPositionColor(new Vector3(width, 0.0f, backgroundZ), backgroundColor));
		vertices.Add(new VertexPositionColor(new Vector3(width, height, backgroundZ), backgroundColor));
		indices.Add(rimIndexStart);
		indices.Add(rimIndexStart + 2);
		indices.Add(rimIndexStart + 1);
		indices.Add(rimIndexStart + 0);
		indices.Add(rimIndexStart + 3);
		indices.Add(rimIndexStart + 2);
		numPrimitives += 2;
	}

	private static void AddRim(
		DynamicArray<VertexPositionColor> vertices,
		DynamicArray<int> indices,
		ref int numPrimitives,
		float width,
		float height)
	{
		const float rimZ = 2.0f;

		var rimIndexStart = vertices.GetSize();
		vertices.Add(new VertexPositionColor(new Vector3(0.0f, height, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(0.0f, 0.0f, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(RimW, 0.0f, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(RimW, height, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(RimW, height - RimW, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - RimW, height, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - RimW, height - RimW, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(RimW, RimW, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - RimW, RimW, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - RimW, 0.0f, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width, height, rimZ), RimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width, 0.0f, rimZ), RimColor));
		indices.Add(rimIndexStart + 1);
		indices.Add(rimIndexStart);
		indices.Add(rimIndexStart + 2);
		indices.Add(rimIndexStart);
		indices.Add(rimIndexStart + 3);
		indices.Add(rimIndexStart + 2);
		indices.Add(rimIndexStart + 3);
		indices.Add(rimIndexStart + 5);
		indices.Add(rimIndexStart + 6);
		indices.Add(rimIndexStart + 4);
		indices.Add(rimIndexStart + 3);
		indices.Add(rimIndexStart + 6);
		indices.Add(rimIndexStart + 2);
		indices.Add(rimIndexStart + 7);
		indices.Add(rimIndexStart + 9);
		indices.Add(rimIndexStart + 7);
		indices.Add(rimIndexStart + 8);
		indices.Add(rimIndexStart + 9);
		indices.Add(rimIndexStart + 9);
		indices.Add(rimIndexStart + 5);
		indices.Add(rimIndexStart + 11);
		indices.Add(rimIndexStart + 5);
		indices.Add(rimIndexStart + 10);
		indices.Add(rimIndexStart + 11);
		numPrimitives += 8;
	}

	/// <summary>
	/// Commit changes to data used for rendering.
	/// This will copy the given data.
	/// </summary>
	/// <param name="vertices">Vertex array.</param>
	/// <param name="indices">Index array.</param>
	/// <param name="numPrimitives">Number of primitives.</param>
	/// <param name="peakNps">Peak notes per second value.</param>
	/// <param name="peakRps">Peak rows per second value.</param>
	private void UpdateData(
		IReadOnlyDynamicArray<VertexPositionColor> vertices,
		IReadOnlyDynamicArray<int> indices,
		int numPrimitives,
		double peakNps,
		double peakRps)
	{
		lock (DataLock)
		{
			Vertices.CopyFrom(vertices);
			Indices.CopyFrom(indices);
			NumPrimitives = numPrimitives;
			PeakNps = peakNps;
			PeakRps = peakRps;
		}
	}

	/// <summary>
	/// Begin an update to the computed data.
	/// This will enqueue data to be processed on the update thread.
	/// </summary>
	private void RefreshData()
	{
		var p = Preferences.Instance.PreferencesDensityGraph;

		// Avoid doing unnecessary computations when we don't show the density graph.
		if (!p.ShowDensityGraph)
			return;

		var lowColor = new Color(p.DensityGraphLowColor);
		var highColor = new Color(p.DensityGraphHighColor);
		var backgroundColor = new Color(p.DensityGraphBackgroundColor);

		var w = Bounds.Width;
		var h = Bounds.Height;

		// For vertical orientation we still render horizontally but then rotate the view matrix
		// prior to rendering. This keeps the primitive generation logic simple.
		if (EffectOrientation == Orientation.Vertical)
		{
			w = Bounds.Height;
			h = Bounds.Width;
		}

		if (StepDensity == null)
		{
			State.EnqueueData(null, 0.0, w, h, lowColor, highColor, backgroundColor, p.DensityGraphColorModeValue,
				p.AccumulationType);
		}
		else
		{
			State.EnqueueData(StepDensity.GetMeasures(), StepDensity.GetLastMeasurePlusOneTime(), w, h, lowColor,
				highColor, backgroundColor, p.DensityGraphColorModeValue, p.AccumulationType);
		}
	}

	#endregion Primitive Generation

	#region IObserver

	public void OnNotify(string eventId, StepDensity notifier, object payload)
	{
		switch (eventId)
		{
			case NotificationMeasuresChanged:
				RefreshData();
				break;
		}
	}

	public void OnNotify(string eventId, PreferencesDensityGraph notifier, object payload)
	{
		switch (eventId)
		{
			case NotificationDensityGraphColorsChanged:
			case NotificationDensityGraphColorModeChanged:
			case NotificationShowDensityGraphChanged:
			case NotificationAccumulationTypeChanged:
				RefreshData();
				break;
		}
	}

	#endregion IObserver
}
