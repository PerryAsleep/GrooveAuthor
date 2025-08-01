﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameExtensions;
using StepManiaEditor.AutogenConfig;
using StepManiaEditor.UI;
using StepManiaLibrary;
using StepManiaLibrary.PerformedChart;
using static StepManiaEditor.Utils;
using static StepManiaEditor.ImGuiUtils;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.EditorSongImageUtils;
using static StepManiaEditor.MiniMap;
using Color = Microsoft.Xna.Framework.Color;
using Path = Fumen.Path;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

[assembly: InternalsVisibleTo("StepManiaEditorTests")]

namespace StepManiaEditor;

/// <summary>
/// The Editor.
/// Implemented as a MonoGame Game.
/// </summary>
public sealed class Editor :
	Game,
	Fumen.IObserver<EditorSong>,
	Fumen.IObserver<EditorChart>,
	Fumen.IObserver<PreferencesOptions>,
	Fumen.IObserver<PreferencesAudio>,
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
	/// Modes for entering new notes.
	/// </summary>
	public enum NoteEntryMode
	{
		/// <summary>
		/// Normal behavior.
		/// </summary>
		Normal,

		/// <summary>
		/// When entering a note the position will advance based on the current snap level.
		/// This prevents entering holds, but is convenient for entering stream.
		/// </summary>
		AdvanceBySnap,
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

	/// <summary>
	/// Enumeration for feet.
	/// </summary>
	public enum Foot
	{
		Left,
		Right,
	}

	private readonly EditorMouseState EditorMouseState = new();
	private bool CanShowRightClickPopupThisFrame;

	private bool MovingFocalPoint;
	private bool FocalPointMoved;
	private bool LastMouseUpEventWasUsedForMovingFocalPoint;
	private bool ForceOnlyHorizontalFocalPointMove;
	private bool ForceOnlyVerticalFocalPointMove;
	private Vector2 FocalPointAtMoveStart;
	private Vector2 FocalPointMoveOffset;

	private bool UnsavedChangesLastFrame;
	private string PendingOpenSongFileName;
	private string PendingMusicFile;
	private string PendingImageFile;
	private string PendingVideoFile;
	private string PendingLyricsFile;
	private Action PostSaveFunction;
	private int OpenRecentIndex;
	private bool HasCheckedForAutoLoadingLastSong;
	private bool HasPreparedDeviceSettingsForMonoGame;

	public static readonly ChartType[] SupportedSinglePlayerChartTypes =
	[
		ChartType.dance_single,
		ChartType.dance_double,
		ChartType.dance_solo,
		ChartType.dance_threepanel,
		ChartType.pump_single,
		ChartType.pump_halfdouble,
		ChartType.pump_double,
		ChartType.smx_beginner,
		ChartType.smx_single,
		ChartType.smx_dual,
		ChartType.smx_full,
	];

	public static readonly ChartType[] SupportedChartTypes =
	[
		ChartType.dance_single,
		ChartType.dance_double,
		//dance_couple,
		ChartType.dance_routine,
		ChartType.dance_solo,
		ChartType.dance_threepanel,
		ChartType.pump_single,
		ChartType.pump_halfdouble,
		ChartType.pump_double,
		//pump_couple,
		ChartType.pump_routine,
		ChartType.smx_beginner,
		ChartType.smx_single,
		ChartType.smx_dual,
		ChartType.smx_full,
		ChartType.smx_team,
	];

	private static readonly int MaxNumLanesForAnySupportedChartType;

	private readonly IEditorPlatform PlatformInterface;

	private GraphicsDeviceManager Graphics;
	private SpriteBatch SpriteBatch;
	private ImGuiRenderer ImGuiRenderer;
	private WaveFormRenderer WaveFormRenderer;
	private uint WaveFormTextureHeight;
	private SoundManager SoundManager;
	private MusicManager MusicManager;
	private MiniMap MiniMap;
	private SnapManager SnapManager;

	// UI
	private UIEditEvents UIEditEvents;
	private UIFTUE UIFTUE;

	private readonly UIPatternComparer PatternComparer = new();
	private readonly UIPerformedChartComparer PerformedChartComparer = new();

	private ZoomManager ZoomManager;
	private StaticTextureAtlas TextureAtlas;
	private IntPtr TextureAtlasImGuiTexture;

	private Effect FxaaEffect;
	private Effect WaveformColorEffect;
	private RenderTarget2D WaveformRenderTarget;
	private RenderTarget2D MiniMapRenderTarget;
	private StepDensityEffect DensityGraph;

	private SongLoadTask SongLoadTask;
	private FileSystemWatcher SongFileWatcher;
	private bool ShowingSongFileChangedNotification;
	private bool HasShownUpdateModal;
	private int GarbageCollectFrame;
	private long FrameCount;

	private readonly Dictionary<ChartType, PadData> PadDataByChartType = new();
	private readonly Dictionary<ChartType, StepGraph> StepGraphByChartType = new();
	private Dictionary<ChartType, List<List<GraphNode>>> RootNodesByChartType = new();
	private StepTypeFallbacks StepTypeFallbacks;

	private double PlaybackStartTime;
	private Stopwatch PlaybackStopwatch;

	private bool IsChartAreaSet;
	private Rectangle ChartArea;

	private string LaunchFile;
	private bool OpeningSong;
	private EditorPack ActivePack;

	private EditorSong ActiveSong
	{
		set
		{
			Debug.Assert(IsOnMainThread());
			ActiveSongInternal?.Dispose();
			ActiveSongInternal = value;
		}
		get
		{
			Debug.Assert(IsOnMainThread());
			return ActiveSongInternal;
		}
	}

	private EditorSong ActiveSongInternal;

	private EditorChart FocusedChart
	{
		set
		{
			Debug.Assert(IsOnMainThread());
			FocusedChartInternal = value;
		}
		get
		{
			Debug.Assert(IsOnMainThread());
			return FocusedChartInternal;
		}
	}

	private EditorChart FocusedChartInternal;

	private ActiveEditorChart FocusedChartData
	{
		set
		{
			Debug.Assert(IsOnMainThread());
			FocusedChartDataInternal = value;
		}
		get
		{
			Debug.Assert(IsOnMainThread());
			return FocusedChartDataInternal;
		}
	}

	private ActiveEditorChart FocusedChartDataInternal;

	private readonly List<EditorChart> ActiveCharts = [];
	private readonly List<ActiveEditorChart> ActiveChartData = [];
	private readonly List<EditorEvent> CopiedEvents = [];
	private readonly Dictionary<EditorChart, int> PlayerPerChart = new();

	// Movement controls.
	private bool UpdatingSongTimeDirectly;
	private double? LastKnownSongTime;

	private KeyCommandManager KeyCommandManager;
	private bool Playing;
	private bool PlayingPreview;
	private bool MiniMapCapturingMouse;
	private bool DensityGraphCapturingMouse;
	private bool StartPlayingWhenMouseScrollingDone;
	private bool TransformingSelectedNotes;

	private string FormTitle;

	// Fonts
	private ImFontPtr ImGuiFont;
	private SpriteFont Font;

	// Cursor. Intentionally null to avoid invoking static initializers on SDL mouse
	// classes before SDL is initialized.
	private MouseCursor CurrentDesiredCursor;
	private MouseCursor PreviousDesiredCursor;

	// Performance Monitoring
	private PerformanceMonitor PerformanceMonitor;

	// Debug
	private bool ShowImGuiTestWindow;
	private readonly int MainThreadId;

	// Logger
	private string LogFilePath;
	private readonly LinkedList<Logger.LogMessage> LogBuffer = [];
	private readonly object LogBufferLock = new();

	// Splash
	private const double TotalSplashTime = 1.75;

	// ReSharper disable once MemberInitializerValueIgnored
	private double SplashTime = TotalSplashTime;
	private Texture2D SplashBackground;
	private Texture2D LogoAttribution;

	#region Initialization

	/// <summary>
	/// Static initializer.
	/// </summary>
	static Editor()
	{
		// Set MaxNumLanesForAnySupportedChartType.
		MaxNumLanesForAnySupportedChartType = 0;
		foreach (var chartType in SupportedChartTypes)
		{
			MaxNumLanesForAnySupportedChartType = Math.Max(
				GetChartProperties(chartType).GetNumInputs(),
				MaxNumLanesForAnySupportedChartType);
		}
	}

	/// <summary>
	/// Constructor.
	/// </summary>
	public Editor(string[] args, IEditorPlatform platformInterface)
	{
		PlatformInterface = platformInterface;
		PlatformInterface.Initialize(this);

		// Record main thread id.
		MainThreadId = Environment.CurrentManagedThreadId;

		InitializeCulture();

		// Create a logger first so we can log any startup messages.
		InitializeLogger();
		InitializePreferences();
		InitializeImGuiUtils();
		InitializeAutogenConfigsAsync();
		InitializeZoomManager();
		InitializeSoundManager();
		InitializeMusicManager();
		InitializeGraphics();
		InitializeContentManager();
		InitializeMouseVisibility();
		InitializeWindowResizing();
		InitializeVSync();
		InitializeSnapManager();
		InitializeObservers();
		InitializePerformanceMonitor();

		// Update the window title immediately.
		UpdateWindowTitle();

		// Record requested file to open.
		if (args != null && args.Length > 0)
		{
			LaunchFile = args[0];
			if (args.Length > 1)
			{
				Logger.Warn(
					$"{args.Length} launch arguments provided but only the first will be used. Full argument list: {string.Join(", ", args)}");
			}
		}

#if DEBUG
		// Disable the splash screen on debug builds.
		// It is required to show it on Release builds as it includes the FMOD logo which is required to be
		// shown on a splash screen under the FMOD EULA.
		SplashTime = 0.0f;
#endif
	}

	private void PreparingDeviceSettings(object graphicsDeviceInformation, PreparingDeviceSettingsEventArgs args)
	{
		if (HasPreparedDeviceSettingsForMonoGame)
			return;
		HasPreparedDeviceSettingsForMonoGame = true;
		var p = Preferences.Instance;
		args.GraphicsDeviceInformation.GraphicsProfile = GraphicsProfile.HiDef;
		args.GraphicsDeviceInformation.PresentationParameters.IsFullScreen = false;
		args.GraphicsDeviceInformation.PresentationParameters.IsMaximized = p.WindowMaximized;
		args.GraphicsDeviceInformation.PresentationParameters.BackBufferWidth = p.ViewportWidth;
		args.GraphicsDeviceInformation.PresentationParameters.BackBufferHeight = p.ViewportHeight;
	}

	/// <summary>
	/// Override of MonoGame Game Initialize method.
	/// From MonoGame:
	///  Override this to initialize the game and load any needed non-graphical resources.
	///  Initializes attached GameComponent instances and calls LoadContent.
	/// </summary>
	protected override void Initialize()
	{
		InitializeDragDrop();
		InitializeImGui();
		InitializeFonts();
		InitializeGuiDpiScale();
		InitializeScreenHeight();
		InitializeWaveFormRenderer();
		InitializeDensityGraph();
		InitializeMiniMap();
		InitializeEditorPack();
		InitializeUIHelpers();
		InitializeKeyCommandManager();
		InitializeDocumentation();
		InitializeSongLoadTask();
		_ = RefreshLatestVersion();
		base.Initialize();
	}

	private void InitializeCulture()
	{
		// Default the application culture to the invariant culture to ensure consistent parsing in all file I/O.
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
	}

	private void InitializeLogger()
	{
		var canLogToFile = true;
		var logToFileError = "";
		var logDirectory = PlatformInterface.GetLogsDirectory();
		try
		{
			// Make a log directory if one doesn't exist.
			Directory.CreateDirectory(logDirectory);

			// Start the logger and write to disk as well as the internal buffer to display.
			var appName = GetAppName();
			var logFileName = appName + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".log";
			LogFilePath = Path.Combine(logDirectory, logFileName);
			Logger.StartUp(new Logger.Config
			{
				WriteToConsole = false,

				WriteToFile = true,
				LogFilePath = LogFilePath,
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

	private void InitializePreferences()
	{
		// Set default preference values which are only known at runtime.
		// Set the default waveform load parallelism to a little under the logical processor count.
		// When starting up there are a lot of loads occurring asynchronously.
		var defaultWaveformLoadParallelism = Math.Max(2, Environment.ProcessorCount - 6);
		PreferencesWaveForm.InitializeRuntimeDefaults(defaultWaveformLoadParallelism);

		// Load Preferences synchronously so they can be used immediately.
		Preferences.Load(this, PlatformInterface.GetPreferencesSaveFileName());
	}

	private void InitializeDragDrop()
	{
		AllowDragDrop(true);
		Window.FileDrop += OnDragDrop;
	}

	private void InitializeImGuiUtils()
	{
		Init(this);
	}

	private void InitializeAutogenConfigsAsync()
	{
		var configDirectory = PlatformInterface.GetAutogenConfigsDirectory();
		PerformedChartConfigManager.Instance.SetConfigDirectory(configDirectory);
		ExpressedChartConfigManager.Instance.SetConfigDirectory(configDirectory);
		PatternConfigManager.Instance.SetConfigDirectory(configDirectory);

		PatternConfigManager.Instance.SetConfigComparer(PatternComparer);
		PerformedChartConfigManager.Instance.SetConfigComparer(PerformedChartComparer);

		// Load autogen configs asynchronously.
		PerformedChartConfigManager.Instance.LoadConfigsAsync();
		ExpressedChartConfigManager.Instance.LoadConfigsAsync();
		PatternConfigManager.Instance.LoadConfigsAsync();
	}

	private void InitializeZoomManager()
	{
		ZoomManager = new ZoomManager();
	}

	private void InitializeSoundManager()
	{
		var p = Preferences.Instance.PreferencesAudio;
		SoundManager = new SoundManager((uint)p.DspBufferSize, p.DspNumBuffers);
	}

	private void InitializeMusicManager()
	{
		var p = Preferences.Instance.PreferencesAudio;
		MusicManager = new MusicManager(SoundManager, p.AudioOffset, PlatformInterface.GetResourceDirectory());
		MusicManager.SetMusicRate(p.MusicRate);
		MusicManager.SetMainVolume(p.MainVolume);
		MusicManager.SetMusicVolume(p.MusicVolume);
		MusicManager.SetAssistTickVolume(p.AssistTickVolume);
		MusicManager.SetAssistTickAttackTime(p.AssistTickAttackTime);
		MusicManager.SetUseAssistTick(p.UseAssistTick);
		MusicManager.SetSkipAssistTicksOnBeatTicks(p.SkipAssistTickOnBeatTick);
		MusicManager.SetBeatTickVolume(p.BeatTickVolume);
		MusicManager.SetBeatTickAttackTime(p.BeatTickAttackTime);
		MusicManager.SetUseBeatTick(p.UseBeatTick);
		MusicManager.SetSkipBeatTicksOnAssistTicks(p.SkipBeatTickOnAssistTick);
	}

	private void InitializeGraphics()
	{
		Graphics = new GraphicsDeviceManager(this);
		Graphics.GraphicsProfile = GraphicsProfile.HiDef;
		Graphics.PreparingDeviceSettings += PreparingDeviceSettings;
	}

	private void InitializeKeyCommandManager()
	{
		KeyCommandManager = new KeyCommandManager();
		UIKeyRebindModal.Instance.SetKeyCommandManager(KeyCommandManager);
		UIControls.Instance.Initialize(KeyCommandManager);

		// @formatter:off
		const string fileIo = "File I/O";
		AddKeyCommand(fileIo, "Open", nameof(PreferencesKeyBinds.Open), OnOpen);
		AddKeyCommand(fileIo, "Open Containing Folder", nameof(PreferencesKeyBinds.OpenContainingFolder), OnOpenContainingFolder);
		AddKeyCommand(fileIo, "Save As", nameof(PreferencesKeyBinds.SaveAs), OnSaveAs);
		AddKeyCommand(fileIo, "Save", nameof(PreferencesKeyBinds.Save), OnSave);
		AddKeyCommand(fileIo, "Save Pack", nameof(PreferencesKeyBinds.SavePackFile), () => { ActivePack?.SaveItgManiaPack(false); } );
		AddKeyCommand(fileIo, "New", nameof(PreferencesKeyBinds.New), OnNew);
		AddKeyCommand(fileIo, "Reload", nameof(PreferencesKeyBinds.Reload), OnReload);
		AddKeyCommand(fileIo, "Close", nameof(PreferencesKeyBinds.Close), OnClose);

		const string undo = "Undo";
		AddKeyCommand(undo, "Undo", nameof(PreferencesKeyBinds.Undo), OnUndo, true);
		AddKeyCommand(undo, "Redo", nameof(PreferencesKeyBinds.Redo), OnRedo, true);

		const string selection = "Selection";
		UIControls.Instance.AddStaticCommand(selection, "Select Note", "Left Mouse Button");
		UIControls.Instance.AddStaticCommand(selection, "Select Region", "Drag Left Mouse Button");
		UIControls.Instance.AddCommand(selection, "Select Misc. Events In Region", nameof(PreferencesKeyBinds.MouseSelectionAltBehavior), UIControls.MultipleKeysJoinString + "Drag Left Mouse Button");
		UIControls.Instance.AddCommand(selection, "Add to Selection", nameof(PreferencesKeyBinds.MouseSelectionControlBehavior), UIControls.MultipleKeysJoinString + "Left Mouse Button");
		UIControls.Instance.AddCommand(selection, "Extend Selection", nameof(PreferencesKeyBinds.MouseSelectionShiftBehavior), UIControls.MultipleKeysJoinString + "Left Mouse Button");

		AddKeyCommand(selection, "Select Row Range", nameof(PreferencesKeyBinds.SelectRowRange), OnSelectRowRange);
		AddKeyCommand(selection, "Select All Notes", nameof(PreferencesKeyBinds.SelectAllNotes), OnSelectAll);
		AddKeyCommand(selection, "Select All Taps", nameof(PreferencesKeyBinds.SelectAllTaps), () => OnSelectAll(e => e is EditorTapNoteEvent));
		AddKeyCommand(selection, "Select All Mines", nameof(PreferencesKeyBinds.SelectAllMines), () => OnSelectAll(e => e is EditorMineNoteEvent));
		AddKeyCommand(selection, "Select All Fakes", nameof(PreferencesKeyBinds.SelectAllFakes), () => OnSelectAll(e => e is EditorFakeNoteEvent));
		AddKeyCommand(selection, "Select All Lifts", nameof(PreferencesKeyBinds.SelectAllLifts), () => OnSelectAll(e => e is EditorLiftNoteEvent));
		AddKeyCommand(selection, "Select All Holds", nameof(PreferencesKeyBinds.SelectAllHolds), () => OnSelectAll(e => e is EditorHoldNoteEvent hn && !hn.IsRoll()));
		AddKeyCommand(selection, "Select All Rolls", nameof(PreferencesKeyBinds.SelectAllRolls), () => OnSelectAll(e => e is EditorHoldNoteEvent hn && hn.IsRoll()));
		AddKeyCommand(selection, "Select All Holds and Rolls", nameof(PreferencesKeyBinds.SelectAllHoldsAndRolls), () => OnSelectAll(e => e is EditorHoldNoteEvent));
		AddKeyCommand(selection, "Select All Misc. Events", nameof(PreferencesKeyBinds.SelectAllMiscEvents), OnSelectAllAlt);
		AddKeyCommand(selection, "Select All", nameof(PreferencesKeyBinds.SelectAll), OnSelectAllShift);
		AddKeyCommand(selection, "Select All Patterns", nameof(PreferencesKeyBinds.SelectAllPatterns), () => OnSelectAll(e => e is EditorPatternEvent));

		const string copyPaste = "Copy / Paste";
		AddKeyCommand(copyPaste, "Copy", nameof(PreferencesKeyBinds.Copy), OnCopy, true);
		AddKeyCommand(copyPaste, "Cut", nameof(PreferencesKeyBinds.Cut), OnCut, true);
		AddKeyCommand(copyPaste, "Paste", nameof(PreferencesKeyBinds.Paste), OnPaste, true);

		const string song = "Song Shortcuts";
		AddKeyCommand(song, "Automatically Apply All Song Assets", nameof(PreferencesKeyBinds.AutoApplyAllSongAssets), () => ApplyBestSongAssets(false));
		AddKeyCommand(song, "Automatically Apply Unset Song Assets", nameof(PreferencesKeyBinds.AutoApplyUnsetSongAssets), () => ApplyBestSongAssets(true));

		const string sound = "Sound";
		AddKeyCommand(sound, "Toggle Preview", nameof(PreferencesKeyBinds.TogglePreview), OnTogglePlayPreview);
		AddKeyCommand(sound, "Toggle Assist Tick", nameof(PreferencesKeyBinds.ToggleAssistTick), OnToggleAssistTick);
		AddKeyCommand(sound, "Toggle Beat Tick", nameof(PreferencesKeyBinds.ToggleBeatTick), OnToggleBeatTick);
		AddKeyCommand(sound, "Decrease Music Rate", nameof(PreferencesKeyBinds.DecreaseMusicRate), OnDecreaseMusicRate, true);
		AddKeyCommand(sound, "Increase Music Rate", nameof(PreferencesKeyBinds.IncreaseMusicRate), OnIncreaseMusicRate, true);

		const string general = "General";
		AddKeyCommand(general, "Play / Pause", nameof(PreferencesKeyBinds.PlayPause), OnTogglePlayback);
		AddKeyCommand(general, "Cancel / Go Back", nameof(PreferencesKeyBinds.CancelGoBack), OnEscape);
		AddKeyCommand(general, "Toggle Note Entry Mode", nameof(PreferencesKeyBinds.ToggleNoteEntryMode), OnToggleNoteEntryMode);
		AddKeyCommand(general, "Toggle Spacing Mode", nameof(PreferencesKeyBinds.ToggleSpacingMode), OnToggleSpacingMode);
		AddKeyCommand(general, "Toggle Waveform", nameof(PreferencesKeyBinds.ToggleWaveForm), OnToggleWaveform);
		AddKeyCommand(general, "Toggle Dark", nameof(PreferencesKeyBinds.ToggleDark), OnToggleDark);
		UIControls.Instance.AddCommand(general, "Lock Receptor Move Axis", nameof(PreferencesKeyBinds.LockReceptorMoveAxis));
		UIControls.Instance.AddStaticCommand(general, "Context Menu", "Right Mouse Button");
		UIControls.Instance.AddStaticCommand(general, "Exit", "Alt+F4");

		const string chartSelection ="Chart Selection";
		AddKeyCommand(chartSelection, "Open Previous Chart", nameof(PreferencesKeyBinds.OpenPreviousChart), OpenPreviousChart);
		AddKeyCommand(chartSelection, "Open Next Chart", nameof(PreferencesKeyBinds.OpenNextChart), OpenNextChart);
		AddKeyCommand(chartSelection, "Close Focused Chart", nameof(PreferencesKeyBinds.CloseFocusedChart), CloseFocusedChart);
		AddKeyCommand(chartSelection, "Keep Chart Open", nameof(PreferencesKeyBinds.KeepChartOpen), SetFocusedChartHasDedicatedTab);
		AddKeyCommand(chartSelection, "Move Focused Chart Left", nameof(PreferencesKeyBinds.MoveFocusedChartLeft), MoveFocusedChartLeft, true);
		AddKeyCommand(chartSelection, "Move Focused Chart Right", nameof(PreferencesKeyBinds.MoveFocusedChartRight), MoveFocusedChartRight, true);
		AddKeyCommand(chartSelection, "Focus Previous Chart", nameof(PreferencesKeyBinds.FocusPreviousChart), FocusPreviousChart, true);
		AddKeyCommand(chartSelection, "Focus Next Chart", nameof(PreferencesKeyBinds.FocusNextChart), FocusNextChart, true);

		const string zoom = "Zoom";
		UIControls.Instance.AddCommand(zoom, "Zoom", nameof(PreferencesKeyBinds.ScrollZoom), UIControls.MultipleKeysJoinString + "Scroll");
		UIControls.Instance.AddCommand(zoom, "Change Spacing For Current Mode", nameof(PreferencesKeyBinds.ScrollSpacing), UIControls.MultipleKeysJoinString + "Scroll");

		const string navigation = "Navigation";
		AddKeyCommand(navigation, "Move Up", nameof(PreferencesKeyBinds.MoveUp), OnMoveUp, true);
		UIControls.Instance.AddStaticCommand(navigation, "Move Up", "Scroll Up");
		AddKeyCommand(navigation, "Move Down", nameof(PreferencesKeyBinds.MoveDown), OnMoveDown, true);
		UIControls.Instance.AddStaticCommand(navigation, "Move Down", "Scroll Down");
		AddKeyCommand(navigation, "Move To Previous Row With Steps", nameof(PreferencesKeyBinds.MoveToPreviousRowWithSteps), OnMoveToPreviousRowWithSteps, true);
		AddKeyCommand(navigation, "Move To Next Row With Steps", nameof(PreferencesKeyBinds.MoveToNextRowWithSteps), OnMoveToNextRowWithSteps, true);
		AddKeyCommand(navigation, "Move To Previous Row With Events", nameof(PreferencesKeyBinds.MoveToPreviousRowWithEvent), OnMoveToPreviousRowWithEvents, true);
		AddKeyCommand(navigation, "Move To Next Row With Events", nameof(PreferencesKeyBinds.MoveToNextRowWithEvent), OnMoveToNextRowWithEvents, true);
		AddKeyCommand(navigation, "Move To Start of Stream", nameof(PreferencesKeyBinds.MoveToStartOfStream), OnMoveToStartOfStream, true);
		AddKeyCommand(navigation, "Move To End of Stream", nameof(PreferencesKeyBinds.MoveToEndOfStream), OnMoveToEndOfStream, true);
		AddKeyCommand(navigation, "Move To Previous Measure", nameof(PreferencesKeyBinds.MoveToPreviousMeasure), OnMoveToPreviousMeasure, true);
		AddKeyCommand(navigation, "Move To Next Measure", nameof(PreferencesKeyBinds.MoveToNextMeasure), OnMoveToNextMeasure, true);
		AddKeyCommand(navigation, "Move To Chart Start", nameof(PreferencesKeyBinds.MoveToChartStart), OnMoveToChartStart);
		AddKeyCommand(navigation, "Move To Chart End", nameof(PreferencesKeyBinds.MoveToChartEnd), OnMoveToChartEnd);
		AddKeyCommand(navigation, "Move To Next Label", nameof(PreferencesKeyBinds.MoveToNextLabel), OnMoveToNextLabel, true);
		AddKeyCommand(navigation, "Move To Previous Label", nameof(PreferencesKeyBinds.MoveToPreviousLabel), OnMoveToPreviousLabel, true);

		const string snap = "Snap";
		AddKeyCommand(snap, "Decrease Snap", nameof(PreferencesKeyBinds.DecreaseSnap), OnDecreaseSnap, true);
		AddKeyCommand(snap, "Increase Snap", nameof(PreferencesKeyBinds.IncreaseSnap), OnIncreaseSnap, true);
		AddKeyCommand(snap, "Snap to None", nameof(PreferencesKeyBinds.SnapToNone), () => { SnapManager.SetSnapToLevel(0); });
		AddKeyCommand(snap, "Snap to 1/4", nameof(PreferencesKeyBinds.SnapToQuarters), () => { SnapManager.SetSnapToLevel(1); });
		AddKeyCommand(snap, "Snap to 1/8", nameof(PreferencesKeyBinds.SnapToEighths), () => { SnapManager.SetSnapToLevel(2); });
		AddKeyCommand(snap, "Snap to 1/12", nameof(PreferencesKeyBinds.SnapToTwelfths), () => { SnapManager.SetSnapToLevel(3); });
		AddKeyCommand(snap, "Snap to 1/16", nameof(PreferencesKeyBinds.SnapToSixteenths), () => { SnapManager.SetSnapToLevel(4); });
		AddKeyCommand(snap, "Snap to 1/24", nameof(PreferencesKeyBinds.SnapToTwentyFourths), () => { SnapManager.SetSnapToLevel(5); });
		AddKeyCommand(snap, "Snap to 1/32", nameof(PreferencesKeyBinds.SnapToThirtySeconds), () => { SnapManager.SetSnapToLevel(6); });
		AddKeyCommand(snap, "Snap to 1/48", nameof(PreferencesKeyBinds.SnapToFortyEighths), () => { SnapManager.SetSnapToLevel(7); });
		AddKeyCommand(snap, "Snap to 1/64", nameof(PreferencesKeyBinds.SnapToSixtyFourths), () => { SnapManager.SetSnapToLevel(8); });
		AddKeyCommand(snap, "Snap to 1/192", nameof(PreferencesKeyBinds.SnapToOneHundredNinetySeconds), () => { SnapManager.SetSnapToLevel(9); });

		const string routine = "Routine / Couples / Co-op";
		AddKeyCommand(routine, "Toggle Player", nameof(PreferencesKeyBinds.TogglePlayer), OnTogglePlayer);
		AddKeyCommand(routine, "Set Player 1", nameof(PreferencesKeyBinds.SetPlayer1), () => { SetPlayer(0); } );
		AddKeyCommand(routine, "Set Player 2", nameof(PreferencesKeyBinds.SetPlayer2), () => { SetPlayer(1); } );
		AddKeyCommand(routine, "Set Player 3", nameof(PreferencesKeyBinds.SetPlayer3), () => { SetPlayer(2); } );
		AddKeyCommand(routine, "Set Player 4", nameof(PreferencesKeyBinds.SetPlayer4), () => { SetPlayer(3); } );
		AddKeyCommand(routine, "Select All Current Player Notes", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerNotes), OnSelectAllForCurrentPlayer);
		AddKeyCommand(routine, "Select All Current Player Taps", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerTaps), () => OnSelectAllForCurrentPlayer(e => e is EditorTapNoteEvent));
		AddKeyCommand(routine, "Select All Current Player Mines", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerMines), () => OnSelectAllForCurrentPlayer(e => e is EditorMineNoteEvent));
		AddKeyCommand(routine, "Select All Current Player Fakes", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerFakes), () => OnSelectAllForCurrentPlayer(e => e is EditorFakeNoteEvent));
		AddKeyCommand(routine, "Select All Current Player Lifts", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerLifts), () => OnSelectAllForCurrentPlayer(e => e is EditorLiftNoteEvent));
		AddKeyCommand(routine, "Select All Current Player Holds", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerHolds), () => OnSelectAllForCurrentPlayer(e => e is EditorHoldNoteEvent hn && !hn.IsRoll()));
		AddKeyCommand(routine, "Select All Current Player Rolls", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerRolls), () => OnSelectAllForCurrentPlayer(e => e is EditorHoldNoteEvent hn && hn.IsRoll()));
		AddKeyCommand(routine, "Select All Current Player Holds and Rolls", nameof(PreferencesKeyBinds.SelectAllCurrentPlayerHoldsAndRolls), () => OnSelectAllForCurrentPlayer(e => e is EditorHoldNoteEvent));
		AddKeyCommand(routine, "Convert Selected Notes to Player 1", nameof(PreferencesKeyBinds.ConvertSelectedNotesToPlayer1), () => { UIEditEvents.ConvertSelectedNotesToPlayer(0); });
		AddKeyCommand(routine, "Convert Selected Notes to Player 2", nameof(PreferencesKeyBinds.ConvertSelectedNotesToPlayer2), () => { UIEditEvents.ConvertSelectedNotesToPlayer(1); });
		AddKeyCommand(routine, "Convert Selected Notes to Player 3", nameof(PreferencesKeyBinds.ConvertSelectedNotesToPlayer3), () => { UIEditEvents.ConvertSelectedNotesToPlayer(2); });
		AddKeyCommand(routine, "Convert Selected Notes to Player 4", nameof(PreferencesKeyBinds.ConvertSelectedNotesToPlayer4), () => { UIEditEvents.ConvertSelectedNotesToPlayer(3); });
		AddKeyCommand(routine, "Swap Selected Player 1 and Player 2 Notes", nameof(PreferencesKeyBinds.SwapSelectedPlayer1And2Notes), () => { UIEditEvents.SwapSelectedNotesBetweenPlayers(0, 1); });
		AddKeyCommand(routine, "Swap Selected Player 1 and Player 3 Notes", nameof(PreferencesKeyBinds.SwapSelectedPlayer1And3Notes), () => { UIEditEvents.SwapSelectedNotesBetweenPlayers(0, 2); });
		AddKeyCommand(routine, "Swap Selected Player 1 and Player 4 Notes", nameof(PreferencesKeyBinds.SwapSelectedPlayer1And4Notes), () => { UIEditEvents.SwapSelectedNotesBetweenPlayers(0, 3); });
		AddKeyCommand(routine, "Swap Selected Player 2 and Player 3 Notes", nameof(PreferencesKeyBinds.SwapSelectedPlayer2And3Notes), () => { UIEditEvents.SwapSelectedNotesBetweenPlayers(1, 2); });
		AddKeyCommand(routine, "Swap Selected Player 2 and Player 4 Notes", nameof(PreferencesKeyBinds.SwapSelectedPlayer2And4Notes), () => { UIEditEvents.SwapSelectedNotesBetweenPlayers(1, 3); });
		AddKeyCommand(routine, "Swap Selected Player 3 and Player 4 Notes", nameof(PreferencesKeyBinds.SwapSelectedPlayer3And4Notes), () => { UIEditEvents.SwapSelectedNotesBetweenPlayers(2, 3); });

		const string patterns = "Patterns";
		AddKeyCommand(patterns, "Move To Next Pattern", nameof(PreferencesKeyBinds.MoveToNextPattern), OnMoveToNextPattern, true);
		AddKeyCommand(patterns, "Move To Previous Pattern", nameof(PreferencesKeyBinds.MoveToPreviousPattern), OnMoveToPreviousPattern, true);
		AddKeyCommand(patterns, "Regenerate All Patterns (Fixed Seeds)", nameof(PreferencesKeyBinds.RegenerateAllPatternsFixedSeeds), OnRegenerateAllPatterns);
		AddKeyCommand(patterns, "Regenerate All Patterns (New Seeds)", nameof(PreferencesKeyBinds.RegenerateAllPatternsNewSeeds), OnRegenerateAllPatternsWithNewSeeds);
		AddKeyCommand(patterns, "Regenerate Selected Patterns (Fixed Seeds)", nameof(PreferencesKeyBinds.RegenerateSelectedPatternsFixedSeeds), OnRegenerateSelectedPatterns);
		AddKeyCommand(patterns, "Regenerate Selected Patterns (New Seeds)", nameof(PreferencesKeyBinds.RegenerateSelectedPatternsNewSeeds), OnRegenerateSelectedPatternsWithNewSeeds);

		const string editSelection = "Edit Selection";
		AddKeyCommand(editSelection, "Delete", nameof(PreferencesKeyBinds.Delete), OnDelete);
		AddKeyCommand(editSelection, "Mirror", nameof(PreferencesKeyBinds.Mirror), OnMirrorSelection);
		AddKeyCommand(editSelection, "Flip", nameof(PreferencesKeyBinds.Flip), OnFlipSelection);
		AddKeyCommand(editSelection, "Mirror and Flip", nameof(PreferencesKeyBinds.MirrorAndFlip), OnMirrorAndFlipSelection);
		AddKeyCommand(editSelection, "Shift Left", nameof(PreferencesKeyBinds.ShiftLeft), OnShiftSelectedNotesLeft, true);
		AddKeyCommand(editSelection, "Shift Left And Wrap", nameof(PreferencesKeyBinds.ShiftLeftAndWrap), OnShiftSelectedNotesLeftAndWrap, true);
		AddKeyCommand(editSelection, "Shift Right", nameof(PreferencesKeyBinds.ShiftRight), OnShiftSelectedNotesRight, true);
		AddKeyCommand(editSelection, "Shift Right And Wrap", nameof(PreferencesKeyBinds.ShiftRightAndWrap), OnShiftSelectedNotesRightAndWrap, true);
		AddKeyCommand(editSelection, "Shift Earlier", nameof(PreferencesKeyBinds.ShiftEarlier), OnShiftSelectedNotesEarlier, true);
		AddKeyCommand(editSelection, "Shift Later", nameof(PreferencesKeyBinds.ShiftLater), OnShiftSelectedNotesLater, true);

		const string convertSelection = "Convert Selection";
		AddKeyCommand(convertSelection, "Taps to Mines", nameof(PreferencesKeyBinds.ConvertSelectedTapsToMines), UIEditEvents.ConvertSelectedTapsToMines);
		AddKeyCommand(convertSelection, "Taps to Fakes", nameof(PreferencesKeyBinds.ConvertSelectedTapsToFakes), UIEditEvents.ConvertSelectedTapsToFakes);
		AddKeyCommand(convertSelection, "Taps to Lifts", nameof(PreferencesKeyBinds.ConvertSelectedTapsToLifts), UIEditEvents.ConvertSelectedTapsToLifts);
		AddKeyCommand(convertSelection, "Mines to Taps", nameof(PreferencesKeyBinds.ConvertSelectedMinesToTaps), UIEditEvents.ConvertSelectedMinesToTaps);
		AddKeyCommand(convertSelection, "Mines to Fakes", nameof(PreferencesKeyBinds.ConvertSelectedMinesToFakes), UIEditEvents.ConvertSelectedMinesToFakes);
		AddKeyCommand(convertSelection, "Mines to Lifts", nameof(PreferencesKeyBinds.ConvertSelectedMinesToLifts), UIEditEvents.ConvertSelectedMinesToLifts);
		AddKeyCommand(convertSelection, "Fakes to Taps", nameof(PreferencesKeyBinds.ConvertSelectedFakesToTaps), UIEditEvents.ConvertSelectedFakesToTaps);
		AddKeyCommand(convertSelection, "Lifts to Taps", nameof(PreferencesKeyBinds.ConvertSelectedLiftsToTaps), UIEditEvents.ConvertSelectedLiftsToTaps);
		AddKeyCommand(convertSelection, "Holds to Rolls", nameof(PreferencesKeyBinds.ConvertSelectedHoldsToRolls), UIEditEvents.ConvertSelectedHoldsToRolls);
		AddKeyCommand(convertSelection, "Holds to Taps", nameof(PreferencesKeyBinds.ConvertSelectedHoldsToTaps), UIEditEvents.ConvertSelectedHoldsToTaps);
		AddKeyCommand(convertSelection, "Holds to Mines", nameof(PreferencesKeyBinds.ConvertSelectedHoldsToMines), UIEditEvents.ConvertSelectedHoldsToMines);
		AddKeyCommand(convertSelection, "Rolls to Holds", nameof(PreferencesKeyBinds.ConvertSelectedRollsToHolds), UIEditEvents.ConvertSelectedRollsToHolds);
		AddKeyCommand(convertSelection, "Rolls to Taps", nameof(PreferencesKeyBinds.ConvertSelectedRollsToTaps), UIEditEvents.ConvertSelectedRollsToTaps);
		AddKeyCommand(convertSelection, "Rolls to Mines", nameof(PreferencesKeyBinds.ConvertSelectedRollsToMines), UIEditEvents.ConvertSelectedRollsToMines);
		AddKeyCommand(convertSelection, "Warps to Negative Stops", nameof(PreferencesKeyBinds.ConvertSelectedWarpsToNegativeStops), UIEditEvents.ConvertSelectedWarpsToNegativeStops);
		AddKeyCommand(convertSelection, "Negative Stops to Warps", nameof(PreferencesKeyBinds.ConvertSelectedNegativeStopsToWarps), UIEditEvents.ConvertSelectedNegativeStopsToWarps);

		const string swapSelection = "Swap Selection";
		AddKeyCommand(swapSelection, "Taps and Mines", nameof(PreferencesKeyBinds.SwapSelectedTapsAndMines), UIEditEvents.SwapSelectedTapsAndMines);
		AddKeyCommand(swapSelection, "Taps and Fakes", nameof(PreferencesKeyBinds.SwapSelectedTapsAndFakes), UIEditEvents.SwapSelectedTapsAndFakes);
		AddKeyCommand(swapSelection, "Taps and Lifts", nameof(PreferencesKeyBinds.SwapSelectedTapsAndLifts), UIEditEvents.SwapSelectedTapsAndLifts);
		AddKeyCommand(swapSelection, "Mines and Fakes", nameof(PreferencesKeyBinds.SwapSelectedMinesAndFakes), UIEditEvents.SwapSelectedMinesAndFakes);
		AddKeyCommand(swapSelection, "Mines and Lifts", nameof(PreferencesKeyBinds.SwapSelectedMinesAndLifts), UIEditEvents.SwapSelectedMinesAndLifts);
		AddKeyCommand(swapSelection, "Holds and Rolls", nameof(PreferencesKeyBinds.SwapSelectedHoldsAndRolls), UIEditEvents.SwapSelectedHoldsAndRolls);
		AddKeyCommand(swapSelection, "Holds and Taps", nameof(PreferencesKeyBinds.SwapSelectedHoldsAndTaps), UIEditEvents.SwapSelectedHoldsAndTaps);
		AddKeyCommand(swapSelection, "Holds and Mines", nameof(PreferencesKeyBinds.SwapSelectedHoldsAndMines), UIEditEvents.SwapSelectedHoldsAndMines);
		AddKeyCommand(swapSelection, "Rolls and Taps", nameof(PreferencesKeyBinds.SwapSelectedRollsAndTaps), UIEditEvents.SwapSelectedRollsAndTaps);
		AddKeyCommand(swapSelection, "Rolls and Mines", nameof(PreferencesKeyBinds.SwapSelectedRollsAndMines), UIEditEvents.SwapSelectedRollsAndMines);

		const string eventEntry = "Event Entry";
		AddKeyCommand(eventEntry, "Add Tempo", nameof(PreferencesKeyBinds.AddEventTempo), UIEditEvents.AddTempoEvent);
		AddKeyCommand(eventEntry, "Add Interpolated Scroll Rate", nameof(PreferencesKeyBinds.AddEventInterpolatedScrollRate), UIEditEvents.AddInterpolatedScrollRateEvent);
		AddKeyCommand(eventEntry, "Add Scroll Rate", nameof(PreferencesKeyBinds.AddEventScrollRate), UIEditEvents.AddScrollRateEvent);
		AddKeyCommand(eventEntry, "Add Stop", nameof(PreferencesKeyBinds.AddEventStop), UIEditEvents.AddStopEvent);
		AddKeyCommand(eventEntry, "Add Delay", nameof(PreferencesKeyBinds.AddEventDelay), UIEditEvents.AddDelayEvent);
		AddKeyCommand(eventEntry, "Add Warp", nameof(PreferencesKeyBinds.AddEventWarp), UIEditEvents.AddWarpEvent);
		AddKeyCommand(eventEntry, "Add Fake Region", nameof(PreferencesKeyBinds.AddEventFakeRegion), UIEditEvents.AddFakeRegionEvent);
		AddKeyCommand(eventEntry, "Add Ticks", nameof(PreferencesKeyBinds.AddEventTicks), UIEditEvents.AddTicksEvent);
		AddKeyCommand(eventEntry, "Add Combo Multipliers", nameof(PreferencesKeyBinds.AddEventComboMultipliers), UIEditEvents.AddComboMultipliersEvent);
		AddKeyCommand(eventEntry, "Add Time Signature", nameof(PreferencesKeyBinds.AddEventTimeSignature), UIEditEvents.AddTimeSignatureEvent);
		AddKeyCommand(eventEntry, "Add Label", nameof(PreferencesKeyBinds.AddEventLabel), UIEditEvents.AddLabelEvent);
		AddKeyCommand(eventEntry, "Add Pattern", nameof(PreferencesKeyBinds.AddEventPattern), UIEditEvents.AddPatternEvent);
		AddKeyCommand(eventEntry, "Move Music Preview", nameof(PreferencesKeyBinds.MoveEventPreview), UIEditEvents.MoveMusicPreview);
		AddKeyCommand(eventEntry, "Move End Hint", nameof(PreferencesKeyBinds.MoveEventEndHint), UIEditEvents.MoveEndHint);

		const string noteEntry = "Note Entry";
		AddKeyCommand(noteEntry, "Lane 01", nameof(PreferencesKeyBinds.Arrow0), () => OnLaneInputDown(0), false, () => OnLaneInputUp(0));
		AddKeyCommand(noteEntry, "Lane 02", nameof(PreferencesKeyBinds.Arrow1), () => OnLaneInputDown(1), false, () => OnLaneInputUp(1));
		AddKeyCommand(noteEntry, "Lane 03", nameof(PreferencesKeyBinds.Arrow2), () => OnLaneInputDown(2), false, () => OnLaneInputUp(2));
		AddKeyCommand(noteEntry, "Lane 04", nameof(PreferencesKeyBinds.Arrow3), () => OnLaneInputDown(3), false, () => OnLaneInputUp(3));
		AddKeyCommand(noteEntry, "Lane 05", nameof(PreferencesKeyBinds.Arrow4), () => OnLaneInputDown(4), false, () => OnLaneInputUp(4));
		AddKeyCommand(noteEntry, "Lane 06", nameof(PreferencesKeyBinds.Arrow5), () => OnLaneInputDown(5), false, () => OnLaneInputUp(5));
		AddKeyCommand(noteEntry, "Lane 07", nameof(PreferencesKeyBinds.Arrow6), () => OnLaneInputDown(6), false, () => OnLaneInputUp(6));
		AddKeyCommand(noteEntry, "Lane 08", nameof(PreferencesKeyBinds.Arrow7), () => OnLaneInputDown(7), false, () => OnLaneInputUp(7));
		AddKeyCommand(noteEntry, "Lane 09", nameof(PreferencesKeyBinds.Arrow8), () => OnLaneInputDown(8), false, () => OnLaneInputUp(8));
		AddKeyCommand(noteEntry, "Lane 10", nameof(PreferencesKeyBinds.Arrow9), () => OnLaneInputDown(9), false, () => OnLaneInputUp(9));
		AddKeyCommand(noteEntry, "Switch Note to Mine / Roll", nameof(PreferencesKeyBinds.ArrowModification), OnArrowModificationKeyDown, false, OnArrowModificationKeyUp);
		// @formatter:on

		UIControls.Instance.FinishAddingCommands();
		UIControls.Instance.LogConflicts();
	}

	/// <summary>
	/// Helper to add a key command and register it for the controls UI.
	/// </summary>
	private void AddKeyCommand(
		string category,
		string name,
		string id,
		Action callback,
		bool repeat = false,
		Action releaseCallback = null)
	{
		var blocksInput = Preferences.Instance.PreferencesKeyBinds.BlocksInput(id);
		KeyCommandManager.Register(new KeyCommandManager.Command(name, id, callback, repeat, releaseCallback,
			blocksInput));
		UIControls.Instance.AddCommand(category, name, id);
	}

	private void InitializeDocumentation()
	{
		Documentation.Initialize(PlatformInterface);
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

	private void InitializeObservers()
	{
		Preferences.Instance.PreferencesOptions.AddObserver(this);
		Preferences.Instance.PreferencesAudio.AddObserver(this);
		ActionQueue.Instance.AddObserver(this);
	}

	private void InitializePerformanceMonitor()
	{
		PerformanceMonitor = new PerformanceMonitor(1024, PerformanceTimings.PerfTimings);
		PerformanceMonitor.SetEnabled(!Preferences.Instance.PreferencesPerformance.PerformanceMonitorPaused);
	}

	private void InitializeSnapManager()
	{
		SnapManager = new SnapManager();
	}

	private void InitializeImGui()
	{
		ImGuiRenderer = new ImGuiRenderer(this, ImGuiConfigFlags.DockingEnable, PlatformInterface.GetImGuiSaveFileName());
	}

	private void InitializeFonts()
	{
		try
		{
			var guiScale = GetDpiScale();
			var path = PlatformInterface.GetResourceDirectory();
			var mPlusFontPath = Path.Combine([path, "Content", "Mplus1Code-Medium.ttf"]);

			unsafe
			{
				// Construct a list of glyph ranges.
				var data = new List<ushort>();

				// Add ImGui's Japanese ranges, which include Basic Latin and Latin-1 Supplement.
				var japanese = (ushort*)ImGui.GetIO().Fonts.GetGlyphRangesJapanese();
				var current = japanese;
				while (*current != 0)
				{
					data.Add(*current);
					current++;
				}

				// Add extended Latin A for common European Latin characters.
				data.AddRange([0x0100, 0x017F]);

				// Add extended Latin B as it contains some modern European Latin characters.
				data.AddRange([0x0180, 0x024F]);

				// Terminate the list.
				data.Add(0x0000);

				// Create the font for the glyph ranges.
				fixed (ushort* dataPtr = data.ToArray())
				{
					ImGuiFont = ImGui.GetIO().Fonts
						.AddFontFromFileTTF(mPlusFontPath, (int)(15 * guiScale), null, (IntPtr)dataPtr);
				}
			}

			ImGuiRenderer.RebuildFontAtlas();
			ImGuiLayoutUtils.SetFont(ImGuiFont);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to initialize ImGui fonts. {e}.");
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
		RefreshEditorEventCachedScreenHeight();
	}

	private void InitializeWaveFormRenderer()
	{
		var p = Preferences.Instance;
		WaveFormTextureHeight = GetDesiredWaveFormTextureHeight();
		WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, WaveFormTextureHeight);
		WaveFormRenderer.SetColors(WaveFormColorDense, WaveFormColorSparse);
		WaveFormRenderer.SetXPerChannelScale(p.PreferencesWaveForm.WaveFormMaxXPercentagePerChannel);
		WaveFormRenderer.SetSoundMipMap(MusicManager.GetMusicMipMap());
		WaveFormRenderer.SetFocalPointLocalY(GetFocalPointChartSpaceY());
		RecreateWaveformRenderTarget(true);
	}

	private uint GetDesiredWaveFormTextureHeight()
	{
		return (uint)Math.Max(1, GetViewportHeight());
	}

	private void RecreateWaveformRenderTarget(bool force)
	{
		var desiredHeight = GetDesiredWaveFormTextureHeight();
		if (!force)
		{
			if (WaveFormTextureHeight == desiredHeight)
				return;
		}

		WaveFormTextureHeight = desiredHeight;
		WaveformRenderTarget = new RenderTarget2D(
			GraphicsDevice,
			WaveFormTextureWidth,
			(int)WaveFormTextureHeight,
			false,
			GraphicsDevice.PresentationParameters.BackBufferFormat,
			DepthFormat.Depth24);
	}

	private void InitializeDensityGraph()
	{
		DensityGraph = new StepDensityEffect(GraphicsDevice, Font);
	}

	private void InitializeMiniMap()
	{
		var p = Preferences.Instance;
		MiniMap = new MiniMap(GraphicsDevice, new Rectangle(0, 0, 0, 0), 0);
		MiniMap.SetFadeOutPercentage(0.9);
		MiniMap.SetSelectMode(p.PreferencesMiniMap.MiniMapSelectMode);
	}

	private void InitializeUIHelpers()
	{
		UIEditEvents = new UIEditEvents(this);
		UIFTUE = new UIFTUE(this);

		UIModals.Init(this);

		UILog.Instance.Init(this);
		UISongProperties.Instance.Init(this, GraphicsDevice, ImGuiRenderer);
		UIChartProperties.Instance.Init(this);
		UIPackProperties.Instance.Init(this, ActivePack, GraphicsDevice, ImGuiRenderer);
		UIChartList.Instance.Init(this);
		UIWaveFormPreferences.Instance.Init(MusicManager);
		UIReceptorPreferences.Instance.Init(this);
		UIOptions.Instance.Init(this);
		UIAudioPreferences.Instance.Init(SoundManager);
		UIExpressedChartConfig.Instance.Init(this);
		UIPerformedChartConfig.Instance.Init(this);
		UIPatternConfig.Instance.Init(this);
		UIAutogenConfigs.Instance.Init(this);
		UIAutogenChart.Instance.Init(this);
		UIAutogenChartsForChartType.Instance.Init(this);
		UICopyEventsBetweenCharts.Instance.Init(this);
		UIPatternEvent.Instance.Init(this);
		UIAttackEvent.Instance.Init(this);
		UIPerformance.Instance.Init(PerformanceMonitor);
		UIHotbar.Instance.Init(this);
#if DEBUG
		UIDebug.Instance.Init(this);
#endif
	}

	private void InitializeEditorPack()
	{
		ActivePack = new EditorPack(GraphicsDevice, ImGuiRenderer);
	}

	private void InitializeSongLoadTask()
	{
		SongLoadTask = new SongLoadTask(GraphicsDevice, ImGuiRenderer);
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
		LoadSplashTextures();
		LoadShaders();
		PerformPostContentLoadInitialization();

		base.LoadContent();
	}

	private void LoadTextureAtlas()
	{
		var atlasFileName = "atlas.json";
		var fullAtlasFileName = Path.Combine(PlatformInterface.GetResourceDirectory(), atlasFileName);
		if (!File.Exists(fullAtlasFileName))
		{
			Logger.Error($"Could not find texture atlas file {atlasFileName}.");
			TextureAtlas = new StaticTextureAtlas(new Texture2D(GraphicsDevice, 1, 1));
			return;
		}

		// Initialize the TextureAtlas
		TextureAtlas = StaticTextureAtlas.Load(Content, "atlas", fullAtlasFileName)
		               ?? new StaticTextureAtlas(new Texture2D(GraphicsDevice, 1, 1));

		// Register the TextureAtlas's texture with ImGui so it can be drawn in UI.
		TextureAtlasImGuiTexture = ImGuiRenderer.BindTexture(TextureAtlas.GetTexture());
	}

	private void LoadSplashTextures()
	{
		LogoAttribution ??= Content.Load<Texture2D>(TextureIdLogoAttribution);
		if (SplashBackground == null)
		{
			SplashBackground = new Texture2D(GraphicsDevice, 1, 1);
			SplashBackground.SetData([Color.Black]);
		}
	}

	private void LoadShaders()
	{
		FxaaEffect = Content.Load<Effect>("fxaa");
		WaveformColorEffect = Content.Load<Effect>("waveform-color");
	}

	private void PerformPostContentLoadInitialization()
	{
		_ = InitStepGraphDataAsync();
		CheckForAutoLoadingLastSong();
	}

	#endregion Initialization

	public static string GetAssemblyPath()
	{
		try
		{
			var assembly = Assembly.GetEntryAssembly();
			var programPath = assembly!.Location;
			var programDir = System.IO.Path.GetDirectoryName(programPath);
			if (!string.IsNullOrEmpty(programDir))
				return programDir;
		}
		catch (Exception)
		{
			// Ignored
		}

		return ".";
	}

	#region Shutdown

	private bool HasUnsavedSongOrPackChanges()
	{
		return HasUnsavedSongChanges() || HasUnsavedPackChanges();
	}

	private bool HasUnsavedSongChanges()
	{
		return ActionQueue.Instance.HasUnsavedChanges();
	}

	private bool HasUnsavedPackChanges()
	{
		return ActivePack?.HasUnsavedChanges() ?? false;
	}

	/// <summary>
	/// The window/platform has indicated the user is requesting shutdown.
	/// Return whether or not that shutdown should be allowed.
	/// Games may want to prevent this in situations like having unsaved changes.
	/// </summary>
	/// <returns>True if the app should shut down and false otherwise.</returns>
	public override bool HandleExitRequest()
	{
		if (HasUnsavedSongOrPackChanges())
		{
			PostSaveFunction = OnExitNoSave;
			ShowUnsavedChangesModal();
			return false;
		}

		if (IsSaving())
		{
			PostSaveFunction = OnExitNoSave;
			return false;
		}

		return true;
	}

	protected override void EndRun()
	{
		CloseSong();
		MusicManager.Shutdown();
		// Commit preferences to disk.
		Preferences.Save(PlatformInterface.GetPreferencesSaveFileName());
		// Commit unsaved changes to autogen configs to disk.
		PerformedChartConfigManager.Instance.SynchronizeToDisk();
		ExpressedChartConfigManager.Instance.SynchronizeToDisk();
		PatternConfigManager.Instance.SynchronizeToDisk();
		Logger.Shutdown();

		ActivePack?.Dispose();
		ActivePack = null;

		ImGuiRenderer.UnbindTexture(TextureAtlasImGuiTexture);
		base.EndRun();
	}

	private void OnExit()
	{
		if (HasUnsavedSongOrPackChanges())
		{
			PostSaveFunction = OnExitNoSave;
			ShowUnsavedChangesModal();
		}
		else if (IsSaving())
		{
			PostSaveFunction = OnExitNoSave;
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

	private void UnloadSplashTextures()
	{
		if (LogoAttribution != null)
		{
			LogoAttribution = null;
			Content.UnloadAsset(TextureIdLogoAttribution);
		}

		SplashBackground = null;
	}

	#endregion Shutdown

	#region Graphics

	public GraphicsDeviceManager GetGraphicsDeviceManager()
	{
		return Graphics;
	}

	public bool IsVSyncEnabled()
	{
		return Graphics.SynchronizeWithVerticalRetrace;
	}

	public void SetVSyncEnabled(bool enabled)
	{
		if (enabled != Graphics.SynchronizeWithVerticalRetrace)
		{
			Graphics.SynchronizeWithVerticalRetrace = enabled;
			Graphics.ApplyChanges();
		}
	}

	#endregion Graphics

	#region Static Helpers

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

	public static int GetMaxNumLanesForAnySupportedChartType()
	{
		return MaxNumLanesForAnySupportedChartType;
	}

	#endregion Static Helpers

	#region Helpers

	public IEditorPlatform GetPlatformInterface()
	{
		return PlatformInterface;
	}

	public bool IsOnMainThread()
	{
		return Environment.CurrentManagedThreadId == MainThreadId;
	}

	#endregion Helpers

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
		return GetFocusedChartData()?.Position.SongTime ?? 0.0;
	}

	internal IReadOnlyEditorPosition GetPosition()
	{
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData != null)
			return focusedChartData.Position;
		if (ActiveSong != null)
			return new EditorPosition(ActiveSong);
		return new EditorPosition();
	}

	internal EditorMouseState GetMouseState()
	{
		return EditorMouseState;
	}

	internal EditorSong GetActiveSong()
	{
		return ActiveSong;
	}

	internal EditorChart GetFocusedChart()
	{
		return FocusedChart;
	}

	#endregion State Accessors

	#region Position Modification

	public void SetSongTime(double songTime)
	{
		if (FocusedChartData == null)
			return;
		FocusedChartData.Position.SongTime = songTime;
		SynchronizeActiveChartTimes();
	}

	public void SetChartTime(double chartTime)
	{
		if (FocusedChartData == null)
			return;
		FocusedChartData.Position.ChartTime = chartTime;
		SynchronizeActiveChartTimes();
	}

	public void SetChartPosition(double chartPosition)
	{
		if (FocusedChartData == null)
			return;
		FocusedChartData.Position.ChartPosition = chartPosition;
		SynchronizeActiveChartTimes();
	}

	private void SynchronizeActiveChartTimes()
	{
		foreach (var activeChartData in ActiveChartData)
			if (activeChartData != FocusedChartData)
				SynchronizeActiveChartTime(activeChartData);
	}

	private void SynchronizeActiveChartTime(ActiveEditorChart activeChart)
	{
		// Always synchronize by song time.
		// This ensures that charts will never jump to different positions when switching
		// focus in songs where charts have different timing events.
		activeChart.Position.SongTime = GetPosition().SongTime;
		activeChart.Position.SetDesiredPositionToCurrent();
	}

	private void ResetPosition()
	{
		foreach (var activeChartData in ActiveChartData)
		{
			activeChartData.Position.Reset();
		}
	}

	#endregion Position Modification

	#region Window Resizing

	public void OnResize(object sender, EventArgs e)
	{
		var maximized = Window.IsMaximized();

		// Update window preferences.
		if (!maximized)
		{
			Preferences.Instance.ViewportWidth = GetViewportWidth();
			Preferences.Instance.ViewportHeight = GetViewportHeight();
		}

		Preferences.Instance.WindowMaximized = maximized;

		OnChartAreaChanged();
	}

	public bool GetChartAreaInScreenSpace(out Rectangle chartArea)
	{
		chartArea = ChartArea;
		return IsChartAreaSet;
	}

	public bool GetChartAreaInChartSpaceWithoutHeader(out Rectangle chartArea)
	{
		chartArea = new Rectangle(0, 0, ChartArea.Width, ChartArea.Height);
		var headerHeight = Math.Min(GetChartHeaderHeight(), chartArea.Height);
		chartArea.Y += headerHeight;
		chartArea.Height -= headerHeight;
		return IsChartAreaSet;
	}

	private void OnChartAreaChanged()
	{
		// Update focal point.
		Preferences.Instance.PreferencesReceptors.ClampPositions();

		// Resize the Waveform.
		if (WaveFormRenderer != null)
		{
			WaveFormRenderer.Resize(
				GraphicsDevice,
				WaveFormTextureWidth,
				(uint)Math.Max(1, GetViewportHeight()),
				(uint)Math.Max(1, ChartArea.Height));
			RecreateWaveformRenderTarget(false);
		}

		// Update the EditorEvent cached height.
		RefreshEditorEventCachedScreenHeight();
	}

	private void RefreshEditorEventCachedScreenHeight()
	{
		EditorEvent.SetScreenHeight((uint)Math.Max(1, Math.Max(ChartArea.Height, GetViewportHeight())));
	}

	/// <summary>
	/// Gets the viewport width.
	/// These dimensions include ImGui window dressing but do not include OS level window dressing.
	/// </summary>
	/// <returns>Viewport width in pixels.</returns>
	public int GetViewportWidth()
	{
		return Graphics.GraphicsDevice.Viewport.Width;
	}

	/// <summary>
	/// Gets the viewport height.
	/// These dimensions include ImGui window dressing but do not include OS level window dressing.
	/// </summary>
	/// <returns>Viewport height in pixels.</returns>
	public int GetViewportHeight()
	{
		return Graphics.GraphicsDevice.Viewport.Height;
	}

	public bool IsWindowSizeInitialized()
	{
		// On the first frame the backbuffer and form may not be set to correct values.
		// On first launch where the default setting is to be maximized this results in
		// incorrect values being used for layouts which depend on window size. This
		// occurs because Monogame performs one update before processing the events from
		// the OS about the window size. We can't assume the maximized backbuffer size
		// from the window size because of DPI / OS-specific dressing sizes. Because of
		// all of this we need to wait one frame before performing any logic which depends
		// on window size.
		return FrameCount > 2;
	}

	#endregion Window Resizing

	#region Focal Point

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int TransformChartSpaceXToScreenSpaceX(int chartSpaceX)
	{
		return ChartArea.X + chartSpaceX;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int TransformChartSpaceYToScreenSpaceY(int chartSpaceY)
	{
		return ChartArea.Y + chartSpaceY;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int TransformScreenSpaceXToChartSpaceX(int screenSpaceX)
	{
		return screenSpaceX - ChartArea.X;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int TransformScreenSpaceYToChartSpaceY(int screenSpaceY)
	{
		return screenSpaceY - ChartArea.Y;
	}

	public int GetFocalPointChartSpaceX()
	{
		if (Preferences.Instance.PreferencesReceptors.CenterHorizontally)
			return ChartArea.Width >> 1;
		return Preferences.Instance.PreferencesReceptors.ChartSpacePositionX;
	}

	public int GetFocalPointScreenSpaceX()
	{
		return TransformChartSpaceXToScreenSpaceX(GetFocalPointChartSpaceX());
	}

	public int GetFocalPointChartSpaceY()
	{
		return Preferences.Instance.PreferencesReceptors.ChartSpacePositionY;
	}

	public int GetFocalPointScreenSpaceY()
	{
		return TransformChartSpaceYToScreenSpaceY(GetFocalPointChartSpaceY());
	}

	public Vector2 GetFocalPointChartSpace()
	{
		return new Vector2(GetFocalPointChartSpaceX(), GetFocalPointChartSpaceY());
	}

	public Vector2 GetFocalPointScreenSpace()
	{
		return new Vector2(GetFocalPointScreenSpaceX(), GetFocalPointScreenSpaceY());
	}

	private void SetFocalPointScreenSpace(int screenSpaceX, int screenSpaceY)
	{
		var p = Preferences.Instance.PreferencesReceptors;
		p.ChartSpacePositionX = TransformScreenSpaceXToChartSpaceX(screenSpaceX);
		p.ChartSpacePositionY = TransformScreenSpaceYToChartSpaceY(screenSpaceY);
	}

	#endregion Focal Point

	#region Edit Locking

	public bool CanEdit()
	{
		if (ActiveSong != null && !ActiveSong.CanBeEdited())
			return false;
		if (ActiveSong != null && !ActiveSong.CanAllChartsBeEdited())
			return false;
		if (FocusedChart != null && !FocusedChart.CanBeEdited())
			return false;
		if (ActionQueue.Instance.IsDoingOrUndoing())
			return false;
		return true;
	}

	internal static bool CanSongBeEdited(EditorSong song)
	{
		if (ActionQueue.Instance.IsDoingOrUndoing())
			return false;
		return song?.CanBeEdited() ?? false;
	}

	internal static bool CanChartBeEdited(EditorChart chart)
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

			PlatformInterface.PlayExclamationSound();
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
		Debug.Assert(IsOnMainThread());

		FrameCount++;

		// We want to set the focus to the scene and off the UI when the app starts.
		// This is so that the UI doesn't capture input which is more likely intended
		// for the scene, like Tab for selection. Needing to do this on frame 3 is
		// a bit of a hack, but it is the earliest frame which has an effect on the UI.
		if (FrameCount == 3)
			ImGui.SetWindowFocus();

		PerformanceMonitor.SetTimeFromTimeSpan(PerformanceTimings.Present, PreviousPresentTime.Ticks);

		PerformanceMonitor.SetEnabled(!Preferences.Instance.PreferencesPerformance.PerformanceMonitorPaused);
		PerformanceMonitor.BeginFrameFromTimeSpan(gameTime.TotalGameTime.Ticks);
		PerformanceMonitor.SetTimeFromTimeSpan(PerformanceTimings.PresentWait, PresentWaitTime.Ticks);
		PerformanceMonitor.StartTiming(PerformanceTimings.EditorCPU);
		PerformanceMonitor.Time(PerformanceTimings.Update, () =>
		{
			var currentTime = gameTime.TotalGameTime.TotalSeconds;

			// Perform manual garbage collection.
			if (GarbageCollectFrame > 0)
			{
				GarbageCollectFrame--;
				if (GarbageCollectFrame == 0)
					GC.Collect();
			}

			MainThreadDispatcher.Pump();

			PlatformInterface.Update(gameTime);

			var newChartArea = UIDockSpace.GetCentralNodeArea();
			if (newChartArea != ChartArea)
			{
				ChartArea = newChartArea;
				IsChartAreaSet = true;
				OnChartAreaChanged();
			}

			CheckForAutoLoadingLastSong();

			ActiveSong?.Update(currentTime);
			ActivePack?.Update(currentTime);
			SoundManager.Update();

			ProcessInput(gameTime, currentTime);

			ZoomManager.Update(currentTime);
			UpdateMusicAndPosition(currentTime);

			PerformanceMonitor.Time(PerformanceTimings.ChartEvents, () =>
			{
				UpdateChartPositions();
				UpdateChartEvents();
				UpdateAutoPlay();
			});

			PerformanceMonitor.Time(PerformanceTimings.MiniMap, UpdateMiniMap);
			PerformanceMonitor.Time(PerformanceTimings.Waveform, UpdateWaveFormRenderer);

			UpdateDensityGraph();

			UpdateReceptors();

			// Update the Window title if the state of unsaved changes has changed.
			var hasUnsavedChanges = HasUnsavedSongChanges();
			if (UnsavedChangesLastFrame != hasUnsavedChanges)
			{
				UnsavedChangesLastFrame = hasUnsavedChanges;
				UpdateWindowTitle();
			}

			CheckForShowingUpdateModal();

			// Update splash screen timer.
			UpdateSplashTime(gameTime);

			base.Update(gameTime);
		});
	}

	private void UpdateSplashTime(GameTime gameTime)
	{
		// Update splash screen timer.
		if (SplashTime > 0.0)
		{
			SplashTime -= gameTime.ElapsedGameTime.TotalSeconds;
			if (SplashTime <= 0.0)
			{
				UnloadSplashTextures();
				SplashTime = 0.0;
			}
		}
	}

	private void CheckForShowingUpdateModal()
	{
		if (!UIFTUE.HasCompletedFTUE())
			return;
		if (Preferences.Instance.PreferencesOptions.SuppressUpdateNotification)
			return;
		if (HasShownUpdateModal)
			return;

		var latestVersion = GetAppLatestVersion();
		var latestVersionUrl = GetLatestVersionUrl();
		if (latestVersion == null || string.IsNullOrEmpty(latestVersionUrl))
			return;
		var version = GetAppVersion();
		if (version == null)
			return;
		if (latestVersion <= version)
			return;

		UIModals.OpenModalOneButton(
			"Update Available",
			$"An update is available from {GetPrettyVersion(version)} to {GetPrettyVersion(latestVersion)}.",
			"Okay", () => { },
			() =>
			{
				ImGui.TextLinkOpenURL(latestVersionUrl);

				ImGui.Checkbox("Don't notify on available updates.",
					ref Preferences.Instance.PreferencesOptions.SuppressUpdateNotification);
			});
		HasShownUpdateModal = true;
	}

	private void UpdateMusicAndPosition(double currentTime)
	{
		var focusedChartData = GetFocusedChartData();
		if (!Playing && focusedChartData != null)
		{
			if (focusedChartData.Position.IsInterpolatingChartPosition())
			{
				UpdatingSongTimeDirectly = true;
				focusedChartData.Position.UpdateChartPositionInterpolation(currentTime);
				SynchronizeActiveChartTimes();
				MusicManager.SetMusicTimeInSeconds(focusedChartData.Position.SongTime);
				UpdatingSongTimeDirectly = false;
			}

			if (focusedChartData.Position.IsInterpolatingSongTime())
			{
				UpdatingSongTimeDirectly = true;
				focusedChartData.Position.UpdateSongTimeInterpolation(currentTime);
				SynchronizeActiveChartTimes();
				MusicManager.SetMusicTimeInSeconds(focusedChartData.Position.SongTime);
				UpdatingSongTimeDirectly = false;
			}
		}

		var pAudio = Preferences.Instance.PreferencesAudio;
		MusicManager.SetPreviewParameters(
			ActiveSong?.SampleStart ?? 0.0,
			ActiveSong?.SampleLength ?? 0.0,
			pAudio.PreviewFadeInTime,
			pAudio.PreviewFadeOutTime);

		if (Playing)
		{
			UpdatingSongTimeDirectly = true;

			SetSongTime(PlaybackStartTime +
			            PlaybackStopwatch.Elapsed.TotalSeconds * Preferences.Instance.PreferencesAudio.MusicRate);

			MusicManager.Update(FocusedChart);

			if (focusedChartData != null)
			{
				// The goal is to set the SongTime to match the actual time of the music
				// being played through FMOD. Querying the time from FMOD does not have high
				// enough precision, so we need to use our own timer.
				// The best C# timer for this task is a StopWatch, but StopWatches have been
				// reported to drift, sometimes up to a half a second per hour. If we detect
				// it has drifted significantly by comparing it to the time from FMOD, then
				// snap it back.
				const double maxDeviation = 0.1;
				var musicSongTime = MusicManager.GetMusicSongTime();

				if (focusedChartData.Position.SongTime - musicSongTime > maxDeviation)
				{
					PlaybackStartTime -= 0.5 * maxDeviation;
					SetSongTime(PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds);
				}
				else if (musicSongTime - focusedChartData.Position.SongTime > maxDeviation)
				{
					PlaybackStartTime += 0.5 * maxDeviation;
					SetSongTime(PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds);
				}

				foreach (var activeChartData in ActiveChartData)
					activeChartData.Position.SetDesiredPositionToCurrent();
			}

			UpdatingSongTimeDirectly = false;
		}
		else
		{
			MusicManager.Update(FocusedChart);
		}
	}

	private void UpdateReceptors()
	{
		foreach (var activeChartData in ActiveChartData)
			activeChartData.UpdateReceptors(Playing);
	}

	private void UpdateWaveFormRenderer()
	{
		var pWave = Preferences.Instance.PreferencesWaveForm;

		// Performance optimization. Do not update the texture if we won't render it.
		if (!pWave.ShowWaveForm || !pWave.EnableWaveForm)
			return;

		var waveFormPPS = GetFocusedChartData()?.GetWaveFormPPS() ?? 1.0;

		// Update the WaveFormRenderer.
		WaveFormRenderer.SetFocalPointLocalY(GetFocalPointChartSpaceY());
		WaveFormRenderer.SetXPerChannelScale(pWave.WaveFormMaxXPercentagePerChannel);
		WaveFormRenderer.SetDenseScale(pWave.DenseScale);
		WaveFormRenderer.Update(GetPosition().SongTime, waveFormPPS * ZoomManager.GetSpacingZoom());
	}

	internal uint GetWaveFormWidth(ActiveEditorChart activeChart)
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		if (p.WaveFormScaleWidthToChart && activeChart != null)
		{
			if (p.WaveFormScaleXWhenZooming)
				return (uint)Math.Max(0, activeChart.GetLaneAreaWidthWithCurrentScale());
			return (uint)Math.Max(0, activeChart.GetLaneAreaWidth());
		}

		if (p.WaveFormScaleXWhenZooming)
			return (uint)(WaveFormTextureWidth * ZoomManager.GetSizeZoom());
		return WaveFormTextureWidth;
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
		var inFocus = IsActive;

		CurrentDesiredCursor = MouseCursor.Arrow;
		CanShowRightClickPopupThisFrame = false;

		var focusedChartData = GetFocusedChartData();
		focusedChartData?.UpdateSelectedRegion(currentTime);

		BeginImGuiFrame();
		// TODO: Remove remaining input processing from ImGuiRenderer.
		ImGuiRenderer.UpdateInput(gameTime);

		// Process Mouse Input.
		var state = Mouse.GetState();
		var (mouseChartTime, mouseChartPosition) = FindChartTimeAndRowForScreenY(state.Y);
		EditorMouseState.Update(state, mouseChartTime, mouseChartPosition, inFocus);
		var lmbState = EditorMouseState.GetButtonState(EditorMouseState.Button.Left);
		if (lmbState.UpThisFrame())
			LastMouseUpEventWasUsedForMovingFocalPoint = false;

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

		var mouseX = EditorMouseState.X();
		var mouseY = EditorMouseState.Y();

		// Early out if ImGui is using the mouse.
		if (imGuiWantMouse)
		{
			// The only case where we still want to process mouse input is dragging the chart headers.
			ProcessInputForDraggingHeader();

			// ImGui may want the mouse on a release when we are selecting. Stop selecting in that case.
			focusedChartData?.FinishSelectedRegion();
			UpdateCursor();
			return;
		}

		var atEdgeOfScreen = mouseX <= 0 || mouseX >= GetViewportWidth() - 1 || mouseY <= 0 || mouseY >= GetViewportHeight() - 1;
		var downThisFrame = lmbState.DownThisFrame();
		var clickingToDragWindowEdge = atEdgeOfScreen && downThisFrame;

		// If the user has clicked this frame, and they are at the edge of screen treat that as an intent to resize the window.
		if (!clickingToDragWindowEdge)
		{
			var inReceptorArea = Receptor.IsInReceptorArea(
				mouseX,
				mouseY,
				GetFocalPointScreenSpace(),
				ZoomManager.GetSizeZoom(),
				TextureAtlas,
				GetFocusedChartData()?.GetArrowGraphicManager(),
				FocusedChart);

			var inDensityArea = DensityGraph.IsInDensityGraphArea(mouseX, mouseY);
			var inMiniMapArea = MiniMap.IsScreenPositionInMiniMapBounds(mouseX, mouseY);
			var uiInterferingWithRegionClicking = inDensityArea || inMiniMapArea;

			var wereSceneElementsCapturingMouse = MiniMapCapturingMouse || MovingFocalPoint || DensityGraphCapturingMouse;

			// Process input for the mini map.
			if (!(focusedChartData?.IsSelectedRegionActive() ?? false) && !MovingFocalPoint && !DensityGraphCapturingMouse)
				ProcessInputForMiniMap();

			// Process input for the density graph
			if (!(focusedChartData?.IsSelectedRegionActive() ?? false) && !MovingFocalPoint && !MiniMapCapturingMouse)
				ProcessInputForDensityGraph();

			// Process input for grabbing the receptors and moving the focal point.
			if (!(focusedChartData?.IsSelectedRegionActive() ?? false) && !MiniMapCapturingMouse && !DensityGraphCapturingMouse)
			{
				ProcessInputForMovingFocalPoint(inReceptorArea, false);
				// Update cursor based on whether the receptors could be grabbed.
				if (inReceptorArea)
				{
					var p = Preferences.Instance.PreferencesReceptors;
					var canMoveX = !p.LockPositionX && !p.CenterHorizontally;
					var canMoveY = !p.LockPositionY;
					if (canMoveX && canMoveY)
						CurrentDesiredCursor = MouseCursor.SizeAll;
					else if (canMoveX)
						CurrentDesiredCursor = MouseCursor.SizeWE;
					else if (canMoveY)
						CurrentDesiredCursor = MouseCursor.SizeNS;
				}
			}

			var areSceneElementsCapturingMouse = MiniMapCapturingMouse || MovingFocalPoint || DensityGraphCapturingMouse;

			// Process input for selecting a region.
			if (!areSceneElementsCapturingMouse)
			{
				GetFocusedChartData()?.ProcessInputForSelectedRegion(
					currentTime,
					uiInterferingWithRegionClicking,
					EditorMouseState,
					lmbState);
			}

			// Process input for clicking an unfocused chart to focus it.
			if (lmbState.UpThisFrame() && !wereSceneElementsCapturingMouse && !areSceneElementsCapturingMouse)
			{
				var clickDownPos = lmbState.GetLastClickDownPosition();
				var clickUpPos = lmbState.GetLastClickUpPosition();

				foreach (var activeChart in ActiveChartData)
				{
					var area = activeChart.GetFullChartScreenSpaceArea();
					if (area.Contains(clickDownPos) && area.Contains(clickUpPos))
					{
						if (activeChart != focusedChartData)
						{
							SetChartFocused(activeChart.GetChart());
						}

						break;
					}
				}
			}

			// Process right click popup eligibility.
			CanShowRightClickPopupThisFrame = !MiniMapCapturingMouse && !MovingFocalPoint && !DensityGraphCapturingMouse;
		}

		UpdateCursor();

		// Process input for scrolling and zooming.
		ProcessInputForScrollingAndZooming(currentTime, gameTime.ElapsedGameTime.TotalSeconds);
	}

	private void BeginImGuiFrame()
	{
		ImGuiRenderer.BeforeLayout();
		if ((ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.DockingEnable) != 0)
		{
			UIDockSpace.PrepareDockSpace(IsWindowSizeInitialized());
		}
	}

	private void UpdateCursor()
	{
		// Setting the cursor every frame prevents it from changing to support normal application
		// behavior like indicating resizeability at the edges of the window. But not setting every frame
		// causes it to go back to the Default. Set it every frame only if it setting it to something
		// other than the Default.
		if ((CurrentDesiredCursor != null && CurrentDesiredCursor != PreviousDesiredCursor) ||
		    CurrentDesiredCursor != MouseCursor.Arrow)
		{
			Mouse.SetCursor(CurrentDesiredCursor);
		}

		PreviousDesiredCursor = CurrentDesiredCursor;
	}

	/// <summary>
	/// Processes input for moving the focal point with the mouse by dragging a header.
	/// </summary>
	/// <remarks>Helper for ProcessInput.</remarks>
	private void ProcessInputForDraggingHeader()
	{
		var overDraggableArea = false;
		foreach (var chart in ActiveChartData)
		{
			if (chart.IsDraggableAreaHovered())
			{
				overDraggableArea = true;
				break;
			}
		}

		ProcessInputForMovingFocalPoint(overDraggableArea, true);
	}

	/// <summary>
	/// Processes input for moving the focal point with the mouse.
	/// </summary>
	/// <remarks>Helper for ProcessInput.</remarks>
	private void ProcessInputForMovingFocalPoint(bool inReceptorArea, bool fromHeaderMove)
	{
		var p = Preferences.Instance.PreferencesReceptors;
		var canMoveX = !p.CenterHorizontally;
		if (p.LockPositionX && (!fromHeaderMove || !p.AllowChartMoveWhenPositionLocked))
			canMoveX = false;
		var canMoveY = !(p.LockPositionY || fromHeaderMove);
		var canMove = canMoveX || canMoveY;

		// Begin moving focal point.
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame()
		    && inReceptorArea
		    && canMove)
		{
			MovingFocalPoint = true;
			ForceOnlyHorizontalFocalPointMove = !canMoveY;
			ForceOnlyVerticalFocalPointMove = !canMoveX;
			FocalPointAtMoveStart = GetFocalPointChartSpace();
			FocalPointMoveOffset = new Vector2(EditorMouseState.X() - GetFocalPointScreenSpaceX(),
				EditorMouseState.Y() - GetFocalPointScreenSpaceY());
		}

		// Move focal point.
		if (MovingFocalPoint)
		{
			void SetReceptorPosition(int x, int y)
			{
				if (p.ChartSpacePositionX != x || p.ChartSpacePositionY != y)
					FocalPointMoved = true;
				p.ChartSpacePositionX = x;
				p.ChartSpacePositionY = y;
			}

			var newX = TransformScreenSpaceXToChartSpaceX(EditorMouseState.X()) - (int)FocalPointMoveOffset.X;
			var newY = TransformScreenSpaceYToChartSpaceY(EditorMouseState.Y()) - (int)FocalPointMoveOffset.Y;

			if (ForceOnlyHorizontalFocalPointMove)
			{
				SetReceptorPosition(newX, (int)FocalPointAtMoveStart.Y);
			}
			else if (ForceOnlyVerticalFocalPointMove)
			{
				SetReceptorPosition((int)FocalPointAtMoveStart.X, newY);
			}
			else if (KeyCommandManager.IsAnyInputDown(Preferences.Instance.PreferencesKeyBinds.LockReceptorMoveAxis))
			{
				if (Math.Abs(newX - FocalPointAtMoveStart.X) > Math.Abs(newY - FocalPointAtMoveStart.Y))
					SetReceptorPosition(newX, (int)FocalPointAtMoveStart.Y);
				else
					SetReceptorPosition((int)FocalPointAtMoveStart.X, newY);
			}
			else
			{
				SetReceptorPosition(newX, newY);
			}
		}

		// Stop moving focal point.
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).Up() && MovingFocalPoint)
		{
			LastMouseUpEventWasUsedForMovingFocalPoint = FocalPointMoved;
			MovingFocalPoint = false;
			FocalPointMoved = false;
			FocalPointMoveOffset = new Vector2();
		}
	}

	public bool WasLastMouseUpUsedForMovingFocalPoint()
	{
		return LastMouseUpEventWasUsedForMovingFocalPoint;
	}

	/// <summary>
	/// Processes input for scrolling and zooming.
	/// </summary>
	/// <remarks>Helper for ProcessInput.</remarks>
	private void ProcessInputForScrollingAndZooming(double currentTime, double deltaTime)
	{
		var pScroll = Preferences.Instance.PreferencesScroll;
		var scrollDelta = (float)EditorMouseState.ScrollDeltaSinceLastFrame() / EditorMouseState.GetDefaultScrollDetentValue();
		var focusedChartData = GetFocusedChartData();

		// When starting interpolation start at the time from the previous frame. This lets one frame of
		// movement occur this frame. For input like touchpads and smooth scroll wheels we can receive input
		// each frame, in which case we want to ensure each frame moves.
		var startTimeForInput = currentTime - deltaTime;

		// Process input for zoom controls.
		var zoomManagerCaptureInput = ZoomManager.ProcessInput(startTimeForInput, KeyCommandManager, scrollDelta);

		// If the input was used for controlling zoom levels do not continue to process it.
		if (zoomManagerCaptureInput || scrollDelta.FloatEquals(0.0f))
			return;

		// Adjust position.
		var timeDelta = pScroll.ScrollWheelTime / ZoomManager.GetSpacingZoom() * -scrollDelta;
		var rowDelta = pScroll.ScrollWheelRows / ZoomManager.GetSpacingZoom() * -scrollDelta;
		if (Playing)
		{
			if (focusedChartData != null)
			{
				PlaybackStartTime += timeDelta;
				SetSongTime(PlaybackStartTime +
				            PlaybackStopwatch.Elapsed.TotalSeconds * Preferences.Instance.PreferencesAudio.MusicRate);

				if (pScroll.StopPlaybackWhenScrolling)
				{
					StopPlayback();
				}
				else
				{
					MusicManager.SetMusicTimeInSeconds(GetPosition().SongTime);
				}

				UpdateAutoPlayFromScrolling();
			}
		}
		else
		{
			if (SnapManager.GetCurrentRows() == 0)
			{
				if (focusedChartData != null)
				{
					if (pScroll.SpacingMode == SpacingMode.ConstantTime)
						focusedChartData.Position.BeginSongTimeInterpolation(startTimeForInput, timeDelta);
					else
						focusedChartData.Position.BeginChartPositionInterpolation(startTimeForInput, rowDelta);
				}
			}
			else
			{
				// TODO: On touchpads where scroll delta will be small, we may want to accumulate more scroll
				// before moving. Without doing that on a touchpad we end up scrolling very fast.
				if (scrollDelta > 0.0f)
					OnMoveUp();
				else
					OnMoveDown();
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
			Preferences.Instance.PreferencesReceptors.ChartSpacePositionX = (int)FocalPointAtMoveStart.X;
			Preferences.Instance.PreferencesReceptors.ChartSpacePositionY = (int)FocalPointAtMoveStart.Y;
			return;
		}

		if (IsPlayingPreview())
		{
			StopPreview();
			return;
		}

		if (Playing)
		{
			StopPlayback();
			return;
		}

		GetFocusedChartData()?.ClearSelection();
	}

	private void OnToggleWaveform()
	{
		Preferences.Instance.PreferencesWaveForm.ShowWaveForm = !Preferences.Instance.PreferencesWaveForm.ShowWaveForm;
	}

	private void OnToggleDark()
	{
		Preferences.Instance.PreferencesDark.ShowDarkBg = !Preferences.Instance.PreferencesDark.ShowDarkBg;
	}

	#endregion Input Processing

	#region Playing Music

	private void StartPlayback()
	{
		if (Playing)
			return;

		StopPreview();

		var position = GetPosition();
		BeginPlaybackStopwatch(position.SongTime);
		MusicManager.StartPlayback(position.SongTime);

		Playing = true;

		// Start updating the AutoPlayer immediately.
		UpdateAutoPlay();
	}

	private void BeginPlaybackStopwatch(double songTime)
	{
		PlaybackStartTime = songTime;
		PlaybackStopwatch = new Stopwatch();
		PlaybackStopwatch.Start();
	}

	private void StopPlayback()
	{
		if (!Playing)
			return;

		PlaybackStopwatch.Stop();
		MusicManager.StopPlayback();

		foreach (var activeChartData in ActiveChartData)
			activeChartData.StopAutoPlayer();

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
		MusicManager.LoadMusicAsync(GetFullPathToFocusedChartMusicFile(), false,
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
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData != null)
			SetChartPosition(focusedChartData.Position.ChartPosition);
	}

	private void OnSyncOffsetChanged()
	{
		// Re-set the position to recompute the chart and song times.
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData != null)
			SetChartPosition(focusedChartData.Position.ChartPosition);
	}

	private void OnAudioOffsetChanged()
	{
		var playing = Playing;
		if (playing)
			StopPlayback();
		MusicManager.SetMusicOffset(Preferences.Instance.PreferencesAudio.AudioOffset);
		MusicManager.SetMusicTimeInSeconds(GetPosition().SongTime);
		if (playing)
			StartPlayback();
	}

	private void OnMusicRateChanged()
	{
		MusicManager.SetMusicRate(Preferences.Instance.PreferencesAudio.MusicRate);
		if (Playing)
		{
			// When adjusting the rate while playing restart our timer and base it on the current music time.
			var musicSongTime = MusicManager.GetMusicSongTime();
			SetSongTime(musicSongTime);
			BeginPlaybackStopwatch(GetPosition().SongTime);
		}
	}

	private void OnDecreaseMusicRate()
	{
		var musicRate = Preferences.Instance.PreferencesAudio.MusicRate;
		var musicRateByTens = (int)(musicRate * 10.0);
		if (((double)musicRateByTens).DoubleEquals(musicRate * 10.0))
			musicRateByTens--;
		var newMusicRate = Math.Clamp(musicRateByTens / 10.0, MusicManager.MinMusicRate, MusicManager.MaxMusicRate);
		if (newMusicRate.DoubleEquals(musicRate))
			return;
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(Preferences.Instance.PreferencesAudio,
			nameof(PreferencesAudio.MusicRate), newMusicRate, false));
	}

	private void OnIncreaseMusicRate()
	{
		var musicRate = Preferences.Instance.PreferencesAudio.MusicRate;
		var musicRateByTens = (int)(musicRate * 10.0);
		musicRateByTens++;
		var newMusicRate = Math.Clamp(musicRateByTens / 10.0, MusicManager.MinMusicRate, MusicManager.MaxMusicRate);
		if (newMusicRate.DoubleEquals(musicRate))
			return;
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(Preferences.Instance.PreferencesAudio,
			nameof(PreferencesAudio.MusicRate), newMusicRate, false));
	}

	private void OnMainVolumeChanged()
	{
		MusicManager.SetMainVolume(Preferences.Instance.PreferencesAudio.MainVolume);
	}

	private void OnMusicVolumeChanged()
	{
		MusicManager.SetMusicVolume(Preferences.Instance.PreferencesAudio.MusicVolume);
	}

	private void OnAssistTickVolumeChanged()
	{
		MusicManager.SetAssistTickVolume(Preferences.Instance.PreferencesAudio.AssistTickVolume);
	}

	private void OnAssistTickAttackTimeChanged()
	{
		MusicManager.SetAssistTickAttackTime(Preferences.Instance.PreferencesAudio.AssistTickAttackTime);
	}

	private void OnToggleAssistTick()
	{
		Preferences.Instance.PreferencesAudio.UseAssistTick = !Preferences.Instance.PreferencesAudio.UseAssistTick;
	}

	private void OnUseAssistTickChanged()
	{
		MusicManager.SetUseAssistTick(Preferences.Instance.PreferencesAudio.UseAssistTick);
	}

	private void OnSkipAssistTickOnBeatTickChanged()
	{
		MusicManager.SetSkipAssistTicksOnBeatTicks(Preferences.Instance.PreferencesAudio.SkipAssistTickOnBeatTick);
	}

	private void OnBeatTickVolumeChanged()
	{
		MusicManager.SetBeatTickVolume(Preferences.Instance.PreferencesAudio.BeatTickVolume);
	}

	private void OnBeatTickAttackTimeChanged()
	{
		MusicManager.SetBeatTickAttackTime(Preferences.Instance.PreferencesAudio.BeatTickAttackTime);
	}

	private void OnToggleBeatTick()
	{
		Preferences.Instance.PreferencesAudio.UseBeatTick = !Preferences.Instance.PreferencesAudio.UseBeatTick;
	}

	private void OnUseBeatTickChanged()
	{
		MusicManager.SetUseBeatTick(Preferences.Instance.PreferencesAudio.UseBeatTick);
	}

	private void OnSkipBeatTickOnAssistTickChanged()
	{
		MusicManager.SetSkipBeatTicksOnAssistTicks(Preferences.Instance.PreferencesAudio.SkipBeatTickOnAssistTick);
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

	public double GetSizeCap()
	{
		return ZoomManager.GetSizeCap();
	}

	#endregion Zoom

	#region Drawing

	protected override void Draw(GameTime gameTime)
	{
		Debug.Assert(IsOnMainThread());
		PerformanceMonitor.Time(PerformanceTimings.Draw, () =>
		{
			// Draw anything which rendering to custom render targets first.
			PreDrawToRenderTargets();

			// Prior to rendering set the render target to the screen.
			GraphicsDevice.SetRenderTarget(null, true);

			DrawBackground();
			if (Preferences.Instance.PreferencesDark.DarkBgDrawOrder == PreferencesDark.DrawOrder.AfterBackground)
				DrawDarkBackground();

			DrawWaveForm();
			if (Preferences.Instance.PreferencesDark.DarkBgDrawOrder == PreferencesDark.DrawOrder.AfterWaveForm)
				DrawDarkBackground();

			ImGui.PushFont(ImGuiFont);

			SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

			DrawActiveChartBoundaries();
			DrawMeasureMarkers();
			DrawRegions(false);
			DrawReceptors();
			DrawSnapIndicator();
			DrawPlayerIndicator();

			// Start a window within the central node so we can clip
			if (UIDockSpace.BeginCentralNodeAreaWindow())
			{
				// Draw the chart events, which include misc event widgets needing to be clipped.
				DrawChartEvents();

				// Draw the chart headers which also need to be clipped.
				DrawChartHeaders();
			}

			UIDockSpace.EndCentralNodeAreaWindow();

			DrawReceptorForegroundEffects();
			DrawSelectedMouseRegion();
			DrawRegions(true);

			DrawMiniMap();

			SpriteBatch.End();

			DrawDensityGraph();

			DrawGui();

			ImGui.PopFont();

			ImGuiRenderer.AfterLayout();

			DrawSplash();

			base.Draw(gameTime);
		});
		PerformanceMonitor.EndTiming(PerformanceTimings.EditorCPU);
	}

	/// <summary>
	/// Performs all draw calls to custom render targets.
	/// After performing all renders, sets the render target to the backbuffer for the final draws.
	/// </summary>
	private void PreDrawToRenderTargets()
	{
		PreDrawWaveFormToRenderTarget();
		PreDrawMiniMapToRenderTarget();
	}

	/// <summary>
	/// Draws to the waveform custom render target.
	/// </summary>
	private void PreDrawWaveFormToRenderTarget()
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		if (!p.ShowWaveForm || !p.EnableWaveForm)
			return;

		// Draw the waveform to its internal render target.
		var waveFormRenterTarget = WaveFormRenderer.Draw();

		// Determine the sparse color.
		var sparseColor = p.WaveFormSparseColor;
		switch (p.WaveFormSparseColorOption)
		{
			case PreferencesWaveForm.SparseColorOption.DarkerDenseColor:
				sparseColor.X = p.WaveFormDenseColor.X * p.WaveFormSparseColorScale;
				sparseColor.Y = p.WaveFormDenseColor.Y * p.WaveFormSparseColorScale;
				sparseColor.Z = p.WaveFormDenseColor.Z * p.WaveFormSparseColorScale;
				sparseColor.W = p.WaveFormDenseColor.W;
				break;
			case PreferencesWaveForm.SparseColorOption.SameAsDenseColor:
				sparseColor = p.WaveFormDenseColor;
				break;
		}

		// Configure the shader to recolor the waveform.
		WaveformColorEffect.CurrentTechnique = WaveformColorEffect.Techniques["color"];
		WaveformColorEffect.Parameters["bgColor"].SetValue(p.WaveFormBackgroundColor);
		WaveformColorEffect.Parameters["denseColor"].SetValue(p.WaveFormDenseColor);
		WaveformColorEffect.Parameters["sparseColor"].SetValue(sparseColor);

		// Draw the recolored waveform to our render target.
		GraphicsDevice.SetRenderTarget(WaveformRenderTarget);
		GraphicsDevice.Clear(Color.Transparent);
		SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, WaveformColorEffect);
		WaveformColorEffect.CurrentTechnique.Passes[0].Apply();
		SpriteBatch.Draw(waveFormRenterTarget,
			new Rectangle(0, 0, waveFormRenterTarget.Width, waveFormRenterTarget.Height), Color.White);
		SpriteBatch.End();
	}

	private void PreDrawMiniMapToRenderTarget()
	{
		MiniMapRenderTarget = MiniMap.Draw();
	}

	private void DrawChartHeaders()
	{
		UIChartHeader.DrawBackground(ChartArea);
		for (var i = 0; i < ActiveChartData.Count;)
		{
			// The header has a close button which can delete the chart.
			var chart = ActiveChartData[i];
			chart.DrawHeader();
			if (i < ActiveChartData.Count && chart == ActiveChartData[i])
				i++;
		}
	}

	private void DrawBackground()
	{
		// If the background should be hidden, just clear with black and return.
		if (Preferences.Instance.PreferencesOptions.HideSongBackground)
		{
			GraphicsDevice.Clear(Color.Black);
			return;
		}

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
		switch (Preferences.Instance.PreferencesOptions.BackgroundImageSize)
		{
			case PreferencesOptions.BackgroundImageSizeMode.ChartArea:
				ActiveSong.GetBackground().GetTexture()
					.DrawTexture(SpriteBatch, ChartArea.X, ChartArea.Y, (uint)ChartArea.Width, (uint)ChartArea.Height);
				break;
			case PreferencesOptions.BackgroundImageSizeMode.Window:
				ActiveSong.GetBackground().GetTexture()
					.DrawTexture(SpriteBatch, 0, 0, (uint)GetViewportWidth(), (uint)GetViewportHeight());
				break;
		}

		SpriteBatch.End();
	}

	private bool IsDarkBackgroundVisible()
	{
		var p = Preferences.Instance.PreferencesDark;
		if (!p.ShowDarkBg)
			return false;
		if (p.Size == PreferencesDark.SizeMode.Charts && GetFocusedChartData() == null)
			return false;
		return true;
	}

	private void DrawDarkBackground()
	{
		if (!IsDarkBackgroundVisible())
			return;

		var p = Preferences.Instance.PreferencesDark;
		var (x, w) = GetDarkBgXAndW();

		SpriteBatch.Begin();
		TextureAtlas.Draw("dark-bg", SpriteBatch, new RectangleF(x, 0, w, GetViewportHeight()),
			new Color(p.Color.X, p.Color.Y, p.Color.Z, p.Color.W));
		SpriteBatch.End();
	}

	private (int, int) GetDarkBgXAndW()
	{
		switch (Preferences.Instance.PreferencesDark.Size)
		{
			case PreferencesDark.SizeMode.Charts:
				return GetActiveChartsXAndWidth();
			case PreferencesDark.SizeMode.Window:
				return (0, GetViewportWidth());
		}

		return (0, 0);
	}

	private void DrawReceptors()
	{
		var sizeZoom = ZoomManager.GetSizeZoom();
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (!ActiveChartData[i].IsVisible())
				continue;
			ActiveChartData[i].DrawReceptors(sizeZoom, TextureAtlas, SpriteBatch);
		}
	}

	private void DrawSnapIndicator()
	{
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return;
		var arrowGraphicManager = focusedChartData.GetArrowGraphicManager();
		if (arrowGraphicManager == null)
			return;

		var (fillTextureId, color) = arrowGraphicManager.GetSnapIndicatorFillTexture(SnapManager.GetCurrentGetSubDivision());
		if (string.IsNullOrEmpty(fillTextureId))
			return;
		var (fillW, fillH) = TextureAtlas.GetDimensions(fillTextureId);

		var rimTextureId = ArrowGraphicManager.GetSnapIndicatorRimTexture();
		var (rimW, rimH) = TextureAtlas.GetDimensions(rimTextureId);

		var zoom = ZoomManager.GetSizeZoom();
		var receptorsLeftEdge = focusedChartData.GetScreenSpaceXOfLanesStartWithCurrentScale();
		var leftX = receptorsLeftEdge - rimW * 0.5 * zoom;
		var y = GetFocalPointScreenSpaceY();

		// Draw fill.
		TextureAtlas.Draw(
			fillTextureId,
			SpriteBatch,
			new Vector2((float)leftX, y),
			new Vector2((float)(fillW * 0.5), (float)(fillH * 0.5)),
			color,
			(float)zoom,
			0.0f,
			SpriteEffects.None);

		// Draw rim.
		TextureAtlas.Draw(
			rimTextureId,
			SpriteBatch,
			new Vector2((float)leftX, y),
			new Vector2((float)(rimW * 0.5), (float)(rimH * 0.5)),
			Color.White,
			(float)zoom,
			0.0f,
			SpriteEffects.None);
	}

	private void DrawPlayerIndicator()
	{
		if (FocusedChart == null || !FocusedChart.IsMultiPlayer())
			return;
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return;
		var arrowGraphicManager = focusedChartData.GetArrowGraphicManager();
		if (arrowGraphicManager == null)
			return;
		var player = GetPlayer(FocusedChart);
		var rimTextureId = ArrowGraphicManager.GetPlayerMarkerRimTexture();
		var (rimW, rimH) = TextureAtlas.GetDimensions(rimTextureId);
		var (fillTextureId, color) = arrowGraphicManager.GetPlayerMarkerFillTexture(player);
		var (fillW, fillH) = TextureAtlas.GetDimensions(fillTextureId);

		var zoom = ZoomManager.GetSizeZoom();
		var x = focusedChartData.GetScreenSpaceXOfLanesEndWithCurrentScale() + rimW * 0.5 * zoom;
		var y = GetFocalPointScreenSpaceY();

		// Draw fill.
		TextureAtlas.Draw(
			fillTextureId,
			SpriteBatch,
			new Vector2((float)x, y),
			new Vector2((float)(fillW * 0.5), (float)(fillH * 0.5)),
			color,
			(float)zoom,
			0.0f,
			SpriteEffects.None);

		// Draw rim.
		TextureAtlas.Draw(
			rimTextureId,
			SpriteBatch,
			new Vector2((float)x, y),
			new Vector2((float)(rimW * 0.5), (float)(rimH * 0.5)),
			Color.White,
			(float)zoom,
			0.0f,
			SpriteEffects.None);
	}

	private void DrawReceptorForegroundEffects()
	{
		var sizeZoom = ZoomManager.GetSizeZoom();
		for (var i = 0; i < ActiveCharts.Count; i++)
			ActiveChartData[i].DrawReceptorForegroundEffects(sizeZoom, TextureAtlas, SpriteBatch);
	}

	private void DrawWaveForm()
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		if (!p.ShowWaveForm || !p.EnableWaveForm)
			return;

		if (ActiveCharts.Count == 0)
			return;

		if (p.AntiAlias)
		{
			// Configure FXAA.
			FxaaEffect.CurrentTechnique = FxaaEffect.Techniques["fxaa"];
			FxaaEffect.Parameters["fxaaQualitySubpix"].SetValue(p.AntiAliasSubpix);
			FxaaEffect.Parameters["fxaaQualityEdgeThreshold"].SetValue(p.AntiAliasEdgeThreshold);
			FxaaEffect.Parameters["fxaaQualityEdgeThresholdMin"].SetValue(p.AntiAliasEdgeThresholdMin);
			FxaaEffect.Parameters["inverseRenderTargetWidth"].SetValue(1.0f / WaveformRenderTarget.Width);
			FxaaEffect.Parameters["inverseRenderTargetHeight"].SetValue(1.0f / WaveformRenderTarget.Height);
			FxaaEffect.Parameters["renderTargetTexture"].SetValue(WaveformRenderTarget);

			// Draw the recolored waveform with antialiasing to the back buffer.
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, FxaaEffect);
			DrawWaveFormRenderTargetToSpriteBatch();
			SpriteBatch.End();
		}
		else
		{
			// Draw the recolored waveform to the back buffer.
			SpriteBatch.Begin();
			DrawWaveFormRenderTargetToSpriteBatch();
			SpriteBatch.End();
		}
	}

	private void DrawWaveFormRenderTargetToSpriteBatch()
	{
		foreach (var activeChart in ActiveChartData)
			if (activeChart.ShouldDrawWaveForm())
				DrawWaveFormRenderTargetToSpriteBatch(activeChart);
	}

	private void DrawWaveFormRenderTargetToSpriteBatch(ActiveEditorChart activeChart)
	{
		if (!activeChart.IsVisible())
			return;
		var width = (int)GetWaveFormWidth(activeChart);
		var x = activeChart.GetFocalPointX() - (width >> 1);
		SpriteBatch.Draw(WaveformRenderTarget,
			new Rectangle(x, TransformChartSpaceYToScreenSpaceY(0), width, WaveformRenderTarget.Height), Color.White);
	}

	private void DrawActiveChartBoundaries()
	{
		var y = ChartArea.Y;
		var w = GetActiveChartBoundaryWidth();
		var h = ChartArea.Height;
		foreach (var activeChartData in ActiveChartData)
		{
			if (!activeChartData.IsVisible())
				continue;
			var textureId = activeChartData.IsFocused() ? TextureIdFocusedChartBoundary : TextureIdUnfocusedChartBoundary;
			var rect = new RectangleF(activeChartData.GetScreenSpaceXOfFullChartAreaStart(), y, w, h);
			TextureAtlas.Draw(textureId, SpriteBatch, rect);
			rect = new RectangleF(activeChartData.GetScreenSpaceXOfFullChartAreaEnd() - w, y, w, h);
			TextureAtlas.Draw(textureId, SpriteBatch, rect);
		}
	}

	private void DrawMeasureMarkers()
	{
		if (!Preferences.Instance.PreferencesOptions.RenderMarkers)
			return;

		foreach (var activeChartData in ActiveChartData)
		{
			if (!activeChartData.IsVisible())
				continue;
			foreach (var visibleMarker in activeChartData.GetVisibleMarkers())
			{
				visibleMarker.Draw(TextureAtlas, SpriteBatch, Font);
			}
		}
	}

	private void DrawRegions(bool selections)
	{
		if (!selections && !Preferences.Instance.PreferencesOptions.RenderRegions)
			return;

		foreach (var activeChartData in ActiveChartData)
		{
			if (!activeChartData.IsVisible())
				continue;
			foreach (var visibleRegion in activeChartData.GetVisibleRegions())
			{
				if (selections != visibleRegion.IsRegionSelection())
					continue;
				visibleRegion.DrawRegion(TextureAtlas, SpriteBatch, GetViewportHeight());
			}
		}
	}

	private void DrawSelectedMouseRegion()
	{
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return;
		var mouseRegion = focusedChartData.GetSelectedMouseRegion();
		if (mouseRegion?.IsActive() ?? false)
			mouseRegion.DrawRegion(TextureAtlas, SpriteBatch, GetViewportHeight());
	}

	private void DrawChartEvents()
	{
		var renderMiscEvents = Preferences.Instance.PreferencesOptions.RenderMiscEvents;
		var renderNotes = Preferences.Instance.PreferencesOptions.RenderNotes;
		if (!renderMiscEvents && !renderNotes)
			return;

		var focalPointScreenSpaceY = GetFocalPointScreenSpaceY();

		foreach (var activeChartData in ActiveChartData)
		{
			if (!activeChartData.IsVisible())
				continue;

			var eventsBeingEdited = new List<EditorEvent>();
			var arrowGraphicManager = activeChartData.GetArrowGraphicManager();

			foreach (var visibleEvent in activeChartData.GetVisibleEvents())
			{
				if (!renderMiscEvents && visibleEvent.IsMiscEvent())
					continue;

				if (!renderNotes && !visibleEvent.IsMiscEvent())
					continue;

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
					    && visibleEvent.GetEndChartTime() < activeChartData.Position.ChartTime
					    && visibleEvent.IsConsumedByReceptors())
						continue;

					// Cut off hold end notes which intersect the receptors.
					if (visibleEvent is EditorHoldNoteEvent hold)
					{
						if (Playing && hold.IsConsumedByReceptors()
						            && hold.GetEndChartTime() > activeChartData.Position.ChartTime
						            && hold.GetChartTime() < activeChartData.Position.ChartTime)
						{
							hold.SetNextDrawActive(true, focalPointScreenSpaceY);
						}
					}
				}

				// Draw the event.
				visibleEvent.Draw(TextureAtlas, SpriteBatch, arrowGraphicManager);
			}

			// Draw events being edited.
			foreach (var visibleEvent in eventsBeingEdited)
			{
				visibleEvent.Draw(TextureAtlas, SpriteBatch, arrowGraphicManager);
			}
		}
	}

	#endregion Drawing

	#region Chart Update

	private void UpdateChartPositions()
	{
		var focalPointX = GetFocalPointScreenSpaceX();
		var focalPointY = GetFocalPointScreenSpaceY();

		var focusedChartIndex = 0;
		ActiveEditorChart focusedChartData = null;
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (ActiveCharts[i] == FocusedChart)
			{
				focusedChartData = ActiveChartData[i];
				focusedChartIndex = i;
				break;
			}
		}

		// Focused Chart.
		var xLeftOfFocusedChart = 0;
		var xRightOfFocusedChart = 0;
		if (focusedChartData != null)
		{
			focusedChartData.SetFocalPoint(focalPointX, focalPointY);
			xLeftOfFocusedChart = focusedChartData.GetScreenSpaceXOfFullChartAreaStart();
			xRightOfFocusedChart = focusedChartData.GetScreenSpaceXOfFullChartAreaEnd();
		}

		// Charts to the left of the focused chart.
		var x = xLeftOfFocusedChart;
		for (var i = focusedChartIndex - 1; i >= 0; i--)
		{
			var fullWidth = ActiveChartData[i].GetChartScreenSpaceWidth();
			var width = ActiveChartData[i].GetLaneAndWaveFormAreaWidth();
			var xOffset = ActiveChartData[i].GetRelativeXPositionOfLanesAndWaveFormFromChartArea();
			ActiveChartData[i].SetFocalPoint(x - fullWidth + xOffset + (width >> 1), focalPointY);
			x -= fullWidth;
		}

		// Charts to the right of the focused chart.
		x = xRightOfFocusedChart;
		for (var i = focusedChartIndex + 1; i < ActiveCharts.Count; i++)
		{
			var fullWidth = ActiveChartData[i].GetChartScreenSpaceWidth();
			var width = ActiveChartData[i].GetLaneAndWaveFormAreaWidth();
			var xOffset = ActiveChartData[i].GetRelativeXPositionOfLanesAndWaveFormFromChartArea();
			ActiveChartData[i].SetFocalPoint(x + xOffset + (width >> 1), focalPointY);
			x += fullWidth;
		}
	}

	private (int, int) GetActiveChartsXAndWidth()
	{
		if (ActiveChartData.Count == 0)
			return (0, 0);
		var x = ActiveChartData[0].GetScreenSpaceXOfFullChartAreaStart();
		var finalX = ActiveChartData[^1].GetScreenSpaceXOfFullChartAreaEnd();
		return (x, finalX - x);
	}

	private void UpdateChartEvents()
	{
		var screenHeight = GetViewportHeight();
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (ActiveChartData[i].IsVisible())
			{
				ActiveChartData[i].UpdateRowSelection();
				ActiveChartData[i].UpdateChartEvents(screenHeight);
			}
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
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return (0.0, 0.0);
		return focusedChartData.FindChartTimeAndRowForScreenSpaceY(desiredScreenY);
	}

	#endregion Chart Update

	#region Autoplay

	private void UpdateAutoPlay()
	{
		if (!Playing)
			return;
		foreach (var activeChartData in ActiveChartData)
			activeChartData.UpdateAutoPlayer();
	}

	private void UpdateAutoPlayFromScrolling()
	{
		foreach (var activeChartData in ActiveChartData)
			activeChartData.StopAutoPlayer();
	}

	#endregion Autoplay

	#region MiniMap

	private void ProcessInputForMiniMap()
	{
		if (!Preferences.Instance.PreferencesMiniMap.ShowMiniMap)
			return;

		var pScroll = Preferences.Instance.PreferencesScroll;
		var focalPointY = GetFocalPointScreenSpaceY();

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

		// Set the Song Position based on the scroll bar position.
		MiniMapCapturingMouse |= miniMapNeedsMouseThisFrame;
		if (MiniMapCapturingMouse)
		{
			// When moving the scroll bar, pause or stop playback.
			if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame() && Playing)
			{
				// Set a flag to unpause playback unless the preference is to completely stop when scrolling.
				StartPlayingWhenMouseScrollingDone = !pScroll.StopPlaybackWhenDraggingScrollBars;
				StopPlayback();
			}

			// Set the music position based off of the scroll bar position.
			var editorPosition = MiniMap.GetEditorPosition();
			switch (GetMiniMapSpacingMode())
			{
				case SpacingMode.ConstantTime:
				{
					SetChartTime(editorPosition +
					             focalPointY / (pScroll.TimeBasedPixelsPerSecond * ZoomManager.GetSpacingZoom()));
					break;
				}
				case SpacingMode.ConstantRow:
				{
					SetChartPosition(editorPosition +
					                 focalPointY / (pScroll.RowBasedPixelsPerRow * ZoomManager.GetSpacingZoom()));
					break;
				}
			}

			UpdateAutoPlayFromScrolling();
		}

		// When letting go, start playing again.
		if (miniMapCapturingMouseLastFrame && !MiniMapCapturingMouse && StartPlayingWhenMouseScrollingDone)
		{
			StartPlayingWhenMouseScrollingDone = false;
			StartPlayback();
		}
	}

	private void UpdateMiniMapBounds()
	{
		var p = Preferences.Instance;
		var x = 0;
		switch (p.PreferencesMiniMap.MiniMapPosition)
		{
			case Position.RightSideOfWindow:
			{
				x = ChartArea.X + ChartArea.Width - p.PreferencesMiniMap.GetPositionOffsetUiScaled() -
				    p.PreferencesMiniMap.GetMiniMapWidthScaled();
				break;
			}
			case Position.LeftSideOfWindow:
			{
				x = ChartArea.X + p.PreferencesMiniMap.GetPositionOffsetUiScaled();
				break;
			}
			case Position.FocusedChartWithoutScaling:
			{
				var focusedChartData = GetFocusedChartData();
				if (focusedChartData != null)
					x = focusedChartData.GetScreenSpaceXOfMiscEventsEnd() + p.PreferencesMiniMap.GetPositionOffsetUiScaled();
				break;
			}
			case Position.FocusedChartWithScaling:
			{
				var focusedChartData = GetFocusedChartData();
				if (focusedChartData != null)
					x = focusedChartData.GetScreenSpaceXOfMiscEventsEndWithCurrentScale() +
					    p.PreferencesMiniMap.GetPositionOffsetUiScaled();
				break;
			}
		}

		var y = TransformChartSpaceYToScreenSpaceY(GetMiniMapYPaddingFromTopInChartSpace());
		var textureHeight = Math.Max(0,
			GetViewportHeight() - GetMiniMapYPaddingFromTopInScreenSpace() - GetMiniMapYPaddingFromBottom());
		var visibleHeight = (uint)Math.Min(textureHeight,
			Math.Max(0, ChartArea.Height - GetMiniMapYPaddingFromTopInChartSpace() - GetMiniMapYPaddingFromBottom()));

		MiniMap.UpdateBounds(
			GraphicsDevice,
			new Rectangle(x, y, p.PreferencesMiniMap.GetMiniMapWidthScaled(), textureHeight),
			visibleHeight);
	}

	private void UpdateMiniMapSpacing()
	{
		var p = Preferences.Instance.PreferencesMiniMap;
		MiniMap.SetShouldQuantizePositions(p.QuantizePositions);
		MiniMap.SetLaneSpacing(p.MiniMapNoteWidth, p.MiniMapNoteSpacing);
		MiniMap.SetPatternWidth(p.PatternsWidth);
		MiniMap.SetPreviewWidth(p.PreviewWidth);
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

		var chartData = GetFocusedChartData();
		var arrowGraphicManager = chartData?.GetArrowGraphicManager();
		if (FocusedChart?.GetEvents() == null || chartData == null || arrowGraphicManager == null)
		{
			MiniMap.UpdateNoChart();
			return;
		}

		UpdateMiniMapBounds();
		UpdateMiniMapSpacing();

		var pScroll = Preferences.Instance.PreferencesScroll;

		MiniMap.SetSelectMode(pMiniMap.MiniMapSelectMode);
		MiniMap.SetNumLanes((uint)FocusedChart.NumInputs);

		var position = GetPosition();
		var screenHeight = GetViewportHeight();
		var spacingZoom = ZoomManager.GetSpacingZoom();
		var chartTime = position.ChartTime;
		var chartPosition = position.ChartPosition;
		var spaceByRow = GetMiniMapSpacingMode() == SpacingMode.ConstantRow;

		// Configure the region to show in the mini map.
		if (!spaceByRow)
		{
			// Editor Area. The visible time range.
			var pps = pScroll.TimeBasedPixelsPerSecond * spacingZoom;
			var editorAreaTimeStart = chartTime - GetFocalPointScreenSpaceY() / pps;
			var editorAreaTimeEnd = editorAreaTimeStart + screenHeight / pps;
			var editorAreaTimeRange = editorAreaTimeEnd - editorAreaTimeStart;

			// Determine the end time.
			var maxTimeFromChart = FocusedChart.GetEndChartTime();

			// Full Area. The time from the chart, extended in both directions by the editor range.
			var fullAreaTimeStart = FocusedChart.GetStartChartTime() - editorAreaTimeRange;
			var fullAreaTimeEnd = maxTimeFromChart + editorAreaTimeRange;

			// Content Area. The time from the chart.
			var contentAreaTimeStart = FocusedChart.GetStartChartTime();
			var contentAreaTimeEnd = maxTimeFromChart;

			// Update the MiniMap with the ranges.
			MiniMap.UpdateBegin(
				fullAreaTimeStart, fullAreaTimeEnd,
				contentAreaTimeStart, contentAreaTimeEnd,
				pMiniMap.MiniMapVisibleTimeRange,
				editorAreaTimeStart, editorAreaTimeEnd,
				chartTime,
				arrowGraphicManager);
		}
		else
		{
			// Editor Area. The visible row range.
			var ppr = pScroll.RowBasedPixelsPerRow * spacingZoom;
			var editorAreaRowStart = chartPosition - GetFocalPointScreenSpaceY() / ppr;
			var editorAreaRowEnd = editorAreaRowStart + screenHeight / ppr;
			var editorAreaRowRange = editorAreaRowEnd - editorAreaRowStart;

			// Determine the end row.
			var maxRowFromChart = FocusedChart.GetEndPosition();

			// Full Area. The area from the chart, extended in both directions by the editor range.
			var fullAreaRowStart = 0.0 - editorAreaRowRange;
			var fullAreaRowEnd = maxRowFromChart + editorAreaRowRange;

			// Content Area. The rows from the chart.
			var contentAreaTimeStart = 0.0;
			var contentAreaTimeEnd = maxRowFromChart;

			// Update the MiniMap with the ranges.
			MiniMap.SetNumLanes((uint)FocusedChart.NumInputs);
			MiniMap.UpdateBegin(
				fullAreaRowStart, fullAreaRowEnd,
				contentAreaTimeStart, contentAreaTimeEnd,
				pMiniMap.MiniMapVisibleRowRange,
				editorAreaRowStart, editorAreaRowEnd,
				chartPosition,
				arrowGraphicManager);
		}

		// Set the chartPosition to the top of the area so we can use it for scanning down for notes to add.
		if (spaceByRow)
			chartPosition = MiniMap.GetMiniMapAreaStart();
		else
			FocusedChart.TryGetChartPositionFromTime(MiniMap.GetMiniMapAreaStart(), ref chartPosition);

		// Add patterns.
		if (pMiniMap.ShowPatterns)
		{
			var patterns = FocusedChart.GetPatterns();
			var patternEnumerator = patterns.FindBestByPosition(chartPosition);
			EditorPatternEvent eventNeedingToBeAdded = null;
			var eventNeedingToBeAddedEndRow = 0;
			var eventNeedingToBeAddedEndChartTime = 0.0;
			while (patternEnumerator != null && patternEnumerator.MoveNext())
			{
				var currentEvent = patternEnumerator.Current!;

				// Check if a pattern currently under consideration should be combined into this one.
				if (eventNeedingToBeAdded != null)
				{
					// If the pattern under consideration aligns with this pattern, extend it.
					if (eventNeedingToBeAddedEndRow == currentEvent.GetRow())
					{
						eventNeedingToBeAddedEndRow = currentEvent.GetEndRow();
						eventNeedingToBeAddedEndChartTime = currentEvent.GetEndChartTime();
						continue;
					}

					// Otherwise, complete the pattern and continue.
					if (MiniMap.AddPattern(
						    spaceByRow ? eventNeedingToBeAdded.GetRow() : eventNeedingToBeAdded.GetChartTime(),
						    spaceByRow ? eventNeedingToBeAddedEndRow : eventNeedingToBeAddedEndChartTime) ==
					    AddResult.BelowBottom)
						break;
				}

				// Record information about this pattern but do not submit it yet.
				// We want to check the pattern to see if it lines up with this one.
				eventNeedingToBeAdded = patternEnumerator.Current!;
				eventNeedingToBeAddedEndRow = eventNeedingToBeAdded.GetEndRow();
				eventNeedingToBeAddedEndChartTime = eventNeedingToBeAdded.GetEndChartTime();
			}

			// When done looping checking for completing any current pattern under consideration.
			if (eventNeedingToBeAdded != null)
			{
				MiniMap.AddPattern(
					spaceByRow ? eventNeedingToBeAdded.GetRow() : eventNeedingToBeAdded.GetChartTime(),
					spaceByRow ? eventNeedingToBeAddedEndRow : eventNeedingToBeAddedEndChartTime);
			}
		}

		// Add preview.
		if (pMiniMap.ShowPreview)
		{
			var preview = FocusedChart.GetPreview();
			if (spaceByRow)
				MiniMap.AddPreview(preview.GetChartPosition(), preview.GetEndChartPosition());
			else
				MiniMap.AddPreview(preview.GetChartTime(), preview.GetEndChartTime());
		}

		// Add labels.
		if (pMiniMap.ShowLabels)
		{
			var labels = FocusedChart.GetLabels();
			var labelEnumerator = labels.FindBestByPosition(chartPosition);
			while (labelEnumerator != null && labelEnumerator.MoveNext())
			{
				// Don't add labels at row 0. They don't look good over the song start line in the mini map.
				if (labelEnumerator.Current!.GetRow() == 0)
					continue;
				if (MiniMap.AddLabel(spaceByRow
					    ? labelEnumerator.Current!.GetChartPosition()
					    : labelEnumerator.Current!.GetChartTime()) == AddResult.BelowBottom)
					break;
			}
		}

		// Add notes.
		var enumerator = FocusedChart.GetEvents().FindBestByPosition(chartPosition);
		if (enumerator == null)
		{
			MiniMap.UpdateEnd();
			return;
		}

		var numNotesAdded = 0;
		var maxNotesToAdd = Preferences.Instance.PreferencesOptions.MiniMapMaxNotesToDraw;

		// Scan backwards until we have checked every lane for a long note which may
		// be extending through the given start row.
		var holdStartNotes = chartData.ScanBackwardsForHolds(enumerator, chartPosition);
		foreach (var hsn in holdStartNotes)
		{
			MiniMap.AddHold(
				hsn,
				spaceByRow ? hsn.GetChartPosition() : hsn.GetChartTime(),
				spaceByRow ? hsn.GetEndChartPosition() : hsn.GetEndChartTime(),
				hsn.IsRoll(),
				hsn.IsSelected());
			numNotesAdded++;
		}

		// Add normal notes.
		while (enumerator.MoveNext())
		{
			var e = enumerator.Current;
			if (e is EditorTapNoteEvent or EditorFakeNoteEvent or EditorLiftNoteEvent)
			{
				numNotesAdded++;
				if (MiniMap.AddTapNote(e, spaceByRow ? e.GetChartPosition() : e.GetChartTime(),
					    e.IsSelected()) ==
				    AddResult.BelowBottom)
					break;
			}
			else if (e is EditorMineNoteEvent mine)
			{
				numNotesAdded++;
				if (MiniMap.AddMine(mine, spaceByRow ? e.GetChartPosition() : e.GetChartTime(),
					    e.IsSelected()) ==
				    AddResult.BelowBottom)
					break;
			}
			else if (e is EditorHoldNoteEvent hold)
			{
				numNotesAdded++;
				if (MiniMap.AddHold(
					    hold,
					    spaceByRow ? hold.GetChartPosition() : hold.GetChartTime(),
					    spaceByRow ? hold.GetEndChartPosition() : hold.GetEndChartTime(),
					    hold.IsRoll(),
					    hold.IsSelected()) == AddResult.BelowBottom)
					break;
			}

			if (numNotesAdded > maxNotesToAdd)
				break;
		}

		MiniMap.UpdateEnd();
	}

	private void DrawMiniMap()
	{
		if (!Preferences.Instance.PreferencesMiniMap.ShowMiniMap)
			return;
		if (FocusedChart == null)
			return;
		if (MiniMapRenderTarget == null)
			return;
		SpriteBatch.Draw(MiniMapRenderTarget,
			new Rectangle(MiniMap.GetX(), MiniMap.GetY(), MiniMapRenderTarget.Width, MiniMapRenderTarget.Height), Color.White);
	}

	#endregion MiniMap

	#region Density Graph

	private void UpdateDensityGraph()
	{
		UpdateDensityGraphBounds();

		var screenHeight = GetViewportHeight();
		var spacingZoom = ZoomManager.GetSpacingZoom();
		var chartTime = GetPosition().ChartTime;
		var pps = Preferences.Instance.PreferencesScroll.TimeBasedPixelsPerSecond * spacingZoom;
		var timeStart = chartTime - GetFocalPointScreenSpaceY() / pps;
		var timeEnd = timeStart + screenHeight / pps;
		DensityGraph.Update(timeStart, timeEnd, chartTime);
	}

	private void UpdateDensityGraphBounds()
	{
		var p = Preferences.Instance.PreferencesDensityGraph;
		var x = 0;
		var y = 0;
		var w = 0;
		var h = 0;
		var orientation = StepDensityEffect.Orientation.Vertical;
		switch (p.DensityGraphPositionValue)
		{
			case PreferencesDensityGraph.DensityGraphPosition.RightSideOfWindow:
			{
				w = p.GetDensityGraphHeightUiScaled();
				x = ChartArea.X + ChartArea.Width - w - p.GetDensityGraphPositionOffsetUiScaled();
				h = ChartArea.Height - GetChartHeaderHeight() + p.GetDensityGraphWidthOffsetUiScaled() * 2;
				y = ChartArea.Y + GetChartHeaderHeight() - p.GetDensityGraphWidthOffsetUiScaled();
				break;
			}
			case PreferencesDensityGraph.DensityGraphPosition.LeftSideOfWindow:
			{
				w = p.GetDensityGraphHeightUiScaled();
				x = ChartArea.X + p.GetDensityGraphPositionOffsetUiScaled();
				h = ChartArea.Height - GetChartHeaderHeight() + p.GetDensityGraphWidthOffsetUiScaled() * 2;
				y = ChartArea.Y + GetChartHeaderHeight() - p.GetDensityGraphWidthOffsetUiScaled();
				break;
			}
			case PreferencesDensityGraph.DensityGraphPosition.FocusedChartWithoutScaling:
			{
				var focusedChartData = GetFocusedChartData();
				if (focusedChartData != null)
					x = focusedChartData.GetScreenSpaceXOfMiscEventsEnd() + p.GetDensityGraphPositionOffsetUiScaled();
				w = p.GetDensityGraphHeightUiScaled();
				h = ChartArea.Height - GetChartHeaderHeight() + p.GetDensityGraphWidthOffsetUiScaled() * 2;
				y = ChartArea.Y + GetChartHeaderHeight() - p.GetDensityGraphWidthOffsetUiScaled();
				break;
			}
			case PreferencesDensityGraph.DensityGraphPosition.FocusedChartWithScaling:
			{
				var focusedChartData = GetFocusedChartData();
				if (focusedChartData != null)
					x = focusedChartData.GetScreenSpaceXOfMiscEventsEndWithCurrentScale() +
					    p.GetDensityGraphPositionOffsetUiScaled();
				w = p.GetDensityGraphHeightUiScaled();
				h = ChartArea.Height - GetChartHeaderHeight() + p.GetDensityGraphWidthOffsetUiScaled() * 2;
				y = ChartArea.Y + GetChartHeaderHeight() - p.GetDensityGraphWidthOffsetUiScaled();
				break;
			}
			case PreferencesDensityGraph.DensityGraphPosition.TopOfFocusedChart:
			{
				var focusedChartData = GetFocusedChartData();
				if (focusedChartData != null)
				{
					var x1 = focusedChartData.GetScreenSpaceXOfLanesStart();
					var x2 = focusedChartData.GetScreenSpaceXOfLanesEnd();
					x = x1 - p.GetDensityGraphWidthOffsetUiScaled();
					w = x2 - x1 + p.GetDensityGraphWidthOffsetUiScaled() * 2;
				}

				y = ChartArea.Y + GetChartHeaderHeight() + p.GetDensityGraphPositionOffsetUiScaled();
				h = p.GetDensityGraphHeightUiScaled();
				orientation = StepDensityEffect.Orientation.Horizontal;
				break;
			}
			case PreferencesDensityGraph.DensityGraphPosition.BottomOfFocusedChart:
			{
				var focusedChartData = GetFocusedChartData();
				if (focusedChartData != null)
				{
					var x1 = focusedChartData.GetScreenSpaceXOfLanesStart();
					var x2 = focusedChartData.GetScreenSpaceXOfLanesEnd();
					x = x1 - p.GetDensityGraphWidthOffsetUiScaled();
					w = x2 - x1 + p.GetDensityGraphWidthOffsetUiScaled() * 2;
				}

				y = ChartArea.Y + ChartArea.Height - p.GetDensityGraphHeightUiScaled() -
				    p.GetDensityGraphPositionOffsetUiScaled();
				h = p.GetDensityGraphHeightUiScaled();
				orientation = StepDensityEffect.Orientation.Horizontal;
				break;
			}
		}

		DensityGraph.UpdateBounds(new Rectangle(x, y, w, h), orientation);
	}

	private void ProcessInputForDensityGraph()
	{
		if (!Preferences.Instance.PreferencesDensityGraph.ShowDensityGraph)
			return;

		var pScroll = Preferences.Instance.PreferencesScroll;

		var densityGraphCapturingMouseLastFrame = DensityGraphCapturingMouse;

		var densityGraphNeedsMouseThisFrame = false;
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame())
		{
			densityGraphNeedsMouseThisFrame = DensityGraph.MouseDown(EditorMouseState.X(), EditorMouseState.Y());
		}

		DensityGraph.MouseMove(EditorMouseState.X(), EditorMouseState.Y());
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).UpThisFrame()
		    || (DensityGraphCapturingMouse && EditorMouseState.GetButtonState(EditorMouseState.Button.Left).Up()))
		{
			DensityGraph.MouseUp(EditorMouseState.X(), EditorMouseState.Y());
		}

		DensityGraphCapturingMouse = DensityGraph.WantsMouse();

		// Set the Song Position based on the scroll bar position.
		DensityGraphCapturingMouse |= densityGraphNeedsMouseThisFrame;
		if (DensityGraphCapturingMouse)
		{
			// When moving the scroll bar, pause or stop playback.
			if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame() && Playing)
			{
				// Set a flag to unpause playback unless the preference is to completely stop when scrolling.
				StartPlayingWhenMouseScrollingDone = !pScroll.StopPlaybackWhenDraggingScrollBars;
				StopPlayback();
			}

			// Set the music position based off of the scroll bar time.
			SetChartTime(DensityGraph.GetTimeFromScrollBar());

			UpdateAutoPlayFromScrolling();
		}

		// When letting go, start playing again.
		if (densityGraphCapturingMouseLastFrame && !DensityGraphCapturingMouse && StartPlayingWhenMouseScrollingDone)
		{
			StartPlayingWhenMouseScrollingDone = false;
			StartPlayback();
		}
	}

	private void DrawDensityGraph()
	{
		if (FocusedChart == null)
			return;
		DensityGraph.Draw();
	}

	public double GetActiveChartPeakNPS()
	{
		return DensityGraph.GetPeakNps();
	}

	public double GetActiveChartPeakRPS()
	{
		return DensityGraph.GetPeakRps();
	}

	#endregion Density Graph

	#region Gui Rendering

	private void DrawGui()
	{
		DrawMainMenuUI();

		UIFTUE.Draw();

#if DEBUG
		UIDebug.Instance.Draw();
#endif
		UIPerformance.Instance.Draw();
		DrawImGuiTestWindow();

		UIAbout.Instance.Draw();
		UIControls.Instance.Draw();
		UILog.Instance.Draw(LogBuffer, LogBufferLock, LogFilePath);
		UIScrollPreferences.Instance.Draw();
		UISelectionPreferences.Instance.Draw();
		UIWaveFormPreferences.Instance.Draw();
		UIDarkPreferences.Instance.Draw();
		UIMiniMapPreferences.Instance.Draw();
		UIReceptorPreferences.Instance.Draw();
		UIOptions.Instance.Draw();
		UIAudioPreferences.Instance.Draw();
		UIStreamPreferences.Instance.Draw();
		UIDensityGraphPreferences.Instance.Draw();
		UINoteColorPreferences.Instance.Draw();

		UISongProperties.Instance.Draw(ActiveSong);
		UIChartProperties.Instance.Draw(FocusedChart);
		UIPackProperties.Instance.Draw();
		UIChartList.Instance.Draw(ActiveSong, FocusedChart);
		UIExpressedChartConfig.Instance.Draw();
		UIPerformedChartConfig.Instance.Draw();
		UIPatternConfig.Instance.Draw();
		UIAutogenConfigs.Instance.Draw();
		UIAutogenChart.Instance.Draw();
		UIAutogenChartsForChartType.Instance.Draw();
		UICopyEventsBetweenCharts.Instance.Draw();
		UIPatternEvent.Instance.Draw(GetFocusedChartData()?.GetLastSelectedPatternEvent());
		UIAttackEvent.Instance.Draw(GetFocusedChartData()?.GetLastSelectedAttackEvent());
		UIHotbar.Instance.Draw();

		if (CanShowRightClickPopupThisFrame && EditorMouseState.GetButtonState(EditorMouseState.Button.Right).UpThisFrame())
		{
			ImGui.OpenPopup("RightClickPopup");
		}

		var lastPos = EditorMouseState.GetButtonState(EditorMouseState.Button.Right).GetLastClickUpPosition();
		DrawRightClickMenu((int)lastPos.X, (int)lastPos.Y);

		UIModals.Draw(PlatformInterface);
	}

	private void DrawMainMenuUI()
	{
		var p = Preferences.Instance;
		var keyBinds = p.PreferencesKeyBinds;
		var canEdit = CanEdit();
		var hasSong = ActiveSong != null;
		var hasChart = FocusedChart != null;
		var canEditSong = canEdit && hasSong;
		var canOpen = CanLoadSongs();
		if (ImGui.BeginMainMenuBar())
		{
			if (ImGui.BeginMenu("File"))
			{
				if (ImGui.MenuItem("New Song", UIControls.GetCommandString(keyBinds.New), false, canOpen))
				{
					OnNew();
				}

				ImGui.Separator();
				if (ImGui.MenuItem("Open", UIControls.GetCommandString(keyBinds.Open), false, canOpen))
				{
					OnOpen();
				}

				if (ImGui.BeginMenu("Open Recent", p.RecentFiles.Count > 0))
				{
					if (ImGui.BeginTable("##OpenRecentTable", 3, ImGuiTableFlags.Borders))
					{
						ImGui.TableSetupColumn("File Name");
						ImGui.TableSetupColumn("Song Title");
						ImGui.TableSetupColumn("Pack");
						ImGui.TableHeadersRow();

						var openRecentFile = false;
						for (var i = 0; i < p.RecentFiles.Count; i++)
						{
							var recentFile = p.RecentFiles[i];

							ImGui.TableNextRow();
							ImGui.TableSetColumnIndex(0);
							if (ImGui.Selectable($"{recentFile.GetFileName() ?? ""}##OpenRecentTable{i}", false,
								    ImGuiSelectableFlags.SpanAllColumns))
							{
								OpenRecentIndex = i;
								openRecentFile = true;
							}

							ImGui.TableSetColumnIndex(1);
							ImGui.TextUnformatted(recentFile.GetSongName() ?? "");

							ImGui.TableSetColumnIndex(2);
							ImGui.TextUnformatted(recentFile.GetPackName() ?? "");
						}

						if (openRecentFile)
							OnOpenRecentFile();

						ImGui.EndTable();
					}

					ImGui.EndMenu();
				}

				if (ImGui.MenuItem("Open Containing Folder", UIControls.GetCommandString(keyBinds.OpenContainingFolder), false,
					    hasSong))
					OnOpenContainingFolder();
				if (ImGui.MenuItem("Reload", UIControls.GetCommandString(keyBinds.Reload), false,
					    canOpen && canEditSong && p.RecentFiles.Count > 0))
					OnReload();
				if (ImGui.MenuItem("Close", UIControls.GetCommandString(keyBinds.Close), false, canEditSong))
					OnClose();

				ImGui.Separator();
				var editorFileName = ActiveSong?.GetFileName();
				if (!string.IsNullOrEmpty(editorFileName))
				{
					if (ImGui.MenuItem($"Save {editorFileName}", UIControls.GetCommandString(keyBinds.Save), false, canEditSong))
						OnSave();
				}
				else
				{
					if (ImGui.MenuItem("Save", UIControls.GetCommandString(keyBinds.Save), false, canEditSong))
						OnSave();
				}

				if (ImGui.MenuItem("Save As...", UIControls.GetCommandString(keyBinds.SaveAs), false, canEditSong))
					OnSaveAs();

				var hasPackFile = ActivePack?.GetItgManiaPack() != null;
				if (ImGui.MenuItem($"Save {ItgManiaPack.FileName}", UIControls.GetCommandString(keyBinds.SavePackFile), false,
					    hasPackFile))
				{
					ActivePack?.SaveItgManiaPack(false);
				}

				if (ImGui.BeginMenu("Advanced Save Options"))
				{
					var titleColumnWidth = UiScaled(220);
					if (ImGuiLayoutUtils.BeginTable("AdvancedSaveOptionsTable", titleColumnWidth))
					{
						// ReSharper disable StringLiteralTypo
						ImGuiLayoutUtils.DrawRowCheckbox(true, "Require Identical Timing in SM Files", Preferences.Instance,
							nameof(Preferences.RequireIdenticalTimingInSmFiles), false,
							"In sm files, many kinds of events must be identical between all charts. Most notably, all events" +
							" which affect timing must be identical but this also extends to other kinds of miscellaneous" +
							" events like attacks. If \"Require Identical Timing in SM Files\" is checked" +
							$" then when saving sm files {GetAppName()} will treat any discrepancies in these events between" +
							" charts as an error and will fail to save the chart. If this option is not checked then discrepancies" +
							" will be treated as a warning and the song's Timing Chart will be used for these events in every chart." +
							" Note that if this option is unchecked then for songs with different timing events between" +
							" charts it can result in sm files with data loss and timing that does not match what you see" +
							$" in {GetAppName()}." +
							"\n\nThe specific events which much match in sm files are:" +
							"\nMusic Offset              (#OFFSET)" +
							"\nDisplay Tempo             (#DISPLAYBPM)" +
							"\nTime Signatures           (#TIMESIGNATURES)" +
							"\nTempos                    (#BPM)" +
							"\nStops                     (#STOPS)" +
							"\nDelays                    (#DELAYS)" +
							"\nTick Counts               (#TICKCOUNTS)" +
							"\nAttacks                   (#ATTACKS)" +
							"\n\nThe above list does not include events which are completely incompatible with sm files." +
							" Those events are:" +
							"\nCombo Multipliers         (#COMBOS)" +
							"\nWarps                     (#WARPS)" +
							"\nScroll Rates              (#SCROLLS)" +
							"\nInterpolated Scroll Rates (#SPEEDS)" +
							"\nFakes                     (#FAKES)" +
							"\nLabels                    (#LABELS)");
						// ReSharper restore StringLiteralTypo

						// ReSharper disable StringLiteralTypo
						ImGuiLayoutUtils.DrawRowCheckbox(true, "Remove Chart Timing", Preferences.Instance,
							nameof(Preferences.OmitChartTimingData), false,
							"If checked then individual charts will have their timing data omitted from their files." +
							" The timing data from the song's Timing Chart will be used and saved at the song level." +
							" This has no effect on sm files which are already limited to only using timing data specified" +
							" at the song level. Under normal circumstances this option is not recommended but if you" +
							" use Stepmania files for other applications which struggle with chart timing data or you" +
							" are working under additional restrictions to file format this option may be useful. " +
							"\n\nThe specific events that will be omitted are those which Stepmania treats as signaling " +
							"chart timing. These events are:" +
							"\nMusic offset              (#OFFSET)" +
							"\nDisplay Tempo             (#DISPLAYBPM)" +
							"\nTime Signatures           (#TIMESIGNATURES)" +
							"\nTempos                    (#BPM)" +
							"\nStops                     (#STOPS)" +
							"\nDelays                    (#DELAYS)" +
							"\nTick Counts               (#TICKCOUNTS)" +
							"\nCombo Multipliers         (#COMBOS)" +
							"\nWarps                     (#WARPS)" +
							"\nScroll Rates              (#SCROLLS)" +
							"\nInterpolated Scroll Rates (#SPEEDS)" +
							"\nFakes                     (#FAKES)" +
							"\nLabels                    (#LABELS)");
						// ReSharper restore StringLiteralTypo

						ImGuiLayoutUtils.DrawRowCheckbox(true, "Remove Custom Save Data", Preferences.Instance,
							nameof(Preferences.OmitCustomSaveData), false,
							$"{GetAppName()} saves custom data into sm/ssc files that Stepmania safely ignores." +
							$" This data is required for some {GetAppName()} functionality like Patterns, sync compensation" +
							" for assist tick and waveform visuals, and automatic chart generation." +
							" It is not recommended to remove this data as it can result in your files losing functionality" +
							$" and appearing out of sync in {GetAppName()}." +
							" However, checking this option will remove this data when saving." +
							" If you are working under restrictions to file format beyond normal Stepmania requirements" +
							" this option may be useful.");

						// ReSharper disable StringLiteralTypo
						ImGuiLayoutUtils.DrawRowCheckbox(true, "Anonymize Save Data", Preferences.Instance,
							nameof(Preferences.AnonymizeSaveData), false,
							"If checked then when saving, the following data will be omitted:" +
							"\nSong Credit        (#CREDIT)" +
							"\nSong Genre         (#GENRE)" +
							"\nSong Origin        (#ORIGIN)" +
							"\nSong Banner        (#BANNER)" +
							"\nSong Background    (#BACKGROUND)" +
							"\nSong CD Title      (#CDTITLE)" +
							"\nSong Jacket        (#JACKET)" +
							"\nSong CD Image      (#CDIMAGE)" +
							"\nSong Disc Image    (#DISCIMAGE)" +
							"\nSong Preview Video (#PREVIEWVID)" +
							"\nSong Lyrics        (#LYRICSPATH)" +
							"\nChart Name         (#CHARTNAME)" +
							"\nChart Description  (#DESCRIPTION)" +
							"\nChart Credit       (#CREDIT)" +
							"\nChart Style        (#CHARTSTYLE)" +
							"\n\nThis is intended for contest submissions which require anonymized files and expect" +
							" these fields to be blank.");
						// ReSharper restore StringLiteralTypo

						ImGuiLayoutUtils.DrawRowCheckbox(true, "Use StepF2 Format for Pump Routine", Preferences.Instance,
							nameof(Preferences.UseStepF2ForPumpRoutine), false,
							"StepF2 (a.k.a. StepP1) is a fork of Stepmania which supports Pump Co-Op emulation, however it" +
							" uses a file format which is not compatible with stock Stepmania and only supports up to four" +
							" players. This format also does not support rolls, lifts, or per-player mines. If this option" +
							$" is checked then when {GetAppName()} saves a Pump Routine chart it will use the StepF2 format.");

						ImGuiLayoutUtils.EndTable();
					}

					ImGui.EndMenu();
				}

				ImGui.Separator();
				if (ImGui.MenuItem("Exit", "Alt+F4"))
					OnExit();

				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Edit"))
			{
				var selectedEvents = GetFocusedChartData()?.GetSelection()?.GetSelectedEvents();
				UIEditEvents.DrawAddEventMenu();
				ImGui.Separator();
				UIEditEvents.DrawSelectAllMenu();

				var hasSelectedEvents = selectedEvents?.Any() ?? false;
				if (!hasSelectedEvents)
					PushDisabled();
				UIEditEvents.DrawConvertSelectedMenu(selectedEvents);
				UIEditEvents.DrawSwapSelectedMenu(selectedEvents);
				UIEditEvents.DrawShiftSelectedMenu(selectedEvents);
				if (!hasSelectedEvents)
					PopDisabled();

				ImGui.Separator();
				UIEditEvents.DrawConvertAllMenu();
				UIEditEvents.DrawSwapAllMenu();
				UIEditEvents.DrawShiftAllMenu();
				ImGui.Separator();
				if (ImGui.MenuItem("Copy", UIControls.GetCommandString(keyBinds.Copy)))
					OnCopy();
				if (ImGui.MenuItem("Paste", UIControls.GetCommandString(keyBinds.Paste)))
					OnPaste();
				if (ImGui.MenuItem("Delete", UIControls.GetCommandString(keyBinds.Delete)))
					OnDelete();
				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("View"))
			{
				if (ImGui.MenuItem("Options"))
					UIOptions.Instance.Open(true);

				ImGui.Separator();
				if (ImGui.MenuItem("Song Properties"))
					UISongProperties.Instance.Open(true);
				if (ImGui.MenuItem("Chart Properties"))
					UIChartProperties.Instance.Open(true);
				if (ImGui.MenuItem("Pack Properties"))
					UIPackProperties.Instance.Open(true);
				if (ImGui.MenuItem("Chart List"))
					UIChartList.Instance.Open(true);
				if (ImGui.MenuItem("Hotbar"))
					UIHotbar.Instance.Open(true);

				ImGui.Separator();
				if (ImGui.MenuItem("Scroll Preferences"))
					UIScrollPreferences.Instance.Open(true);
				if (ImGui.MenuItem("Selection Preferences"))
					UISelectionPreferences.Instance.Open(true);

				ImGui.Separator();
				if (ImGui.MenuItem("Waveform Preferences"))
					UIWaveFormPreferences.Instance.Open(true);
				if (ImGui.MenuItem("Dark Preferences"))
					UIDarkPreferences.Instance.Open(true);
				if (ImGui.MenuItem("Mini Map Preferences"))
					UIMiniMapPreferences.Instance.Open(true);
				if (ImGui.MenuItem("Receptor Preferences"))
					UIReceptorPreferences.Instance.Open(true);
				if (ImGui.MenuItem("Stream Preferences"))
					UIStreamPreferences.Instance.Open(true);
				if (ImGui.MenuItem("Density Graph Preferences"))
					UIDensityGraphPreferences.Instance.Open(true);
				if (ImGui.MenuItem("Note Color Preferences"))
					UINoteColorPreferences.Instance.Open(true);

				ImGui.Separator();
				if (ImGui.MenuItem("Log"))
					UILog.Instance.Open(true);

				ImGui.Separator();
				if (ImGui.MenuItem("Performance Metrics"))
					UIPerformance.Instance.Open(true);
#if DEBUG
				if (ImGui.MenuItem("ImGui Demo Window"))
					ShowImGuiTestWindow = true;
				if (ImGui.MenuItem("Debug Window"))
					UIDebug.Instance.Open(true);
#endif
				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Song"))
			{
				if (ImGui.MenuItem("New Song", UIControls.GetCommandString(keyBinds.New), false, canOpen))
				{
					OnNew();
				}

				ImGui.Separator();
				if (ImGui.MenuItem("View Song Properties"))
					UISongProperties.Instance.Open(true);
				if (ImGui.MenuItem("View Pack Properties"))
					UIPackProperties.Instance.Open(true);

				ImGui.Separator();
				if (ImGui.MenuItem("Automatically Apply Best Assets (All)",
					    UIControls.GetCommandString(keyBinds.AutoApplyAllSongAssets), false, canEditSong))
				{
					ApplyBestSongAssets(false);
				}

				if (ImGui.MenuItem("Automatically Apply Best Assets (Unset Only)",
					    UIControls.GetCommandString(keyBinds.AutoApplyUnsetSongAssets), false, canEditSong))
				{
					ApplyBestSongAssets(true);
				}

				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Chart"))
			{
				if (ImGui.BeginMenu("New Chart", canEditSong))
				{
					DrawNewChartSelectableList();
					ImGui.EndMenu();
				}

				var chartName = FocusedChart == null ? "Current" : FocusedChart.GetShortName();
				if (ImGui.MenuItem($"Clone {chartName} Chart", canEditSong && hasChart))
					ActionQueue.Instance.Do(new ActionCloneChart(this, FocusedChart));
				if (ImGui.MenuItem("Autogen New Chart...", canEditSong && hasChart))
					ShowAutogenChartUI(FocusedChart);

				ImGui.Separator();
				if (ImGui.MenuItem("View Chart Properties"))
					UIChartProperties.Instance.Open(true);
				if (ImGui.MenuItem("View Chart List"))
					UIChartList.Instance.Open(true);

				ImGui.Separator();
				DrawCopyChartEventsMenuItems(FocusedChart);

				ImGui.Separator();
				if (ImGui.MenuItem("Advanced Event Copy..."))
					UICopyEventsBetweenCharts.Instance.Open(true);

				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Autogen"))
			{
				var disabled = !canEdit;
				if (disabled)
					PushDisabled();

				if (ImGui.MenuItem("Autogen New Chart...", hasChart))
					ShowAutogenChartUI(FocusedChart);
				if (ImGui.MenuItem("Autogen New Set of Charts...", hasChart))
					UIAutogenChartsForChartType.Instance.Open(true);

				ImGui.Separator();

				var hasPatterns = FocusedChart != null && FocusedChart.HasPatterns();
				if (!hasPatterns)
					PushDisabled();

				if (ImGui.MenuItem("Regenerate All Patterns (Fixed Seeds)",
					    UIControls.GetCommandString(keyBinds.RegenerateAllPatternsFixedSeeds)))
					OnRegenerateAllPatterns();
				if (ImGui.MenuItem("Regenerate All Patterns (New Seeds)",
					    UIControls.GetCommandString(keyBinds.RegenerateAllPatternsNewSeeds)))
					OnRegenerateAllPatternsWithNewSeeds();
				if (ImGui.Selectable("Clear All Patterns"))
					ActionQueue.Instance.Do(new ActionDeletePatternNotes(
						FocusedChart,
						FocusedChart!.GetPatterns()));

				ImGui.Separator();

				var hasSelectedPatterns = GetFocusedChartData()?.GetSelection()?.HasSelectedPatterns() ?? false;
				if (!hasSelectedPatterns)
					PushDisabled();

				if (ImGui.MenuItem("Regenerate Selected Patterns (Fixed Seeds)",
					    UIControls.GetCommandString(keyBinds.RegenerateSelectedPatternsFixedSeeds)))
					OnRegenerateSelectedPatterns();
				if (ImGui.MenuItem("Regenerate Selected Patterns (New Seeds)",
					    UIControls.GetCommandString(keyBinds.RegenerateSelectedPatternsNewSeeds)))
					OnRegenerateSelectedPatternsWithNewSeeds();
				if (ImGui.Selectable("Clear Selected Patterns"))
					ActionQueue.Instance.Do(new ActionDeletePatternNotes(
						FocusedChart,
						GetFocusedChartData()?.GetSelection()?.GetSelectedPatterns()));

				if (!hasSelectedPatterns)
					PopDisabled();

				ImGui.Separator();
				if (ImGui.MenuItem("Next Pattern", UIControls.GetCommandString(keyBinds.MoveToNextPattern)))
					OnMoveToNextPattern();
				if (ImGui.MenuItem("Previous Pattern", UIControls.GetCommandString(keyBinds.MoveToPreviousPattern)))
					OnMoveToPreviousPattern();

				if (!hasPatterns)
					PopDisabled();

				ImGui.Separator();
				if (ImGui.MenuItem("Configuration..."))
					UIAutogenConfigs.Instance.Open(true);

				if (disabled)
					PopDisabled();

				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Audio"))
			{
				if (ImGui.MenuItem("Audio Preferences"))
					UIAudioPreferences.Instance.Open(true);
				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Layout"))
			{
				if (ImGui.BeginMenu("Reset Layout"))
				{
					if (ImGui.MenuItem("Default Layout"))
						p.PreferencesOptions.ResetLayout = PreferencesOptions.Layout.Default;
					if (ImGui.MenuItem("Expanded Layout"))
						p.PreferencesOptions.ResetLayout = PreferencesOptions.Layout.Expanded;
					ImGui.Separator();
					if (ImGui.MenuItem("Portrait Layout"))
						p.PreferencesOptions.ResetLayout = PreferencesOptions.Layout.Portrait;
					if (ImGui.MenuItem("Portrait Hi-Res Layout"))
						p.PreferencesOptions.ResetLayout = PreferencesOptions.Layout.PortraitHighRes;
					ImGui.EndMenu();
				}

				ImGui.Separator();
				if (ImGui.BeginMenu("Resolution"))
				{
					var sortedModes = new List<DisplayMode>(GraphicsAdapter.DefaultAdapter.SupportedDisplayModes);
					for (var i = sortedModes.Count - 1; i >= 0; i--)
					{
						if (ImGui.MenuItem($"{sortedModes[i].Width,5} x{sortedModes[i].Height,5}"))
						{
							Window.SetResolution(sortedModes[i].Width, sortedModes[i].Height);
						}
					}

					ImGui.EndMenu();
				}

				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("Help"))
			{
				if (ImGui.Selectable($"About {GetAppName()}"))
					UIAbout.Instance.Open(true);
				if (ImGui.Selectable("Controls"))
					UIControls.Instance.Open(true);
				if (ImGui.Selectable("Documentation"))
					Documentation.OpenDocumentation();
				if (ImGui.Selectable($"Open {GetAppName()} on GitHub"))
					Documentation.OpenGitHub();
				if (ImGui.Selectable("Show Intro Dialogs"))
				{
					Preferences.Instance.LastCompletedFtueVersion = null;
					Preferences.Instance.FtueIndex = 0;
				}

				ImGui.EndMenu();
			}

			ImGui.EndMainMenuBar();
		}
	}

	private void ApplyBestSongAssets(bool ifUnset)
	{
		if (!CanEdit())
			return;
		var action = new ActionSetSongAssets(ActiveSong, ifUnset);
		if (action.WillHaveAnEffect())
		{
			ActionQueue.Instance.Do(action);
		}
		else
		{
			if (ifUnset)
				Logger.Info("All unset assets are already set to their best values.");
			else
				Logger.Info("All assets are already set to their best values.");
		}
	}

	internal void DrawCopyChartEventsMenuItems(EditorChart chart)
	{
		if (chart == null)
			return;
		var song = chart.GetEditorSong();
		var canEditSong = CanEdit() && song != null;
		var canEditChart = CanEdit();

		if (ImGui.BeginMenu($"Replace {chart.GetShortName()} Chart's Non-Step Events From", canEditSong && canEditChart))
		{
			UIChartList.DrawChartList(
				song,
				chart,
				selectedChart =>
				{
					if (chart != selectedChart)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							selectedChart,
							UICopyEventsBetweenCharts.GetStepmaniaTypes(),
							new List<EditorChart> { chart }));
					}
				});

			ImGui.EndMenu();
		}

		if (ImGui.BeginMenu($"Replace {chart.GetShortName()} Chart's Timing and Scroll Events From", canEditSong && canEditChart))
		{
			UIChartList.DrawChartList(
				song,
				chart,
				selectedChart =>
				{
					if (chart != selectedChart)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							selectedChart,
							UICopyEventsBetweenCharts.GetTimingAndScrollTypes(),
							new List<EditorChart> { chart }));
					}
				});

			ImGui.EndMenu();
		}

		if (ImGui.BeginMenu($"Replace {chart.GetShortName()} Chart's Timing Events From", canEditSong && canEditChart))
		{
			UIChartList.DrawChartList(
				song,
				chart,
				selectedChart =>
				{
					if (chart != selectedChart)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							selectedChart,
							UICopyEventsBetweenCharts.GetTimingTypes(),
							new List<EditorChart> { chart }));
					}
				});

			ImGui.EndMenu();
		}

		if (ImGui.BeginMenu($"Copy {chart.GetShortName()} Chart's Non-Step Events To", canEditSong && canEditChart))
		{
			if (ImGui.MenuItem("All Charts", canEditSong && canEditChart))
			{
				var allOtherCharts = new List<EditorChart>();
				foreach (var songChart in song!.GetCharts())
				{
					if (songChart == chart)
						continue;
					allOtherCharts.Add(songChart);
				}

				if (allOtherCharts.Count > 0)
				{
					ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
						chart,
						UICopyEventsBetweenCharts.GetStepmaniaTypes(),
						allOtherCharts));
				}
			}

			UIChartList.DrawChartList(
				song,
				chart,
				selectedChart =>
				{
					if (chart != selectedChart)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							chart,
							UICopyEventsBetweenCharts.GetStepmaniaTypes(),
							new List<EditorChart> { selectedChart }));
					}
				});

			ImGui.EndMenu();
		}

		if (ImGui.BeginMenu($"Copy {chart.GetShortName()} Chart's Timing and Scroll Events To", canEditSong && canEditChart))
		{
			if (ImGui.MenuItem("All Charts", canEditSong && canEditChart))
			{
				var allOtherCharts = new List<EditorChart>();
				foreach (var songChart in song!.GetCharts())
				{
					if (songChart == chart)
						continue;
					allOtherCharts.Add(songChart);
				}

				if (allOtherCharts.Count > 0)
				{
					ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
						chart,
						UICopyEventsBetweenCharts.GetTimingAndScrollTypes(),
						allOtherCharts));
				}
			}

			UIChartList.DrawChartList(
				song,
				chart,
				selectedChart =>
				{
					if (chart != selectedChart)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							chart,
							UICopyEventsBetweenCharts.GetTimingAndScrollTypes(),
							new List<EditorChart> { selectedChart }));
					}
				});

			ImGui.EndMenu();
		}

		if (ImGui.BeginMenu($"Copy {chart.GetShortName()} Chart's Timing Events To", canEditSong && canEditChart))
		{
			if (ImGui.MenuItem("All Charts", canEditSong && canEditChart))
			{
				var allOtherCharts = new List<EditorChart>();
				foreach (var songChart in song!.GetCharts())
				{
					if (songChart == chart)
						continue;
					allOtherCharts.Add(songChart);
				}

				if (allOtherCharts.Count > 0)
				{
					ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
						chart,
						UICopyEventsBetweenCharts.GetTimingTypes(),
						allOtherCharts));
				}
			}

			UIChartList.DrawChartList(
				song,
				chart,
				selectedChart =>
				{
					if (chart != selectedChart)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							chart,
							UICopyEventsBetweenCharts.GetTimingTypes(),
							new List<EditorChart> { selectedChart }));
					}
				});

			ImGui.EndMenu();
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

	internal void ShowAutogenChartUI(EditorChart sourceChart = null)
	{
		if (sourceChart != null && !sourceChart.SupportsAutogenFeatures())
			sourceChart = null;

		UIAutogenChart.Instance.SetChart(sourceChart);
		UIAutogenChart.Instance.Open(true);
	}

	private void DrawRightClickMenu(int x, int y)
	{
		if (ImGui.BeginPopup("RightClickPopup"))
		{
			var anyItemsDrawn = false;

			if (ActiveSong == null)
			{
				anyItemsDrawn = true;
				if (ImGui.MenuItem("New Song", UIControls.GetCommandString(Preferences.Instance.PreferencesKeyBinds.New)))
				{
					OnNew();
				}
			}

			var focusedChartData = GetFocusedChartData();
			var activeChartUnderCursor = GetChartForRightClickMenu(x, y);

			// Check what items are under the cursor.
			var isInMiniMapArea = Preferences.Instance.PreferencesMiniMap.ShowMiniMap
			                      && MiniMap.IsScreenPositionInMiniMapBounds(x, y);
			var isInDensityGraphArea = Preferences.Instance.PreferencesDensityGraph.ShowDensityGraph
			                           && DensityGraph.IsInDensityGraphArea(x, y);
			var isInWaveFormArea = false;
			if (activeChartUnderCursor != null)
			{
				var waveformWidth = GetWaveFormWidth(activeChartUnderCursor);
				var focalPointX = activeChartUnderCursor.GetFocalPointX();
				isInWaveFormArea = Preferences.Instance.PreferencesWaveForm.ShowWaveForm
				                   && Preferences.Instance.PreferencesWaveForm.EnableWaveForm
				                   && x >= focalPointX - (waveformWidth >> 1)
				                   && x <= focalPointX + (waveformWidth >> 1);
			}

			var isInDarkArea = false;
			if (Preferences.Instance.PreferencesDark.ShowDarkBg)
			{
				var (darkX, darkW) = GetDarkBgXAndW();
				isInDarkArea = x >= darkX && x <= darkX + darkW;
			}

			var isInReceptorArea = Receptor.IsInReceptorArea(x, y, GetFocalPointScreenSpace(), ZoomManager.GetSizeZoom(),
				TextureAtlas,
				focusedChartData?.GetArrowGraphicManager(), FocusedChart);

			// Selection Items.
			// Only show items if it is obvious what chart they are for.
			if (focusedChartData?.GetSelection().HasSelectedEvents() ?? false)
			{
				anyItemsDrawn = true;
				UIEditEvents.DrawSelectionMenu();
			}

			if (focusedChartData == activeChartUnderCursor && activeChartUnderCursor != null)
			{
				anyItemsDrawn = true;
				UIEditEvents.DrawSelectAllMenu();
				UIEditEvents.DrawAddEventMenu();
			}

			// Chart Delete / Clone / Autogen.
			if (activeChartUnderCursor != null)
			{
				var chart = activeChartUnderCursor.GetChart();
				if (anyItemsDrawn)
					ImGui.Separator();
				if (ImGui.MenuItem($"Delete {chart.GetShortName()} Chart"))
					ActionQueue.Instance.Do(new ActionDeleteChart(this, chart));
				if (ImGui.MenuItem($"Clone {chart.GetShortName()} Chart"))
					ActionQueue.Instance.Do(new ActionCloneChart(this, chart));
				if (ImGui.MenuItem($"Autogen New Chart From {chart.GetShortName()} Chart...", chart.SupportsAutogenFeatures()))
					ShowAutogenChartUI(chart);

				ImGui.Separator();
				DrawCopyChartEventsMenuItems(chart);
				anyItemsDrawn = true;
			}

			// Preferences based on what is under the cursor.
			var anyObjectHovered = false;
			if (isInMiniMapArea)
			{
				if (anyItemsDrawn)
					ImGui.Separator();
				if (ImGui.BeginMenu("Mini Map Preferences"))
				{
					UIMiniMapPreferences.Instance.DrawContents();
					ImGui.EndMenu();
				}

				anyObjectHovered = true;
				anyItemsDrawn = true;
			}
			else if (isInDensityGraphArea)
			{
				if (anyItemsDrawn)
					ImGui.Separator();
				if (ImGui.BeginMenu("Density Graph Preferences"))
				{
					UIDensityGraphPreferences.DrawContents();
					ImGui.EndMenu();
				}

				if (ImGui.BeginMenu("Stream Preferences"))
				{
					UIStreamPreferences.DrawContents();
					ImGui.EndMenu();
				}

				anyObjectHovered = true;
				anyItemsDrawn = true;
			}
			else if (isInReceptorArea)
			{
				if (anyItemsDrawn)
					ImGui.Separator();
				if (ImGui.BeginMenu("Receptor Preferences"))
				{
					UIReceptorPreferences.Instance.DrawContents();
					ImGui.EndMenu();
				}

				anyObjectHovered = true;
				anyItemsDrawn = true;
			}
			else
			{
				if (isInWaveFormArea)
				{
					if (anyItemsDrawn)
						ImGui.Separator();
					if (ImGui.BeginMenu("Waveform Preferences"))
					{
						UIWaveFormPreferences.Instance.DrawContents();
						ImGui.EndMenu();
					}

					anyObjectHovered = true;
					anyItemsDrawn = true;
				}

				if (isInDarkArea)
				{
					if (!anyObjectHovered && anyItemsDrawn)
						ImGui.Separator();

					if (ImGui.BeginMenu("Dark Preferences"))
					{
						UIDarkPreferences.Instance.DrawContents();
						ImGui.EndMenu();
					}

					anyObjectHovered = true;
					anyItemsDrawn = true;
				}
			}

			if (!anyObjectHovered && (ActiveSong?.GetBackground()?.GetTexture()?.IsBound() ?? false))
			{
				if (anyItemsDrawn)
					ImGui.Separator();

				var o = Preferences.Instance.PreferencesOptions;
				var hideBg = o.HideSongBackground;
				if (ImGui.Checkbox("Hide Background", ref hideBg))
				{
					if (hideBg != o.HideSongBackground)
					{
						ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<bool>(o,
							nameof(PreferencesOptions.HideSongBackground), hideBg, false));
					}

					ImGui.EndMenu();
				}

				// ReSharper disable once RedundantAssignment
				anyObjectHovered = true;
				anyItemsDrawn = true;
			}

			// New Chart.
			if (ActiveSong != null)
			{
				if (anyItemsDrawn)
					ImGui.Separator();

				var canEditSong = ActiveSong.CanBeEdited();
				if (!canEditSong)
					PushDisabled();

				if (ImGui.BeginMenu("New Chart"))
				{
					DrawNewChartSelectableList();
					ImGui.EndMenu();
				}

				if (!canEditSong)
					PopDisabled();
			}

			ImGui.EndPopup();
		}
	}

	private ActiveEditorChart GetChartForRightClickMenu(int x, int y)
	{
		if (ActiveCharts.Count == 1)
			return FocusedChartData;
		foreach (var activeChart in ActiveChartData)
			if (activeChart.GetFullChartScreenSpaceArea().Contains(x, y))
				return activeChart;
		return null;
	}

	/// <summary>
	/// Shows a modal for unsaved changes to the active song and/or pack.
	/// If the user saves their changes and PostSaveFunction is set, then PostSaveFunction
	/// will be invoked after the save is complete.
	/// In all cases, the PostSaveFunction will be cleared after the modal is complete.
	/// </summary>
	/// <param name="onlyPack">
	/// If true, then the modal will only show a notification about potential unsaved pack changes,
	/// and it will not show any notification about unsaved song changes. If there are no unsaved
	/// pack changes then the modal will not show, even if there are unsaved song changes.
	/// </param>
	/// <param name="newSongFilePromptingModal">
	/// The path to a new song file which is prompting this unsaved changes modal. In many situations
	/// there is no new song prompting the modal, such as exiting the application. However, if there is
	/// a new song, it may be in the same pack as the current song. In this situation, the
	/// pack will not need to be reloaded so we can avoid prompting for unsaved pack changes.
	/// </param>
	private void ShowUnsavedChangesModal(
		bool onlyPack = false,
		string newSongFilePromptingModal = null)
	{
		// Determine which assets to notify the user about in the modal.
		var unsavedSong = !onlyPack && HasUnsavedSongChanges();
		var unsavedPack = HasUnsavedPackChanges();
		if (unsavedPack && !string.IsNullOrEmpty(newSongFilePromptingModal))
		{
			if (ActiveSong != null && EditorPack.AreSongsInSamePack(ActiveSong.GetFileFullPath(), newSongFilePromptingModal))
			{
				unsavedPack = false;
			}
		}

		// If, due to the conditions above, we do not want a show a modal, just invoke the PostSaveFunction and return.
		if (!unsavedPack && !unsavedSong)
		{
			TryInvokePostSaveFunction();
			return;
		}

		// Configure the modal text.
		string title;
		string message;
		var songHasTitle = !string.IsNullOrEmpty(ActiveSong?.Title);
		if (unsavedSong && unsavedPack)
		{
			title = "Unsaved Simfile And Pack Changes";
			if (songHasTitle)
			{
				message = $"Do you want to save your changes to {ActiveSong.Title} and {ItgManiaPack.FileName}?";
			}
			else
			{
				message = $"Do you want to save your changes to the current simfile and {ItgManiaPack.FileName}?";
			}
		}
		else if (unsavedSong)
		{
			title = "Unsaved Simfile Changes";
			if (songHasTitle)
			{
				message = $"Do you want to save your changes to {ActiveSong.Title}?";
			}
			else
			{
				message = "Do you want to save your changes to the current simfile?";
			}
		}
		else
		{
			title = "Unsaved Pack Changes";
			message = $"Do you want to save your changes to {ItgManiaPack.FileName}?";
		}

		message += "\n\nYour changes will be lost if you don't save them.";

		// Show the modal.
		UIModals.OpenModalThreeButtons(
			title,
			message,
			"Cancel", () => { PostSaveFunction = null; },
			"Don't Save", TryInvokePostSaveFunction,
			"Save", () =>
			{
				// Save the song. Saving the song will automatically save the pack.
				if (unsavedSong)
				{
					if (CanSaveWithoutLocationPrompt())
					{
						OnSave();
					}
					else
					{
						OnSaveAs();
					}
				}
				// Only if we didn't save the song, and we need to save the pack, save the pack.
				else if (unsavedPack)
				{
					ActivePack.SaveItgManiaPack(false,
						() => { MainThreadDispatcher.RunOnMainThread(TryInvokePostSaveFunction); });
				}
			});
	}

	private void ShowFileChangedModal()
	{
		// Do not show the notification if one is already showing.
		if (ShowingSongFileChangedNotification)
			return;

		var fileName = ActiveSong?.GetFileName() ?? "The current song";

		UIModals.OpenModalTwoButtons(
			"External Simfile Modification",
			$"{fileName} was modified externally.",
			"Ignore", () => { ShowingSongFileChangedNotification = false; },
			"Reload", () =>
			{
				ShowingSongFileChangedNotification = false;
				OnReload(true);
			},
			() =>
			{
				var unsavedSong = HasUnsavedSongChanges();
				var unsavedPack = HasUnsavedPackChanges();

				if (unsavedSong && unsavedPack)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, UILog.GetColor(LogLevel.Warn));
					ImGui.TextUnformatted(
						$"Warning: There are unsaved changes to {fileName} and {ItgManiaPack.FileName}. Reloading will lose these changes.");
					ImGui.PopStyleColor();
					ImGui.Separator();
				}
				else if (unsavedSong)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, UILog.GetColor(LogLevel.Warn));
					ImGui.TextUnformatted("Warning: There are unsaved changes. Reloading will lose these changes.");
					ImGui.PopStyleColor();
					ImGui.Separator();
				}
				else if (unsavedPack)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, UILog.GetColor(LogLevel.Warn));
					ImGui.TextUnformatted(
						$"Warning: There are unsaved changes to {ItgManiaPack.FileName}. Reloading will lose these changes.");
					ImGui.PopStyleColor();
					ImGui.Separator();
				}

				ImGui.Checkbox("Don't notify on external song file changes.",
					ref Preferences.Instance.PreferencesOptions.SuppressExternalSongModificationNotification);
			});
		ShowingSongFileChangedNotification = true;
	}

	[Conditional("DEBUG")]
	private void DrawImGuiTestWindow()
	{
		if (ShowImGuiTestWindow)
		{
			ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
			ImGui.ShowDemoWindow(ref ShowImGuiTestWindow);
		}
	}

	/// <summary>
	/// Draws the splash screen.
	/// The splash screen with an FMOD logo is required by the FMOD EULA.
	/// </summary>
	public void DrawSplash()
	{
		if (SplashTime <= 0.0)
			return;
		SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

		var screenW = GetViewportWidth();
		var screenH = GetViewportHeight();

		// Draw dark background
		const float bgFadeDuration = 0.2f;
		var bgAlpha = (float)Interpolation.Lerp(0.7f, 0.0f, TotalSplashTime - bgFadeDuration, TotalSplashTime,
			TotalSplashTime - SplashTime);
		var bgColor = new Color(Color.White, bgAlpha);
		SpriteBatch.Draw(SplashBackground, new Rectangle(0, 0, screenW, screenH), bgColor);

		// Draw logo with required attribution information.
		const float logoFadeDuration = 0.3f;
		var logoAlpha = (float)Interpolation.Lerp(1.0f, 0.0f, TotalSplashTime - logoFadeDuration, TotalSplashTime,
			TotalSplashTime - SplashTime);
		var logoColor = new Color(Color.White, logoAlpha);
		var textureW = LogoAttribution.Bounds.Width;
		var textureH = LogoAttribution.Bounds.Height;
		var destinationRect = new Rectangle((screenW - textureW) >> 1, (screenH - textureH) >> 1, textureW, textureH);
		SpriteBatch.Draw(LogoAttribution, destinationRect, logoColor);

		SpriteBatch.End();
	}

	#endregion Gui Rendering

	#region StepGraphs

	/// <summary>
	/// Initializes all PadData and creates corresponding StepGraphs for all ChartTypes
	/// specified in the StartupChartTypes.
	/// </summary>
	private async Task InitStepGraphDataAsync()
	{
		foreach (var chartType in SupportedChartTypes)
			PadDataByChartType[chartType] = null;
		foreach (var chartType in SupportedSinglePlayerChartTypes)
			StepGraphByChartType[chartType] = null;

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
		var validStepGraphTypes = new List<ChartType>();
		foreach (var chartType in SupportedSinglePlayerChartTypes)
		{
			if (pOptions.StartupStepGraphs.Contains(chartType))
			{
				if (PadDataByChartType.TryGetValue(chartType, out var padData) && padData != null)
					validStepGraphTypes.Add(chartType);
			}
		}

		if (validStepGraphTypes.Count > 0)
		{
			var stepGraphTasks = new Task<StepGraph>[validStepGraphTypes.Count];
			var index = 0;
			foreach (var stepGraphType in validStepGraphTypes)
			{
				stepGraphTasks[index++] = CreateStepGraph(stepGraphType);
			}

			await Task.WhenAll(stepGraphTasks);
			for (var i = 0; i < validStepGraphTypes.Count; i++)
			{
				StepGraphByChartType[validStepGraphTypes[i]] = stepGraphTasks[i].Result;
			}

			// Set up root nodes.
			var rootNodesByChartType = new Dictionary<ChartType, List<List<GraphNode>>>();
			await Task.Run(() =>
			{
				for (var i = 0; i < validStepGraphTypes.Count; i++)
				{
					var chartType = validStepGraphTypes[i];
					var rootNodes = new List<List<GraphNode>>();
					var stepGraph = StepGraphByChartType[chartType];
					var found = true;

					// Add the root node as the first tier.
					rootNodes.Add([stepGraph.GetRoot()]);

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
		StepTypeFallbacks = await StepTypeFallbacks.Load(Path.Combine(PlatformInterface.GetResourceDirectory(),
			StepTypeFallbacks.DefaultFallbacksFileName));
	}

	/// <summary>
	/// Loads PadData for the given ChartType.
	/// </summary>
	/// <param name="chartType">ChartType to load PadData for.</param>
	/// <returns>Loaded PadData or null if any errors were generated.</returns>
	private async Task<PadData> LoadPadData(ChartType chartType)
	{
		var chartTypeString = ChartTypeString(chartType);
		var fileName = $"{chartTypeString}.json";

		var fullFileName = Path.Combine(PlatformInterface.GetResourceDirectory(), fileName);
		if (!File.Exists(fullFileName))
		{
			Logger.Warn($"Could not find PadData file {fileName}.");
			return null;
		}

		Logger.Info($"Loading {chartTypeString} PadData from {fileName}.");
		var padData = await PadData.LoadPadData(chartTypeString, fullFileName);
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

		var fullFileName = Path.Combine(PlatformInterface.GetResourceDirectory(), fileName);
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
		return PadDataByChartType.GetValueOrDefault(chartType);
	}

	public bool GetStepGraph(ChartType chartType, out StepGraph stepGraph, bool logErrorOnFailure)
	{
		var result = StepGraphByChartType.TryGetValue(chartType, out stepGraph) && stepGraph != null;
		if (!result && logErrorOnFailure)
		{
			Logger.Error($"No {GetPrettyEnumString(chartType)} StepGraph is loaded."
			             + " You can specify which StepGraphs are loaded in the Options window.");
		}

		return result;
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

	private bool CanLoadSongs()
	{
		// Songs may reference patterns which require autogen configs to be loaded.
		return PerformedChartConfigManager.Instance.HasFinishedLoading()
		       && ExpressedChartConfigManager.Instance.HasFinishedLoading()
		       && PatternConfigManager.Instance.HasFinishedLoading();
	}

	private void CheckForAutoLoadingLastSong()
	{
		if (HasCheckedForAutoLoadingLastSong || !CanLoadSongs())
			return;
		HasCheckedForAutoLoadingLastSong = true;

		// Prefer opening the explicitly request launch file if one is present.
		if (!string.IsNullOrEmpty(LaunchFile))
		{
			Logger.Info($"Opening file provided as command line argument: {LaunchFile}");
			OnOpenFile(LaunchFile);
			return;
		}

		// If we have a saved file to open, open it now.
		if (Preferences.Instance.PreferencesOptions.OpenLastOpenedFileOnLaunch
		    && Preferences.Instance.RecentFiles.Count > 0)
		{
			OpenRecentIndex = 0;
			OnOpenRecentFile();
		}
	}

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

		var songDirectory = ActiveSong.GetFileDirectory();
		if (string.IsNullOrEmpty(songDirectory))
			songDirectory = Preferences.Instance.OpenFileDialogInitialDirectory;
		var fileName = ActiveSong.GetFileName();
		if (string.IsNullOrEmpty(fileName))
			fileName = ActiveSong.Title;
		var (confirmed, fullPath) =
			PlatformInterface.ShowSaveSimFileDialog(songDirectory, fileName, ActiveSong.GetFileFormat()?.Type);
		if (!confirmed)
			return;

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

		// Save the song.
		var saveParameters = new EditorSong.SaveParameters(fileType, fullPath, (success) =>
		{
			UpdateWindowTitle();
			UpdateRecentFilesForActiveSong();
			if (success)
			{
				ActionQueue.Instance.OnSaved();
			}
			else
			{
				var fileName = editorSong.GetFileName();
				if (string.IsNullOrEmpty(fileName))
					fileName = "file";
				UIModals.OpenModalOneButton(
					"Save Failure",
					$"Failed to save {fileName}. Check the log for details.",
					"Okay", () => { });
			}

			TryInvokePostSaveFunction();
		}, ActivePack)
		{
			RequireIdenticalTimingInSmFiles = Preferences.Instance.RequireIdenticalTimingInSmFiles,
			OmitChartTimingData = Preferences.Instance.OmitChartTimingData,
			OmitCustomSaveData = Preferences.Instance.OmitCustomSaveData,
			AnonymizeSaveData = Preferences.Instance.AnonymizeSaveData,
			UseStepF2ForPumpRoutine = Preferences.Instance.UseStepF2ForPumpRoutine,
		};
		editorSong?.Save(saveParameters);
	}

	/// <summary>
	/// Starts the process of opening a Song file by presenting a dialog to choose a Song.
	/// </summary>
	private void OpenSongFile()
	{
		if (!CanLoadSongs())
			return;

		var (confirmed, fileName) = PlatformInterface.ShowOpenSimFileDialog(Preferences.Instance.OpenFileDialogInitialDirectory);
		if (!confirmed)
			return;

		var pOptions = Preferences.Instance.PreferencesOptions;
		Preferences.Instance.OpenFileDialogInitialDirectory = System.IO.Path.GetDirectoryName(fileName);
		OpenSongFileAsync(fileName,
			new DefaultChartListProvider(pOptions.DefaultStepsType, pOptions.DefaultDifficultyType));
	}

	public void OpenSongFile(string fileName, bool osRequest = false)
	{
		if (string.IsNullOrEmpty(fileName))
			return;

		// If this open request came from the OS it may occur
		// before we are ready to load songs. If that is the case,
		// store this song as the file to launch.
		if (!HasCheckedForAutoLoadingLastSong && osRequest)
		{
			LaunchFile = fileName;
			return;
		}

		OnOpenFile(fileName);
	}

	/// <summary>
	/// Starts the process of opening a selected Song.
	/// Will cancel previous OpenSongFileAsync requests if called while already loading.
	/// </summary>
	/// <param name="fileName">File name of the Song file to open.</param>
	/// <param name="chartListProvider">IActiveChartListProvider for determining which active charts to use.</param>
	private void OpenSongFileAsync(
		string fileName,
		IActiveChartListProvider chartListProvider)
	{
		if (!CanLoadSongs())
			return;

		OpeningSong = true;
		CloseSong();

		// Start the load. If we are already loading, return.
		// The previous call will use the newly provided file state.
		_ = MainThreadDispatcher.RunContinuationOnMainThread(
			SongLoadTask.Start(new SongLoadState(fileName, chartListProvider)),
			(results) =>
			{
				Debug.Assert(IsOnMainThread());

				// The results may be null if we have multiple calls to load simultaneous and our call was cancelled.
				if (results == null)
					return;

				CloseSong();
				OpeningSong = false;

				// Get the newly loaded song.
				ActiveSong = results.GetSong();
				ActivePack.SetSong(ActiveSong);
				if (ActiveSong == null)
				{
					return;
				}

				// Observe the song and its charts.
				ActiveSong.AddObservers(this, this);

				// Observe the song file for changes.
				StartObservingSongFile(results.GetFileName());

				// Set the position and zoom to the last used values for this song.
				var desiredChartPosition = 0.0;
				var desiredZoom = 1.0;
				var savedInfo = GetMostRecentSavedSongInfoForActiveSong();
				if (savedInfo != null)
				{
					desiredChartPosition = savedInfo.ChartPosition;
					desiredZoom = savedInfo.SpacingZoom;
				}

				// Set the active and focused charts. Ensure the focal point doesn't move.
				// Set every chart to have a dedicated tab.
				var focalPointX = GetFocalPointScreenSpaceX();
				var focalPointY = GetFocalPointScreenSpaceY();
				ActiveCharts.Clear();
				ActiveChartData.Clear();
				foreach (var activeChart in results.GetActiveCharts())
					SetChartHasDedicatedTab(activeChart, true);
				SetChartFocused(results.GetFocusedChart());
				SetFocalPointScreenSpace(focalPointX, focalPointY);

				// Set position and zoom.
				ResetPosition();
				SetChartPosition(desiredChartPosition);
				ZoomManager.SetZoom(desiredZoom);

				// Insert a new entry at the top of the saved recent files.
				UpdateRecentFilesForActiveSong();
			});
	}

	private void StartObservingSongFile(string fileName)
	{
		try
		{
			var dir = System.IO.Path.GetDirectoryName(fileName);
			var file = System.IO.Path.GetFileName(fileName);
			if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
			{
				SongFileWatcher?.Dispose();
				SongFileWatcher = new FileSystemWatcher(dir);
				SongFileWatcher.NotifyFilter = NotifyFilters.LastWrite;
				SongFileWatcher.Changed += OnSongFileChangedNotification;
				SongFileWatcher.Filter = file;
				SongFileWatcher.EnableRaisingEvents = true;
			}
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to observe {fileName} for changes: {e}");
		}
	}

	private void OnSongFileChangedNotification(object sender, FileSystemEventArgs e)
	{
		if (e.ChangeType != WatcherChangeTypes.Changed)
			return;
		if (Preferences.Instance.PreferencesOptions.SuppressExternalSongModificationNotification)
			return;

		// Check for showing a notification on the main thread.
		MainThreadDispatcher.RunOnMainThread(CheckForShowingSongFileChangedNotification);
	}

	private void CheckForShowingSongFileChangedNotification()
	{
		if (ActiveSong == null)
			return;

		// If we are saving, we expect a notification that the file has changed.
		if (ActiveSong.IsSaving())
			return;

		// There is no clean way to identify whether the notification is due to a change originating
		// from this application or an external application. If we haven't saved recently, assume it
		// is an external application.
		var timeSinceLastSave = DateTime.Now - ActiveSong.GetLastSaveCompleteTime();
		if (timeSinceLastSave.TotalSeconds < 3)
		{
			return;
		}

		ShowFileChangedModal();
	}

	private void StopObservingSongFile()
	{
		SongFileWatcher?.Dispose();
		SongFileWatcher = null;
	}

	private SavedSongInformation GetMostRecentSavedSongInfoForActiveSong()
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

	private void UpdateRecentFilesForActiveSong()
	{
		if (ActiveSong == null || string.IsNullOrEmpty(ActiveSong.GetFileFullPath()))
			return;

		var chartPosition = GetPosition().ChartPosition;
		var spacingZoom = ZoomManager.GetSpacingZoom();

		var p = Preferences.Instance;
		var pOptions = p.PreferencesOptions;
		var savedSongInfo = new SavedSongInformation(
			ActiveSong.GetFileFullPath(),
			ActiveSong.Title,
			ActivePack?.GetPackName(),
			spacingZoom,
			chartPosition,
			ActiveChartData,
			GetFocusedChartData());
		p.RecentFiles.RemoveAll(info => info.FileName == ActiveSong.GetFileFullPath());
		p.RecentFiles.Insert(0, savedSongInfo);
		if (p.RecentFiles.Count > pOptions.RecentFilesHistorySize)
		{
			p.RecentFiles.RemoveRange(
				pOptions.RecentFilesHistorySize,
				p.RecentFiles.Count - pOptions.RecentFilesHistorySize);
		}
	}

	private string GetFullPathToFocusedChartMusicFile()
	{
		return GetFullPathToSongResource(FocusedChart?.GetMusicFileToUseForChart());
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

	private void OnOpenContainingFolder()
	{
		if (ActiveSong == null)
			return;
		var dir = ActiveSong.GetFileDirectory();
		if (string.IsNullOrEmpty(dir))
			return;
		PlatformInterface.OpenFileBrowser(dir);
	}

	private void OnOpen()
	{
		if (!CanLoadSongs())
			return;

		if (HasUnsavedSongOrPackChanges())
		{
			PostSaveFunction = OpenSongFile;
			ShowUnsavedChangesModal();
		}
		else
		{
			OpenSongFile();
		}
	}

	private void OnOpenFile(string songFile)
	{
		if (!CanLoadSongs())
			return;

		PendingOpenSongFileName = songFile;
		if (HasUnsavedSongOrPackChanges())
		{
			PostSaveFunction = OnOpenFileNoSave;
			ShowUnsavedChangesModal(false, songFile);
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
			new DefaultChartListProvider(pOptions.DefaultStepsType, pOptions.DefaultDifficultyType));
	}

	public void ReloadPack()
	{
		if (HasUnsavedPackChanges())
		{
			PostSaveFunction = OnReloadPackNoSave;
			ShowUnsavedChangesModal(true);
		}
		else
		{
			OnReloadPackNoSave();
		}
	}

	private void OnReloadPackNoSave()
	{
		ActivePack?.Refresh();
	}

	private void OnReload()
	{
		OnReload(false);
	}

	private void OnReload(bool ignoreUnsavedChanges)
	{
		if (!CanLoadSongs())
			return;
		OpenRecentIndex = 0;
		OnOpenRecentFile(ignoreUnsavedChanges);
	}

	private void OnOpenRecentFile(bool ignoreUnsavedChanges = false)
	{
		if (!CanLoadSongs())
			return;

		var p = Preferences.Instance;
		if (OpenRecentIndex >= p.RecentFiles.Count)
			return;

		if (!ignoreUnsavedChanges && HasUnsavedSongOrPackChanges())
		{
			PostSaveFunction = OpenRecentFile;
			ShowUnsavedChangesModal(false, p.RecentFiles[OpenRecentIndex].FileName);
		}
		else
		{
			OpenRecentFile();
		}
	}

	private void OpenRecentFile()
	{
		if (!CanLoadSongs())
			return;

		var p = Preferences.Instance;
		if (OpenRecentIndex >= p.RecentFiles.Count)
			return;

		OpenSongFileAsync(p.RecentFiles[OpenRecentIndex].FileName, p.RecentFiles[OpenRecentIndex]);
	}

	private void OnNew()
	{
		if (!CanLoadSongs())
			return;

		if (HasUnsavedSongOrPackChanges())
		{
			PostSaveFunction = OnNewNoSave;
			ShowUnsavedChangesModal();
		}
		else
		{
			OnNewNoSave();
		}
	}

	private void OnNewNoSave()
	{
		Debug.Assert(IsOnMainThread());

		if (!CanLoadSongs())
			return;

		CloseSong();
		ActiveSong = new EditorSong(GraphicsDevice, ImGuiRenderer);
		ActivePack.SetSong(ActiveSong);
		ActiveSong.AddObservers(this, this);

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

		ResetPosition();
		ZoomManager.SetZoom(1.0);
	}

	private void OnClose()
	{
		if (HasUnsavedSongOrPackChanges())
		{
			PostSaveFunction = OnCloseNoSave;
			ShowUnsavedChangesModal();
		}
		else
		{
			OnCloseNoSave();
		}
	}

	private void OnCloseNoSave()
	{
		CloseSong();
		ResetPosition();
		ZoomManager.SetZoom(1.0);
	}

	private void CloseSong()
	{
		// First, save the current zoom and position to the song history so we can restore them when
		// opening this song again later.
		var savedSongInfo = GetMostRecentSavedSongInfoForActiveSong();
		savedSongInfo?.Update(ActiveChartData, GetFocusedChartData(), ZoomManager.GetSpacingZoom(), GetPosition().ChartPosition);
		UnloadSongResources();
	}

	private void UnloadSongResources()
	{
		Debug.Assert(IsOnMainThread());

		// When unloading everything, perform garbage collection.
		// We wait two frames because closing the song and unloading resources is often
		// done in response to a UI click, which happens in the Draw() call. Waiting one
		// frame would put us in the next Update() call, but we still need one more Draw()
		// after ActiveSong is no longer set to draw one new frame with no song data.
		// Performing a manual collection here has a few benefits:
		// 1) We know the timing of this major unload event. This is an appropriate time to collect as the resources
		//    unloaded here (particularly the SoundMipMap) are massive.
		// 2) Immediately after unloading is a good time for a hitch as nothing is animating and no audio is playing.
		// 3) We do not want random hitches when playing / editing.
		if (ActiveSong != null)
			GarbageCollectFrame = 2;

		ActiveSong?.RemoveObservers(this, this);
		ActiveSong?.RemoveObservers();

		// Close any UI which holds on to Song/Chart state.
		UIAutogenChart.Instance.Close();
		UIAutogenChartsForChartType.Instance.Close();
		UICopyEventsBetweenCharts.Instance.Close();

		StopPlayback();
		MusicManager.UnloadAsync();
		ActiveSong = null;
		if (!OpeningSong)
			ActivePack.SetSong(ActiveSong);
		FocusedChart = null;
		FocusedChartData = null;
		LastKnownSongTime = null;
		foreach (var activeChartData in ActiveChartData)
		{
			activeChartData.Clear();
		}

		ActiveChartData.Clear();
		ActiveCharts.Clear();
		PlayerPerChart.Clear();
		EditorMouseState.SetActiveChart(null);
		DensityGraph.SetStepDensity(null);
		DensityGraph.ResetBufferCapacities();
		UpdateWindowTitle();
		ActionQueue.Instance.Clear();
		StopObservingSongFile();
		RefreshPlayerLimitFromFocusedChart();
	}

	private void UpdateWindowTitle()
	{
		Debug.Assert(IsOnMainThread());

		if (Window == null)
			return;
		var hasUnsavedChanges = HasUnsavedSongChanges();
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

		if (FocusedChart != null)
		{
			sb.Append(
				$" [{GetPrettyEnumString(FocusedChart.ChartType)} - {GetPrettyEnumString(FocusedChart.ChartDifficultyType)}]");
		}

		sb.Append(" - ");
		sb.Append(appName);
		var newTitle = sb.ToString();

		// Accessing the title is an expensive operation.
		if (!string.Equals(FormTitle, newTitle))
		{
			FormTitle = newTitle;
			Window.Title = FormTitle;
		}
	}

	#endregion Save and Load

	#region Selection

	internal IReadOnlySelection GetSelection()
	{
		return GetFocusedChartData()?.GetSelection();
	}

	public void OnNoteTransformationBegin()
	{
		TransformingSelectedNotes = true;
	}

	internal void OnNoteTransformationEnd(List<EditorEvent> transformedEvents)
	{
		TransformingSelectedNotes = false;

		// When a transformation ends, set the selection to the transformed notes.
		// Technically this will deselect notes if the user performed a transform
		// on a set of events where not all were eligible to be transformed. For
		// example, if they selected all events (including rate altering events)
		// and then mirrored the selection, this would deselect the rate altering
		// events. However, this logic also guarantees that after a transform,
		// including transforms initiated from undo and redo, the selection contains
		// the modified notes.
		ActiveEditorChart activeChartDataForSelection = null;
		if (transformedEvents != null && transformedEvents.Count > 0)
		{
			activeChartDataForSelection = GetActiveChartData(transformedEvents[0].GetEditorChart());
			if (activeChartDataForSelection != null)
			{
				// Do not set the selection if it isn't the focused chart. We only ever want the
				// focused chart to have selected notes.
				if (activeChartDataForSelection == FocusedChartData)
				{
					// When selecting events, only select them if they are added to the chart.
					// When copying notes we clone them, and they aren't added to the chart.
					// When undoing a paste of those notes, we do not want to select these cloned
					// notes.
					activeChartDataForSelection.SetSelectedEvents(transformedEvents, true);
				}
				else
				{
					activeChartDataForSelection.ClearSelection();
				}
			}
		}

		// Clear the selection for every other active chart.
		// This is a stopgap measure to ensure that active charts do not hold on to deleted
		// events in their selections. Technically the TransformingSelectedNotes flag has
		// some issues. If we paste notes from chart A to B, B has focus. Then we undo. This
		// causes the notes in B to become deleted but chart B ignores that for deleting them
		// from its selection due to the transform flag. B then holds on to deleted notes in
		// its selection.
		foreach (var otherActiveChartData in ActiveChartData)
		{
			if (otherActiveChartData == activeChartDataForSelection)
				continue;
			otherActiveChartData.ClearSelection();
		}
	}

	private void OnDelete()
	{
		if (EditEarlyOut())
			return;
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return;
		GetFocusedChartData().OnDelete();
	}

	private void OnEventAdded(EditorEvent addedEvent)
	{
		var activeChartData = GetActiveChartData(addedEvent.GetEditorChart());
		activeChartData?.OnEventAdded(addedEvent);

		// When adding a pattern, select it.
		if (addedEvent is EditorPatternEvent pattern)
		{
			OnSelectPattern(pattern);
		}
	}

	private void OnEventsAdded(IReadOnlyList<EditorEvent> addedEvents)
	{
		if (addedEvents == null || addedEvents.Count == 0)
			return;
		var activeChartData = GetActiveChartData(addedEvents[0].GetEditorChart());
		activeChartData?.OnEventsAdded(addedEvents);

		// When adding a pattern, select it.
		// If multiple patterns were added, select the last one.
		for (var i = addedEvents.Count - 1; i >= 0; i--)
		{
			if (addedEvents[i] is EditorPatternEvent pattern)
			{
				OnSelectPattern(pattern);
				break;
			}
		}
	}

	private void OnEventDeleted(EditorEvent deletedEvent)
	{
		var activeChartData = GetActiveChartData(deletedEvent.GetEditorChart());
		activeChartData?.OnEventDeleted(deletedEvent, TransformingSelectedNotes);
	}

	private void OnEventsDeleted(IReadOnlyList<EditorEvent> deletedEvents)
	{
		if (deletedEvents == null || deletedEvents.Count == 0)
			return;
		var activeChartData = GetActiveChartData(deletedEvents[0].GetEditorChart());
		activeChartData?.OnEventsDeleted(deletedEvents, TransformingSelectedNotes);
	}

	private void OnSelectRowRange()
	{
		GetFocusedChartData()?.ToggleRowSelection();
	}

	public void OnSelectAll()
	{
		GetFocusedChartData()?.OnSelectAll();
	}

	public void OnSelectAll(int player)
	{
		GetFocusedChartData()?.OnSelectAll(player);
	}

	public void OnSelectAllForCurrentPlayer()
	{
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return;
		var player = GetPlayer(FocusedChart);
		focusedChartData.OnSelectAll(player);
	}

	public void OnSelectAllAlt()
	{
		GetFocusedChartData()?.OnSelectAllAlt();
	}

	public void OnSelectAllShift()
	{
		GetFocusedChartData()?.OnSelectAllShift();
	}

	internal void OnSelectAllForCurrentPlayer(Func<EditorEvent, bool> isSelectable)
	{
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return;
		var player = GetPlayer(FocusedChart);
		focusedChartData.OnSelectAll(e => e.GetPlayer() == player && isSelectable(e));
	}

	internal void OnSelectAll(Func<EditorEvent, bool> isSelectable)
	{
		GetFocusedChartData()?.OnSelectAll(isSelectable);
	}

	public void OnShiftSelectedNotesLeft()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftSelectedNotesLeft();
	}

	internal void OnShiftNotesLeft(IEnumerable<EditorEvent> events)
	{
		if (EditEarlyOut())
			return;
		foreach (var editorEvent in events)
		{
			var activeChartData = GetActiveChartData(editorEvent.GetEditorChart());
			activeChartData?.OnShiftNotesLeft(events);
			break;
		}
	}

	public void OnShiftSelectedNotesLeftAndWrap()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftSelectedNotesLeftAndWrap();
	}

	internal void OnShiftNotesLeftAndWrap(IEnumerable<EditorEvent> events)
	{
		if (EditEarlyOut())
			return;
		foreach (var editorEvent in events)
		{
			var activeChartData = GetActiveChartData(editorEvent.GetEditorChart());
			activeChartData?.OnShiftNotesLeftAndWrap(events);
			break;
		}
	}

	public void OnShiftSelectedNotesRight()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftSelectedNotesRight();
	}

	internal void OnShiftNotesRight(IEnumerable<EditorEvent> events)
	{
		if (EditEarlyOut())
			return;
		foreach (var editorEvent in events)
		{
			var activeChartData = GetActiveChartData(editorEvent.GetEditorChart());
			activeChartData?.OnShiftNotesRight(events);
			break;
		}
	}

	public void OnShiftSelectedNotesRightAndWrap()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftSelectedNotesRightAndWrap();
	}

	internal void OnShiftNotesRightAndWrap(IEnumerable<EditorEvent> events)
	{
		if (EditEarlyOut())
			return;
		foreach (var editorEvent in events)
		{
			var activeChartData = GetActiveChartData(editorEvent.GetEditorChart());
			activeChartData?.OnShiftNotesRightAndWrap(events);
			break;
		}
	}

	public int GetShiftNotesRows()
	{
		var rows = SnapManager.GetCurrentRows();
		if (rows == 0)
			rows = MaxValidDenominator;
		return rows;
	}

	public void OnShiftSelectedNotesEarlier()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftSelectedNotesEarlier(GetShiftNotesRows());
	}

	internal void OnShiftNotesEarlier(IEnumerable<EditorEvent> events)
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftNotesEarlier(events, GetShiftNotesRows());
	}

	public void OnShiftSelectedNotesLater()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftSelectedNotesLater(GetShiftNotesRows());
	}

	internal void OnShiftNotesLater(IEnumerable<EditorEvent> events)
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftNotesLater(events, GetShiftNotesRows());
	}

	public void OnMirrorSelection()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnMirrorSelection();
	}

	public void OnFlipSelection()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnFlipSelection();
	}

	public void OnMirrorAndFlipSelection()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnMirrorAndFlipSelection();
	}

	#endregion Selection

	#region Copy Paste

	private void OnCopy()
	{
		var focusedChartData = GetFocusedChartData();
		var selection = focusedChartData?.GetSelection();
		if (selection == null)
			return;

		// Do not alter the currently copied events if nothing is selected.
		if (!selection.HasSelectedEvents())
			return;

		// Clone the events now for copying them to avoid modifications made
		// after the copy affecting the copied notes. For example if we copy,
		// then mirror, then paste, we want to paste un-mirrored notes.
		CopiedEvents.Clear();
		foreach (var selectedEvent in selection.GetSelectedEvents())
			CopiedEvents.Add(selectedEvent.Clone());
		CopiedEvents.Sort();
	}

	private void OnCut()
	{
		OnCopy();
		OnDelete();
	}

	private void OnPaste()
	{
		if (EditEarlyOut())
			return;
		if (CopiedEvents.Count == 0)
			return;
		var earliestRow = CopiedEvents[0].GetRow();
		var currentRow = Math.Max(0, GetPosition().GetNearestRow());
		ActionQueue.Instance.Do(new ActionPasteEvents(this, FocusedChart, CopiedEvents, currentRow - earliestRow));
	}

	#endregion Copy Paste

	#region Chart Navigation

	private void OpenPreviousChart()
	{
		if (ActiveSong == null)
			return;

		var index = 0;
		var sortedCharts = ActiveSong.GetSortedCharts();
		if (sortedCharts.Count == 0)
			return;

		foreach (var chart in sortedCharts)
		{
			if (chart == FocusedChart)
			{
				var previousIndex = index - 1;
				if (previousIndex < 0)
					previousIndex = sortedCharts.Count - 1;
				if (previousIndex != index)
					SetChartFocused(sortedCharts[previousIndex]);
				return;
			}

			index++;
		}

		SetChartFocused(sortedCharts[^1]);
	}

	private void OpenNextChart()
	{
		if (ActiveSong == null)
			return;

		var index = 0;
		var sortedCharts = ActiveSong.GetSortedCharts();
		if (sortedCharts.Count == 0)
			return;

		foreach (var chart in sortedCharts)
		{
			if (chart == FocusedChart)
			{
				var nextIndex = (index + 1) % sortedCharts.Count;
				if (nextIndex != index)
					SetChartFocused(sortedCharts[nextIndex]);
				return;
			}

			index++;
		}

		SetChartFocused(sortedCharts[0]);
	}

	private void FocusPreviousChart()
	{
		if (ActiveSong == null || FocusedChart == null || ActiveCharts.Count < 1)
			return;
		var index = 0;
		foreach (var activeChart in ActiveCharts)
		{
			if (activeChart == FocusedChart)
			{
				var newFocusedChartIndex = index - 1;
				if (newFocusedChartIndex < 0)
					newFocusedChartIndex = ActiveCharts.Count - 1;
				SetChartFocused(ActiveCharts[newFocusedChartIndex]);
				return;
			}

			index++;
		}
	}

	private void FocusNextChart()
	{
		if (ActiveSong == null || FocusedChart == null || ActiveCharts.Count < 1)
			return;
		var index = 0;
		foreach (var activeChart in ActiveCharts)
		{
			if (activeChart == FocusedChart)
			{
				SetChartFocused(ActiveCharts[(index + 1) % ActiveCharts.Count]);
				return;
			}

			index++;
		}
	}

	private void MoveFocusedChartLeft()
	{
		MoveActiveChartLeft(GetFocusedChart());
	}

	private void MoveFocusedChartRight()
	{
		MoveActiveChartRight(GetFocusedChart());
	}

	private void CloseFocusedChart()
	{
		CloseChart(GetFocusedChart());
	}

	private void SetFocusedChartHasDedicatedTab()
	{
		SetChartHasDedicatedTab(GetFocusedChart(), true);
	}

	private void OnToggleSpacingMode()
	{
		var ordinal = (int)Preferences.Instance.PreferencesScroll.SpacingMode;
		var numValues = Enum.GetNames(typeof(SpacingMode)).Length;
		ordinal = (ordinal + 1) % numValues;
		Preferences.Instance.PreferencesScroll.SpacingMode = (SpacingMode)ordinal;
		Logger.Info($"Set Spacing Mode to {Preferences.Instance.PreferencesScroll.SpacingMode}");
	}

	internal void OnActiveChartPositionChanged(ActiveEditorChart activeChart)
	{
		if (activeChart != GetFocusedChartData())
			return;

		LastKnownSongTime = activeChart.Position.SongTime;

		// Update the music time
		if (!UpdatingSongTimeDirectly)
		{
			var songTime = activeChart.Position.SongTime;
			activeChart.Position.SetDesiredPositionToCurrent();
			MusicManager.SetMusicTimeInSeconds(songTime);

			if (Playing)
			{
				BeginPlaybackStopwatch(songTime);
			}
		}
	}

	internal SnapManager GetSnapManager()
	{
		return SnapManager;
	}

	private void OnDecreaseSnap()
	{
		SnapManager.DecreaseSnap();
	}

	private void OnIncreaseSnap()
	{
		SnapManager.IncreaseSnap();
	}

	private void MoveToChartPosition(double chartPosition)
	{
		if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
			StopPlayback();
		SetChartPosition(chartPosition);
		UpdateAutoPlayFromScrolling();
	}

	public void OnMoveUp()
	{
		if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
			StopPlayback();

		var rows = SnapManager.GetCurrentRows();
		if (rows == 0)
		{
			OnMoveToPreviousMeasure();
		}
		else
		{
			var chartPosition = GetPosition().ChartPosition;
			var newChartPosition = (int)chartPosition / rows * rows;
			if (newChartPosition == (int)chartPosition)
				newChartPosition -= rows;
			SetChartPosition(newChartPosition);
			UpdateAutoPlayFromScrolling();
		}
	}

	public void OnMoveDown()
	{
		if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
			StopPlayback();

		var rows = SnapManager.GetCurrentRows();
		if (rows == 0)
		{
			OnMoveToNextMeasure();
		}
		else
		{
			var newChartPosition = (int)GetPosition().ChartPosition / rows * rows + rows;
			SetChartPosition(newChartPosition);
			UpdateAutoPlayFromScrolling();
		}
	}

	private void OnMoveToPreviousRowWithSteps()
	{
		if (FocusedChart == null)
			return;

		var chartPosition = GetPosition().ChartPosition;
		var enumerator = FocusedChart.GetEvents().FindGreatestBeforeChartPosition(chartPosition);
		if (enumerator == null)
			return;
		while (enumerator.MovePrev())
		{
			if (!enumerator.Current!.IsStep())
				continue;
			MoveToChartPosition(enumerator.Current!.GetChartPosition());
			break;
		}
	}

	private void OnMoveToNextRowWithSteps()
	{
		if (FocusedChart == null)
			return;

		var chartPosition = GetPosition().ChartPosition;
		var enumerator = FocusedChart.GetEvents().FindLeastAfterChartPosition(chartPosition);
		if (enumerator == null)
			return;
		while (enumerator.MoveNext())
		{
			if (!enumerator.Current!.IsStep())
				continue;

			MoveToChartPosition(enumerator.Current!.GetChartPosition());
			break;
		}
	}

	private void OnMoveToPreviousRowWithEvents()
	{
		if (FocusedChart == null)
			return;

		var chartPosition = GetPosition().ChartPosition;
		var enumerator = FocusedChart.GetEvents().FindGreatestBeforeChartPosition(chartPosition);
		if (enumerator == null || !enumerator.MoveNext() || !enumerator.IsCurrentValid())
			return;
		MoveToChartPosition(enumerator.Current!.GetChartPosition());
	}

	private void OnMoveToNextRowWithEvents()
	{
		if (FocusedChart == null)
			return;

		var chartPosition = GetPosition().ChartPosition;
		var enumerator = FocusedChart.GetEvents().FindLeastAfterChartPosition(chartPosition);
		if (enumerator == null || !enumerator.MoveNext() || !enumerator.IsCurrentValid())
			return;
		MoveToChartPosition(enumerator.Current!.GetChartPosition());
	}

	private void OnMoveToStartOfStream()
	{
		MoveToStreamBoundary(true);
	}

	private void OnMoveToEndOfStream()
	{
		MoveToStreamBoundary(false);
	}

	private void MoveToStreamBoundary(bool preceding)
	{
		if (FocusedChart == null)
			return;

		var chartPosition = GetPosition().ChartPosition;

		// Get the step which borders the position in the direction we are searching.
		var enumerator = preceding
			? FocusedChart.GetEvents().FindGreatestBeforeChartPosition(chartPosition)
			: FocusedChart.GetEvents().FindLeastAfterChartPosition(chartPosition);
		if (enumerator == null || !AdvanceForwards(enumerator) || !enumerator.IsCurrentValid())
			return;
		while (!enumerator.Current!.IsStep())
		{
			if (!AdvanceForwards(enumerator))
				return;
		}

		var borderingStepRow = enumerator.Current!.GetRow();
		var precedingStepEnumerator = enumerator.Clone();
		int? streamSpacing = null;

		// Determine the spacing of the stream. Prefer examining the space between the note
		// and the note in the opposite direction we are searching.
		AdvanceBackwards(enumerator);
		while (enumerator.IsCurrentValid())
		{
			var note = enumerator.Current!;
			if (note.IsStep())
			{
				streamSpacing = Math.Abs(note.GetRow() - borderingStepRow);
				break;
			}

			AdvanceBackwards(enumerator);
		}

		// If we couldn't find a note in the opposite direction, try the given search direction.
		if (streamSpacing == null)
		{
			enumerator = precedingStepEnumerator.Clone();
			AdvanceForwards(enumerator);
			while (enumerator.IsCurrentValid())
			{
				var note = enumerator.Current!;
				if (note.IsStep())
				{
					streamSpacing = Math.Abs(borderingStepRow - note.GetRow());
					break;
				}

				AdvanceForwards(enumerator);
			}
		}

		// If this is the only step, just move to the step.
		if (streamSpacing == null)
		{
			MoveToChartPosition(precedingStepEnumerator.Current!.GetChartPosition());
			return;
		}

		// Advance by the stream spacing.
		enumerator = precedingStepEnumerator.Clone();
		AdvanceForwards(enumerator);
		var rowFromPrevious = borderingStepRow;
		while (enumerator.IsCurrentValid())
		{
			var note = enumerator.Current!;
			if (note.IsStep())
			{
				var spacing = Math.Abs(rowFromPrevious - note.GetRow());
				if (spacing != streamSpacing)
				{
					break;
				}

				rowFromPrevious = note.GetRow();
			}

			AdvanceForwards(enumerator);
		}

		MoveToChartPosition(rowFromPrevious);
		return;

		// ReSharper disable once UnusedLocalFunctionReturnValue
		bool AdvanceBackwards(IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator inEnum)
		{
			return preceding ? inEnum.MoveNext() : inEnum.MovePrev();
		}

		bool AdvanceForwards(IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator inEnum)
		{
			return preceding ? inEnum.MovePrev() : inEnum.MoveNext();
		}
	}

	private void OnMoveToPreviousMeasure()
	{
		var chartPosition = GetPosition().ChartPosition;
		var rate = FocusedChart?.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(chartPosition);
		if (rate == null)
			return;
		var sig = rate.GetTimeSignature();
		var rows = sig.GetNumerator() * (MaxValidDenominator * NumBeatsPerMeasure / sig.GetDenominator());
		MoveToChartPosition(chartPosition - rows);
	}

	private void OnMoveToNextMeasure()
	{
		var chartPosition = GetPosition().ChartPosition;
		var rate = FocusedChart?.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(chartPosition);
		if (rate == null)
			return;
		var sig = rate.GetTimeSignature();
		var rows = sig.GetNumerator() * (MaxValidDenominator * NumBeatsPerMeasure / sig.GetDenominator());
		MoveToChartPosition(chartPosition + rows);
	}

	public void OnMoveToPreviousLabel()
	{
		OnMoveToPreviousLabel(GetPosition().ChartPosition);
	}

	public void OnMoveToPreviousLabel(double chartPosition)
	{
		var label = FocusedChart?.GetLabels()?.FindPreviousEventWithLooping(chartPosition);
		if (label == null)
			return;
		MoveToChartPosition(label.GetRow());
	}

	public void OnMoveToNextLabel()
	{
		OnMoveToNextLabel(GetPosition().ChartPosition);
	}

	public void OnMoveToNextLabel(double chartPosition)
	{
		var label = FocusedChart?.GetLabels()?.FindNextEventWithLooping(chartPosition);
		if (label == null)
			return;
		MoveToChartPosition(label.GetRow());
	}

	public void OnMoveToPreviousPattern()
	{
		OnMoveToPreviousPattern(GetPosition().ChartPosition);
	}

	internal void OnMoveToPreviousPattern(EditorPatternEvent currentPattern)
	{
		OnMoveToPreviousPattern(currentPattern.GetChartPosition());
	}

	private void OnMoveToPreviousPattern(double chartPosition)
	{
		if (FocusedChart == null)
			return;
		var patterns = FocusedChart.GetPatterns();
		if (patterns.GetCount() == 0)
			return;

		// Get the previous pattern.
		// If there is no next pattern, loop to the end.
		var pattern = patterns.FindGreatestPreceding(chartPosition) ?? patterns.Last();

		// Get the EditorPatternEvent.
		if (pattern == null)
			return;
		if (!pattern.MoveNext())
			return;
		if (!pattern.IsCurrentValid())
			return;
		var patternEvent = pattern.Current;

		// Move to the position of the next pattern.
		MoveToChartPosition(patternEvent!.ChartRow);

		// Select the pattern.
		OnSelectPattern(patternEvent);
	}

	public void OnMoveToNextPattern()
	{
		OnMoveToNextPattern(GetPosition().ChartPosition);
	}

	internal void OnMoveToNextPattern(EditorPatternEvent currentPattern)
	{
		OnMoveToNextPattern(currentPattern.GetChartPosition());
	}

	private void OnMoveToNextPattern(double chartPosition)
	{
		if (FocusedChart == null)
			return;
		var patterns = FocusedChart.GetPatterns();
		if (patterns.GetCount() == 0)
			return;

		// Get the next pattern.
		// If there is no next pattern, loop to the first.
		var pattern = patterns.FindLeastFollowing(chartPosition) ?? patterns.First();

		// Get the EditorPatternEvent.
		if (pattern == null)
			return;
		if (!pattern.MoveNext())
			return;
		if (!pattern.IsCurrentValid())
			return;
		var patternEvent = pattern.Current;

		// Move to the position of the next pattern.
		MoveToChartPosition(patternEvent!.ChartRow);

		// Select the pattern.
		OnSelectPattern(patternEvent);
	}

	private void OnSelectPattern(EditorPatternEvent pattern)
	{
		GetActiveChartData(pattern.GetEditorChart())?.SelectPattern(pattern);
		UIPatternEvent.Instance.Open(true);
	}

	private void OnMoveToChartStart()
	{
		MoveToChartPosition(0.0);
	}

	private void OnMoveToChartEnd()
	{
		MoveToChartPosition(FocusedChart?.GetEndPosition() ?? 0.0);
	}

	private void OnSelectAttack(EditorAttackEvent attack)
	{
		GetActiveChartData(attack.GetEditorChart())?.SelectAttack(attack);
		UIAttackEvent.Instance.Open(true);
	}

	#endregion Chart Navigation

	#region Lane Input

	private void OnToggleNoteEntryMode()
	{
		var ordinal = (int)Preferences.Instance.NoteEntryMode;
		var numValues = Enum.GetNames(typeof(NoteEntryMode)).Length;
		ordinal = (ordinal + 1) % numValues;
		Preferences.Instance.NoteEntryMode = (NoteEntryMode)ordinal;
		Logger.Info($"Set Note Entry Mode to {Preferences.Instance.NoteEntryMode}");
	}

	private void OnArrowModificationKeyDown()
	{
		if (EditEarlyOut())
			return;
		var focusedChartData = GetFocusedChartData();
		focusedChartData?.OnArrowModificationKeyDown();
	}

	private void OnArrowModificationKeyUp()
	{
		if (EditEarlyOut())
			return;
		var focusedChartData = GetFocusedChartData();
		focusedChartData?.OnArrowModificationKeyUp();
	}

	private void OnLaneInputDown(int lane)
	{
		if (EditEarlyOut())
			return;
		var focusedChartData = GetFocusedChartData();
		focusedChartData?.OnLaneInputDown(lane, Playing, SnapManager.GetCurrentRows());
	}

	private void OnLaneInputUp(int lane)
	{
		var focusedChartData = GetFocusedChartData();
		focusedChartData?.OnLaneInputUp(lane);
	}

	private bool CancelLaneInput()
	{
		var focusedChartData = GetFocusedChartData();
		return focusedChartData?.CancelLaneInput() ?? false;
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

	internal void UpdateSongImage(SongImageType imageType, string path)
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

	/// <summary>
	/// Returns counts used for ActiveEditorCharts to determine their rendering limits.
	/// </summary>
	/// <returns>
	/// Number of visible charts.
	/// Total number of events for all visible charts.
	/// Total number of rate altering events for all visible charts.
	/// </returns>
	public (int numVisibleCharts, int numEvents, int numRateAlteringEvents) GetActiveChartCounts()
	{
		var numVisible = 0;
		var numEvents = 0;
		var numRateAlteringEvents = 0;
		foreach (var activeChart in ActiveChartData)
		{
			if (activeChart.IsVisible())
			{
				numVisible++;
				numEvents += activeChart.GetChart().GetEvents().GetCount();
				numRateAlteringEvents += activeChart.GetChart().GetRateAlteringEvents().GetCount();
			}
		}

		return (numVisible, numEvents, numRateAlteringEvents);
	}


	internal ActiveEditorChart GetFocusedChartData()
	{
		return FocusedChartData;
	}

	internal ActiveEditorChart GetActiveChartData(EditorChart chart)
	{
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (ActiveCharts[i] == chart)
				return ActiveChartData[i];
		}

		return null;
	}

	internal bool IsChartActive(EditorChart chart)
	{
		return GetActiveChartData(chart) != null;
	}

	internal bool DoesChartHaveDedicatedTab(EditorChart chart)
	{
		return GetActiveChartData(chart)?.HasDedicatedTab() ?? false;
	}

	private void RemoveActiveChart(EditorChart chart)
	{
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (ActiveCharts[i] == chart)
			{
				ActiveChartData[i].Clear();
				ActiveChartData.RemoveAt(i);
				ActiveCharts.RemoveAt(i);
				break;
			}
		}
	}

	internal void SetChartHasDedicatedTab(EditorChart chart, bool hasDedicatedTab)
	{
		if (chart == null)
			return;

		if (hasDedicatedTab)
		{
			ShowChart(chart, true);
			GetActiveChartData(chart)?.SetDedicatedTab(true);
		}
		else
		{
			// When setting a chart inactive, it is the only active chart, then do not close it.
			if (chart == FocusedChart && ActiveCharts.Count == 1)
			{
				GetActiveChartData(chart)?.SetDedicatedTab(false);
			}
			else
			{
				CloseChart(chart);
			}
		}
	}

	internal void CloseChart(EditorChart chart)
	{
		if (chart == null)
			return;

		var removingFocusedChart = FocusedChart == chart;

		// Get the original focal point so we can ensure it doesn't move.
		var focalPointX = GetFocalPointScreenSpaceX();
		var focalPointY = GetFocalPointScreenSpaceY();

		// Focus on an adjacent chart if we are removing the focused chart and an adjacent chart
		// exists. Do this first before removing the active chart because otherwise we'd temporarily
		// not be able to retrieve the old focused chart data while updating the focus.
		if (removingFocusedChart)
		{
			var focusedChartIndex = 0;
			for (var i = 0; i < ActiveCharts.Count; i++)
			{
				if (ActiveCharts[i] == chart)
				{
					focusedChartIndex = i;
					break;
				}
			}

			if (ActiveCharts.Count > 1)
			{
				if (focusedChartIndex + 1 < ActiveCharts.Count)
					SetChartFocused(ActiveCharts[focusedChartIndex + 1]);
				else if (focusedChartIndex - 1 >= 0 && focusedChartIndex - 1 < ActiveCharts.Count)
					SetChartFocused(ActiveCharts[focusedChartIndex - 1]);
			}
		}

		// Close the chart.
		RemoveActiveChart(chart);

		// If there are no more charts, clear the FocusedChart member.
		if (removingFocusedChart && ActiveCharts.Count == 0)
		{
			FocusedChart = null;
			FocusedChartData = null;
		}

		// Prevent the focal point from moving.
		SetFocalPointScreenSpace(focalPointX, focalPointY);

		UpdateChartPositions();
		RefreshPlayerLimitFromFocusedChart();
	}

	internal void MoveActiveChartLeft(EditorChart chart)
	{
		if (chart == null)
			return;

		var index = int.MaxValue;
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (chart == ActiveCharts[i])
			{
				index = i;
				break;
			}
		}

		if (index == int.MaxValue || index == 0)
			return;

		(ActiveCharts[index - 1], ActiveCharts[index]) = (ActiveCharts[index], ActiveCharts[index - 1]);
		(ActiveChartData[index - 1], ActiveChartData[index]) = (ActiveChartData[index], ActiveChartData[index - 1]);
	}

	internal void MoveActiveChartRight(EditorChart chart)
	{
		if (chart == null)
			return;

		var index = int.MaxValue;
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (chart == ActiveCharts[i])
			{
				index = i;
				break;
			}
		}

		if (index == int.MaxValue || index == ActiveCharts.Count - 1)
			return;

		(ActiveCharts[index + 1], ActiveCharts[index]) = (ActiveCharts[index], ActiveCharts[index + 1]);
		(ActiveChartData[index + 1], ActiveChartData[index]) = (ActiveChartData[index], ActiveChartData[index + 1]);
	}

	internal void ShowChart(EditorChart chart, bool withDedicatedTab)
	{
		if (chart == null)
			return;

		// If this chart is already active then return. It is already being shown.
		if (IsChartActive(chart))
			return;

		// Add an ActiveEditorChart for this chart, making it active and visible.
		ActiveCharts.Add(chart);
		var activeChartData = new ActiveEditorChart(
			this,
			chart,
			ZoomManager,
			TextureAtlas,
			KeyCommandManager);
		ActiveChartData.Add(activeChartData);
		SynchronizeActiveChartTime(activeChartData);

		// If this is the only shown chart, set it focused.
		if (ActiveCharts.Count == 1)
			SetChartFocused(chart);

		if (withDedicatedTab)
			activeChartData.SetDedicatedTab(true);

		// If there are now multiple charts shown without dedicated tabs, close the other charts.
		CleanUpActiveChartsWithoutDedicatedTabs(activeChartData);

		UpdateChartPositions();
	}

	private void CleanUpActiveChartsWithoutDedicatedTabs(ActiveEditorChart chartWithoutDedicatedTabToKeep)
	{
		var numChartsWithoutDedicatedTabs = 0;
		for (var i = 0; i < ActiveChartData.Count; i++)
		{
			if (!ActiveChartData[i].HasDedicatedTab())
			{
				numChartsWithoutDedicatedTabs++;
				if (numChartsWithoutDedicatedTabs > 1)
					break;
			}
		}

		if (numChartsWithoutDedicatedTabs <= 1)
			return;

		for (var i = ActiveChartData.Count - 1; i >= 0; i--)
		{
			if (ActiveChartData[i] == chartWithoutDedicatedTabToKeep)
				continue;
			if (!ActiveChartData[i].HasDedicatedTab())
			{
				CloseChart(ActiveCharts[i]);
				numChartsWithoutDedicatedTabs--;
			}

			if (numChartsWithoutDedicatedTabs <= 1)
				break;
		}
	}

	internal void SetChartFocused(EditorChart chart, bool undoable = false)
	{
		Debug.Assert(IsOnMainThread());

		if (chart == null)
			return;

		// We want to ensure the newly focused chart uses the same song time as the previously focused
		// chart. This prevents charts from jumping when they use different rate altering events.
		var oldSongTime = GetPosition().SongTime;
		// In the case of no active charts the position will be at row / chart position 0. Use a
		// better position.
		if (ActiveCharts.Count == 0)
		{
			// If we are playing the music, get the position from the music.
			if (Playing)
			{
				oldSongTime = MusicManager.GetMusicSongTime();
			}
			// Failing that, use the last song time from when a chart was open.
			else if (LastKnownSongTime != null)
			{
				oldSongTime = (double)LastKnownSongTime;
			}
		}


		var oldFocusedChartData = GetFocusedChartData();
		if (ActiveSong == null || FocusedChart == chart)
			return;

		// If the focused chart is being changed as an undoable action, enqueue the action and return.
		// The ActionSelectChart will invoke this method again with undoable set to false.
		if (undoable)
		{
			ActionQueue.Instance.Do(new ActionSelectChart(this, chart));
			return;
		}

		int? leftPosOfAllCharts = null;
		if (ActiveChartData.Count > 0)
			leftPosOfAllCharts = ActiveChartData[0].GetScreenSpaceXOfFullChartAreaStart();

		var currentActiveChart = GetActiveChartData(chart);

		// Set the focused chart.
		oldFocusedChartData?.SetFocused(false);
		FocusedChart = chart;

		// Ensure that if we are focusing on a chart, it is active.
		if (FocusedChart != null && currentActiveChart == null)
		{
			ActiveCharts.Insert(0, FocusedChart);
			var activeChartData = new ActiveEditorChart(
				this,
				chart,
				ZoomManager,
				TextureAtlas,
				KeyCommandManager);
			ActiveChartData.Insert(0, activeChartData);
		}

		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (ActiveCharts[i] == FocusedChart)
				FocusedChartData = ActiveChartData[i];
		}

		UpdatingSongTimeDirectly = true;
		FocusedChartData.Position.SongTime = oldSongTime;
		FocusedChartData.Position.SetDesiredPositionToCurrent();
		UpdatingSongTimeDirectly = false;
		FocusedChartData.SetFocused(true);
		CleanUpActiveChartsWithoutDedicatedTabs(FocusedChartData);

		// Adjust the focal point to keep the charts in the same spot.
		UpdateChartPositions();
		if (leftPosOfAllCharts != null)
		{
			var delta = ActiveChartData[0].GetScreenSpaceXOfFullChartAreaStart() - (int)leftPosOfAllCharts;
			if (delta != 0)
			{
				SetFocalPointScreenSpace(GetFocalPointScreenSpaceX() - delta, GetFocalPointScreenSpaceY());
				UpdateChartPositions();
			}
		}

		DensityGraph.SetStepDensity(FocusedChart?.GetStepDensity());
		EditorMouseState.SetActiveChart(FocusedChart);

		RefreshPlayerLimitFromFocusedChart();

		// Window title depends on the active chart.
		UpdateWindowTitle();

		// Start loading music for this Chart.
		OnMusicChanged();
		OnMusicPreviewChanged();
	}

	internal EditorChart AddChart(ChartType chartType, bool selectNewChart)
	{
		Debug.Assert(IsOnMainThread());

		if (ActiveSong == null)
			return null;
		var chart = ActiveSong.AddChart(chartType);
		FinishAddingChart(chart, selectNewChart);
		return chart;
	}

	internal EditorChart AddChart(EditorChart chart, bool selectNewChart)
	{
		Debug.Assert(IsOnMainThread());

		if (ActiveSong == null)
			return null;
		ActiveSong.AddChart(chart);
		FinishAddingChart(chart, selectNewChart);
		return chart;
	}

	private void FinishAddingChart(EditorChart chart, bool selectNewChart)
	{
		if (chart == null)
			return;
		chart.AddObserver(this);
		if (selectNewChart)
			SetChartFocused(chart);
		var player = Math.Max(0, Math.Min(chart.MaxPlayers - 1, Preferences.Instance.Player));
		PlayerPerChart.TryAdd(chart, player);
	}

	internal void DeleteChart(EditorChart chart, EditorChart chartToSelect)
	{
		Debug.Assert(IsOnMainThread());

		if (ActiveSong == null)
			return;

		CloseChart(chart);
		chart.RemoveObserver(this);
		ActiveSong.DeleteChart(chart);
		PlayerPerChart.Remove(chart);

		if (chartToSelect != null)
		{
			SetChartFocused(chartToSelect);
		}
		else if (FocusedChart == chart)
		{
			var newActiveChart = ActiveSong.SelectBestChart(FocusedChart.ChartType, FocusedChart.ChartDifficultyType);
			SetChartFocused(newActiveChart);
		}
	}

	#endregion Chart Selection

	#region Drag and Drop

	public override bool OnDragEnter(string[] files)
	{
		if (files?.Length != 1)
			return false;
		var file = files[0];

		// Get the extension to determine if the file type is supported.
		if (!Path.GetExtensionWithoutSeparator(file, out var extension))
			return false;
		return IsExtensionSupportedForFileDrop(extension);
	}

	/// <summary>
	/// Called when dropping a file into the window.
	/// </summary>
	private void OnDragDrop(object sender, FileDropEventArgs args)
	{
		var files = args.Files;
		if (files == null)
			return;
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
	public static bool IsExtensionSupportedForFileDrop(string extension)
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

	#region Patterns

	private void OnRegenerateSelectedPatterns()
	{
		var selection = GetFocusedChartData()?.GetSelection();
		if (selection == null)
			return;
		RegeneratePatterns(selection.GetSelectedPatterns(), false);
	}

	private void OnRegenerateSelectedPatternsWithNewSeeds()
	{
		var selection = GetFocusedChartData()?.GetSelection();
		if (selection == null)
			return;
		RegeneratePatterns(selection.GetSelectedPatterns(), true);
	}

	private void OnRegenerateAllPatterns()
	{
		if (FocusedChart == null)
			return;
		RegeneratePatterns(FocusedChart.GetPatterns(), false);
	}

	private void OnRegenerateAllPatternsWithNewSeeds()
	{
		if (FocusedChart == null)
			return;
		RegeneratePatterns(FocusedChart.GetPatterns(), true);
	}

	private void RegeneratePatterns(IEnumerable<EditorPatternEvent> patterns, bool useNewSeeds)
	{
		if (patterns == null || !patterns.Any())
			return;

		if (EditEarlyOut())
			return;

		if (useNewSeeds)
		{
			// Generate and commit new seeds as one action. This needs to be separate from the pattern
			// regeneration as generating patterns is asynchronous.
			var newSeedsAction = new ActionMultiple();
			var random = new Random();
			foreach (var pattern in patterns)
			{
				newSeedsAction.EnqueueAndDo(new ActionSetObjectFieldOrPropertyValue<int>(
					pattern, nameof(EditorPatternEvent.RandomSeed), random.Next(), true));
			}

			ActionQueue.Instance.EnqueueWithoutDoing(newSeedsAction);
		}

		// Regenerate the patterns.
		ActionQueue.Instance.Do(new ActionAutoGeneratePatterns(this, FocusedChart, patterns));
	}

	internal UIPatternComparer GetPatternComparer()
	{
		return PatternComparer;
	}

	internal UIPerformedChartComparer GetPerformedChartComparer()
	{
		return PerformedChartComparer;
	}

	#endregion Patterns

	#region Routine

	private void OnTogglePlayer()
	{
		if (FocusedChart == null)
			return;

		// Interpret toggling the player as intent to change it across all charts.
		PlayerPerChart.Clear();

		var p = Preferences.Instance;
		p.Player = (p.Player + 1) % FocusedChart.MaxPlayers;
		PlayerPerChart.Add(FocusedChart, p.Player);
	}

	private void SetPlayer(int player)
	{
		if (FocusedChart == null)
			return;

		// Interpret setting the player as intent to change it across all charts.
		PlayerPerChart.Clear();

		var p = Preferences.Instance;
		p.Player = Math.Clamp(player, 0, FocusedChart.MaxPlayers - 1);
		PlayerPerChart.Add(FocusedChart, p.Player);
	}

	private void RefreshPlayerLimitFromFocusedChart()
	{
		RefreshPlayerLimitFromChart(FocusedChart);
	}

	private void RefreshPlayerLimitFromChart(EditorChart chart)
	{
		if (chart == null)
			return;
		var p = Preferences.Instance;
		var player = p.Player;
		if (PlayerPerChart.TryGetValue(chart, out var cachedPlayer))
			player = cachedPlayer;
		player = Math.Clamp(player, 0, chart.MaxPlayers - 1);
		PlayerPerChart[chart] = player;
		if (chart == FocusedChart)
			p.Player = player;
	}

	public int GetPlayer()
	{
		return GetPlayer(FocusedChart);
	}

	internal int GetPlayer(EditorChart chart)
	{
		var p = Preferences.Instance;
		if (chart == null)
			return p.Player;
		if (PlayerPerChart.TryGetValue(chart, out var chartPlayer))
			return chartPlayer;
		return Math.Clamp(p.Player, 0, chart.MaxPlayers - 1);
	}

	#endregion Routine

	#region IObserver

	void Fumen.IObserver<EditorSong>.OnNotify(string eventId, EditorSong song, object payload)
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

	void Fumen.IObserver<EditorChart>.OnNotify(string eventId, EditorChart chart, object payload)
	{
		if (chart != FocusedChart)
			return;
		var focusedChartData = GetFocusedChartData();

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
			case EditorChart.NotificationEventAdded:
				OnEventAdded((EditorEvent)payload);
				break;
			case EditorChart.NotificationEventsAdded:
				OnEventsAdded((List<EditorEvent>)payload);
				break;
			case EditorChart.NotificationEventDeleted:
				OnEventDeleted((EditorEvent)payload);
				break;
			case EditorChart.NotificationEventsDeleted:
				OnEventsDeleted((List<EditorEvent>)payload);
				break;
			case EditorChart.NotificationEventsMoveStart:
				focusedChartData.OnEventMoveStart((EditorEvent)payload);
				break;
			case EditorChart.NotificationEventsMoveEnd:
				focusedChartData.OnEventMoveEnd((EditorEvent)payload);
				break;
			case EditorChart.NotificationAttackRequestEdit:
				OnSelectAttack((EditorAttackEvent)payload);
				break;
			case EditorChart.NotificationPatternRequestEdit:
				OnSelectPattern((EditorPatternEvent)payload);
				break;
			case EditorChart.NotificationMaxPlayersChanged:
				RefreshPlayerLimitFromChart(chart);
				break;
		}
	}

	void Fumen.IObserver<PreferencesOptions>.OnNotify(string eventId, PreferencesOptions options, object payload)
	{
		switch (eventId)
		{
			case PreferencesOptions.NotificationUndoHistorySizeChanged:
				OnUndoHistorySizeChanged();
				break;
		}
	}

	void Fumen.IObserver<PreferencesAudio>.OnNotify(string eventId, PreferencesAudio audio, object payload)
	{
		switch (eventId)
		{
			case PreferencesAudio.NotificationAudioOffsetChanged:
				OnAudioOffsetChanged();
				break;
			case PreferencesAudio.NotificationMusicRateChanged:
				OnMusicRateChanged();
				break;
			case PreferencesAudio.NotificationMainVolumeChanged:
				OnMainVolumeChanged();
				break;
			case PreferencesAudio.NotificationMusicVolumeChanged:
				OnMusicVolumeChanged();
				break;
			case PreferencesAudio.NotificationAssistTickVolumeChanged:
				OnAssistTickVolumeChanged();
				break;
			case PreferencesAudio.NotificationAssistTickAttackTimeChanged:
				OnAssistTickAttackTimeChanged();
				break;
			case PreferencesAudio.NotificationUseAssistTickChanged:
				OnUseAssistTickChanged();
				break;
			case PreferencesAudio.NotificationSkipAssistTickOnBeatTickChanged:
				OnSkipAssistTickOnBeatTickChanged();
				break;
			case PreferencesAudio.NotificationBeatTickVolumeChanged:
				OnBeatTickVolumeChanged();
				break;
			case PreferencesAudio.NotificationBeatTickAttackTimeChanged:
				OnBeatTickAttackTimeChanged();
				break;
			case PreferencesAudio.NotificationUseBeatTickChanged:
				OnUseBeatTickChanged();
				break;
			case PreferencesAudio.NotificationSkipBeatTickOnAssistTickChanged:
				OnSkipBeatTickOnAssistTickChanged();
				break;
		}
	}

	void Fumen.IObserver<ActionQueue>.OnNotify(string eventId, ActionQueue actionQueue, object payload)
	{
		switch (eventId)
		{
			case ActionQueue.NotificationAsyncActionStarted:
				OnCanEditChanged();
				break;
		}
	}

	#endregion IObserver

	#region Debug

#if DEBUG
	[Conditional("DEBUG")]
	public void DebugShowSplashSequence()
	{
		LoadSplashTextures();
		SplashTime = TotalSplashTime;
	}

	[Conditional("DEBUG")]
	public void DebugSaveTimeAndZoom()
	{
		Preferences.Instance.DebugSongTime = GetPosition().SongTime;
		Preferences.Instance.DebugZoom = ZoomManager.GetSpacingZoom();
	}

	[Conditional("DEBUG")]
	public void DebugLoadTimeAndZoom()
	{
		SetSongTime(Preferences.Instance.DebugSongTime);
		ZoomManager.SetZoom(Preferences.Instance.DebugZoom);
	}

#endif

	#endregion Debug
}
