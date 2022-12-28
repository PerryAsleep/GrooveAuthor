using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fumen.ChartDefinition;
using Fumen.Converters;
using static Fumen.Utils;
using StepManiaLibrary;
using static StepManiaLibrary.Constants;
using static StepManiaEditor.Utils;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Path = Fumen.Path;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using static Fumen.Converters.SMCommon;
using System.Text;
using System.Media;
using System.IO;
using System.Linq;

namespace StepManiaEditor
{
	internal sealed class Editor : Game
	{
		/// <summary>
		/// How to space Chart Events when rendering.
		/// </summary>
		public enum SpacingMode
		{
			/// <summary>
			/// Spacing between notes is based on time.
			/// When playing, this is effectively a CMOD.
			/// </summary>
			ConstantTime,

			/// <summary>
			/// Spacing between notes is based on row.
			/// </summary>
			ConstantRow,

			/// <summary>
			/// Spacing between nodes varies based on rate altering Events in the Chart.
			/// When playing, this is effectively an XMOD.
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

		public class SnapData
		{
			public int Rows;
			public string Texture;
		}
		private SnapData[] SnapLevels;
		private int SnapIndex = 0;

		private EditorMouseState EditorMouseState = new EditorMouseState();
		private bool CanShowRightClickPopupThisFrame = false;

		private bool MovingFocalPoint;
		private Vector2 FocalPointAtMoveStart = new Vector2();
		private Vector2 FocalPointMoveOffset = new Vector2();

		private string PendingOpenFileName;
		private ChartType PendingOpenFileChartType;
		private ChartDifficultyType PendingOpenFileChartDifficultyType;

		private bool UnsavedChangesLastFrame = false;
		private bool ShowSavePopup = false;
		private Action PostSaveFunction = null;
		private int OpenRecentIndex = 0;

		public static readonly ChartType[] SupportedChartTypes = new[]
		{
			ChartType.dance_single,
			ChartType.dance_double,
			//dance_couple,
			//dance_routine,
			ChartType.dance_solo,
			ChartType.dance_threepanel,

			ChartType.pump_single,
			ChartType.pump_halfdouble,
			ChartType.pump_double,
			//pump_couple,
			//pump_routine,
			ChartType.smx_beginner,
			ChartType.smx_single,
			ChartType.smx_dual,
			ChartType.smx_full,
			//ChartType.smx_team,
		};

		private GraphicsDeviceManager Graphics;
		private SpriteBatch SpriteBatch;
		private ImGuiRenderer ImGuiRenderer;
		private WaveFormRenderer WaveFormRenderer;
		private SoundManager SoundManager;
		private MusicManager MusicManager;
		private MiniMap MiniMap;
		private ArrowGraphicManager ArrowGraphicManager;
		private UISongProperties UISongProperties;
		private UIChartProperties UIChartProperties;
		private UIChartList UIChartList;
		private UIWaveFormPreferences UIWaveFormPreferences;
		private UIScrollPreferences UIScrollPreferences;
		private UIMiniMapPreferences UIMiniMapPreferences;
		private UIReceptorPreferences UIReceptorPreferences;
		private UIOptions UIOptions;
		private UIChartPosition UIChartPosition;

		private TextureAtlas TextureAtlas;

		private Effect FxaaEffect;
		private Effect WaveformColorEffect;
		private RenderTarget2D[] WaveformRenderTargets;

		private CancellationTokenSource LoadSongCancellationTokenSource;
		private Task LoadSongTask;

		private Dictionary<ChartType, PadData> PadDataByChartType = new Dictionary<ChartType, PadData>();
		private Dictionary<ChartType, StepGraph> StepGraphByChartType = new Dictionary<ChartType, StepGraph>();

		private double PlaybackStartTime;
		private Stopwatch PlaybackStopwatch;

		private EditorSong ActiveSong;
		private EditorChart ActiveChart;

		private List<EditorEvent> VisibleEvents = new List<EditorEvent>();
		private List<EditorMarkerEvent> VisibleMarkers = new List<EditorMarkerEvent>();
		private List<IChartRegion> VisibleRegions = new List<IChartRegion>();
		private EditorPreviewRegionEvent VisiblePreview = null;
		private SelectedRegion SelectedRegion = new SelectedRegion();
		private HashSet<EditorEvent> SelectedEvents = new HashSet<EditorEvent>();
		private EditorEvent LastSelectedEvent;
		private Receptor[] Receptors = null;
		private EventSpacingHelper SpacingHelper;

		private EditorEvent[] NextAutoPlayNotes = null;

		private double WaveFormPPS = 1.0;

		// Movement controls.
		private EditorPosition Position;
		private bool UpdatingSongTimeDirectly;
		private double SongTimeInterpolationTimeStart = 0.0;
		private double SongTimeAtStartOfInterpolation = 0.0;
		private double DesiredSongTime = 0.0;

		// Note controls
		private LaneEditState[] LaneEditStates;

		// Zoom controls
		public const double MinZoom = 0.000001;
		public const double MaxZoom = 1000000.0;
		private double ZoomInterpolationTimeStart = 0.0;
		private double Zoom = 1.0;
		private double ZoomAtStartOfInterpolation = 1.0;
		private double DesiredZoom = 1.0;

		private KeyCommandManager KeyCommandManager;
		private bool Playing = false;
		private bool PlayingPreview = false;
		private bool MiniMapCapturingMouse = false;
		private bool StartPlayingWhenMiniMapDone = false;

		private uint MaxScreenHeight;

		// Fonts
		private ImFontPtr ImGuiFont;
		private SpriteFont MonogameFont_MPlus1Code_Medium;

		// Cursor
		private Cursor CurrentDesiredCursor = Cursors.Default;
		private Cursor PreviousDesiredCursor = Cursors.Default;

		// Debug
		private bool ParallelizeUpdateLoop = false;
		private bool RenderChart = true;
		private double UpdateTimeTotal;
		private double UpdateTimeWaveForm;
		private double UpdateTimeMiniMap;
		private double UpdateTimeChartEvents;
		private double DrawTimeTotal;

		// Logger
		private readonly LinkedList<Logger.LogMessage> LogBuffer = new LinkedList<Logger.LogMessage>();
		private readonly object LogBufferLock = new object();

		private bool ShowImGuiTestWindow = true;

		public Editor()
		{
			// Create a logger first so we can log any startup messages.
			CreateLogger();

			// Load Preferences synchronously so they can be used immediately.
			Preferences.Load(this);

			Position = new EditorPosition(OnPositionChanged);
			SoundManager = new SoundManager();
			MusicManager = new MusicManager(SoundManager);

			Graphics = new GraphicsDeviceManager(this);
			Graphics.GraphicsProfile = GraphicsProfile.HiDef;

			KeyCommandManager = new KeyCommandManager();
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Z }, OnUndo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Z }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Y }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.O }, OnOpen, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.S }, OnSaveAs, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.S }, OnSave, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.N }, OnNew, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.R }, OnReload, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.A }, OnSelectAll, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftAlt, Keys.A }, OnSelectAllAlt, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.A }, OnSelectAllShift, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Space }, OnTogglePlayback, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.P }, OnTogglePlayPreview, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Escape }, OnEscape, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Left }, OnDecreaseSnap, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Right }, OnIncreaseSnap, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Up }, OnMoveUp, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Down }, OnMoveDown, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.PageUp }, OnMoveToPreviousMeasure, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.PageDown }, OnMoveToNextMeasure, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Home }, OnMoveToChartStart, false, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.End }, OnMoveToChartEnd, false, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Delete }, OnDelete, false, null, true));

			var arrowInputKeys = new[] { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };
			var index = 0;
			foreach (var key in arrowInputKeys)
			{
				var capturedIndex = index;
				void Down() { OnLaneInputDown(capturedIndex); }
				void Up() { OnLaneInputUp(capturedIndex); }
				index++;
				KeyCommandManager.Register(new KeyCommandManager.Command(new[] { key }, Down, false, Up, true));
			}
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftShift }, OnShiftDown, false, OnShiftUp, true));

			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			Window.AllowUserResizing = true;
			Window.ClientSizeChanged += OnResize;

			IsFixedTimeStep = false;
			Graphics.SynchronizeWithVerticalRetrace = true;

			// Set up snap levels for all valid denominators.
			SnapLevels = new SnapData[ValidDenominators.Length + 1];
			SnapLevels[0] = new SnapData { Rows = 0 };
			for (var denominatorIndex = 0; denominatorIndex < ValidDenominators.Length; denominatorIndex++)
			{
				SnapLevels[denominatorIndex + 1] = new SnapData
				{
					Rows = MaxValidDenominator / ValidDenominators[denominatorIndex],
					Texture = ArrowGraphicManager.GetSnapIndicatorTexture(ValidDenominators[denominatorIndex])
				};
			}

			UpdateWindowTitle();
		}

		private void CreateLogger()
		{
			var programPath = System.Reflection.Assembly.GetEntryAssembly().Location;
			var programDir = System.IO.Path.GetDirectoryName(programPath);
			var appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
			var logDirectory = Path.Combine(programDir, "logs");

			var canLogToFile = true;
			var logToFileError = "";
			try
			{
				// Make a log directory if one doesn't exist.
				Directory.CreateDirectory(logDirectory);

				// Start the logger and write to disk as well as the internal buffer to display.
				var logFileName = appName + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".log";
				var logFilePath = Path.Combine(logDirectory, logFileName);
				Logger.StartUp(new Logger.Config
				{
					WriteToConsole = false,

					WriteToFile = true,
					LogFilePath = logFilePath,
					LogFileFlushIntervalSeconds = 20,
					LogFileBufferSizeBytes = 10240,

					WriteToBuffer = true,
					BufferSize = 1024,
					BufferLock = LogBufferLock,
					Buffer = LogBuffer
				});
			}
			catch (Exception e)
			{
				canLogToFile = false;
				logToFileError = e.ToString();
			}

			// If we can't log to a file just log to the internal buffer.
			if (!canLogToFile)
			{
				Logger.StartUp(new Logger.Config
				{
					WriteToConsole = false,
					WriteToFile = false,

					WriteToBuffer = true,
					BufferSize = 1024,
					BufferLock = LogBufferLock,
					Buffer = LogBuffer
				});

				// Log an error that we were enable to log to a file.
				Logger.Error($"Unable to log to disk. {logToFileError}");
			}

			// Clean up old log files.
			try
			{
				var files = Directory.GetFiles(logDirectory);
				var allLogFiles = new List<FileInfo>();
				foreach (var file in files)
				{
					var fi = new FileInfo(file);
					if (fi.Extension != ".log")
						continue;
					allLogFiles.Add(fi);
				}
				if (allLogFiles.Count > MaxLogFiles)
				{
					allLogFiles = allLogFiles.OrderByDescending(fi => fi.CreationTime).ToList();
					allLogFiles.RemoveRange(0, MaxLogFiles);
					foreach (var fi in allLogFiles)
					{
						System.IO.File.Delete(fi.FullName);
					}
				}
			}
			catch (Exception e)
			{
				Logger.Warn($"Unable to clean up old log files. {e}");
			}
		}

		protected override void Initialize()
		{
			var p = Preferences.Instance;
			Graphics.PreferredBackBufferHeight = p.WindowHeight;
			Graphics.PreferredBackBufferWidth = p.WindowWidth;
			Graphics.IsFullScreen = p.WindowFullScreen;
			Graphics.ApplyChanges();

			if (p.WindowMaximized)
			{
				((Form)Control.FromHandle(Window.Handle)).WindowState = FormWindowState.Maximized;
			}

			((Form)Form.FromHandle(Window.Handle)).FormClosing += ClosingForm;

			var guiScale = GetDpiScale();
			ImGuiRenderer = new ImGuiRenderer(this);
			var programPath = System.Reflection.Assembly.GetEntryAssembly().Location;
			var programDir = System.IO.Path.GetDirectoryName(programPath);
			var mPlusFontPath = Path.Combine(programDir, @"Content\Mplus1Code-Medium.ttf");
			ImGuiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(mPlusFontPath, (int)(15 * guiScale), null, ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
			ImGuiRenderer.RebuildFontAtlas();
			ImGuiLayoutUtils.SetFont(ImGuiFont);
			if (!guiScale.DoubleEquals(1.0))
				ImGui.GetStyle().ScaleAllSizes((float)guiScale);

			MonogameFont_MPlus1Code_Medium = Content.Load<SpriteFont>("mplus1code-medium");

			foreach (var adapter in GraphicsAdapter.Adapters)
			{
				MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
			}

			p.PreferencesReceptors.ClampViewportPositions();

			EditorEvent.SetScreenHeight(MaxScreenHeight);

			WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, MaxScreenHeight);
			WaveFormRenderer.SetColors(WaveFormColorDense, WaveFormColorSparse);
			WaveFormRenderer.SetXPerChannelScale(p.PreferencesWaveForm.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetSoundMipMap(MusicManager.GetMusicMipMap());
			WaveFormRenderer.SetFocalPointY(GetFocalPointY());

			WaveformRenderTargets = new RenderTarget2D[2];
			for (int i = 0; i < 2; i++)
			{
				WaveformRenderTargets[i] = new RenderTarget2D(
					GraphicsDevice,
					WaveFormTextureWidth,
					(int)MaxScreenHeight,
					false,
					GraphicsDevice.PresentationParameters.BackBufferFormat,
					DepthFormat.Depth24);
			}

			MiniMap = new MiniMap(GraphicsDevice, new Rectangle(0, 0, 0, 0));
			MiniMap.SetSelectMode(p.PreferencesMiniMap.MiniMapSelectMode);

			TextureAtlas = new TextureAtlas(GraphicsDevice, 2048, 2048, 1);

			UISongProperties = new UISongProperties(this, GraphicsDevice, ImGuiRenderer);
			UIChartProperties = new UIChartProperties();
			UIChartList = new UIChartList(this);
			UIWaveFormPreferences = new UIWaveFormPreferences(MusicManager);
			UIScrollPreferences = new UIScrollPreferences();
			UIMiniMapPreferences = new UIMiniMapPreferences();
			UIReceptorPreferences = new UIReceptorPreferences(this);
			UIOptions = new UIOptions();
			UIChartPosition = new UIChartPosition(this);

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
			foreach (var textureId in ArrowGraphicManager.GetAllTextureIds())
			{
				var texture = Content.Load<Texture2D>(textureId);
				TextureAtlas.AddTexture(textureId, texture, true);

				texture = ArrowGraphicManager.GenerateSelectedTexture(GraphicsDevice, texture);
				if (texture != null)
				{
					TextureAtlas.AddTexture(ArrowGraphicManager.GetSelectedTextureId(textureId), texture, true);
				}
			}

			// Generate and add measure marker texture.
			var measureMarkerTexture = new Texture2D(GraphicsDevice, MarkerTextureWidth, 1);
			var textureData = new uint[MarkerTextureWidth];
			for (var i = 0; i < MarkerTextureWidth; i++)
				textureData[i] = 0xFFFFFFFF;
			measureMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdMeasureMarker, measureMarkerTexture, true);

			// Generate and add beat marker texture.
			var beatMarkerTexture = new Texture2D(GraphicsDevice, MarkerTextureWidth, 1);
			for (var i = 0; i < MarkerTextureWidth; i++)
				textureData[i] = 0xFF7F7F7F;
			beatMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdBeatMarker, beatMarkerTexture, true);

			// Generate and add generic region rect texture.
			var regionRectTexture = new Texture2D(GraphicsDevice, 1, 1);
			textureData = new uint[1];
			textureData[0] = 0xFFFFFFFF;
			regionRectTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdRegionRect, regionRectTexture, true);

			InitPadDataAndStepGraphsAsync();

			// If we have a saved file to open, open it now.
			if (Preferences.Instance.PreferencesOptions.OpenLastOpenedFileOnLaunch
				&& Preferences.Instance.RecentFiles.Count > 0)
			{
				OpenRecentIndex = 0;
				OnOpenRecentFile();
			}

			FxaaEffect = Content.Load<Effect>("fxaa");
			WaveformColorEffect = Content.Load<Effect>("waveform-color");

			base.LoadContent();
		}

		public void OnResize(object sender, EventArgs e)
		{
			var maximized = ((Form)Control.FromHandle(Window.Handle)).WindowState == FormWindowState.Maximized;
			var w = GetViewportWidth();
			var h = GetViewportHeight();

			// Update window preferences.
			if (!maximized)
			{
				Preferences.Instance.WindowWidth = w;
				Preferences.Instance.WindowHeight = h;
			}

			Preferences.Instance.WindowFullScreen = Graphics.IsFullScreen;
			Preferences.Instance.WindowMaximized = maximized;

			// Update focal point.
			Preferences.Instance.PreferencesReceptors.ClampViewportPositions();
		}

		public int GetViewportWidth()
		{
			return Graphics.GraphicsDevice.Viewport.Width;
		}

		public int GetViewportHeight()
		{
			return Graphics.GraphicsDevice.Viewport.Height;
		}

		public int GetFocalPointX()
		{
			if (Preferences.Instance.PreferencesReceptors.CenterHorizontally)
				return GetViewportWidth() >> 1;
			return Preferences.Instance.PreferencesReceptors.PositionX;
		}

		public int GetFocalPointY()
		{
			return Preferences.Instance.PreferencesReceptors.PositionY;
		}

		public Vector2 GetFocalPoint()
		{
			return new Vector2(GetFocalPointX(), GetFocalPointY());
		}

		protected override void Update(GameTime gameTime)
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			ProcessInput(gameTime);

			TextureAtlas.Update();

			if (!Playing)
			{
				if (!Position.SongTime.DoubleEquals(DesiredSongTime))
				{
					UpdatingSongTimeDirectly = true;

					Position.SongTime = Interpolation.Lerp(
						SongTimeAtStartOfInterpolation,
						DesiredSongTime,
						SongTimeInterpolationTimeStart,
						SongTimeInterpolationTimeStart + 0.1,
						gameTime.TotalGameTime.TotalSeconds);

					MusicManager.SetMusicTimeInSeconds(Position.SongTime);

					UpdatingSongTimeDirectly = false;
				}
			}

			if (!Zoom.DoubleEquals(DesiredZoom))
			{
				SetZoom(Interpolation.Lerp(
					ZoomAtStartOfInterpolation,
					DesiredZoom,
					ZoomInterpolationTimeStart,
					ZoomInterpolationTimeStart + 0.1,
					gameTime.TotalGameTime.TotalSeconds), false);
			}

			var pOptions = Preferences.Instance.PreferencesOptions;
			MusicManager.SetPreviewParameters(
				ActiveSong?.SampleStart ?? 0.0,
				ActiveSong?.SampleLength ?? 0.0,
				pOptions.PreviewFadeInTime,
				pOptions.PreviewFadeOutTime);

			if (Playing)
			{
				UpdatingSongTimeDirectly = true;

				Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

				MusicManager.Update(Position.SongTime);
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
					if (Position.SongTime >= 0.0 && Position.SongTime < MusicManager.GetMusicLengthInSeconds())
					{
						if (Position.SongTime - fmodSongTime > maxDeviation)
						{
							PlaybackStartTime -= (0.5 * maxDeviation);
							Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
						}
						else if (fmodSongTime - Position.SongTime > maxDeviation)
						{
							PlaybackStartTime += (0.5 * maxDeviation);
							Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
						}
					}
				}

				DesiredSongTime = Position.SongTime;

				UpdatingSongTimeDirectly = false;
			}
			else
			{
				MusicManager.Update(Position.SongTime);
			}

			Action timedUpdateChartEvents = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateChartEvents();
				UpdateAutoPlay();
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

			if (Receptors != null)
			{
				foreach (var receptor in Receptors)
				{
					receptor.Update(Playing, Position.ChartPosition);
				}
			}

			var hasUnsavedChanges = ActionQueue.Instance.HasUnsavedChanges();
			if (UnsavedChangesLastFrame != hasUnsavedChanges)
			{
				UnsavedChangesLastFrame = hasUnsavedChanges;
				UpdateWindowTitle();
			}

			base.Update(gameTime);

			stopWatch.Stop();
			UpdateTimeTotal = stopWatch.Elapsed.TotalSeconds;
		}

		#region Input Processing

		/// <summary>
		/// Process keyboard and mouse input.
		/// </summary>
		/// <param name="gameTime">Current GameTime.</param>
		private void ProcessInput(GameTime gameTime)
		{
			var inFocus = IsApplicationFocused();

			CurrentDesiredCursor = Cursors.Default;
			CanShowRightClickPopupThisFrame = false;

			SelectedRegion.UpdateTime(gameTime.TotalGameTime.TotalSeconds);

			// ImGui needs to update its frame even the app is not in focus.
			// There may be a way to decouple input processing and advancing the frame, but for now
			// use the ImGuiRenderer.Update method and give it a flag so it knows to not process input.
			(var imGuiWantMouse, var imGuiWantKeyboard) = ImGuiRenderer.Update(gameTime, inFocus);

			// Do not do any further input processing if the application does not have focus.
			if (!inFocus)
				return;

			// Process Keyboard Input.
			if (imGuiWantKeyboard)
				KeyCommandManager.CancelAllCommands();
			else
				KeyCommandManager.Update(gameTime.TotalGameTime.TotalSeconds);

			// Process Mouse Input.
			var state = Mouse.GetState();
			var (mouseChartTime, mouseChartPosition) = FindChartTimeAndRowForScreenY(state.Y);
			EditorMouseState.UpdateMouseState(state, mouseChartTime, mouseChartPosition);

			// Early out if ImGui is using the mouse.
			if (imGuiWantMouse)
			{
				// ImGui may want the mouse on a release when we are selecting. Stop selecting in that case.
				if (SelectedRegion.IsActive())
					FinishSelectedRegion();
				return;
			}

			var inReceptorArea = Receptor.IsInReceptorArea(
				EditorMouseState.X(),
				EditorMouseState.Y(),
				GetFocalPoint(),
				GetSizeZoom(),
				TextureAtlas,
				ArrowGraphicManager,
				ActiveChart);

			// Process input for the mini map.
			if (!SelectedRegion.IsActive() && !MovingFocalPoint)
				ProcessInputForMiniMap();

			// Process input for grabbing the receptors and moving the focal point.
			if (!SelectedRegion.IsActive() && !MiniMapCapturingMouse)
			{
				ProcessInputForMovingFocalPoint(inReceptorArea);
				// Update cursor based on whether the receptors could be grabbed.
				if (inReceptorArea && !Preferences.Instance.PreferencesReceptors.LockPosition)
					CurrentDesiredCursor = Cursors.SizeAll;
			}

			// Process input for selecting a region.
			if (!MiniMapCapturingMouse && !MovingFocalPoint)
				ProcessInputForSelectedRegion(gameTime);

			// Process right click popup eligibility.
			CanShowRightClickPopupThisFrame = (!MiniMapCapturingMouse && !MovingFocalPoint && !MovingFocalPoint);

			// Setting the cursor every frame prevents it from changing to support normal application
			// behavior like indicating resizability at the edges of the window. But not setting every frame
			// causes it to go back to the Default. Set it every frame only if it setting it to something
			// other than the Default.
			if (CurrentDesiredCursor != PreviousDesiredCursor || CurrentDesiredCursor != Cursors.Default)
			{
				Cursor.Current = CurrentDesiredCursor;
			}
			PreviousDesiredCursor = CurrentDesiredCursor;

			// Process input for scrolling and zooming.
			ProcessInputForScrollingAndZooming(gameTime);
		}

		/// <summary>
		/// Processes input for moving the focal point with the mouse.
		/// </summary>
		/// <remarks>Helper for ProcessInput.</remarks>
		private void ProcessInputForMovingFocalPoint(bool inReceptorArea)
		{
			// Begin moving focal point.
			if (EditorMouseState.LeftClickDownThisFrame()
				&& inReceptorArea
				&& !Preferences.Instance.PreferencesReceptors.LockPosition)
			{
				MovingFocalPoint = true;
				FocalPointAtMoveStart = new Vector2(GetFocalPointX(), GetFocalPointY());
				FocalPointMoveOffset = new Vector2(EditorMouseState.X() - GetFocalPointX(), EditorMouseState.Y() - GetFocalPointY());
			}

			// Move focal point.
			if (MovingFocalPoint)
			{
				var newX = EditorMouseState.X() - (int)FocalPointMoveOffset.X;
				var newY = EditorMouseState.Y() - (int)FocalPointMoveOffset.Y;

				if (KeyCommandManager.IsKeyDown(Keys.LeftShift))
				{
					if (Math.Abs(newX - FocalPointAtMoveStart.X) > Math.Abs(newY - FocalPointAtMoveStart.Y))
					{
						Preferences.Instance.PreferencesReceptors.PositionX = newX;
						Preferences.Instance.PreferencesReceptors.PositionY = (int)FocalPointAtMoveStart.Y;
					}
					else
					{
						Preferences.Instance.PreferencesReceptors.PositionX = (int)FocalPointAtMoveStart.X;
						Preferences.Instance.PreferencesReceptors.PositionY = newY;
					}
				}
				else
				{
					Preferences.Instance.PreferencesReceptors.PositionX = newX;
					Preferences.Instance.PreferencesReceptors.PositionY = newY;
				}
			}

			// Stop moving focal point.
			if (EditorMouseState.LeftReleased() && MovingFocalPoint)
			{
				MovingFocalPoint = false;
				FocalPointMoveOffset = new Vector2();
				ActionQueue.Instance.EnqueueWithoutDoing(new ActionMoveFocalPoint(
					(int)FocalPointAtMoveStart.X,
					(int)FocalPointAtMoveStart.Y,
					Preferences.Instance.PreferencesReceptors.PositionX,
					Preferences.Instance.PreferencesReceptors.PositionY));
			}
		}

		/// <summary>
		/// Processes input for selecting regions with the mouse.
		/// </summary>
		/// <remarks>Helper for ProcessInput.</remarks>
		private void ProcessInputForSelectedRegion(GameTime gameTime)
		{
			// Starting a selection.
			if (EditorMouseState.LeftClickDownThisFrame())
			{
				var y = EditorMouseState.Y();
				var (chartTime, chartPosition) = FindChartTimeAndRowForScreenY(y);
				var xInChartSpace = (EditorMouseState.X() - GetFocalPointX()) / GetSizeZoom();
				SelectedRegion.Start(
					xInChartSpace,
					y,
					chartTime,
					chartPosition,
					GetSizeZoom(),
					GetFocalPointX(),
					gameTime.TotalGameTime.TotalSeconds);
			}

			// Dragging a selection.
			if (EditorMouseState.LeftDown() && SelectedRegion.IsActive())
			{
				var xInChartSpace = (EditorMouseState.X() - GetFocalPointX()) / GetSizeZoom();
				SelectedRegion.UpdatePerFrameValues(xInChartSpace, EditorMouseState.Y(), GetSizeZoom(), GetFocalPointX());
			}

			// Releasing a selection.
			if (EditorMouseState.LeftReleased() && SelectedRegion.IsActive())
			{
				FinishSelectedRegion();
			}
		}

		/// <summary>
		/// Processes input for scrolling and zooming.
		/// </summary>
		/// <remarks>Helper for ProcessInput.</remarks>
		private void ProcessInputForScrollingAndZooming(GameTime gameTime)
		{
			var pScroll = Preferences.Instance.PreferencesScroll;
			var scrollDelta = EditorMouseState.ScrollDeltaSinceLastFrame();
			var scrollShouldZoom = KeyCommandManager.IsKeyDown(Keys.LeftControl);

			// Hack.
			if (KeyCommandManager.IsKeyDown(Keys.OemPlus))
			{
				SetZoom(Zoom * 1.0001, true);
			}
			if (KeyCommandManager.IsKeyDown(Keys.OemMinus))
			{
				SetZoom(Zoom / 1.0001, true);
			}

			// Scrolling
			// TODO: wtf are these values
			if (scrollShouldZoom)
			{
				if (scrollDelta > 0)
				{
					SetDesiredZoom(DesiredZoom * 1.2);
					ZoomInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
					ZoomAtStartOfInterpolation = Zoom;
				}

				if (scrollDelta < 0)
				{
					SetDesiredZoom(DesiredZoom / 1.2);
					ZoomInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
					ZoomAtStartOfInterpolation = Zoom;
				}
			}
			else
			{
				if (scrollDelta > 0)
				{
					var delta = 0.25 * (1.0 / Zoom);
					if (Playing)
					{
						PlaybackStartTime -= delta;
						Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

						if (pScroll.StopPlaybackWhenScrolling)
						{
							StopPlayback();
						}
						else
						{
							MusicManager.SetMusicTimeInSeconds(Position.SongTime);
						}
						UpdateAutoPlayFromScrolling();
					}
					else
					{
						if (SnapLevels[SnapIndex].Rows == 0)
						{
							DesiredSongTime -= delta;
							SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
							SongTimeAtStartOfInterpolation = Position.SongTime;
						}
						else
						{
							OnMoveUp();
						}
					}
				}

				if (scrollDelta < 0)
				{
					var delta = 0.25 * (1.0 / Zoom);
					if (Playing)
					{
						PlaybackStartTime += delta;
						Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

						if (pScroll.StopPlaybackWhenScrolling)
						{
							StopPlayback();
						}
						else
						{
							MusicManager.SetMusicTimeInSeconds(Position.SongTime);
						}
						UpdateAutoPlayFromScrolling();
					}
					else
					{
						if (SnapLevels[SnapIndex].Rows == 0)
						{
							DesiredSongTime += delta;
							SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
							SongTimeAtStartOfInterpolation = Position.SongTime;
						}
						else
						{
							OnMoveDown();
						}
					}
				}
			}
		}

		#endregion Input Processing

		private void StartPlayback()
		{
			if (Playing)
				return;

			StopPreview();

			if (!MusicManager.IsMusicLoaded() || Position.SongTime < 0.0 || Position.SongTime > MusicManager.GetMusicLengthInSeconds())
			{
				PlaybackStartTime = Position.SongTime;
			}
			else
			{
				PlaybackStartTime = MusicManager.GetMusicTimeInSeconds();
			}

			PlaybackStopwatch = new Stopwatch();
			PlaybackStopwatch.Start();
			MusicManager.StartPlayback(Position.SongTime);

			Playing = true;
		}

		private void StopPlayback()
		{
			if (!Playing)
				return;

			PlaybackStopwatch.Stop();
			MusicManager.StopPlayback();
			StopAutoPlay();

			Playing = false;
		}

		private void OnPositionChanged()
		{
			// Update events being edited.
			UpdateLaneEditStatesFromPosition();

			// Update the music time
			if (!UpdatingSongTimeDirectly)
			{
				var songTime = Position.SongTime;
				DesiredSongTime = songTime;
				MusicManager.SetMusicTimeInSeconds(songTime);

				if (Playing)
				{
					PlaybackStartTime = songTime;
					PlaybackStopwatch = new Stopwatch();
					PlaybackStopwatch.Start();
				}
			}
		}

		public void SetZoom(double zoom, bool setDesiredZoom)
		{
			Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
			if (setDesiredZoom)
				SetDesiredZoom(Zoom);
		}

		public void SetDesiredZoom(double desiredZoom)
		{
			DesiredZoom = Math.Clamp(desiredZoom, MinZoom, MaxZoom);
		}

		/// <summary>
		/// Gets the zoom to use for sizing objects.
		/// When zooming in we only zoom the spacing, not the scale of objects.
		/// </summary>
		/// <returns>Zoom level to be used as a multiplier.</returns>
		public double GetSizeZoom()
		{
			return Zoom > 1.0 ? 1.0 : Zoom;
		}

		/// <summary>
		/// Gets the zoom to use for spacing objects.
		/// Objects are spaced one to one with the zoom level.
		/// </summary>
		/// <returns>Zoom level to be used as a multiplier.</returns>
		public double GetSpacingZoom()
		{
			return Zoom;
		}

		#region Drawing

		protected override void Draw(GameTime gameTime)
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			// Draw anything which rendering to custom render targets first.
			PreDrawToRenderTargets();

			DrawBackground();

			DrawWaveForm();

			ImGui.PushFont(ImGuiFont);

			SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

			DrawMiniMap();
			if (RenderChart)
			{
				DrawMeasureMarkers();
				DrawRegions();
				DrawReceptors();
				DrawSnapIndicators();
				DrawChartEvents();
				DrawReceptorForegroundEffects();
				DrawSelectedRegion();
			}

			SpriteBatch.End();

			DrawGui(gameTime);

			ImGui.PopFont();
			ImGuiRenderer.AfterLayout();

			base.Draw(gameTime);

			stopWatch.Stop();
			DrawTimeTotal = stopWatch.Elapsed.TotalSeconds;
		}

		/// <summary>
		/// Performs all draw calls to custom render targets.
		/// After performing all renders, sets the render target to the backbuffer for the final draws.
		/// </summary>
		private void PreDrawToRenderTargets()
		{
			GraphicsDevice.Clear(Color.Transparent);
			PreDrawWaveFormToRenderTargets();
			GraphicsDevice.SetRenderTarget(null);
		}

		/// <summary>
		/// Draws to the waveform custom render targets.
		/// This renders the waveform to WaveformRenderTargets[0] then renders a recoloring pass
		/// to WaveformRenderTargets[1]. DrawWaveForm will finish rendering WaveformRenderTargets[1]
		/// to the backbuffer.
		/// </summary>
		private void PreDrawWaveFormToRenderTargets()
		{
			var p = Preferences.Instance.PreferencesWaveForm;
			if (!p.ShowWaveForm)
				return;

			// Draw the waveform to the first render target.
			GraphicsDevice.SetRenderTarget(WaveformRenderTargets[0]);
			GraphicsDevice.Clear(Color.Transparent);
			SpriteBatch.Begin();
			WaveFormRenderer.Draw(SpriteBatch, 0, 0);
			SpriteBatch.End();

			// Determine the sparse color.
			var sparseColor = p.WaveFormSparseColor;
			switch (p.WaveFormSparseColorOption)
			{
				case UIWaveFormPreferences.SparseColorOption.DarkerDenseColor:
					sparseColor.X = p.WaveFormDenseColor.X * p.WaveFormSparseColorScale;
					sparseColor.Y = p.WaveFormDenseColor.Y * p.WaveFormSparseColorScale;
					sparseColor.Z = p.WaveFormDenseColor.Z * p.WaveFormSparseColorScale;
					sparseColor.W = p.WaveFormDenseColor.W;
					break;
				case UIWaveFormPreferences.SparseColorOption.SameAsDenseColor:
					sparseColor = p.WaveFormDenseColor;
					break;
			}

			// Configure the shader to recolor the waveform.
			WaveformColorEffect.CurrentTechnique = WaveformColorEffect.Techniques["color"];
			WaveformColorEffect.Parameters["bgColor"].SetValue(p.WaveFormBackgroundColor);
			WaveformColorEffect.Parameters["denseColor"].SetValue(p.WaveFormDenseColor);
			WaveformColorEffect.Parameters["sparseColor"].SetValue(sparseColor);

			// Draw the recolored waveform to the second render target.
			GraphicsDevice.SetRenderTarget(WaveformRenderTargets[1]);
			GraphicsDevice.Clear(Color.Transparent);
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, WaveformColorEffect);
			WaveformColorEffect.CurrentTechnique.Passes[0].Apply();
			SpriteBatch.Draw((Texture2D)WaveformRenderTargets[0], new Rectangle(0, 0, WaveformRenderTargets[0].Width, WaveformRenderTargets[0].Height), Color.White);
			SpriteBatch.End();
		}

		private void DrawBackground()
		{
			// If there is no background image, just clear with black and return.
			if (!(ActiveSong?.Background?.GetTexture()?.IsBound() ?? false))
			{
				GraphicsDevice.Clear(Color.Black);
				return;
			}

			// If we have a background texture, clear with the average color of the texture.
			var color = ActiveSong.Background.GetTexture().GetTextureColor();
			var (r, g, b, a) = ToFloats(color);
			GraphicsDevice.Clear(new Color(r, g, b, a));

			// Draw the background texture.
			SpriteBatch.Begin();
			ActiveSong.Background.GetTexture().DrawTexture(SpriteBatch, 0, 0, (uint)GetViewportWidth(), (uint)GetViewportHeight(), TextureLayoutMode.Box);
			SpriteBatch.End();
		}

		private void DrawReceptors()
		{
			if (ActiveChart == null || ArrowGraphicManager == null || Receptors == null)
				return;

			var sizeZoom = GetSizeZoom();
			foreach (var receptor in Receptors)
				receptor.Draw(GetFocalPoint(), sizeZoom, TextureAtlas, SpriteBatch);
		}

		private void DrawSnapIndicators()
		{
			if (ActiveChart == null || ArrowGraphicManager == null || Receptors == null)
				return;
			var snapTextureId = SnapLevels[SnapIndex].Texture;
			if (string.IsNullOrEmpty(snapTextureId))
				return;
			var (receptorTextureId, _) = ArrowGraphicManager.GetReceptorTexture(0);
			var (receptorTextureWidth, _) = TextureAtlas.GetDimensions(receptorTextureId);
			var zoom = GetSizeZoom();
			var receptorLeftEdge = GetFocalPointX() - (ActiveChart.NumInputs * 0.5 * receptorTextureWidth * zoom);

			var (snapTextureWidth, snapTextureHeight) = TextureAtlas.GetDimensions(snapTextureId);
			var leftX = receptorLeftEdge - snapTextureWidth * 0.5 * zoom;
			var y = GetFocalPointY();

			TextureAtlas.Draw(
				snapTextureId,
				SpriteBatch,
				new Vector2((float)leftX, y),
				new Vector2((float)(snapTextureWidth * 0.5), (float)(snapTextureHeight * 0.5)),
				Color.White,
				(float)zoom,
				0.0f,
				SpriteEffects.None);
		}

		private void DrawReceptorForegroundEffects()
		{
			if (ActiveChart == null || ArrowGraphicManager == null || Receptors == null)
				return;

			var sizeZoom = GetSizeZoom();
			foreach (var receptor in Receptors)
				receptor.DrawForegroundEffects(GetFocalPoint(), sizeZoom, TextureAtlas, SpriteBatch);
		}

		private void UpdateWaveFormRenderer()
		{
			var pWave = Preferences.Instance.PreferencesWaveForm;

			// Performance optimization. Do not update the texture if we won't render it.
			if (!pWave.ShowWaveForm)
				return;

			// Update the WaveFormRenderer.
			WaveFormRenderer.SetFocalPointY(GetFocalPointY());
			WaveFormRenderer.SetXPerChannelScale(pWave.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetDenseScale(pWave.DenseScale);
			WaveFormRenderer.SetScaleXWhenZooming(pWave.WaveFormScaleXWhenZooming);
			WaveFormRenderer.Update(Position.SongTime, Zoom, WaveFormPPS);
		}

		private void DrawWaveForm()
		{
			var p = Preferences.Instance.PreferencesWaveForm;
			if (!p.ShowWaveForm)
				return;

			var x = GetFocalPointX() - (WaveFormTextureWidth >> 1);

			// At this point WaveformRenderTargets[1] contains the recolored waveform.
			// We now draw that to the backbuffer with an optional antialiasing pass.

			if (p.AntiAlias)
			{
				// Configure FXAA.
				FxaaEffect.CurrentTechnique = FxaaEffect.Techniques["fxaa"];
				FxaaEffect.Parameters["fxaaQualitySubpix"].SetValue(p.AntiAliasSubpix);
				FxaaEffect.Parameters["fxaaQualityEdgeThreshold"].SetValue(p.AntiAliasEdgeThreshold);
				FxaaEffect.Parameters["fxaaQualityEdgeThresholdMin"].SetValue(p.AntiAliasEdgeThresholdMin);
				FxaaEffect.Parameters["inverseRenderTargetWidth"].SetValue(1.0f / WaveformRenderTargets[1].Width);
				FxaaEffect.Parameters["inverseRenderTargetHeight"].SetValue(1.0f / WaveformRenderTargets[1].Height);
				FxaaEffect.Parameters["renderTargetTexture"].SetValue(WaveformRenderTargets[1]);

				// Draw the recolored waveform with antialiasing to the back buffer.
				SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, FxaaEffect);
				SpriteBatch.Draw(WaveformRenderTargets[1], new Rectangle(x, 0, WaveformRenderTargets[1].Width, WaveformRenderTargets[1].Height), Color.White);
				SpriteBatch.End();
			}
			else
			{
				// Draw the recolored waveform to the back buffer.
				SpriteBatch.Begin();
				SpriteBatch.Draw(WaveformRenderTargets[1], new Rectangle(x, 0, WaveformRenderTargets[1].Width, WaveformRenderTargets[1].Height), Color.White);
				SpriteBatch.End();
			}
		}

		private void DrawMeasureMarkers()
		{
			foreach (var visibleMarker in VisibleMarkers)
			{
				visibleMarker.Draw(TextureAtlas, SpriteBatch, MonogameFont_MPlus1Code_Medium);
			}
		}

		private void DrawRegions()
		{
			foreach (var visibleRegion in VisibleRegions)
			{
				visibleRegion.DrawRegion(TextureAtlas, SpriteBatch, GetViewportHeight());
			}
		}

		private void DrawSelectedRegion()
		{
			if (SelectedRegion.IsActive())
			{
				SelectedRegion.DrawRegion(TextureAtlas, SpriteBatch, GetViewportHeight());
			}
		}

		private void DrawChartEvents()
		{
			var eventsBeingEdited = new List<EditorEvent>();

			// Draw the preview if it is visible.
			// This will only draw the miscellaneous editor event for the preview.
			// The region will be drawn by DrawRegions.
			if (VisiblePreview != null)
				VisiblePreview.Draw();

			foreach (var visibleEvent in VisibleEvents)
			{
				// Capture events being edited to draw after all events not being edited.
				if (visibleEvent.IsBeingEdited())
				{
					eventsBeingEdited.Add(visibleEvent);
					continue;
				}

				if (Playing)
				{
					// Skip events entirely above the receptors.
					if (Preferences.Instance.PreferencesReceptors.AutoPlayHideArrows
						&& visibleEvent.GetChartTime() < Position.ChartTime
						&& (visibleEvent is EditorTapNoteEvent
							|| visibleEvent is EditorHoldStartNoteEvent
							|| visibleEvent is EditorHoldEndNoteEvent))
						continue;

					// Cut off hold end notes which intersect the receptors.
					if (visibleEvent is EditorHoldEndNoteEvent hen)
					{
						if (Playing && hen.GetChartTime() > Position.ChartTime
									&& hen.GetHoldStartNote().GetChartTime() < Position.ChartTime)
						{
							hen.SetNextDrawActive(true, GetFocalPointY());
						}
					}
				}

				// Draw the event.
				visibleEvent.Draw(TextureAtlas, SpriteBatch, ArrowGraphicManager);
			}

			// Draw events being edited.
			foreach (var visibleEvent in eventsBeingEdited)
			{
				visibleEvent.Draw(TextureAtlas, SpriteBatch, ArrowGraphicManager);
			}
		}

		#endregion Drawing

		#region Chart Update

		/// <summary>
		/// Sets VisibleEvents, VisibleMarkers, VisibleRegions, and VisiblePreview to store the currently visible
		/// objects based on the current EditorPosition and the SpacingMode.
		/// Updates SelectedRegion.
		/// </summary>
		/// <remarks>
		/// Sets the WaveFormPPS.
		/// </remarks>
		private void UpdateChartEvents()
		{
			// TODO: Crash when switching songs from doubles to singles.

			// Clear the current state of visible events
			VisibleEvents.Clear();
			VisibleMarkers.Clear();
			VisibleRegions.Clear();
			VisiblePreview = null;
			SelectedRegion.ClearPerFrameData();

			if (ActiveChart == null || ActiveChart.EditorEvents == null || ArrowGraphicManager == null)
				return;

			// Get an EventSpacingHelper to perform y calculations.
			SpacingHelper = EventSpacingHelper.GetSpacingHelper(ActiveChart);

			List<EditorEvent> holdBodyEvents = new List<EditorEvent>();
			List<EditorEvent> noteEvents = new List<EditorEvent>();

			var screenHeight = GetViewportHeight();
			var focalPointX = GetFocalPointX();
			var focalPointY = GetFocalPointY();
			var numArrows = ActiveChart.NumInputs;

			var spacingZoom = GetSpacingZoom();
			var sizeZoom = GetSizeZoom();

			// Determine graphic dimensions based on the zoom level.
			var (arrowTexture, _) = ArrowGraphicManager.GetArrowTexture(0, 0, false);
			double arrowW, arrowH;
			(arrowW, arrowH) = TextureAtlas.GetDimensions(arrowTexture);
			arrowW *= sizeZoom;
			arrowH *= sizeZoom;
			var (holdCapTexture, _) = ArrowGraphicManager.GetHoldEndTexture(0, 0, false, false);
			var (_, holdCapTextureHeight) = TextureAtlas.GetDimensions(holdCapTexture);
			var holdCapHeight = holdCapTextureHeight * sizeZoom;
			if (ArrowGraphicManager.AreHoldCapsCentered())
				holdCapHeight *= 0.5;

			// Determine the starting x and y position in screen space.
			// Y extended slightly above the top of the screen so that we start drawing arrows
			// before their midpoints.
			var startPosX = focalPointX - (numArrows * arrowW * 0.5);
			var startPosY = 0.0 - Math.Max(holdCapHeight, arrowH * 0.5);

			// Set up the MiscEventWidgetLayoutManager.
			var miscEventAlpha = (float)Interpolation.Lerp(1.0, 0.0, MiscEventScaleToStartingFading, MiscEventMinScale, sizeZoom);
			const int widgetStartPadding = 10;
			const int widgetMeasureNumberFudge = 10;
			var lMiscWidgetPos = startPosX
								 - widgetStartPadding
								 + EditorMarkerEvent.GetNumberRelativeAnchorPos(sizeZoom)
								 - EditorMarkerEvent.GetNumberAlpha(sizeZoom) * widgetMeasureNumberFudge;
			var rMiscWidgetPos = focalPointX
								 + (numArrows * arrowW * 0.5) + widgetStartPadding;
			MiscEventWidgetLayoutManager.BeginFrame(lMiscWidgetPos, rMiscWidgetPos);

			// TODO: Fix Negative Scrolls resulting in cutting off notes prematurely.
			// If a chart has negative scrolls then we technically need to render notes which come before
			// the chart position at the top of the screen.
			// More likely the most visible problem will be at the bottom of the screen where if we
			// were to detect the first note which falls below the bottom it would prevent us from
			// finding the next set of notes which might need to be rendered because they appear 
			// above.

			// Get the current time and position.
			var time = Position.ChartTime;
			double chartPosition = 0.0;
			if (!ActiveChart.TryGetChartPositionFromTime(time, ref chartPosition))
				return;

			// Find the interpolated scroll rate to use as a multiplier.
			// The interpolated scroll rate to use is the value at the current exact time.
			var interpolatedScrollRate = GetInterpolatedScrollRate(time, chartPosition);

			// Now, scroll up to the top of the screen so we can start processing events going downwards.
			// We know what time / pos we are drawing at the receptors, but not the rate to get to that time from the top
			// of the screen.
			// We need to find the greatest preceding rate event, and continue until it is beyond the start of the screen.
			// Then we need to find the greatest preceding notes by scanning upwards.
			// Once we find that note, we start iterating downwards while also keeping track of the rate events along the way.

			var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, (int)chartPosition, time);
			if (rateEnumerator == null)
				return;

			// Scan upwards to find the earliest rate altering event that should be used to start rendering.
			var previousRateEventY = (double)focalPointY;
			var previousRateEventRow = chartPosition;
			var previousRateEventTime = time;
			EditorRateAlteringEvent rateEvent = null;
			while (previousRateEventY >= startPosY && rateEnumerator.MovePrev())
			{
				// On the rate altering event which is active for the current chart position,
				// Record the pixels per second to use for the WaveForm.
				if (rateEvent == null)
					SetWaveFormPps(rateEnumerator.Current, interpolatedScrollRate);

				rateEvent = rateEnumerator.Current;
				SpacingHelper.UpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
				previousRateEventY = SpacingHelper.GetYPreceding(previousRateEventTime, previousRateEventRow, previousRateEventY, rateEvent.GetChartTime(), rateEvent.GetRow());
				previousRateEventRow = rateEvent.GetRow();
				previousRateEventTime = rateEvent.GetChartTime();
			}

			// Now we know the position of first rate altering event to use.
			// We can now determine the chart time and position at the top of the screen.
			var (chartTimeAtTopOfScreen, chartPositionAtTopOfScreen) =
				SpacingHelper.GetChartTimeAndRow(startPosY, previousRateEventY, rateEvent.GetChartTime(), rateEvent.GetRow());

			var beatMarkerRow = (int)chartPositionAtTopOfScreen;
			var beatMarkerLastRecordedRow = -1;

			// Now that we know the position at the start of the screen we can find the first event to start rendering.
			var enumerator = ActiveChart.EditorEvents.FindBest(chartPositionAtTopOfScreen);
			if (enumerator == null)
				return;

			// Scan backwards until we have checked every lane for a long note which may
			// be extending through the given start row. We cannot add the end events yet because
			// we do not know at what position they will end until we scan down.
			var holdEndNotesNeedingToBeAdded = new EditorHoldEndNoteEvent[ActiveChart.NumInputs];
			var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
			foreach (var hsn in holdStartNotes)
			{
				// This is technically incorrect.
				// We are using the rate altering event active at the screen, but there could be more
				// rate altering events between the top of the screen and the start of the hold.
				hsn.SetDimensions(
					startPosX + hsn.GetLane() * arrowW,
					SpacingHelper.GetY(hsn, previousRateEventY, rateEvent) - (arrowH * 0.5),
					arrowW,
					arrowH,
					sizeZoom);
				noteEvents.Add(hsn);

				holdEndNotesNeedingToBeAdded[hsn.GetLane()] = hsn.GetHoldEndNote();
			}

			var hasNextRateEvent = rateEnumerator.MoveNext();
			EditorRateAlteringEvent nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

			var regionsNeedingToBeAdded = new List<IChartRegion>();

			// Start any regions including the preview and the selected region.
			StartRegions(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, nextRateEvent, startPosX, numArrows * arrowW, chartTimeAtTopOfScreen, chartPositionAtTopOfScreen, startPosY, miscEventAlpha);

			// Now we can scan forward
			while (enumerator.MoveNext())
			{
				var e = enumerator.Current;

				// Check to see if we have crossed into a new rate altering event section
				if (nextRateEvent != null && e.GetEvent() == nextRateEvent.GetEvent())
				{
					// Add a misc widget for this rate event.
					var rateEventY = SpacingHelper.GetY(e, previousRateEventY, rateEvent);
					nextRateEvent.Alpha = miscEventAlpha;
					MiscEventWidgetLayoutManager.PositionEvent(nextRateEvent, rateEventY);
					noteEvents.Add(nextRateEvent);

					// Add a region for this event if appropriate.
					if (nextRateEvent is IChartRegion region)
						AddRegion(region, ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, startPosX, numArrows * arrowW);

					// Update beat markers for the section for the previous rate event.
					UpdateBeatMarkers(rateEvent, ref beatMarkerRow, ref beatMarkerLastRecordedRow, nextRateEvent, startPosX, sizeZoom, previousRateEventY);

					// Update rate parameters.
					rateEvent = nextRateEvent;
					SpacingHelper.UpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
					previousRateEventY = rateEventY;

					// Advance next rate altering event.
					hasNextRateEvent = rateEnumerator.MoveNext();
					nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

					// Update any regions needing to be updated based on the new rate altering event.
					UpdateRegions(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, nextRateEvent, startPosX, numArrows * arrowW, startPosY, miscEventAlpha);
					
					continue;
				}

				// Determine y position.
				var y = SpacingHelper.GetY(e, previousRateEventY, rateEvent);
				var arrowY = y - (arrowH * 0.5);
				if (arrowY > screenHeight)
					break;

				// Record note.
				if (e is EditorTapNoteEvent || e is EditorHoldStartNoteEvent || e is EditorHoldEndNoteEvent || e is EditorMineNoteEvent)
				{
					var noteY = arrowY;
					var noteH = arrowH;

					if (e is EditorHoldEndNoteEvent hen)
					{
						var start = hen.GetHoldStartNote();
						var endY = y + holdCapHeight;

						noteY = start.Y + arrowH * 0.5f;
						noteH = endY - noteY;

						holdBodyEvents.Add(e);

						// Remove from holdEndNotesNeedingToBeAdded.
						holdEndNotesNeedingToBeAdded[e.GetLane()] = null;
					}
					else if (e is EditorHoldStartNoteEvent hsn)
					{
						// Record that there is in an in-progress hold that will need to be ended.
						holdEndNotesNeedingToBeAdded[e.GetLane()] = hsn.GetHoldEndNote();
						noteEvents.Add(e);
					}
					else
					{
						noteEvents.Add(e);
					}

					e.SetDimensions(startPosX + e.GetLane() * arrowW, noteY, arrowW, noteH, sizeZoom);
				}
				else
				{
					e.Alpha = miscEventAlpha;
					MiscEventWidgetLayoutManager.PositionEvent(e, y);
					noteEvents.Add(e);

					// Add a region for this event if appropriate.
					if (e is IChartRegion region)
						AddRegion(region, ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, startPosX, numArrows * arrowW);
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

				var start = holdEndNote.GetHoldStartNote();
				var endY = SpacingHelper.GetY(holdEndNote, previousRateEventY, rateEvent) + holdCapHeight;
				var noteH = endY - (start.Y + (arrowH * 0.5f));

				holdEndNote.SetDimensions(
					startPosX + holdEndNote.GetLane() * arrowW,
					start.Y + arrowH * 0.5,
					arrowW,
					noteH,
					sizeZoom);

				holdBodyEvents.Add(holdEndNote);
			}

			// We also need to update beat markers beyond the final note.
			UpdateBeatMarkers(rateEvent, ref beatMarkerRow, ref beatMarkerLastRecordedRow, nextRateEvent, startPosX, sizeZoom, previousRateEventY);

			// We also need to complete regions which end beyond the bounds of the screen.
			EndRegions(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent);

			// Store the notes and holds so we can render them.
			VisibleEvents.AddRange(holdBodyEvents);
			VisibleEvents.AddRange(noteEvents);
		}


		/// <summary>
		/// Given a RedBlackTree<EditorRateAlteringEvent> and a value, find the greatest preceding value.
		/// If no value precedes the given value, instead find the least value that follows or is
		/// equal to the given value.
		/// </summary>
		/// <remarks>
		/// This is a common pattern when knowing a position or a time and wanting to find the first event to
		/// start enumerator over for rendering.
		/// </remarks>
		/// <param name="tree">RedBlackTree to search.</param>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		/// <returns>Enumerator to best value or null if a value could not be found.</returns>
		private RedBlackTree<EditorRateAlteringEvent>.Enumerator FindBest(RedBlackTree<EditorRateAlteringEvent> tree, int row, double chartTime)
		{
			var pos = new EditorDummyRateAlteringEvent(ActiveChart, row, chartTime);
			var enumerator = tree.FindGreatestPreceding(pos, false);
			if (enumerator == null)
				enumerator = tree.FindLeastFollowing(pos, true);
			return enumerator;
		}

		/// <summary>
		/// Sets the pixels per second to use on the WaveFormRenderer.
		/// </summary>
		/// <param name="rateEvent">Current rate altering event.</param>
		/// <param name="interpolatedScrollRate">Current interpolated scroll rate.</param>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		void SetWaveFormPps(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate)
		{
			var pScroll = Preferences.Instance.PreferencesScroll;
			switch (pScroll.SpacingMode)
			{
				case SpacingMode.ConstantTime:
					WaveFormPPS = pScroll.TimeBasedPixelsPerSecond;
					break;
				case SpacingMode.ConstantRow:
					WaveFormPPS = pScroll.RowBasedPixelsPerRow * rateEvent.RowsPerSecond;
					if (pScroll.RowBasedWaveFormScrollMode == WaveFormScrollMode.MostCommonTempo)
						WaveFormPPS *= (ActiveChart.MostCommonTempo / rateEvent.Tempo);
					break;
				case SpacingMode.Variable:
					var tempo = ActiveChart.MostCommonTempo;
					if (pScroll.RowBasedWaveFormScrollMode != WaveFormScrollMode.MostCommonTempo)
						tempo = rateEvent.Tempo;
					var useRate = pScroll.RowBasedWaveFormScrollMode ==
									WaveFormScrollMode.CurrentTempoAndRate;
					WaveFormPPS = pScroll.VariablePixelsPerSecondAtDefaultBPM
									* (tempo / PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM);
					if (useRate)
					{
						var rate = rateEvent.ScrollRate * interpolatedScrollRate;
						if (rate <= 0.0)
							rate = 1.0;
						WaveFormPPS *= rate;
					}
					break;
			}
		}

		/// <summary>
		/// Gets the interpolated scroll rate to use for the given Chart time and position.
		/// </summary>
		/// <param name="chartTime">Chart time.</param>
		/// <param name="chartPosition">Chart position.</param>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		/// <returns>Interpolated scroll rate.</returns>
		private double GetInterpolatedScrollRate(double chartTime, double chartPosition)
		{
			// Find the interpolated scroll rate to use as a multiplier.
			// The interpolated scroll rate to use is the value at the current exact time.
			var interpolatedScrollRate = 1.0;
			if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.Variable)
			{
				var ratePosEventForChecking = new EditorInterpolatedRateAlteringEvent(ActiveChart,
					new ScrollRateInterpolation(0.0, 0, 0L, false)
					{
						IntegerPosition = (int)chartPosition,
						TimeMicros = ToMicros(chartTime),
					});

				var interpolatedScrollRateEnumerator =
					ActiveChart.InterpolatedScrollRateEvents.FindGreatestPreceding(ratePosEventForChecking);
				if (interpolatedScrollRateEnumerator != null)
				{
					interpolatedScrollRateEnumerator.MoveNext();
					var interpolatedRateEvent = interpolatedScrollRateEnumerator.Current;
					if (interpolatedRateEvent.InterpolatesByTime())
						interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromTime(chartTime);
					else
						interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromRow(chartPosition);
				}
				else
				{
					interpolatedScrollRateEnumerator = ActiveChart.InterpolatedScrollRateEvents.FindLeastFollowing(ratePosEventForChecking, true);
					if (interpolatedScrollRateEnumerator != null)
					{
						interpolatedScrollRateEnumerator.MoveNext();
						var interpolatedRateEvent = interpolatedScrollRateEnumerator.Current;
						if (interpolatedRateEvent.InterpolatesByTime())
							interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromTime(chartTime);
						else
							interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromRow(chartPosition);
					}
				}
			}

			return interpolatedScrollRate;
		}

		/// <summary>
		/// Given a chart position, scans backwards for hold notes which begin earlier and end later.
		/// </summary>
		/// <param name="enumerator">Enumerator to use for scanning backwards.</param>
		/// <param name="chartPosition">Chart position to use for checking.</param>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		/// <returns>List of EditorHoldStartNotes.</returns>
		private List<EditorHoldStartNoteEvent> ScanBackwardsForHolds(RedBlackTree<EditorEvent>.Enumerator enumerator,
			double chartPosition)
		{
			var lanesChecked = new bool[ActiveChart.NumInputs];
			var numLanesChecked = 0;
			var holds = new List<EditorHoldStartNoteEvent>();
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
							if (e is EditorHoldStartNoteEvent hsn)
							{
								holds.Add(hsn);
							}
						}
					}
				}
			}

			foreach (var editState in LaneEditStates)
			{
				if (!editState.IsActive())
					continue;
				if (!(editState.GetEventBeingEdited() is EditorHoldStartNoteEvent hsn))
					continue;
				if (hsn.GetRow() < chartPosition && hsn.GetRow() + hsn.GetLength() > chartPosition)
					holds.Add(hsn);
			}

			return holds;
		}

		private List<IChartRegion> ScanBackwardsForRegions(double chartPosition, double chartTime)
		{
			var row = (int)chartPosition;
			var regions = new List<IChartRegion>();
			var stop = ActiveChart.GetStopEventOverlapping(row, chartTime);
			if (stop != null)
				regions.Add(stop);
			var delay = ActiveChart.GetDelayEventOverlapping(row, chartTime);
			if (delay != null)
				regions.Add(delay);
			var fake = ActiveChart.GetFakeSegmentEventOverlapping(row, chartTime);
			if (fake != null)
				regions.Add(fake);
			var warp = ActiveChart.GetWarpEventOverlapping(row, chartTime);
			if (warp != null)
				regions.Add(warp);
			return regions;
		}

		private bool DoesPreviewExtendThroughChartTime(double chartTime)
		{
			if (!ActiveSong.IsUsingSongForPreview())
				return false;

			var previewStartChartTime = ActiveSong.PreviewEvent.GetRegionPosition();
			return previewStartChartTime < chartTime && previewStartChartTime + ActiveSong.PreviewEvent.GetRegionDuration() >= chartTime;
		}

		private void CheckForAddingPreviewEvent(
			ref List<IChartRegion> regionsNeedingToBeAdded,
			double previousRateEventY,
			EditorRateAlteringEvent previousRateEvent,
			EditorRateAlteringEvent nextRateEvent,
			double x,
			double w,
			double startPosY,
			float miscEventAlpha)
		{
			if (VisiblePreview != null || !ActiveSong.IsUsingSongForPreview())
				return;

			// Check if the preview time falls within the current rate altering event range.
			var previewTime = ActiveSong.PreviewEvent.GetRegionPosition();
			if (previewTime >= previousRateEvent.GetChartTime()
				&& (nextRateEvent == null || previewTime < nextRateEvent.GetChartTime()))
			{
				AddPreviewEvent(ref regionsNeedingToBeAdded, previousRateEventY, previousRateEvent, x, w, startPosY, miscEventAlpha);
			}
		}

		private void AddPreviewEvent(ref List<IChartRegion> regionsNeedingToBeAdded, double previousRateEventY, EditorRateAlteringEvent previousRateEvent, double x, double w, double startPosY, float miscEventAlpha)
		{
			VisiblePreview = ActiveSong.PreviewEvent;

			// Start a region for the preview.
			var y = SpacingHelper.GetRegionY(VisiblePreview, previousRateEventY, previousRateEvent.GetChartTime(), previousRateEvent.GetRow());
			VisiblePreview.SetRegionX(x);
			VisiblePreview.SetRegionY(y);
			VisiblePreview.SetRegionW(w);
			regionsNeedingToBeAdded.Add(VisiblePreview);

			// The preview also uses a misc event widget.
			VisiblePreview.ShouldDrawMiscEvent = y > startPosY;
			VisiblePreview.Alpha = miscEventAlpha;
			MiscEventWidgetLayoutManager.PositionEvent(ActiveSong.PreviewEvent, y);
		}

		private void AddRegion(IChartRegion region, ref List<IChartRegion> regionsNeedingToBeAdded, double previousRateEventY, EditorRateAlteringEvent rateEvent, double x, double w)
		{
			if (region == null || !region.IsVisible(Preferences.Instance.PreferencesScroll.SpacingMode))
				return;
			if (regionsNeedingToBeAdded.Contains(region))
				return;
			region.SetRegionX(x);
			region.SetRegionY(SpacingHelper.GetRegionY(region, previousRateEventY, rateEvent.GetChartTime(), rateEvent.GetRow()));
			region.SetRegionW(w);
			regionsNeedingToBeAdded.Add(region);
		}

		private void CheckForCompletingRegions(ref List<IChartRegion> regionsNeedingToBeAdded, double previousRateEventY, EditorRateAlteringEvent previousRateEvent, EditorRateAlteringEvent nextRateEvent)
		{
			var remainingRegionsNeededToBeAdded = new List<IChartRegion>();
			foreach (var region in regionsNeedingToBeAdded)
			{
				var regionEnd = region.GetRegionPosition() + region.GetRegionDuration();
				if (nextRateEvent == null ||
					((region.AreRegionUnitsTime() && nextRateEvent.GetChartTime() > regionEnd)
					|| (!region.AreRegionUnitsTime() && nextRateEvent.GetRow() > regionEnd)))
				{
					region.SetRegionH(SpacingHelper.GetRegionH(region, previousRateEventY, previousRateEvent));
					VisibleRegions.Add(region);
					continue;
				}
				remainingRegionsNeededToBeAdded.Add(region);
			}
			regionsNeedingToBeAdded = remainingRegionsNeededToBeAdded;
		}

		private void CheckForUpdatingSelectedRegionStartY(double previousRateEventY, EditorRateAlteringEvent rateEvent, EditorRateAlteringEvent nextRateEvent)
		{
			if (!SelectedRegion.IsActive() || SelectedRegion.HasStartYBeenUpdatedThisFrame())
				return;

			switch (Preferences.Instance.PreferencesScroll.SpacingMode)
			{
				case SpacingMode.ConstantTime:
					{
						if (SelectedRegion.GetStartChartTime() < rateEvent.GetChartTime()
							|| nextRateEvent == null
							|| SelectedRegion.GetStartChartTime() < nextRateEvent.GetChartTime())
						{
							SelectedRegion.UpdatePerFrameDerivedStartY(SpacingHelper.GetY(SelectedRegion.GetStartChartTime(), SelectedRegion.GetStartChartPosition(), previousRateEventY, rateEvent.GetChartTime(), rateEvent.GetRow()));
						}
						break;
					}
				case SpacingMode.ConstantRow:
				case SpacingMode.Variable:
					{
						if (SelectedRegion.GetStartChartPosition() < rateEvent.GetRow()
							|| nextRateEvent == null
							|| SelectedRegion.GetStartChartPosition() < nextRateEvent.GetRow())
						{
							SelectedRegion.UpdatePerFrameDerivedStartY(SpacingHelper.GetY(SelectedRegion.GetStartChartTime(), SelectedRegion.GetStartChartPosition(), previousRateEventY, rateEvent.GetChartTime(), rateEvent.GetRow()));
						}
						break;
					}
			}
		}

		private void CheckForUpdatingSelectedRegionCurrentValues(
			double previousRateEventY,
			EditorRateAlteringEvent rateEvent,
			EditorRateAlteringEvent nextRateEvent)
		{
			if (!SelectedRegion.IsActive() || SelectedRegion.HaveCurrentValuesBeenUpdatedThisFrame())
				return;

			if (SelectedRegion.GetCurrentY() < previousRateEventY
				|| nextRateEvent == null
				|| SelectedRegion.GetCurrentY() < SpacingHelper.GetY(nextRateEvent, previousRateEventY, rateEvent))
			{
				var (chartTime, chartPosition) = SpacingHelper.GetChartTimeAndRow(
					SelectedRegion.GetCurrentY(), previousRateEventY, rateEvent.GetChartTime(), rateEvent.GetRow());
				SelectedRegion.UpdatePerFrameDerivedChartTimeAndPosition(chartTime, chartPosition);
			}
		}

		/// <summary>
		/// Handles starting and updating any pending regions at the start of the main tick loop
		/// when the first rate altering event is known.
		/// Pending regions include normal regions needing to be added to VisibleRegions,
		/// the preview region, and the SelectedRegion.
		/// </summary>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		private void StartRegions(
			ref List<IChartRegion> regionsNeedingToBeAdded,
			double previousRateEventY,
			EditorRateAlteringEvent rateEvent,
			EditorRateAlteringEvent nextRateEvent,
			double chartRegionX,
			double chartRegionW,
			double chartTimeAtTopOfScreen,
			double chartPositionAtTopOfScreen,
			double startPosY,
			float miscEventAlpha)
		{
			// Check for adding the preview if it extends through the top of the screen.
			if (DoesPreviewExtendThroughChartTime(chartTimeAtTopOfScreen))
				AddPreviewEvent(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, chartRegionX, chartRegionW, startPosY, miscEventAlpha);
			// The preview event may also begin during the current rate section but start after the start of the screen.
			// This will capture the preview even if starts after the bottom of the screen, which is not a problem.
			CheckForAddingPreviewEvent(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, nextRateEvent, chartRegionX, chartRegionW, startPosY, miscEventAlpha);

			// Check for adding regions which extend through the top of the screen.
			var regions = ScanBackwardsForRegions(chartPositionAtTopOfScreen, chartTimeAtTopOfScreen);
			foreach (var region in regions)
				AddRegion(region, ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, chartRegionX, chartRegionW);

			// Check to see if any regions needing to be added will complete before the next rate altering event.
			CheckForCompletingRegions(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, nextRateEvent);

			// Check for updating the SelectedRegion.
			CheckForUpdatingSelectedRegionStartY(previousRateEventY, rateEvent, nextRateEvent);
			CheckForUpdatingSelectedRegionCurrentValues(previousRateEventY, rateEvent, nextRateEvent);
		}

		/// <summary>
		/// Handles updating any pending regions when the current rate altering event changes
		/// while processing events in the main tick loop.
		/// Pending regions include normal regions needing to be added to VisibleRegions,
		/// the preview region, and the SelectedRegion.
		/// </summary>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		private void UpdateRegions(
			ref List<IChartRegion> regionsNeedingToBeAdded,
			double previousRateEventY,
			EditorRateAlteringEvent rateEvent,
			EditorRateAlteringEvent nextRateEvent,
			double chartRegionX,
			double chartRegionW,
			double startPosY,
			float miscEventAlpha)
		{
			// Add the preview if it starts within this new rate altering event.
			// This will capture the preview even if starts after the bottom of the screen, which is not a problem.
			CheckForAddingPreviewEvent(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, nextRateEvent, chartRegionX, chartRegionW, startPosY, miscEventAlpha);

			// Check to see if any regions needing to be added will complete before the next rate altering event.
			CheckForCompletingRegions(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, nextRateEvent);

			// Check for updating the SelectedRegion.
			CheckForUpdatingSelectedRegionStartY(previousRateEventY, rateEvent, nextRateEvent);
			CheckForUpdatingSelectedRegionCurrentValues(previousRateEventY, rateEvent, nextRateEvent);
		}

		/// <summary>
		/// Handles completing any pending regions this tick.
		/// Pending regions include normal regions needing to be added to VisibleRegions,
		/// and the SelectedRegion.
		/// </summary>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		private void EndRegions(
			ref List<IChartRegion> regionsNeedingToBeAdded,
			double previousRateEventY,
			EditorRateAlteringEvent rateEvent)
		{
			// We do not need to scan forward for more rate mods.
			EditorRateAlteringEvent nextRateEvent = null;

			CheckForCompletingRegions(ref regionsNeedingToBeAdded, previousRateEventY, rateEvent, nextRateEvent);

			// Check for updating the SelectedRegion.
			CheckForUpdatingSelectedRegionStartY(previousRateEventY, rateEvent, nextRateEvent);
			CheckForUpdatingSelectedRegionCurrentValues(previousRateEventY, rateEvent, nextRateEvent);
		}

		/// <summary>
		/// Helper method to update beat marker events.
		/// Adds new MarkerEvents to VisibleMarkers.
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
		/// <param name="previousRateEventY">Y position of previous rate altering event.</param>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		private void UpdateBeatMarkers(
			EditorRateAlteringEvent currentRateEvent,
			ref int currentRow,
			ref int lastRecordedRow,
			EditorRateAlteringEvent nextRateEvent,
			double x,
			double sizeZoom,
			double previousRateEventY)
		{
			if (sizeZoom < MeasureMarkerMinScale)
				return;
			if (VisibleMarkers.Count >= MaxMarkersToDraw)
				return;

			// Based on the current rate altering event, determine the beat spacing and snap the current row to a beat.
			var beatsPerMeasure = currentRateEvent.LastTimeSignature.Signature.Numerator;
			var rowsPerBeat = (MaxValidDenominator * NumBeatsPerMeasure * beatsPerMeasure)
			                  / currentRateEvent.LastTimeSignature.Signature.Denominator / beatsPerMeasure;

			// Determine which integer measure and beat we are on. Clamped due to warps.
			var rowRelativeToTimeSignatureStart = Math.Max(0,
				currentRow - currentRateEvent.LastTimeSignature.IntegerPosition);
			// We need to snap the row forward since we are starting with a row that might not be on a beat boundary.
			var beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
			currentRow = currentRateEvent.LastTimeSignature.IntegerPosition +
			             beatRelativeToTimeSignatureStart * rowsPerBeat;

			var markerWidth = ActiveChart.NumInputs * MarkerTextureWidth * sizeZoom;

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
				var absoluteBeatTime = currentRateEvent.ChartTimeForFollowingEvents + rowRelativeToLastRateChangeEvent * currentRateEvent.SecondsPerRow;
				var relativeBeatTime = absoluteBeatTime - currentRateEvent.GetChartTime();
				var y = SpacingHelper.GetY(relativeBeatTime, rowRelativeToLastRateChangeEvent, previousRateEventY);

				// If advancing this beat forward moved us over the next rate altering event boundary, loop again.
				if (nextRateEvent != null && currentRow > nextRateEvent.GetRow())
				{
					currentRow = nextRateEvent.GetRow();
					return;
				}

				// If advancing moved beyond the end of the screen then we are done.
				if (y > GetViewportHeight())
					return;

				// Determine if this marker is a measure marker instead of a beat marker.
				rowRelativeToTimeSignatureStart =
					currentRow - currentRateEvent.LastTimeSignature.IntegerPosition;
				beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
				var measureMarker = beatRelativeToTimeSignatureStart % beatsPerMeasure == 0;
				var measure = currentRateEvent.LastTimeSignature.MetricPosition.Measure +
				              (beatRelativeToTimeSignatureStart / beatsPerMeasure);

				// Record the marker.
				if (measureMarker || sizeZoom > BeatMarkerMinScale)
					VisibleMarkers.Add(new EditorMarkerEvent(x, y, markerWidth, 1, sizeZoom, measureMarker, measure));

				lastRecordedRow = currentRow;

				if (VisibleMarkers.Count >= MaxMarkersToDraw)
					return;

				// Advance one beat.
				currentRow += rowsPerBeat;
			}
		}

		/// <summary>
		/// Given a y position in screen space, return the corresponding chart time and row.
		/// This is O(log(N)) time complexity on the number of rate altering events in the chart
		/// plus an additional linear scan of rate altering events between the focal point and
		/// the given y position.
		/// </summary>
		/// <param name="desiredScreenY">Y position in screen space.</param>
		/// <returns>Tuple where the first value is the chart time and the second is the row.</returns>
		private (double, double) FindChartTimeAndRowForScreenY(int desiredScreenY)
		{
			if (ActiveChart == null)
				return (0.0, 0.0);

			// Set up a spacing helper with isolated state for searching for the time and row.
			var spacingHelper = EventSpacingHelper.GetSpacingHelper(ActiveChart);

			double desiredY = desiredScreenY;

			// The only point where we know the screen space y position as well as the chart time and chart position
			// is at the focal point. We will use this as an anchor for scanning for the rate event to use for the
			// desired Y position. As we scan upwards or downwards through rate events we can keep track of the rate
			// event's Y position by calculating it from the previous rate event, and then finally calculate the
			// desired Y position's chart time and chart position from rate event's screen Y position and its rate
			// information.
			var focalPointChartTime = Position.ChartTime;
			double focalPointChartPosition = 0.0;
			if (!ActiveChart.TryGetChartPositionFromTime(focalPointChartTime, ref focalPointChartPosition))
				return (0.0, 0.0);
			var focalPointY = (double)GetFocalPointY();

			var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, (int)focalPointChartPosition, focalPointChartTime);
			if (rateEnumerator == null)
				return (0.0, 0.0);
			rateEnumerator.MoveNext();

			var interpolatedScrollRate = GetInterpolatedScrollRate(focalPointChartTime, focalPointChartPosition);
			var spacingZoom = GetSpacingZoom();

			// Determine the active rate event's position and rate information.
			spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
			var rateEventY = spacingHelper.GetY(rateEnumerator.Current, focalPointY, focalPointChartTime, focalPointChartPosition);
			var rateChartTime = rateEnumerator.Current.GetChartTime();
			var rateRow = rateEnumerator.Current.GetRow();

			// If the desired Y is above the focal point.
			if (desiredY < focalPointY)
			{
				// Scan upwards until we find the rate event that is active for the desired Y.
				while (true)
				{
					// If the current rate event is above the focal point, or there is no preceding rate event,
					// then this is the rate event we should use for determining the chart time and row of the
					// desired position.
					if (rateEventY <= desiredY || !rateEnumerator.MovePrev())
						return spacingHelper.GetChartTimeAndRow(desiredY, rateEventY, rateChartTime, rateRow);

					// Otherwise, now that we have advance the rate enumerator to its preceding event, we can
					// update the the current rate event variables to check again next loop.
					spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
					rateEventY = spacingHelper.GetY(rateEnumerator.Current, rateEventY, rateChartTime, rateRow);
					rateChartTime = rateEnumerator.Current.GetChartTime();
					rateRow = rateEnumerator.Current.GetRow();
				}
			}
			// If the desired Y is below the focal point.
			else if (desiredY > focalPointY)
			{
				while (true)
				{
					// If there is no following rate event then the current rate event should be used for
					// determining the chart time and row of the desired position.
					if (!rateEnumerator.MoveNext())
						return spacingHelper.GetChartTimeAndRow(desiredY, rateEventY, rateChartTime, rateRow);

					// Otherwise, we need to determine the position of the next rate event. If it is beyond
					// the desired position then we have gone to far and we need to use the previous rate
					// information to determine the chart time and row of the desired position.
					rateEventY = spacingHelper.GetY(rateEnumerator.Current, rateEventY, rateChartTime, rateRow);
					spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
					rateChartTime = rateEnumerator.Current.GetChartTime();
					rateRow = rateEnumerator.Current.GetRow();

					if (rateEventY >= desiredY)
						return spacingHelper.GetChartTimeAndRowFromPreviousRate(desiredY, rateEventY, rateChartTime, rateRow);
				}
			}

			// The desired Y is exactly at the focal point.
			return (focalPointChartTime, focalPointChartPosition);
		}

		#endregion Chart Update

		#region Autoplay

		private void UpdateAutoPlay()
		{
			if (!Playing || ActiveChart == null || NextAutoPlayNotes == null || Receptors == null)
				return;

			var nextEvents = ActiveChart.GetNextInputNotes(Position.ChartPosition);

			for (var lane = 0; lane < ActiveChart.NumInputs; lane++)
			{
				// If the next has changed it means we have just passed over an event.
				if (NextAutoPlayNotes[lane] != nextEvents[lane])
				{
					// Since we have already passed a note, we should offset any animations so they begin
					// as if they started at the precise moment the event passed.
					var timeDelta = NextAutoPlayNotes[lane] == null ? 0.0 : 
						Position.ChartTime - NextAutoPlayNotes[lane].GetChartTime();

					// The new next event can be null at the end of the chart. We need to release any
					// held input in this case.
					if (nextEvents[lane] == null && Receptors[lane].IsAutoplayHeld())
					{
						Receptors[lane].OnAutoplayInputUp(timeDelta);
					}

					// Only process inputs if the current input is not null.
					// This helps ensure that when starting playing in the middle of a chart
					// we don't incorrectly show input immediately.
					if (NextAutoPlayNotes[lane] != null)
					{
						// If the event that just passed is a hold end, release input.
						if (NextAutoPlayNotes[lane].GetEvent() is LaneHoldEndNote)
						{
							Receptors[lane].OnAutoplayInputUp(timeDelta);
						}
						else
						{
							// For both taps an hold starts, press input.
							Receptors[lane].OnAutoplayInputDown(timeDelta);

							// For taps, release them immediately.
							if (NextAutoPlayNotes[lane].GetEvent() is LaneTapNote)
							{
								Receptors[lane].OnAutoplayInputUp(timeDelta);
							}
						}
					}

					// If the next event is a hold end (i.e. we are in a hold) and the currently
					// tracked note is null (i.e. we just started playback), then start input to
					// hold the note.
					else if (NextAutoPlayNotes[lane] == null
					         && nextEvents[lane] != null
					         && nextEvents[lane].GetEvent() is LaneHoldEndNote)
					{
						Receptors[lane].OnAutoplayInputDown(timeDelta);
					}
				}

				// Cache the next event for next time.
				NextAutoPlayNotes[lane] = nextEvents[lane];
			}
		}

		private void UpdateAutoPlayFromScrolling()
		{
			StopAutoPlay();
		}

		private void StopAutoPlay()
		{
			if (ActiveChart == null || NextAutoPlayNotes == null || Receptors == null)
				return;

			for (var lane = 0; lane < ActiveChart.NumInputs; lane++)
			{
				//if (NextAutoPlayNotes[lane] is EditorHoldEndNoteEvent hen)
				//	hen.Active = false;
				NextAutoPlayNotes[lane] = null;
				Receptors[lane].OnAutoplayInputCancel();
			}
		}

		#endregion Autoplay

		#region MiniMap

		private void ProcessInputForMiniMap()
		{
			var pScroll = Preferences.Instance.PreferencesScroll;
			var pMiniMap = Preferences.Instance.PreferencesMiniMap;
			var focalPointY = GetFocalPointY();

			var miniMapCapturingMouseLastFrame = MiniMapCapturingMouse;

			var miniMapNeedsMouseThisFrame = false;
			if (EditorMouseState.LeftClickDownThisFrame())
			{
				miniMapNeedsMouseThisFrame = MiniMap.MouseDown(EditorMouseState.X(), EditorMouseState.Y());
			}

			MiniMap.MouseMove(EditorMouseState.X(), EditorMouseState.Y());
			if (EditorMouseState.LeftClickUpThisFrame() || (MiniMapCapturingMouse && EditorMouseState.LeftReleased()))
			{
				MiniMap.MouseUp(EditorMouseState.X(), EditorMouseState.Y());
			}

			MiniMapCapturingMouse = MiniMap.WantsMouse();

			// Set the Song Position based on the MiniMap position
			MiniMapCapturingMouse |= miniMapNeedsMouseThisFrame;
			if (MiniMapCapturingMouse)
			{
				// When moving the MiniMap, pause or stop playback.
				if (EditorMouseState.LeftClickDownThisFrame() && Playing)
				{
					// Set a flag to unpause playback unless the preference is to completely stop when scrolling.
					StartPlayingWhenMiniMapDone = !pMiniMap.MiniMapStopPlaybackWhenScrolling;
					StopPlayback();
				}

				// Set the music position based off of the MiniMap editor area.
				var editorPosition = MiniMap.GetEditorPosition();
				switch (GetMiniMapSpacingMode())
				{
					case SpacingMode.ConstantTime:
					{
						Position.ChartTime =
							editorPosition + (focalPointY / (pScroll.TimeBasedPixelsPerSecond * GetSpacingZoom()));
						break;
					}
					case SpacingMode.ConstantRow:
					{
						Position.ChartPosition =
							editorPosition + (focalPointY / (pScroll.RowBasedPixelsPerRow * GetSpacingZoom()));
						break;
					}
				}
				UpdateAutoPlayFromScrolling();
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
			var p = Preferences.Instance;
			var focalPointX = GetFocalPointX();
			var x = 0;
			var sizeZoom = GetSizeZoom();
			switch (p.PreferencesMiniMap.MiniMapPosition)
			{
				case MiniMap.Position.RightSideOfWindow:
				{
					x = Graphics.PreferredBackBufferWidth - (int)p.PreferencesMiniMap.MiniMapXPadding - (int)p.PreferencesMiniMap.MiniMapWidth;
					break;
				}
				case MiniMap.Position.RightOfChartArea:
				{
					x = (int)(focalPointX + (WaveFormTextureWidth >> 1) + (int)p.PreferencesMiniMap.MiniMapXPadding);
					break;
				}
				case MiniMap.Position.MountedToWaveForm:
				{
					if (p.PreferencesWaveForm.WaveFormScaleXWhenZooming)
					{
						x = (int)(focalPointX + (WaveFormTextureWidth >> 1) * sizeZoom + (int)p.PreferencesMiniMap.MiniMapXPadding);
					}
					else
					{
						x = (int)(focalPointX + (WaveFormTextureWidth >> 1) + (int)p.PreferencesMiniMap.MiniMapXPadding);
					}

					break;
				}
				case MiniMap.Position.MountedToChart:
				{
					var receptorBounds = Receptor.GetBounds(GetFocalPoint(), sizeZoom, TextureAtlas, ArrowGraphicManager, ActiveChart);
					x = receptorBounds.Item1 + receptorBounds.Item3 + (int)p.PreferencesMiniMap.MiniMapXPadding;
					break;
				}
			}

			var h = Math.Max(0, Graphics.PreferredBackBufferHeight - GetMiniMapYPaddingFromTop() - GetMiniMapYPaddingFromBottom());

			MiniMap.UpdateBounds(
				GraphicsDevice,
				new Rectangle(x, GetMiniMapYPaddingFromTop(), (int)p.PreferencesMiniMap.MiniMapWidth, h));
		}

		private void UpdateMiniMapLaneSpacing()
		{
			MiniMap.SetLaneSpacing(
				Preferences.Instance.PreferencesMiniMap.MiniMapNoteWidth,
				Preferences.Instance.PreferencesMiniMap.MiniMapNoteSpacing);
		}

		private SpacingMode GetMiniMapSpacingMode()
		{
			if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.Variable)
				return Preferences.Instance.PreferencesMiniMap.MiniMapSpacingModeForVariable;
			return Preferences.Instance.PreferencesScroll.SpacingMode;
		}

		private void UpdateMiniMap()
		{
			var pMiniMap = Preferences.Instance.PreferencesMiniMap;
			// Performance optimization. Do not update the MiniMap if we won't render it.
			if (!pMiniMap.ShowMiniMap)
				return;

			if (ActiveChart == null || ActiveChart.EditorEvents == null || ArrowGraphicManager == null)
			{
				MiniMap.UpdateNoChart();
				return;
			}

			UpdateMiniMapBounds();
			UpdateMiniMapLaneSpacing();

			var pScroll = Preferences.Instance.PreferencesScroll;

			MiniMap.SetSelectMode(pMiniMap.MiniMapSelectMode);

			switch (GetMiniMapSpacingMode())
			{
				case SpacingMode.ConstantTime:
				{
					var screenHeight = GetViewportHeight();
					var spacingZoom = GetSpacingZoom();
					var pps = pScroll.TimeBasedPixelsPerSecond * spacingZoom;
					var time = Position.ChartTime;

					// Editor Area. The visible time range.
					var editorAreaTimeStart = time - (GetFocalPointY() / pps);
					var editorAreaTimeEnd = editorAreaTimeStart + (screenHeight / pps);
					var editorAreaTimeRange = editorAreaTimeEnd - editorAreaTimeStart;

					// Determine the end time.
					var maxTimeFromChart = ActiveChart.GetEndTime(false);

					// Full Area. The time from the chart, extended in both directions by the editor range.
					var fullAreaTimeStart = ActiveChart.GetStartTime(false) - editorAreaTimeRange;
					var fullAreaTimeEnd = maxTimeFromChart + editorAreaTimeRange;

					// Content Area. The time from the chart.
					var contentAreaTimeStart = ActiveChart.GetStartTime(false);
					var contentAreaTimeEnd = maxTimeFromChart;

					// Update the MiniMap with the ranges.
					MiniMap.SetNumLanes((uint)ActiveChart.NumInputs);
					MiniMap.UpdateBegin(
						fullAreaTimeStart, fullAreaTimeEnd,
						contentAreaTimeStart, contentAreaTimeEnd,
						pMiniMap.MiniMapVisibleTimeRange,
						editorAreaTimeStart, editorAreaTimeEnd,
						ArrowGraphicManager);

					// Add notes
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(MiniMap.GetMiniMapAreaStart(), ref chartPosition))
						break;
					var enumerator = ActiveChart.EditorEvents.FindBest(chartPosition);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.GetEvent(),
							ToSeconds(hsn.GetEvent().TimeMicros),
							ToSeconds(hsn.GetHoldEndNote().GetEvent().TimeMicros),
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNoteEvent)
							continue;

						if (e is EditorTapNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.GetEvent(), ToSeconds(e.GetEvent().TimeMicros)) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.GetEvent(), ToSeconds(e.GetEvent().TimeMicros)) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNoteEvent hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.GetEvent(),
								    ToSeconds(e.GetEvent().TimeMicros),
								    ToSeconds(hsn.GetHoldEndNote().GetEvent().TimeMicros),
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
					var screenHeight = GetViewportHeight();

					var chartPosition = Position.ChartPosition;
					var spacingZoom = GetSpacingZoom();
					var ppr = pScroll.RowBasedPixelsPerRow * spacingZoom;

					// Editor Area. The visible row range.
					var editorAreaRowStart = chartPosition - (GetFocalPointY() / ppr);
					var editorAreaRowEnd = editorAreaRowStart + (screenHeight / ppr);
					var editorAreaRowRange = editorAreaRowEnd - editorAreaRowStart;

					// Determine the end row.
					var lastEvent = ActiveChart.EditorEvents.Last();
					var maxRowFromChart = 0.0;
					if (lastEvent.MoveNext())
						maxRowFromChart = lastEvent.Current.GetEvent().IntegerPosition;

					if (ActiveSong.LastSecondHint > 0.0)
					{
						var lastSecondChartPosition = 0.0;
						if (ActiveChart.TryGetChartPositionFromTime(ActiveSong.LastSecondHint, ref lastSecondChartPosition))
						{
							maxRowFromChart = Math.Max(lastSecondChartPosition, maxRowFromChart);
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
						pMiniMap.MiniMapVisibleRowRange,
						editorAreaRowStart, editorAreaRowEnd,
						ArrowGraphicManager);

					// Add notes
					chartPosition = MiniMap.GetMiniMapAreaStart();
					var enumerator = ActiveChart.EditorEvents.FindBest(chartPosition);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.GetEvent(),
							hsn.GetEvent().IntegerPosition,
							hsn.GetHoldEndNote().GetEvent().IntegerPosition,
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNoteEvent)
							continue;

						if (e is EditorTapNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.GetEvent(), e.GetEvent().IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.GetEvent(), e.GetEvent().IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNoteEvent hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.GetEvent(),
								    e.GetEvent().IntegerPosition,
								    hsn.GetHoldEndNote().GetEvent().IntegerPosition,
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
			if (!Preferences.Instance.PreferencesMiniMap.ShowMiniMap)
				return;
			MiniMap.Draw(SpriteBatch);
		}

		#endregion MiniMap

		#region Gui Rendering

		private void DrawGui(GameTime gameTime)
		{
			DrawMainMenuUI();

			DrawDebugUI();

			if (ShowImGuiTestWindow)
			{
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref ShowImGuiTestWindow);
			}

			UILog.Draw(LogBuffer, LogBufferLock);
			UIScrollPreferences.Draw();
			UIWaveFormPreferences.Draw();
			UIMiniMapPreferences.Draw();
			UIReceptorPreferences.Draw();
			UIOptions.Draw();

			UISongProperties.Draw(ActiveSong);
			UIChartProperties.Draw(ActiveChart);
			UIChartList.Draw(ActiveSong, ActiveChart);
			
			UIChartPosition.Draw(
				GetFocalPointX(),
				Graphics.PreferredBackBufferHeight - GetChartPositionUIYPAddingFromBottom() - (int)(UIChartPosition.Height * 0.5),
				SnapLevels[SnapIndex]);

			if (ShowSavePopup)
			{
				ShowSavePopup = false;
				SystemSounds.Exclamation.Play();
				ImGui.OpenPopup(GetSavePopupTitle());
			}

			if (CanShowRightClickPopupThisFrame && EditorMouseState.RightClickUpThisFrame())
			{
				ImGui.OpenPopup("RightClickPopup");
			}
			DrawRightClickMenu((int)EditorMouseState.LastRightClickUpPosition.X, (int)EditorMouseState.LastRightClickUpPosition.Y);

			DrawUnsavedChangesPopup();
		}

		private void DrawMainMenuUI()
		{
			var p = Preferences.Instance;
			if (ImGui.BeginMainMenuBar())
			{
				if (ImGui.BeginMenu("File"))
				{
					if (ImGui.MenuItem("New Song", "Ctrl+N"))
					{
						OnNew();
					}
					if (ImGui.BeginMenu("New Chart", ActiveSong != null))
					{
						DrawNewChartSelectableList();
						ImGui.EndMenu();
					}

					ImGui.Separator();
					if (ImGui.MenuItem("Open", "Ctrl+O"))
					{
						OnOpen();
					}
					if (ImGui.BeginMenu("Open Recent", p.RecentFiles.Count > 0))
					{
						for (var i = 0; i < p.RecentFiles.Count; i++)
						{
							var recentFile = p.RecentFiles[i];
							var fileNameWithPath = recentFile.FileName;
							var fileName = System.IO.Path.GetFileName(fileNameWithPath);
							if (ImGui.MenuItem(fileName))
							{
								OpenRecentIndex = i;
								OnOpenRecentFile();
							}
						}

						ImGui.EndMenu();
					}

					if (ImGui.MenuItem("Reload", "Ctrl+R", false, ActiveSong != null && p.RecentFiles.Count > 0))
					{
						OnReload();
					}

					if (ImGui.MenuItem("Close", "", false, ActiveSong != null))
					{
						OnClose();
					}

					ImGui.Separator();
					var editorFileName = ActiveSong?.FileName;
					if (!string.IsNullOrEmpty(editorFileName))
					{
						if (ImGui.MenuItem($"Save {editorFileName}", "Ctrl+S"))
						{
							OnSave();
						}
					}
					else
					{
						if (ImGui.MenuItem("Save", "Ctrl+S", false, ActiveSong != null))
						{
							OnSave();
						}
					}
					if (ImGui.MenuItem("Save As...", "Ctrl+Shift+S", false, ActiveSong != null))
					{
						OnSaveAs();
					}

					ImGui.Separator();
					if (ImGui.MenuItem("Exit", "Alt+F4"))
					{
						OnExit();
					}

					ImGui.EndMenu();
				}

				if (ImGui.BeginMenu("View"))
				{
					if (ImGui.MenuItem("Options"))
						p.PreferencesOptions.ShowOptionsWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Song Properties"))
						p.ShowSongPropertiesWindow = true;
					if (ImGui.MenuItem("Chart Properties"))
						p.ShowChartPropertiesWindow = true;
					if (ImGui.MenuItem("Chart List"))
						p.ShowChartListWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Waveform Preferences"))
						p.PreferencesWaveForm.ShowWaveFormPreferencesWindow = true;
					if (ImGui.MenuItem("Scroll Preferences"))
						p.PreferencesScroll.ShowScrollControlPreferencesWindow = true;
					if (ImGui.MenuItem("Mini Map Preferences"))
						p.PreferencesMiniMap.ShowMiniMapPreferencesWindow = true;
					if (ImGui.MenuItem("Receptor Preferences"))
						p.PreferencesReceptors.ShowReceptorPreferencesWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Log"))
						p.ShowLogWindow = true;
					if (ImGui.MenuItem("ImGui Demo Window"))
						ShowImGuiTestWindow = true;
					ImGui.EndMenu();
				}

				ImGui.EndMainMenuBar();
			}
		}

		private void DrawNewChartSelectableList()
		{
			foreach (var chartType in SupportedChartTypes)
			{
				if (ImGui.Selectable(GetPrettyEnumString(chartType)))
				{
					ActionQueue.Instance.Do(new ActionAddChart(this, chartType));
				}
			}
		}

		private void DrawRightClickMenu(int x, int y)
		{
			if (ImGui.BeginPopup("RightClickPopup"))
			{
				if (ActiveSong == null)
				{
					if (ImGui.MenuItem("New Song", "Ctrl+N"))
					{
						OnNew();
					}
				}

				var isInMiniMapArea = Preferences.Instance.PreferencesMiniMap.ShowMiniMap
					&& MiniMap.IsScreenPositionInMiniMapBounds(x, y);
				var isInWaveFormArea = Preferences.Instance.PreferencesWaveForm.ShowWaveForm
					&& x >= (GetFocalPointX() - (WaveFormTextureWidth >> 1))
					&& x <= (GetFocalPointX() + (WaveFormTextureWidth >> 1));
				var isInReceptorArea = Receptor.IsInReceptorArea(x, y, GetFocalPoint(), GetSizeZoom(), TextureAtlas, ArrowGraphicManager, ActiveChart);

				if (isInMiniMapArea)
				{
					if (ImGui.BeginMenu("Mini Map Preferences"))
					{
						UIMiniMapPreferences.DrawContents();
						ImGui.EndMenu();
					}
				}
				else if (isInReceptorArea)
				{
					if (ImGui.BeginMenu("Receptor Preferences"))
					{
						UIReceptorPreferences.DrawContents();
						ImGui.EndMenu();
					}
				}
				else if (isInWaveFormArea)
				{
					if (ImGui.BeginMenu("Waveform Preferences"))
					{
						UIWaveFormPreferences.DrawContents();
						ImGui.EndMenu();
					}
				}

				var row = Math.Max(0, Position.GetNearestRow());
				if (ActiveChart != null)
				{
					if (ImGui.BeginMenu("Add"))
					{
						var nearestMeasureBoundaryRow = ActiveChart.GetNearestMeasureBoundaryRow(row);
						var eventsAtNearestMeasureBoundary = ActiveChart.EditorEvents.FindEventsAtRow(nearestMeasureBoundaryRow);

						var events = ActiveChart.EditorEvents.FindEventsAtRow(row);
						bool hasTempoEvent = false;
						bool hasInterpolatedScrollRateEvent = false;
						bool hasScrollRateEvent = false;
						bool hasStopEvent = false;
						bool hasDelayEvent = false;
						bool hasWarpEvent = false;
						bool hasFakeEvent = false;
						bool hasTickCountEvent = false;
						bool hasMultipliersEvent = false;
						bool hasTimeSignatureEvent = false;
						bool hasLabelEvent = false;

						foreach (var currentEvent in events)
						{
							if (currentEvent is EditorTempoEvent)
								hasTempoEvent = true;
							else if (currentEvent is EditorInterpolatedRateAlteringEvent)
								hasInterpolatedScrollRateEvent = true;
							else if (currentEvent is EditorScrollRateEvent)
								hasScrollRateEvent = true;
							else if (currentEvent is EditorStopEvent)
								hasStopEvent = true;
							else if (currentEvent is EditorDelayEvent)
								hasDelayEvent = true;
							else if (currentEvent is EditorWarpEvent)
								hasWarpEvent = true;
							else if (currentEvent is EditorFakeSegmentEvent)
								hasFakeEvent = true;
							else if (currentEvent is EditorTickCountEvent)
								hasTickCountEvent = true;
							else if (currentEvent is EditorMultipliersEvent)
								hasMultipliersEvent = true;
							// Skipping time signatures as we only place them on measure boundaries
							//else if (currentEvent is EditorTimeSignatureEvent)
							//	hasTimeSignatureEvent = true;
							else if (currentEvent is EditorLabelEvent)
								hasLabelEvent = true;
						}
						foreach (var currentEvent in eventsAtNearestMeasureBoundary)
						{
							if (currentEvent is EditorTimeSignatureEvent)
								hasTimeSignatureEvent = true;
						}

						double chartTime = 0.0;
						ActiveChart.TryGetTimeFromChartPosition(row, ref chartTime);

						double nearestMeasureChartTime = 0.0;
						ActiveChart.TryGetTimeFromChartPosition(nearestMeasureBoundaryRow, ref nearestMeasureChartTime);

						var currentRateAlteringEvent = ActiveChart.FindActiveRateAlteringEventForPosition(row);

						DrawAddEventMenuItem("Tempo", !hasTempoEvent, UITempoColorRGBA, EditorTempoEvent.EventShortDescription, row, chartTime, () =>
						{
							return new EditorTempoEvent(ActiveChart, new Tempo(currentRateAlteringEvent?.Tempo ?? EditorChart.DefaultTempo));
						});

						ImGui.Separator();
						DrawAddEventMenuItem("Interpolated Scroll Rate", !hasInterpolatedScrollRateEvent, UISpeedsColorRGBA, EditorInterpolatedRateAlteringEvent.EventShortDescription, row, chartTime, () =>
						{
							return new EditorInterpolatedRateAlteringEvent(ActiveChart, new ScrollRateInterpolation(EditorChart.DefaultScrollRate, MaxValidDenominator, 0L, false));
						});
						DrawAddEventMenuItem("Scroll Rate", !hasScrollRateEvent, UIScrollsColorRGBA, EditorScrollRateEvent.EventShortDescription, row, chartTime, () =>
						{
							return new EditorScrollRateEvent(ActiveChart, new ScrollRate(EditorChart.DefaultScrollRate));
						});

						ImGui.Separator();
						DrawAddEventMenuItem("Stop", !hasStopEvent, UIStopColorRGBA, EditorStopEvent.EventShortDescription, row, chartTime, () =>
						{
							var stopLength = ToMicros(currentRateAlteringEvent.SecondsPerRow * MaxValidDenominator);
							return new EditorStopEvent(ActiveChart, new Stop(stopLength, false));
						});
						DrawAddEventMenuItem("Delay", !hasDelayEvent, UIDelayColorRGBA, EditorDelayEvent.EventShortDescription, row, chartTime, () =>
						{
							var stopLength = ToMicros(currentRateAlteringEvent.SecondsPerRow * MaxValidDenominator);
							return new EditorDelayEvent(ActiveChart, new Stop(stopLength, true));
						});
						DrawAddEventMenuItem("Warp", !hasWarpEvent, UIWarpColorRGBA, EditorWarpEvent.EventShortDescription, row, chartTime, () =>
						{
							return new EditorWarpEvent(ActiveChart, new Warp(MaxValidDenominator));
						});

						ImGui.Separator();
						DrawAddEventMenuItem("Fake Region", !hasFakeEvent, UIFakesColorRGBA, EditorFakeSegmentEvent.EventShortDescription, row, chartTime, () =>
						{
							var fakeLength = ToMicros(currentRateAlteringEvent.SecondsPerRow * MaxValidDenominator);
							return new EditorFakeSegmentEvent(ActiveChart, new FakeSegment(fakeLength));
						});
						DrawAddEventMenuItem("Ticks", !hasTickCountEvent, UITicksColorRGBA, EditorTickCountEvent.EventShortDescription, row, chartTime, () =>
						{
							return new EditorTickCountEvent(ActiveChart, new TickCount(EditorChart.DefaultTickCount));
						});
						DrawAddEventMenuItem("Combo Multipliers", !hasMultipliersEvent, UIMultipliersColorRGBA, EditorMultipliersEvent.EventShortDescription, row, chartTime, () =>
						{
							return new EditorMultipliersEvent(ActiveChart, new Multipliers(EditorChart.DefaultHitMultiplier, EditorChart.DefaultMissMultiplier));
						});
						DrawAddEventMenuItem("Time Signature", !hasTimeSignatureEvent, UITimeSignatureColorRGBA, EditorTimeSignatureEvent.EventShortDescription, nearestMeasureBoundaryRow, nearestMeasureChartTime, () =>
						{
							return new EditorTimeSignatureEvent(ActiveChart, new TimeSignature(EditorChart.DefaultTimeSignature));
						}, true);
						DrawAddEventMenuItem("Label", !hasLabelEvent, UILabelColorRGBA, EditorLabelEvent.EventShortDescription, row, chartTime, () =>
						{
							return new EditorLabelEvent(ActiveChart, new Fumen.ChartDefinition.Label("New Label"));
						});

						ImGui.EndMenu();
					}
				}

				if (ActiveSong != null)
				{
					if (ImGui.BeginMenu("New Chart"))
					{
						DrawNewChartSelectableList();
						ImGui.EndMenu();
					}
				}

				ImGui.EndPopup();
			}
		}

		private void DrawAddEventMenuItem(string name, bool enabled, uint color, string toolTipText, int row, double chartTime, Func<EditorEvent> createEventFunc, bool onlyOnePerMeasure = false)
		{
			if (MenuItemWithColor(name, enabled, color))
			{
				var newEvent = createEventFunc();
				newEvent.SetRow(row);
				newEvent.SetChartTime(chartTime);
				ActionQueue.Instance.Do(new ActionAddEditorEvent(newEvent));
			}
			if (!enabled)
			{
				if (onlyOnePerMeasure)
					toolTipText += $"\n\nOnly one {name} event can be specified per measure.\nThere is already a {name} specified on the measure at row {row}.";
				else
					toolTipText += $"\n\nOnly one {name} event can be specified per row.\nThere is already a {name} specified on row {row}.";
			}
			ToolTip(toolTipText);
		}

		private string GetSavePopupTitle()
		{
			var appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
			return $"{appName}##save";
		}

		private void DrawUnsavedChangesPopup()
		{
			if (ImGui.BeginPopupModal(GetSavePopupTitle()))
			{
				if (!string.IsNullOrEmpty(ActiveSong.Title))
					ImGui.Text($"Do you want to save the changes you made to {ActiveSong.Title}?\nYour changes will be lost if you don't save them.");
				else
					ImGui.Text($"Do you want to save your changes?\nYour changes will be lost if you don't save them.");
				
				ImGui.Separator();
				if (ImGui.Button("Save"))
				{
					if (CanSaveWithoutLocationPrompt())
					{
						OnSave();
					}
					else
					{
						OnSaveAs();
					}
					ImGui.CloseCurrentPopup();
				}
				ImGui.SameLine();
				if (ImGui.Button("Don't Save"))
				{
					TryInvokePostSaveFunction();
					ImGui.CloseCurrentPopup();
				}
				ImGui.SetItemDefaultFocus();
				ImGui.SameLine();
				if (ImGui.Button("Cancel"))
				{
					ImGui.CloseCurrentPopup();
				}

				ImGui.EndPopup();
			}
		}


		private void DrawDebugUI()
		{
			if (ImGui.Begin("Debug"))
			{
				ImGui.Checkbox("Parallelize Update Loop", ref ParallelizeUpdateLoop);
				ImGui.Checkbox("Render Chart", ref RenderChart);
				ImGui.Text($"Update Time:       {UpdateTimeTotal:F6} seconds");
				ImGui.Text($"  Waveform:        {UpdateTimeWaveForm:F6} seconds");
				ImGui.Text($"  Mini Map:        {UpdateTimeMiniMap:F6} seconds");
				ImGui.Text($"  Chart Events:    {UpdateTimeChartEvents:F6} seconds");
				ImGui.Text($"Draw Time:         {DrawTimeTotal:F6} seconds");
				ImGui.Text($"Total Time:        {(UpdateTimeTotal + DrawTimeTotal):F6} seconds");
				ImGui.Text($"Total FPS:         {(1.0f / (UpdateTimeTotal + DrawTimeTotal)):F6}");
				ImGui.Text($"Actual Time:       {1.0f / ImGui.GetIO().Framerate:F6} seconds");
				ImGui.Text($"Actual FPS:        {ImGui.GetIO().Framerate:F6}");

				if (ImGui.Button("Reload Song"))
				{
					StopPreview();
					MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), GetSongTime, true);
				}

				if (ImGui.Button("Save Time and Zoom"))
				{
					Preferences.Instance.DebugSongTime = Position.SongTime;
					Preferences.Instance.DebugZoom = Zoom;
				}

				if (ImGui.Button("Load Time and Zoom"))
				{
					Position.SongTime = Preferences.Instance.DebugSongTime;
					SetZoom(Preferences.Instance.DebugZoom, true);
				}
			}
			ImGui.End();
		}

		#endregion Gui Rendering

		#region Loading

		/// <summary>
		/// Initializes all PadData and creates corresponding StepGraphs for all ChartTypes
		/// specified in the StartupChartTypes.
		/// </summary>
		private async void InitPadDataAndStepGraphsAsync()
		{
			var pOptions = Preferences.Instance.PreferencesOptions;
			var tasks = new Task<bool>[pOptions.StartupChartTypes.Length];
			for (var i = 0; i < pOptions.StartupChartTypes.Length; i++)
			{
				tasks[i] = LoadPadDataAndCreateStepGraph(pOptions.StartupChartTypes[i]);
			}

			await Task.WhenAll(tasks);
		}

		/// <summary>
		/// Loads PadData and creates a StepGraph for the given StepMania StepsType.
		/// </summary>
		/// <returns>
		/// True if no errors were generated and false otherwise.
		/// </returns>
		private async Task<bool> LoadPadDataAndCreateStepGraph(ChartType chartType)
		{
			if (PadDataByChartType.ContainsKey(chartType))
				return true;

			PadDataByChartType[chartType] = null;
			StepGraphByChartType[chartType] = null;

			// Load the PadData.
			PadDataByChartType[chartType] = await LoadPadData(chartType);
			if (PadDataByChartType[chartType] == null)
			{
				PadDataByChartType.Remove(chartType);
				StepGraphByChartType.Remove(chartType);
				return false;
			}

			// Create the StepGraph.
			await Task.Run(() =>
			{
				Logger.Info($"Creating {chartType} StepGraph.");
				StepGraphByChartType[chartType] = StepGraph.CreateStepGraph(
					PadDataByChartType[chartType],
					PadDataByChartType[chartType].StartingPositions[0][0][L],
					PadDataByChartType[chartType].StartingPositions[0][0][R]);
				Logger.Info($"Finished creating {chartType} StepGraph.");
			});

			return true;
		}

		/// <summary>
		/// Loads PadData for the given ChartType.
		/// </summary>
		/// <param name="chartType">ChartType to load PadData for.</param>
		/// <returns>Loaded PadData or null if any errors were generated.</returns>
		private static async Task<PadData> LoadPadData(ChartType chartType)
		{
			var chartTypeString = ChartTypeString(chartType);
			var fileName = $"{chartTypeString}.json";
			Logger.Info($"Loading PadData from {fileName}.");
			var padData = await PadData.LoadPadData(chartTypeString, fileName);
			if (padData == null)
				return null;
			Logger.Info($"Finished loading {chartTypeString} PadData.");
			return padData;
		}

		/// <summary>
		/// Starts the process of opening a Song file by presenting a dialog to choose a Song.
		/// </summary>
		private void OpenSongFile()
		{
			var pOptions = Preferences.Instance.PreferencesOptions;
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
						pOptions.DefaultStepsType,
						pOptions.DefaultDifficultyType);
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
		private async void OpenSongFileAsync(
			string fileName,
			ChartType chartType,
			ChartDifficultyType chartDifficultyType)
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
				EditorChart newActiveChart = null;
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

						LoadSongCancellationTokenSource.Token.ThrowIfCancellationRequested();
						ActiveSong = new EditorSong(
							this,
							fileName,
							song,
							GraphicsDevice,
							ImGuiRenderer);

						// Select the best Chart to make active.
						newActiveChart = SelectBestChart(ActiveSong, chartType, chartDifficultyType);
						LoadSongCancellationTokenSource.Token.ThrowIfCancellationRequested();
					}
					catch (OperationCanceledException)
					{
						// Upon cancellation null out the Song and ActiveChart.
						UnloadSongResources();
					}
					finally
					{
						LoadSongCancellationTokenSource?.Dispose();
						LoadSongCancellationTokenSource = null;
					}
				}, LoadSongCancellationTokenSource.Token);
				await LoadSongTask;
				if (ActiveSong == null)
				{
					return;
				}

				// Insert a new entry at the top of the saved recent files.
				UpdateRecentFilesForActiveSong();

				OnChartSelected(newActiveChart, false);

				// Find a better spot for this.
				Position.ChartPosition = 0.0;
				DesiredSongTime = Position.SongTime;

				SetZoom(1.0, true);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {fileName}. {e}");
			}
		}

		private void UpdateRecentFilesForActiveSong()
		{
			if (ActiveSong == null || string.IsNullOrEmpty(ActiveSong.FileFullPath))
				return;

			var p = Preferences.Instance;
			var pOptions = p.PreferencesOptions;
			var savedSongInfo = new Preferences.SavedSongInformation
			{
				FileName = ActiveSong.FileFullPath,
				LastChartType = ActiveChart?.ChartType ?? pOptions.DefaultStepsType,
				LastChartDifficultyType = ActiveChart?.ChartDifficultyType ?? pOptions.DefaultDifficultyType,
			};
			p.RecentFiles.RemoveAll(info => info.FileName == ActiveSong.FileFullPath);
			p.RecentFiles.Insert(0, savedSongInfo);
			if (p.RecentFiles.Count > pOptions.RecentFilesHistorySize)
			{
				p.RecentFiles.RemoveRange(
					pOptions.RecentFilesHistorySize,
					p.RecentFiles.Count - pOptions.RecentFilesHistorySize);
			}
		}
		


		/// <summary>
		/// Helper method when loading a Song to select the best Chart to be the active Chart.
		/// </summary>
		/// <param name="song">Song.</param>
		/// <param name="preferredChartType">The preferred ChartType (StepMania StepsType) to use.</param>
		/// <param name="preferredChartDifficultyType">The preferred DifficultyType to use.</param>
		/// <returns>Best Chart to use or null if no Charts exist.</returns>
		private EditorChart SelectBestChart(EditorSong song, ChartType preferredChartType, ChartDifficultyType preferredChartDifficultyType)
		{
			if (song.Charts.Count == 0)
				return null;
			var hasChartsOfPreferredType = song.Charts.TryGetValue(preferredChartType, out var preferredChartsByType);

			// Choose the preferred chart, if it exists.
			if (hasChartsOfPreferredType)
			{
				foreach (var chart in preferredChartsByType)
				{
					if (chart.ChartDifficultyType == preferredChartDifficultyType)
						return chart;
				}
			}

			var orderedDifficultyTypes = new[]
			{
				ChartDifficultyType.Challenge,
				ChartDifficultyType.Hard,
				ChartDifficultyType.Medium,
				ChartDifficultyType.Easy,
				ChartDifficultyType.Beginner,
				ChartDifficultyType.Edit,
			};

			// If the preferred chart doesn't exist, try to choose the highest difficulty type
			// of the preferred chart type.
			if (hasChartsOfPreferredType)
			{
				foreach (var currDifficultyType in orderedDifficultyTypes)
				{
					foreach (var chart in preferredChartsByType)
					{
						if (chart.ChartDifficultyType == currDifficultyType)
							return chart;
					}
				}
			}

			// No charts of the specified type exist. Try the next best type.
			ChartType nextBestChartType = ChartType.dance_single;
			var hasNextBestChartType = true;
			if (preferredChartType == ChartType.dance_single)
				nextBestChartType = ChartType.dance_double;
			else if (preferredChartType == ChartType.dance_double)
				nextBestChartType = ChartType.dance_single;
			else if (preferredChartType == ChartType.pump_single)
				nextBestChartType = ChartType.pump_double;
			else if (preferredChartType == ChartType.pump_double)
				nextBestChartType = ChartType.pump_single;
			else
				hasNextBestChartType = false;
			if (hasNextBestChartType)
			{
				if (song.Charts.TryGetValue(nextBestChartType, out var nextBestChartsByType))
				{
					foreach (var currDifficultyType in orderedDifficultyTypes)
					{
						foreach (var chart in nextBestChartsByType)
						{
							if (chart.ChartDifficultyType == currDifficultyType)
								return chart;
						}
					}
				}
			}

			// At this point, just return the first chart we have.
			foreach (var charts in song.Charts)
			{
				return charts.Value[0];
			}
			return null;
		}

		private string GetFullPathToMusicFile()
		{
			string musicFile = null;

			// If the active chart has a music file defined, use that.
			if (ActiveChart != null)
				musicFile = ActiveChart.MusicPath;

			// If the active chart does not have a music file defined, fall back to use the song's music file.
			if (string.IsNullOrEmpty(musicFile))
				musicFile = ActiveSong?.MusicPath;

			return GetFullPathToSongResource(musicFile);
		}

		private string GetFullPathToMusicPreviewFile()
		{
			return GetFullPathToSongResource(ActiveSong?.MusicPreviewPath);
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
					fullPath = Path.Combine(ActiveSong.FileDirectory, relativeFile);
			}

			return fullPath;
		}

		private void UnloadSongResources()
		{
			StopPlayback();
			MusicManager.UnloadAsync();
			LaneEditStates = Array.Empty<LaneEditState>();
			ClearSelectedEvents();
			ActiveSong = null;
			ActiveChart = null;
			Position.ActiveChart = null;
			ArrowGraphicManager = null;
			Receptors = null;
			NextAutoPlayNotes = null;
			EditorMouseState.SetActiveChart(null);
			UpdateWindowTitle();
			ActionQueue.Instance.Clear();
		}

		private void UpdateWindowTitle()
		{
			var hasUnsavedChanges = ActionQueue.Instance.HasUnsavedChanges();
			var appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
			var sb = new StringBuilder();
			var title = "New File";
			if (ActiveSong != null && !string.IsNullOrEmpty(ActiveSong.FileName))
			{
				title = ActiveSong.FileName;
			}
			sb.Append(title);
			if (hasUnsavedChanges)
			{
				sb.Append(" * ");
			}
			if (ActiveChart != null)
			{ 
				sb.Append($" [{GetPrettyEnumString(ActiveChart.ChartType)} - {GetPrettyEnumString(ActiveChart.ChartDifficultyType)}]");
			}
			sb.Append(" - ");
			sb.Append(appName);
			Window.Title = sb.ToString();
		}

		#endregion Loading

		#region Selection

		private void SelectEvent(EditorEvent e, bool setLastSelected)
		{
			if (setLastSelected)
				LastSelectedEvent = e;
			if (e.IsSelected())
				return;
			e.Select();
			foreach (var selectedEvent in e.GetEventsSelectedTogether())
				SelectedEvents.Add(selectedEvent);
		}

		private void DeSelectEvent(EditorEvent e)
		{
			if (!e.IsSelected())
				return;
			if (LastSelectedEvent == e)
				LastSelectedEvent = null;
			e.DeSelect();
			foreach (var selectedEvent in e.GetEventsSelectedTogether())
				SelectedEvents.Remove(selectedEvent);
		}

		private void ClearSelectedEvents()
		{
			foreach (var selectedEvent in SelectedEvents)
				selectedEvent.DeSelect();
			SelectedEvents.Clear();
			LastSelectedEvent = null;
		}

		public void OnDelete()
		{
			if (ActiveChart == null || SelectedEvents.Count < 1)
				return;
			var eventsToDelete = new List<EditorEvent>();
			foreach(var editorEvent in SelectedEvents)
			{
				if (!editorEvent.CanBeDeleted)
					continue;
				eventsToDelete.Add(editorEvent);
			}
			if (eventsToDelete.Count == 0)
				return;
			ActionQueue.Instance.Do(new ActionDeleteEditorEvents(eventsToDelete, false));
		}

		public void OnEventsDeleted()
		{
			ClearSelectedEvents();
		}

		public void OnSelectAll()
		{
			OnSelectAllImpl((EditorEvent e) => { return e.IsSelectableWithoutModifiers(); });
		}

		public void OnSelectAllAlt()
		{
			OnSelectAllImpl((EditorEvent e) => { return e.IsSelectableWithModifiers(); });
		}

		public void OnSelectAllShift()
		{
			OnSelectAllImpl((EditorEvent e) => { return e.IsSelectableWithoutModifiers() || e.IsSelectableWithModifiers(); });
		}

		private void OnSelectAllImpl(Func<EditorEvent, bool> isSelectable)
		{
			if (ActiveChart == null)
				return;

			EditorEvent lastEvent = null;
			foreach (var editorEvent in ActiveChart.EditorEvents)
			{
				if (isSelectable(editorEvent))
				{
					SelectEvent(editorEvent, false);
					lastEvent = editorEvent;
				}
			}
			if (lastEvent != null)
				SelectEvent(lastEvent, true);
		}

		/// <summary>
		/// Finishes selecting a region with the mouse.
		/// </summary>
		private void FinishSelectedRegion()
		{
			if (ActiveChart == null)
			{
				SelectedRegion.Stop();
				return;
			}

			// Collect the newly selected notes.
			var newlySelectedEvents = new List<EditorEvent>();

			var alt = KeyCommandManager.IsKeyDown(Keys.LeftAlt);
			Func<EditorEvent, bool> isSelectable = alt ?
				((EditorEvent e) => { return e.IsSelectableWithModifiers(); })
				: ((EditorEvent e) => { return e.IsSelectableWithoutModifiers(); });

			var (arrowTexture, _) = ArrowGraphicManager.GetArrowTexture(0, 0, false);
			double arrowWidthUnscaled, arrowHeightUnscaled;
			(arrowWidthUnscaled, arrowHeightUnscaled) = TextureAtlas.GetDimensions(arrowTexture);
			var halfArrowH = arrowHeightUnscaled * GetSizeZoom() * 0.5;

			// For clicking, we want to select only one note. The latest note whose bounding rect
			// overlaps with the point that was clicked. The events are sorted but we cannot binary
			// search them because we only want to consider events which are in the same lane as
			// the click. A binary search won't consider every event so we may miss an event which
			// overlaps the click. However, the visible events list is limited in size such that it
			// small enough to iterate through when updating and rendering. A click happens rarely,
			// and when it does happen it happens at most once per frame, so iterating when clicking
			// is performant enough.
			var isClick = SelectedRegion.IsClick();
			if (isClick)
			{
				var (x, y) = SelectedRegion.GetCurrentPosition();
				EditorEvent best = null;
				foreach (var visibleEvent in VisibleEvents)
				{
					// Skip hold end notes. Visible events is sorted for rendering and they are rendered
					// first. We will capture clicking holds by the hold start notes.
					if (visibleEvent is EditorHoldEndNoteEvent)
						continue;
					// Early out if we have searched beyond the selected y. Add an extra half arrow
					// height to this check so that short miscellaneous events do not cause us to
					// early out prematurealy.
					if (visibleEvent.Y > y + halfArrowH)
						break;
					if (visibleEvent.DoesPointIntersect(x, y))
						best = visibleEvent;
				}

				if (best != null)
					newlySelectedEvents.Add(best);
			}

			// A region was selected, collect all notes in the selected region.
			else
			{
				var lanesWidth = ActiveChart.NumInputs * arrowWidthUnscaled;
				var (minChartX, maxChartX) = SelectedRegion.GetSelectedXChartSpaceRange();
				var minLane = (int)Math.Floor((minChartX + lanesWidth * 0.5) / arrowWidthUnscaled);
				var maxLane = (int)Math.Floor((maxChartX + lanesWidth * 0.5) / arrowWidthUnscaled);

				// TODO: Selecting misc events isn't working as intended.

				if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.ConstantTime)
				{
					var (minTime, maxTime) = SelectedRegion.GetSelectedChartTimeRange();

					// Extend the time to capture the tops and bottoms of arrows.
					// This is an approximation as there may be rate altering events during the range
					// between the arrow's center and its edges.
					minTime -= GetTimeRangeOfYPixelDurationAtTime(minTime, halfArrowH);
					maxTime += GetTimeRangeOfYPixelDurationAtTime(maxTime, halfArrowH);

					var enumerator = ActiveChart.EditorEvents.FindFirstAfterChartTime(minTime);

					while (enumerator.MoveNext())
					{
						if (enumerator.Current.GetChartTime() > maxTime)
							break;
						if (!isSelectable(enumerator.Current))
							continue;
						var lane = enumerator.Current.GetLane();
						if (lane < minLane || lane > maxLane)
							continue;
						newlySelectedEvents.Add(enumerator.Current);
					}
				}
				else
				{
					var (minPosition, maxPosition) = SelectedRegion.GetSelectedChartPositionRange();

					// Extend the position to capture the tops and bottoms of arrows.
					// This is an approximation as there may be rate altering events during the range
					// between the arrow's center and its edges.
					minPosition -= GetPositionRangeOfYPixelDurationAtTime(minPosition, halfArrowH);
					maxPosition += GetPositionRangeOfYPixelDurationAtTime(maxPosition, halfArrowH);

					var enumerator = ActiveChart.EditorEvents.FindFirstAfterChartPosition(minPosition);

					while (enumerator.MoveNext())
					{
						if (enumerator.Current.GetRow() > maxPosition)
							break;
						if (!isSelectable(enumerator.Current))
							continue;
						var lane = enumerator.Current.GetLane();
						if (lane < minLane || lane > maxLane)
							continue;
						newlySelectedEvents.Add(enumerator.Current);
					}
				}
			}

			var ctrl = KeyCommandManager.IsKeyDown(Keys.LeftControl);
			var shift = KeyCommandManager.IsKeyDown(Keys.LeftShift);

			// If holding shift, select everything from the previously selected note
			// to the newly selected notes, in addition to the newly selected notes.
			if (shift)
			{
				if (LastSelectedEvent != null && newlySelectedEvents.Count > 0)
				{
					EventTree.Enumerator enumerator;
					EditorEvent end;
					var firstNewNote = newlySelectedEvents[0];
					if (firstNewNote.CompareTo(LastSelectedEvent) < 0)
					{
						enumerator = ActiveChart.EditorEvents.Find(firstNewNote);
						end = LastSelectedEvent;
					}
					else
					{
						enumerator = ActiveChart.EditorEvents.Find(LastSelectedEvent);
						end = firstNewNote;
					}
					if (enumerator != null)
					{
						bool last;
						while (enumerator.MoveNext())
						{
							last = enumerator.Current == end;
							if (isSelectable(enumerator.Current))
							{
								SelectEvent(enumerator.Current, last);
							}
							if (last)
								break;
						}
					}
				}

				// Select the newly selected notes.
				for (var i = 0; i < newlySelectedEvents.Count; i++)
				{
					SelectEvent(newlySelectedEvents[i], i == newlySelectedEvents.Count - 1);
				}
			}

			// If holding control, toggle the selected notes.
			else if (ctrl)
			{
				for (var i = 0; i < newlySelectedEvents.Count; i++)
				{
					if (newlySelectedEvents[i].IsSelected())
						DeSelectEvent(newlySelectedEvents[i]);
					else
						SelectEvent(newlySelectedEvents[i], i == newlySelectedEvents.Count - 1);
				}
			}

			// If holding no modifier key, deselect everything and select the newly
			// selected notes.
			else
			{
				ClearSelectedEvents();
				for (var i = 0; i < newlySelectedEvents.Count; i++)
				{
					SelectEvent(newlySelectedEvents[i], i == newlySelectedEvents.Count - 1);
				}
			}

			SelectedRegion.Stop();
		}

		/// <summary>
		/// Given a duration in pixel space in y, returns that duration as time based on the
		/// rate altering event present at the given time. Note that this duration is an
		/// approximation as the given pixel range may cover multiple rate altering events
		/// and only the rate altering event present at the given time is considered.
		/// </summary>
		/// <param name="time">Time to use for determining the current rate.</param>
		/// <param name="duration">Y duration in pixels,</param>
		/// <returns>Duration in time.</returns>
		/// <remarks>
		/// Helper for FinishSelectedRegion. Used to approximate the time of arrow tops and bottoms
		/// from their centers.
		/// </remarks>
		private double GetTimeRangeOfYPixelDurationAtTime(double time, double duration)
		{
			var currentTime = Position.ChartTime;
			double currentPosition = 0.0;
			if (!ActiveChart.TryGetChartPositionFromTime(currentTime, ref currentPosition))
				return 0.0;
			var interpolatedScrollRate = GetInterpolatedScrollRate(currentTime, currentPosition);

			var rae = ActiveChart.FindActiveRateAlteringEventForTime(time);
			var spacingHelper = EventSpacingHelper.GetSpacingHelper(ActiveChart);
			spacingHelper.UpdatePpsAndPpr(rae, interpolatedScrollRate, GetSpacingZoom());

			return duration / spacingHelper.GetPps();
		}

		/// <summary>
		/// Given a duration in pixel space in y, returns that duration as rows based on the
		/// rate altering event present at the given time. Note that this duration is an
		/// approximation as the given pixel range may cover multiple rate altering events
		/// and only the rate altering event present at the given position is considered.
		/// </summary>
		/// <param name="position">Chart position to use for determining the current rate.</param>
		/// <param name="duration">Y duration in pixels,</param>
		/// <returns>Duration in rows.</returns>
		/// <remarks>
		/// Helper for FinishSelectedRegion. Used to approximate the row of arrow tops and bottoms
		/// from their centers.
		/// </remarks>
		private double GetPositionRangeOfYPixelDurationAtTime(double position, double duration)
		{
			var currentPosition = Position.ChartPosition;
			double currentTime = 0.0;
			if (!ActiveChart.TryGetTimeFromChartPosition(currentPosition, ref currentTime))
				return 0.0;
			var interpolatedScrollRate = GetInterpolatedScrollRate(currentTime, currentPosition);

			var rae = ActiveChart.FindActiveRateAlteringEventForPosition(position);
			var spacingHelper = EventSpacingHelper.GetSpacingHelper(ActiveChart);
			spacingHelper.UpdatePpsAndPpr(rae, interpolatedScrollRate, GetSpacingZoom());

			return duration / spacingHelper.GetPpr();
		}

		#endregion Selection

		public bool IsChartSupported(Chart chart)
		{
			if (!TryGetChartType(chart.Type, out var chartType))
				return false;
			var typeSupported = false;
			foreach (var supportedType in SupportedChartTypes)
			{
				if (supportedType == chartType)
				{
					typeSupported = true;
					break;
				}
			}

			if (!typeSupported)
				return false;

			if (!Enum.TryParse<ChartDifficultyType>(chart.DifficultyType, out _))
				return false;

			return true;
		}

		private void OnDecreaseSnap()
		{
			SnapIndex--;
			if (SnapIndex < 0)
				SnapIndex = SnapLevels.Length - 1;

			if (SnapLevels[SnapIndex].Rows == 0)
				Logger.Info("Snap off");
			else
				Logger.Info($"Snap to 1/{MaxValidDenominator / SnapLevels[SnapIndex].Rows * 4}");
		}

		private void OnIncreaseSnap()
		{
			SnapIndex++;
			if (SnapIndex >= SnapLevels.Length)
				SnapIndex = 0;

			if (SnapLevels[SnapIndex].Rows == 0)
				Logger.Info("Snap off");
			else
				Logger.Info($"Snap to 1/{MaxValidDenominator / SnapLevels[SnapIndex].Rows * 4}");
		}

		private void OnMoveUp()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();

			var rows = SnapLevels[SnapIndex].Rows;
			if (rows == 0)
			{
				OnMoveToPreviousMeasure();
			}
			else
			{
				var newChartPosition = ((int)Position.ChartPosition / rows) * rows;
				if (newChartPosition == (int)Position.ChartPosition)
					newChartPosition -= rows;
				Position.ChartPosition = newChartPosition;
				UpdateAutoPlayFromScrolling();
			}
		}

		private void OnMoveDown()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();

			var rows = SnapLevels[SnapIndex].Rows;
			if (rows == 0)
			{
				OnMoveToNextMeasure();
			}
			else
			{
				Position.ChartPosition = ((int)Position.ChartPosition / rows) * rows + rows;
				UpdateAutoPlayFromScrolling();
			}
		}

		private void OnMoveToPreviousMeasure()
		{
			var rate = ActiveChart?.FindActiveRateAlteringEventForPosition(Position.ChartPosition);
			if (rate == null)
				return;
			var sig = rate.LastTimeSignature.Signature;
			var rows = sig.Numerator * (MaxValidDenominator * NumBeatsPerMeasure / sig.Denominator);
			Position.ChartPosition -= rows;

			UpdateAutoPlayFromScrolling();
		}

		private void OnMoveToNextMeasure()
		{
			var rate = ActiveChart?.FindActiveRateAlteringEventForPosition(Position.ChartPosition);
			if (rate == null)
				return;
			var sig = rate.LastTimeSignature.Signature;
			var rows = sig.Numerator * (MaxValidDenominator * NumBeatsPerMeasure / sig.Denominator);
			Position.ChartPosition += rows;

			UpdateAutoPlayFromScrolling();
		}

		private void OnMoveToChartStart()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();

			Position.ChartPosition = 0.0;

			UpdateAutoPlayFromScrolling();
		}

		private void OnMoveToChartEnd()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();
			
			if (ActiveChart != null)
				Position.ChartPosition = ActiveChart.GetEndPosition();
			else
				Position.ChartPosition = 0.0;

			UpdateAutoPlayFromScrolling();
		}

		private void OnShiftDown()
		{
			if (LaneEditStates == null)
				return;

			foreach (var laneEditState in LaneEditStates)
			{
				if (laneEditState.IsActive() && laneEditState.GetEventBeingEdited() != null)
				{
					laneEditState.Shift(false);
				}
			}
		}

		private void OnShiftUp()
		{
			if (LaneEditStates == null)
				return;

			foreach (var laneEditState in LaneEditStates)
			{
				if (laneEditState.IsActive() && laneEditState.GetEventBeingEdited() != null)
				{
					laneEditState.Shift(true);
				}
			}
		}

		private void OnLaneInputDown(int lane)
		{
			if (ActiveChart == null)
				return;
			if (lane < 0 || lane >= ActiveChart.NumInputs)
				return;

			Receptors?[lane].OnInputDown();

			if (Position.ChartPosition < 0)
				return;
			
			// TODO: If playing we should take sync into account and adjust the position.

			var row = Position.GetNearestRow();
			if (SnapLevels[SnapIndex].Rows != 0)
			{
				var snappedRow = (row / SnapLevels[SnapIndex].Rows) * SnapLevels[SnapIndex].Rows;
				if (row - snappedRow >= (SnapLevels[SnapIndex].Rows * 0.5))
					snappedRow += SnapLevels[SnapIndex].Rows;
				row = snappedRow;
			}

			// If there is a tap, mine, or hold start at this location, delete it now.
			// Deleting immediately feels more responsive than deleting on the input up event.
			EditorAction deleteAction = null;
			var existingEvent = ActiveChart.EditorEvents.FindNoteAt(row, lane, true);
			if (existingEvent is EditorMineNoteEvent || existingEvent is EditorTapNoteEvent)
				deleteAction = new ActionDeleteEditorEvents(existingEvent);
			else if (existingEvent is EditorHoldStartNoteEvent hsn && hsn.GetRow() == row)
				deleteAction = new ActionDeleteHoldEvent(hsn);
			if (deleteAction != null)
			{
				LaneEditStates[lane].StartEditingWithDelete(existingEvent.GetRow(), deleteAction);
			}

			SetLaneInputDownNote(lane, row);

			// If we are playing, immediately commit the note so it comes out as a tap and not a short hold.
			if (Playing)
			{
				OnLaneInputUp(lane);
			}
		}

		private void SetLaneInputDownNote(int lane, int row)
		{
			// If the existing state is only a delete, revert back to that delete operation.
			if (LaneEditStates[lane].IsOnlyDelete())
			{
				LaneEditStates[lane].Clear(false);
			}

			// Otherwise, set the state to be editing a tap or a mine.
			else
			{
				double chartTime = 0.0;
				ActiveChart.TryGetTimeFromChartPosition(row, ref chartTime);

				if (KeyCommandManager.IsKeyDown(Keys.LeftShift))
				{
					LaneEditStates[lane].SetEditingTapOrMine(new EditorMineNoteEvent(ActiveChart, new LaneNote
					{
						Lane = lane,
						IntegerPosition = row,
						TimeMicros = ToMicros(chartTime),
						SourceType = NoteChars[(int)NoteType.Mine].ToString()
					}, true));
				}
				else
				{
					LaneEditStates[lane].SetEditingTapOrMine(new EditorTapNoteEvent(ActiveChart, new LaneTapNote
					{
						Lane = lane,
						IntegerPosition = row,
						TimeMicros = ToMicros(chartTime),
					}, true));
				}
			}
		}

		private void OnLaneInputUp(int lane)
		{
			if (ActiveChart == null)
				return;
			if (lane < 0 || lane >= ActiveChart.NumInputs)
				return;

			Receptors?[lane].OnInputUp();

			if (!LaneEditStates[lane].IsActive())
				return;

			// If this action is only a delete, just commit the existing delete action.
			if (LaneEditStates[lane].IsOnlyDelete())
			{
				LaneEditStates[lane].Commit();
				return;
			}

			var row = LaneEditStates[lane].GetEventBeingEdited().GetRow();
			var existingEvent = ActiveChart.EditorEvents.FindNoteAt(row, lane, true);

			var newNoteIsMine = LaneEditStates[lane].GetEventBeingEdited() is EditorMineNoteEvent;
			var newNoteIsTap = LaneEditStates[lane].GetEventBeingEdited() is EditorTapNoteEvent;

			// Handle a new tap note overlapping an existing note.
			if (newNoteIsMine || newNoteIsTap)
			{
				if (existingEvent != null)
				{
					var existingIsTap = existingEvent is EditorTapNoteEvent;
					var existingIsMine = existingEvent is EditorMineNoteEvent;

					// Tap note over existing tap note.
					if (existingIsTap || existingIsMine)
					{
						// If the existing note is a tap and this note is a mine, then replace it with the mine.
						if (!existingIsMine && newNoteIsMine)
						{
							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
							{
								new ActionDeleteEditorEvents(existingEvent)
							});
						}

						// In all other cases, just delete the existing note and don't add anything else.
						else
						{
							LaneEditStates[lane].Clear(true);
							ActionQueue.Instance.Do(new ActionDeleteEditorEvents(existingEvent));
							return;
						}
					}

					// Tap note over hold note.
					else if (existingEvent is EditorHoldStartNoteEvent || existingEvent is EditorHoldEndNoteEvent)
					{
						var holdStart = existingEvent is EditorHoldStartNoteEvent
							? (EditorHoldStartNoteEvent)existingEvent
							: ((EditorHoldEndNoteEvent)existingEvent).GetHoldStartNote();

						// If the tap note starts at the beginning of the hold, delete the hold.
						if (row == holdStart.GetRow())
						{
							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
							{
								new ActionDeleteHoldEvent(holdStart)
							});
						}

						// If the tap note is in the in the middle of the hold, shorten the hold.
						else
						{
							// Move the hold up by a 16th.
							var newHoldEndRow = row - (MaxValidDenominator / 4);

							// If the hold would have a non-positive length, delete it and replace it with a tap.
							if (newHoldEndRow <= holdStart.GetRow())
							{
								var deleteHold = new ActionDeleteHoldEvent(holdStart);
								var insertNewNoteAtHoldStart = new ActionAddEditorEvent(new EditorTapNoteEvent(ActiveChart, new LaneTapNote
								{
									Lane = lane,
									IntegerPosition = holdStart.GetEvent().IntegerPosition,
									TimeMicros = holdStart.GetEvent().TimeMicros,
								}));

								LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
								{
									deleteHold,
									insertNewNoteAtHoldStart
								});
							}

							// Otherwise, the new length is valid. Update it.
							else
							{
								var changeLength = new ActionChangeHoldLength(holdStart, newHoldEndRow - holdStart.GetRow());
								LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
								{
									changeLength
								});
							}
						}
					}
				}
			}

			// Handle a new hold note overlapping any existing notes
			else if (LaneEditStates[lane].GetEventBeingEdited() is EditorHoldStartNoteEvent editHsn)
			{
				var length = editHsn.GetLength();
				var roll = editHsn.IsRoll();

				if (existingEvent is EditorHoldEndNoteEvent hen)
					existingEvent = hen.GetHoldStartNote();

				// If the hold is completely within another hold, do not add or delete notes, but make sure the outer
				// hold is the same type (hold/roll) as the new type.
				if (existingEvent != null
				    && existingEvent is EditorHoldStartNoteEvent hsnFull
				    && hsnFull.GetRow() <= row
				    && hsnFull.GetRow() + hsnFull.GetLength() >= row + length)
				{
					LaneEditStates[lane].Clear(true);
					if (hsnFull.IsRoll() != roll)
						ActionQueue.Instance.Do(new ActionChangeHoldType(hsnFull, roll));
					return;
				}

				var deleteActions = new List<EditorAction>();

				// If existing holds overlap with only the start or end of the new hold, delete them and extend the
				// new hold to cover their range. We just need to extend the new event now. The deletion of the
				// old event will will be handled below when we check for events fully contained within the new
				// hold region.
				if (existingEvent != null
				    && existingEvent is EditorHoldStartNoteEvent hsnStart
				    && hsnStart.GetRow() < row
				    && hsnStart.GetRow() + hsnStart.GetLength() >= row
				    && hsnStart.GetRow() + hsnStart.GetLength() < row + length)
				{
					row = hsnStart.GetRow();
					length = editHsn.GetHoldEndNote().GetRow() - hsnStart.GetRow();
				}
				existingEvent = ActiveChart.EditorEvents.FindNoteAt(row + length, lane, true);
				if (existingEvent != null
				    && existingEvent is EditorHoldStartNoteEvent hsnEnd
				    && hsnEnd.GetRow() <= row + length
					&& hsnEnd.GetRow() + hsnEnd.GetLength() >= row + length
				    && hsnEnd.GetRow() > row)
				{
					length = hsnEnd.GetRow() + hsnEnd.GetLength() - row;
				}

				// For any event in the same lane within the region of the new hold, delete them.
				var e = ActiveChart.EditorEvents.FindBest(row);
				if (e != null)
				{
					while (e.MoveNext() && e.Current.GetRow() <= row + length)
					{
						if (e.Current.GetRow() < row)
							continue;
						if (e.Current.GetLane() != lane)
							continue;
						if (e.Current.IsBeingEdited())
							continue;
						if (e.Current is EditorTapNoteEvent || e.Current is EditorMineNoteEvent)
							deleteActions.Add(new ActionDeleteEditorEvents(e.Current));
						else if (e.Current is EditorHoldStartNoteEvent innerHsn && innerHsn.GetRow() + innerHsn.GetLength() <= row + length)
							deleteActions.Add(new ActionDeleteHoldEvent(innerHsn));
					}
				}

				// Set the state to be editing a new hold after running the delete actions.
				LaneEditStates[lane].SetEditingHold(ActiveChart, lane, row, LaneEditStates[lane].GetStartingRow(), length, roll, deleteActions);
			}

			LaneEditStates[lane].Commit();
		}

		private void UpdateLaneEditStatesFromPosition()
		{
			if (LaneEditStates == null)
				return;

			var row = Math.Max(0, Position.GetNearestRow());
			for (var lane = 0; lane < LaneEditStates.Length; lane++)
			{
				var laneEditState = LaneEditStates[lane];
				if (!laneEditState.IsActive())
					continue;

				// If moving back to the starting position.
				// In other words, the current state of the note being edited should be a tap.
				if (laneEditState.GetStartingRow() == row)
				{
					// If the event is a hold, convert it to a tap.
					// This will also convert holds to tap even if the starting action was deleting an existing note.
					if (laneEditState.GetEventBeingEdited() is EditorHoldStartNoteEvent)
					{
						SetLaneInputDownNote(lane, row);
					}
				}

				// If the current position is different than the starting position.
				// In other words, the current state of the note being edited should be a hold.
				else
				{
					var holdStartRow = laneEditState.GetStartingRow() < row ? laneEditState.GetStartingRow() : row;
					var holdEndRow = laneEditState.GetStartingRow() > row ? laneEditState.GetStartingRow() : row;

					// If the event is a tap, mine, deletion, or it is a hold with different bounds, convert it to a new hold.
					if (laneEditState.GetEventBeingEdited() is null
					    || laneEditState.GetEventBeingEdited() is EditorTapNoteEvent
					    || laneEditState.GetEventBeingEdited() is EditorMineNoteEvent
					    || (laneEditState.GetEventBeingEdited() is EditorHoldStartNoteEvent h
					        && (holdStartRow != h.GetRow() || holdEndRow != h.GetHoldEndNote().GetRow())))
					{
						var roll = KeyCommandManager.IsKeyDown(Keys.LeftShift);
						LaneEditStates[lane].SetEditingHold(ActiveChart, lane, holdStartRow, laneEditState.GetStartingRow(), holdEndRow - holdStartRow, roll);
					}
				}
			}
		}

		private bool CancelLaneInput()
		{
			var anyCancelled = false;
			foreach (var laneEditState in LaneEditStates)
			{
				if (laneEditState.IsActive())
				{
					laneEditState.Clear(true);
					anyCancelled = true;
				}
			}
			return anyCancelled;
		}

		private void OnUndo()
		{
			ActionQueue.Instance.Undo();
		}

		private void OnRedo()
		{
			ActionQueue.Instance.Redo();
		}

		private void OnEscape()
		{
			if (CancelLaneInput())
				return;
			if (MovingFocalPoint)
			{
				MovingFocalPoint = false;
				Preferences.Instance.PreferencesReceptors.PositionX = (int)FocalPointAtMoveStart.X;
				Preferences.Instance.PreferencesReceptors.PositionY = (int)FocalPointAtMoveStart.Y;
				return;
			}
			if (IsPlayingPreview())
				StopPreview();
			else if (Playing)
				StopPlayback();
			else if (SelectedEvents.Count > 0)
				ClearSelectedEvents();
		}

		public void ClosingForm(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (ActionQueue.Instance.HasUnsavedChanges())
			{
				e.Cancel = true;
				PostSaveFunction = OnExitNoSave;
				ShowSavePopup = true;
			}
		}

		private void OnExit()
		{
			if (ActionQueue.Instance.HasUnsavedChanges())
			{
				PostSaveFunction = OnExitNoSave;
				ShowSavePopup = true;
			}
			else
			{
				OnExitNoSave();
			}
		}

		private void OnExitNoSave()
		{
			Exit();
		}

		private void OnOpen()
		{
			if (ActionQueue.Instance.HasUnsavedChanges())
			{
				PostSaveFunction = OpenSongFile;
				ShowSavePopup = true;
			}
			else
			{
				OpenSongFile();
			}
		}

		private void OnReload()
		{
			OpenRecentIndex = 0;
			OnOpenRecentFile();
		}

		private void OnOpenRecentFile()
		{
			var p = Preferences.Instance;
			if (OpenRecentIndex >= p.RecentFiles.Count)
				return;

			if (ActionQueue.Instance.HasUnsavedChanges())
			{
				PostSaveFunction = OpenRecentFile;
				ShowSavePopup = true;
			}
			else
			{
				OpenRecentFile();
			}
		}

		private void OpenRecentFile()
		{
			var p = Preferences.Instance;
			if (OpenRecentIndex >= p.RecentFiles.Count)
				return;

			OpenSongFileAsync(p.RecentFiles[OpenRecentIndex].FileName,
							p.RecentFiles[OpenRecentIndex].LastChartType,
							p.RecentFiles[OpenRecentIndex].LastChartDifficultyType);
		}

		private void OnNew()
		{
			if (ActionQueue.Instance.HasUnsavedChanges())
			{
				PostSaveFunction = OnNewNoSave;
				ShowSavePopup = true;
			}
			else
			{
				OnNewNoSave();
			}
		}

		private void OnNewNoSave()
		{
			UnloadSongResources();
			ActiveSong = new EditorSong(this, GraphicsDevice, ImGuiRenderer);
			Position.ChartPosition = 0.0;
			DesiredSongTime = Position.SongTime;
			SetZoom(1.0, true);
		}

		private void OnClose()
		{
			if (ActionQueue.Instance.HasUnsavedChanges())
			{
				PostSaveFunction = OnCloseNoSave;
				ShowSavePopup = true;
			}
			else
			{
				OnCloseNoSave();
			}
		}

		private void OnCloseNoSave()
		{
			UnloadSongResources();
			Position.ChartPosition = 0.0;
			DesiredSongTime = Position.SongTime;
			SetZoom(1.0, true);
		}

		private void TryInvokePostSaveFunction()
		{
			if (PostSaveFunction != null)
				PostSaveFunction();
			PostSaveFunction = null;
		}

		private bool CanSaveWithoutLocationPrompt()
		{
			if (ActiveSong == null)
				return false;

			if (ActiveSong.FileFormat == null)
				return false;

			if (string.IsNullOrEmpty(ActiveSong.FileFullPath))
				return false;

			return true;
		}

		private void OnSave()
		{
			if (!CanSaveWithoutLocationPrompt())
			{
				OnSaveAs();
				return;
			}
			Save(ActiveSong.FileFormat.Type, ActiveSong.FileFullPath, ActiveSong);
		}

		private void OnSaveAs()
		{
			if (ActiveSong == null)
				return;

			SaveFileDialog saveFileDialog1 = new SaveFileDialog();
			saveFileDialog1.Filter = "SSC File|*.ssc|SM File|*.sm";
			saveFileDialog1.Title = "Save As...";
			saveFileDialog1.FilterIndex = 0;
			if (ActiveSong.FileFormat != null && ActiveSong.FileFormat.Type == FileFormatType.SM)
			{
				saveFileDialog1.FilterIndex = 2;
			}
			if (saveFileDialog1.ShowDialog() != DialogResult.OK)
				return;
			
			var fullPath = saveFileDialog1.FileName;
			var extension = System.IO.Path.GetExtension(fullPath);
			var fileFormat = FileFormat.GetFileFormatByExtension(extension);
			if (fileFormat == null)
				return;

			Save(fileFormat.Type, fullPath, ActiveSong);
		}

		private void Save(FileFormatType fileType, string fullPath, EditorSong editorSong)
		{
			// TODO: Check for incompatible features with SM format.
			if (fileType == FileFormatType.SM)
			{

			}

			// Temp hack to not overwrite original file.
			//var start = fullPath.Substring(0, fullPath.LastIndexOf('.'));
			//var end = fullPath.Substring(fullPath.LastIndexOf('.'));
			//fullPath = $"{start}-exported{end}";

			var song = ActiveSong.SaveToSong();
			var config = new SMWriterBase.SMWriterBaseConfig
			{
				FilePath = fullPath,
				Song = song,
				MeasureSpacingBehavior = SMWriterBase.MeasureSpacingBehavior.UseLeastCommonMultiple,
				PropertyEmissionBehavior = SMWriterBase.PropertyEmissionBehavior.Stepmania,
			};
			switch (fileType)
			{
				case FileFormatType.SM:
					new SMWriter(config).Save();
					break;
				case FileFormatType.SSC:
					new SSCWriter(config).Save();
					break;
				default:
					Logger.Error("Unsupported file format. Cannot save.");
					break;
			}

			// Update the ActiveSong's file path information.
			editorSong.SetFullFilePath(fullPath);
			UpdateWindowTitle();
			UpdateRecentFilesForActiveSong();

			ActionQueue.Instance.OnSaved();

			TryInvokePostSaveFunction();
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

		public void OnSongMusicChanged(EditorSong song)
		{
			OnMusicChangedInternal();
		}

		public void OnSongMusicPreviewChanged(EditorSong song)
		{
			if (ActiveChart == null || song != ActiveChart.EditorSong)
				return;
			OnMusicPreviewChangedInternal();
		}

		public void OnSongMusicOffsetChanged(EditorSong song)
		{
			if (ActiveChart == null || song != ActiveChart.EditorSong)
				return;
			OnMusicOffsetChangedInternal();
		}

		public void OnChartMusicChanged(EditorChart chart)
		{
			if (ActiveChart == null || chart != ActiveChart)
				return;
			OnMusicChangedInternal();
		}

		public void OnChartMusicPreviewChanged(EditorChart chart)
		{
			if (ActiveChart == null || chart != ActiveChart)
				return;
			OnMusicPreviewChangedInternal();
		}

		public void OnChartMusicOffsetChanged(EditorChart chart)
		{
			if (ActiveChart == null || chart != ActiveChart)
				return;
			OnMusicOffsetChangedInternal();
		}

		private void OnMusicChangedInternal()
		{
			StopPreview();
			MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), GetSongTime);
		}

		private void OnMusicPreviewChangedInternal()
		{
			StopPreview();
			MusicManager.LoadMusicPreviewAsync(GetFullPathToMusicPreviewFile());
		}

		private void OnMusicOffsetChangedInternal()
		{
			// Re-set the position to recompute the chart and song times.
			Position.ChartPosition = Position.ChartPosition;
		}

		private double GetSongTime()
		{
			return Position.SongTime;
		}

		internal EditorPosition GetPosition()
		{
			return Position;
		}

		internal EditorMouseState GetMouseState()
		{
			return EditorMouseState;
		}

		public EditorChart GetActiveChart()
		{
			return ActiveChart;
		}

		public void OnChartDifficultyTypeChanged(EditorChart chart)
		{
			if (ActiveChart == null || chart != ActiveChart)
				return;
			ActiveChart.EditorSong.UpdateChartSort();
		}

		public void OnChartRatingChanged(EditorChart chart)
		{
			if (ActiveChart == null || chart != ActiveChart)
				return;
			ActiveChart.EditorSong.UpdateChartSort();
		}

		public void OnChartNameChanged(EditorChart chart)
		{
			if (ActiveChart == null || chart != ActiveChart)
				return;
			ActiveChart.EditorSong.UpdateChartSort();
		}

		public void OnChartDescriptionChanged(EditorChart chart)
		{
			if (ActiveChart == null || chart != ActiveChart)
				return;
			ActiveChart.EditorSong.UpdateChartSort();
		}

		public void OnChartSelected(EditorChart chart, bool undoable = true)
		{
			if (ActiveSong == null || ActiveChart == chart)
				return;

			// If the active chart is being changed as an undoable action, enqueue the action and return.
			// The ActionSelectChart will invoke this method again with undoable set to false.
			if (undoable)
			{
				ActionQueue.Instance.Do(new ActionSelectChart(this, chart));
				return;
			}

			ActiveChart = chart;

			// The Position needs to know about the active chart for doing time and row calculations.
			Position.ActiveChart = ActiveChart;
			EditorMouseState.SetActiveChart(ActiveChart);

			// The preview event region needs to know about the active chart since rendering the preview
			// with rate altering events from the chart requires knowing the chart's music offset.
			ActiveSong.PreviewEvent.ActiveChart = ActiveChart;

			if (ActiveChart != null)
			{
				// Update the recent file entry for the current song so that tracks the selected chart
				var p = Preferences.Instance;
				if (p.RecentFiles.Count > 0 && p.RecentFiles[0].FileName == ActiveSong.FileFullPath)
				{
					p.RecentFiles[0].LastChartType = ActiveChart.ChartType;
					p.RecentFiles[0].LastChartDifficultyType = ActiveChart.ChartDifficultyType;
				}

				// The receptors and arrow graphics depend on the active chart.
				ArrowGraphicManager = ArrowGraphicManager.CreateArrowGraphicManager(ActiveChart.ChartType);
				var laneEditStates = new LaneEditState[ActiveChart.NumInputs];
				var receptors = new Receptor[ActiveChart.NumInputs];
				NextAutoPlayNotes = new EditorEvent[ActiveChart.NumInputs];
				for (var i = 0; i < ActiveChart.NumInputs; i++)
				{
					laneEditStates[i] = new LaneEditState();
					receptors[i] = new Receptor(i, ArrowGraphicManager, ActiveChart);
				}
				Receptors = receptors;
				LaneEditStates = laneEditStates;
			}
			else
			{
				ArrowGraphicManager = null;
				Receptors = null;
				LaneEditStates = null;
			}

			// Window title depends on the active chart.
			UpdateWindowTitle();

			// Start loading music for this Chart.
			OnChartMusicChanged(ActiveChart);
			OnChartMusicPreviewChanged(ActiveChart);
		}

		public EditorChart AddChart(ChartType chartType, bool selectNewChart)
		{
			if (ActiveSong == null)
				return null;
			var chart = ActiveSong.AddChart(chartType);
			if (selectNewChart)
				OnChartSelected(chart, false);
			return chart;
		}

		public EditorChart AddChart(EditorChart chart, bool selectNewChart)
		{
			if (ActiveSong == null)
				return null;
			ActiveSong.AddChart(chart);
			if (selectNewChart)
				OnChartSelected(chart, false);
			return chart;
		}

		public void DeleteChart(EditorChart chart, EditorChart chartToSelect)
		{
			if (ActiveSong == null)
				return;
			ActiveSong.DeleteChart(chart);
			if (chartToSelect != null)
				OnChartSelected(chartToSelect, false);
			else if (ActiveChart == chart)
			{
				var newActiveChart = SelectBestChart(ActiveSong, ActiveChart.ChartType, ActiveChart.ChartDifficultyType);
				OnChartSelected(newActiveChart, false);
			}
		}
	}
}
