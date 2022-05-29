using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Path = Fumen.Path;
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

		/// <summary>
		/// How the WaveForm should scroll when not using ConstantTime SpacingMode.
		/// </summary>
		public enum WaveFormScrollMode
		{
			/// <summary>
			/// Scroll matching the current tempo.
			/// </summary>
			CurrentTempo,

			/// <summary>
			/// Scroll matching the current tempo and rate multipliers.
			/// </summary>
			CurrentTempoAndRate,

			/// <summary>
			/// Scroll matching the most common tempo of the Chart.
			/// </summary>
			MostCommonTempo
		}

		private Vector2 FocalPoint;
		

		private string PendingOpenFileName;
		private string PendingOpenFileChartType;
		private string PendingOpenFileChartDifficultyType;

		private GraphicsDeviceManager Graphics;
		private SpriteBatch SpriteBatch;
		private ImGuiRenderer ImGuiRenderer;
		private WaveFormRenderer WaveFormRenderer;
		private SoundManager SoundManager;
		private MusicManager MusicManager;
		private MiniMap MiniMap;
		private UISongProperties UISongProperties;

		private TextureAtlas TextureAtlas;

		private CancellationTokenSource LoadSongCancellationTokenSource;
		private Task LoadSongTask;

		private Dictionary<string, PadData> PadDataByStepsType = new Dictionary<string, PadData>();
		private Dictionary<string, StepGraph> StepGraphByStepsType = new Dictionary<string, StepGraph>();

		private double PlaybackStartTime;
		private Stopwatch PlaybackStopwatch;

		private ActionQueue ActionQueue;
		private EditorSong EditorSong;

		private Chart ActiveChart;
		private double ChartPosition = 0.0;
		private RedBlackTree<EditorEvent> EditorEvents;
		private RedBlackTree<EditorRateAlteringEvent> RateAlteringEventsBySongTime;
		private RedBlackTree<EditorRateAlteringEvent> RateAlteringEventsByRow;
		private RedBlackTree<EditorInterpolatedRateAlteringEvent> InterpolatedScrollRateEvents;

		private List<EditorEvent> VisibleEvents = new List<EditorEvent>();
		private List<MarkerEvent> VisibleMarkers = new List<MarkerEvent>();

		private double WaveFormPPS = 1.0;
		private double CurrentChartMostCommonTempo = 1.0;

		// temp controls
		private int MouseScrollValue = 0;
		private double SongTimeInterpolationTimeStart = 0.0;
		private double SongTime = 0.0;
		private double SongTimeAtStartOfInterpolation = 0.0;
		private double DesiredSongTime = 0.0;
		private double ZoomInterpolationTimeStart = 0.0;
		private double Zoom = 1.0;
		private double ZoomAtStartOfInterpolation = 1.0;
		private double DesiredZoom = 1.0;

		private KeyCommandManager KeyCommandManager;
		private bool Playing = false;
		private bool PlayingPreview = false;
		private bool MiniMapCapturingMouse = false;
		private bool StartPlayingWhenMiniMapDone = false;
		private MouseState PreviousMouseState;

		private uint MaxScreenHeight;

		private ImFontPtr ImGuiFont;
		private SpriteFont MonogameFont_MPlus1Code_Medium;

		// Debug
		private bool ParallelizeUpdateLoop = false;
		private double UpdateTimeTotal;
		private double UpdateTimeWaveForm;
		private double UpdateTimeMiniMap;
		private double UpdateTimeChartEvents;
		private double DrawTimeTotal;

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
			{ "Darker Dense Color", "Same As Dense Color", "Unique Color" };

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
			MusicManager = new MusicManager(SoundManager);

			Graphics = new GraphicsDeviceManager(this);

			ActionQueue = new ActionQueue();

			KeyCommandManager = new KeyCommandManager();
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Z }, OnUndo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Z }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Y }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.O }, OnOpen, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Space }, OnTogglePlayback, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.P }, OnTogglePlayPreview, false));

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
			ImGuiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(
				@"C:\Users\perry\Projects\Fumen\StepManiaEditor\Content\Mplus1Code-Medium.ttf",
				15,
				null,
				ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
			ImGuiRenderer.RebuildFontAtlas();

			MonogameFont_MPlus1Code_Medium = Content.Load<SpriteFont>("mplus1code-medium");

			foreach (var adapter in GraphicsAdapter.Adapters)
			{
				MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
			}

			WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, MaxScreenHeight);
			WaveFormRenderer.SetXPerChannelScale(Preferences.Instance.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetSoundMipMap(MusicManager.GetMusicMipMap());
			WaveFormRenderer.SetFocalPoint(FocalPoint);

			MiniMap = new MiniMap(GraphicsDevice, new Rectangle(0, 0, 0, 0));
			MiniMap.SetSelectMode(Preferences.Instance.MiniMapSelectMode);
			UpdateMiniMapBounds();
			UpdateMiniMapLaneSpacing();

			TextureAtlas = new TextureAtlas(GraphicsDevice, 2048, 2048, 1);

			UISongProperties = new UISongProperties(this, GraphicsDevice, ImGuiRenderer, ActionQueue);

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
			foreach (var kvp in ArrowTextureByBeatSubdivision)
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

			var measureMarkerTexture = new Texture2D(GraphicsDevice, DefaultArrowWidth, 1);
			var textureData = new uint[DefaultArrowWidth];
			for (var i = 0; i < DefaultArrowWidth; i++)
				textureData[i] = 0xFFFFFFFF;
			measureMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdMeasureMarker, measureMarkerTexture);

			var beatMarkerTexture = new Texture2D(GraphicsDevice, DefaultArrowWidth, 1);
			for (var i = 0; i < DefaultArrowWidth; i++)
				textureData[i] = 0xFF7F7F7F;
			beatMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdBeatMarker, beatMarkerTexture);

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

			// Update ViewPort dependent state
			UpdateMiniMapBounds();
		}

		protected override void Update(GameTime gameTime)
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

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

					MusicManager.SetMusicTimeInSeconds(SongTime);
				}
			}

			if (Zoom != DesiredZoom)
			{
				SetZoom(Interpolation.Lerp(
					ZoomAtStartOfInterpolation,
					DesiredZoom,
					ZoomInterpolationTimeStart,
					ZoomInterpolationTimeStart + 0.1,
					gameTime.TotalGameTime.TotalSeconds), false);
			}

			MusicManager.SetPreviewParameters(
				EditorSong?.SampleStart ?? 0.0,
				EditorSong?.SampleLength ?? 0.0,
				Preferences.Instance.PreviewFadeInTime,
				Preferences.Instance.PreviewFadeOutTime);

			if (Playing)
			{
				SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

				MusicManager.Update(SongTime);
				if (MusicManager.IsMusicLoaded())
				{
					// The goal is to set the SongTime to match the actual time of the music
					// being played through FMOD. Querying the time from FMOD does not have high
					// enough precision, so we need to use our own timer.
					// The best C# timer for this task is a StopWatch, but StopWatches have been
					// reported to drift, sometimes up to a half a second per hour. If we detect
					// it has drifted significantly by comparing it to the time from FMOD, then
					// snap it back.
					var maxDeviation = 0.1;
					var fmodSongTime = MusicManager.GetMusicTimeInSeconds();
					if (SongTime >= 0.0 && SongTime < MusicManager.GetMusicLengthInSeconds())
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
				}

				DesiredSongTime = SongTime;
			}
			else
			{
				MusicManager.Update(SongTime);
			}

			// If using SongTime
			UpdateChartPositionFromSongTime();

			Action timedUpdateChartEvents = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateChartEvents();
				s.Stop();
				UpdateTimeChartEvents = s.Elapsed.TotalSeconds;
			};
			Action timedUpdateMiniMap = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateMiniMap();
				s.Stop();
				UpdateTimeMiniMap = s.Elapsed.TotalSeconds;
			};
			Action timedUpdateWaveForm = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateWaveFormRenderer();
				s.Stop();
				UpdateTimeWaveForm = s.Elapsed.TotalSeconds;
			};

			// CPU heavy updates.
			// This kind of parallelization isn't helpful as it has to create new threads each frame.
			if (ParallelizeUpdateLoop)
			{
				Parallel.Invoke(
					() => timedUpdateChartEvents(),
					() => timedUpdateMiniMap(),
					() => timedUpdateWaveForm());
			}
			else
			{
				timedUpdateChartEvents();
				timedUpdateMiniMap();
				timedUpdateWaveForm();
			}

			base.Update(gameTime);

			stopWatch.Stop();
			UpdateTimeTotal = stopWatch.Elapsed.TotalSeconds;
		}

		private void ProcessInput(GameTime gameTime)
		{
			// Let imGui process input so we can see if we should ignore it.
			(bool imGuiWantMouse, bool imGuiWantKeyboard) = ImGuiRenderer.Update(gameTime);

			// Process Keyboard Input.
			if (imGuiWantKeyboard)
				KeyCommandManager.CancelAllCommands();
			else
				KeyCommandManager.Update(gameTime.TotalGameTime.TotalSeconds);
			var scrollShouldZoom = !imGuiWantKeyboard && KeyCommandManager.IsKeyDown(Keys.LeftControl);

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
				ProcessInputForMiniMap(mouseState);

				//mouseState.

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

							if (Preferences.Instance.StopPlaybackWhenScrolling)
							{
								StopPlayback();
							}
							else
							{
								MusicManager.SetMusicTimeInSeconds(SongTime);
							}
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

							if (Preferences.Instance.StopPlaybackWhenScrolling)
							{
								StopPlayback();
							}
							else
							{
								MusicManager.SetMusicTimeInSeconds(SongTime);
							}
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

			PreviousMouseState = mouseState;
		}

		private void StartPlayback()
		{
			if (Playing)
				return;

			StopPreview();

			if (!MusicManager.IsMusicLoaded() || SongTime < 0.0 || SongTime > MusicManager.GetMusicLengthInSeconds())
			{
				PlaybackStartTime = SongTime;
			}
			else
			{
				PlaybackStartTime = MusicManager.GetMusicTimeInSeconds();
			}

			PlaybackStopwatch = new Stopwatch();
			PlaybackStopwatch.Start();
			MusicManager.StartPlayback(SongTime);

			Playing = true;
		}

		private void StopPlayback()
		{
			if (!Playing)
				return;

			PlaybackStopwatch.Stop();
			MusicManager.StopPlayback();

			Playing = false;
		}

		private void SetSongTime(double songTime)
		{
			SongTime = songTime;
			DesiredSongTime = songTime;
			MusicManager.SetMusicTimeInSeconds(SongTime);

			if (Playing)
			{
				PlaybackStartTime = SongTime;
				PlaybackStopwatch = new Stopwatch();
				PlaybackStopwatch.Start();
			}
		}

		private void SetZoom(double zoom, bool setDesiredZoom)
		{
			Zoom = zoom;
			if (setDesiredZoom)
			{
				DesiredZoom = zoom;
			}

			UpdateMiniMapBounds();
		}

		protected override void Draw(GameTime gameTime)
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			GraphicsDevice.Clear(Color.Black);

			SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
			if (Preferences.Instance.ShowWaveForm)
			{
				WaveFormRenderer.Draw(SpriteBatch);
			}

			DrawMiniMap();

			DrawReceptors();
			DrawChartEvents();
			SpriteBatch.End();

			DrawGui(gameTime);

			base.Draw(gameTime);

			stopWatch.Stop();
			DrawTimeTotal = stopWatch.Elapsed.TotalSeconds;
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
				TextureAtlas.Draw(
					TextureIdReceptor,
					SpriteBatch,
					new Rectangle((int)x, (int)y, (int)arrowSize, (int)arrowSize),
					rot[i % rot.Length],
					1.0f);
			}
		}

		private void UpdateWaveFormRenderer()
		{
			// Performance optimization. Do not update the texture if we won't render it.
			if (!Preferences.Instance.ShowWaveForm)
				return;

			WaveFormRenderer.SetFocalPoint(FocalPoint);
			WaveFormRenderer.SetXPerChannelScale(Preferences.Instance.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetColors(
				Preferences.Instance.WaveFormDenseColor.X, Preferences.Instance.WaveFormDenseColor.Y,
				Preferences.Instance.WaveFormDenseColor.Z,
				Preferences.Instance.WaveFormSparseColor.X, Preferences.Instance.WaveFormSparseColor.Y,
				Preferences.Instance.WaveFormSparseColor.Z);
			WaveFormRenderer.SetScaleXWhenZooming(Preferences.Instance.WaveFormScaleXWhenZooming);
			WaveFormRenderer.Update(SongTime, Zoom, WaveFormPPS);
		}

		/// <summary>
		/// Given a RedBlackTree and a value, find the greatest preceding value.
		/// If no value precedes the given value, instead find the least value that follows or is
		/// equal to the given value.
		/// </summary>
		/// <remarks>
		/// This is a common pattern when knowing a position or a time and wanting to find the first event to
		/// start enumerator over for rendering.
		/// </remarks>
		/// <typeparam name="T">Type of data in the tree.</typeparam>
		/// <typeparam name="U">Type of data to compare against for finding.</typeparam>
		/// <param name="tree">RedBlackTree to search.</param>
		/// <param name="data">Value to use for comparisons.</param>
		/// <param name="comparer">Comparer function.</param>
		/// <returns>Enumerator to best value or null if a value could not be found.</returns>
		private RedBlackTree<T>.Enumerator FindBest<T, U>(RedBlackTree<T> tree, U data, Func<U, T, int> comparer)
			where T : IComparable<T>
		{
			var enumerator = tree.FindGreatestPreceding(data, comparer);
			if (enumerator == null)
				enumerator = tree.FindLeastFollowing(data, comparer, true);
			return enumerator;
		}

		/// <summary>
		/// Sets VisibleEvents and VisibleMarkers to lists of all the currently visible objects
		/// based on the current time or position and the SpacingMode.
		/// </summary>
		/// <remarks>
		/// Sets the WaveFormPPS.
		/// </remarks>
		private void UpdateChartEvents()
		{
			// TODO: Cleanup
			// TODO: Crash when switching songs from doubles to singles.

			VisibleEvents.Clear();
			VisibleMarkers.Clear();

			if (EditorEvents == null)
				return;

			List<EditorEvent> holdBodyEvents = new List<EditorEvent>();
			List<EditorEvent> noteEvents = new List<EditorEvent>();

			var screenHeight = Graphics.GraphicsDevice.Viewport.Height;

			var spacingZoom = Zoom;
			var sizeZoom = Zoom;
			if (sizeZoom > 1.0)
				sizeZoom = 1.0;
			var arrowSize = DefaultArrowWidth * sizeZoom;
			var holdCapHeight = DefaultHoldCapHeight * sizeZoom;

			var numArrows = ActiveChart.NumInputs;
			var xStart = FocalPoint.X - (numArrows * arrowSize * 0.5);

			// TODO: Common(?) code for determining song time by chart position or vice versa based on scroll mode

			switch (Preferences.Instance.SpacingMode)
			{
				case SpacingMode.ConstantTime:
				{
					WaveFormPPS = Preferences.Instance.TimeBasedPixelsPerSecond;

					var pps = Preferences.Instance.TimeBasedPixelsPerSecond * spacingZoom;
					var time = SongTime + GetMusicOffset();
					var timeAtTopOfScreen = time - (FocalPoint.Y / pps);
					double chartPosition = 0.0;
					if (!TryGetChartPositionFromSongTime(timeAtTopOfScreen, ref chartPosition))
						return;

					// Beat markers.
					if (sizeZoom >= MeasureMarkerMinScale)
					{
						// Find the first rate altering event to use.
						var rateEnumerator = FindBest(RateAlteringEventsByRow, chartPosition,
							EditorRateAlteringEvent.CompareToRow);
						if (rateEnumerator == null)
							return;
						rateEnumerator.MoveNext();

						// Record the current and next rate altering event.
						var currentRateEvent = rateEnumerator.Current;
						EditorRateAlteringEvent nextRateEvent = null;
						if (rateEnumerator.MoveNext())
							nextRateEvent = rateEnumerator.Current;
						var currentRow = (int)chartPosition;
						var lastRecordedRow = -1;

						// Update beat markers for every rate section until we have hit the bottom of the screen.
						while (!UpdateBeatMarkers(
							       currentRateEvent,
							       ref currentRow,
							       ref lastRecordedRow,
							       nextRateEvent,
							       xStart,
							       sizeZoom,
							       pps,
							       timeAtTopOfScreen))
						{
							currentRateEvent = rateEnumerator.Current;
							nextRateEvent = null;
							if (rateEnumerator.MoveNext())
								nextRateEvent = rateEnumerator.Current;
						}
					}

					var enumerator = FindBest(EditorEvents, chartPosition, EditorEvent.CompareToRow);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						hsn.X = xStart + hsn.GetLane() * arrowSize;
						hsn.Y = ((hsn.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps - (arrowSize * 0.5);
						hsn.W = arrowSize;
						hsn.H = arrowSize;
						hsn.Scale = sizeZoom;
						noteEvents.Add(hsn);

						var end = hsn.GetHoldEndNote();
						holdBodyEvents.Add(end);
						end.X = xStart + end.GetLane() * arrowSize;
						end.Y = ((hsn.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps;
						end.W = arrowSize;
						end.H = (((end.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps) - end.Y + holdCapHeight;
						end.Scale = sizeZoom;
					}

					// Scan forward and add notes.
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
								holdBodyEvents.Add(end);
								end.X = xStart + end.GetLane() * arrowSize;
								end.Y = ((e.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps;
								end.W = arrowSize;
								end.H = (((end.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps) - end.Y +
								        holdCapHeight;
								end.Scale = sizeZoom;
							}
						}

						noteEvents.Add(e);

						if (noteEvents.Count + holdBodyEvents.Count > MaxEventsToDraw)
							break;
					}

					break;
				}

				case SpacingMode.ConstantRow:
				{
					var time = SongTime + GetMusicOffset();
					double chartPosition = 0.0;
					if (!TryGetChartPositionFromSongTime(time, ref chartPosition))
						return;
					var ppr = Preferences.Instance.RowBasedPixelsPerRow * spacingZoom;
					var chartPositionAtTopOfScreen = chartPosition - (FocalPoint.Y / ppr);

					// Update WaveForm scroll scroll rate
					{
						var rateEnumerator = FindBest(RateAlteringEventsByRow, chartPosition,
							EditorRateAlteringEvent.CompareToRow);
						if (rateEnumerator != null)
						{
							rateEnumerator.MoveNext();
							var pps = Preferences.Instance.RowBasedPixelsPerRow * rateEnumerator.Current.RowsPerSecond;
							WaveFormPPS = pps;
							if (Preferences.Instance.RowBasedWaveFormScrollMode == WaveFormScrollMode.MostCommonTempo)
							{
								WaveFormPPS *= (CurrentChartMostCommonTempo / rateEnumerator.Current.Tempo);
							}
						}
					}

					// Beat markers.
					if (sizeZoom >= MeasureMarkerMinScale)
					{
						// Find the first rate altering event to use.
						var rateEnumerator = FindBest(RateAlteringEventsByRow, chartPositionAtTopOfScreen,
							EditorRateAlteringEvent.CompareToRow);
						if (rateEnumerator == null)
							return;
						rateEnumerator.MoveNext();

						// Record the current and next rate altering event.
						var currentRateEvent = rateEnumerator.Current;
						EditorRateAlteringEvent nextRateEvent = null;
						if (rateEnumerator.MoveNext())
							nextRateEvent = rateEnumerator.Current;
						var currentRow = (int)chartPositionAtTopOfScreen;
						var lastRecordedRow = -1;

						// Update beat markers for every rate section until we have hit the bottom of the screen.
						while (!UpdateBeatMarkers(
							       currentRateEvent,
							       ref currentRow,
							       ref lastRecordedRow,
							       nextRateEvent,
							       xStart,
							       sizeZoom,
							       ppr,
							       chartPositionAtTopOfScreen * ppr))
						{
							currentRateEvent = rateEnumerator.Current;
							nextRateEvent = null;
							if (rateEnumerator.MoveNext())
								nextRateEvent = rateEnumerator.Current;
						}
					}

					var enumerator = FindBest(EditorEvents, chartPositionAtTopOfScreen, EditorEvent.CompareToRow);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
					foreach (var hsn in holdStartNotes)
					{
						hsn.X = xStart + hsn.GetLane() * arrowSize;
						hsn.Y = (hsn.GetRow() - chartPositionAtTopOfScreen) * ppr - (arrowSize * 0.5);
						hsn.W = arrowSize;
						hsn.H = arrowSize;
						hsn.Scale = sizeZoom;
						noteEvents.Add(hsn);

						var end = hsn.GetHoldEndNote();
						holdBodyEvents.Add(end);
						end.X = xStart + end.GetLane() * arrowSize;
						end.Y = (hsn.GetRow() - chartPositionAtTopOfScreen) * ppr;
						end.W = arrowSize;
						end.H = ((end.GetRow() - chartPositionAtTopOfScreen) * ppr) - end.Y + holdCapHeight;
						end.Scale = sizeZoom;
					}

					// Scan forward and add notes.
					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						var y = (e.GetRow() - chartPositionAtTopOfScreen) * ppr - (arrowSize * 0.5);
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
								holdBodyEvents.Add(end);
								end.X = xStart + end.GetLane() * arrowSize;
								end.Y = (e.GetRow() - chartPositionAtTopOfScreen) * ppr;
								end.W = arrowSize;
								end.H = ((end.GetRow() - chartPositionAtTopOfScreen) * ppr) - end.Y + holdCapHeight;
								end.Scale = sizeZoom;
							}
						}

						noteEvents.Add(e);

						if (noteEvents.Count + holdBodyEvents.Count > MaxEventsToDraw)
							break;
					}

					break;
				}

				case SpacingMode.Variable:
				{
					// TODO: Fix Negative Scrolls resulting in cutting off notes prematurely.
					// If a chart has negative scrolls then we technically need to render notes which come before
					// the chart position at the top of the screen.
					// More likely the most visible problem will be at the bottom of the screen where if we
					// were to detect the first note which falls below the bottom it would prevent us from
					// finding the next set of notes which might need to be rendered because they appear 
					// above.

					var time = SongTime + GetMusicOffset();
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
					var interpolatedScrollRateEnumerator =
						InterpolatedScrollRateEvents.FindGreatestPreceding(ratePosEventForChecking);
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

					var rateEnumerator = FindBest(RateAlteringEventsByRow, chartPosition, EditorRateAlteringEvent.CompareToRow);
					if (rateEnumerator == null)
						return;

					// Scan upwards to find the earliest rate altering event that should be used to start rendering.
					var previousRateEventPixelPosition = (double)FocalPoint.Y;
					var previousRateEventRow = chartPosition;
					var pps = 1.0;
					var ppr = 1.0;
					EditorRateAlteringEvent rateEvent = null;
					while (previousRateEventPixelPosition > 0.0 && rateEnumerator.MovePrev())
					{
						// On the rate altering event which is active for the current chart position,
						// Record the pixels per second to use for the WaveForm.
						if (rateEvent == null)
						{
							var tempo = CurrentChartMostCommonTempo;
							if (Preferences.Instance.RowBasedWaveFormScrollMode != WaveFormScrollMode.MostCommonTempo)
								tempo = rateEnumerator.Current.Tempo;
							var useRate = Preferences.Instance.RowBasedWaveFormScrollMode ==
							              WaveFormScrollMode.CurrentTempoAndRate;
							WaveFormPPS = Preferences.Instance.VariablePixelsPerSecondAtDefaultBPM
							              * (tempo / Preferences.DefaultVariableSpeedBPM);
							if (useRate)
							{
								var rate = rateEnumerator.Current.ScrollRate * interpolatedScrollRate;
								if (rate <= 0.0)
									rate = 1.0;
								WaveFormPPS *= rate;
							}
						}

						rateEvent = rateEnumerator.Current;
						var scrollRateForThisSection = rateEvent.ScrollRate * interpolatedScrollRate;
						pps = Preferences.Instance.VariablePixelsPerSecondAtDefaultBPM
						      * (rateEvent.Tempo / Preferences.DefaultVariableSpeedBPM)
						      * scrollRateForThisSection
						      * spacingZoom;
						ppr = pps * rateEvent.SecondsPerRow;
						var rateEventPositionInPixels =
							previousRateEventPixelPosition - ((previousRateEventRow - rateEvent.Row) * ppr);
						previousRateEventPixelPosition = rateEventPositionInPixels;
						previousRateEventRow = rateEvent.Row;
					}

					// Now we know the position of first rate altering event to use.
					// We can now determine the chart position at the top of the screen
					var pixelPositionAtTopOfScreen = 0.0;
					var chartPositionAtTopOfScreen =
						rateEvent.Row + (pixelPositionAtTopOfScreen - previousRateEventPixelPosition) *
						rateEvent.RowsPerSecond / pps;

					var beatMarkerRow = (int)chartPositionAtTopOfScreen;
					var beatMarkerLastRecordedRow = -1;

					// Now that we know the position at the start of the screen we can find the first event to start rendering.
					var enumerator = FindBest(EditorEvents, chartPositionAtTopOfScreen, EditorEvent.CompareToRow);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row. We cannot add the end events yet because
					// we do not know at what position they will end until we scan down.
					var holdEndNotesNeedingToBeAdded = new EditorHoldEndNote[ActiveChart.NumInputs];
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
					foreach (var hsn in holdStartNotes)
					{
						hsn.X = xStart + hsn.GetLane() * arrowSize;
						hsn.Y = (hsn.ChartEvent.IntegerPosition - chartPositionAtTopOfScreen) * ppr -
						        (arrowSize * 0.5);
						hsn.W = arrowSize;
						hsn.H = arrowSize;
						hsn.Scale = sizeZoom;
						noteEvents.Add(hsn);

						holdEndNotesNeedingToBeAdded[hsn.GetLane()] = hsn.GetHoldEndNote();
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
							// Update beat markers for the section for the previous rate event.
							UpdateBeatMarkers(
								rateEvent,
								ref beatMarkerRow,
								ref beatMarkerLastRecordedRow,
								nextRateEvent,
								xStart,
								sizeZoom,
								ppr,
								previousRateEventPixelPosition);

							// Update rate parameters.
							rateEvent = nextRateEvent;
							var previousPPR = ppr;
							var scrollRateForThisSection = rateEvent.ScrollRate * interpolatedScrollRate;
							pps = Preferences.Instance.VariablePixelsPerSecondAtDefaultBPM
							      * (rateEvent.Tempo / Preferences.DefaultVariableSpeedBPM)
							      * scrollRateForThisSection
							      * spacingZoom;
							ppr = pps * rateEvent.SecondsPerRow;
							var rateEventPositionInPixels = previousRateEventPixelPosition +
							                                ((rateEvent.Row - previousRateEventRow) * previousPPR);
							previousRateEventPixelPosition = rateEventPositionInPixels;
							previousRateEventRow = rateEvent.Row;

							// Advance next rate altering event.
							hasNextRateEvent = rateEnumerator.MoveNext();
							nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;
							continue;
						}

						// Determine y position.
						var y = previousRateEventPixelPosition +
							(e.ChartEvent.IntegerPosition - rateEvent.Row) * ppr - (arrowSize * 0.5);
						if (y > screenHeight)
							break;

						// Record note.
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

								// Remove from holdEndNotesNeedingToBeAdded.
								holdEndNotesNeedingToBeAdded[e.GetLane()] = null;
							}

							else if (e is EditorHoldStartNote hsn)
							{
								// Record that there is in an in-progress hold that will need to be ended.
								holdEndNotesNeedingToBeAdded[e.GetLane()] = hsn.GetHoldEndNote();
								noteEvents.Add(e);
							}
							else
							{
								noteEvents.Add(e);
							}
						}

						if (noteEvents.Count + holdBodyEvents.Count > MaxEventsToDraw)
							break;
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

					// Also, wrap up any beats.
					UpdateBeatMarkers(
						rateEvent,
						ref beatMarkerRow,
						ref beatMarkerLastRecordedRow,
						nextRateEvent,
						xStart,
						sizeZoom,
						ppr,
						previousRateEventPixelPosition);

					break;
				}
			}

			VisibleEvents.AddRange(holdBodyEvents);
			VisibleEvents.AddRange(noteEvents);
		}

		/// <summary>
		/// Helper method to update beat marker events.
		/// Adds new MarkerEvents to VisibleMarkers.
		/// Almost all of the logic for updating beat marker placement is independent of SpacingMode.
		/// Expected to be called in a loop over EditorRateAlteringEvents which encompass the visible area.
		/// </summary>
		/// <param name="currentRateEvent">
		/// The current EditorRateAlteringEvent.
		/// MarkerEvents will be filled for the region in this event up until the given
		/// nextRateEvent, or end of the visible area defined by the viewport's height.
		/// </param>
		/// <param name="currentRow">
		/// The current row to start with. This row may not be on a beat boundary. If it is not on a beat
		/// boundary then MarkerEvents will be added starting with the following beat.
		/// This parameter is passed by reference so the beat marker logic can maintain state about where
		/// it left off.
		/// </param>
		/// <param name="lastRecordedRow">
		/// The last row that this method recorded a beat for.
		/// This parameter is passed by reference so the beat marker logic can maintain state about where
		/// it left off.
		/// </param>
		/// <param name="nextRateEvent">
		/// The EditorRateAlteringEvent following currentRateEvent or null if no such event follows it.
		/// </param>
		/// <param name="x">X position in pixels to set on the MarkerEvents.</param>
		/// <param name="sizeZoom">Current zoom level to use for setting the width and scale of the MarkerEvents.</param>
		/// <param name="spacingRate">
		///	For ConstantTime this rate is pixels per second.
		/// For ConstantRow this rate is is pixels per row.
		/// For Variable this rate is is pixels per row.
		/// </param>
		/// <param name="spacingBaseValue">
		///	For ConstantTime this value is the time at the top of the screen.
		/// For ConstantRow this value is the pixel position of row at the top of the screen.
		/// For Variable this value is the pixel position of the previous rate altering event.
		/// </param>
		private bool UpdateBeatMarkers(
			EditorRateAlteringEvent currentRateEvent,
			ref int currentRow,
			ref int lastRecordedRow,
			EditorRateAlteringEvent nextRateEvent,
			double x,
			double sizeZoom,
			double spacingRate,
			double spacingBaseValue)
		{
			if (sizeZoom < MeasureMarkerMinScale)
				return true;
			if (VisibleMarkers.Count >= MaxMarkersToDraw)
				return true;

			// Based on the current rate altering event, determine the beat spacing and snap the current row to a beat.
			var beatsPerMeasure = currentRateEvent.LastTimeSignature.Signature.Numerator;
			var rowsPerBeat = (SMCommon.MaxValidDenominator * SMCommon.NumBeatsPerMeasure * beatsPerMeasure)
			                  / currentRateEvent.LastTimeSignature.Signature.Denominator / beatsPerMeasure;

			// Determine which integer measure and beat we are on. Clamped due to warps.
			var rowRelativeToTimeSignatureStart = Math.Max(0,
				currentRow - currentRateEvent.LastTimeSignature.IntegerPosition);
			// We need to snap the row forward since we are starting with a row that might not be on a beat boundary.
			var beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
			currentRow = currentRateEvent.LastTimeSignature.IntegerPosition +
			             beatRelativeToTimeSignatureStart * rowsPerBeat;

			while (true)
			{
				// When changing time signatures we don't want to render the same row twice.
				if (currentRow == lastRecordedRow)
				{
					currentRow += rowsPerBeat;
					continue;
				}

				// Determine the y position of this marker.
				// Clamp due to warps.
				var rowRelativeToLastRateChangeEvent = Math.Max(0, currentRow - currentRateEvent.RowForFollowingEvents);
				rowRelativeToLastRateChangeEvent = Math.Max(0, rowRelativeToLastRateChangeEvent);
				var y = 0.0;
				switch (Preferences.Instance.SpacingMode)
				{
					case SpacingMode.ConstantTime:
					{
						// Y is the time of the row times the pixels per second, shifted by the time at the top of the screen.
						var beatTime = currentRateEvent.SongTimeForFollowingEvents +
						               rowRelativeToLastRateChangeEvent * currentRateEvent.SecondsPerRow;
						y = (beatTime - spacingBaseValue) * spacingRate;
						break;
					}

					case SpacingMode.ConstantRow:
					{
						// Y is the current row times the pixels per row minus the pixel position at the top of the screen
						y = currentRow * spacingRate - spacingBaseValue;
						break;
					}

					case SpacingMode.Variable:
					{
						// Y is the previous rate event's pixel position plus the relative row times the pixels per row.
						y = spacingBaseValue + rowRelativeToLastRateChangeEvent * spacingRate;
						break;
					}
				}

				// If advancing this beat forward moved us over the next rate altering event boundary, loop again.
				if (nextRateEvent != null && currentRow > nextRateEvent.Row)
				{
					currentRow = (int)nextRateEvent.Row;
					return false;
				}

				// If advancing moved beyond the end of the screen then we are done.
				if (y > Graphics.GraphicsDevice.Viewport.Height)
				{
					return true;
				}

				// Determine if this marker is a measure marker instead of a beat marker.
				rowRelativeToTimeSignatureStart =
					currentRow - currentRateEvent.LastTimeSignature.IntegerPosition;
				beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
				var measureMarker = beatRelativeToTimeSignatureStart % beatsPerMeasure == 0;
				var measure = currentRateEvent.LastTimeSignature.MetricPosition.Measure +
				              (beatRelativeToTimeSignatureStart / beatsPerMeasure);

				// Record the marker.
				if (measureMarker || sizeZoom > BeatMarkerMinScale)
				{
					VisibleMarkers.Add(new MarkerEvent
					{
						X = x,
						Y = y,
						W = ActiveChart.NumInputs * DefaultArrowWidth * sizeZoom,
						H = 1,
						MeasureMarker = measureMarker,
						Measure = measure,
						Scale = sizeZoom
					});
				}

				lastRecordedRow = currentRow;

				if (VisibleMarkers.Count >= MaxMarkersToDraw)
					return true;

				// Advance one beat.
				currentRow += rowsPerBeat;
			}
		}

		#region MiniMap

		private void ProcessInputForMiniMap(MouseState mouseState)
		{
			var mouseDownThisFrame = mouseState.LeftButton == ButtonState.Pressed &&
			                         PreviousMouseState.LeftButton != ButtonState.Pressed;
			var mouseUpThisFrame = mouseState.LeftButton != ButtonState.Pressed &&
			                       PreviousMouseState.LeftButton == ButtonState.Pressed;
			var miniMapCapturingMouseLastFrame = MiniMapCapturingMouse;

			var miniMapNeedsMouseThisFrame = false;
			if (mouseDownThisFrame)
			{
				miniMapNeedsMouseThisFrame = MiniMap.MouseDown(mouseState.X, mouseState.Y);
			}

			MiniMap.MouseMove(mouseState.X, mouseState.Y);
			if (mouseUpThisFrame)
			{
				MiniMap.MouseUp(mouseState.X, mouseState.Y);
			}

			MiniMapCapturingMouse = MiniMap.WantsMouse();

			// Set the Song Position based on the MiniMap position
			miniMapNeedsMouseThisFrame |= MiniMapCapturingMouse;
			if (miniMapNeedsMouseThisFrame)
			{
				// When moving the MiniMap, pause or stop playback.
				if (mouseDownThisFrame && Playing)
				{
					// Set a flag to unpause playback unless the preference is to completely stop when scrolling.
					StartPlayingWhenMiniMapDone = !Preferences.Instance.MiniMapStopPlaybackWhenScrolling;
					StopPlayback();
				}

				// Set the music position based off of the MiniMap editor area.
				var editorPosition = MiniMap.GetEditorPosition();
				switch (GetMiniMapSpacingMode())
				{
					case SpacingMode.ConstantTime:
					{
						SetSongTime(editorPosition + (FocalPoint.Y / (Preferences.Instance.TimeBasedPixelsPerSecond * Zoom)) -
						            GetMusicOffset());
						break;
					}
					case SpacingMode.ConstantRow:
					{
						var chartPosition =
							editorPosition + (FocalPoint.Y / (Preferences.Instance.RowBasedPixelsPerRow * Zoom));
						var songTime = 0.0;
						if (TryGetSongTimeFromChartPosition(chartPosition, ref songTime))
						{
							SetSongTime(songTime);
						}

						break;
					}
				}
			}

			// When letting go of the MiniMap, start playing again.
			if (miniMapCapturingMouseLastFrame && !MiniMapCapturingMouse && StartPlayingWhenMiniMapDone)
			{
				StartPlayingWhenMiniMapDone = false;
				StartPlayback();
			}
		}

		private void UpdateMiniMapBounds()
		{
			var x = 0;
			var zoom = Math.Min(1.0, Zoom);
			switch (Preferences.Instance.MiniMapPosition)
			{
				case MiniMap.Position.RightSideOfWindow:
				{
					x = Graphics.PreferredBackBufferWidth - MiniMapXPadding - (int)Preferences.Instance.MiniMapWidth;
					break;
				}
				case MiniMap.Position.RightOfChartArea:
				{
					x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) + MiniMapXPadding);
					break;
				}
				case MiniMap.Position.MountedToWaveForm:
				{
					if (Preferences.Instance.WaveFormScaleXWhenZooming)
					{
						x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) * zoom + MiniMapXPadding);
					}
					else
					{
						x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) + MiniMapXPadding);
					}

					break;
				}
				case MiniMap.Position.MountedToChart:
				{
					x = (int)(FocalPoint.X + ((ActiveChart.NumInputs * DefaultArrowWidth) >> 1) * zoom + MiniMapXPadding);
					break;
				}
			}

			var h = Math.Max(0, Graphics.PreferredBackBufferHeight - MiniMapYPaddingFromTop - MiniMapYPaddingFromBottom);

			MiniMap.UpdateBounds(
				GraphicsDevice,
				new Rectangle(x, MiniMapYPaddingFromTop, (int)Preferences.Instance.MiniMapWidth, h));
		}

		private void UpdateMiniMapLaneSpacing()
		{
			MiniMap.SetLaneSpacing(
				Preferences.Instance.MiniMapNoteWidth,
				Preferences.Instance.MiniMapNoteSpacing);
		}

		private SpacingMode GetMiniMapSpacingMode()
		{
			if (Preferences.Instance.SpacingMode == SpacingMode.Variable)
				return Preferences.Instance.MiniMapSpacingModeForVariable;
			return Preferences.Instance.SpacingMode;
		}

		private void UpdateMiniMap()
		{
			// Performance optimization. Do not update the MiniMap if we won't render it.
			if (!Preferences.Instance.ShowMiniMap)
				return;

			if (EditorEvents == null)
				return;

			MiniMap.SetSelectMode(Preferences.Instance.MiniMapSelectMode);

			switch (GetMiniMapSpacingMode())
			{
				case SpacingMode.ConstantTime:
				{
					var screenHeight = Graphics.GraphicsDevice.Viewport.Height;
					var spacingZoom = Zoom;
					var pps = Preferences.Instance.TimeBasedPixelsPerSecond * spacingZoom;
					var time = SongTime + GetMusicOffset();

					// Editor Area. The visible time range.
					var editorAreaTimeStart = time - (FocalPoint.Y / pps);
					var editorAreaTimeEnd = editorAreaTimeStart + (screenHeight / pps);
					var editorAreaTimeRange = editorAreaTimeEnd - editorAreaTimeStart;

					// Determine the end time.
					var lastEvent = EditorEvents.Last();
					var maxTimeFromChart = 0.0;
					if (lastEvent.MoveNext())
						maxTimeFromChart = lastEvent.Current.ChartEvent.TimeMicros / 1000000.0;
					if (ActiveChart.Extras.TryGetExtra(SMCommon.TagLastSecondHint, out double lastSecondHint))
						maxTimeFromChart = Math.Max(maxTimeFromChart, lastSecondHint);

					// Full Area. The time from the chart, extended in both directions by the editor range.
					var fullAreaTimeStart = 0.0 - editorAreaTimeRange;
					var fullAreaTimeEnd = maxTimeFromChart + editorAreaTimeRange;

					// Content Area. The time from the chart.
					var contentAreaTimeStart = 0.0;
					var contentAreaTimeEnd = maxTimeFromChart;

					// Update the MiniMap with the ranges.
					MiniMap.SetNumLanes((uint)ActiveChart.NumInputs);
					MiniMap.UpdateBegin(
						fullAreaTimeStart, fullAreaTimeEnd,
						contentAreaTimeStart, contentAreaTimeEnd,
						Preferences.Instance.MiniMapVisibleTimeRange,
						editorAreaTimeStart, editorAreaTimeEnd);

					// Add notes
					double chartPosition = 0.0;
					if (!TryGetChartPositionFromSongTime(MiniMap.GetMiniMapAreaStart(), ref chartPosition))
						break;
					var enumerator = FindBest(EditorEvents, chartPosition, EditorEvent.CompareToRow);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.ChartEvent,
							hsn.ChartEvent.TimeMicros / 1000000.0,
							hsn.GetHoldEndNote().ChartEvent.TimeMicros / 1000000.0,
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNote)
							continue;

						if (e is EditorTapNote)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.ChartEvent, e.ChartEvent.TimeMicros / 1000000.0) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNote)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.ChartEvent, e.ChartEvent.TimeMicros / 1000000.0) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNote hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.ChartEvent,
								    e.ChartEvent.TimeMicros / 1000000.0,
								    hsn.GetHoldEndNote().ChartEvent.TimeMicros / 1000000.0,
								    hsn.IsRoll()) == MiniMap.AddResult.BelowBottom)
								break;
						}

						if (numNotesAdded > MiniMapMaxNotesToDraw)
							break;
					}

					break;
				}

				case SpacingMode.ConstantRow:
				{
					var time = SongTime + GetMusicOffset();
					var screenHeight = Graphics.GraphicsDevice.Viewport.Height;
					double chartPosition = 0.0;
					if (!TryGetChartPositionFromSongTime(time, ref chartPosition))
						return;
					var spacingZoom = Zoom;
					var ppr = Preferences.Instance.RowBasedPixelsPerRow * spacingZoom;

					// Editor Area. The visible row range.
					var editorAreaRowStart = chartPosition - (FocalPoint.Y / ppr);
					var editorAreaRowEnd = editorAreaRowStart + (screenHeight / ppr);
					var editorAreaRowRange = editorAreaRowEnd - editorAreaRowStart;

					// Determine the end row.
					var lastEvent = EditorEvents.Last();
					var maxRowFromChart = 0.0;
					if (lastEvent.MoveNext())
						maxRowFromChart = lastEvent.Current.ChartEvent.IntegerPosition;
					if (ActiveChart.Extras.TryGetExtra(SMCommon.TagLastSecondHint, out double lastSecondHint))
					{
						var lastSecondChartPosition = 0.0;
						if (TryGetChartPositionFromSongTime(time, ref lastSecondChartPosition))
						{
							maxRowFromChart = Math.Max(lastSecondChartPosition, lastSecondHint);
						}
					}

					// Full Area. The area from the chart, extended in both directions by the editor range.
					var fullAreaRowStart = 0.0 - editorAreaRowRange;
					var fullAreaRowEnd = maxRowFromChart + editorAreaRowRange;

					// Content Area. The rows from the chart.
					var contentAreaTimeStart = 0.0;
					var contentAreaTimeEnd = maxRowFromChart;

					// Update the MiniMap with the ranges.
					MiniMap.SetNumLanes((uint)ActiveChart.NumInputs);
					MiniMap.UpdateBegin(
						fullAreaRowStart, fullAreaRowEnd,
						contentAreaTimeStart, contentAreaTimeEnd,
						Preferences.Instance.MiniMapVisibleRowRange,
						editorAreaRowStart, editorAreaRowEnd);

					// Add notes
					chartPosition = MiniMap.GetMiniMapAreaStart();
					var enumerator = FindBest(EditorEvents, chartPosition, EditorEvent.CompareToRow);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.ChartEvent,
							hsn.ChartEvent.IntegerPosition,
							hsn.GetHoldEndNote().ChartEvent.IntegerPosition,
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNote)
							continue;

						if (e is EditorTapNote)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.ChartEvent, e.ChartEvent.IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNote)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.ChartEvent, e.ChartEvent.IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNote hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.ChartEvent,
								    e.ChartEvent.IntegerPosition,
								    hsn.GetHoldEndNote().ChartEvent.IntegerPosition,
								    hsn.IsRoll()) == MiniMap.AddResult.BelowBottom)
								break;
						}

						if (numNotesAdded > MiniMapMaxNotesToDraw)
							break;
					}


					break;
				}
			}

			MiniMap.UpdateEnd();
		}

		private void DrawMiniMap()
		{
			if (!Preferences.Instance.ShowMiniMap)
				return;
			MiniMap.Draw(SpriteBatch);
		}

		#endregion MiniMap

		/// <summary>
		/// Given a chart position, scans backwards for hold notes which begin earlier and end later.
		/// </summary>
		/// <param name="enumerator">Enumerator to use for scanning backwards.</param>
		/// <param name="chartPosition">Chart position to use for checking.</param>
		/// <returns>List of EditorHoldStartNotes.</returns>
		private List<EditorHoldStartNote> ScanBackwardsForHolds(RedBlackTree<EditorEvent>.Enumerator enumerator,
			double chartPosition)
		{
			var lanesChecked = new bool[ActiveChart.NumInputs];
			var numLanesChecked = 0;
			var holds = new List<EditorHoldStartNote>();
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

						if (e.GetRow() + e.GetLength() > chartPosition)
						{
							if (e is EditorHoldStartNote hsn)
							{
								holds.Add(hsn);
							}
						}
					}
				}
			}

			return holds;
		}

		private void DrawChartEvents()
		{
			foreach (var visibleMarker in VisibleMarkers)
			{
				visibleMarker.Draw(TextureAtlas, SpriteBatch, MonogameFont_MPlus1Code_Medium);
			}

			foreach (var visibleEvent in VisibleEvents)
			{
				visibleEvent.Draw(TextureAtlas, SpriteBatch);
			}
		}


		#region Gui Rendering

		private void DrawGui(GameTime gameTime)
		{
			ImGui.PushFont(ImGuiFont);

			DrawMainMenuUI();

			DrawDebugUI();

			if (ShowImGuiTestWindow)
			{
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref ShowImGuiTestWindow);
			}

			DrawLogUI();
			DrawScrollControlUI();
			DrawWaveFormUI();
			DrawMiniMapUI();
			DrawOptionsUI();

			UISongProperties.Draw(EditorSong);
			DrawChartPropertiesUI();

			ImGui.PopFont();

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
						    EditorSong != null && Preferences.Instance.RecentFiles.Count > 0))
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
					if (ImGui.MenuItem("Options"))
						Preferences.Instance.ShowOptionsWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Song Properties"))
						Preferences.Instance.ShowSongPropertiesWindow = true;
					if (ImGui.MenuItem("Chart Properties"))
						Preferences.Instance.ShowChartPropertiesWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Waveform Controls"))
						Preferences.Instance.ShowWaveFormWindow = true;
					if (ImGui.MenuItem("Scroll Controls"))
						Preferences.Instance.ShowScrollControlWindow = true;
					if (ImGui.MenuItem("Mini Map Controls"))
						Preferences.Instance.ShowMiniMapWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Log"))
						Preferences.Instance.ShowLogWindow = true;
					if (ImGui.MenuItem("ImGui Demo Window"))
						ShowImGuiTestWindow = true;
					ImGui.EndMenu();
				}

				ImGui.EndMainMenuBar();
			}
		}

		private void DrawDebugUI()
		{
			ImGui.Begin("Debug");
			{
				ImGui.Checkbox("Parallelize Update Loop", ref ParallelizeUpdateLoop);
				ImGui.Text($"Update Time:       {UpdateTimeTotal:F6} seconds");
				ImGui.Text($"  Waveform:        {UpdateTimeWaveForm:F6} seconds");
				ImGui.Text($"  Mini Map:        {UpdateTimeMiniMap:F6} seconds");
				ImGui.Text($"  Chart Events:    {UpdateTimeChartEvents:F6} seconds");
				ImGui.Text($"Draw Time:         {DrawTimeTotal:F6} seconds");
				ImGui.Text($"Total Time:        {(UpdateTimeTotal + DrawTimeTotal):F6} seconds");
				ImGui.Text($"Total FPS:         {(1.0f / (UpdateTimeTotal + DrawTimeTotal)):F6}");
				ImGui.Text($"Actual Time:       {1.0f / ImGui.GetIO().Framerate:F6} seconds");
				ImGui.Text($"Actual FPS:        {ImGui.GetIO().Framerate:F6}");

				if (ImGui.Button("Save Time and Zoom"))
				{
					Preferences.Instance.DebugSongTime = SongTime;
					Preferences.Instance.DebugZoom = Zoom;
				}

				if (ImGui.Button("Load Time and Zoom"))
				{
					SetSongTime(Preferences.Instance.DebugSongTime);
					SetZoom(Preferences.Instance.DebugZoom, true);
				}
			}
			ImGui.End();
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
			if (ImGui.Checkbox("Scale X When Zooming", ref Preferences.Instance.WaveFormScaleXWhenZooming))
				UpdateMiniMapBounds();
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
				MusicManager.GetMusicMipMap().SetLoadParallelism(Preferences.Instance.WaveFormLoadingMaxParallelism);
			}

			ImGui.End();
		}

		private void DrawMiniMapUI()
		{
			if (!Preferences.Instance.ShowMiniMapWindow)
				return;
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Mini Map", ref Preferences.Instance.ShowMiniMapWindow, ImGuiWindowFlags.NoScrollbar);

			ImGui.PushItemWidth(200);

			ImGui.Checkbox("Show Mini Map", ref Preferences.Instance.ShowMiniMap);

			ImGui.Separator();
			if (ComboFromEnum("Position", ref Preferences.Instance.MiniMapPosition))
				UpdateMiniMapBounds();
			if (SliderUInt("Width", ref Preferences.Instance.MiniMapWidth, 2, 128, null, ImGuiSliderFlags.None))
				UpdateMiniMapBounds();
			if (SliderUInt("Note Width", ref Preferences.Instance.MiniMapNoteWidth, 1, 32, null, ImGuiSliderFlags.None))
				UpdateMiniMapLaneSpacing();
			if (SliderUInt("Note Spacing", ref Preferences.Instance.MiniMapNoteSpacing, 0, 32, null, ImGuiSliderFlags.None))
				UpdateMiniMapLaneSpacing();

			ImGui.Separator();
			ComboFromEnum("Select Mode", ref Preferences.Instance.MiniMapSelectMode);
			ImGui.SameLine();
			HelpMarker("How the editor should move when selecting an area outside of the editor range in the mini map."
			           + "\nMove Editor To Cursor:         Move the editor to the cursor, not to the area under the cursor."
			           + "\n                               This is the natural option if you consider the mini map like a scroll bar."
			           + "\nMove Editor To Selected Area:  Move the editor to the area under the cursor, not to the cursor."
			           + "\n                               This is the natural option if you consider the mini map like a map.");

			ImGui.Checkbox("Always Grab", ref Preferences.Instance.MiniMapGrabWhenClickingOutsideEditorArea);
			ImGui.SameLine();
			HelpMarker("When the Select Mode is set to Move Editor To Cursor, enabling this will cause the editor range "
			           + "\nto be grabbed while holding down the left mouse button.");
			ImGui.Checkbox("Stop Playback When Scrolling", ref Preferences.Instance.MiniMapStopPlaybackWhenScrolling);

			ImGui.Separator();
			ComboFromEnum("Spacing Mode For Variable Scroll Spacing",
				ref Preferences.Instance.MiniMapSpacingModeForVariable,
				Preferences.MiniMapVariableSpacingModes,
				"MiniMapVariableScrollSpacing");
			ImGui.SameLine();
			HelpMarker("The Spacing Mode the MiniMap should use when the Scroll Spacing Mode is Variable.");
			SliderUInt("Constant Time Spacing Range (seconds)", ref Preferences.Instance.MiniMapVisibleTimeRange, 30, 300, null,
				ImGuiSliderFlags.Logarithmic);
			SliderUInt("Constant Row Spacing Range (rows)", ref Preferences.Instance.MiniMapVisibleRowRange, 3072, 28800, null,
				ImGuiSliderFlags.Logarithmic);

			ImGui.Separator();
			if (ImGui.Button("Restore Defaults"))
			{
				Preferences.Instance.ShowMiniMap = Preferences.DefaultShowMiniMap;
				Preferences.Instance.MiniMapSelectMode = Preferences.DefualtMiniMapSelectMode;
				Preferences.Instance.MiniMapGrabWhenClickingOutsideEditorArea =
					Preferences.DefaultMiniMapGrabWhenClickingOutsideEditorArea;
				Preferences.Instance.MiniMapStopPlaybackWhenScrolling = Preferences.DefaultMiniMapStopPlaybackWhenScrolling;
				Preferences.Instance.MiniMapWidth = Preferences.DefaultMiniMapWidth;
				Preferences.Instance.MiniMapNoteWidth = Preferences.DefaultMiniMapNoteWidth;
				Preferences.Instance.MiniMapNoteSpacing = Preferences.DefaultMiniMapNoteSpacing;
				Preferences.Instance.MiniMapPosition = Preferences.DefaultMiniMapPosition;
				Preferences.Instance.MiniMapSpacingModeForVariable = Preferences.DefaultMiniMapSpacingModeForVariable;
				Preferences.Instance.MiniMapVisibleTimeRange = Preferences.DefaultMiniMapVisibleTimeRange;
				Preferences.Instance.MiniMapVisibleRowRange = Preferences.DefaultMiniMapVisibleRowRange;
				UpdateMiniMapBounds();
				UpdateMiniMapLaneSpacing();
			}

			ImGui.PopItemWidth();

			ImGui.End();
		}

		private void DrawScrollControlUI()
		{
			if (!Preferences.Instance.ShowScrollControlWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Scroll Controls", ref Preferences.Instance.ShowScrollControlWindow, ImGuiWindowFlags.NoScrollbar);

			ImGui.Checkbox("Stop Playback When Scrolling", ref Preferences.Instance.StopPlaybackWhenScrolling);

			ComboFromEnum("Scroll Mode", ref Preferences.Instance.ScrollMode);
			ImGui.SameLine();
			HelpMarker("The Scroll Mode to use when editing. When playing the Scroll Mode is always Time."
			           + "\nTime: Scrolling moves time."
			           + "\nRow:  Scrolling moves rows.");

			ComboFromEnum("Spacing Mode", ref Preferences.Instance.SpacingMode);
			ImGui.SameLine();
			HelpMarker("How events in the Chart should be spaced when rendering."
			           + "\nConstant Time: Events are spaced by their time."
			           + "\n               Equivalent to a CMOD when scrolling by time."
			           + "\nConstant Row:  Spacing is based on row and rows are treated as always the same distance apart."
			           + "\n               Scroll rate modifiers are ignored."
			           + "\n               Other rate altering events like stops and tempo changes affect the scroll rate."
			           + "\nVariable:      Spacing is based on tempo and is affected by all rate altering events."
			           + "\n               Equivalent to a XMOD when scrolling by time.");

			ImGui.Separator();
			ImGui.Text("Constant Time Spacing Options");
			ImGui.SliderFloat("Speed###Time", ref Preferences.Instance.TimeBasedPixelsPerSecond, 1.0f, 100000.0f, null,
				ImGuiSliderFlags.Logarithmic);
			ImGui.SameLine();
			if (ImGui.Button("Reset##TimeSpeed"))
			{
				Preferences.Instance.TimeBasedPixelsPerSecond = Preferences.DefaultTimeBasedPixelsPerSecond;
			}

			ImGui.SameLine();
			HelpMarker("Speed in pixels per second at default zoom level.");

			ImGui.Separator();
			ImGui.Text("Constant Row Spacing Options");
			ImGui.SliderFloat("Spacing", ref Preferences.Instance.RowBasedPixelsPerRow, 0.05f, 100.0f, null,
				ImGuiSliderFlags.Logarithmic);
			ImGui.SameLine();
			if (ImGui.Button("Reset##RowDistance"))
			{
				Preferences.Instance.RowBasedPixelsPerRow = Preferences.DefaultRowBasedPixelsPerRow;
			}

			ImGui.SameLine();
			HelpMarker(
				$"Spacing in pixels per row at default zoom level. A row is 1/{SMCommon.MaxValidDenominator} of a {SMCommon.NumBeatsPerMeasure}/{SMCommon.NumBeatsPerMeasure} beat.");

			ImGui.Separator();
			ImGui.Text("Variable Spacing Options");
			ImGui.SliderFloat("Speed###Variable", ref Preferences.Instance.VariablePixelsPerSecondAtDefaultBPM, 1.0f, 100000.0f,
				null, ImGuiSliderFlags.Logarithmic);
			ImGui.SameLine();
			if (ImGui.Button("Reset##VariableSpeed"))
			{
				Preferences.Instance.VariablePixelsPerSecondAtDefaultBPM = Preferences.DefaultVariablePixelsPerSecond;
			}

			ImGui.SameLine();
			HelpMarker($"Speed in pixels per second at default zoom level at {Preferences.DefaultVariableSpeedBPM} BPM.");

			ImGui.Separator();
			ComboFromEnum("Waveform Scroll Mode", ref Preferences.Instance.RowBasedWaveFormScrollMode);
			ImGui.SameLine();
			HelpMarker("How the wave form should scroll when the Chart does not scroll with Constant Time."
			           + "\nCurrent Tempo:          The wave form will match the current tempo, ignoring rate changes."
			           + "\n                        Best option for Charts which have legitimate musical tempo changes."
			           + "\n                        Bad option for sm file stutter gimmicks as they momentarily double the tempo."
			           + "\nCurrent Tempo And Rate: The wave form will match the current tempo and rate."
			           + "\n                        Rates that are less than or equal 0 will be ignored."
			           + "\n                        Best option to match ssc file scroll gimmicks."
			           + "\n                        Bad option for sm file stutter gimmicks as they momentarily double the tempo."
			           + "\nMost Common Tempo:      The wave form will match the most common tempo in the Chart, ignoring rate changes."
			           + "\n                        Best option to achieve smooth scrolling when the Chart is effectively one tempo"
			           + "\n                        but has brief scroll rate gimmicks.");

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

			DragDouble(ref Preferences.Instance.PreviewFadeInTime, "Preview Fade In Time", 0.001f, "%.3f", true, 0.0);
			DragDouble(ref Preferences.Instance.PreviewFadeOutTime, "Preview Fade Out Time", 0.001f, "%.3f", true, 0.0);

			ImGui.End();
		}

		private void DrawChartPropertiesUI()
		{
			if (!Preferences.Instance.ShowChartPropertiesWindow)
				return;
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

				UnloadSongResources();

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
						var song = await reader.LoadAsync(LoadSongCancellationTokenSource.Token);
						if (song == null)
						{
							Logger.Error($"Failed to load {fileName}");
							return;
						}

						Logger.Info($"Loaded {fileName}");

						EditorSong = new EditorSong(
							this,
							System.IO.Path.GetDirectoryName(fileName),
							song,
							GraphicsDevice,
							ImGuiRenderer);

						// Select the best Chart to make active.
						ActiveChart = SelectBestChart(song, chartType, chartDifficultyType);
						LoadSongCancellationTokenSource.Token.ThrowIfCancellationRequested();

						// Set up Tree of EditorEvents for the Chart.
						SetUpEditorEvents();
					}
					catch (OperationCanceledException)
					{
						// Upon cancellation null out the Song and ActiveChart.
						UnloadSongResources();
					}
					finally
					{
						LoadSongCancellationTokenSource.Dispose();
						LoadSongCancellationTokenSource = null;
					}
				}, LoadSongCancellationTokenSource.Token);
				await LoadSongTask;
				if (EditorSong == null)
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
				SetZoom(1.0, true);

				// Start loading music for this Chart.
				OnSongMusicChanged();
				OnSongMusicPreviewChanged();
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
			while (current.MovePrev() && numLanesChecked < ActiveChart.NumInputs)
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

		private string GetFullPathToMusicFile()
		{
			string musicFile = null;

			// If the active chart has a music file defined, use that.
			// TODO: EditorChart value
			//if (ActiveChart != null)
			//	musicFile = ActiveChart.MusicFile;

			// If the active chart does not have a music file defined, fall back to use the song's music file.
			if (string.IsNullOrEmpty(musicFile))
				musicFile = EditorSong?.MusicPath;

			return GetFullPathToSongResource(musicFile);
		}

		private string GetFullPathToMusicPreviewFile()
		{
			return GetFullPathToSongResource(EditorSong?.MusicPreviewPath);
		}

		private string GetFullPathToSongResource(string relativeFile)
		{
			// Most Charts in StepMania use relative paths for their music files.
			// Determine the absolute path.
			string fullPath = null;
			if (!string.IsNullOrEmpty(relativeFile))
			{
				if (System.IO.Path.IsPathRooted(relativeFile))
					fullPath = relativeFile;
				else
					fullPath = Path.Combine(EditorSong.FileDirectory, relativeFile);
			}

			return fullPath;
		}

		private void UnloadSongResources()
		{
			EditorSong = null;
			ActiveChart = null;
			EditorEvents = null;
			RateAlteringEventsBySongTime = null;
			RateAlteringEventsByRow = null;
			InterpolatedScrollRateEvents = null;
			ActionQueue.Clear();
		}

		#endregion Loading

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
			var firstTempo = true;
			var firstScrollRate = true;
			TimeSignature lastTimeSignature = null;
			var timePerTempo = new Dictionary<double, long>();
			var lastTempoChangeTime = 0L;
			for (var eventIndex = 0; eventIndex < ActiveChart.Layers[0].Events.Count; eventIndex++)
			{
				var chartEvent = ActiveChart.Layers[0].Events[eventIndex];

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

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstTempo)
					{
						foreach (var previousRateAlteringEvent in rateAlteringEventsBySongTime)
						{
							previousRateAlteringEvent.RowsPerSecond = newRowsPerSecond;
							previousRateAlteringEvent.SecondsPerRow = newSecondsPerRow;
							previousRateAlteringEvent.Tempo = tc.TempoBPM;
						}
					}

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
						Tempo = tc.TempoBPM,
						LastTimeSignature = lastTimeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);

					if (!firstTempo)
					{
						timePerTempo.TryGetValue(lastTempo, out var currentTempoTime);
						timePerTempo[lastTempo] = currentTempoTime + tc.TimeMicros - lastTempoChangeTime;
						lastTempoChangeTime = tc.TimeMicros;
					}

					previousEvent = newEvent;
					lastTempo = tc.TempoBPM;
					firstTempo = false;
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
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious +
						                             (stop.LengthMicros / 1000000.0),
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = lastTempo,
						LastTimeSignature = lastTimeSignature,
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
						Tempo = lastTempo,
						LastTimeSignature = lastTimeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
				}
				else if (chartEvent is ScrollRate scrollRate)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstScrollRate)
					{
						foreach (var previousRateAlteringEvent in rateAlteringEventsBySongTime)
						{
							previousRateAlteringEvent.ScrollRate = scrollRate.Rate;
						}
					}

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
						Tempo = lastTempo,
						LastTimeSignature = lastTimeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
					lastScrollRate = scrollRate.Rate;
					firstScrollRate = false;
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
				else if (chartEvent is TimeSignature timeSignature)
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
						ScrollRate = lastScrollRate,
						Tempo = lastTempo,
						LastTimeSignature = timeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					lastTimeSignature = timeSignature;
				}
			}

			if (previousEvent.ChartEvent != null)
			{
				timePerTempo.TryGetValue(lastTempo, out var lastTempoTime);
				timePerTempo[lastTempo] = lastTempoTime + previousEvent.ChartEvent.TimeMicros - lastTempoChangeTime;
			}

			var longestTempoTime = -1L;
			var mostCommonTempo = 0.0;
			foreach (var kvp in timePerTempo)
			{
				if (kvp.Value > longestTempoTime)
				{
					longestTempoTime = kvp.Value;
					mostCommonTempo = kvp.Key;
				}
			}

			EditorEvents = editorEvents;
			RateAlteringEventsBySongTime = rateAlteringEventsBySongTime;
			RateAlteringEventsByRow = rateAlteringEventsByRow;
			InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
			CurrentChartMostCommonTempo = mostCommonTempo;
		}

		private bool TryGetChartPositionFromSongTime(double songTime, ref double chartPosition)
		{
			if (RateAlteringEventsBySongTime == null)
				return false;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator =
				RateAlteringEventsBySongTime.FindGreatestPreceding(new EditorRateAlteringEvent { SongTime = songTime });
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

		private void OnUndo()
		{
			var action = ActionQueue.Undo();
		}

		private void OnRedo()
		{
			var action = ActionQueue.Redo();
		}

		private void OnOpen()
		{
			OpenSongFile();
		}

		private void OnTogglePlayback()
		{
			if (Playing)
				StopPlayback();
			else
				StartPlayback();
		}

		public void OnTogglePlayPreview()
		{
			if (Playing)
				StopPlayback();

			if (!PlayingPreview)
				StartPreview();
			else
				StopPreview();
		}

		public void StartPreview()
		{
			var success = MusicManager.StartPreviewPlayback();
			if (success)
				PlayingPreview = true;
		}

		public void StopPreview()
		{
			if (!PlayingPreview)
				return;

			MusicManager.StopPreviewPlayback();
			PlayingPreview = false;
		}

		public bool IsPlayingPreview()
		{
			return PlayingPreview;
		}

		public void OnSongMusicChanged()
		{
			MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), GetSongTime);
		}

		public void OnSongMusicPreviewChanged()
		{
			StopPreview();
			MusicManager.LoadMusicPreviewAsync(GetFullPathToMusicPreviewFile());
		}

		private double GetMusicOffset()
		{
			// TODO: EditorChart value
			//if (ActiveChart != null)
			//	return ActiveChart.ChartOffsetFromMusic;
			return EditorSong.MusicOffset;
		}

		private double GetSongTime()
		{
			return SongTime;
		}
	}
}
