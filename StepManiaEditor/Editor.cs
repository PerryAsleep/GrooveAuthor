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
using StepManiaLibrary;
using static StepManiaEditor.Utils;
using static StepManiaEditor.ImGuiUtils;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Path = Fumen.Path;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using static Fumen.Converters.SMCommon;
using System.Text;
using System.Media;
using System.IO;
using System.Linq;
using static StepManiaEditor.EditorSongImageUtils;
using StepManiaLibrary.PerformedChart;
using MonoGameExtensions;

namespace StepManiaEditor;

/// <summary>
/// The Editor.
/// Implemented as a MonoGame Game.
/// </summary>
internal sealed class Editor :
	Game,
	Fumen.IObserver<EditorSong>,
	Fumen.IObserver<EditorChart>,
	Fumen.IObserver<PreferencesOptions>,
	Fumen.IObserver<PreferencesExpressedChartConfig>,
	Fumen.IObserver<ActionQueue>
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
		MostCommonTempo,
	}

	public class SnapData
	{
		public int Rows;
		public string Texture;
	}

	private SnapData[] SnapLevels;
	private int SnapIndex;

	private readonly EditorMouseState EditorMouseState = new();
	private bool CanShowRightClickPopupThisFrame;

	private bool MovingFocalPoint;
	private Vector2 FocalPointAtMoveStart;
	private Vector2 FocalPointMoveOffset;

	private string PendingOpenFileName;
	private ChartType PendingOpenFileChartType;
	private ChartDifficultyType PendingOpenFileChartDifficultyType;

	private bool UnsavedChangesLastFrame;
	private string PendingOpenSongFileName;
	private string PendingMusicFile;
	private string PendingImageFile;
	private string PendingVideoFile;
	private string PendingLyricsFile;
	private bool ShowSavePopup;
	private Action PostSaveFunction;
	private int OpenRecentIndex;

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
	private readonly SoundManager SoundManager = new();
	private MusicManager MusicManager;
	private MiniMap MiniMap;
	private ArrowGraphicManager ArrowGraphicManager;
	private UISongProperties UISongProperties;
	private UIChartProperties UIChartProperties;
	private UIChartList UIChartList;
	private UIWaveFormPreferences UIWaveFormPreferences;
	private UIScrollPreferences UIScrollPreferences;
	private UISelectionPreferences UISelectionPreferences;
	private UIMiniMapPreferences UIMiniMapPreferences;
	private UIReceptorPreferences UIReceptorPreferences;
	private UIOptions UIOptions;
	private UIChartPosition UIChartPosition;
	private UIExpressedChartConfig UIExpressedChartConfig;
	private UIPerformedChartConfig UIPerformedChartConfig;
	private UIAutogenConfigs UIAutogenConfigs;
	private UIAutogenChart UIAutogenChart;
	private ZoomManager ZoomManager;
	private StaticTextureAtlas TextureAtlas;
	private IntPtr TextureAtlasImGuiTexture;

	private Effect FxaaEffect;
	private Effect WaveformColorEffect;
	private RenderTarget2D[] WaveformRenderTargets;

	private CancellationTokenSource LoadSongCancellationTokenSource;
	private Task LoadSongTask;

	private readonly Dictionary<ChartType, PadData> PadDataByChartType = new();
	private readonly Dictionary<ChartType, StepGraph> StepGraphByChartType = new();
	private Dictionary<ChartType, List<List<GraphNode>>> RootNodesByChartType = new();
	private StepTypeFallbacks StepTypeFallbacks;

	private double PlaybackStartTime;
	private Stopwatch PlaybackStopwatch;

	private EditorSong ActiveSong;
	private EditorChart ActiveChart;

	private readonly List<EditorEvent> VisibleEvents = new();
	private readonly List<EditorMarkerEvent> VisibleMarkers = new();
	private readonly List<IChartRegion> VisibleRegions = new();
	private readonly SelectedRegion SelectedRegion = new();
	private readonly HashSet<EditorEvent> SelectedEvents = new();
	private readonly List<EditorEvent> CopiedEvents = new();
	private EditorEvent LastSelectedEvent;
	private bool TransformingNotes;
	private Receptor[] Receptors;
	private EventSpacingHelper SpacingHelper;
	private AutoPlayer AutoPlayer;

	private double WaveFormPPS = 1.0;

	// Movement controls.
	private EditorPosition Position;
	private bool UpdatingSongTimeDirectly;

	// Note controls
	private LaneEditState[] LaneEditStates;

	private KeyCommandManager KeyCommandManager;
	private bool Playing;
	private bool PlayingPreview;
	private bool MiniMapCapturingMouse;
	private bool StartPlayingWhenMiniMapDone;

	private uint MaxScreenHeight;

	// Fonts
	private ImFontPtr ImGuiFont;
	private SpriteFont Font;

	// Cursor
	private Cursor CurrentDesiredCursor = Cursors.Default;
	private Cursor PreviousDesiredCursor = Cursors.Default;

	// Debug
	private bool RenderChart = true;
	private double UpdateTimeTotal;
	private double UpdateTimeWaveForm;
	private double UpdateTimeMiniMap;
	private double UpdateTimeChartEvents;
	private double DrawTimeTotal;

	// Logger
	private readonly LinkedList<Logger.LogMessage> LogBuffer = new();
	private readonly object LogBufferLock = new();

	private bool ShowImGuiTestWindow = true;

	#region Initialization

	/// <summary>
	/// Constructor.
	/// </summary>
	public Editor()
	{
		// Create a logger first so we can log any startup messages.
		InitializeLogger();

		// Load Preferences synchronously so they can be used immediately.
		Preferences.Load(this);

		InitializeZoomManager();
		InitializeEditorPosition();
		InitializeMusicManager();
		InitializeGraphics();
		InitializeKeyCommandManager();
		InitializeContentManager();
		InitializeMouseVisibility();
		InitializeWindowResizing();
		InitializeVSync();
		InitializeSnapLevels();
		InitializeObservers();

		// Update the window title immediately.
		UpdateWindowTitle();
	}

	/// <summary>
	/// Override of MonoGame Game Initialize method.
	/// From MonoGame:
	///  Override this to initialize the game and load any needed non-graphical resources.
	///  Initializes attached GameComponent instances and calls LoadContent.
	/// </summary>
	protected override void Initialize()
	{
		InitializeWindowSize();
		InitializeFormCallbacks();
		InitializeFonts();
		InitializeGuiDpiScale();
		InitializeScreenHeight();
		InitializeWaveFormRenderer();
		InitializeMiniMap();
		InitializeUIHelpers();
		base.Initialize();
	}

	private void InitializeLogger()
	{
		var assembly = System.Reflection.Assembly.GetEntryAssembly();
		string logDirectory = null;

		var canLogToFile = true;
		var logToFileError = "";
		try
		{
			if (assembly != null)
			{
				var programPath = assembly.Location;
				var programDir = System.IO.Path.GetDirectoryName(programPath);
				logDirectory = Path.Combine(programDir, "logs");
			}

			if (logDirectory == null)
			{
				throw new Exception("Could not determine log directory.");
			}

			// Make a log directory if one doesn't exist.
			Directory.CreateDirectory(logDirectory);

			// Start the logger and write to disk as well as the internal buffer to display.
			var appName = GetAppName();
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
				Buffer = LogBuffer,
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
				Buffer = LogBuffer,
			});

			// Log an error that we were enable to log to a file.
			Logger.Error($"Unable to log to disk. {logToFileError}");
		}

		// Clean up old log files.
		try
		{
			if (logDirectory == null)
			{
				throw new Exception("Could not determine log directory.");
			}

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
					File.Delete(fi.FullName);
				}
			}
		}
		catch (Exception e)
		{
			Logger.Warn($"Unable to clean up old log files. {e}");
		}
	}

	private void InitializeZoomManager()
	{
		ZoomManager = new ZoomManager();
	}

	private void InitializeEditorPosition()
	{
		Position = new EditorPosition(OnPositionChanged);
	}

	private void InitializeMusicManager()
	{
		MusicManager = new MusicManager(SoundManager, Preferences.Instance.PreferencesOptions.AudioOffset);
		MusicManager.SetVolume(Preferences.Instance.PreferencesOptions.Volume);
	}

	private void InitializeGraphics()
	{
		Graphics = new GraphicsDeviceManager(this);
		Graphics.GraphicsProfile = GraphicsProfile.HiDef;
	}

	private void InitializeKeyCommandManager()
	{
		KeyCommandManager = new KeyCommandManager();
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Z }, OnUndo, true));
		KeyCommandManager.Register(
			new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Z }, OnRedo, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Y }, OnRedo, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.O }, OnOpen));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.S }, OnSaveAs));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.S }, OnSave));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.N }, OnNew));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.R }, OnReload));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.A }, OnSelectAll));
		KeyCommandManager.Register(
			new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftAlt, Keys.A }, OnSelectAllAlt));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.A },
			OnSelectAllShift));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Space }, OnTogglePlayback));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.P }, OnTogglePlayPreview));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Escape }, OnEscape));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Left }, OnDecreaseSnap, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Right }, OnIncreaseSnap, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Up }, OnMoveUp, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Down }, OnMoveDown, true));
		KeyCommandManager.Register(
			new KeyCommandManager.Command(new[] { Keys.PageUp }, OnMoveToPreviousMeasure, true, null, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.PageDown }, OnMoveToNextMeasure, true, null, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Home }, OnMoveToChartStart, false, null, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.End }, OnMoveToChartEnd, false, null, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Delete }, OnDelete, false, null, true));

		KeyCommandManager.Register(new KeyCommandManager.Command(
			new[] { Keys.LeftControl, Keys.LeftShift, Keys.LeftAlt, Keys.Left }, OnShiftSelectedNotesLeft, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Left },
			OnShiftSelectedNotesLeftAndWrap, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(
			new[] { Keys.LeftControl, Keys.LeftShift, Keys.LeftAlt, Keys.Right }, OnShiftSelectedNotesRight, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Right },
			OnShiftSelectedNotesRightAndWrap, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Up },
			OnShiftSelectedNotesEarlier, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Down },
			OnShiftSelectedNotesLater, true));

		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.C }, OnCopy, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.X }, OnCut, true));
		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.V }, OnPaste, true));

		var arrowInputKeys = new[] { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };
		var index = 0;
		foreach (var key in arrowInputKeys)
		{
			var capturedIndex = index;

			void Down()
			{
				OnLaneInputDown(capturedIndex);
			}

			void Up()
			{
				OnLaneInputUp(capturedIndex);
			}

			index++;
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { key }, Down, false, Up, true));
		}

		KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftShift }, OnShiftDown, false, OnShiftUp, true));
	}

	private void InitializeContentManager()
	{
		Content.RootDirectory = "Content";
	}

	private void InitializeMouseVisibility()
	{
		IsMouseVisible = true;
	}

	private void InitializeWindowResizing()
	{
		Window.AllowUserResizing = true;
		Window.ClientSizeChanged += OnResize;
	}

	private void InitializeVSync()
	{
		IsFixedTimeStep = false;
		Graphics.SynchronizeWithVerticalRetrace = true;
	}

	private void InitializeSnapLevels()
	{
		// Set up snap levels for all valid denominators.
		SnapLevels = new SnapData[ValidDenominators.Length + 1];
		SnapLevels[0] = new SnapData { Rows = 0 };
		for (var denominatorIndex = 0; denominatorIndex < ValidDenominators.Length; denominatorIndex++)
		{
			SnapLevels[denominatorIndex + 1] = new SnapData
			{
				Rows = MaxValidDenominator / ValidDenominators[denominatorIndex],
				Texture = ArrowGraphicManager.GetSnapIndicatorTexture(ValidDenominators[denominatorIndex]),
			};
		}
	}

	private void InitializeObservers()
	{
		Preferences.Instance.PreferencesOptions.AddObserver(this);
		Preferences.Instance.PreferencesExpressedChartConfig.AddObserver(this);
		ActionQueue.Instance.AddObserver(this);
	}

	private void InitializeWindowSize()
	{
		var p = Preferences.Instance;
		Graphics.PreferredBackBufferHeight = p.WindowHeight;
		Graphics.PreferredBackBufferWidth = p.WindowWidth;
		Graphics.IsFullScreen = p.WindowFullScreen;
		Graphics.ApplyChanges();
	}

	private void InitializeFormCallbacks()
	{
		var p = Preferences.Instance;
		var form = (Form)Control.FromHandle(Window.Handle);
		if (p.WindowMaximized)
		{
			form.WindowState = FormWindowState.Maximized;
		}

		form.FormClosing += ClosingForm;
		form.AllowDrop = true;
		form.DragEnter += DragEnter;
		form.DragDrop += DragDrop;
	}

	private void InitializeFonts()
	{
		var guiScale = GetDpiScale();
		ImGuiRenderer = new ImGuiRenderer(this);
		var assembly = System.Reflection.Assembly.GetEntryAssembly();
		if (assembly != null)
		{
			var programPath = assembly.Location;
			var programDir = System.IO.Path.GetDirectoryName(programPath);
			var mPlusFontPath = Path.Combine(programDir, @"Content\Mplus1Code-Medium.ttf");
			ImGuiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(mPlusFontPath, (int)(15 * guiScale), null,
				ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
			ImGuiRenderer.RebuildFontAtlas();
			ImGuiLayoutUtils.SetFont(ImGuiFont);
		}
		else
		{
			Logger.Error("Failed to initialize ImGui fonts. Could not find assembly.");
		}

		Font = Content.Load<SpriteFont>("mplus1code-medium");
	}

	private void InitializeGuiDpiScale()
	{
		var guiScale = GetDpiScale();
		if (!guiScale.DoubleEquals(1.0))
			ImGui.GetStyle().ScaleAllSizes((float)guiScale);
	}

	private void InitializeScreenHeight()
	{
		var p = Preferences.Instance;
		foreach (var adapter in GraphicsAdapter.Adapters)
			MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
		p.PreferencesReceptors.ClampViewportPositions();
		EditorEvent.SetScreenHeight(MaxScreenHeight);
	}

	private void InitializeWaveFormRenderer()
	{
		var p = Preferences.Instance;
		WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, MaxScreenHeight);
		WaveFormRenderer.SetColors(WaveFormColorDense, WaveFormColorSparse);
		WaveFormRenderer.SetXPerChannelScale(p.PreferencesWaveForm.WaveFormMaxXPercentagePerChannel);
		WaveFormRenderer.SetSoundMipMap(MusicManager.GetMusicMipMap());
		WaveFormRenderer.SetFocalPointY(GetFocalPointY());
		WaveformRenderTargets = new RenderTarget2D[2];
		for (var i = 0; i < 2; i++)
		{
			WaveformRenderTargets[i] = new RenderTarget2D(
				GraphicsDevice,
				WaveFormTextureWidth,
				(int)MaxScreenHeight,
				false,
				GraphicsDevice.PresentationParameters.BackBufferFormat,
				DepthFormat.Depth24);
		}
	}

	private void InitializeMiniMap()
	{
		var p = Preferences.Instance;
		MiniMap = new MiniMap(GraphicsDevice, new Rectangle(0, 0, 0, 0));
		MiniMap.SetSelectMode(p.PreferencesMiniMap.MiniMapSelectMode);
	}

	private void InitializeUIHelpers()
	{
		UISongProperties = new UISongProperties(this, GraphicsDevice, ImGuiRenderer);
		UIChartProperties = new UIChartProperties();
		UIChartList = new UIChartList(this);
		UIWaveFormPreferences = new UIWaveFormPreferences(MusicManager);
		UIScrollPreferences = new UIScrollPreferences();
		UISelectionPreferences = new UISelectionPreferences();
		UIMiniMapPreferences = new UIMiniMapPreferences();
		UIReceptorPreferences = new UIReceptorPreferences(this);
		UIOptions = new UIOptions();
		UIChartPosition = new UIChartPosition(this);
		UIExpressedChartConfig = new UIExpressedChartConfig(this);
		UIPerformedChartConfig = new UIPerformedChartConfig(this);
		UIAutogenConfigs = new UIAutogenConfigs(this);
		UIAutogenChart = new UIAutogenChart(this);
	}

	/// <summary>
	/// Override of MonoGame LoadContent method.
	/// From MonoGame:
	///  Override this to load graphical resources required by the game.
	/// </summary>
	protected override void LoadContent()
	{
		// Initialize the SpriteBatch.
		SpriteBatch = new SpriteBatch(GraphicsDevice);

		LoadTextureAtlas();
		LoadShaders();
		PerformPostContentLoadInitialization();

		base.LoadContent();
	}

	private void LoadTextureAtlas()
	{
		var atlasFileName = "atlas.json";
		var fullAtlasFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, atlasFileName);
		if (!File.Exists(fullAtlasFileName))
		{
			Logger.Error($"Could not find texture atlas file {atlasFileName}.");
			TextureAtlas = new StaticTextureAtlas(new Texture2D(GraphicsDevice, 0, 0));
			return;
		}

		// Initialize the TextureAtlas
		TextureAtlas = StaticTextureAtlas.Load(Content, "atlas", fullAtlasFileName)
		               ?? new StaticTextureAtlas(new Texture2D(GraphicsDevice, 0, 0));

		// Register the TextureAtlas's texture with ImGui so it can be drawn in UI.
		TextureAtlasImGuiTexture = ImGuiRenderer.BindTexture(TextureAtlas.GetTexture());
	}

	private void LoadShaders()
	{
		FxaaEffect = Content.Load<Effect>("fxaa");
		WaveformColorEffect = Content.Load<Effect>("waveform-color");
	}

	private void PerformPostContentLoadInitialization()
	{
		InitStepGraphDataAsync();

		// If we have a saved file to open, open it now.
		if (Preferences.Instance.PreferencesOptions.OpenLastOpenedFileOnLaunch
		    && Preferences.Instance.RecentFiles.Count > 0)
		{
			OpenRecentIndex = 0;
			OnOpenRecentFile();
		}
	}

	#endregion Initialization

	#region Shutdown

	public void ClosingForm(object sender, System.ComponentModel.CancelEventArgs e)
	{
		if (ActionQueue.Instance.HasUnsavedChanges())
		{
			e.Cancel = true;
			PostSaveFunction = OnExitNoSave;
			ShowSavePopup = true;
		}
	}

	protected override void EndRun()
	{
		CloseSong();
		Preferences.Save();
		Logger.Shutdown();

		ImGuiRenderer.UnbindTexture(TextureAtlasImGuiTexture);

		base.EndRun();
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

	#endregion Shutdown

	#region Static Helpers

	public static string GetAppName()
	{
		return System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
	}

	public static bool IsChartSupported(Chart chart)
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

	#endregion Static Helpers

	#region Texture Atlas

	public TextureAtlas GetTextureAtlas()
	{
		return TextureAtlas;
	}

	public IntPtr GetTextureAtlasImGuiTexture()
	{
		return TextureAtlasImGuiTexture;
	}

	#endregion Texture Atlas

	#region State Accessors

	public double GetSongTime()
	{
		return Position.SongTime;
	}

	public EditorPosition GetPosition()
	{
		return Position;
	}

	public EditorMouseState GetMouseState()
	{
		return EditorMouseState;
	}

	public EditorSong GetActiveSong()
	{
		return ActiveSong;
	}

	public EditorChart GetActiveChart()
	{
		return ActiveChart;
	}

	#endregion State Accessors

	#region Window Resizing

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

	#endregion Window Resizing

	#region Focal Point

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

	#endregion Focal Point

	#region Edit Locking

	public bool CanEdit()
	{
		if (ActiveSong != null && !ActiveSong.CanBeEdited())
			return false;
		if (ActiveChart != null && !ActiveChart.CanBeEdited())
			return false;
		if (ActionQueue.Instance.IsDoingOrUndoing())
			return false;
		return true;
	}

	public static bool CanSongBeEdited(EditorSong song)
	{
		if (ActionQueue.Instance.IsDoingOrUndoing())
			return false;
		return song?.CanBeEdited() ?? false;
	}

	public static bool CanChartBeEdited(EditorChart chart)
	{
		if (ActionQueue.Instance.IsDoingOrUndoing())
			return false;
		return chart?.CanBeEdited() ?? false;
	}

	private void OnCanEditChanged()
	{
		// Cancel input when content cannot be edited.
		// This prevents editor events which are actively being edited from being saved.
		if (!CanEdit())
			CancelLaneInput();
	}

	private bool EditEarlyOut()
	{
		if (!CanEdit())
		{
			if (IsSaving())
			{
				Logger.Warn("Edits cannot be made while saving.");
			}
			else
			{
				Logger.Warn("Edits cannot be made asynchronous edits are running.");
			}

			SystemSounds.Exclamation.Play();
			return true;
		}

		return false;
	}

	#endregion Edit Locking

	#region Update

	/// <summary>
	/// Override of MonoGame Game Update method.
	/// Called once per frame.
	/// From MonoGame:
	///  Called when the game should update.
	/// </summary>
	protected override void Update(GameTime gameTime)
	{
		// Time the Update method.
		var stopWatch = new Stopwatch();
		stopWatch.Start();

		var currentTime = gameTime.TotalGameTime.TotalSeconds;

		ActiveSong?.Update();
		SoundManager.Update();

		ProcessInput(gameTime, currentTime);

		ZoomManager.Update(currentTime);
		UpdateMusicAndPosition(currentTime);
		UpdateTimeChartEvents = Fumen.Utils.Timed(() =>
		{
			UpdateChartEvents();
			UpdateAutoPlay();
		});
		UpdateTimeMiniMap = Fumen.Utils.Timed(UpdateMiniMap);
		UpdateTimeWaveForm = Fumen.Utils.Timed(UpdateWaveFormRenderer);
		UpdateReceptors();

		// Update the Window title if the state of unsaved changes has changed.
		var hasUnsavedChanges = ActionQueue.Instance.HasUnsavedChanges();
		if (UnsavedChangesLastFrame != hasUnsavedChanges)
		{
			UnsavedChangesLastFrame = hasUnsavedChanges;
			UpdateWindowTitle();
		}

		base.Update(gameTime);

		// Record total update time.
		stopWatch.Stop();
		UpdateTimeTotal = stopWatch.Elapsed.TotalSeconds;
	}

	private void UpdateMusicAndPosition(double currentTime)
	{
		if (!Playing)
		{
			if (Position.IsInterpolatingChartPosition())
			{
				UpdatingSongTimeDirectly = true;
				Position.UpdateChartPositionInterpolation(currentTime);
				MusicManager.SetMusicTimeInSeconds(Position.SongTime);
				UpdatingSongTimeDirectly = false;
			}

			if (Position.IsInterpolatingSongTime())
			{
				UpdatingSongTimeDirectly = true;
				Position.UpdateSongTimeInterpolation(currentTime);
				MusicManager.SetMusicTimeInSeconds(Position.SongTime);
				UpdatingSongTimeDirectly = false;
			}
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
				if (CanMusicBeUsedToDetermineSongTime(out var musicSongTime))
				{
					if (Position.SongTime - musicSongTime > maxDeviation)
					{
						PlaybackStartTime -= 0.5 * maxDeviation;
						Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
					}
					else if (musicSongTime - Position.SongTime > maxDeviation)
					{
						PlaybackStartTime += 0.5 * maxDeviation;
						Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
					}
				}
			}

			Position.SetDesiredPositionToCurrent();

			UpdatingSongTimeDirectly = false;
		}
		else
		{
			MusicManager.Update(Position.SongTime);
		}
	}

	private void UpdateReceptors()
	{
		if (Receptors != null)
		{
			foreach (var receptor in Receptors)
			{
				receptor.Update(Playing, Position.ChartPosition);
			}
		}
	}

	private void UpdateWaveFormRenderer()
	{
		var pWave = Preferences.Instance.PreferencesWaveForm;

		// Performance optimization. Do not update the texture if we won't render it.
		if (!pWave.ShowWaveForm || !pWave.EnableWaveForm)
			return;

		// Update the WaveFormRenderer.
		WaveFormRenderer.SetFocalPointY(GetFocalPointY());
		WaveFormRenderer.SetXPerChannelScale(pWave.WaveFormMaxXPercentagePerChannel);
		WaveFormRenderer.SetDenseScale(pWave.DenseScale);
		WaveFormRenderer.SetScaleXWhenZooming(pWave.WaveFormScaleXWhenZooming);
		WaveFormRenderer.Update(Position.SongTime, ZoomManager.GetSpacingZoom(), WaveFormPPS);
	}

	#endregion Update

	#region Input Processing

	/// <summary>
	/// Process keyboard and mouse input.
	/// </summary>
	/// <param name="gameTime">Current GameTime. Needed for ImGui.</param>
	/// <param name="currentTime">Current time in seconds.</param>
	private void ProcessInput(GameTime gameTime, double currentTime)
	{
		var inFocus = IsApplicationFocused();

		CurrentDesiredCursor = Cursors.Default;
		CanShowRightClickPopupThisFrame = false;

		SelectedRegion.UpdateTime(currentTime);

		// TODO: Remove remaining input processing from ImGuiRenderer.
		ImGuiRenderer.UpdateInput(gameTime);

		// ImGui needs to be told when a new frame begins after processing input.
		// This application also relies on the new frame being begun in input processing
		// as some inputs need to check bounds with ImGui elements that require pushing
		// font state.
		ImGuiRenderer.BeforeLayout();

		// Process Mouse Input.
		var state = Mouse.GetState();
		var (mouseChartTime, mouseChartPosition) = FindChartTimeAndRowForScreenY(state.Y);
		EditorMouseState.Update(state, mouseChartTime, mouseChartPosition, inFocus);

		// Do not do any further input processing if the application does not have focus.
		if (!inFocus)
			return;

		var imGuiWantMouse = ImGui.GetIO().WantCaptureMouse;
		var imGuiWantKeyboard = ImGui.GetIO().WantCaptureKeyboard;

		// Process Keyboard Input.
		if (imGuiWantKeyboard)
			KeyCommandManager.CancelAllCommands();
		else
			KeyCommandManager.Update(currentTime);

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
			ZoomManager.GetSizeZoom(),
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
			ProcessInputForSelectedRegion(currentTime);

		// Process right click popup eligibility.
		CanShowRightClickPopupThisFrame = !MiniMapCapturingMouse && !MovingFocalPoint && !MovingFocalPoint;

		// Setting the cursor every frame prevents it from changing to support normal application
		// behavior like indicating resizeability at the edges of the window. But not setting every frame
		// causes it to go back to the Default. Set it every frame only if it setting it to something
		// other than the Default.
		if (CurrentDesiredCursor != PreviousDesiredCursor || CurrentDesiredCursor != Cursors.Default)
		{
			Cursor.Current = CurrentDesiredCursor;
		}

		PreviousDesiredCursor = CurrentDesiredCursor;

		// Process input for scrolling and zooming.
		ProcessInputForScrollingAndZooming(currentTime);
	}

	/// <summary>
	/// Processes input for moving the focal point with the mouse.
	/// </summary>
	/// <remarks>Helper for ProcessInput.</remarks>
	private void ProcessInputForMovingFocalPoint(bool inReceptorArea)
	{
		// Begin moving focal point.
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame()
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
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).Up() && MovingFocalPoint)
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
	private void ProcessInputForSelectedRegion(double currentTime)
	{
		var sizeZoom = ZoomManager.GetSizeZoom();

		// Starting a selection.
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame())
		{
			var y = EditorMouseState.Y();
			var (chartTime, chartPosition) = FindChartTimeAndRowForScreenY(y);
			var xInChartSpace = (EditorMouseState.X() - GetFocalPointX()) / sizeZoom;
			SelectedRegion.Start(
				xInChartSpace,
				y,
				chartTime,
				chartPosition,
				sizeZoom,
				GetFocalPointX(),
				currentTime);
		}

		// Dragging a selection.
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).Down() && SelectedRegion.IsActive())
		{
			var xInChartSpace = (EditorMouseState.X() - GetFocalPointX()) / sizeZoom;
			SelectedRegion.UpdatePerFrameValues(xInChartSpace, EditorMouseState.Y(), sizeZoom, GetFocalPointX());
		}

		// Releasing a selection.
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).Up() && SelectedRegion.IsActive())
		{
			FinishSelectedRegion();
		}
	}

	/// <summary>
	/// Processes input for scrolling and zooming.
	/// </summary>
	/// <remarks>Helper for ProcessInput.</remarks>
	private void ProcessInputForScrollingAndZooming(double currentTime)
	{
		var pScroll = Preferences.Instance.PreferencesScroll;
		var scrollDelta = (float)EditorMouseState.ScrollDeltaSinceLastFrame() / EditorMouseState.GetDefaultScrollDetentValue();

		// Process input for zoom controls.
		var zoomManagerCaptureInput = ZoomManager.ProcessInput(currentTime, KeyCommandManager, scrollDelta);

		// If the input was used for controlling zoom levels do not continue to process it.
		if (zoomManagerCaptureInput || scrollDelta.FloatEquals(0.0f))
			return;

		// Adjust position.
		var timeDelta = pScroll.ScrollWheelTime / ZoomManager.GetSpacingZoom() * (scrollDelta > 0.0f ? -1.0f : 1.0f);
		var rowDelta = pScroll.ScrollWheelRows / ZoomManager.GetSpacingZoom() * (scrollDelta > 0.0f ? -1.0f : 1.0f);
		if (Playing)
		{
			PlaybackStartTime += timeDelta;
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
				if (pScroll.SpacingMode == SpacingMode.ConstantTime)
					Position.BeginSongTimeInterpolation(currentTime, timeDelta);
				else
					Position.BeginChartPositionInterpolation(currentTime, rowDelta);
			}
			else
			{
				if (scrollDelta > 0.0f)
					OnMoveUp();
				else
					OnMoveDown();
			}
		}
	}

	#endregion Input Processing

	#region Playing Music

	/// <summary>
	/// Returns whether the music can be used to determine the song time.
	/// The music can normally be used to determine the song time, but it cannot be used if
	/// it is before the start or after the end.
	/// </summary>
	/// <param name="musicSongTime">The song time as determined from querying the music.</param>
	/// <returns>Whether the music can be used to determine the song time.</returns>
	private bool CanMusicBeUsedToDetermineSongTime(out double musicSongTime)
	{
		musicSongTime = 0.0;
		if (!MusicManager.IsMusicLoaded())
			return false;
		var musicLen = MusicManager.GetMusicLengthInSeconds();
		var musicWithinRangeToUseForSongTime = !MusicManager.IsMusicAtMinOrMaxPosition(out musicSongTime);
		// To use the music to determine the song time the song time and the music must be within the bounds
		// of the music.
		return Position.SongTime >= 0.0 && Position.SongTime < musicLen && musicWithinRangeToUseForSongTime;
	}

	private void StartPlayback()
	{
		if (Playing)
			return;

		StopPreview();

		if (!CanMusicBeUsedToDetermineSongTime(out var musicSongTime))
		{
			PlaybackStartTime = Position.SongTime;
		}
		else
		{
			PlaybackStartTime = musicSongTime;
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
		AutoPlayer?.Stop();

		Playing = false;
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

	private void OnMusicChanged()
	{
		StopPreview();
		MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), GetSongTime, false,
			Preferences.Instance.PreferencesWaveForm.EnableWaveForm);
	}

	private void OnMusicPreviewChanged()
	{
		StopPreview();
		MusicManager.LoadMusicPreviewAsync(GetFullPathToMusicPreviewFile());
	}

	private void OnMusicOffsetChanged()
	{
		// Re-set the position to recompute the chart and song times.
		Position.ChartPosition = Position.ChartPosition;
	}

	private void OnSyncOffsetChanged()
	{
		// Re-set the position to recompute the chart and song times.
		Position.ChartPosition = Position.ChartPosition;
	}

	private void OnAudioOffsetChanged()
	{
		var playing = Playing;
		if (playing)
			StopPlayback();
		MusicManager.SetMusicOffset(Preferences.Instance.PreferencesOptions.AudioOffset);
		MusicManager.SetMusicTimeInSeconds(Position.SongTime);
		if (playing)
			StartPlayback();
	}

	private void OnVolumeChanged()
	{
		MusicManager.SetVolume(Preferences.Instance.PreferencesOptions.Volume);
	}

	#endregion Playing Music

	#region Zoom

	public void SetSpacingZoom(double zoom)
	{
		ZoomManager.SetZoom(zoom);
	}

	public double GetSpacingZoom()
	{
		return ZoomManager.GetSpacingZoom();
	}

	#endregion Zoom

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

		DrawGui();

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
		if (!p.ShowWaveForm || !p.EnableWaveForm)
			return;

		// Draw the waveform to the first render target.
		GraphicsDevice.SetRenderTarget(WaveformRenderTargets[0]);
		GraphicsDevice.Clear(Color.Transparent);
		SpriteBatch.Begin();
		WaveFormRenderer.Draw(SpriteBatch);
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
		SpriteBatch.Draw(WaveformRenderTargets[0],
			new Rectangle(0, 0, WaveformRenderTargets[0].Width, WaveformRenderTargets[0].Height), Color.White);
		SpriteBatch.End();
	}

	private void DrawBackground()
	{
		// If there is no background image, just clear with black and return.
		if (!(ActiveSong?.GetBackground()?.GetTexture()?.IsBound() ?? false))
		{
			GraphicsDevice.Clear(Color.Black);
			return;
		}

		// If we have a background texture, clear with the average color of the texture.
		var color = ActiveSong.GetBackground().GetTexture().GetTextureColor();
		var (r, g, b, a) = ToFloats(color);
		GraphicsDevice.Clear(new Color(r, g, b, a));

		// Draw the background texture.
		SpriteBatch.Begin();
		ActiveSong.GetBackground().GetTexture()
			.DrawTexture(SpriteBatch, 0, 0, (uint)GetViewportWidth(), (uint)GetViewportHeight());
		SpriteBatch.End();
	}

	private void DrawReceptors()
	{
		if (ActiveChart == null || ArrowGraphicManager == null || Receptors == null)
			return;

		var sizeZoom = ZoomManager.GetSizeZoom();
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
		var zoom = ZoomManager.GetSizeZoom();
		var receptorLeftEdge = GetFocalPointX() - ActiveChart.NumInputs * 0.5 * receptorTextureWidth * zoom;

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

		var sizeZoom = ZoomManager.GetSizeZoom();
		foreach (var receptor in Receptors)
			receptor.DrawForegroundEffects(GetFocalPoint(), sizeZoom, TextureAtlas, SpriteBatch);
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
			SpriteBatch.Draw(WaveformRenderTargets[1],
				new Rectangle(x, 0, WaveformRenderTargets[1].Width, WaveformRenderTargets[1].Height), Color.White);
			SpriteBatch.End();
		}
		else
		{
			// Draw the recolored waveform to the back buffer.
			SpriteBatch.Begin();
			SpriteBatch.Draw(WaveformRenderTargets[1],
				new Rectangle(x, 0, WaveformRenderTargets[1].Width, WaveformRenderTargets[1].Height), Color.White);
			SpriteBatch.End();
		}
	}

	private void DrawMeasureMarkers()
	{
		foreach (var visibleMarker in VisibleMarkers)
		{
			visibleMarker.Draw(TextureAtlas, SpriteBatch, Font);
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
				    && visibleEvent.GetEndChartTime() < Position.ChartTime
				    && (visibleEvent is EditorTapNoteEvent || visibleEvent is EditorHoldNoteEvent))
					continue;

				// Cut off hold end notes which intersect the receptors.
				if (visibleEvent is EditorHoldNoteEvent hold)
				{
					if (Playing && hold.GetEndChartTime() > Position.ChartTime
					            && hold.GetChartTime() < Position.ChartTime)
					{
						hold.SetNextDrawActive(true, GetFocalPointY());
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
	/// Sets VisibleEvents, VisibleMarkers, and VisibleRegions to store the currently visible
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
		SelectedRegion.ClearPerFrameData();

		if (ActiveChart == null || ActiveChart.EditorEvents == null || ArrowGraphicManager == null)
			return;

		// Get an EventSpacingHelper to perform y calculations.
		SpacingHelper = EventSpacingHelper.GetSpacingHelper(ActiveChart);

		var noteEvents = new List<EditorEvent>();

		var screenHeight = GetViewportHeight();
		var focalPointX = GetFocalPointX();
		var focalPointY = GetFocalPointY();
		var numArrows = ActiveChart.NumInputs;

		var spacingZoom = ZoomManager.GetSpacingZoom();
		var sizeZoom = ZoomManager.GetSizeZoom();

		// Determine graphic dimensions based on the zoom level.
		var (arrowW, arrowH) = GetArrowDimensions();
		var (holdCapTexture, _) = ArrowGraphicManager.GetHoldEndTexture(0, 0, false, false);
		var (_, holdCapTextureHeight) = TextureAtlas.GetDimensions(holdCapTexture);
		var holdCapHeight = holdCapTextureHeight * sizeZoom;
		if (ArrowGraphicManager.AreHoldCapsCentered())
			holdCapHeight *= 0.5;

		// Determine the starting x and y position in screen space.
		// Y extended slightly above the top of the screen so that we start drawing arrows
		// before their midpoints.
		var startPosX = focalPointX - numArrows * arrowW * 0.5;
		var startPosY = 0.0 - Math.Max(holdCapHeight, arrowH * 0.5);

		var noteAlpha = (float)Interpolation.Lerp(1.0, 0.0, NoteScaleToStartFading, NoteMinScale, sizeZoom);

		// Set up the MiscEventWidgetLayoutManager.
		var miscEventAlpha = (float)Interpolation.Lerp(1.0, 0.0, MiscEventScaleToStartFading, MiscEventMinScale, sizeZoom);
		BeginMiscEventWidgetLayoutManagerFrame();

		// TODO: Fix Negative Scrolls resulting in cutting off notes prematurely.
		// If a chart has negative scrolls then we technically need to render notes which come before
		// the chart position at the top of the screen.
		// More likely the most visible problem will be at the bottom of the screen where if we
		// were to detect the first note which falls below the bottom it would prevent us from
		// finding the next set of notes which might need to be rendered because they appear 
		// above.

		// Get the current time and position.
		var time = Position.ChartTime;
		var chartPosition = Position.ChartPosition;

		// Find the interpolated scroll rate to use as a multiplier.
		var interpolatedScrollRate = GetCurrentInterpolatedScrollRate();

		// Now, scroll up to the top of the screen so we can start processing events going downwards.
		// We know what time / pos we are drawing at the receptors, but not the rate to get to that time from the top
		// of the screen.
		// We need to find the greatest preceding rate event, and continue until it is beyond the start of the screen.
		// Then we need to find the greatest preceding notes by scanning upwards.
		// Once we find that note, we start iterating downwards while also keeping track of the rate events along the way.

		var rateEnumerator = ActiveChart.RateAlteringEvents.FindBest(Position);
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
			previousRateEventY = SpacingHelper.GetYPreceding(previousRateEventTime, previousRateEventRow, previousRateEventY,
				rateEvent!.GetChartTime(), rateEvent.GetRow());
			previousRateEventRow = rateEvent.GetRow();
			previousRateEventTime = rateEvent.GetChartTime();
		}

		// Now we know the position of first rate altering event to use.
		// We can now determine the chart time and position at the top of the screen.
		var (chartTimeAtTopOfScreen, chartPositionAtTopOfScreen) =
			SpacingHelper.GetChartTimeAndRow(startPosY, previousRateEventY, rateEvent!.GetChartTime(), rateEvent.GetRow());

		var beatMarkerRow = (int)chartPositionAtTopOfScreen;
		var beatMarkerLastRecordedRow = -1;
		var numRateAlteringEventsProcessed = 1;

		// Now that we know the position at the start of the screen we can find the first event to start rendering.
		var enumerator = ActiveChart.EditorEvents.FindBestByPosition(chartPositionAtTopOfScreen);
		if (enumerator == null)
			return;

		// Scan backwards until we have checked every lane for a long note which may
		// be extending through the given start row. We cannot add the end events yet because
		// we do not know at what position they will end until we scan down.
		var holdsNeedingToBeCompleted = new EditorHoldNoteEvent[ActiveChart.NumInputs];
		var holdNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
		foreach (var hn in holdNotes)
		{
			// This is technically incorrect.
			// We are using the rate altering event active at the screen, but there could be more
			// rate altering events between the top of the screen and the start of the hold.
			hn.SetDimensions(
				startPosX + hn.GetLane() * arrowW,
				SpacingHelper.GetY(hn, previousRateEventY) - arrowH * 0.5,
				arrowW,
				0.0, // we do not know the height yet.
				sizeZoom);
			noteEvents.Add(hn);

			holdsNeedingToBeCompleted[hn.GetLane()] = hn;
		}

		var hasNextRateEvent = rateEnumerator.MoveNext();
		var nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

		var regionsNeedingToBeAdded = new List<IChartRegion>();
		var addedRegions = new HashSet<IChartRegion>();

		// Start any regions including the selected region.
		// This call will also check for completing regions within the current rate altering event.
		StartRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent, nextRateEvent, startPosX,
			numArrows * arrowW, chartTimeAtTopOfScreen, chartPositionAtTopOfScreen);
		// Check for completing holds within the current rate altering event.
		CheckForCompletingHolds(holdsNeedingToBeCompleted, previousRateEventY, nextRateEvent);

		// Now we can scan forward
		var reachedEndOfScreen = false;
		while (enumerator.MoveNext())
		{
			var e = enumerator.Current;

			// Check to see if we have crossed into a new rate altering event section
			if (nextRateEvent != null && e == nextRateEvent)
			{
				// Add a misc widget for this rate event.
				var rateEventY = SpacingHelper.GetY(e, previousRateEventY);
				nextRateEvent.Alpha = miscEventAlpha;
				MiscEventWidgetLayoutManager.PositionEvent(nextRateEvent, rateEventY);
				noteEvents.Add(nextRateEvent);

				// Add a region for this event if appropriate.
				if (nextRateEvent is IChartRegion region)
					AddRegion(region, ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent, startPosX,
						numArrows * arrowW);

				// Update beat markers for the section for the previous rate event.
				UpdateBeatMarkers(rateEvent, ref beatMarkerRow, ref beatMarkerLastRecordedRow, nextRateEvent, startPosX, sizeZoom,
					previousRateEventY);

				// Update rate parameters.
				rateEvent = nextRateEvent;
				SpacingHelper.UpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
				previousRateEventY = rateEventY;

				// Advance next rate altering event.
				hasNextRateEvent = rateEnumerator.MoveNext();
				nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

				// Update any regions needing to be updated based on the new rate altering event.
				UpdateRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent, nextRateEvent);
				// Check for completing any holds needing to be completed within the new rate altering event.
				CheckForCompletingHolds(holdsNeedingToBeCompleted, previousRateEventY, nextRateEvent);

				numRateAlteringEventsProcessed++;
				continue;
			}

			// Determine y position.
			var y = SpacingHelper.GetY(e, previousRateEventY);
			var arrowY = y - arrowH * 0.5;

			// If we have advanced beyond the end of the screen we can finish.
			// An exception to this rule is if the current scroll rate is negative. We do not
			// want to end processing on a negative region, particularly for regions which end
			// beyond the end of the screen.
			if (arrowY > screenHeight && !SpacingHelper.IsScrollRateNegative())
			{
				reachedEndOfScreen = true;
				break;
			}

			// Record note.
			if (e is EditorTapNoteEvent || e is EditorHoldNoteEvent || e is EditorMineNoteEvent)
			{
				noteEvents.Add(e);
				e.SetDimensions(startPosX + e.GetLane() * arrowW, arrowY, arrowW, arrowH, sizeZoom);
				e.Alpha = noteAlpha;

				if (e is EditorHoldNoteEvent hn)
				{
					// Record that there is in an in-progress hold that will need to be ended.
					if (!CheckForCompletingHold(hn, previousRateEventY, nextRateEvent))
						holdsNeedingToBeCompleted[e.GetLane()] = hn;
				}
			}
			else
			{
				if (e!.IsMiscEvent())
				{
					e.Alpha = miscEventAlpha;
					MiscEventWidgetLayoutManager.PositionEvent(e, y);
				}

				noteEvents.Add(e);

				// Add a region for this event if appropriate.
				if (e is IChartRegion region)
					AddRegion(region, ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent, startPosX,
						numArrows * arrowW);
			}

			// If we have collected the maximum number of events per frame, stop processing.
			if (noteEvents.Count > MaxEventsToDraw)
				break;
		}

		// Now we need to wrap up any holds which are still not yet complete.
		// We do not need to scan forward for more rate events.
		CheckForCompletingHolds(holdsNeedingToBeCompleted, previousRateEventY, null);

		// We also need to update beat markers beyond the final note.
		UpdateBeatMarkers(rateEvent, ref beatMarkerRow, ref beatMarkerLastRecordedRow, nextRateEvent, startPosX, sizeZoom,
			previousRateEventY);

		// If the user is selecting a region and is zoomed out so far that we processed the maximum number of notes
		// per frame without finding both ends of the selected region, then keep iterating through rate altering events
		// to try and complete the selected region.
		if (!reachedEndOfScreen && SelectedRegion.IsActive())
		{
			while (nextRateEvent != null
			       && (!SelectedRegion.HasStartYBeenUpdatedThisFrame() || !SelectedRegion.HaveCurrentValuesBeenUpdatedThisFrame())
			       && numRateAlteringEventsProcessed < MaxRateAlteringEventsToProcessPerFrame)
			{
				var rateEventY = SpacingHelper.GetY(nextRateEvent, previousRateEventY);
				rateEvent = nextRateEvent;
				SpacingHelper.UpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
				previousRateEventY = rateEventY;

				// Advance to the next rate altering event.
				hasNextRateEvent = rateEnumerator.MoveNext();
				nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

				// Update any regions needing to be updated based on the new rate altering event.
				UpdateRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent, nextRateEvent);
				numRateAlteringEventsProcessed++;
			}
		}

		// Normal case of needing to complete regions which end beyond the bounds of the screen.
		EndRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent);

		// Store the notes and holds so we can render them.
		VisibleEvents.AddRange(noteEvents);
	}

	private (double, double) GetArrowDimensions(bool scaled = true)
	{
		var (arrowTexture, _) = ArrowGraphicManager.GetArrowTexture(0, 0, false);
		(double arrowW, double arrowH) = TextureAtlas.GetDimensions(arrowTexture);
		if (scaled)
		{
			var sizeZoom = ZoomManager.GetSizeZoom();
			arrowW *= sizeZoom;
			arrowH *= sizeZoom;
		}

		return (arrowW, arrowH);
	}

	private double GetHoldCapHeight()
	{
		var (holdCapTexture, _) = ArrowGraphicManager.GetHoldEndTexture(0, 0, false, false);
		var (_, holdCapTextureHeight) = TextureAtlas.GetDimensions(holdCapTexture);
		var holdCapHeight = holdCapTextureHeight * ZoomManager.GetSizeZoom();
		if (ArrowGraphicManager.AreHoldCapsCentered())
			holdCapHeight *= 0.5;
		return holdCapHeight;
	}

	private void BeginMiscEventWidgetLayoutManagerFrame()
	{
		const int widgetStartPadding = 10;
		const int widgetMeasureNumberFudge = 10;

		var (arrowW, _) = GetArrowDimensions();

		var focalPointX = GetFocalPointX();
		var startPosX = focalPointX - ActiveChart.NumInputs * arrowW * 0.5;
		var endXPos = focalPointX + ActiveChart.NumInputs * arrowW * 0.5;

		var lMiscWidgetPos = startPosX
		                     - widgetStartPadding
		                     + EditorMarkerEvent.GetNumberRelativeAnchorPos(ZoomManager.GetSizeZoom())
		                     - EditorMarkerEvent.GetNumberAlpha(ZoomManager.GetSizeZoom()) * widgetMeasureNumberFudge;
		var rMiscWidgetPos = endXPos + widgetStartPadding;
		MiscEventWidgetLayoutManager.BeginFrame(lMiscWidgetPos, rMiscWidgetPos);
	}

	/// <summary>
	/// Sets the pixels per second to use on the WaveFormRenderer.
	/// </summary>
	/// <param name="rateEvent">Current rate altering event.</param>
	/// <param name="interpolatedScrollRate">Current interpolated scroll rate.</param>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void SetWaveFormPps(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate)
	{
		var pScroll = Preferences.Instance.PreferencesScroll;
		switch (pScroll.SpacingMode)
		{
			case SpacingMode.ConstantTime:
				WaveFormPPS = pScroll.TimeBasedPixelsPerSecond;
				break;
			case SpacingMode.ConstantRow:
				WaveFormPPS = pScroll.RowBasedPixelsPerRow * rateEvent.GetRowsPerSecond();
				if (pScroll.RowBasedWaveFormScrollMode == WaveFormScrollMode.MostCommonTempo)
					WaveFormPPS *= ActiveChart.GetMostCommonTempo() / rateEvent.GetTempo();
				break;
			case SpacingMode.Variable:
				var tempo = ActiveChart.GetMostCommonTempo();
				if (pScroll.RowBasedWaveFormScrollMode != WaveFormScrollMode.MostCommonTempo)
					tempo = rateEvent.GetTempo();
				var useRate = pScroll.RowBasedWaveFormScrollMode ==
				              WaveFormScrollMode.CurrentTempoAndRate;
				WaveFormPPS = pScroll.VariablePixelsPerSecondAtDefaultBPM
				              * (tempo / PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM);
				if (useRate)
				{
					var rate = rateEvent.GetScrollRate() * interpolatedScrollRate;
					if (rate <= 0.0)
						rate = 1.0;
					WaveFormPPS *= rate;
				}

				break;
		}
	}

	/// <summary>
	/// Gets the current interpolated scroll rate to use for the active Chart.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	/// <returns>Interpolated scroll rate.</returns>
	private double GetCurrentInterpolatedScrollRate()
	{
		// Find the interpolated scroll rate to use as a multiplier.
		// The interpolated scroll rate to use is the value at the current exact time.
		var interpolatedScrollRate = 1.0;
		if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.Variable)
		{
			var ratePosEventForChecking = (EditorInterpolatedRateAlteringEvent)EditorEvent.CreateEvent(
				EventConfig.CreateScrollRateInterpolationConfig(ActiveChart, (int)Position.ChartPosition, Position.ChartTime, 0.0,
					0));

			var interpolatedScrollRateEnumerator =
				ActiveChart.InterpolatedScrollRateEvents.FindGreatestPreceding(ratePosEventForChecking);
			if (interpolatedScrollRateEnumerator != null)
			{
				interpolatedScrollRateEnumerator.MoveNext();
				var interpolatedRateEvent = interpolatedScrollRateEnumerator.Current;
				if (interpolatedRateEvent!.InterpolatesByTime())
					interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromTime(Position.ChartTime);
				else
					interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromRow(Position.ChartPosition);
			}
			else
			{
				interpolatedScrollRateEnumerator =
					ActiveChart.InterpolatedScrollRateEvents.FindLeastFollowing(ratePosEventForChecking, true);
				if (interpolatedScrollRateEnumerator != null)
				{
					interpolatedScrollRateEnumerator.MoveNext();
					var interpolatedRateEvent = interpolatedScrollRateEnumerator.Current;
					if (interpolatedRateEvent!.InterpolatesByTime())
						interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromTime(Position.ChartTime);
					else
						interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromRow(Position.ChartPosition);
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
	private List<EditorHoldNoteEvent> ScanBackwardsForHolds(RedBlackTree<EditorEvent>.Enumerator enumerator,
		double chartPosition)
	{
		// Get all the holds overlapping the given position.
		var holdsPerLane = ActiveChart.GetHoldsOverlapping(chartPosition, enumerator);
		var holds = new List<EditorHoldNoteEvent>();
		foreach (var hold in holdsPerLane)
		{
			if (hold != null)
				holds.Add(hold);
		}

		// Add holds being edited.
		foreach (var editState in LaneEditStates)
		{
			if (!editState.IsActive())
				continue;
			if (!(editState.GetEventBeingEdited() is EditorHoldNoteEvent hn))
				continue;
			if (hn.GetRow() < chartPosition && hn.GetRow() + hn.GetLength() > chartPosition)
				holds.Add(hn);
		}

		return holds;
	}

	private void AddRegion(
		IChartRegion region,
		ref List<IChartRegion> regionsNeedingToBeAdded,
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent,
		double x,
		double w)
	{
		if (region == null || !region.IsVisible(Preferences.Instance.PreferencesScroll.SpacingMode))
			return;
		if (regionsNeedingToBeAdded.Contains(region) || addedRegions.Contains(region))
			return;
		region.SetRegionX(x);
		region.SetRegionY(SpacingHelper.GetRegionY(region, previousRateEventY));
		region.SetRegionW(w);
		regionsNeedingToBeAdded.Add(region);

		// This region may also complete during this rate altering event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent);
	}

	private void CheckForCompletingRegions(
		ref List<IChartRegion> regionsNeedingToBeAdded,
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent)
	{
		var remainingRegionsNeededToBeAdded = new List<IChartRegion>();
		foreach (var region in regionsNeedingToBeAdded)
		{
			var regionEnd = region.GetRegionPosition() + region.GetRegionDuration();
			if (nextRateEvent == null ||
			    (region.AreRegionUnitsTime() && nextRateEvent.GetChartTime() > regionEnd)
			    || (!region.AreRegionUnitsTime() && nextRateEvent.GetRow() > regionEnd))
			{
				region.SetRegionH(SpacingHelper.GetRegionH(region, previousRateEventY));
				VisibleRegions.Add(region);
				addedRegions.Add(region);
				continue;
			}

			remainingRegionsNeededToBeAdded.Add(region);
		}

		regionsNeedingToBeAdded = remainingRegionsNeededToBeAdded;
	}

	private void CheckForUpdatingSelectedRegionStartY(double previousRateEventY, EditorRateAlteringEvent rateEvent,
		EditorRateAlteringEvent nextRateEvent)
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
					SelectedRegion.UpdatePerFrameDerivedStartY(SpacingHelper.GetY(SelectedRegion.GetStartChartTime(),
						SelectedRegion.GetStartChartPosition(), previousRateEventY, rateEvent.GetChartTime(),
						rateEvent.GetRow()));
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
					SelectedRegion.UpdatePerFrameDerivedStartY(SpacingHelper.GetY(SelectedRegion.GetStartChartTime(),
						SelectedRegion.GetStartChartPosition(), previousRateEventY, rateEvent.GetChartTime(),
						rateEvent.GetRow()));
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
		    || SelectedRegion.GetCurrentY() < SpacingHelper.GetY(nextRateEvent, previousRateEventY))
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
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent rateEvent,
		EditorRateAlteringEvent nextRateEvent,
		double chartRegionX,
		double chartRegionW,
		double chartTimeAtTopOfScreen,
		double chartPositionAtTopOfScreen)
	{
		// Check for adding regions which extend through the top of the screen.
		var regions = ActiveChart.GetRegionsOverlapping(chartPositionAtTopOfScreen, chartTimeAtTopOfScreen);
		foreach (var region in regions)
			AddRegion(region, ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent, chartRegionX,
				chartRegionW);

		// Check to see if any regions needing to be added will complete before the next rate altering event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent);

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
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent rateEvent,
		EditorRateAlteringEvent nextRateEvent)
	{
		// Check to see if any regions needing to be added will complete before the next rate altering event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent);

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
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent rateEvent)
	{
		// We do not need to scan forward for more rate mods so we can use null for the next rate event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, null);

		// Check for updating the SelectedRegion.
		CheckForUpdatingSelectedRegionStartY(previousRateEventY, rateEvent, null);
		CheckForUpdatingSelectedRegionCurrentValues(previousRateEventY, rateEvent, null);
	}

	/// <summary>
	/// Handles completing any pending holds when the current rate altering event changes
	/// while processing events in the main tick loop and holds end within the new rate
	/// altering event range.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void CheckForCompletingHolds(
		EditorHoldNoteEvent[] holds,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent)
	{
		for (var i = 0; i < holds.Length; i++)
		{
			if (holds[i] == null)
				continue;
			if (CheckForCompletingHold(holds[i], previousRateEventY, nextRateEvent))
				holds[i] = null;
		}
	}

	/// <summary>
	/// Handles completing a pending hold when the current rate altering event changes
	/// while processing events in the main tick loop and the hold ends within the new rate
	/// altering event range.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private bool CheckForCompletingHold(
		EditorHoldNoteEvent hold,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent)
	{
		var holdEndRow = hold.GetRow() + hold.GetLength();
		if (nextRateEvent == null || holdEndRow <= nextRateEvent.GetRow())
		{
			var holdEndY = SpacingHelper.GetYForRow(holdEndRow, previousRateEventY) + GetHoldCapHeight();
			hold.H = holdEndY - hold.Y;
			return true;
		}

		return false;
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

		var ts = currentRateEvent.GetTimeSignature();

		// Based on the current rate altering event, determine the beat spacing and snap the current row to a beat.
		var beatsPerMeasure = ts.Signature.Numerator;
		var rowsPerBeat = MaxValidDenominator * NumBeatsPerMeasure * beatsPerMeasure
		                  / ts.Signature.Denominator / beatsPerMeasure;

		// Determine which integer measure and beat we are on. Clamped due to warps.
		var rowRelativeToTimeSignatureStart = Math.Max(0, currentRow - ts.IntegerPosition);
		// We need to snap the row forward since we are starting with a row that might not be on a beat boundary.
		var beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
		currentRow = ts.IntegerPosition + beatRelativeToTimeSignatureStart * rowsPerBeat;

		var markerWidth = ActiveChart.NumInputs * MarkerTextureWidth * sizeZoom;

		while (true)
		{
			// When changing time signatures we don't want to render the same row twice,
			// so advance if we have already processed this row.
			// Also check to ensure that the current row is within range for the current rate event.
			// In some edge cases it may not be. For example, when we have finished but the last
			// rate altering event is negative so we consider one more rate altering event.
			if (currentRow == lastRecordedRow || currentRow < currentRateEvent.GetRow())
			{
				currentRow += rowsPerBeat;
				continue;
			}

			var y = SpacingHelper.GetYForRow(currentRow, previousRateEventY);

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
			rowRelativeToTimeSignatureStart = currentRow - ts.IntegerPosition;
			beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
			var measureMarker = beatRelativeToTimeSignatureStart % beatsPerMeasure == 0;
			var measure = ts.MetricPosition.Measure + beatRelativeToTimeSignatureStart / beatsPerMeasure;

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
		var focalPointChartPosition = Position.ChartPosition;
		var focalPointY = (double)GetFocalPointY();
		var rateEnumerator = ActiveChart.RateAlteringEvents.FindBest(Position);
		if (rateEnumerator == null)
			return (0.0, 0.0);
		rateEnumerator.MoveNext();

		var interpolatedScrollRate = GetCurrentInterpolatedScrollRate();
		var spacingZoom = ZoomManager.GetSpacingZoom();

		// Determine the active rate event's position and rate information.
		spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
		var rateEventY = spacingHelper.GetY(rateEnumerator.Current, focalPointY, focalPointChartTime, focalPointChartPosition);
		var rateChartTime = rateEnumerator.Current!.GetChartTime();
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
		if (!Playing)
			return;
		AutoPlayer?.Update(Position);
	}

	private void UpdateAutoPlayFromScrolling()
	{
		AutoPlayer?.Stop();
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
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame())
		{
			miniMapNeedsMouseThisFrame = MiniMap.MouseDown(EditorMouseState.X(), EditorMouseState.Y());
		}

		MiniMap.MouseMove(EditorMouseState.X(), EditorMouseState.Y());
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).UpThisFrame()
		    || (MiniMapCapturingMouse && EditorMouseState.GetButtonState(EditorMouseState.Button.Left).Up()))
		{
			MiniMap.MouseUp(EditorMouseState.X(), EditorMouseState.Y());
		}

		MiniMapCapturingMouse = MiniMap.WantsMouse();

		// Set the Song Position based on the MiniMap position
		MiniMapCapturingMouse |= miniMapNeedsMouseThisFrame;
		if (MiniMapCapturingMouse)
		{
			// When moving the MiniMap, pause or stop playback.
			if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame() && Playing)
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
						editorPosition + focalPointY / (pScroll.TimeBasedPixelsPerSecond * ZoomManager.GetSpacingZoom());
					break;
				}
				case SpacingMode.ConstantRow:
				{
					Position.ChartPosition =
						editorPosition + focalPointY / (pScroll.RowBasedPixelsPerRow * ZoomManager.GetSpacingZoom());
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
		var sizeZoom = ZoomManager.GetSizeZoom();
		switch (p.PreferencesMiniMap.MiniMapPosition)
		{
			case MiniMap.Position.RightSideOfWindow:
			{
				x = Graphics.PreferredBackBufferWidth - (int)p.PreferencesMiniMap.MiniMapXPadding -
				    (int)p.PreferencesMiniMap.MiniMapWidth;
				break;
			}
			case MiniMap.Position.RightOfChartArea:
			{
				x = focalPointX + (WaveFormTextureWidth >> 1) + (int)p.PreferencesMiniMap.MiniMapXPadding;
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
					x = focalPointX + (WaveFormTextureWidth >> 1) + (int)p.PreferencesMiniMap.MiniMapXPadding;
				}

				break;
			}
			case MiniMap.Position.MountedToChart:
			{
				var receptorBounds =
					Receptor.GetBounds(GetFocalPoint(), sizeZoom, TextureAtlas, ArrowGraphicManager, ActiveChart);
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
				var spacingZoom = ZoomManager.GetSpacingZoom();
				var pps = pScroll.TimeBasedPixelsPerSecond * spacingZoom;
				var time = Position.ChartTime;

				// Editor Area. The visible time range.
				var editorAreaTimeStart = time - GetFocalPointY() / pps;
				var editorAreaTimeEnd = editorAreaTimeStart + screenHeight / pps;
				var editorAreaTimeRange = editorAreaTimeEnd - editorAreaTimeStart;

				// Determine the end time.
				var maxTimeFromChart = ActiveChart.GetEndChartTime();

				// Full Area. The time from the chart, extended in both directions by the editor range.
				var fullAreaTimeStart = ActiveChart.GetStartChartTime() - editorAreaTimeRange;
				var fullAreaTimeEnd = maxTimeFromChart + editorAreaTimeRange;

				// Content Area. The time from the chart.
				var contentAreaTimeStart = ActiveChart.GetStartChartTime();
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
				var chartPosition = 0.0;
				if (!ActiveChart.TryGetChartPositionFromTime(MiniMap.GetMiniMapAreaStart(), ref chartPosition))
					break;
				var enumerator = ActiveChart.EditorEvents.FindBestByPosition(chartPosition);
				if (enumerator == null)
					break;

				var numNotesAdded = 0;

				// Scan backwards until we have checked every lane for a long note which may
				// be extending through the given start row.
				var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
				foreach (var hsn in holdStartNotes)
				{
					MiniMap.AddHold(
						(LaneHoldStartNote)hsn.GetFirstEvent(),
						hsn.GetChartTime(),
						hsn.GetEndChartTime(),
						hsn.IsRoll(),
						hsn.IsSelected());
					numNotesAdded++;
				}

				while (enumerator.MoveNext())
				{
					var e = enumerator.Current;
					if (e is EditorTapNoteEvent)
					{
						numNotesAdded++;
						if (MiniMap.AddNote((LaneNote)e.GetFirstEvent(), e.GetChartTime(), e.IsSelected()) ==
						    MiniMap.AddResult.BelowBottom)
							break;
					}
					else if (e is EditorMineNoteEvent)
					{
						numNotesAdded++;
						if (MiniMap.AddMine((LaneNote)e.GetFirstEvent(), e.GetChartTime(), e.IsSelected()) ==
						    MiniMap.AddResult.BelowBottom)
							break;
					}
					else if (e is EditorHoldNoteEvent hold)
					{
						numNotesAdded++;
						if (MiniMap.AddHold(
							    (LaneHoldStartNote)hold.GetFirstEvent(),
							    hold.GetChartTime(),
							    hold.GetEndChartTime(),
							    hold.IsRoll(),
							    hold.IsSelected()) == MiniMap.AddResult.BelowBottom)
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
				var spacingZoom = ZoomManager.GetSpacingZoom();
				var ppr = pScroll.RowBasedPixelsPerRow * spacingZoom;

				// Editor Area. The visible row range.
				var editorAreaRowStart = chartPosition - GetFocalPointY() / ppr;
				var editorAreaRowEnd = editorAreaRowStart + screenHeight / ppr;
				var editorAreaRowRange = editorAreaRowEnd - editorAreaRowStart;

				// Determine the end row.
				var maxRowFromChart = ActiveChart.GetEndPosition();

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
				var enumerator = ActiveChart.EditorEvents.FindBestByPosition(chartPosition);
				if (enumerator == null)
					break;

				var numNotesAdded = 0;

				// Scan backwards until we have checked every lane for a long note which may
				// be extending through the given start row.
				var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
				foreach (var hsn in holdStartNotes)
				{
					MiniMap.AddHold(
						(LaneHoldStartNote)hsn.GetFirstEvent(),
						hsn.GetChartPosition(),
						hsn.GetEndChartPosition(),
						hsn.IsRoll(),
						hsn.IsSelected());
					numNotesAdded++;
				}

				while (enumerator.MoveNext())
				{
					var e = enumerator.Current;
					if (e is EditorTapNoteEvent)
					{
						numNotesAdded++;
						if (MiniMap.AddNote((LaneNote)e.GetFirstEvent(), e.GetChartPosition(), e.IsSelected()) ==
						    MiniMap.AddResult.BelowBottom)
							break;
					}
					else if (e is EditorMineNoteEvent)
					{
						numNotesAdded++;
						if (MiniMap.AddMine((LaneNote)e.GetFirstEvent(), e.GetChartPosition(), e.IsSelected()) ==
						    MiniMap.AddResult.BelowBottom)
							break;
					}
					else if (e is EditorHoldNoteEvent hold)
					{
						numNotesAdded++;
						if (MiniMap.AddHold(
							    (LaneHoldStartNote)hold.GetFirstEvent(),
							    hold.GetChartPosition(),
							    hold.GetEndChartPosition(),
							    hold.IsRoll(),
							    hold.IsSelected()) == MiniMap.AddResult.BelowBottom)
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

	private void DrawGui()
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
		UISelectionPreferences.Draw();
		UIWaveFormPreferences.Draw();
		UIMiniMapPreferences.Draw();
		UIReceptorPreferences.Draw();
		UIOptions.Draw();

		UISongProperties.Draw(ActiveSong);
		UIChartProperties.Draw(ActiveChart);
		UIChartList.Draw(ActiveSong, ActiveChart);
		UIExpressedChartConfig.Draw();
		UIPerformedChartConfig.Draw();
		UIAutogenConfigs.Draw();
		UIAutogenChart.Draw();

		UIChartPosition.Draw(
			GetFocalPointX(),
			Graphics.PreferredBackBufferHeight - GetChartPositionUIYPaddingFromBottom() - (int)(UIChartPosition.Height * 0.5),
			SnapLevels[SnapIndex]);

		if (ShowSavePopup)
		{
			ShowSavePopup = false;
			SystemSounds.Exclamation.Play();
			ImGui.OpenPopup(GetSavePopupTitle());
		}

		if (CanShowRightClickPopupThisFrame && EditorMouseState.GetButtonState(EditorMouseState.Button.Right).UpThisFrame())
		{
			ImGui.OpenPopup("RightClickPopup");
		}

		var lastPos = EditorMouseState.GetButtonState(EditorMouseState.Button.Right).GetLastClickUpPosition();
		DrawRightClickMenu((int)lastPos.X, (int)lastPos.Y);

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
				var editorFileName = ActiveSong?.GetFileName();
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
				{
					p.PreferencesOptions.ShowOptionsWindow = true;
					ImGui.SetWindowFocus(UIOptions.WindowTitle);
				}

				ImGui.Separator();
				if (ImGui.MenuItem("Song Properties"))
				{
					p.ShowSongPropertiesWindow = true;
					ImGui.SetWindowFocus(UISongProperties.WindowTitle);
				}

				if (ImGui.MenuItem("Chart Properties"))
				{
					p.ShowChartPropertiesWindow = true;
					ImGui.SetWindowFocus(UIChartProperties.WindowTitle);
				}

				if (ImGui.MenuItem("Chart List"))
				{
					p.ShowChartListWindow = true;
					ImGui.SetWindowFocus(UIChartList.WindowTitle);
				}

				ImGui.Separator();
				if (ImGui.MenuItem("Scroll Preferences"))
				{
					p.PreferencesScroll.ShowScrollControlPreferencesWindow = true;
					ImGui.SetWindowFocus(UIScrollPreferences.WindowTitle);
				}

				if (ImGui.MenuItem("Selection Preferences"))
				{
					p.PreferencesSelection.ShowSelectionControlPreferencesWindow = true;
					ImGui.SetWindowFocus(UISelectionPreferences.WindowTitle);
				}

				ImGui.Separator();
				if (ImGui.MenuItem("Waveform Preferences"))
				{
					p.PreferencesWaveForm.ShowWaveFormPreferencesWindow = true;
					ImGui.SetWindowFocus(UIWaveFormPreferences.WindowTitle);
				}

				if (ImGui.MenuItem("Mini Map Preferences"))
				{
					p.PreferencesMiniMap.ShowMiniMapPreferencesWindow = true;
					ImGui.SetWindowFocus(UIMiniMapPreferences.WindowTitle);
				}

				if (ImGui.MenuItem("Receptor Preferences"))
				{
					p.PreferencesReceptors.ShowReceptorPreferencesWindow = true;
					ImGui.SetWindowFocus(UIReceptorPreferences.WindowTitle);
				}

				ImGui.Separator();
				if (ImGui.MenuItem("Log"))
				{
					p.ShowLogWindow = true;
					ImGui.SetWindowFocus(UILog.WindowTitle);
				}

				if (ImGui.MenuItem("ImGui Demo Window"))
					ShowImGuiTestWindow = true;
				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Autogen"))
			{
				var disabled = !CanEdit();
				if (disabled)
					PushDisabled();

				if (ImGui.MenuItem("Configuration"))
				{
					p.ShowAutogenConfigsWindow = true;
					ImGui.SetWindowFocus(UIAutogenConfigs.WindowTitle);
				}

				if (ImGui.Selectable("Autogen New Chart"))
				{
					ShowAutogenChartUI(ActiveChart);
				}

				if (ImGui.BeginMenu("Autogen New Set of Charts"))
				{
					if (ActiveSong == null || ActiveSong.GetNumCharts() == 0)
					{
						PushDisabled();
						ImGui.Text("No Charts to Autogen From");
						PopDisabled();
					}
					else
					{
						foreach (var chartType in SupportedChartTypes)
						{
							var charts = ActiveSong.GetCharts(chartType);
							if (charts?.Count > 0)
							{
								if (ImGui.BeginMenu($"{GetPrettyEnumString(chartType)} Charts"))
								{
									DrawAutogenerateChartsOfTypeSelectableList(chartType);
									ImGui.EndMenu();
								}
							}
						}
					}

					ImGui.EndMenu();
				}

				if (disabled)
					PopDisabled();

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

	public void ShowAutogenChartUI(EditorChart sourceChart = null)
	{
		UIAutogenChart.Show(sourceChart);
	}

	public void DrawAutogenerateChartsOfTypeSelectableList(ChartType sourceChartType)
	{
		foreach (var chartType in SupportedChartTypes)
		{
			if (ImGui.Selectable($"Autogen New {GetPrettyEnumString(chartType)} Charts"))
			{
				// TODO
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
			                       && x >= GetFocalPointX() - (WaveFormTextureWidth >> 1)
			                       && x <= GetFocalPointX() + (WaveFormTextureWidth >> 1);
			var isInReceptorArea = Receptor.IsInReceptorArea(x, y, GetFocalPoint(), ZoomManager.GetSizeZoom(), TextureAtlas,
				ArrowGraphicManager, ActiveChart);

			if (SelectedEvents.Count > 0)
			{
				if (ImGui.BeginMenu("Selection"))
				{
					if (ImGui.MenuItem("Mirror"))
					{
						ActionQueue.Instance.Do(new ActionMirrorSelection(this, ActiveChart, SelectedEvents));
					}

					if (ImGui.MenuItem("Flip"))
					{
						ActionQueue.Instance.Do(new ActionFlipSelection(this, ActiveChart, SelectedEvents));
					}

					if (ImGui.MenuItem("Mirror and Flip"))
					{
						ActionQueue.Instance.Do(new ActionMirrorAndFlipSelection(this, ActiveChart, SelectedEvents));
					}

					if (ImGui.MenuItem("Shift Right"))
					{
						OnShiftSelectedNotesRight();
					}

					if (ImGui.MenuItem("Shift Right and Wrap", "Ctrl+Shift+Right"))
					{
						OnShiftSelectedNotesRightAndWrap();
					}

					if (ImGui.MenuItem("Shift Left"))
					{
						OnShiftSelectedNotesLeft();
					}

					if (ImGui.MenuItem("Shift Left and Wrap", "Ctrl+Shift+Left"))
					{
						OnShiftSelectedNotesLeftAndWrap();
					}

					var rows = SnapLevels[SnapIndex].Rows;
					if (rows == 0)
						rows = MaxValidDenominator;
					var shiftAmount = $"1/{MaxValidDenominator / rows * NumBeatsPerMeasure}";

					if (ImGui.MenuItem($"Shift Earlier ({shiftAmount})", "Ctrl+Shift+Up"))
					{
						OnShiftSelectedNotesEarlier();
					}

					if (ImGui.MenuItem($"Shift Later ({shiftAmount})", "Ctrl+Shift+Down"))
					{
						OnShiftSelectedNotesLater();
					}

					if (ImGui.BeginMenu("Convert"))
					{
						if (ImGui.MenuItem("Taps to Mines"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorTapNoteEvent,
								(e) => EditorEvent.CreateEvent(
									EventConfig.CreateMineConfig(ActiveChart, e.GetChartPosition(), e.GetChartTime(),
										e.GetLane()))));
						}

						if (ImGui.MenuItem("Mines to Taps"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorMineNoteEvent,
								(e) => EditorEvent.CreateEvent(
									EventConfig.CreateTapConfig(ActiveChart, e.GetChartPosition(), e.GetChartTime(),
										e.GetLane()))));
						}

						ImGui.Separator();
						if (ImGui.MenuItem("Holds to Rolls"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
								(e) => EditorHoldNoteEvent.CreateHold(ActiveChart, e.GetLane(), e.GetRow(), e.GetLength(),
									true)));
						}

						if (ImGui.MenuItem("Holds to Taps"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
								(e) => EditorEvent.CreateEvent(
									EventConfig.CreateTapConfig(ActiveChart, e.GetChartPosition(), e.GetChartTime(),
										e.GetLane()))));
						}

						if (ImGui.MenuItem("Holds to Mines"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
								(e) => EditorEvent.CreateEvent(
									EventConfig.CreateMineConfig(ActiveChart, e.GetChartPosition(), e.GetChartTime(),
										e.GetLane()))));
						}

						ImGui.Separator();
						if (ImGui.MenuItem("Rolls to Holds"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
								(e) => EditorHoldNoteEvent.CreateHold(ActiveChart, e.GetLane(), e.GetRow(), e.GetLength(),
									false)));
						}

						if (ImGui.MenuItem("Rolls to Taps"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
								(e) => EditorEvent.CreateEvent(
									EventConfig.CreateTapConfig(ActiveChart, e.GetChartPosition(), e.GetChartTime(),
										e.GetLane()))));
						}

						if (ImGui.MenuItem("Rolls to Mines"))
						{
							ActionQueue.Instance.Do(new ActionChangeNoteType(this, ActiveChart, SelectedEvents,
								(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
								(e) => EditorEvent.CreateEvent(
									EventConfig.CreateMineConfig(ActiveChart, e.GetChartPosition(), e.GetChartTime(),
										e.GetLane()))));
						}

						ImGui.EndMenu();
					}

					ImGui.EndMenu();
				}
			}

			if (ImGui.BeginMenu("Select All"))
			{
				if (ImGui.MenuItem("Notes", "Ctrl+A"))
				{
					OnSelectAll();
				}

				if (ImGui.Selectable("Taps"))
				{
					OnSelectAllImpl((e) => e is EditorTapNoteEvent);
				}

				if (ImGui.Selectable("Mines"))
				{
					OnSelectAllImpl((e) => e is EditorMineNoteEvent);
				}

				if (ImGui.Selectable("Holds"))
				{
					OnSelectAllImpl((e) => e is EditorHoldNoteEvent hn && !hn.IsRoll());
				}

				if (ImGui.Selectable("Rolls"))
				{
					OnSelectAllImpl((e) => e is EditorHoldNoteEvent hn && hn.IsRoll());
				}

				if (ImGui.Selectable("Holds and Rolls"))
				{
					OnSelectAllImpl((e) => e is EditorHoldNoteEvent);
				}

				if (ImGui.MenuItem("Miscellaneous Events", "Ctrl+Alt+A"))
				{
					OnSelectAllAlt();
				}

				if (ImGui.MenuItem("Notes and Miscellaneous Events", "Ctrl+Shift+A"))
				{
					OnSelectAllShift();
				}

				ImGui.EndMenu();
			}

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
				if (ImGui.BeginMenu("Add Event"))
				{
					var nearestMeasureBoundaryRow = ActiveChart.GetNearestMeasureBoundaryRow(row);
					var eventsAtNearestMeasureBoundary = ActiveChart.EditorEvents.FindEventsAtRow(nearestMeasureBoundaryRow);

					var events = ActiveChart.EditorEvents.FindEventsAtRow(row);
					var hasTempoEvent = false;
					var hasInterpolatedScrollRateEvent = false;
					var hasScrollRateEvent = false;
					var hasStopEvent = false;
					var hasDelayEvent = false;
					var hasWarpEvent = false;
					var hasFakeEvent = false;
					var hasTickCountEvent = false;
					var hasMultipliersEvent = false;
					var hasTimeSignatureEvent = false;
					var hasLabelEvent = false;

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

					var chartTime = 0.0;
					ActiveChart.TryGetTimeFromChartPosition(row, ref chartTime);

					var nearestMeasureChartTime = 0.0;
					ActiveChart.TryGetTimeFromChartPosition(nearestMeasureBoundaryRow, ref nearestMeasureChartTime);

					var currentRateAlteringEvent = ActiveChart.FindActiveRateAlteringEventForPosition(row);

					DrawAddEventMenuItem("Tempo", !hasTempoEvent, UITempoColorRGBA, EditorTempoEvent.EventShortDescription, row,
						() => EditorEvent.CreateEvent(
							EventConfig.CreateTempoConfig(ActiveChart, row, chartTime,
								currentRateAlteringEvent?.GetTempo() ?? EditorChart.DefaultTempo)));

					ImGui.Separator();
					DrawAddEventMenuItem("Interpolated Scroll Rate", !hasInterpolatedScrollRateEvent, UISpeedsColorRGBA,
						EditorInterpolatedRateAlteringEvent.EventShortDescription, row,
						() => EditorEvent.CreateEvent(
							EventConfig.CreateScrollRateInterpolationConfig(ActiveChart, row, chartTime)));
					DrawAddEventMenuItem("Scroll Rate", !hasScrollRateEvent, UIScrollsColorRGBA,
						EditorScrollRateEvent.EventShortDescription, row,
						() => EditorEvent.CreateEvent(EventConfig.CreateScrollRateConfig(ActiveChart, row, chartTime)));

					ImGui.Separator();
					DrawAddEventMenuItem("Stop", !hasStopEvent, UIStopColorRGBA, EditorStopEvent.EventShortDescription, row,
						() =>
						{
							var stopTime = currentRateAlteringEvent.GetSecondsPerRow() * MaxValidDenominator;
							return EditorEvent.CreateEvent(EventConfig.CreateStopConfig(ActiveChart, row, chartTime, stopTime));
						});
					DrawAddEventMenuItem("Delay", !hasDelayEvent, UIDelayColorRGBA, EditorDelayEvent.EventShortDescription, row,
						() =>
						{
							var stopTime = currentRateAlteringEvent.GetSecondsPerRow() * MaxValidDenominator;
							return EditorEvent.CreateEvent(EventConfig.CreateDelayConfig(ActiveChart, row, chartTime, stopTime));
						});
					DrawAddEventMenuItem("Warp", !hasWarpEvent, UIWarpColorRGBA, EditorWarpEvent.EventShortDescription, row,
						() => EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(ActiveChart, row, chartTime)));

					ImGui.Separator();
					DrawAddEventMenuItem("Fake Region", !hasFakeEvent, UIFakesColorRGBA,
						EditorFakeSegmentEvent.EventShortDescription, row, () =>
						{
							var fakeLength = currentRateAlteringEvent.GetSecondsPerRow() * MaxValidDenominator;
							return EditorEvent.CreateEvent(EventConfig.CreateFakeConfig(ActiveChart, row, chartTime, fakeLength));
						});
					DrawAddEventMenuItem("Ticks", !hasTickCountEvent, UITicksColorRGBA,
						EditorTickCountEvent.EventShortDescription, row,
						() => EditorEvent.CreateEvent(EventConfig.CreateTickCountConfig(ActiveChart, row, chartTime)));
					DrawAddEventMenuItem("Combo Multipliers", !hasMultipliersEvent, UIMultipliersColorRGBA,
						EditorMultipliersEvent.EventShortDescription, row,
						() => EditorEvent.CreateEvent(EventConfig.CreateMultipliersConfig(ActiveChart, row, chartTime)));
					DrawAddEventMenuItem("Time Signature", !hasTimeSignatureEvent, UITimeSignatureColorRGBA,
						EditorTimeSignatureEvent.EventShortDescription, nearestMeasureBoundaryRow,
						() => EditorEvent.CreateEvent(EventConfig.CreateTimeSignatureConfig(ActiveChart,
							nearestMeasureBoundaryRow, nearestMeasureChartTime, EditorChart.DefaultTimeSignature)), true);
					DrawAddEventMenuItem("Label", !hasLabelEvent, UILabelColorRGBA, EditorLabelEvent.EventShortDescription, row,
						() => EditorEvent.CreateEvent(EventConfig.CreateLabelConfig(ActiveChart, row, chartTime)));

					ImGui.Separator();
					if (MenuItemWithColor("(Move) Music Preview", true, UIPreviewColorRGBA))
					{
						var startTime = Math.Max(0.0, Position.SongTime);
						ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(ActiveSong,
							nameof(EditorSong.SampleStart), startTime, true));
					}

					ToolTip(EditorPreviewRegionEvent.EventShortDescription);
					if (MenuItemWithColor("(Move) End Hint", true, UILastSecondHintColorRGBA))
					{
						var currentTime = Math.Max(0.0, Position.ChartTime);
						ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(ActiveSong,
							nameof(EditorSong.LastSecondHint), currentTime, true));
					}

					ToolTip(EditorLastSecondHintEvent.EventShortDescription);

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

	private void DrawAddEventMenuItem(string name, bool enabled, uint color, string toolTipText, int row,
		Func<EditorEvent> createEventFunc, bool onlyOnePerMeasure = false)
	{
		if (MenuItemWithColor(name, enabled, color))
		{
			ActionQueue.Instance.Do(new ActionAddEditorEvent(createEventFunc()));
		}

		if (!enabled)
		{
			if (onlyOnePerMeasure)
				toolTipText +=
					$"\n\nOnly one {name} event can be specified per measure.\nThere is already a {name} specified on the measure at row {row}.";
			else
				toolTipText +=
					$"\n\nOnly one {name} event can be specified per row.\nThere is already a {name} specified on row {row}.";
		}

		ToolTip(toolTipText);
	}

	private string GetSavePopupTitle()
	{
		var appName = GetAppName();
		return $"{appName}##save";
	}

	private void DrawUnsavedChangesPopup()
	{
		if (ImGui.BeginPopupModal(GetSavePopupTitle()))
		{
			if (!string.IsNullOrEmpty(ActiveSong.Title))
				ImGui.Text(
					$"Do you want to save the changes you made to {ActiveSong.Title}?\nYour changes will be lost if you don't save them.");
			else
				ImGui.Text("Do you want to save your changes?\nYour changes will be lost if you don't save them.");

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
			ImGui.Checkbox("Render Chart", ref RenderChart);
			ImGui.Text($"Update Time:       {UpdateTimeTotal:F6} seconds");
			ImGui.Text($"  Waveform:        {UpdateTimeWaveForm:F6} seconds");
			ImGui.Text($"  Mini Map:        {UpdateTimeMiniMap:F6} seconds");
			ImGui.Text($"  Chart Events:    {UpdateTimeChartEvents:F6} seconds");
			ImGui.Text($"Draw Time:         {DrawTimeTotal:F6} seconds");
			ImGui.Text($"Total Time:        {UpdateTimeTotal + DrawTimeTotal:F6} seconds");
			ImGui.Text($"Total FPS:         {1.0f / (UpdateTimeTotal + DrawTimeTotal):F6}");
			ImGui.Text($"Actual Time:       {1.0f / ImGui.GetIO().Framerate:F6} seconds");
			ImGui.Text($"Actual FPS:        {ImGui.GetIO().Framerate:F6}");

			if (ImGui.Button("Reload Song"))
			{
				StopPreview();
				MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), GetSongTime, true,
					Preferences.Instance.PreferencesWaveForm.EnableWaveForm);
			}

			if (ImGui.Button("Save Time and Zoom"))
			{
				Preferences.Instance.DebugSongTime = Position.SongTime;
				Preferences.Instance.DebugZoom = ZoomManager.GetSpacingZoom();
			}

			if (ImGui.Button("Load Time and Zoom"))
			{
				Position.SongTime = Preferences.Instance.DebugSongTime;
				ZoomManager.SetZoom(Preferences.Instance.DebugZoom);
			}
		}

		ImGui.End();
	}

	#endregion Gui Rendering

	#region StepGraphs

	/// <summary>
	/// Initializes all PadData and creates corresponding StepGraphs for all ChartTypes
	/// specified in the StartupChartTypes.
	/// </summary>
	private async void InitStepGraphDataAsync()
	{
		foreach (var chartType in SupportedChartTypes)
		{
			PadDataByChartType[chartType] = null;
			StepGraphByChartType[chartType] = null;
		}

		// Attempt to load pad data for all supported chart types.
		var padDataTasks = new Task<PadData>[SupportedChartTypes.Length];
		for (var i = 0; i < SupportedChartTypes.Length; i++)
		{
			PadDataByChartType[SupportedChartTypes[i]] = null;
			padDataTasks[i] = LoadPadData(SupportedChartTypes[i]);
		}

		await Task.WhenAll(padDataTasks);
		for (var i = 0; i < SupportedChartTypes.Length; i++)
		{
			PadDataByChartType[SupportedChartTypes[i]] = padDataTasks[i].Result;
		}

		// Create StepGraphs.
		var pOptions = Preferences.Instance.PreferencesOptions;
		var validStepGraphTypes = new HashSet<ChartType>();
		for (var i = 0; i < pOptions.StartupChartTypes.Length; i++)
		{
			var chartType = pOptions.StartupChartTypes[i];
			if (PadDataByChartType.TryGetValue(chartType, out var padData) && padData != null)
				validStepGraphTypes.Add(chartType);
		}

		ChartType[] validStepGraphArray = null;
		if (validStepGraphTypes.Count > 0)
		{
			validStepGraphArray = validStepGraphTypes.ToArray();

			var stepGraphTasks = new Task<StepGraph>[validStepGraphArray.Length];
			var index = 0;
			foreach (var stepGraphType in validStepGraphArray)
			{
				stepGraphTasks[index++] = CreateStepGraph(stepGraphType);
			}

			await Task.WhenAll(stepGraphTasks);
			for (var i = 0; i < validStepGraphTypes.Count; i++)
			{
				StepGraphByChartType[validStepGraphArray[i]] = stepGraphTasks[i].Result;
			}
		}

		// Set up root nodes.
		if (validStepGraphArray != null)
		{
			var rootNodesByChartType = new Dictionary<ChartType, List<List<GraphNode>>>();
			await Task.Run(() =>
			{
				for (var i = 0; i < validStepGraphArray.Length; i++)
				{
					var chartType = validStepGraphArray[i];
					var rootNodes = new List<List<GraphNode>>();
					var stepGraph = StepGraphByChartType[chartType];
					var found = true;

					// Add the root node as the first tier.
					rootNodes.Add(new List<GraphNode> { stepGraph.GetRoot() });

					// Loop over the remaining tiers.
					for (var tier = 1; tier < stepGraph.PadData.StartingPositions.Length; tier++)
					{
						var nodesAtTier = new List<GraphNode>();
						foreach (var pos in stepGraph.PadData.StartingPositions[tier])
						{
							var node = stepGraph.FindGraphNode(
								pos[Constants.L], GraphArrowState.Resting,
								pos[Constants.R], GraphArrowState.Resting);
							if (node == null)
							{
								Logger.Error(
									$"Could not find a node in the {GetPrettyEnumString(chartType)} StepGraph for StartingPosition with"
									+ $" left on {pos[Constants.L]} and right on {pos[Constants.R]}.");
								found = false;
								break;
							}

							nodesAtTier.Add(node);
						}

						if (!found)
							break;
						rootNodes.Add(nodesAtTier);
					}

					if (found)
						rootNodesByChartType.Add(chartType, rootNodes);
				}
			});
			RootNodesByChartType = rootNodesByChartType;
		}

		// Load the default StepTypeFallbacks.
		StepTypeFallbacks = await StepTypeFallbacks.Load(StepTypeFallbacks.DefaultFallbacksFileName);
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

		var fullFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
		if (!File.Exists(fullFileName))
		{
			Logger.Warn($"Could not find PadData file {fileName}.");
			return null;
		}

		Logger.Info($"Loading {chartTypeString} PadData from {fileName}.");
		var padData = await PadData.LoadPadData(chartTypeString, fileName);
		if (padData == null)
			return null;
		Logger.Info($"Finished loading {chartTypeString} PadData.");
		return padData;
	}

	/// <summary>
	/// Loads StepGraph for the given StepMania StepsType.
	/// </summary>
	/// <param name="chartType">ChartType to load StepGraph for.</param>
	/// <returns>Loaded StepGraph or null if any errors were generated.</returns>
	private async Task<StepGraph> CreateStepGraph(ChartType chartType)
	{
		var chartTypeString = ChartTypeString(chartType);
		var fileName = $"{chartTypeString}.fsg";

		var fullFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
		if (!File.Exists(fullFileName))
		{
			Logger.Warn($"Could not find StepGraph file {fileName}.");
			return null;
		}

		Logger.Info($"Loading {chartTypeString} StepGraph from {fileName}.");
		var stepGraph = await StepGraph.LoadAsync(fullFileName, PadDataByChartType[chartType]);
		if (stepGraph == null)
			return null;
		Logger.Info($"Finished loading {chartTypeString} StepGraph.");
		return stepGraph;
	}

	public PadData GetPadData(ChartType chartType)
	{
		if (PadDataByChartType.TryGetValue(chartType, out var padData))
			return padData;
		return null;
	}

	public bool GetStepGraph(ChartType chartType, out StepGraph stepGraph)
	{
		return StepGraphByChartType.TryGetValue(chartType, out stepGraph);
	}

	public bool GetStepGraphRootNodes(ChartType chartType, out List<List<GraphNode>> rootNodes)
	{
		return RootNodesByChartType.TryGetValue(chartType, out rootNodes);
	}

	public StepTypeFallbacks GetStepTypeFallbacks()
	{
		return StepTypeFallbacks;
	}

	#endregion StepGraphs

	#region Save and Load

	private bool IsSaving()
	{
		return ActiveSong?.IsSaving() ?? false;
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

		if (ActiveSong.GetFileFormat() == null)
			return false;

		if (string.IsNullOrEmpty(ActiveSong.GetFileFullPath()))
			return false;

		return true;
	}

	private void OnSave()
	{
		if (EditEarlyOut())
			return;

		if (!CanSaveWithoutLocationPrompt())
		{
			OnSaveAs();
			return;
		}

		Save(ActiveSong.GetFileFormat().Type, ActiveSong.GetFileFullPath(), ActiveSong);
	}

	private void OnSaveAs()
	{
		if (EditEarlyOut())
			return;

		if (ActiveSong == null)
			return;

		var saveFileDialog1 = new SaveFileDialog();
		saveFileDialog1.Filter = "SSC File|*.ssc|SM File|*.sm";
		saveFileDialog1.Title = "Save As...";
		saveFileDialog1.FilterIndex = 0;
		if (ActiveSong.GetFileFormat() != null && ActiveSong.GetFileFormat().Type == FileFormatType.SM)
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
		if (EditEarlyOut())
			return;

		editorSong.Save(fileType, fullPath, () =>
		{
			UpdateWindowTitle();
			UpdateRecentFilesForActiveSong(Position.ChartPosition, ZoomManager.GetSpacingZoom());
			ActionQueue.Instance.OnSaved();
			TryInvokePostSaveFunction();
		});
	}

	/// <summary>
	/// Starts the process of opening a Song file by presenting a dialog to choose a Song.
	/// </summary>
	private void OpenSongFile()
	{
		var pOptions = Preferences.Instance.PreferencesOptions;
		using var openFileDialog = new OpenFileDialog();
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

			CloseSong();

			// Start an asynchronous series of operations to load the song.
			EditorChart newActiveChart = null;
			EditorSong newActiveSong = null;
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
					await Task.Run(() =>
					{
						newActiveSong = new EditorSong(
							fileName,
							song,
							GraphicsDevice,
							ImGuiRenderer,
							IsChartSupported,
							this,
							this);
					});

					// Select the best Chart to make active.
					newActiveChart = SelectBestChart(newActiveSong, chartType, chartDifficultyType);
					LoadSongCancellationTokenSource.Token.ThrowIfCancellationRequested();
				}
				catch (OperationCanceledException)
				{
					// Upon cancellation null out the Song and ActiveChart.
					CloseSong();
				}
				finally
				{
					LoadSongCancellationTokenSource?.Dispose();
					LoadSongCancellationTokenSource = null;
				}
			}, LoadSongCancellationTokenSource.Token);
			await LoadSongTask;
			ActiveSong = newActiveSong;
			if (ActiveSong == null)
			{
				return;
			}

			// Set the position and zoom to the last used values for this song.
			var desiredChartPosition = 0.0;
			var desiredZoom = 1.0;
			var savedInfo = GetMostRecentSavedSongInfoForActiveSong();
			if (savedInfo != null)
			{
				desiredChartPosition = savedInfo.ChartPosition;
				desiredZoom = savedInfo.SpacingZoom;
			}

			// Insert a new entry at the top of the saved recent files.
			UpdateRecentFilesForActiveSong(desiredChartPosition, desiredZoom);

			OnChartSelected(newActiveChart, false);

			// Set position and zoom.
			Position.Reset();
			Position.ChartPosition = desiredChartPosition;
			ZoomManager.SetZoom(desiredZoom);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {fileName}. {e}");
		}
	}

	private Preferences.SavedSongInformation GetMostRecentSavedSongInfoForActiveSong()
	{
		if (ActiveSong == null || string.IsNullOrEmpty(ActiveSong.GetFileFullPath()))
			return null;
		foreach (var savedInfo in Preferences.Instance.RecentFiles)
		{
			if (savedInfo.FileName == ActiveSong.GetFileFullPath())
			{
				return savedInfo;
			}
		}

		return null;
	}

	private void UpdateRecentFilesForActiveSong(double chartPosition, double spacingZoom)
	{
		if (ActiveSong == null || string.IsNullOrEmpty(ActiveSong.GetFileFullPath()))
			return;

		var p = Preferences.Instance;
		var pOptions = p.PreferencesOptions;
		var savedSongInfo = new Preferences.SavedSongInformation
		{
			FileName = ActiveSong.GetFileFullPath(),
			LastChartType = ActiveChart?.ChartType ?? pOptions.DefaultStepsType,
			LastChartDifficultyType = ActiveChart?.ChartDifficultyType ?? pOptions.DefaultDifficultyType,
			SpacingZoom = spacingZoom,
			ChartPosition = chartPosition,
		};
		p.RecentFiles.RemoveAll(info => info.FileName == ActiveSong.GetFileFullPath());
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
	private EditorChart SelectBestChart(EditorSong song, ChartType preferredChartType,
		ChartDifficultyType preferredChartDifficultyType)
	{
		var preferredChartsByType = song.GetCharts(preferredChartType);
		var hasChartsOfPreferredType = preferredChartsByType != null;

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
			foreach (var currentDifficultyType in orderedDifficultyTypes)
			{
				foreach (var chart in preferredChartsByType)
				{
					if (chart.ChartDifficultyType == currentDifficultyType)
						return chart;
				}
			}
		}

		// No charts of the specified type exist. Try the next best type.
		var nextBestChartType = ChartType.dance_single;
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
			var nextBestChartsByType = song.GetCharts(nextBestChartType);
			if (nextBestChartsByType != null)
			{
				foreach (var currentDifficultyType in orderedDifficultyTypes)
				{
					foreach (var chart in nextBestChartsByType)
					{
						if (chart.ChartDifficultyType == currentDifficultyType)
							return chart;
					}
				}
			}
		}

		// At this point, just return the first chart we have.
		foreach (var supportedChartType in SupportedChartTypes)
		{
			var charts = song.GetCharts(supportedChartType);
			if (charts?.Count > 0)
			{
				return charts[0];
			}
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
				fullPath = Path.Combine(ActiveSong.GetFileDirectory(), relativeFile);
		}

		return fullPath;
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

	private void OnOpenFile(string songFile)
	{
		PendingOpenSongFileName = songFile;
		if (ActionQueue.Instance.HasUnsavedChanges())
		{
			PostSaveFunction = OnOpenFileNoSave;
			ShowSavePopup = true;
		}
		else
		{
			OnOpenFileNoSave();
		}
	}

	private void OnOpenFileNoSave()
	{
		if (string.IsNullOrEmpty(PendingOpenSongFileName))
			return;
		var pOptions = Preferences.Instance.PreferencesOptions;
		OpenSongFileAsync(PendingOpenSongFileName,
			pOptions.DefaultStepsType,
			pOptions.DefaultDifficultyType);
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
		CloseSong();
		ActiveSong = new EditorSong(GraphicsDevice, ImGuiRenderer, this);

		if (!string.IsNullOrEmpty(PendingMusicFile))
		{
			ActiveSong.MusicPath = PendingMusicFile;
			PendingMusicFile = null;
		}

		if (!string.IsNullOrEmpty(PendingLyricsFile))
		{
			ActiveSong.LyricsPath = PendingLyricsFile;
			PendingLyricsFile = null;
		}

		if (!string.IsNullOrEmpty(PendingImageFile))
		{
			switch (GetBestSongImageType(PendingImageFile))
			{
				case SongImageType.Background:
					ActiveSong.BackgroundPath = PendingImageFile;
					break;
				case SongImageType.Banner:
					ActiveSong.BannerPath = PendingImageFile;
					break;
				case SongImageType.Jacket:
					ActiveSong.JacketPath = PendingImageFile;
					break;
				case SongImageType.CDImage:
					ActiveSong.CDImagePath = PendingImageFile;
					break;
				case SongImageType.DiscImage:
					ActiveSong.DiscImagePath = PendingImageFile;
					break;
				case SongImageType.CDTitle:
					ActiveSong.CDTitlePath = PendingImageFile;
					break;
			}

			PendingImageFile = null;
		}

		if (!string.IsNullOrEmpty(PendingVideoFile))
		{
			// Assume that a video file is meant to be the background.
			ActiveSong.BackgroundPath = PendingVideoFile;
			PendingVideoFile = null;
		}

		Position.Reset();
		ZoomManager.SetZoom(1.0);
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
		CloseSong();
		Position.Reset();
		ZoomManager.SetZoom(1.0);
	}

	private void CloseSong()
	{
		// First, save the current zoom and position to the song history so we can restore them when
		// opening this song again later.
		var savedSongInfo = GetMostRecentSavedSongInfoForActiveSong();
		savedSongInfo?.UpdatePosition(ZoomManager.GetSpacingZoom(), Position.ChartPosition);
		UnloadSongResources();
	}

	private void UnloadSongResources()
	{
		// Close any UI which holds on to Song/Chart state.
		UIAutogenChart.Close();

		StopPlayback();
		MusicManager.UnloadAsync();
		LaneEditStates = Array.Empty<LaneEditState>();
		ClearSelectedEvents();
		ActiveSong = null;
		ActiveChart = null;
		Position.ActiveChart = null;
		ArrowGraphicManager = null;
		Receptors = null;
		AutoPlayer = null;
		EditorMouseState.SetActiveChart(null);
		UpdateWindowTitle();
		ActionQueue.Instance.Clear();
	}

	private void UpdateWindowTitle()
	{
		if (Window == null)
			return;
		var hasUnsavedChanges = ActionQueue.Instance.HasUnsavedChanges();
		var appName = GetAppName();
		var sb = new StringBuilder();
		var title = "New File";
		if (ActiveSong != null && !string.IsNullOrEmpty(ActiveSong.GetFileName()))
		{
			title = ActiveSong.GetFileName();
		}

		sb.Append(title);
		if (hasUnsavedChanges)
		{
			sb.Append(" * ");
		}

		if (ActiveChart != null)
		{
			sb.Append(
				$" [{GetPrettyEnumString(ActiveChart.ChartType)} - {GetPrettyEnumString(ActiveChart.ChartDifficultyType)}]");
		}

		sb.Append(" - ");
		sb.Append(appName);
		Window.Title = sb.ToString();
	}

	#endregion Save and Load

	#region Selection

	public void OnNoteTransformationBegin()
	{
		TransformingNotes = true;
	}

	public void OnNoteTransformationEnd(List<EditorEvent> transformedEvents)
	{
		TransformingNotes = false;

		// When a transformation ends, set the selection to the transformed notes.
		// Technically this will deselect notes if the user performed a transform
		// on a set of events where not all were eligible to be transformed. For
		// example, if they selected all events (including rate altering events)
		// and then mirrored the selection, this would deselect the rate altering
		// events. However, this logic also guarantees that after a transform,
		// including transforms initiated from undo and redo, the selection contains
		// the modified notes.
		SetSelectEvents(transformedEvents);
	}

	private void SelectEvent(EditorEvent e, bool setLastSelected)
	{
		if (setLastSelected)
			LastSelectedEvent = e;
		if (e.IsSelected())
			return;
		e.Select();
		SelectedEvents.Add(e);
	}

	private void DeselectEvent(EditorEvent e)
	{
		if (!e.IsSelected())
			return;
		if (LastSelectedEvent == e)
			LastSelectedEvent = null;
		e.Deselect();
		SelectedEvents.Remove(e);
	}

	private void ClearSelectedEvents()
	{
		foreach (var selectedEvent in SelectedEvents)
			selectedEvent.Deselect();
		SelectedEvents.Clear();
		LastSelectedEvent = null;
	}

	private void OnDelete()
	{
		if (EditEarlyOut())
			return;

		if (ActiveChart == null || SelectedEvents.Count < 1)
			return;
		var eventsToDelete = new List<EditorEvent>();
		foreach (var editorEvent in SelectedEvents)
		{
			if (!editorEvent.CanBeDeleted())
				continue;
			eventsToDelete.Add(editorEvent);
		}

		if (eventsToDelete.Count == 0)
			return;
		ActionQueue.Instance.Do(new ActionDeleteEditorEvents(eventsToDelete, false));
	}

	private void OnEventsDeleted(List<EditorEvent> deletedEvents)
	{
		if (SelectedEvents.Count == 0)
			return;

		// When transforming notes we expect selected notes to be moved which requires
		// deleting them, then modifying them, and then re-adding them. We don't want
		// to deselect notes when they are moving.
		if (TransformingNotes)
			return;

		// If a selected note was deleted, deselect it.
		foreach (var deletedEvent in deletedEvents)
		{
			if (SelectedEvents.Contains(deletedEvent))
				DeselectEvent(deletedEvent);
		}
	}

	private void SetSelectEvents(List<EditorEvent> selectedEvents)
	{
		ClearSelectedEvents();
		foreach (var selectedEvent in selectedEvents)
			SelectEvent(selectedEvent, true);
	}

	public void OnSelectAll()
	{
		OnSelectAllImpl((e) => e.IsSelectableWithoutModifiers());
	}

	public void OnSelectAllAlt()
	{
		OnSelectAllImpl((e) => e.IsSelectableWithModifiers());
	}

	public void OnSelectAllShift()
	{
		OnSelectAllImpl((e) => e.IsSelectableWithoutModifiers() || e.IsSelectableWithModifiers());
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
		Func<EditorEvent, bool> isSelectable = alt
			? (e) => e.IsSelectableWithModifiers()
			: (e) => e.IsSelectableWithoutModifiers();

		var (_, arrowHeightUnscaled) = GetArrowDimensions(false);
		var halfArrowH = arrowHeightUnscaled * ZoomManager.GetSizeZoom() * 0.5;
		var halfMiscEventH = ImGuiLayoutUtils.GetMiscEditorEventHeight() * 0.5;

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
				// Early out if we have searched beyond the selected y. Add an extra half arrow
				// height to this check so that short miscellaneous events do not cause us to
				// early out prematurely.
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
			var (minLane, maxLane) = GetSelectedLanes();

			var fullyOutsideLanes = maxLane < 0 || minLane >= ActiveChart.NumInputs;
			var partiallyOutsideLanes = !fullyOutsideLanes && (minLane < 0 || maxLane >= ActiveChart.NumInputs);
			var selectMiscEvents = alt || fullyOutsideLanes;

			// Select by time.
			if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.ConstantTime)
			{
				var (minTime, maxTime) = SelectedRegion.GetSelectedChartTimeRange();

				// Select notes.
				if (!selectMiscEvents)
				{
					// Adjust the time to account for the selection preference for how much of an event should be
					// within the selected region.
					var (adjustedMinTime, adjustedMaxTime) = AdjustSelectionTimeRange(minTime, maxTime, halfArrowH);
					if (adjustedMinTime < adjustedMaxTime)
					{
						var enumerator = ActiveChart.EditorEvents.FindFirstAfterChartTime(adjustedMinTime);
						while (enumerator.MoveNext())
						{
							if (enumerator.Current!.GetChartTime() > adjustedMaxTime)
								break;
							if (!isSelectable(enumerator.Current))
								continue;
							var lane = enumerator.Current.GetLane();
							if (lane < minLane || lane > maxLane)
								continue;
							newlySelectedEvents.Add(enumerator.Current);
						}
					}

					// If nothing was selected and the selection was partially outside of the lanes, treat it as
					// an attempt to select misc events.
					if (newlySelectedEvents.Count == 0 && partiallyOutsideLanes)
						selectMiscEvents = true;
				}

				// Select misc. events.
				if (selectMiscEvents)
				{
					// Adjust the time to account for the selection preference for how much of an event should be
					// within the selected region.
					var (adjustedMinTime, adjustedMaxTime) = AdjustSelectionTimeRange(minTime, maxTime, halfMiscEventH);

					// Collect potential misc events.
					var potentialEvents = new List<EditorEvent>();

					// Loop over the misc events in the selected time range and determine their positions.
					BeginMiscEventWidgetLayoutManagerFrame();
					var minPosition = 0.0;
					ActiveChart.TryGetChartPositionFromTime(adjustedMinTime, ref minPosition);
					var enumerator = ActiveChart.MiscEvents.FindBestByPosition(minPosition);
					while (enumerator != null && enumerator.MoveNext())
					{
						var miscEvent = enumerator.Current;
						if (miscEvent!.GetChartTime() > adjustedMaxTime)
							break;
						if (!miscEvent.IsSelectableWithModifiers())
							continue;
						MiscEventWidgetLayoutManager.PositionEvent(miscEvent);
						potentialEvents.Add(miscEvent);
					}

					// Now that we know the x positions of the potential misc events, check each
					// event to see if it overlaps the selected region and add it to newlySelectedEvents.
					var (xStart, xEnd) = SelectedRegion.GetSelectedXRange();
					foreach (var potentialEvent in potentialEvents)
					{
						if (!(potentialEvent.GetChartTime() >= adjustedMinTime &&
						      potentialEvent.GetChartTime() <= adjustedMaxTime))
							continue;
						if (!DoesMiscEventFallWithinRange(potentialEvent, xStart, xEnd))
							continue;
						newlySelectedEvents.Add(potentialEvent);
					}
				}
			}

			// Select by chart position.
			else
			{
				var (minPosition, maxPosition) = SelectedRegion.GetSelectedChartPositionRange();

				// Select notes.
				if (!selectMiscEvents)
				{
					// Adjust the position to account for the selection preference for how much of an event should be
					// within the selected region.
					var (adjustedMinPosition, adjustedMaxPosition) =
						AdjustSelectionPositionRange(minPosition, maxPosition, halfArrowH);
					if (adjustedMinPosition < adjustedMaxPosition)
					{
						var enumerator = ActiveChart.EditorEvents.FindFirstAfterChartPosition(adjustedMinPosition);
						while (enumerator != null && enumerator.MoveNext())
						{
							if (enumerator.Current!.GetRow() > adjustedMaxPosition)
								break;
							if (!isSelectable(enumerator.Current))
								continue;
							var lane = enumerator.Current.GetLane();
							if (lane < minLane || lane > maxLane)
								continue;
							newlySelectedEvents.Add(enumerator.Current);
						}
					}

					// If nothing was selected and the selection was partially outside of the lanes, treat it as
					// an attempt to select misc events.
					if (newlySelectedEvents.Count == 0 && partiallyOutsideLanes)
						selectMiscEvents = true;
				}

				// Select misc. events.
				if (selectMiscEvents)
				{
					// Adjust the position to account for the selection preference for how much of an event should be
					// within the selected region.
					var (adjustedMinPosition, adjustedMaxPosition) =
						AdjustSelectionPositionRange(minPosition, maxPosition, halfMiscEventH);

					// Collect potential misc events.
					var potentialEvents = new List<EditorEvent>();

					// Loop over the misc events in the selected time range and determine their positions.
					BeginMiscEventWidgetLayoutManagerFrame();
					var enumerator = ActiveChart.MiscEvents.FindBestByPosition(adjustedMinPosition);
					while (enumerator != null && enumerator.MoveNext())
					{
						var miscEvent = enumerator.Current;
						if (miscEvent!.GetRow() > adjustedMaxPosition)
							break;
						if (!miscEvent.IsSelectableWithModifiers())
							continue;
						MiscEventWidgetLayoutManager.PositionEvent(miscEvent);
						potentialEvents.Add(miscEvent);
					}

					// Now that we know the x positions of the potential misc events, check each
					// event to see if it overlaps the selected region and add it to newlySelectedEvents.
					var (xStart, xEnd) = SelectedRegion.GetSelectedXRange();
					foreach (var potentialEvent in potentialEvents)
					{
						if (!(potentialEvent.GetRow() >= adjustedMinPosition && potentialEvent.GetRow() <= adjustedMaxPosition))
							continue;
						if (!DoesMiscEventFallWithinRange(potentialEvent, xStart, xEnd))
							continue;
						newlySelectedEvents.Add(potentialEvent);
					}
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
					var checkLane = Preferences.Instance.PreferencesSelection.RegionMode ==
					                PreferencesSelection.SelectionRegionMode.TimeOrPositionAndLane;
					var minLane = Math.Min(newlySelectedEvents[0].GetLane(),
						Math.Min(LastSelectedEvent.GetLane(), newlySelectedEvents[^1].GetLane()));
					var maxLane = Math.Max(newlySelectedEvents[0].GetLane(),
						Math.Max(LastSelectedEvent.GetLane(), newlySelectedEvents[^1].GetLane()));

					while (enumerator.MoveNext())
					{
						var last = enumerator.Current == end;
						if (isSelectable(enumerator.Current))
						{
							if (!checkLane ||
							    (enumerator.Current!.GetLane() >= minLane && enumerator.Current.GetLane() <= maxLane))
							{
								SelectEvent(enumerator.Current, last);
							}
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
					DeselectEvent(newlySelectedEvents[i]);
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
	/// Gets the min and max lanes encompassed by the SelectedRegion based on the current selection preferences.
	/// </summary>
	/// <returns>Min and max lanes from the SelectedRegion.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private (int, int) GetSelectedLanes()
	{
		var (arrowWidthUnscaled, _) = GetArrowDimensions(false);
		var lanesWidth = ActiveChart.NumInputs * arrowWidthUnscaled;
		var (minChartX, maxChartX) = SelectedRegion.GetSelectedXChartSpaceRange();

		// Determine the min and max lanes to consider for selection based on the preference for how notes should be considered.
		int minLane, maxLane;
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				minLane = (int)Math.Floor((minChartX + lanesWidth * 0.5) / arrowWidthUnscaled);
				maxLane = (int)Math.Floor((maxChartX + lanesWidth * 0.5) / arrowWidthUnscaled);
				break;
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				minLane = (int)Math.Floor((minChartX + lanesWidth * 0.5 + arrowWidthUnscaled * 0.5) / arrowWidthUnscaled);
				maxLane = (int)Math.Floor((maxChartX + lanesWidth * 0.5 - arrowWidthUnscaled * 0.5) / arrowWidthUnscaled);
				break;
			case PreferencesSelection.SelectionMode.OverlapAll:
				minLane = (int)Math.Ceiling((minChartX + lanesWidth * 0.5) / arrowWidthUnscaled);
				maxLane = (int)Math.Floor((maxChartX + lanesWidth * 0.5) / arrowWidthUnscaled) - 1;
				break;
		}

		return (minLane, maxLane);
	}

	/// <summary>
	/// Given a time range defined by the given min and max time, returns an adjusted min and max time that
	/// are expanded by the given distance value. The given distance value is typically half the height of
	/// an event that should be captured by a selection, like half of an arrow height or half of a misc.
	/// event height.
	/// </summary>
	/// <returns>Adjusted min and max time.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private (double, double) AdjustSelectionTimeRange(double minTime, double maxTime, double halfHeight)
	{
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				// This is an approximation as there may be rate altering events during the range.
				return (minTime - GetTimeRangeOfYPixelDurationAtTime(minTime, halfHeight),
					maxTime + GetTimeRangeOfYPixelDurationAtTime(maxTime, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapAll:
				// This is an approximation as there may be rate altering events during the range.
				return (minTime + GetTimeRangeOfYPixelDurationAtTime(minTime, halfHeight),
					maxTime - GetTimeRangeOfYPixelDurationAtTime(maxTime, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				return (minTime, maxTime);
		}
	}

	/// <summary>
	/// Given a position range defined by the given min and max position, returns an adjusted min and max
	/// position that are expanded by the given distance value. The given distance value is typically half
	/// the height of an event that should be captured by a selection, like half of an arrow height or half
	/// of a misc. event height.
	/// </summary>
	/// <returns>Adjusted min and max position.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private (double, double) AdjustSelectionPositionRange(double minPosition, double maxPosition, double halfHeight)
	{
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				// This is an approximation as there may be rate altering events during the range.
				return (minPosition - GetPositionRangeOfYPixelDurationAtPosition(minPosition, halfHeight),
					maxPosition + GetPositionRangeOfYPixelDurationAtPosition(maxPosition, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapAll:
				// This is an approximation as there may be rate altering events during the range.
				return (minPosition + GetPositionRangeOfYPixelDurationAtPosition(minPosition, halfHeight),
					maxPosition - GetPositionRangeOfYPixelDurationAtPosition(maxPosition, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				return (minPosition, maxPosition);
		}
	}

	/// <summary>
	/// Returns whether the given EditorEvent is eligible to be selected based on its x values by checking
	/// if the range defined by it falls within the given start and end x values, taking into account the
	/// current selection preferences.
	/// </summary>
	/// <returns>Whether the given EditorEvent falls within the given range.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private bool DoesMiscEventFallWithinRange(EditorEvent editorEvent, double xStart, double xEnd)
	{
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				return editorEvent.X <= xEnd && editorEvent.X + editorEvent.W >= xStart;
			case PreferencesSelection.SelectionMode.OverlapAll:
				return editorEvent.X >= xStart && editorEvent.X + editorEvent.W <= xEnd;
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				return editorEvent.X + editorEvent.W * 0.5 >= xStart && editorEvent.X + editorEvent.W * 0.5 <= xEnd;
		}
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
		var rae = ActiveChart.FindActiveRateAlteringEventForTime(time);
		var spacingHelper = EventSpacingHelper.GetSpacingHelper(ActiveChart);
		spacingHelper.UpdatePpsAndPpr(rae, GetCurrentInterpolatedScrollRate(), ZoomManager.GetSpacingZoom());

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
	private double GetPositionRangeOfYPixelDurationAtPosition(double position, double duration)
	{
		var rae = ActiveChart.FindActiveRateAlteringEventForPosition(position);
		var spacingHelper = EventSpacingHelper.GetSpacingHelper(ActiveChart);
		spacingHelper.UpdatePpsAndPpr(rae, GetCurrentInterpolatedScrollRate(), ZoomManager.GetSpacingZoom());

		return duration / spacingHelper.GetPpr();
	}

	private void OnShiftSelectedNotesLeft()
	{
		if (EditEarlyOut())
			return;
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(this, ActiveChart, SelectedEvents, false, false));
	}

	private void OnShiftSelectedNotesLeftAndWrap()
	{
		if (EditEarlyOut())
			return;
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(this, ActiveChart, SelectedEvents, false, true));
	}

	private void OnShiftSelectedNotesRight()
	{
		if (EditEarlyOut())
			return;
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(this, ActiveChart, SelectedEvents, true, false));
	}

	private void OnShiftSelectedNotesRightAndWrap()
	{
		if (EditEarlyOut())
			return;
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(this, ActiveChart, SelectedEvents, true, true));
	}

	private void OnShiftSelectedNotesEarlier()
	{
		if (EditEarlyOut())
			return;
		var rows = SnapLevels[SnapIndex].Rows;
		if (rows == 0)
			rows = MaxValidDenominator;
		ActionQueue.Instance.Do(new ActionShiftSelectionRow(this, ActiveChart, SelectedEvents, -rows));
	}

	private void OnShiftSelectedNotesLater()
	{
		if (EditEarlyOut())
			return;
		var rows = SnapLevels[SnapIndex].Rows;
		if (rows == 0)
			rows = MaxValidDenominator;
		ActionQueue.Instance.Do(new ActionShiftSelectionRow(this, ActiveChart, SelectedEvents, rows));
	}

	#endregion Selection

	#region Copy Paste

	private void OnCopy()
	{
		// Do not alter the currently copied events if nothing is selected.
		if (SelectedEvents.Count == 0)
			return;

		CopiedEvents.Clear();
		foreach (var selectedEvent in SelectedEvents)
			CopiedEvents.Add(selectedEvent);
		CopiedEvents.Sort();
	}

	private void OnCut()
	{
		OnCopy();
		OnDelete();
	}

	private void OnPaste()
	{
		if (CopiedEvents.Count == 0)
			return;
		var earliestRow = CopiedEvents[0].GetRow();
		var currentRow = Math.Max(0, Position.GetNearestRow());
		ActionQueue.Instance.Do(new ActionPasteEvents(this, ActiveChart, CopiedEvents, currentRow - earliestRow));
	}

	#endregion Copy Paste

	#region Chart Navigation

	private void OnPositionChanged()
	{
		// Update events being edited.
		UpdateLaneEditStatesFromPosition();

		// Update the music time
		if (!UpdatingSongTimeDirectly)
		{
			var songTime = Position.SongTime;
			Position.SetDesiredPositionToCurrent();
			MusicManager.SetMusicTimeInSeconds(songTime);

			if (Playing)
			{
				PlaybackStartTime = songTime;
				PlaybackStopwatch = new Stopwatch();
				PlaybackStopwatch.Start();
			}
		}
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
			var newChartPosition = (int)Position.ChartPosition / rows * rows;
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
			Position.ChartPosition = Position.ChartPosition / rows * rows + rows;
			UpdateAutoPlayFromScrolling();
		}
	}

	private void OnMoveToPreviousMeasure()
	{
		var rate = ActiveChart?.FindActiveRateAlteringEventForPosition(Position.ChartPosition);
		if (rate == null)
			return;
		var sig = rate.GetTimeSignature().Signature;
		var rows = sig.Numerator * (MaxValidDenominator * NumBeatsPerMeasure / sig.Denominator);
		Position.ChartPosition -= rows;

		UpdateAutoPlayFromScrolling();
	}

	private void OnMoveToNextMeasure()
	{
		var rate = ActiveChart?.FindActiveRateAlteringEventForPosition(Position.ChartPosition);
		if (rate == null)
			return;
		var sig = rate.GetTimeSignature().Signature;
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

	#endregion Chart Navigation

	#region Lane Input

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
		if (EditEarlyOut())
			return;

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
			var snappedRow = row / SnapLevels[SnapIndex].Rows * SnapLevels[SnapIndex].Rows;
			if (row - snappedRow >= SnapLevels[SnapIndex].Rows * 0.5)
				snappedRow += SnapLevels[SnapIndex].Rows;
			row = snappedRow;
		}

		// If there is a tap, mine, or hold start at this location, delete it now.
		// Deleting immediately feels more responsive than deleting on the input up event.
		var existingEvent = ActiveChart.EditorEvents.FindNoteAt(row, lane, true);
		if (existingEvent != null && existingEvent.GetRow() == row)
			LaneEditStates[lane].StartEditingWithDelete(row, new ActionDeleteEditorEvents(existingEvent));

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
			var chartTime = 0.0;
			ActiveChart.TryGetTimeFromChartPosition(row, ref chartTime);

			if (KeyCommandManager.IsKeyDown(Keys.LeftShift))
			{
				var config = EventConfig.CreateMineConfig(ActiveChart, row, chartTime, lane);
				config.IsBeingEdited = true;
				LaneEditStates[lane].SetEditingTapOrMine(EditorEvent.CreateEvent(config));
			}
			else
			{
				var config = EventConfig.CreateTapConfig(ActiveChart, row, chartTime, lane);
				config.IsBeingEdited = true;
				LaneEditStates[lane].SetEditingTapOrMine(EditorEvent.CreateEvent(config));
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
						LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
							new List<EditorAction>
							{
								new ActionDeleteEditorEvents(existingEvent),
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
				else if (existingEvent is EditorHoldNoteEvent hn)
				{
					// If the tap note starts at the beginning of the hold, delete the hold.
					if (row == existingEvent.GetRow())
					{
						LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
							new List<EditorAction>
							{
								new ActionDeleteEditorEvents(existingEvent),
							});
					}

					// If the tap note is in the in the middle of the hold, shorten the hold.
					else
					{
						// Move the hold up by a 16th.
						var newHoldEndRow = row - MaxValidDenominator / 4;

						// If the hold would have a non-positive length, delete it and replace it with a tap.
						if (newHoldEndRow <= existingEvent.GetRow())
						{
							var deleteHold = new ActionDeleteEditorEvents(existingEvent);

							var config = EventConfig.CreateTapConfig(ActiveChart, existingEvent.GetRow(),
								existingEvent.GetChartTime(), lane);
							var insertNewNoteAtHoldStart = new ActionAddEditorEvent(EditorEvent.CreateEvent(config));

							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
								new List<EditorAction>
								{
									deleteHold,
									insertNewNoteAtHoldStart,
								});
						}

						// Otherwise, the new length is valid. Update it.
						else
						{
							var changeLength = new ActionChangeHoldLength(hn, newHoldEndRow - hn.GetRow());
							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
								new List<EditorAction>
								{
									changeLength,
								});
						}
					}
				}
			}
		}

		// Handle a new hold note overlapping any existing notes
		else if (LaneEditStates[lane].GetEventBeingEdited() is EditorHoldNoteEvent editHold)
		{
			var length = editHold.GetLength();
			var roll = editHold.IsRoll();

			// If the hold is completely within another hold, do not add or delete notes, but make sure the outer
			// hold is the same type (hold/roll) as the new type.
			if (existingEvent is EditorHoldNoteEvent holdFull
			    && holdFull.GetRow() <= row
			    && holdFull.GetRow() + holdFull.GetLength() >= row + length)
			{
				LaneEditStates[lane].Clear(true);
				if (holdFull.IsRoll() != roll)
					ActionQueue.Instance.Do(new ActionChangeHoldType(holdFull, roll));
				return;
			}

			var deleteActions = new List<EditorAction>();

			// If existing holds overlap with only the start or end of the new hold, delete them and extend the
			// new hold to cover their range. We just need to extend the new event now. The deletion of the
			// old event will will be handled below when we check for events fully contained within the new
			// hold region.
			if (existingEvent is EditorHoldNoteEvent hsnStart
			    && hsnStart.GetRow() < row
			    && hsnStart.GetEndRow() >= row
			    && hsnStart.GetEndRow() < row + length)
			{
				row = hsnStart.GetRow();
				length = editHold.GetEndRow() - hsnStart.GetRow();
			}

			existingEvent = ActiveChart.EditorEvents.FindNoteAt(row + length, lane, true);
			if (existingEvent is EditorHoldNoteEvent hsnEnd
			    && hsnEnd.GetRow() <= row + length
			    && hsnEnd.GetEndRow() >= row + length
			    && hsnEnd.GetRow() > row)
			{
				length = hsnEnd.GetEndRow() - row;
			}

			// For any event in the same lane within the region of the new hold, delete them.
			var e = ActiveChart.EditorEvents.FindBestByPosition(row);
			if (e != null)
			{
				while (e.MoveNext() && e.Current!.GetRow() <= row + length)
				{
					if (e.Current.GetRow() < row)
						continue;
					if (e.Current.GetLane() != lane)
						continue;
					if (e.Current.IsBeingEdited())
						continue;
					if (e.Current is EditorTapNoteEvent || e.Current is EditorMineNoteEvent)
						deleteActions.Add(new ActionDeleteEditorEvents(e.Current));
					else if (e.Current is EditorHoldNoteEvent innerHold && innerHold.GetEndRow() <= row + length)
						deleteActions.Add(new ActionDeleteEditorEvents(innerHold));
				}
			}

			// Set the state to be editing a new hold after running the delete actions.
			LaneEditStates[lane].SetEditingHold(ActiveChart, lane, row, LaneEditStates[lane].GetStartingRow(), length, roll,
				deleteActions);
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
				if (laneEditState.GetEventBeingEdited() is EditorHoldNoteEvent)
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
				    || (laneEditState.GetEventBeingEdited() is EditorHoldNoteEvent h
				        && (holdStartRow != h.GetRow() || holdEndRow != h.GetEndRow())))
				{
					var roll = KeyCommandManager.IsKeyDown(Keys.LeftShift);
					LaneEditStates[lane].SetEditingHold(ActiveChart, lane, holdStartRow, laneEditState.GetStartingRow(),
						holdEndRow - holdStartRow, roll);
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

	#endregion Lane Input

	#region Undo

	private void OnUndoHistorySizeChanged()
	{
		ActionQueue.Instance.Resize(Preferences.Instance.PreferencesOptions.UndoHistorySize);
	}

	private void OnUndo()
	{
		// Not all undoable actions affect the song / chart but for simplicity it is easier to
		// prevent all undos when the song / chart is locked for edits.
		if (EditEarlyOut())
			return;
		ActionQueue.Instance.Undo();
	}

	private void OnRedo()
	{
		// Not all undoable actions affect the song / chart but for simplicity it is easier to
		// prevent all undos when the song / chart is locked for edits.
		if (EditEarlyOut())
			return;
		ActionQueue.Instance.Redo();
	}

	#endregion Undo

	#region Song Media Files

	private void OnOpenAudioFile(string audioFile)
	{
		if (ActiveSong != null)
		{
			var relativePath = Path.GetRelativePath(ActiveSong.GetFileDirectory(), audioFile);
			UpdateMusicPath(relativePath);
		}
		else
		{
			PendingMusicFile = audioFile;
			OnNew();
		}
	}

	private void OnOpenImageFile(string imageFile)
	{
		if (ActiveSong != null)
		{
			var relativePath = Path.GetRelativePath(ActiveSong.GetFileDirectory(), imageFile);
			UpdateSongImage(GetBestSongImageType(imageFile), relativePath);
		}
		else
		{
			PendingImageFile = imageFile;
			OnNew();
		}
	}

	private void OnOpenVideoFile(string videoFile)
	{
		if (ActiveSong != null)
		{
			var relativePath = Path.GetRelativePath(ActiveSong.GetFileDirectory(), videoFile);
			UpdateSongImage(SongImageType.Background, relativePath);
		}
		else
		{
			PendingVideoFile = videoFile;
			OnNew();
		}
	}

	public void UpdateSongImage(SongImageType imageType, string path)
	{
		if (ActiveSong == null || path == null)
			return;
		switch (imageType)
		{
			case SongImageType.Background:
				if (path == ActiveSong.BackgroundPath)
					return;
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong,
					nameof(EditorSong.BackgroundPath), path, true));
				break;
			case SongImageType.Banner:
				if (path == ActiveSong.BannerPath)
					return;
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong, nameof(EditorSong.BannerPath), path, true));
				break;
			case SongImageType.Jacket:
				if (path == ActiveSong.JacketPath)
					return;
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong, nameof(EditorSong.JacketPath), path, true));
				break;
			case SongImageType.CDImage:
				if (path == ActiveSong.CDImagePath)
					return;
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong, nameof(EditorSong.CDImagePath), path, true));
				break;
			case SongImageType.DiscImage:
				if (path == ActiveSong.DiscImagePath)
					return;
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong,
					nameof(EditorSong.DiscImagePath), path, true));
				break;
			case SongImageType.CDTitle:
				if (path == ActiveSong.CDTitlePath)
					return;
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong, nameof(EditorSong.CDTitlePath), path, true));
				break;
		}
	}

	public void UpdateMusicPath(string musicPath)
	{
		if (ActiveSong == null || musicPath == null || musicPath == ActiveSong.MusicPath)
			return;
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong, nameof(EditorSong.MusicPath), musicPath, true));
	}

	private void OnOpenLyricsFile(string lyricsFile)
	{
		if (ActiveSong != null)
		{
			var relativePath = Path.GetRelativePath(ActiveSong.GetFileDirectory(), lyricsFile);
			UpdateLyricsPath(relativePath);
		}
		else
		{
			PendingLyricsFile = lyricsFile;
			OnNew();
		}
	}

	public void UpdateLyricsPath(string lyricsPath)
	{
		if (ActiveSong == null || lyricsPath == null || lyricsPath == ActiveSong.LyricsPath)
			return;
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyReference<string>(ActiveSong, nameof(EditorSong.LyricsPath), lyricsPath, true));
	}

	#endregion Song Media Files

	#region Chart Selection

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

		ClearSelectedEvents();
		ActiveChart = chart;

		// The Position needs to know about the active chart for doing time and row calculations.
		Position.ActiveChart = ActiveChart;
		EditorMouseState.SetActiveChart(ActiveChart);

		if (ActiveChart != null)
		{
			// Update the recent file entry for the current song so that tracks the selected chart
			var p = Preferences.Instance;
			if (p.RecentFiles.Count > 0 && p.RecentFiles[0].FileName == ActiveSong.GetFileFullPath())
			{
				p.RecentFiles[0].UpdateChart(ActiveChart.ChartType, ActiveChart.ChartDifficultyType);
			}

			// The receptors and arrow graphics depend on the active chart.
			ArrowGraphicManager = ArrowGraphicManager.CreateArrowGraphicManager(ActiveChart.ChartType);
			var laneEditStates = new LaneEditState[ActiveChart.NumInputs];
			var receptors = new Receptor[ActiveChart.NumInputs];
			for (var i = 0; i < ActiveChart.NumInputs; i++)
			{
				laneEditStates[i] = new LaneEditState();
				receptors[i] = new Receptor(i, ArrowGraphicManager, ActiveChart);
			}

			Receptors = receptors;
			AutoPlayer = new AutoPlayer(ActiveChart, Receptors);
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
		OnMusicChanged();
		OnMusicPreviewChanged();
	}

	public EditorChart AddChart(ChartType chartType, bool selectNewChart)
	{
		if (ActiveSong == null)
			return null;
		var chart = ActiveSong.AddChart(chartType, this);
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
		{
			OnChartSelected(chartToSelect, false);
		}
		else if (ActiveChart == chart)
		{
			var newActiveChart = SelectBestChart(ActiveSong, ActiveChart.ChartType, ActiveChart.ChartDifficultyType);
			OnChartSelected(newActiveChart, false);
		}
	}

	#endregion Chart Selection

	#region Drag and Drop

	/// <summary>
	/// Called when dragging a file into the window.
	/// </summary>
	public void DragEnter(object sender, DragEventArgs e)
	{
		if (e.Data == null)
			return;
		// The application only supports opening one file at a time.
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
			return;
		var files = (string[])e.Data.GetData(DataFormats.FileDrop);
		if (files.Length != 1)
		{
			e.Effect = DragDropEffects.None;
			return;
		}

		var file = files[0];

		// Get the extension to determine if the file type is supported.
		if (!Path.GetExtensionWithoutSeparator(file, out var extension))
		{
			e.Effect = DragDropEffects.None;
			return;
		}

		// Set the effect for the drop based on if the file type is supported.
		if (IsExtensionSupportedForFileDrop(extension))
			e.Effect = DragDropEffects.Copy;
		else
			e.Effect = DragDropEffects.None;
	}

	/// <summary>
	/// Called when dropping a file into the window.
	/// </summary>
	public void DragDrop(object sender, DragEventArgs e)
	{
		if (e.Data == null)
			return;
		// The application only supports opening one file at a time.
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
			return;
		var files = (string[])e.Data.GetData(DataFormats.FileDrop);
		if (files.Length != 1)
			return;
		var file = files[0];

		// Get the extension to determine if the file type is supported.
		if (!Path.GetExtensionWithoutSeparator(file, out var extension))
			return;

		// Based on the type, open the file.
		if (IsSongExtensionSupported(extension))
			OnOpenFile(file);
		else if (ExpectedAudioFormats.Contains(extension))
			OnOpenAudioFile(file);
		else if (ExpectedImageFormats.Contains(extension))
			OnOpenImageFile(file);
		else if (ExpectedVideoFormats.Contains(extension))
			OnOpenVideoFile(file);
		else if (ExpectedLyricsFormats.Contains(extension))
			OnOpenLyricsFile(file);
	}

	/// <summary>
	/// Returns whether or not a file with the given extension is supported for
	/// opening via drag and drop.
	/// </summary>
	/// <param name="extension">Extension without separator.</param>
	/// <returns>Whether or not the file is supported for opening via drag and drop.</returns>
	private static bool IsExtensionSupportedForFileDrop(string extension)
	{
		if (IsSongExtensionSupported(extension))
			return true;
		if (ExpectedAudioFormats.Contains(extension))
			return true;
		if (ExpectedImageFormats.Contains(extension))
			return true;
		if (ExpectedVideoFormats.Contains(extension))
			return true;
		if (ExpectedLyricsFormats.Contains(extension))
			return true;
		return false;
	}

	/// <summary>
	/// Returns whether or not a file with the given extension is a supported song file.
	/// </summary>
	/// <param name="extension">Extension without separator.</param>
	/// <returns>Whether or not the file is supported.</returns>
	private static bool IsSongExtensionSupported(string extension)
	{
		// sm and ssc files are supported.
		var fileFormat = FileFormat.GetFileFormatByExtension(extension);
		if (fileFormat != null && (fileFormat.Type == FileFormatType.SM || fileFormat.Type == FileFormatType.SSC))
			return true;
		return false;
	}

	#endregion Drag and Drop

	#region IObserver

	public void OnNotify(string eventId, EditorSong song, object payload)
	{
		if (song != ActiveSong)
			return;

		switch (eventId)
		{
			case EditorSong.NotificationCanEditChanged:
				OnCanEditChanged();
				break;
			case EditorSong.NotificationMusicChanged:
				OnMusicChanged();
				break;
			case EditorSong.NotificationMusicPreviewChanged:
				OnMusicPreviewChanged();
				break;
			case EditorSong.NotificationMusicOffsetChanged:
				OnMusicOffsetChanged();
				break;
			case EditorSong.NotificationSyncOffsetChanged:
				OnSyncOffsetChanged();
				break;
		}
	}

	public void OnNotify(string eventId, EditorChart chart, object payload)
	{
		if (chart != ActiveChart)
			return;

		switch (eventId)
		{
			case EditorChart.NotificationCanEditChanged:
				OnCanEditChanged();
				break;
			case EditorChart.NotificationDifficultyTypeChanged:
			case EditorChart.NotificationRatingChanged:
			case EditorChart.NotificationNameChanged:
			case EditorChart.NotificationDescriptionChanged:
				chart.GetEditorSong().UpdateChartSort();
				break;

			case EditorChart.NotificationMusicChanged:
				OnMusicChanged();
				break;
			case EditorChart.NotificationMusicOffsetChanged:
				OnMusicOffsetChanged();
				break;
			case EditorChart.NotificationEventsDeleted:
				OnEventsDeleted((List<EditorEvent>)payload);
				break;
		}
	}

	public void OnNotify(string eventId, PreferencesOptions options, object payload)
	{
		switch (eventId)
		{
			case PreferencesOptions.NotificationAudioOffsetChanged:
				OnAudioOffsetChanged();
				break;
			case PreferencesOptions.NotificationVolumeChanged:
				OnVolumeChanged();
				break;
			case PreferencesOptions.NotificationUndoHistorySizeChanged:
				OnUndoHistorySizeChanged();
				break;
		}
	}

	public void OnNotify(string eventId, PreferencesExpressedChartConfig options, object payload)
	{
		switch (eventId)
		{
			case PreferencesExpressedChartConfig.NotificationConfigRename:
				var payloadNames = (Tuple<string, string>)payload;
				OnExpressedChartConfigNameChanged(payloadNames.Item1, payloadNames.Item2);
				break;
		}
	}

	public void OnNotify(string eventId, ActionQueue actionQueue, object payload)
	{
		switch (eventId)
		{
			case ActionQueue.NotificationAsyncActionStarted:
				OnCanEditChanged();
				break;
		}
	}

	#endregion IObserver

	private void OnExpressedChartConfigNameChanged(string oldName, string newName)
	{
		// When an Expressed Chart Config name has changed, update all loaded Charts directly without enqueueing actions.
		// We don't want to introduce unintended actions to be undone / redone. Undoing changing an Expressed Chart Config
		// name will result in this notification being triggered again.
		if (ActiveSong != null)
		{
			foreach (var chart in ActiveSong.GetCharts())
			{
				if (chart.ExpressedChartConfig.Equals(oldName))
					chart.ExpressedChartConfig = newName;
			}
		}
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
}
