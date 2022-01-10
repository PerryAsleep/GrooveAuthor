using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FMOD;
using Fumen.ChartDefinition;
using Fumen.Converters;
using StepManiaLibrary;
using static StepManiaLibrary.Constants;
using static StepManiaEditor.Utils;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace StepManiaEditor
{
	public class Editor : Game
	{
		/// <summary>
		/// How to control the position of the Chart when scrolling.
		/// </summary>
		public enum ScrollMode
		{
			/// <summary>
			/// Scrolling moves time.
			/// This is the mode used playing the song.
			/// </summary>
			Time,
			/// <summary>
			/// Scrolling moves rows.
			/// </summary>
			Row,
		}

		/// <summary>
		/// How to space Chart Events when rendering.
		/// </summary>
		public enum SpacingMode
		{
			/// <summary>
			/// Spacing between notes is based on time.
			/// Using a Time ScrollMode with a ConstantTime SpacingMode is effectively a CMOD.
			/// </summary>
			ConstantTime,
			/// <summary>
			/// Spacing between notes is based on row.
			/// </summary>
			ConstantRow,
			/// <summary>
			/// Spacing between nodes varies based on rate altering Events in the Chart.
			/// Using TimeBased ScrollMode with a Variable SpacingMode is effectively an XMOD.
			/// </summary>
			Variable,
		}

		private Vector2 FocalPoint;

		private const int WaveFormTextureWidth = DefaultArrowWidth * 8;

		private string PendingOpenFileName;
		private string PendingOpenFileChartType;
		private string PendingOpenFileChartDifficultyType;
		private string PendingMusicFile;

		private GraphicsDeviceManager Graphics;
		private SpriteBatch SpriteBatch;
		private ImGuiRenderer ImGuiRenderer;
		private WaveFormRenderer WaveFormRenderer;
		private SoundManager SoundManager;
		private SoundMipMap MusicMipMap;

		private TextureAtlas TextureAtlas;

		private Dictionary<string, PadData> PadDataByStepsType = new Dictionary<string, PadData>();
		private Dictionary<string, StepGraph> StepGraphByStepsType = new Dictionary<string, StepGraph>();
		private string SongDirectory;
		private string SongMipMapFile;
		private CancellationTokenSource LoadSongCancellationTokenSource;
		private Task LoadSongTask;
		private Sound Music;
		private int MusicNumChannels;
		private int MusicBitsPerSample;
		private uint MusicTotalBytes;
		private uint MusicSampleRate;
		public ChannelGroup MusicChannelGroup;
		public Channel MusicChannel;
		private CancellationTokenSource LoadMusicCancellationTokenSource;
		private Task LoadMusicTask;

		private double PlaybackStartTime;
		private Stopwatch PlaybackStopwatch;

		private Song Song;
		private Chart ActiveChart;
		private double ChartPosition = 0.0;
		private RedBlackTree<EditorEvent> EditorEvents;
		
		private RedBlackTree<EditorRateAlteringEvent> RateAlteringEventsBySongTime;
		private RedBlackTree<EditorRateAlteringEvent> RateAlteringEventsByRow;
		private RedBlackTree<EditorInterpolatedRateAlteringEvent> InterpolatedScrollRateEvents;
		
		private List<EditorEvent> VisibleEvents = new List<EditorEvent>();

		// temp controls
		private int MouseScrollValue = 0;
		private double SongTimeInterpolationTimeStart = 0.0;
		private double SongTime = 0.0;
		private double PreviousFrameSongTime = 0.0;
		private double SongTimeAtStartOfInterpolation = 0.0;
		private double DesiredSongTime = 0.0;
		private double ZoomInterpolationTimeStart = 0.0;
		private double Zoom = 1.0;
		private double ZoomAtStartOfInterpolation = 1.0;
		private double DesiredZoom = 1.0;

		private const int QuarterNoteSeparationAtDefaultZoom = 128;

		private bool SpacePressed = false;
		private bool Playing = false;

		private uint MaxScreenHeight;

		private ImFontPtr Font;

		// Logger GUI
		private readonly LinkedList<Logger.LogMessage> LogBuffer = new LinkedList<Logger.LogMessage>();
		private readonly object LogBufferLock = new object();
		private readonly string[] LogWindowDateStrings = { "None", "HH:mm:ss", "HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss.fff" };

		private readonly System.Numerics.Vector4[] LogWindowLevelColors =
		{
			new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f),
		};

		// WaveForm GUI
		private readonly string[] WaveFormWindowSparseColorOptions =
			{"Darker Dense Color", "Same As Dense Color", "Unique Color"};



		private bool ShowImGuiTestWindow = true;

		public Editor()
		{
			var logFileName = "StepManiaChartEditor " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".log";
			Logger.StartUp(new Logger.Config
			{
				WriteToConsole = false,

				WriteToFile = true,
				LogFilePath = Path.Combine(@"C:\Users\perry\Projects\Fumen\Logs", logFileName),
				LogFileFlushIntervalSeconds = 20,
				LogFileBufferSizeBytes = 10240,

				WriteToBuffer = true,
				BufferSize = 1024,
				BufferLock = LogBufferLock,
				Buffer = LogBuffer
			});

			// Load Preferences synchronously so they can be used immediately.
			Preferences.Load();

			FocalPoint = new Vector2(Preferences.Instance.WindowWidth >> 1, 100 + (DefaultArrowWidth >> 1));

			SoundManager = new SoundManager();
			SoundManager.CreateChannelGroup("MusicChannelGroup", out MusicChannelGroup);
			MusicMipMap = new SoundMipMap(SoundManager);
			MusicMipMap.SetLoadParallelism(Preferences.Instance.WaveFormLoadingMaxParallelism);

			Graphics = new GraphicsDeviceManager(this);

			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			Window.AllowUserResizing = true;
			Window.ClientSizeChanged += OnResize;

			IsFixedTimeStep = false;
			Graphics.SynchronizeWithVerticalRetrace = true;
		}

		protected override void Initialize()
		{
			Graphics.PreferredBackBufferHeight = Preferences.Instance.WindowHeight;
			Graphics.PreferredBackBufferWidth = Preferences.Instance.WindowWidth;
			Graphics.IsFullScreen = Preferences.Instance.WindowFullScreen;
			Graphics.ApplyChanges();

			if (Preferences.Instance.WindowMaximized)
			{
				((Form)Control.FromHandle(Window.Handle)).WindowState = FormWindowState.Maximized;
			}

			ImGuiRenderer = new ImGuiRenderer(this);
			// TODO: Load font from install directory
			Font = ImGui.GetIO().Fonts.AddFontFromFileTTF(
				@"C:\Users\perry\Projects\Fumen\StepManiaEditor\Mplus1Code-Medium.ttf",
				15,
				null,
				ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
			ImGuiRenderer.RebuildFontAtlas();

			foreach (var adapter in GraphicsAdapter.Adapters)
			{
				MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
			}

			WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, MaxScreenHeight);
			WaveFormRenderer.SetXPerChannelScale(Preferences.Instance.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetSoundMipMap(MusicMipMap);
			WaveFormRenderer.SetFocalPoint(FocalPoint);

			TextureAtlas = new TextureAtlas(GraphicsDevice, 2048, 2048, 1);

			base.Initialize();
		}

		protected override void EndRun()
		{
			Preferences.Save();
			Logger.Shutdown();
			base.EndRun();
		}

		protected override void LoadContent()
		{
			SpriteBatch = new SpriteBatch(GraphicsDevice);

			// Load textures from disk and add them to the Texture Atlas.
			foreach (var kvp in ArrowTextureBySubdivision)
				TextureAtlas.AddTexture(kvp.Value, Content.Load<Texture2D>(kvp.Value));
			TextureAtlas.AddTexture(TextureIdMine, Content.Load<Texture2D>(TextureIdMine));
			TextureAtlas.AddTexture(TextureIdReceptor, Content.Load<Texture2D>(TextureIdReceptor));
			TextureAtlas.AddTexture(TextureIdReceptorFlash, Content.Load<Texture2D>(TextureIdReceptorFlash));
			TextureAtlas.AddTexture(TextureIdReceptorGlow, Content.Load<Texture2D>(TextureIdReceptorGlow));
			TextureAtlas.AddTexture(TextureIdHoldActive, Content.Load<Texture2D>(TextureIdHoldActive));
			TextureAtlas.AddTexture(TextureIdHoldActiveCap, Content.Load<Texture2D>(TextureIdHoldActiveCap));
			TextureAtlas.AddTexture(TextureIdHoldInactive, Content.Load<Texture2D>(TextureIdHoldInactive));
			TextureAtlas.AddTexture(TextureIdHoldInactiveCap, Content.Load<Texture2D>(TextureIdHoldInactiveCap));
			TextureAtlas.AddTexture(TextureIdRollActive, Content.Load<Texture2D>(TextureIdRollActive));
			TextureAtlas.AddTexture(TextureIdRollActiveCap, Content.Load<Texture2D>(TextureIdRollActiveCap));
			TextureAtlas.AddTexture(TextureIdRollInactive, Content.Load<Texture2D>(TextureIdRollInactive));
			TextureAtlas.AddTexture(TextureIdRollInactiveCap, Content.Load<Texture2D>(TextureIdRollInactiveCap));

			// TODO: Add textures for measure markers.

			InitPadDataAndStepGraphsAsync();

			// If we have a saved file to open, open it now.
			if (Preferences.Instance.OpenLastOpenedFileOnLaunch
				&& Preferences.Instance.RecentFiles.Count > 0)
			{
				OpenSongFileAsync(Preferences.Instance.RecentFiles[0].FileName,
					Preferences.Instance.RecentFiles[0].LastChartType,
					Preferences.Instance.RecentFiles[0].LastChartDifficultyType);
			}

			base.LoadContent();
		}

		public void OnResize(object sender, EventArgs e)
		{
			var maximized = ((Form)Control.FromHandle(Window.Handle)).WindowState == FormWindowState.Maximized;

			// Update window preferences.
			if (!maximized)
			{
				Preferences.Instance.WindowWidth = Graphics.GraphicsDevice.Viewport.Width;
				Preferences.Instance.WindowHeight = Graphics.GraphicsDevice.Viewport.Height;
			}

			Preferences.Instance.WindowFullScreen = Graphics.IsFullScreen;
			Preferences.Instance.WindowMaximized = maximized;

			// Update FocalPoint.
			FocalPoint.X = Graphics.GraphicsDevice.Viewport.Width >> 1;
		}

		protected override void Update(GameTime gameTime)
		{
			PreviousFrameSongTime = SongTime;

			ProcessInput(gameTime);

			TextureAtlas.Update();
			
			if (!Playing)
			{
				if (SongTime != DesiredSongTime)
				{
					SongTime = Interpolation.Lerp(
						SongTimeAtStartOfInterpolation,
						DesiredSongTime,
						SongTimeInterpolationTimeStart,
						SongTimeInterpolationTimeStart + 0.1,
						gameTime.TotalGameTime.TotalSeconds);

					SetMusicPosition(SongTime);
				}
			}

			if (Zoom != DesiredZoom)
			{
				Zoom = Interpolation.Lerp(
					ZoomAtStartOfInterpolation,
					DesiredZoom,
					ZoomInterpolationTimeStart,
					ZoomInterpolationTimeStart + 0.1,
					gameTime.TotalGameTime.TotalSeconds);
			}

			if (Playing)
			{
				SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
				MusicChannel.setPaused(SongTime < 0.0);

				// The goal is to set the SongTime to match the actual time of the music
				// being played through FMOD. Querying the time from FMOD does not have high
				// enough precision, so we need to use our own timer.
				// The best C# timer for this task is a StopWatch, but StopWatches have been
				// reported to drift, sometimes up to a half a second per hour. If we detect
				// it has drifted significantly by comparing it to the time from FMOD, then
				// snap it back.
				var maxDeviation = 0.1;
				var fmodSongTime = GetMusicPosition();
				if (SongTime >= 0.0 && SongTime < GetMusicLength())
				{
					if (SongTime - fmodSongTime > maxDeviation)
					{
						PlaybackStartTime -= (0.5 * maxDeviation);
						SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
					}
					else if (fmodSongTime - SongTime > maxDeviation)
					{
						PlaybackStartTime += (0.5 * maxDeviation);
						SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
					}
				}

				DesiredSongTime = SongTime;
			}

			// If using SongTime
			UpdateChartPositionFromSongTime();
			UpdateChartEvents();
			UpdateWaveFormRenderer();

			base.Update(gameTime);
		}

		private void ProcessInput(GameTime gameTime)
		{
			// Let imGui process input so we can see if we should ignore it.
			(bool imGuiWantMouse, bool imGuiWantKeyboard) = ImGuiRenderer.Update(gameTime);

			// Process Keyboard Input
			var scrollShouldZoom = false;
			if (!imGuiWantKeyboard)
			{
				var bHoldingCtrl = Keyboard.GetState().IsKeyDown(Keys.LeftControl);
				scrollShouldZoom = bHoldingCtrl;

				var bHoldingSpace = Keyboard.GetState().IsKeyDown(Keys.Space);
				if (!SpacePressed && bHoldingSpace)
				{
					if (Playing)
						StopPlayback();
					else
						StartPlayback();
				}

				SpacePressed = bHoldingSpace;
			}

			// Process Mouse Input
			var mouseState = Mouse.GetState();
			var newMouseScrollValue = mouseState.ScrollWheelValue;
			if (imGuiWantMouse)
			{
				// Update our last tracked mouse scroll value even if imGui captured it so that
				// once we start capturing input again we don't process a scroll.
				MouseScrollValue = newMouseScrollValue;
			}
			else
			{
				// TODO: wtf are these values
				if (scrollShouldZoom)
				{
					if (MouseScrollValue < newMouseScrollValue)
					{
						DesiredZoom *= 1.2;
						ZoomInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
						ZoomAtStartOfInterpolation = Zoom;
						MouseScrollValue = newMouseScrollValue;
					}

					if (MouseScrollValue > newMouseScrollValue)
					{
						DesiredZoom /= 1.2;
						ZoomInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
						ZoomAtStartOfInterpolation = Zoom;
						MouseScrollValue = newMouseScrollValue;
					}
				}
				else
				{
					if (MouseScrollValue < newMouseScrollValue)
					{
						var delta = 0.25 * (1.0 / Zoom);
						if (Playing)
						{
							PlaybackStartTime -= delta;
							SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
							SetMusicPosition(SongTime);
							MusicChannel.setPaused(SongTime < 0.0);
						}
						else
						{
							DesiredSongTime -= delta;
							SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
							SongTimeAtStartOfInterpolation = SongTime;
						}

						MouseScrollValue = newMouseScrollValue;
					}

					if (MouseScrollValue > newMouseScrollValue)
					{
						var delta = 0.25 * (1.0 / Zoom);
						if (Playing)
						{
							PlaybackStartTime += delta;
							SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
							SetMusicPosition(SongTime);
							MusicChannel.setPaused(SongTime < 0.0);
						}
						else
						{
							DesiredSongTime += delta;
							SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
							SongTimeAtStartOfInterpolation = SongTime;
						}

						MouseScrollValue = newMouseScrollValue;
					}
				}
			}
		}

		private void StartPlayback()
		{
			if (Playing)
				return;

			if (SongTime < 0.0 || SongTime > GetMusicLength())
			{
				PlaybackStartTime = SongTime;
			}
			else
			{
				PlaybackStartTime = GetMusicPosition();
			}

			PlaybackStopwatch = new Stopwatch();
			PlaybackStopwatch.Start();
			MusicChannel.setPaused(SongTime < 0.0);

			Playing = true;
		}

		private void StopPlayback()
		{
			if (!Playing)
				return;

			PlaybackStopwatch.Stop();
			MusicChannel.setPaused(true);

			Playing = false;
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.Black);

			SpriteBatch.Begin();
			if (Preferences.Instance.ShowWaveForm)
			{
				WaveFormRenderer.Draw(SpriteBatch);
			}

			DrawReceptors();
			DrawChartEvents();
			SpriteBatch.End();

			DrawGui(gameTime);

			base.Draw(gameTime);
		}

		private void DrawReceptors()
		{
			if (ActiveChart == null)
				return;

			var numArrows = ActiveChart.NumInputs;
			var zoom = Zoom;
			if (zoom > 1.0)
				zoom = 1.0;
			var arrowSize = DefaultArrowWidth * zoom;
			var xStart = FocalPoint.X - (numArrows * arrowSize * 0.5);
			var y = FocalPoint.Y - (arrowSize * 0.5);

			var rot = new[] { (float)Math.PI * 0.5f, 0.0f, (float)Math.PI, (float)Math.PI * 1.5f };
			for (var i = 0; i < numArrows; i++)
			{
				var x = xStart + i * arrowSize;
				TextureAtlas.Draw(TextureIdReceptor, SpriteBatch, new Rectangle((int)x, (int)y, (int)arrowSize, (int)arrowSize),
					rot[i % rot.Length]);
			}
		}

		private void UpdateWaveFormRenderer()
		{
			WaveFormRenderer.SetFocalPoint(FocalPoint);
			WaveFormRenderer.SetXPerChannelScale(Preferences.Instance.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetColors(
				Preferences.Instance.WaveFormDenseColor.X, Preferences.Instance.WaveFormDenseColor.Y,
				Preferences.Instance.WaveFormDenseColor.Z,
				Preferences.Instance.WaveFormSparseColor.X, Preferences.Instance.WaveFormSparseColor.Y,
				Preferences.Instance.WaveFormSparseColor.Z);
			WaveFormRenderer.SetScaleXWhenZooming(Preferences.Instance.WaveFormScaleXWhenZooming);

			var waveFormPPS = 0.0;
			switch (Preferences.Instance.SpacingMode)
			{
				case SpacingMode.ConstantTime:
				{
					waveFormPPS = Preferences.Instance.TimeBasedPixelsPerSecond;
					break;
				}
				case SpacingMode.ConstantRow:
				case SpacingMode.Variable:
				{
					// TODO
					waveFormPPS = Preferences.Instance.RowBasedPixelsPerSecondAtDefaultBPM;
					break;
				}
			}
			WaveFormRenderer.Update(SongTime, Zoom, waveFormPPS);
		}

		private void UpdateChartEvents()
		{
			// TODO: Cleanup

			VisibleEvents.Clear();

			if (EditorEvents == null)
				return;

			var screenHeight = Graphics.GraphicsDevice.Viewport.Height;

			var spacingZoom = Zoom;
			var sizeZoom = Zoom;
			if (sizeZoom > 1.0)
				sizeZoom = 1.0;
			var arrowSize = DefaultArrowWidth * sizeZoom;
			var holdCapHeight = DefaultHoldCapHeight * sizeZoom;

			var numArrows = ActiveChart.NumInputs;
			var xStart = FocalPoint.X - (numArrows * arrowSize * 0.5);
			
			switch (Preferences.Instance.SpacingMode)
			{
				case SpacingMode.ConstantTime:
				{
					var pps = Preferences.Instance.TimeBasedPixelsPerSecond * spacingZoom;
					var time = SongTime + ActiveChart.ChartOffsetFromMusic;
					var timeAtTopOfScreen = time - (FocalPoint.Y / pps);
					double chartPosition = 0.0;
					if (!TryGetChartPositionFromSongTime(timeAtTopOfScreen, ref chartPosition))
						return;

					var pos = EditorEvent.CreateEvent(new LaneTapNote { IntegerPosition = (int)chartPosition });
					var enumerator = EditorEvents.FindGreatestPreceding(pos);
					if (enumerator == null)
						enumerator = EditorEvents.FindLeastFollowing(pos);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var lanesChecked = new bool[ActiveChart.NumInputs];
					var numLanesChecked = 0;
					var current = new RedBlackTree<EditorEvent>.Enumerator(enumerator);
					while (current.MovePrev() && numLanesChecked < ActiveChart.NumInputs)
					{
						var e = current.Current;
						var lane = e.GetLane();
						if (lane >= 0)
						{
							if (!lanesChecked[lane])
							{
								lanesChecked[lane] = true;
								numLanesChecked++;

								if (e.GetRow() + e.GetLength() > pos.GetRow())
								{
									if (e is EditorHoldStartNote hsn)
									{
										e.X = xStart + e.GetLane() * arrowSize;
										e.Y = ((e.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps - (arrowSize * 0.5);
										e.W = arrowSize;
										e.H = arrowSize;
										e.Scale = sizeZoom;
										VisibleEvents.Add(e);

										var end = hsn.GetHoldEndNote();
										VisibleEvents.Add(end);
										end.X = xStart + end.GetLane() * arrowSize;
										end.Y = ((e.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps;
										end.W = arrowSize;
										end.H = (((end.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps) - end.Y + holdCapHeight;
										end.Scale = sizeZoom;
									}
								}
							}
						}
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						var y = ((e.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps - (arrowSize * 0.5);
						if (y > screenHeight)
							break;

						if (e is EditorHoldEndNote)
							continue;

						if (e is EditorTapNote
							|| e is EditorMineNote
						    || e is EditorHoldStartNote)
						{
							e.X = xStart + e.GetLane() * arrowSize;
							e.Y = y;
							e.W = arrowSize;
							e.H = arrowSize;
							e.Scale = sizeZoom;

							if (e is EditorHoldStartNote hsn)
							{
								var end = hsn.GetHoldEndNote();
								VisibleEvents.Add(end);
								end.X = xStart + end.GetLane() * arrowSize;
								end.Y = ((e.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps;
								end.W = arrowSize;
								end.H = (((end.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps) - end.Y + holdCapHeight;
								end.Scale = sizeZoom;
							}
						}

						VisibleEvents.Add(e);
					}

					break;
				}
					
				case SpacingMode.ConstantRow:
				{
					break;
				}
				case SpacingMode.Variable:
				{
					// Negative Scrolls.
					// If a chart has negative scrolls then we may need to render notes which come before
					// the chart position at the top of the screen.
					// More likely the most visible problem will be at the bottom of the screen where if we
					// were to detect the first note which falls below the bottom it would prevent us from
					// finding the next set of notes which might need to be rendered because they appear 
					// above.

					List<EditorEvent> holdBodyEvents = new List<EditorEvent>();
					List<EditorEvent> noteEvents = new List<EditorEvent>();

					var time = SongTime + ActiveChart.ChartOffsetFromMusic;
					double chartPosition = 0.0;
					if (!TryGetChartPositionFromSongTime(time, ref chartPosition))
						return;
					var ratePosEventForChecking = new EditorInterpolatedRateAlteringEvent
					{
						Row = chartPosition,
						SongTime = time
					};

					// Find the interpolated scroll rate to use as a multiplier.
					// The interpolated scroll rate to use is the value at the current exact time.
					var interpolatedScrollRate = 1.0;
					var interpolatedScrollRateEnumerator = InterpolatedScrollRateEvents.FindGreatestPreceding(ratePosEventForChecking);
					if (interpolatedScrollRateEnumerator != null)
					{
						interpolatedScrollRateEnumerator.MoveNext();
						var interpolatedRateEvent = interpolatedScrollRateEnumerator.Current;
						if (interpolatedRateEvent.InterpolatesByTime())
							interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromTime(time);
						else
							interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromRow(chartPosition);
					}

					// Now, scroll up to the top of the screen so we can start processing events going downwards.
					// We know what time / pos we are drawing at the receptors, but not the rate to get to that time from the top
					// of the screen.
					// We need to find the greatest preceding rate event, and continue until it is beyond the start of the screen.
					// Then we need to find the greatest preceding notes by scanning upwards.
					// Once we find that note, we start iterating downwards while also keeping track of the rate events along the way.

					// Need to be careful with 0 scroll rates. We need to put a cap in: max notes to render above and max below.

					var noteForChecking = new EditorRateAlteringEvent
					{
						Row = chartPosition,
						SongTime = time
					};
					var rateEnumerator = RateAlteringEventsByRow.FindGreatestPreceding(noteForChecking);
					if (rateEnumerator == null)
						rateEnumerator = RateAlteringEventsByRow.FindLeastFollowing(noteForChecking);
					if (rateEnumerator == null)
						return;

					// Scan upwards to find the earliest rate altering event that should be used to start rendering.
					var previousRateEventPixelPosition = (double)FocalPoint.Y;
					var previousRateEventRow = chartPosition;
					var pps = 1.0;
					var ppr = 1.0;
					EditorRateAlteringEvent rateEvent = null;
					while(previousRateEventPixelPosition > 0.0 && rateEnumerator.MovePrev())
					{
						rateEvent = rateEnumerator.Current;
						var scrollRateForThisSection = rateEvent.ScrollRate * interpolatedScrollRate;
						pps = Preferences.Instance.RowBasedPixelsPerSecondAtDefaultBPM
						          * (rateEvent.Tempo / Preferences.Instance.RowBasedDefaultBPM)
						          * scrollRateForThisSection
						          * spacingZoom;
						ppr = pps * rateEvent.SecondsPerRow;
						var rateEventPositionInPixels = previousRateEventPixelPosition - ((previousRateEventRow - rateEvent.Row) * ppr);
						previousRateEventPixelPosition = rateEventPositionInPixels;
						previousRateEventRow = rateEvent.Row;
					};

					// Now we know the position of first rate altering event to use.
					// We can now determine the chart position at the top of the screen
					var pixelPositionAtTopOfScreen = 0.0;
					var chartPositionAtTopOfScreen =
						rateEvent.Row + (pixelPositionAtTopOfScreen - previousRateEventPixelPosition) *
						rateEvent.RowsPerSecond / pps;

					// Now that we know the position at the start of the screen we can find the first event to start rendering.
					var pos = EditorEvent.CreateEvent(new LaneTapNote { IntegerPosition = (int)chartPositionAtTopOfScreen });
					var enumerator = EditorEvents.FindGreatestPreceding(pos);
					if (enumerator == null)
						enumerator = EditorEvents.FindLeastFollowing(pos);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row. We cannot add the end events yet because
					// we do not know at what position they will end until we scan down.
					var lanesChecked = new bool[ActiveChart.NumInputs];
					var numLanesChecked = 0;
					var current = new RedBlackTree<EditorEvent>.Enumerator(enumerator);
					var holdEndNotesNeedingToBeAdded = new EditorHoldEndNote[ActiveChart.NumInputs];
					while (current.MovePrev() && numLanesChecked < ActiveChart.NumInputs)
					{
						var e = current.Current;
						var lane = e.GetLane();
						if (lane >= 0)
						{
							if (!lanesChecked[lane])
							{
								lanesChecked[lane] = true;
								numLanesChecked++;

								if (e.GetRow() + e.GetLength() > pos.GetRow())
								{
									if (e is EditorHoldStartNote hsn)
									{
										e.X = xStart + e.GetLane() * arrowSize;
										e.Y = (e.ChartEvent.IntegerPosition - chartPositionAtTopOfScreen) * ppr -
										      (arrowSize * 0.5);
										e.W = arrowSize;
										e.H = arrowSize;
										e.Scale = sizeZoom;
										noteEvents.Add(e);

										holdEndNotesNeedingToBeAdded[e.GetLane()] = hsn.GetHoldEndNote();
									}
								}
							}
						}
					}

					var hasNextRateEvent = rateEnumerator.MoveNext();
					EditorRateAlteringEvent nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

					// Now we can scan forward
					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						
						// Check to see if we have crossed into a new rate altering event section
						if (nextRateEvent != null && e.ChartEvent == nextRateEvent.ChartEvent)
						{
							// update rate parameters
							rateEvent = nextRateEvent;

							var previousPPR = ppr;
							var scrollRateForThisSection = rateEvent.ScrollRate * interpolatedScrollRate;
							pps = Preferences.Instance.RowBasedPixelsPerSecondAtDefaultBPM
							      * (rateEvent.Tempo / Preferences.Instance.RowBasedDefaultBPM)
							      * scrollRateForThisSection
							      * spacingZoom;
							ppr = pps * rateEvent.SecondsPerRow;
							var rateEventPositionInPixels = previousRateEventPixelPosition + ((rateEvent.Row - previousRateEventRow) * previousPPR);
							previousRateEventPixelPosition = rateEventPositionInPixels;
							previousRateEventRow = rateEvent.Row;

							// advance next rate altering event.
							hasNextRateEvent = rateEnumerator.MoveNext();
							nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;
							continue;
						}
						
						var y = previousRateEventPixelPosition + 
							(e.ChartEvent.IntegerPosition - rateEvent.Row) * ppr - (arrowSize * 0.5);
						if (y > screenHeight)
							break;
						
						if (e is EditorTapNote
						    || e is EditorHoldStartNote
						    || e is EditorHoldEndNote
						    || e is EditorMineNote)
						{
							e.X = xStart + e.GetLane() * arrowSize;
							e.Y = y;
							e.W = arrowSize;
							e.H = arrowSize;
							e.Scale = sizeZoom;

							if (e is EditorHoldEndNote hen)
							{
								var start = hen.GetHoldStartNote();
								var endY = previousRateEventPixelPosition +
								           (e.ChartEvent.IntegerPosition - rateEvent.Row) * ppr;
								
								e.Y = start.Y + arrowSize * 0.5f;
								e.H = endY - start.Y;

								holdBodyEvents.Add(e);

								// Remove from holdEndNotesNeedingToBeAdded
								holdEndNotesNeedingToBeAdded[e.GetLane()] = null;
							}

							else if (e is EditorHoldStartNote hsn)
							{
								holdEndNotesNeedingToBeAdded[e.GetLane()] = hsn.GetHoldEndNote();
								noteEvents.Add(e);
							}
							else
							{
								noteEvents.Add(e);
							}
						}
						
					}

					// Now we need to wrap up any holds which started before the top of the screen and still are not yet complete.
					// We do not need to scan forward for more rate events.
					foreach (var holdEndNote in holdEndNotesNeedingToBeAdded)
					{
						if (holdEndNote == null)
							continue;

						holdEndNote.X = xStart + holdEndNote.GetLane() * arrowSize;
						holdEndNote.W = arrowSize;
						holdEndNote.Scale = sizeZoom;

						var start = holdEndNote.GetHoldStartNote();
						var endY = previousRateEventPixelPosition +
							           (holdEndNote.ChartEvent.IntegerPosition - rateEvent.Row) * ppr;

						holdEndNote.Y = start.Y + arrowSize * 0.5;
						holdEndNote.H = endY - start.Y;

						holdBodyEvents.Add(holdEndNote);
					}

					VisibleEvents.AddRange(holdBodyEvents);
					VisibleEvents.AddRange(noteEvents);

					break;
				}
			}
		}

		private void DrawChartEvents()
		{
			foreach (var visibleEvent in VisibleEvents)
			{
				visibleEvent.Draw(TextureAtlas, SpriteBatch);
			}
		}


		#region Gui Rendering

		private void DrawGui(GameTime gameTime)
		{
			ImGui.PushFont(Font);

			DrawMainMenuUI();

			// Debug UI
			{
				ImGui.Text("Hello, world!");
				ImGui.Text(string.Format("Application average {0:F3} ms/frame ({1:F1} FPS)", 1000f / ImGui.GetIO().Framerate,
					ImGui.GetIO().Framerate));
			}
			if (ShowImGuiTestWindow)
			{
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref ShowImGuiTestWindow);
			}

			DrawLogUI();
			DrawScrollControlUI();
			DrawWaveFormUI();
			DrawOptionsUI();

			ImGui.PopFont();

			// Call AfterLayout now to finish up and draw all the things
			ImGuiRenderer.AfterLayout();
		}

		private void DrawMainMenuUI()
		{
			if (ImGui.BeginMainMenuBar())
			{
				if (ImGui.BeginMenu("File"))
				{
					if (ImGui.MenuItem("Open", "Ctrl+O"))
					{
						OpenSongFile();
					}

					if (ImGui.BeginMenu("Open Recent", Preferences.Instance.RecentFiles.Count > 0))
					{
						foreach (var recentFile in Preferences.Instance.RecentFiles)
						{
							var fileNameWithPath = recentFile.FileName;
							var fileName = System.IO.Path.GetFileName(fileNameWithPath);
							if (ImGui.MenuItem(fileName))
							{
								OpenSongFileAsync(fileNameWithPath,
									recentFile.LastChartType,
									recentFile.LastChartDifficultyType);
							}
						}

						ImGui.EndMenu();
					}

					if (ImGui.MenuItem("Reload", "Ctrl+R", false,
						Song != null && Preferences.Instance.RecentFiles.Count > 0))
					{
						OpenSongFileAsync(Preferences.Instance.RecentFiles[0].FileName,
							Preferences.Instance.RecentFiles[0].LastChartType,
							Preferences.Instance.RecentFiles[0].LastChartDifficultyType);
					}

					if (ImGui.MenuItem("Exit", "Alt+F4"))
					{
						Exit();
					}

					ImGui.EndMenu();
				}

				if (ImGui.BeginMenu("View"))
				{
					if (ImGui.MenuItem("Log"))
						Preferences.Instance.ShowLogWindow = true;
					if (ImGui.MenuItem("Waveform Controls"))
						Preferences.Instance.ShowWaveFormWindow = true;
					if (ImGui.MenuItem("Scroll Controls"))
						Preferences.Instance.ShowScrollControlWindow = true;
					if (ImGui.MenuItem("Options"))
						Preferences.Instance.ShowOptionsWindow = true;
					if (ImGui.MenuItem("ImGui Demo Window"))
						ShowImGuiTestWindow = true;
					ImGui.EndMenu();
				}

				ImGui.EndMainMenuBar();
			}
		}

		private void DrawLogUI()
		{
			if (!Preferences.Instance.ShowLogWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(200, 100), ImGuiCond.FirstUseEver);
			ImGui.Begin("Log", ref Preferences.Instance.ShowLogWindow, ImGuiWindowFlags.NoScrollbar);
			lock (LogBufferLock)
			{
				ImGui.PushItemWidth(60);
				ComboFromEnum("Level", ref Preferences.Instance.LogWindowLevel);
				ImGui.PopItemWidth();

				ImGui.SameLine();
				ImGui.PushItemWidth(186);
				ImGui.Combo("Time", ref Preferences.Instance.LogWindowDateDisplay, LogWindowDateStrings,
					LogWindowDateStrings.Length);
				ImGui.PopItemWidth();

				ImGui.SameLine();
				ImGui.Checkbox("Wrap", ref Preferences.Instance.LogWindowLineWrap);

				ImGui.Separator();

				var flags = Preferences.Instance.LogWindowLineWrap ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar;
				ImGui.BeginChild("LogMessages", new System.Numerics.Vector2(), false, flags);
				{
					foreach (var message in LogBuffer)
					{
						if (message.Level < Preferences.Instance.LogWindowLevel)
							continue;

						if (Preferences.Instance.LogWindowDateDisplay != 0)
						{
							ImGui.Text(message.Time.ToString(LogWindowDateStrings[Preferences.Instance.LogWindowDateDisplay]));
							ImGui.SameLine();
						}

						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PushTextWrapPos();
						ImGui.TextColored(LogWindowLevelColors[(int)message.Level], message.Message);
						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PopTextWrapPos();
					}
				}
				ImGui.EndChild();
			}

			ImGui.End();
		}

		private void DrawWaveFormUI()
		{
			if (!Preferences.Instance.ShowWaveFormWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Waveform", ref Preferences.Instance.ShowWaveFormWindow, ImGuiWindowFlags.NoScrollbar);

			ImGui.Checkbox("Show Waveform", ref Preferences.Instance.ShowWaveForm);
			ImGui.Checkbox("Scale X When Zooming", ref Preferences.Instance.WaveFormScaleXWhenZooming);
			ImGui.SliderFloat("X Scale", ref Preferences.Instance.WaveFormMaxXPercentagePerChannel, 0.0f, 1.0f);

			ImGui.ColorEdit3("Dense Color", ref Preferences.Instance.WaveFormDenseColor, ImGuiColorEditFlags.NoAlpha);

			ImGui.Combo("Sparse Color Mode", ref Preferences.Instance.WaveFormWindowSparseColorOption,
				WaveFormWindowSparseColorOptions, WaveFormWindowSparseColorOptions.Length);
			if (Preferences.Instance.WaveFormWindowSparseColorOption == 0)
			{
				ImGui.SliderFloat("Sparse Color Scale", ref Preferences.Instance.SparseColorScale, 0.0f, 1.0f);
				Preferences.Instance.WaveFormSparseColor.X =
					Preferences.Instance.WaveFormDenseColor.X * Preferences.Instance.SparseColorScale;
				Preferences.Instance.WaveFormSparseColor.Y =
					Preferences.Instance.WaveFormDenseColor.Y * Preferences.Instance.SparseColorScale;
				Preferences.Instance.WaveFormSparseColor.Z =
					Preferences.Instance.WaveFormDenseColor.Z * Preferences.Instance.SparseColorScale;
			}
			else if (Preferences.Instance.WaveFormWindowSparseColorOption == 1)
			{
				Preferences.Instance.WaveFormSparseColor = Preferences.Instance.WaveFormDenseColor;
			}
			else
			{
				ImGui.ColorEdit3("Sparse Color", ref Preferences.Instance.WaveFormSparseColor, ImGuiColorEditFlags.NoAlpha);
			}

			if (ImGui.SliderInt("Loading Parallelism", ref Preferences.Instance.WaveFormLoadingMaxParallelism, 1, 128))
			{
				MusicMipMap.SetLoadParallelism(Preferences.Instance.WaveFormLoadingMaxParallelism);
			}

			ImGui.End();
		}

		private void DrawScrollControlUI()
		{
			if (!Preferences.Instance.ShowScrollControlWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Scroll Controls", ref Preferences.Instance.ShowScrollControlWindow, ImGuiWindowFlags.NoScrollbar);

			ComboFromEnum("Scroll Mode", ref Preferences.Instance.ScrollMode);
			ComboFromEnum("Spacing Mode", ref Preferences.Instance.SpacingMode);

			ImGui.SliderFloat("Timed Based Speed", ref Preferences.Instance.TimeBasedPixelsPerSecond, 1.0f, 100000.0f, null, ImGuiSliderFlags.Logarithmic);
			ImGui.SliderFloat($"Row Based Speed at {Preferences.Instance.RowBasedDefaultBPM} BPM", ref Preferences.Instance.RowBasedPixelsPerSecondAtDefaultBPM, 1.0f, 1000.0f, null, ImGuiSliderFlags.Logarithmic);
			ImGui.SliderFloat("Row Based BPM Scroll Rate", ref Preferences.Instance.RowBasedDefaultBPM, 1.0f, 1000.0f, null, ImGuiSliderFlags.Logarithmic);

			ImGui.End();
		}

		private void DrawOptionsUI()
		{
			if (!Preferences.Instance.ShowOptionsWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Options", ref Preferences.Instance.ShowOptionsWindow, ImGuiWindowFlags.NoScrollbar);

			if (ImGui.TreeNode("Startup Steps Types"))
			{
				var index = 0;
				foreach (var stepsType in Enum.GetValues(typeof(SMCommon.ChartType)).Cast<SMCommon.ChartType>())
				{
					if (ImGui.Selectable(
						SMCommon.ChartTypeString(stepsType),
						Preferences.Instance.StartupStepsTypesBools[index]))
					{
						if (!ImGui.GetIO().KeyCtrl)
						{
							for (var i = 0; i < Preferences.Instance.StartupStepsTypesBools.Length; i++)
							{
								Preferences.Instance.StartupStepsTypesBools[i] = false;
							}
						}

						Preferences.Instance.StartupStepsTypesBools[index] = !Preferences.Instance.StartupStepsTypesBools[index];
					}

					index++;
				}

				ImGui.TreePop();
			}

			if (ImGui.BeginCombo("Default Steps Type", Preferences.Instance.DefaultStepsType))
			{
				foreach (var stepsType in Enum.GetValues(typeof(SMCommon.ChartType)).Cast<SMCommon.ChartType>())
				{
					var stepsTypeString = SMCommon.ChartTypeString(stepsType);
					if (ImGui.Selectable(
						stepsTypeString,
						Preferences.Instance.DefaultStepsType == stepsTypeString))
					{
						Preferences.Instance.DefaultStepsType = stepsTypeString;
					}
				}

				ImGui.EndCombo();
			}

			if (ImGui.BeginCombo("Default Difficulty Type", Preferences.Instance.DefaultDifficultyType))
			{
				foreach (var difficultyType in Enum.GetValues(typeof(SMCommon.ChartDifficultyType))
					.Cast<SMCommon.ChartDifficultyType>())
				{
					var difficultyTypeString = difficultyType.ToString();
					if (ImGui.Selectable(
						difficultyTypeString,
						Preferences.Instance.DefaultDifficultyType == difficultyTypeString))
					{
						Preferences.Instance.DefaultDifficultyType = difficultyTypeString;
					}
				}

				ImGui.EndCombo();
			}

			ImGui.Checkbox("Open Last Opened File On Launch", ref Preferences.Instance.OpenLastOpenedFileOnLaunch);
			ImGui.SliderInt("Recent File History Size", ref Preferences.Instance.RecentFilesHistorySize, 0, 50);

			ImGui.End();
		}

		#endregion Gui Rendering

		#region Loading

		/// <summary>
		/// Initializes all PadData and creates corresponding StepGraphs for all StepsTypes
		/// specified in the StartupStepsTypes.
		/// </summary>
		private async void InitPadDataAndStepGraphsAsync()
		{
			var tasks = new Task<bool>[Preferences.Instance.StartupStepsTypes.Length];
			for (var i = 0; i < Preferences.Instance.StartupStepsTypes.Length; i++)
			{
				tasks[i] = LoadPadDataAndCreateStepGraph(Preferences.Instance.StartupStepsTypes[i]);
			}

			await Task.WhenAll(tasks);
		}

		/// <summary>
		/// Loads PadData and creates a StepGraph for the given StepMania StepsType.
		/// </summary>
		/// <returns>
		/// True if no errors were generated and false otherwise.
		/// </returns>
		private async Task<bool> LoadPadDataAndCreateStepGraph(string stepsType)
		{
			if (PadDataByStepsType.ContainsKey(stepsType))
				return true;

			PadDataByStepsType[stepsType] = null;
			StepGraphByStepsType[stepsType] = null;

			// Load the PadData.
			PadDataByStepsType[stepsType] = await LoadPadData(stepsType);
			if (PadDataByStepsType[stepsType] == null)
			{
				PadDataByStepsType.Remove(stepsType);
				StepGraphByStepsType.Remove(stepsType);
				return false;
			}

			// Create the StepGraph.
			await Task.Run(() =>
			{
				Logger.Info($"Creating {stepsType} StepGraph.");
				StepGraphByStepsType[stepsType] = StepGraph.CreateStepGraph(
					PadDataByStepsType[stepsType],
					PadDataByStepsType[stepsType].StartingPositions[0][0][L],
					PadDataByStepsType[stepsType].StartingPositions[0][0][R]);
				Logger.Info($"Finished creating {stepsType} StepGraph.");
			});

			return true;
		}

		/// <summary>
		/// Loads PadData for the given stepsType.
		/// </summary>
		/// <param name="stepsType">Stepmania StepsType to load PadData for.</param>
		/// <returns>Loaded PadData or null if any errors were generated.</returns>
		private static async Task<PadData> LoadPadData(string stepsType)
		{
			var fileName = $"{stepsType}.json";
			Logger.Info($"Loading PadData from {fileName}.");
			var padData = await PadData.LoadPadData(stepsType, fileName);
			if (padData == null)
				return null;
			Logger.Info($"Finished loading {stepsType} PadData.");
			return padData;
		}

		/// <summary>
		/// Starts the process of opening a Song file by presenting a dialog to choose a Song.
		/// </summary>
		private void OpenSongFile()
		{
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.InitialDirectory = Preferences.Instance.OpenFileDialogInitialDirectory;
				openFileDialog.Filter = "StepMania Files (*.sm,*.ssc)|*.sm;*.ssc|All files (*.*)|*.*";
				openFileDialog.FilterIndex = 1;

				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					var fileName = openFileDialog.FileName;
					Preferences.Instance.OpenFileDialogInitialDirectory = System.IO.Path.GetDirectoryName(fileName);
					OpenSongFileAsync(openFileDialog.FileName,
						Preferences.Instance.DefaultStepsType,
						Preferences.Instance.DefaultDifficultyType);
				}
			}
		}

		/// <summary>
		/// Starts the process of opening a selected Song.
		/// Will cancel previous OpenSongFileAsync requests if called while already loading.
		/// </summary>
		/// <param name="fileName">File name of the Song file to open.</param>
		/// <param name="chartType">Desired ChartType (StepMania StepsType) to default to once open.</param>
		/// <param name="chartDifficultyType">Desired DifficultyType to default to once open.</param>
		private async void OpenSongFileAsync(string fileName, string chartType, string chartDifficultyType)
		{
			try
			{
				// Store the song file we want to load.
				PendingOpenFileName = fileName;
				PendingOpenFileChartType = chartType;
				PendingOpenFileChartDifficultyType = chartDifficultyType;

				// If we are already loading a song file, cancel that operation so
				// we can start the new load.
				if (LoadSongCancellationTokenSource != null)
				{
					// If we are already cancelling something then return. We don't want
					// calls to this method to collide. We will end up using the most recently
					// requested song file due to the PendingOpenFileName variable.
					if (LoadSongCancellationTokenSource.IsCancellationRequested)
						return;

					// Start the cancellation and wait for it to complete.
					LoadSongCancellationTokenSource?.Cancel();
					await LoadSongTask;
				}

				// Use the most recently requested song file.
				fileName = PendingOpenFileName;
				chartType = PendingOpenFileChartType;
				chartDifficultyType = PendingOpenFileChartDifficultyType;

				// Start an asynchronous series of operations to load the song.
				LoadSongCancellationTokenSource = new CancellationTokenSource();
				LoadSongTask = Task.Run(async () =>
				{
					try
					{
						// Load the song file.
						var reader = Reader.CreateReader(fileName);
						if (reader == null)
						{
							Logger.Error($"Unsupported file format. Cannot parse {fileName}");
							return;
						}

						Logger.Info($"Loading {fileName}...");
						Song = await reader.LoadAsync(LoadSongCancellationTokenSource.Token);
						if (Song == null)
						{
							Logger.Error($"Failed to load {fileName}");
							return;
						}

						Logger.Info($"Loaded {fileName}");

						SongDirectory = System.IO.Path.GetDirectoryName(fileName);

						// Select the best Chart to make active.
						ActiveChart = SelectBestChart(Song, chartType, chartDifficultyType);
						LoadSongCancellationTokenSource.Token.ThrowIfCancellationRequested();

						// Set up Tree of EditorEvents for the Chart.
						SetUpEditorEvents();
					}
					catch (OperationCanceledException)
					{
						// Upon cancellation null out the Song and ActiveChart.
						Song = null;
						ActiveChart = null;
						EditorEvents = null;
						RateAlteringEventsBySongTime = null;
						RateAlteringEventsByRow = null;
						InterpolatedScrollRateEvents = null;
					}
					finally
					{
						LoadSongCancellationTokenSource.Dispose();
						LoadSongCancellationTokenSource = null;
					}
				}, LoadSongCancellationTokenSource.Token);
				await LoadSongTask;
				if (Song == null)
				{
					return;
				}

				// Insert a new entry at the top of the saved recent files.
				var savedSongInfo = new Preferences.SavedSongInformation
				{
					FileName = fileName,
					LastChartType = ActiveChart?.Type,
					LastChartDifficultyType = ActiveChart?.DifficultyType,
				};
				Preferences.Instance.RecentFiles.RemoveAll(info => info.FileName == fileName);
				Preferences.Instance.RecentFiles.Insert(0, savedSongInfo);
				if (Preferences.Instance.RecentFiles.Count > Preferences.Instance.RecentFilesHistorySize)
				{
					Preferences.Instance.RecentFiles.RemoveRange(
						Preferences.Instance.RecentFilesHistorySize,
						Preferences.Instance.RecentFiles.Count - Preferences.Instance.RecentFilesHistorySize);
				}

				// Find a better spot for this
				DesiredSongTime = 0.0;
				SongTime = 0.0;
				DesiredZoom = 1.0;
				Zoom = 1.0;

				// Start loading music for this Chart.
				LoadMusicAsync();
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {fileName}. {e}");
			}
		}

		/// <summary>
		/// Helper method when loading a Song to select the best Chart to be the active Chart.
		/// </summary>
		/// <param name="song">Song.</param>
		/// <param name="preferredChartType">The preferred ChartType (StepMania StepsType) to use.</param>
		/// <param name="preferredChartDifficultyType">The preferred DifficultyType to use.</param>
		/// <returns>Best Chart to use or null if no Charts exist.</returns>
		private Chart SelectBestChart(Song song, string preferredChartType, string preferredChartDifficultyType)
		{
			if (song.Charts.Count == 0)
				return null;

			// Choose the preferred chart, if it exists.
			foreach (var chart in song.Charts)
			{
				if (chart.Type == preferredChartType && chart.DifficultyType == preferredChartDifficultyType)
				{
					return chart;
				}
			}

			var orderdDifficultyTypes = new[]
			{
				SMCommon.ChartDifficultyType.Challenge.ToString(),
				SMCommon.ChartDifficultyType.Hard.ToString(),
				SMCommon.ChartDifficultyType.Medium.ToString(),
				SMCommon.ChartDifficultyType.Easy.ToString(),
				SMCommon.ChartDifficultyType.Beginner.ToString(),
				SMCommon.ChartDifficultyType.Edit.ToString(),
			};

			// If the preferred chart doesn't exist, try to choose the highest difficulty type
			// of the preferred chart type.
			foreach (var currDifficultyType in orderdDifficultyTypes)
			{
				foreach (var chart in song.Charts)
				{
					if (chart.Type == preferredChartType && chart.DifficultyType == currDifficultyType)
					{
						return chart;
					}
				}
			}

			// No charts of the specified type exist. Try the next best type.
			string nextBestChartType = null;
			if (preferredChartType == "dance-single")
				nextBestChartType = "dance-double";
			else if (preferredChartType == "dance-double")
				nextBestChartType = "dance-single";
			else if (preferredChartType == "pump-single")
				nextBestChartType = "pump-double";
			else if (preferredChartType == "pump-double")
				nextBestChartType = "pump-single";
			if (!string.IsNullOrEmpty(nextBestChartType))
			{
				foreach (var currDifficultyType in orderdDifficultyTypes)
				{
					foreach (var chart in song.Charts)
					{
						if (chart.Type == nextBestChartType && chart.DifficultyType == currDifficultyType)
						{
							return chart;
						}
					}
				}
			}

			// At this point, just return the first chart we have.
			return song.Charts[0];
		}

		public RedBlackTree<EditorEvent>.Enumerator FindEvent(EditorEvent pos)
		{
			var best = EditorEvents.FindGreatestPreceding(pos);
			if (best == null)
				best = EditorEvents.FindLeastFollowing(pos);
			if (best == null)
				return null;

			// Scan backwards until we have checked every lane for a long note which may
			// be extending through the given start row.
			var lanesChecked = new bool[ActiveChart.NumInputs];
			var numLanesChecked = 0;
			var current = new RedBlackTree<EditorEvent>.Enumerator(best);
			while(current.MovePrev() && numLanesChecked < ActiveChart.NumInputs)
			{
				var e = current.Current;
				var lane = e.GetLane();
				if (lane >= 0)
				{
					if (lanesChecked[lane])
					{
						lanesChecked[lane] = true;
						numLanesChecked++;

						if (e.GetRow() + e.GetLength() > pos.GetRow())
							best = new RedBlackTree<EditorEvent>.Enumerator(current);
					}
				}
			}

			return best;
		}

		/// <summary>
		/// Asynchronously loads the music file for the ActiveChart.
		/// If the currently ActiveChart's music is already loaded or loading, does nothing.
		/// Will cancel previous LoadMusicAsync requests if called while already loading.
		/// </summary>
		private async void LoadMusicAsync()
		{
			// The music file is defined on a Chart. We must have an active Chart to load the music file.
			if (ActiveChart == null)
				return;

			// The Chart must have a music file defined.
			var musicFile = ActiveChart.MusicFile;
			if (string.IsNullOrEmpty(musicFile))
				return;

			// Most Charts in StepMania use relative paths for their music files.
			// Determine the absolute path.
			string fullPathToMusicFile;
			if (System.IO.Path.IsPathRooted(musicFile))
				fullPathToMusicFile = musicFile;
			else
				fullPathToMusicFile = Path.Combine(SongDirectory, musicFile);

			// Most Charts in one Song use the same music file. Do not reload the
			// music file if we were already using it.
			if (fullPathToMusicFile == SongMipMapFile)
				return;

			// Store the music file we want to load.
			PendingMusicFile = fullPathToMusicFile;

			// If we are already loading a music file, cancel that operation so
			// we can start the new load.
			if (LoadMusicCancellationTokenSource != null)
			{
				// If we are already cancelling then return. We don't want multiple
				// calls to this method to collide. We will end up using the most recently
				// requested music file due to the PendingMusicFile variable.
				if (LoadMusicCancellationTokenSource.IsCancellationRequested)
					return;

				// Start the cancellation and wait for it to complete.
				LoadMusicCancellationTokenSource?.Cancel();
				await LoadMusicTask;
			}

			// Store the new music file.
			SongMipMapFile = PendingMusicFile;

			// Start an asynchronous series of operations to load the music and set up the mip map.
			LoadMusicCancellationTokenSource = new CancellationTokenSource();
			LoadMusicTask = Task.Run(async () =>
			{
				try
				{
					// Release the handle to the old music if it is present.
					if (Music.hasHandle())
						SoundManager.ErrCheck(Music.release());
					Music.handle = IntPtr.Zero;

					// Reset the mip map before loadingLoadMusicAsync the new music because loading the music
					// can take a moment and we don't want to continue to render the old audio.
					MusicMipMap.Reset();

					LoadMusicCancellationTokenSource.Token.ThrowIfCancellationRequested();

					// Load the music file.
					// This is not cancelable. According to FMOD: "you can’t cancel it"
					// https://qa.fmod.com/t/reusing-channels/13145/3
					// Normally this is not a problem, but for hour-long files this is unfortunate.
					Logger.Info($"Loading {SongMipMapFile}...");
					Music = await SoundManager.LoadAsync(SongMipMapFile);
					SoundManager.PlaySound(Music, MusicChannelGroup, out MusicChannel);
					SoundManager.ErrCheck(Music.getFormat(out _, out _, out MusicNumChannels, out MusicBitsPerSample));
					SoundManager.ErrCheck(Music.getLength(out MusicTotalBytes, TIMEUNIT.PCMBYTES));
					SoundManager.ErrCheck(MusicChannel.getFrequency(out float frequency));
					MusicSampleRate = (uint)frequency;
					Logger.Info($"Loaded {SongMipMapFile}...");

					LoadMusicCancellationTokenSource.Token.ThrowIfCancellationRequested();

					// Set up the new sound mip map.
					await MusicMipMap.CreateMipMapAsync(Music, MusicSampleRate, WaveFormTextureWidth, LoadMusicCancellationTokenSource.Token);

					LoadMusicCancellationTokenSource.Token.ThrowIfCancellationRequested();
				}
				catch (OperationCanceledException)
				{
					// Upon cancellation release the music sound handle and clear the mip map data.
					if (Music.hasHandle())
						SoundManager.ErrCheck(Music.release());
					Music.handle = IntPtr.Zero;
					MusicMipMap.Reset();
				}
				finally
				{
					LoadMusicCancellationTokenSource.Dispose();
					LoadMusicCancellationTokenSource = null;
				}
			}, LoadMusicCancellationTokenSource.Token);
			await LoadMusicTask;
		}

		#endregion Loading

		private double GetMusicPosition()
		{
			SoundManager.ErrCheck(MusicChannel.getPosition(out uint bytes, TIMEUNIT.PCMBYTES));
			return (double)bytes / MusicNumChannels / (MusicBitsPerSample >> 3) / MusicSampleRate;
		}

		private void SetMusicPosition(double timeInSeconds)
		{
			uint bytes = 0;
			if (timeInSeconds >= 0.0)
				bytes = (uint)(timeInSeconds * MusicNumChannels * (MusicBitsPerSample >> 3) * MusicSampleRate);
			if (bytes > MusicTotalBytes)
				bytes = MusicTotalBytes - 1;
			SoundManager.ErrCheck(MusicChannel.setPosition(bytes, TIMEUNIT.PCMBYTES));
		}

		private double GetMusicLength()
		{
			return (double)MusicTotalBytes / MusicNumChannels / (MusicBitsPerSample >> 3) / MusicSampleRate;
		}

		private void SetUpEditorEvents()
		{
			var editorEvents = new RedBlackTree<EditorEvent>();
			var rateAlteringEventsBySongTime = new RedBlackTree<EditorRateAlteringEvent>(EditorRateAlteringEvent.SortSongTime());
			var rateAlteringEventsByRow = new RedBlackTree<EditorRateAlteringEvent>(EditorRateAlteringEvent.SortRow());
			var interpolatedScrollRateEvents = new RedBlackTree<EditorInterpolatedRateAlteringEvent>();

			EditorRateAlteringEvent previousEvent = new EditorRateAlteringEvent
			{
				Row = 0,
				SongTime = 0.0,
				RowsPerSecond = 0.0,
				SecondsPerRow = 0.0
			};

			var lastHoldStarts = new EditorHoldStartNote[ActiveChart.NumInputs];
			var lastScrollRateInterpolationValue = 1.0;
			var lastScrollRate = 1.0;
			var lastTempo = 1.0;
			foreach (var chartEvent in ActiveChart.Layers[0].Events)
			{
				var editorEvent = EditorEvent.CreateEvent(chartEvent);
				if (editorEvent != null)
					editorEvents.Insert(editorEvent);

				if (editorEvent is EditorHoldStartNote hs)
				{
					lastHoldStarts[hs.GetLane()] = hs;
				}
				else if (editorEvent is EditorHoldEndNote he)
				{
					var start = lastHoldStarts[he.GetLane()];
					he.SetHoldStartNote(start);
					start.SetHoldEndNote(he);
				}

				if (chartEvent is Tempo tc)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;
					
					var newSecondsPerRow = 60.0 / tc.TempoBPM / (double)SMCommon.MaxValidDenominator;
					var newRowsPerSecond = 1.0 / newSecondsPerRow;

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						RowsPerSecond = newRowsPerSecond,
						SecondsPerRow = newSecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = tc.TempoBPM
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
					lastTempo = tc.TempoBPM;
				}
				else if (chartEvent is Stop stop)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious + (stop.LengthMicros / 1000000.0),
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = lastTempo
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
				}
				else if (chartEvent is Warp warp)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition + warp.LengthIntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = lastTempo
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
				}
				else if (chartEvent is ScrollRate scrollRate)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = scrollRate.Rate,
						Tempo = lastTempo
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
					lastScrollRate = scrollRate.Rate;
				}
				else if (chartEvent is ScrollRateInterpolation scrollRateInterpolation)
				{
					var newEvent = new EditorInterpolatedRateAlteringEvent
					{
						Row = scrollRateInterpolation.IntegerPosition,
						SongTime = scrollRateInterpolation.TimeMicros,
						ChartEvent = scrollRateInterpolation,
						PreviousScrollRate = lastScrollRateInterpolationValue
					};
					interpolatedScrollRateEvents.Insert(newEvent);
					lastScrollRateInterpolationValue = scrollRateInterpolation.Rate;
				}
			}

			EditorEvents = editorEvents;
			RateAlteringEventsBySongTime = rateAlteringEventsBySongTime;
			RateAlteringEventsByRow = rateAlteringEventsByRow;
			InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
		}

		private bool TryGetChartPositionFromSongTime(double songTime, ref double chartPosition)
		{
			if (RateAlteringEventsBySongTime == null)
				return false;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator = RateAlteringEventsBySongTime.FindGreatestPreceding(new EditorRateAlteringEvent { SongTime = songTime });
			// If there is no preceding event (e.g. SongTime is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEventsBySongTime.GetEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return false;

			// Update the ChartPosition based on the cached rate information.
			enumerator.MoveNext();
			var rateEvent = enumerator.Current;
			if (songTime >= rateEvent.SongTime && songTime < rateEvent.SongTimeForFollowingEvents)
				chartPosition = rateEvent.Row;
			else
				chartPosition = rateEvent.Row + rateEvent.RowsPerSecond * (songTime - rateEvent.SongTimeForFollowingEvents);
			return true;
		}

		private void UpdateChartPositionFromSongTime()
		{
			TryGetChartPositionFromSongTime(SongTime, ref ChartPosition);
		}

		private bool TryGetSongTimeFromChartPosition(double chartPosition, ref double songTime)
		{
			if (RateAlteringEventsByRow == null)
				return false;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator = RateAlteringEventsByRow.FindGreatestPreceding(new EditorRateAlteringEvent { Row = chartPosition });
			// If there is no preceding event (e.g. ChartPosition is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEventsByRow.GetEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return false;

			// Update the Song time based on the cached rate information.
			// TODO: Need to take into account Stops vs Delays?
			enumerator.MoveNext();
			var rateEvent = enumerator.Current;
			songTime = rateEvent.SongTimeForFollowingEvents + rateEvent.SecondsPerRow * (chartPosition - rateEvent.Row);
			return true;
		}

		private void UpdateSongTimeFromChartPosition()
		{
			TryGetSongTimeFromChartPosition(ChartPosition, ref SongTime);
		}
	}
}
