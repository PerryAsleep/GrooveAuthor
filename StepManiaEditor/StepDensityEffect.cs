using System;
using System.Threading;
using System.Threading.Tasks;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.StepDensity;
using static StepManiaEditor.PreferencesStream;
using ColorUtils = MonoGameExtensions.ColorUtils;

namespace StepManiaEditor;

/// <summary>
/// StepDensityEffect renders a density graph for an EditorChart's StepDensity.
/// Expected Usage:
///  Call SetStepDensity to update the StepDensity to render.
///  Call UpdateBounds to set the bounds of the effect.
///  Call Draw once each frame to render the effect.
///  Call ResetBufferCapacities to reset capacities for internal rendering buffers.
/// </summary>
internal sealed class StepDensityEffect : Fumen.IObserver<StepDensity>, Fumen.IObserver<PreferencesStream>, IDisposable
{
	private const int MinNumMeasures = 256;
	private const int MinNumVertices = 2048;
	private const int MinNumIndices = 6288;

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
	/// thread which processes measures and turns them into primitives.
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
			DensityGraphColorMode colorMode)
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
			ref DensityGraphColorMode colorMode)
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

	private readonly GraphicsDeviceManager Graphics;
	private readonly GraphicsDevice GraphicsDevice;
	private readonly BasicEffect DensityEffect;

	private readonly CreatePrimitivesState State = new();

	/// <summary>
	/// Lock for primitive data.
	/// </summary>
	private readonly object PrimitiveLock = new();

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
	/// Long-running task for updating primitive data.
	/// </summary>
	private readonly Task UpdatePrimitivesTask;

	/// <summary>
	/// Current StepDensity to render.
	/// </summary>
	private StepDensity StepDensity;

	private Rectangle Bounds;
	private Orientation EffectOrientation = Orientation.Horizontal;

	private bool Disposed;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="graphics">GraphicsDeviceManager to use for the effect.</param>
	/// <param name="graphicsDevice">GraphicsDevice to use for the effect.</param>
	public StepDensityEffect(GraphicsDeviceManager graphics, GraphicsDevice graphicsDevice)
	{
		// Set up the Effect for rendering.
		Graphics = graphics;
		GraphicsDevice = graphicsDevice;
		DensityEffect = new BasicEffect(GraphicsDevice);
		DensityEffect.VertexColorEnabled = true;
		DensityEffect.World = Matrix.Identity;

		// Observe relevant preferences so the effect can be updated accordingly.
		Preferences.Instance.PreferencesStream.AddObserver(this);

		// Start a long running task to process updates to primitives.
		UpdatePrimitivesTask = Task.Factory.StartNew(
			UpdatePrimitives,
			CancellationToken.None,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);
	}

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
			Preferences.Instance.PreferencesStream.RemoveObserver(this);
			StepDensity?.RemoveObserver(this);
			State.SetShouldShutdown();
			UpdatePrimitivesTask.Wait();
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
		RefreshPrimitives();
	}

	/// <summary>
	/// Reset buffer capacities to default values.
	/// Useful when unloading one (large) song and loading another.
	/// </summary>
	public void ResetBufferCapacities()
	{
		State.ResetCapacities();
		lock (PrimitiveLock)
		{
			Vertices.UpdateCapacity(MinNumVertices);
			Indices.UpdateCapacity(MinNumIndices);
		}
	}

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
		RefreshPrimitives();
	}

	/// <summary>
	/// Draw the density graph.
	/// </summary>
	public void Draw()
	{
		if (!Preferences.Instance.PreferencesStream.ShowDensityGraph)
			return;

		var viewportW = Graphics.PreferredBackBufferWidth;
		var viewportH = Graphics.PreferredBackBufferHeight;
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

		lock (PrimitiveLock)
		{
			if (NumPrimitives == 0)
				return;

			foreach (var pass in DensityEffect.CurrentTechnique.Passes)
			{
				pass.Apply();
				GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, Vertices.GetArray(), 0, Vertices.GetSize(),
					Indices.GetArray(), 0, NumPrimitives);
			}
		}

		// Draw primitives for current time visible area and current time line.
	}

	#region Primitive Generation

	/// <summary>
	/// Long-running task to update the primitives used for rendering.
	/// </summary>
	private async void UpdatePrimitives()
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
		const float rimW = 1.0f;

		// Loop continuously, yielding when there is no work.
		while (true)
		{
			// Check for work.
			while (true)
			{
				// Return if we should be shutting down.
				if (State.ShouldShutdown())
					return;

				// Try to pop any enqueued data. If there is data to process then break out and process it below.
				if (State.TryPopEnqueuedData(ref measures, ref vertices, ref indices, ref finalTime, ref width, ref height,
					    ref lowColor, ref highColor, ref backgroundColor, ref colorMode))
					break;
				await Task.Yield();
			}

			// Begin processing new data.
			var numPrimitives = 0;

			// Early out on invalid bounds.
			if (height < 0.0f || width < 0.0f)
			{
				UpdatePrimitives(vertices, indices, numPrimitives);
				continue;
			}

			// Add the background primitives.
			AddBackground(vertices, indices, ref numPrimitives, width, height, ref backgroundColor);

			// Early out due to no measures or not enough area to render measures
			if (measures.GetSize() == 0 || height <= rimW * 2 || width <= rimW * 2)
			{
				// Add the rim primitives.
				AddRim(vertices, indices, ref numPrimitives, rimW, width, height);

				UpdatePrimitives(vertices, indices, numPrimitives);
				continue;
			}

			// Determine the greatest number of steps per measure.
			var greatestStepsPerSecond = 0.0;
			for (var i = 0; i < measures.GetSize(); i++)
			{
				double measureTime;
				if (i + 1 < measures.GetSize())
					measureTime = measures[i + 1].StartTime - measures[i].StartTime;
				else
					measureTime = finalTime - measures[i].StartTime;
				greatestStepsPerSecond = Math.Max(measures[i].Steps / measureTime, greatestStepsPerSecond);
			}

			var previousMeasureHighIndex = 0;
			var previousMeasureLowIndex = 0;
			var previousMeasureHasVerticesWithNoTriangle = false;
			var previousMeasureStepsPerSecond = 0.0;
			var previousPreviousMeasureStepsPerSecond = 0.0;
			var minX = rimW;
			var minY = rimW;
			var stepHeight = height - rimW * 2;
			var stepWidth = width - rimW * 2;
			for (var i = 0; i < measures.GetSize(); i++)
			{
				double measureTime;
				if (i + 1 < measures.GetSize())
					measureTime = measures[i + 1].StartTime - measures[i].StartTime;
				else
					measureTime = finalTime - measures[i].StartTime;
				var stepsPerSecond = measures[i].Steps / measureTime;

				var yPercent = (float)(stepsPerSecond / greatestStepsPerSecond);
				var y = minY + yPercent * stepHeight;
				var x = minX + (float)(measures[i].StartTime / finalTime) * stepWidth;

				// Special Case: No Steps.
				if (measures[i].Steps == 0)
				{
					// If the previous measure also had no steps there is nothing we need to do. No new triangles are needed.
					if (i == 0 || (i >= 1 && measures[i - 1].Steps == 0))
					{
						previousMeasureHasVerticesWithNoTriangle = false;
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
					previousMeasureHasVerticesWithNoTriangle = false;
					previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
					previousMeasureStepsPerSecond = stepsPerSecond;
					continue;
				}

				// Special Case: This measure has the same number of steps per second as the previous two measures and should extend the previous quad.
				if (i >= 2 && stepsPerSecond.DoubleEquals(previousMeasureStepsPerSecond, 0.0001) &&
				    stepsPerSecond.DoubleEquals(previousPreviousMeasureStepsPerSecond, 0.0001))
				{
					vertices[previousMeasureLowIndex] = new VertexPositionColor(
						new Vector3(x, vertices[previousMeasureLowIndex].Position.Y, 0.0f),
						vertices[previousMeasureLowIndex].Color);
					vertices[previousMeasureHighIndex] = new VertexPositionColor(
						new Vector3(x, vertices[previousMeasureHighIndex].Position.Y, 0.0f),
						vertices[previousMeasureHighIndex].Color);
					previousMeasureHasVerticesWithNoTriangle = false;
					previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
					previousMeasureStepsPerSecond = stepsPerSecond;
					continue;
				}

				// Special Case: Previous measure has no steps.
				Color c;
				if (i == 0 || measures[i - 1].Steps == 0)
				{
					// We need to record two new vertices for this measure.
					c = ColorUtils.Interpolate(lowColor, highColor, yPercent);
					vertices.Add(new VertexPositionColor(new Vector3(x, minY, 0.0f),
						colorMode == DensityGraphColorMode.ColorByDensity ? c : lowColor));
					previousMeasureLowIndex = vertices.GetSize() - 1;
					vertices.Add(new VertexPositionColor(new Vector3(x, y, 0.0f), c));
					previousMeasureHighIndex = vertices.GetSize() - 1;
					previousMeasureHasVerticesWithNoTriangle = true;
					previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
					previousMeasureStepsPerSecond = stepsPerSecond;
					continue;
				}

				// Normal case: The previous measure had steps and this measure has a different number of steps.
				// Add two vertices and two triangles.
				c = ColorUtils.Interpolate(lowColor, highColor, yPercent);
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
				previousMeasureHasVerticesWithNoTriangle = false;
				previousPreviousMeasureStepsPerSecond = previousMeasureStepsPerSecond;
				previousMeasureStepsPerSecond = stepsPerSecond;
			}

			// If we made it to the end and there was a measure with unfinished vertices then add one more triangle.
			if (previousMeasureHasVerticesWithNoTriangle)
			{
				vertices.Add(new VertexPositionColor(new Vector3(stepWidth, minY, 0.0f), lowColor));
				indices.Add(previousMeasureLowIndex);
				indices.Add(previousMeasureHighIndex);
				indices.Add(vertices.GetSize() - 1);
				numPrimitives++;
			}

			// Add the rim primitives.
			AddRim(vertices, indices, ref numPrimitives, rimW, width, height);

			// Save results.
			UpdatePrimitives(vertices, indices, numPrimitives);
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
		float rimW,
		float width,
		float height)
	{
		const float rimZ = 2.0f;
		var rimColor = Color.White;

		var rimIndexStart = vertices.GetSize();
		vertices.Add(new VertexPositionColor(new Vector3(0.0f, height, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(0.0f, 0.0f, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(rimW, 0.0f, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(rimW, height, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(rimW, height - rimW, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - rimW, height, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - rimW, height - rimW, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(rimW, rimW, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - rimW, rimW, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width - rimW, 0.0f, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width, height, rimZ), rimColor));
		vertices.Add(new VertexPositionColor(new Vector3(width, 0.0f, rimZ), rimColor));
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
	/// Commit changes to primitives used for rendering.
	/// This will copy the given data.
	/// </summary>
	/// <param name="vertices">Vertex array.</param>
	/// <param name="indices">Index array.</param>
	/// <param name="numPrimitives">Number of primitives.</param>
	private void UpdatePrimitives(
		IReadOnlyDynamicArray<VertexPositionColor> vertices,
		IReadOnlyDynamicArray<int> indices,
		int numPrimitives)
	{
		lock (PrimitiveLock)
		{
			Vertices.CopyFrom(vertices);
			Indices.CopyFrom(indices);
			NumPrimitives = numPrimitives;
		}
	}

	/// <summary>
	/// Begin an update to the primitives.
	/// This will enqueue data to be processed on the update thread.
	/// </summary>
	private void RefreshPrimitives()
	{
		var p = Preferences.Instance.PreferencesStream;

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
			State.EnqueueData(null, 0.0, w, h, lowColor, highColor, backgroundColor, p.DensityGraphColorModeValue);
		}
		else
		{
			State.EnqueueData(StepDensity.GetMeasures(), StepDensity.GetLastMeasurePlusOneTime(), w, h, lowColor,
				highColor, backgroundColor, p.DensityGraphColorModeValue);
		}
	}

	#endregion Primitive Generation

	#region IObserver

	public void OnNotify(string eventId, StepDensity notifier, object payload)
	{
		switch (eventId)
		{
			case NotificationMeasuresChanged:
				RefreshPrimitives();
				break;
		}
	}

	public void OnNotify(string eventId, PreferencesStream notifier, object payload)
	{
		switch (eventId)
		{
			case NotificationDensityGraphColorsChanged:
			case NotificationDensityGraphColorModeChanged:
			case NotificationShowDensityGraphChanged:
				RefreshPrimitives();
				break;
		}
	}

	#endregion IObserver
}
