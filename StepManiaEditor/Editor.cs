using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fumen;
using Fumen.ChartDefinition;
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
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Path = Fumen.Path;

[assembly: InternalsVisibleTo("StepManiaEditorTests")]

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
	private bool AutogenConfigsLoaded;
	private bool HasCheckedForAutoLoadingLastSong;

	public static readonly ChartType[] SupportedChartTypes =
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

	private static readonly int MaxNumLanesForAnySupportedChartType;

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
	private RenderTarget2D[] WaveformRenderTargets;
	private StepDensityEffect DensityGraph;

	private SongLoadTask SongLoadTask;
	private FileSystemWatcher SongFileWatcher;
	private bool ShouldCheckForShowingSongFileChangedNotification;
	private bool ShowingSongFileChangedNotification;
	private int GarbageCollectFrame;

	private readonly Dictionary<ChartType, PadData> PadDataByChartType = new();
	private readonly Dictionary<ChartType, StepGraph> StepGraphByChartType = new();
	private Dictionary<ChartType, List<List<GraphNode>>> RootNodesByChartType = new();
	private StepTypeFallbacks StepTypeFallbacks;

	private double PlaybackStartTime;
	private Stopwatch PlaybackStopwatch;

	private bool IsChartAreaSet;
	private Rectangle ChartArea;

	private EditorSong ActiveSong
	{
		set
		{
			Debug.Assert(IsOnMainThread());
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

	private readonly List<EditorChart> ActiveCharts = new();
	private readonly List<ActiveEditorChart> ActiveChartData = new();
	private readonly List<EditorEvent> CopiedEvents = new();

	// Movement controls.
	private bool UpdatingSongTimeDirectly;

	private KeyCommandManager KeyCommandManager;
	private bool Playing;
	private bool PlayingPreview;
	private bool MiniMapCapturingMouse;
	private bool DensityGraphCapturingMouse;
	private bool StartPlayingWhenMouseScrollingDone;
	private bool TransformingSelectedNotes;

	private uint MaxScreenHeight;

	private string FormTitle;

	// Fonts
	private ImFontPtr ImGuiFont;
	private SpriteFont Font;

	// Cursor
	private MouseCursor CurrentDesiredCursor = MouseCursor.Arrow;
	private MouseCursor PreviousDesiredCursor = MouseCursor.Arrow;

	// Performance Monitoring
	private PerformanceMonitor PerformanceMonitor;

	// Debug
	private bool ShowImGuiTestWindow;
	private readonly int MainThreadId;

	// Logger
	private string LogFilePath;
	private readonly LinkedList<Logger.LogMessage> LogBuffer = new();
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
	public Editor()
	{
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

#if DEBUG
		// Disable the splash screen on debug builds.
		// It is required to show it on Release builds as it includes the FMOD logo which is required to be
		// shown on a splash screen under the FMOD EULA.
		SplashTime = 0.0f;
#endif
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
		InitializeImGui();
		InitializeFonts();
		InitializeGuiDpiScale();
		InitializeScreenHeight();
		InitializeWaveFormRenderer();
		InitializeDensityGraph();
		InitializeMiniMap();
		InitializeUIHelpers();
		InitializeKeyCommandManager();
		InitializeSongLoadTask();
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
		Preferences.Load(this);
	}

	private void InitializeImGuiUtils()
	{
		Init(this);
	}

	private async void InitializeAutogenConfigsAsync()
	{
		PatternConfigManager.Instance.SetConfigComparer(PatternComparer);
		PerformedChartConfigManager.Instance.SetConfigComparer(PerformedChartComparer);

		// Load autogen configs asynchronously.
		await Task.WhenAll(new List<Task>(3)
		{
			PerformedChartConfigManager.Instance.LoadConfigsAsync(),
			ExpressedChartConfigManager.Instance.LoadConfigsAsync(),
			PatternConfigManager.Instance.LoadConfigsAsync(),
		});
		AutogenConfigsLoaded = true;
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
		MusicManager = new MusicManager(SoundManager, p.AudioOffset);
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
	}

	private void InitializeKeyCommandManager()
	{
		var p = Preferences.Instance.PreferencesKeyBinds;
		// @formatter:off
		KeyCommandManager = new KeyCommandManager();

		const string fileIo = "File I/O";
		AddKeyCommand(fileIo, "Open", p.Open, OnOpen);
		AddKeyCommand(fileIo, "Open Containing Folder", p.OpenContainingFolder, OnOpenContainingFolder);
		AddKeyCommand(fileIo, "Save As", p.SaveAs, OnSaveAs);
		AddKeyCommand(fileIo, "Save", p.Save, OnSave);
		AddKeyCommand(fileIo, "New", p.New, OnNew);
		AddKeyCommand(fileIo, "Reload", p.Reload, OnReload);

		const string undo = "Undo";
		AddKeyCommand(undo, "Undo", p.Undo, OnUndo, true);
		AddKeyCommand(undo, "Redo", p.Redo, OnRedo, true);

		const string selection = "Selection";
		UIControls.Instance.AddCommand(selection, "Select Note", "Left Mouse Button");
		UIControls.Instance.AddCommand(selection, "Select In Region", "Drag Left Mouse Button");
		UIControls.Instance.AddCommand(selection, "Select Misc. Events In Region", "Alt+Drag Left Mouse Button");
		UIControls.Instance.AddCommand(selection, "Add to Selection", "Ctrl+Left Mouse Button");
		UIControls.Instance.AddCommand(selection, "Extend Selection", "Shift+Left Mouse Button");
		AddKeyCommand(selection, "Select All Notes", p.SelectAllNotes, OnSelectAll);
		AddKeyCommand(selection, "Select All Misc. Events", p.SelectAllMiscEvents, OnSelectAllAlt);
		AddKeyCommand(selection, "Select All", p.SelectAll, OnSelectAllShift);

		const string copyPaste = "Copy / Paste";
		AddKeyCommand(copyPaste, "Copy", p.Copy, OnCopy, true);
		AddKeyCommand(copyPaste, "Cut", p.Cut, OnCut, true);
		AddKeyCommand(copyPaste, "Paste", p.Paste, OnPaste, true);

		const string sound = "Sound";
		AddKeyCommand(sound, "Toggle Preview", p.TogglePreview, OnTogglePlayPreview);
		AddKeyCommand(sound, "Toggle Assist Tick", p.ToggleAssistTick, OnToggleAssistTick);
		AddKeyCommand(sound, "Toggle Beat Tick", p.ToggleBeatTick, OnToggleBeatTick);
		AddKeyCommand(sound, "Decrease Music Rate", p.DecreaseMusicRate, OnDecreaseMusicRate, true);
		AddKeyCommand(sound, "Increase Music Rate", p.IncreaseMusicRate, OnIncreaseMusicRate, true);

		const string general = "General";
		AddKeyCommand(general, "Play / Pause", p.PlayPause, OnTogglePlayback);
		AddKeyCommand(general, "Cancel / Go Back", p.CancelGoBack, OnEscape);
		AddKeyCommand(general, "Toggle Note Entry Mode", p.ToggleNoteEntryMode, OnToggleNoteEntryMode);
		AddKeyCommand(general, "Toggle Spacing Mode", p.ToggleSpacingMode, OnToggleSpacingMode);
		UIControls.Instance.AddCommand(general, "Context Menu", "Right Mouse Button");
		UIControls.Instance.AddCommand(general, "Exit", "Alt+F4");

		const string chartSelection ="Chart Selection";
		AddKeyCommand(chartSelection, "Open Previous Chart", p.OpenPreviousChart, OpenPreviousChart);
		AddKeyCommand(chartSelection, "Open Next Chart", p.OpenNextChart, OpenNextChart);
		AddKeyCommand(chartSelection, "Close Focused Chart", p.CloseFocusedChart, CloseFocusedChart);
		AddKeyCommand(chartSelection, "Keep Chart Open", p.KeepChartOpen, SetFocusedChartHasDedicatedTab);
		AddKeyCommand(chartSelection, "Move Focused Chart Left", p.MoveFocusedChartLeft, MoveFocusedChartLeft);
		AddKeyCommand(chartSelection, "Move Focused Chart Right", p.MoveFocusedChartRight, MoveFocusedChartRight);
		AddKeyCommand(chartSelection, "Focus Previous Chart", p.FocusPreviousChart, FocusPreviousChart);
		AddKeyCommand(chartSelection, "Focus Next Chart", p.FocusNextChart, FocusNextChart);

		const string zoom = "Zoom";
		UIControls.Instance.AddCommand(zoom, "Zoom In", "Ctrl+Scroll Up");
		UIControls.Instance.AddCommand(zoom, "Zoom Out", "Ctrl+Scroll Down");
		UIControls.Instance.AddCommand(zoom, "Increasing Spacing For Current Mode", "Shift+Scroll Up");
		UIControls.Instance.AddCommand(zoom, "Decrease Spacing For Current Mode", "Shift+Scroll Down");

		const string navigation = "Navigation";
		AddKeyCommand(navigation, "Decrease Snap", p.DecreaseSnap, OnDecreaseSnap, true);
		AddKeyCommand(navigation, "Increase Snap", p.IncreaseSnap, OnIncreaseSnap, true);
		AddKeyCommand(navigation, "Move Up", p.MoveUp, OnMoveUp, true);
		UIControls.Instance.AddCommand(navigation, "Move Up", "Scroll Up");
		AddKeyCommand(navigation, "Move Down", p.MoveDown, OnMoveDown, true);
		UIControls.Instance.AddCommand(navigation, "Move Down", "Scroll Down");
		AddKeyCommand(navigation, "Move To Previous Measure", p.MoveToPreviousMeasure, OnMoveToPreviousMeasure, true);
		AddKeyCommand(navigation, "Move To Next Measure", p.MoveToNextMeasure, OnMoveToNextMeasure, true);
		AddKeyCommand(navigation, "Move To Chart Start", p.MoveToChartStart, OnMoveToChartStart);
		AddKeyCommand(navigation, "Move To Chart End", p.MoveToChartEnd, OnMoveToChartEnd);
		AddKeyCommand(navigation, "Move To Next Label", p.MoveToNextLabel, OnMoveToNextLabel, true);
		AddKeyCommand(navigation, "Move To Previous Label", p.MoveToPreviousLabel, OnMoveToPreviousLabel, true);

		const string patterns = "Patterns";
		AddKeyCommand(patterns, "Move To Next Pattern", p.MoveToNextPattern, OnMoveToNextPattern, true);
		AddKeyCommand(patterns, "Move To Previous Pattern", p.MoveToPreviousPattern, OnMoveToPreviousPattern, true);
		AddKeyCommand(patterns, "Regenerate All Patterns (Fixed Seeds)", p.RegenerateAllPatternsFixedSeeds, OnRegenerateAllPatterns);
		AddKeyCommand(patterns, "Regenerate All Patterns (New Seeds)", p.RegenerateAllPatternsNewSeeds, OnRegenerateAllPatternsWithNewSeeds);
		AddKeyCommand(patterns, "Regenerate Selected Patterns (Fixed Seeds)", p.RegenerateSelectedPatternsFixedSeeds, OnRegenerateSelectedPatterns);
		AddKeyCommand(patterns, "Regenerate Selected Patterns (New Seeds)", p.RegenerateSelectedPatternsNewSeeds, OnRegenerateSelectedPatternsWithNewSeeds);

		const string editSelection = "Edit Selection";
		AddKeyCommand(editSelection, "Delete", p.Delete, OnDelete);
		AddKeyCommand(editSelection, "Shift Left", p.ShiftLeft, OnShiftSelectedNotesLeft, true);
		AddKeyCommand(editSelection, "Shift Left And Wrap", p.ShiftLeftAndWrap, OnShiftSelectedNotesLeftAndWrap, true);
		AddKeyCommand(editSelection, "Shift Right", p.ShiftRight, OnShiftSelectedNotesRight, true);
		AddKeyCommand(editSelection, "Shift Right And Wrap", p.ShiftRightAndWrap, OnShiftSelectedNotesRightAndWrap, true);
		AddKeyCommand(editSelection, "Shift Earlier", p.ShiftEarlier, OnShiftSelectedNotesEarlier, true);
		AddKeyCommand(editSelection, "Shift Later", p.ShiftLater, OnShiftSelectedNotesLater, true);
		// @formatter:on

		const string noteEntry = "Note Entry";
		for (var l = 0; l < PreferencesKeyBinds.NumLaneInputs; l++)
		{
			var capturedLane = l;
			AddKeyCommand(noteEntry, $"Lane {capturedLane + 1:D2} Tap",
				p.GetArrowInputs(capturedLane),
				() => OnLaneInputDown(capturedLane),
				false,
				() => OnLaneInputUp(capturedLane));
		}

		for (var l = 0; l < PreferencesKeyBinds.NumLaneInputs; l++)
		{
			var holdCommandString =
				UIControls.GetCommandString(p.GetArrowInputs(l))
				+ UIControls.MultipleKeysJoinString
				+ UIControls.GetCommandString(p.MoveUp)
				+ UIControls.OrString
				+ UIControls.GetCommandString(p.MoveDown);
			UIControls.Instance.AddCommand(noteEntry, $"Lane {l + 1:D2} Hold", holdCommandString);
		}

		for (var l = 0; l < PreferencesKeyBinds.NumLaneInputs; l++)
		{
			var mineCommandString =
				UIControls.GetCommandString(p.GetArrowInputs(l))
				+ UIControls.MultipleKeysJoinString
				+ UIControls.GetCommandString(p.ArrowModification);
			UIControls.Instance.AddCommand(noteEntry, $"Lane {l + 1:D2} Mine", mineCommandString);
		}

		for (var l = 0; l < PreferencesKeyBinds.NumLaneInputs; l++)
		{
			var rollCommandString =
				UIControls.GetCommandString(p.GetArrowInputs(l))
				+ UIControls.MultipleKeysJoinString
				+ UIControls.GetCommandString(p.ArrowModification)
				+ UIControls.MultipleKeysJoinString
				+ UIControls.GetCommandString(p.MoveUp)
				+ UIControls.OrString
				+ UIControls.GetCommandString(p.MoveDown);
			UIControls.Instance.AddCommand(noteEntry, $"Lane {l + 1:D2} Roll", rollCommandString);
		}

		foreach (var input in p.ArrowModification)
			KeyCommandManager.Register(new KeyCommandManager.Command(input, OnArrowModificationKeyDown, false,
				OnArrowModificationKeyUp));
	}

	/// <summary>
	/// Helper to add a key command and register it for the controls UI.
	/// </summary>
	private void AddKeyCommand(
		string category,
		string name,
		List<Keys[]> inputs,
		Action callback,
		bool repeat = false,
		Action releaseCallback = null)
	{
		foreach (var input in inputs)
		{
			// Register the command, potentially multiple times due to left and right modifier keys.
			var adjustedInput = new Keys[input.Length];
			RegisterKeyCommand(input, adjustedInput, 0, callback, repeat, releaseCallback);
			// Add the command to the UI.
			UIControls.Instance.AddCommand(category, name, input);
		}
	}

	/// <summary>
	/// Recursive helper to register multiple commands for inputs with left modifier keys so they include
	/// right modifier keys as well.
	/// </summary>
	private void RegisterKeyCommand(
		Keys[] input,
		Keys[] adjustedInput,
		int inputIndex,
		Action callback,
		bool repeat = false,
		Action releaseCallback = null)
	{
		if (inputIndex == adjustedInput.Length)
		{
			KeyCommandManager.Register(new KeyCommandManager.Command(adjustedInput, callback, repeat, releaseCallback));
			return;
		}

		switch (input[inputIndex])
		{
			case Keys.LeftControl:
			{
				adjustedInput[inputIndex] = Keys.LeftControl;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				adjustedInput[inputIndex] = Keys.RightControl;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				break;
			}
			case Keys.LeftShift:
			{
				adjustedInput[inputIndex] = Keys.LeftShift;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				adjustedInput[inputIndex] = Keys.RightShift;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				break;
			}
			case Keys.LeftAlt:
			{
				adjustedInput[inputIndex] = Keys.LeftAlt;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				adjustedInput[inputIndex] = Keys.RightAlt;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				break;
			}
			case Keys.LeftWindows:
			{
				adjustedInput[inputIndex] = Keys.LeftWindows;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				adjustedInput[inputIndex] = Keys.RightWindows;
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				break;
			}
			default:
				adjustedInput[inputIndex] = input[inputIndex];
				RegisterKeyCommand(input, adjustedInput, inputIndex + 1, callback, repeat, releaseCallback);
				break;
		}
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

	private void InitializeWindowSize()
	{
		var p = Preferences.Instance;
		Graphics.PreferredBackBufferHeight = p.WindowHeight;
		Graphics.PreferredBackBufferWidth = p.WindowWidth;
		Graphics.IsFullScreen = false;
		Graphics.ApplyChanges();
	}

	private void InitializeImGui()
	{
		ImGuiRenderer = new ImGuiRenderer(this, ImGuiConfigFlags.DockingEnable);
	}

	private void InitializeFormCallbacks()
	{
		var p = Preferences.Instance;
		var form = (Form)Control.FromHandle(Window.Handle);
		if (form == null)
			return;

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
		foreach (var adapter in GraphicsAdapter.Adapters)
			MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
		EditorEvent.SetScreenHeight(MaxScreenHeight);
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
		RecreateWaveformRenderTargets(true);
	}

	private uint GetDesiredWaveFormTextureHeight()
	{
		return (uint)Math.Max(1, GetViewportHeight());
	}

	private void RecreateWaveformRenderTargets(bool force)
	{
		var desiredHeight = GetDesiredWaveFormTextureHeight();
		if (!force)
		{
			if (WaveFormTextureHeight == desiredHeight)
				return;
		}

		WaveFormTextureHeight = desiredHeight;

		WaveformRenderTargets = new RenderTarget2D[2];
		for (var i = 0; i < 2; i++)
		{
			WaveformRenderTargets[i] = new RenderTarget2D(
				GraphicsDevice,
				WaveFormTextureWidth,
				(int)WaveFormTextureHeight,
				false,
				GraphicsDevice.PresentationParameters.BackBufferFormat,
				DepthFormat.Depth24);
		}
	}

	private void InitializeDensityGraph()
	{
		DensityGraph = new StepDensityEffect(Graphics, GraphicsDevice, Font);
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
		UIChartList.Instance.Init(this);
		UIWaveFormPreferences.Instance.Init(MusicManager);
		UIReceptorPreferences.Instance.Init(this);
		UIAudioPreferences.Instance.Init(SoundManager);
		UIExpressedChartConfig.Instance.Init(this);
		UIPerformedChartConfig.Instance.Init(this);
		UIPatternConfig.Instance.Init(this);
		UIAutogenConfigs.Instance.Init(this);
		UIAutogenChart.Instance.Init(this);
		UIAutogenChartsForChartType.Instance.Init(this);
		UICopyEventsBetweenCharts.Instance.Init(this);
		UIPatternEvent.Instance.Init(this);
		UIPerformance.Instance.Init(PerformanceMonitor);
		UIHotbar.Instance.Init(this);
#if DEBUG
		UIDebug.Instance.Init(this);
#endif
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

	private void LoadSplashTextures()
	{
		LogoAttribution ??= Content.Load<Texture2D>(TextureIdLogoAttribution);
		if (SplashBackground == null)
		{
			SplashBackground = new Texture2D(GraphicsDevice, 1, 1);
			SplashBackground.SetData(new[] { Color.Black });
		}
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
			ShowUnsavedChangesModal();
		}
		else if (IsSaving())
		{
			e.Cancel = true;
			PostSaveFunction = OnExitNoSave;
		}
	}

	protected override void EndRun()
	{
		CloseSong();
		MusicManager.Shutdown();
		// Commit preferences to disk.
		Preferences.Save();
		// Commit unsaved changes to autogen configs to disk.
		PerformedChartConfigManager.Instance.SynchronizeToDisk();
		ExpressedChartConfigManager.Instance.SynchronizeToDisk();
		PatternConfigManager.Instance.SynchronizeToDisk();
		Logger.Shutdown();

		ImGuiRenderer.UnbindTexture(TextureAtlasImGuiTexture);

		base.EndRun();
	}

	private void OnExit()
	{
		if (ActionQueue.Instance.HasUnsavedChanges())
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

	public IReadOnlyEditorPosition GetPosition()
	{
		return GetFocusedChartData()?.Position ?? EditorPosition.Zero;
	}

	public EditorMouseState GetMouseState()
	{
		return EditorMouseState;
	}

	public EditorSong GetActiveSong()
	{
		return ActiveSong;
	}

	public EditorChart GetFocusedChart()
	{
		return FocusedChart;
	}

	#endregion State Accessors

	#region Position Modification

	public void SetSongTime(double songTime)
	{
		foreach (var activeChartData in ActiveChartData)
		{
			activeChartData.Position.SongTime = songTime;
		}
	}

	public void SetChartTime(double chartTime)
	{
		foreach (var activeChartData in ActiveChartData)
		{
			activeChartData.Position.ChartTime = chartTime;
		}
	}

	public void SetChartPosition(double chartPosition)
	{
		foreach (var activeChartData in ActiveChartData)
		{
			activeChartData.Position.ChartPosition = chartPosition;
		}
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

	private void SetResolution(int x, int y)
	{
		var form = (Form)Control.FromHandle(Window.Handle);
		if (form == null)
			return;
		form.WindowState = FormWindowState.Normal;
		form.Size = new System.Drawing.Size(x, y);
	}

	public void OnResize(object sender, EventArgs e)
	{
		var form = (Form)Control.FromHandle(Window.Handle);
		if (form == null)
			return;
		var maximized = form.WindowState == FormWindowState.Maximized;
		var w = GetViewportWidth();
		var h = GetViewportHeight();

		// Update window preferences.
		if (!maximized)
		{
			Preferences.Instance.WindowWidth = w;
			Preferences.Instance.WindowHeight = h;
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
				GetWaveFormWidth(),
				(uint)Math.Max(1, ChartArea.Height));
			RecreateWaveformRenderTargets(false);
		}
	}

	/// <summary>
	/// Gets the viewport width.
	/// These dimensions include window dressing like the the title bar.
	/// </summary>
	/// <returns>Viewport width in pixels.</returns>
	public int GetViewportWidth()
	{
		return Graphics.GraphicsDevice.Viewport.Width;
	}

	/// <summary>
	/// Gets the viewport width.
	/// These dimensions include window dressing like the the title bar.
	/// </summary>
	/// <returns>Viewport width in pixels.</returns>
	public int GetViewportHeight()
	{
		return Graphics.GraphicsDevice.Viewport.Height;
	}

	/// <summary>
	/// Gets the back buffer width.
	/// These dimensions only include the application's rendered area and do not
	/// include window dressing like the the title bar.
	/// </summary>
	/// <returns>Back buffer width in pixels.</returns>
	public int GetBackBufferWidth()
	{
		return Graphics.PreferredBackBufferWidth;
	}

	/// <summary>
	/// Gets the back buffer height.
	/// These dimensions only include the application's rendered area and do not
	/// include window dressing like the the title bar.
	/// </summary>
	/// <returns>Back buffer height in pixels.</returns>
	public int GetBackBufferHeight()
	{
		return Graphics.PreferredBackBufferHeight;
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
		Debug.Assert(IsOnMainThread());

		PerformanceMonitor.SetTime(PerformanceTimings.Present, PreviousPresentTime.Ticks);

		PerformanceMonitor.SetEnabled(!Preferences.Instance.PreferencesPerformance.PerformanceMonitorPaused);
		PerformanceMonitor.BeginFrame(gameTime.TotalGameTime.Ticks);
		PerformanceMonitor.SetTime(PerformanceTimings.PresentWait, PresentWaitTime.Ticks);
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

			var newChartArea = UIDockSpace.GetCentralNodeArea();
			if (newChartArea != ChartArea)
			{
				ChartArea = newChartArea;
				IsChartAreaSet = true;
				OnChartAreaChanged();
			}

			CheckForAutoLoadingLastSong();

			ActiveSong?.Update();
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
			var hasUnsavedChanges = ActionQueue.Instance.HasUnsavedChanges();
			if (UnsavedChangesLastFrame != hasUnsavedChanges)
			{
				UnsavedChangesLastFrame = hasUnsavedChanges;
				UpdateWindowTitle();
			}

			CheckForShowingSongFileChangedNotification();

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

	private void UpdateMusicAndPosition(double currentTime)
	{
		var focusedChartData = GetFocusedChartData();
		if (!Playing && focusedChartData != null)
		{
			if (focusedChartData.Position.IsInterpolatingChartPosition())
			{
				UpdatingSongTimeDirectly = true;
				focusedChartData.Position.UpdateChartPositionInterpolation(currentTime);
				foreach (var activeChartData in ActiveChartData)
				{
					if (activeChartData != focusedChartData)
					{
						activeChartData.Position.ChartPosition = focusedChartData.Position.ChartPosition;
						activeChartData.Position.SetDesiredPositionToCurrent();
					}
				}

				MusicManager.SetMusicTimeInSeconds(focusedChartData.Position.SongTime);
				UpdatingSongTimeDirectly = false;
			}

			if (focusedChartData.Position.IsInterpolatingSongTime())
			{
				UpdatingSongTimeDirectly = true;
				focusedChartData.Position.UpdateSongTimeInterpolation(currentTime);
				foreach (var activeChartData in ActiveChartData)
				{
					if (activeChartData != focusedChartData)
					{
						activeChartData.Position.ChartPosition = focusedChartData.Position.ChartPosition;
						activeChartData.Position.SetDesiredPositionToCurrent();
					}
				}

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
		WaveFormRenderer.SetScaleXWhenZooming(pWave.WaveFormScaleXWhenZooming);
		WaveFormRenderer.SetDesiredWaveFormWidth(GetWaveFormWidth());
		WaveFormRenderer.Update(GetPosition().SongTime, waveFormPPS * ZoomManager.GetSpacingZoom());
	}

	public uint GetWaveFormWidth()
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		if (p.WaveFormScaleWidthToChart && FocusedChart != null)
		{
			if (p.WaveFormScaleXWhenZooming)
				return (uint)Math.Max(0, GetFocusedChartData().GetLaneAreaWidthWithCurrentScale());
			return (uint)Math.Max(0, GetFocusedChartData().GetLaneAreaWidth());
		}

		if (p.WaveFormScaleXWhenZooming)
			return (uint)(WaveFormTextureWidth * ZoomManager.GetSpacingZoom());
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
		var inFocus = IsApplicationFocused();

		CurrentDesiredCursor = MouseCursor.Arrow;
		CanShowRightClickPopupThisFrame = false;

		var focusedChartData = GetFocusedChartData();
		focusedChartData?.UpdateSelectedRegion(currentTime);

		// TODO: Remove remaining input processing from ImGuiRenderer.
		ImGuiRenderer.UpdateInput(gameTime);

		// ImGui needs to be told when a new frame begins after processing input.
		// This application also relies on the new frame being begun in input processing
		// as some inputs need to check bounds with ImGui elements that require pushing
		// font state.
		BeginImGuiFrame();

		// Process Mouse Input.
		var state = Mouse.GetState();
		var (mouseChartTime, mouseChartPosition) = FindChartTimeAndRowForScreenY(state.Y);
		EditorMouseState.Update(state, mouseChartTime, mouseChartPosition, inFocus);
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).UpThisFrame())
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
			ProcessInputForDraggingHeader(mouseX, mouseY);

			// ImGui may want the mouse on a release when we are selecting. Stop selecting in that case.
			focusedChartData?.FinishSelectedRegion();
			UpdateCursor();
			return;
		}

		var atEdgeOfScreen = mouseX <= 0 || mouseX >= GetViewportWidth() - 1 || mouseY <= 0 || mouseY >= GetViewportHeight() - 1;
		var downThisFrame = EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame();
		var clickingToDragWindowEdge = atEdgeOfScreen && downThisFrame;

		// If the user has clicked this frame and they are at the edge of screen treat that as an intent to resize the window.
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

			// Process input for the mini map.
			if (!(focusedChartData?.IsSelectedRegionActive() ?? false) && !MovingFocalPoint && !DensityGraphCapturingMouse)
				ProcessInputForMiniMap();

			// Process input for the density graph
			if (!(focusedChartData?.IsSelectedRegionActive() ?? false) && !MovingFocalPoint && !MiniMapCapturingMouse)
				ProcessInputForDensityGraph();

			// Process input for grabbing the receptors and moving the focal point.
			if (!(focusedChartData?.IsSelectedRegionActive() ?? false) && !MiniMapCapturingMouse && !DensityGraphCapturingMouse)
			{
				ProcessInputForMovingFocalPoint(inReceptorArea);
				// Update cursor based on whether the receptors could be grabbed.
				if (inReceptorArea && !Preferences.Instance.PreferencesReceptors.LockPosition)
					CurrentDesiredCursor = MouseCursor.SizeAll;
			}

			// Process input for selecting a region.
			if (!MiniMapCapturingMouse && !MovingFocalPoint && !DensityGraphCapturingMouse)
			{
				GetFocusedChartData()?.ProcessInputForSelectedRegion(
					currentTime,
					uiInterferingWithRegionClicking,
					EditorMouseState,
					EditorMouseState.GetButtonState(EditorMouseState.Button.Left));
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
			UIDockSpace.PrepareDockSpace();
		}
	}

	private void UpdateCursor()
	{
		// Setting the cursor every frame prevents it from changing to support normal application
		// behavior like indicating resizeability at the edges of the window. But not setting every frame
		// causes it to go back to the Default. Set it every frame only if it setting it to something
		// other than the Default.
		if (CurrentDesiredCursor != PreviousDesiredCursor || CurrentDesiredCursor != MouseCursor.Arrow)
		{
			Mouse.SetCursor(CurrentDesiredCursor);
		}

		PreviousDesiredCursor = CurrentDesiredCursor;
	}

	/// <summary>
	/// Processes input for moving the focal point with the mouse by dragging a header.
	/// </summary>
	/// <remarks>Helper for ProcessInput.</remarks>
	private void ProcessInputForDraggingHeader(int mouseX, int mouseY)
	{
		var overDraggableArea = false;
		foreach (var chart in ActiveChartData)
		{
			if (chart.IsOverHeaderDraggableArea(mouseX, mouseY))
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
	private void ProcessInputForMovingFocalPoint(bool inReceptorArea, bool forceOnlyHorizontalMove = false)
	{
		// Begin moving focal point.
		if (EditorMouseState.GetButtonState(EditorMouseState.Button.Left).DownThisFrame()
		    && inReceptorArea
		    && !Preferences.Instance.PreferencesReceptors.LockPosition)
		{
			MovingFocalPoint = true;
			ForceOnlyHorizontalFocalPointMove = forceOnlyHorizontalMove;
			FocalPointAtMoveStart = GetFocalPointChartSpace();
			FocalPointMoveOffset = new Vector2(EditorMouseState.X() - GetFocalPointScreenSpaceX(),
				EditorMouseState.Y() - GetFocalPointScreenSpaceY());
		}

		// Move focal point.
		if (MovingFocalPoint)
		{
			void SetReceptorPosition(int x, int y)
			{
				var p = Preferences.Instance.PreferencesReceptors;
				if (p.ChartSpacePositionX != x || p.ChartSpacePositionY != y)
					FocalPointMoved = true;
				p.ChartSpacePositionX = x;
				p.ChartSpacePositionY = y;
			}

			var newX = TransformScreenSpaceXToChartSpaceX(EditorMouseState.X()) - (int)FocalPointMoveOffset.X;
			var newY = TransformScreenSpaceYToChartSpaceY(EditorMouseState.Y()) - (int)FocalPointMoveOffset.Y;

			if (KeyCommandManager.IsAnyInputDown(Preferences.Instance.PreferencesKeyBinds.LockReceptorMoveAxis))
			{
				if (Math.Abs(newX - FocalPointAtMoveStart.X) > Math.Abs(newY - FocalPointAtMoveStart.Y))
					SetReceptorPosition(newX, (int)FocalPointAtMoveStart.Y);
				else
					SetReceptorPosition((int)FocalPointAtMoveStart.X, newY);
			}
			else if (ForceOnlyHorizontalFocalPointMove)
			{
				SetReceptorPosition(newX, (int)FocalPointAtMoveStart.Y);
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
		// movement occur this frame. For input like touch pads and smooth scroll wheels we can receive input
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
				// TODO: On touch pads where scroll delta will be small, we may want to accumulate more scroll
				// before moving. Without doing that on a touch pad we end up scrolling very fast.
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
			StopPreview();
		else if (Playing)
			StopPlayback();

		GetFocusedChartData()?.ClearSelection();
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
		MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), false, Preferences.Instance.PreferencesWaveForm.EnableWaveForm);
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

			DrawMeasureMarkers();
			DrawRegions();
			DrawReceptors();
			DrawSnapIndicators();

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
			DrawSelectedRegion();

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
		PreDrawWaveFormToRenderTargets();
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

		// Draw the recolored waveform to the second render target.
		GraphicsDevice.SetRenderTarget(WaveformRenderTargets[1]);
		GraphicsDevice.Clear(Color.Transparent);
		SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, WaveformColorEffect);
		WaveformColorEffect.CurrentTechnique.Passes[0].Apply();
		SpriteBatch.Draw(WaveformRenderTargets[0],
			new Rectangle(0, 0, WaveformRenderTargets[0].Width, WaveformRenderTargets[0].Height), Color.White);
		SpriteBatch.End();
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
		TextureAtlas.Draw("dark-bg", SpriteBatch, new Rectangle(x, 0, w, GetViewportHeight()),
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
			ActiveChartData[i].DrawReceptors(sizeZoom, TextureAtlas, SpriteBatch);
	}

	private void DrawSnapIndicators()
	{
		var focusedChartData = GetFocusedChartData();
		if (focusedChartData == null)
			return;
		var arrowGraphicManager = focusedChartData.GetArrowGraphicManager();
		if (arrowGraphicManager == null)
			return;
		var snapTextureId = SnapManager.GetCurrentTexture();
		if (string.IsNullOrEmpty(snapTextureId))
			return;
		var (receptorTextureId, _) = arrowGraphicManager.GetReceptorTexture(0);
		var (receptorTextureWidth, _) = TextureAtlas.GetDimensions(receptorTextureId);
		var zoom = ZoomManager.GetSizeZoom();
		var receptorLeftEdge = GetFocalPointScreenSpaceX() - FocusedChart.NumInputs * 0.5 * receptorTextureWidth * zoom;

		var (snapTextureWidth, snapTextureHeight) = TextureAtlas.GetDimensions(snapTextureId);
		var leftX = receptorLeftEdge - snapTextureWidth * 0.5 * zoom;
		var y = GetFocalPointScreenSpaceY();

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
		var sizeZoom = ZoomManager.GetSizeZoom();
		for (var i = 0; i < ActiveCharts.Count; i++)
			ActiveChartData[i].DrawReceptorForegroundEffects(sizeZoom, TextureAtlas, SpriteBatch);
	}

	private void DrawWaveForm()
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		if (!p.ShowWaveForm || !p.EnableWaveForm)
			return;

		if (FocusedChart == null)
			return;

		// At this point WaveformRenderTargets[1] contains the recolored waveform.
		// We now draw that to the backbuffer with an optional antialiasing pass.

		// If the desired waveform width is greater than the texture width we may need
		// to scale it up by stretching the texture.
		var width = WaveformRenderTargets[1].Width;
		if (p.WaveFormScaleWidthToChart)
		{
			var focusedChartData = GetFocusedChartData();
			if (focusedChartData != null)
				width = Math.Max(width, focusedChartData.GetLaneAndWaveFormAreaWidthWithCurrentScale());
		}

		var x = GetFocalPointScreenSpaceX() - (width >> 1);

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
				new Rectangle(x, TransformChartSpaceYToScreenSpaceY(0), width, WaveformRenderTargets[1].Height), Color.White);
			SpriteBatch.End();
		}
		else
		{
			// Draw the recolored waveform to the back buffer.
			SpriteBatch.Begin();
			SpriteBatch.Draw(WaveformRenderTargets[1],
				new Rectangle(x, TransformChartSpaceYToScreenSpaceY(0), width, WaveformRenderTargets[1].Height), Color.White);
			SpriteBatch.End();
		}
	}

	private void DrawMeasureMarkers()
	{
		if (!Preferences.Instance.PreferencesOptions.RenderMarkers)
			return;

		foreach (var activeChartData in ActiveChartData)
		{
			foreach (var visibleMarker in activeChartData.GetVisibleMarkers())
			{
				visibleMarker.Draw(TextureAtlas, SpriteBatch, Font);
			}
		}
	}

	private void DrawRegions()
	{
		if (!Preferences.Instance.PreferencesOptions.RenderRegions)
			return;

		foreach (var activeChartData in ActiveChartData)
		{
			foreach (var visibleRegion in activeChartData.GetVisibleRegions())
			{
				visibleRegion.DrawRegion(TextureAtlas, SpriteBatch, GetViewportHeight());
			}
		}
	}

	private void DrawSelectedRegion()
	{
		var selectedRegion = GetFocusedChartData()?.GetSelectedRegion();
		if (selectedRegion?.IsActive() ?? false)
		{
			selectedRegion.DrawRegion(TextureAtlas, SpriteBatch, GetViewportHeight());
		}
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
			ActiveChartData[i].UpdateChartEvents(screenHeight);
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
			GetBackBufferHeight() - GetMiniMapYPaddingFromTopInScreenSpace() - GetMiniMapYPaddingFromBottom());
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

			if (numNotesAdded > MiniMapMaxNotesToDraw)
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
		MiniMap.Draw(SpriteBatch);
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

				y = ChartArea.Y + p.GetDensityGraphPositionOffsetUiScaled();
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

		UISongProperties.Instance.Draw(ActiveSong);
		UIChartProperties.Instance.Draw(FocusedChart);
		UIChartList.Instance.Draw(ActiveSong, FocusedChart);
		UIExpressedChartConfig.Instance.Draw();
		UIPerformedChartConfig.Instance.Draw();
		UIPatternConfig.Instance.Draw();
		UIAutogenConfigs.Instance.Draw();
		UIAutogenChart.Instance.Draw();
		UIAutogenChartsForChartType.Instance.Draw();
		UICopyEventsBetweenCharts.Instance.Draw();
		UIPatternEvent.Instance.Draw(GetFocusedChartData()?.GetLastSelectedPatternEvent());
		UIHotbar.Instance.Draw();

		if (CanShowRightClickPopupThisFrame && EditorMouseState.GetButtonState(EditorMouseState.Button.Right).UpThisFrame())
		{
			ImGui.OpenPopup("RightClickPopup");
		}

		var lastPos = EditorMouseState.GetButtonState(EditorMouseState.Button.Right).GetLastClickUpPosition();
		DrawRightClickMenu((int)lastPos.X, (int)lastPos.Y);

		UIModals.Draw();
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
					for (var i = 0; i < p.RecentFiles.Count; i++)
					{
						var recentFile = p.RecentFiles[i];
						var fileNameWithPath = recentFile.FileName;
						var fileName = System.IO.Path.GetFileName(fileNameWithPath);
						if (ImGui.MenuItem(fileName, canOpen))
						{
							OpenRecentIndex = i;
							OnOpenRecentFile();
						}
					}

					ImGui.EndMenu();
				}

				if (ImGui.MenuItem("Open Containing Folder", UIControls.GetCommandString(keyBinds.OpenContainingFolder), false,
					    hasSong))
					OnOpenContainingFolder();
				if (ImGui.MenuItem("Reload", UIControls.GetCommandString(keyBinds.Reload), false,
					    canOpen && canEditSong && p.RecentFiles.Count > 0))
					OnReload();
				if (ImGui.MenuItem("Close", "", false, canEditSong))
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

				if (ImGui.BeginMenu("Advanced Save Options"))
				{
					var titleColumnWidth = UiScaled(150);
					if (ImGuiLayoutUtils.BeginTable("AdvancedSaveOptionsTable", titleColumnWidth))
					{
						ImGuiLayoutUtils.DrawRowCheckbox(true, "Remove Chart Timing", Preferences.Instance,
							nameof(Preferences.OmitChartTimingData), false,
							"If checked then individual charts will have their timing data omitted from their files." +
							" The timing data from the song's Timing Chart will be used and saved at the song level." +
							" This has no effect on sm files which are already limited to only using timing data specified" +
							" at the song level. Under normal circumstances this option is not recommended but if you" +
							" use Stepmania files for other applications which struggle with chart timing data or you" +
							" are working under additional restrictions to file format this option may be useful.");

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
				UIEditEvents.DrawConvertSelectedMenu(selectedEvents);
				UIEditEvents.DrawShiftSelectedMenu(selectedEvents);
				ImGui.Separator();
				UIEditEvents.DrawConvertAllMenu();
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

			if (ImGui.BeginMenu("Chart"))
			{
				if (ImGui.BeginMenu("New Chart", canEditSong))
				{
					DrawNewChartSelectableList();
					ImGui.EndMenu();
				}

				if (ImGui.MenuItem("Clone Current Chart", canEditSong && hasChart))
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
				if (ImGui.MenuItem("Default Layout"))
					p.PreferencesOptions.ResetWindows = true;
				ImGui.Separator();
				if (ImGui.BeginMenu("Resolution"))
				{
					var sortedModes = new List<DisplayMode>(GraphicsAdapter.DefaultAdapter.SupportedDisplayModes);
					for (var i = sortedModes.Count - 1; i >= 0; i--)
					{
						if (ImGui.MenuItem($"{sortedModes[i].Width,5} x{sortedModes[i].Height,5}"))
						{
							SetResolution(sortedModes[i].Width, sortedModes[i].Height);
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

	public void DrawCopyChartEventsMenuItems(EditorChart chart)
	{
		var song = chart?.GetEditorSong();
		var canEditSong = CanEdit() && song != null;
		var canEditChart = CanEdit() && chart != null;

		if (ImGui.BeginMenu("Replace this Chart's Non-Step Events From", canEditSong && canEditChart))
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

		if (ImGui.BeginMenu("Replace this Chart's Timing and Scroll Events From", canEditSong && canEditChart))
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

		if (ImGui.BeginMenu("Replace this Chart's Timing Events From", canEditSong && canEditChart))
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

		if (ImGui.BeginMenu("Copy this Chart's Non-Step Events To", canEditSong && canEditChart))
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

		if (ImGui.BeginMenu("Copy this Chart's Timing and Scroll Events To", canEditSong && canEditChart))
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

		if (ImGui.BeginMenu("Copy this Chart's Timing Events To", canEditSong && canEditChart))
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

	public void ShowAutogenChartUI(EditorChart sourceChart = null)
	{
		UIAutogenChart.Instance.SetChart(sourceChart);
		UIAutogenChart.Instance.Open(true);
	}

	private void DrawRightClickMenu(int x, int y)
	{
		if (ImGui.BeginPopup("RightClickPopup"))
		{
			if (ActiveSong == null)
			{
				if (ImGui.MenuItem("New Song", UIControls.GetCommandString(Preferences.Instance.PreferencesKeyBinds.New)))
				{
					OnNew();
				}
			}

			var focusedChartData = GetFocusedChartData();

			var isInMiniMapArea = Preferences.Instance.PreferencesMiniMap.ShowMiniMap
			                      && MiniMap.IsScreenPositionInMiniMapBounds(x, y);
			var isInDensityGraphArea = Preferences.Instance.PreferencesDensityGraph.ShowDensityGraph
			                           && DensityGraph.IsInDensityGraphArea(x, y);

			var waveformWidth = GetWaveFormWidth();
			var focalPointX = GetFocalPointScreenSpaceX();
			var isInWaveFormArea = Preferences.Instance.PreferencesWaveForm.ShowWaveForm
			                       && Preferences.Instance.PreferencesWaveForm.EnableWaveForm
			                       && x >= focalPointX - (waveformWidth >> 1)
			                       && x <= focalPointX + (waveformWidth >> 1);
			var isInDarkArea = false;
			if (Preferences.Instance.PreferencesDark.ShowDarkBg)
			{
				var (darkX, darkW) = GetDarkBgXAndW();
				isInDarkArea = x >= darkX && x <= darkX + darkW;
			}

			var isInReceptorArea = Receptor.IsInReceptorArea(x, y, GetFocalPointScreenSpace(), ZoomManager.GetSizeZoom(),
				TextureAtlas,
				focusedChartData?.GetArrowGraphicManager(), FocusedChart);

			if (focusedChartData?.GetSelection().HasSelectedEvents() ?? false)
				UIEditEvents.DrawSelectionMenu();

			UIEditEvents.DrawSelectAllMenu();

			var anyObjectHovered = false;
			if (isInMiniMapArea)
			{
				if (ImGui.BeginMenu("Mini Map Preferences"))
				{
					UIMiniMapPreferences.Instance.DrawContents();
					ImGui.EndMenu();
				}

				anyObjectHovered = true;
			}
			else if (isInDensityGraphArea)
			{
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
			}
			else if (isInReceptorArea)
			{
				if (ImGui.BeginMenu("Receptor Preferences"))
				{
					UIReceptorPreferences.Instance.DrawContents();
					ImGui.EndMenu();
				}

				anyObjectHovered = true;
			}
			else
			{
				if (isInWaveFormArea)
				{
					if (ImGui.BeginMenu("Waveform Preferences"))
					{
						UIWaveFormPreferences.Instance.DrawContents();
						ImGui.EndMenu();
					}

					anyObjectHovered = true;
				}

				if (isInDarkArea)
				{
					if (ImGui.BeginMenu("Dark Preferences"))
					{
						UIDarkPreferences.Instance.DrawContents();
						ImGui.EndMenu();
					}

					anyObjectHovered = true;
				}
			}

			if (!anyObjectHovered)
			{
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
			}

			if (FocusedChart != null)
				UIEditEvents.DrawAddEventMenu();

			if (ActiveSong != null)
			{
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

	private void ShowUnsavedChangesModal()
	{
		var message = string.IsNullOrEmpty(ActiveSong.Title)
			? "Do you want to save your changes?\n\nYour changes will be lost if you don't save them."
			: $"Do you want to save the changes you made to {ActiveSong.Title}?\n\nYour changes will be lost if you don't save them.";

		UIModals.OpenModalThreeButtons(
			"Unsaved Changes",
			message,
			"Cancel", () => { PostSaveFunction = null; },
			"Don't Save", TryInvokePostSaveFunction,
			"Save", () =>
			{
				if (CanSaveWithoutLocationPrompt())
				{
					OnSave();
				}
				else
				{
					OnSaveAs();
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
			"External Modification",
			$"{fileName} was modified externally.",
			"Ignore", () => { ShowingSongFileChangedNotification = false; },
			"Reload", () =>
			{
				ShowingSongFileChangedNotification = false;
				OnReload(true);
			},
			() =>
			{
				if (ActionQueue.Instance.HasUnsavedChanges())
				{
					ImGui.TextColored(UILog.GetColor(LogLevel.Warn),
						"Warning: There are unsaved changes. Reloading will lose these changes.");
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
		var validStepGraphTypes = new List<ChartType>();
		foreach (var chartType in SupportedChartTypes)
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
		return AutogenConfigsLoaded;
	}

	private void CheckForAutoLoadingLastSong()
	{
		if (HasCheckedForAutoLoadingLastSong || !CanLoadSongs())
			return;

		// If we have a saved file to open, open it now.
		if (Preferences.Instance.PreferencesOptions.OpenLastOpenedFileOnLaunch
		    && Preferences.Instance.RecentFiles.Count > 0)
		{
			OpenRecentIndex = 0;
			OnOpenRecentFile();
		}

		HasCheckedForAutoLoadingLastSong = true;
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

		var saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "SSC File|*.ssc|SM File|*.sm";
		saveFileDialog.Title = "Save As...";
		saveFileDialog.FilterIndex = 0;
		if (ActiveSong.GetFileFormat() != null && ActiveSong.GetFileFormat().Type == FileFormatType.SM)
		{
			saveFileDialog.FilterIndex = 2;
		}

		var songDirectory = ActiveSong.GetFileDirectory();
		if (!string.IsNullOrEmpty(songDirectory))
			saveFileDialog.InitialDirectory = songDirectory;
		else
			saveFileDialog.InitialDirectory = Preferences.Instance.OpenFileDialogInitialDirectory;

		if (saveFileDialog.ShowDialog() != DialogResult.OK)
			return;

		var fullPath = saveFileDialog.FileName;
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
		})
		{
			OmitChartTimingData = Preferences.Instance.OmitChartTimingData,
			OmitCustomSaveData = Preferences.Instance.OmitCustomSaveData,
			AnonymizeSaveData = Preferences.Instance.AnonymizeSaveData,
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
				new DefaultChartListProvider(pOptions.DefaultStepsType, pOptions.DefaultDifficultyType));
		}
	}

	/// <summary>
	/// Starts the process of opening a selected Song.
	/// Will cancel previous OpenSongFileAsync requests if called while already loading.
	/// </summary>
	/// <param name="fileName">File name of the Song file to open.</param>
	/// <param name="chartListProvider">IActiveChartListProvider for determining which active charts to use.</param>
	private async void OpenSongFileAsync(
		string fileName,
		IActiveChartListProvider chartListProvider)
	{
		if (!CanLoadSongs())
			return;

		CloseSong();

		// Start the load. If we are already loading, return.
		// The previous call will use the newly provided file state.
		var taskComplete = await SongLoadTask.Start(new SongLoadState(fileName, chartListProvider));
		if (!taskComplete)
			return;
		var (newActiveSong, newFocusedChart, newActiveCharts, newFileName) = SongLoadTask.GetResults();

		Debug.Assert(IsOnMainThread());

		CloseSong();

		// Get the newly loaded song.
		ActiveSong = newActiveSong;
		if (ActiveSong == null)
		{
			return;
		}

		// Observe the song and its charts.
		ActiveSong.AddObservers(this, this);

		// Observe the song file for changes.
		StartObservingSongFile(newFileName);

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
		foreach (var activeChart in newActiveCharts)
			SetChartHasDedicatedTab(activeChart, true);
		SetChartFocused(newFocusedChart);
		SetFocalPointScreenSpace(focalPointX, focalPointY);

		// Set position and zoom.
		ResetPosition();
		SetChartPosition(desiredChartPosition);
		ZoomManager.SetZoom(desiredZoom);

		// Insert a new entry at the top of the saved recent files.
		UpdateRecentFilesForActiveSong();
	}

	private void StartObservingSongFile(string fileName)
	{
		try
		{
			var dir = System.IO.Path.GetDirectoryName(fileName);
			var file = System.IO.Path.GetFileName(fileName);
			if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
			{
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
		// This method is called from a separate thread and we don't want to access ActiveSong outside of
		// the main thread.
		ShouldCheckForShowingSongFileChangedNotification = true;
	}

	private void CheckForShowingSongFileChangedNotification()
	{
		if (!ShouldCheckForShowingSongFileChangedNotification)
			return;
		ShouldCheckForShowingSongFileChangedNotification = false;

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

	private string GetFullPathToMusicFile()
	{
		string musicFile = null;

		// If the active chart has a music file defined, use that.
		if (FocusedChart != null)
			musicFile = FocusedChart.MusicPath;

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

	private void OnOpenContainingFolder()
	{
		if (ActiveSong == null)
			return;
		var dir = ActiveSong.GetFileDirectory();
		if (string.IsNullOrEmpty(dir))
			return;
		try
		{
			var psi = new ProcessStartInfo()
			{
				FileName = "explorer.exe",
				WorkingDirectory = dir,
				ArgumentList = { dir },
			};
			Process.Start(psi);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to open {dir}. {e}");
		}
	}

	private void OnOpen()
	{
		if (!CanLoadSongs())
			return;

		if (ActionQueue.Instance.HasUnsavedChanges())
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
		if (ActionQueue.Instance.HasUnsavedChanges())
		{
			PostSaveFunction = OnOpenFileNoSave;
			ShowUnsavedChangesModal();
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

		if (!ignoreUnsavedChanges && ActionQueue.Instance.HasUnsavedChanges())
		{
			PostSaveFunction = OpenRecentFile;
			ShowUnsavedChangesModal();
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

		if (ActionQueue.Instance.HasUnsavedChanges())
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
		if (ActionQueue.Instance.HasUnsavedChanges())
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

		// Close any UI which holds on to Song/Chart state.
		UIAutogenChart.Instance.Close();
		UIAutogenChartsForChartType.Instance.Close();
		UICopyEventsBetweenCharts.Instance.Close();

		StopPlayback();
		MusicManager.UnloadAsync();
		SongLoadTask.ClearResults();
		ActiveSong = null;
		FocusedChart = null;
		foreach (var activeChartDat in ActiveChartData)
		{
			activeChartDat.Clear();
		}

		ActiveChartData.Clear();
		ActiveCharts.Clear();
		EditorMouseState.SetActiveChart(null);
		DensityGraph.SetStepDensity(null);
		DensityGraph.ResetBufferCapacities();
		UpdateWindowTitle();
		ActionQueue.Instance.Clear();
		StopObservingSongFile();
	}

	private void UpdateWindowTitle()
	{
		Debug.Assert(IsOnMainThread());

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

	public IReadOnlySelection GetSelection()
	{
		return GetFocusedChartData()?.GetSelection();
	}

	public void OnNoteTransformationBegin()
	{
		TransformingSelectedNotes = true;
	}

	public void OnNoteTransformationEnd(List<EditorEvent> transformedEvents)
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
		if (transformedEvents != null && transformedEvents.Count > 0)
		{
			var activeChartData = GetActiveChartData(transformedEvents[0].GetEditorChart());
			activeChartData?.SetSelectedEvents(transformedEvents);
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

	public void OnSelectAll()
	{
		GetFocusedChartData()?.OnSelectAll();
	}

	public void OnSelectAllAlt()
	{
		GetFocusedChartData()?.OnSelectAllAlt();
	}

	public void OnSelectAllShift()
	{
		GetFocusedChartData()?.OnSelectAllShift();
	}

	public void OnSelectAll(Func<EditorEvent, bool> isSelectable)
	{
		GetFocusedChartData()?.OnSelectAll(isSelectable);
	}

	public void OnShiftSelectedNotesLeft()
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftSelectedNotesLeft();
	}

	public void OnShiftNotesLeft(IEnumerable<EditorEvent> events)
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

	public void OnShiftNotesLeftAndWrap(IEnumerable<EditorEvent> events)
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

	public void OnShiftNotesRight(IEnumerable<EditorEvent> events)
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

	public void OnShiftNotesRightAndWrap(IEnumerable<EditorEvent> events)
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

	public void OnShiftNotesEarlier(IEnumerable<EditorEvent> events)
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

	public void OnShiftNotesLater(IEnumerable<EditorEvent> events)
	{
		if (EditEarlyOut())
			return;
		GetFocusedChartData()?.OnShiftNotesLater(events, GetShiftNotesRows());
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

		CopiedEvents.Clear();
		foreach (var selectedEvent in selection.GetSelectedEvents())
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
		if (ActiveSong == null || FocusedChart == null)
			return;

		var index = 0;
		var sortedCharts = ActiveSong.GetSortedCharts();
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
	}

	private void OpenNextChart()
	{
		if (ActiveSong == null || FocusedChart == null)
			return;

		var index = 0;
		var sortedCharts = ActiveSong.GetSortedCharts();
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
				SetChartFocused(ActiveCharts[index + 1 % ActiveCharts.Count]);
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

	public void OnActiveChartPositionChanged(ActiveEditorChart activeChart)
	{
		if (activeChart != GetFocusedChartData())
			return;

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

	public SnapManager GetSnapManager()
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

	private void OnMoveToPreviousMeasure()
	{
		var chartPosition = GetPosition().ChartPosition;
		var rate = FocusedChart?.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(chartPosition);
		if (rate == null)
			return;
		var sig = rate.GetTimeSignature();
		var rows = sig.GetNumerator() * (MaxValidDenominator * NumBeatsPerMeasure / sig.GetDenominator());
		SetChartPosition(chartPosition - rows);

		UpdateAutoPlayFromScrolling();
	}

	private void OnMoveToNextMeasure()
	{
		var chartPosition = GetPosition().ChartPosition;
		var rate = FocusedChart?.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(chartPosition);
		if (rate == null)
			return;
		var sig = rate.GetTimeSignature();
		var rows = sig.GetNumerator() * (MaxValidDenominator * NumBeatsPerMeasure / sig.GetDenominator());
		SetChartPosition(chartPosition + rows);

		UpdateAutoPlayFromScrolling();
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

		var desiredRow = label.GetRow();
		SetChartPosition(desiredRow);
		UpdateAutoPlayFromScrolling();
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

		var desiredRow = label.GetRow();
		SetChartPosition(desiredRow);
		UpdateAutoPlayFromScrolling();
	}

	public void OnMoveToPreviousPattern()
	{
		OnMoveToPreviousPattern(GetPosition().ChartPosition);
	}

	public void OnMoveToPreviousPattern(EditorPatternEvent currentPattern)
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
		var desiredRow = patternEvent!.ChartRow;
		SetChartPosition(desiredRow);
		UpdateAutoPlayFromScrolling();

		// Select the pattern.
		OnSelectPattern(patternEvent);
	}

	public void OnMoveToNextPattern()
	{
		OnMoveToNextPattern(GetPosition().ChartPosition);
	}

	public void OnMoveToNextPattern(EditorPatternEvent currentPattern)
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
		var desiredRow = patternEvent!.ChartRow;
		SetChartPosition(desiredRow);
		UpdateAutoPlayFromScrolling();

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
		if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
			StopPlayback();

		SetChartPosition(0.0);

		UpdateAutoPlayFromScrolling();
	}

	private void OnMoveToChartEnd()
	{
		if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
			StopPlayback();

		if (FocusedChart != null)
			SetChartPosition(FocusedChart.GetEndPosition());
		else
			SetChartPosition(0.0);

		UpdateAutoPlayFromScrolling();
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

	public ActiveEditorChart GetFocusedChartData()
	{
		if (FocusedChart == null)
			return null;
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (ActiveCharts[i] == FocusedChart)
				return ActiveChartData[i];
		}

		return null;
	}

	public ActiveEditorChart GetActiveChartData(EditorChart chart)
	{
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (ActiveCharts[i] == chart)
				return ActiveChartData[i];
		}

		return null;
	}

	public bool IsChartActive(EditorChart chart)
	{
		return GetActiveChartData(chart) != null;
	}

	public bool DoesChartHaveDedicatedTab(EditorChart chart)
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

	public void SetChartHasDedicatedTab(EditorChart chart, bool hasDedicatedTab)
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

	public void CloseChart(EditorChart chart)
	{
		if (chart == null)
			return;

		var removingFocusedChart = FocusedChart == chart;

		// Get the original index so we can focus an adjacent chart.
		var originalActiveIndex = 0;
		var focalPointX = GetFocalPointScreenSpaceX();
		var focalPointY = GetFocalPointScreenSpaceY();
		if (removingFocusedChart)
		{
			for (var i = 0; i < ActiveCharts.Count; i++)
			{
				if (ActiveCharts[i] == chart)
				{
					originalActiveIndex = i;
					break;
				}
			}
		}

		// Close the chart.
		RemoveActiveChart(chart);

		// Ensure this chart is no longer focused.
		if (removingFocusedChart)
		{
			// Focus on another chart if other charts are active.
			if (ActiveCharts.Count > 0)
			{
				if (originalActiveIndex < ActiveCharts.Count)
					SetChartFocused(ActiveCharts[originalActiveIndex]);
				else if (originalActiveIndex - 1 >= 0 && originalActiveIndex - 1 < ActiveCharts.Count)
					SetChartFocused(ActiveCharts[originalActiveIndex - 1]);
				else
					SetChartFocused(ActiveCharts[0]);
			}
			else
			{
				FocusedChart = null;
			}
		}

		// Prevent the focal point from moving.
		SetFocalPointScreenSpace(focalPointX, focalPointY);

		UpdateChartPositions();
	}

	public void MoveActiveChartLeft(EditorChart chart)
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

	public void MoveActiveChartRight(EditorChart chart)
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

	public void ShowChart(EditorChart chart, bool withDedicatedTab)
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
		activeChartData.Position.ChartTime = GetPosition().ChartTime;
		activeChartData.Position.SetDesiredPositionToCurrent();

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

	public void SetChartFocused(EditorChart chart, bool undoable = false)
	{
		Debug.Assert(IsOnMainThread());

		if (chart == null)
			return;

		var oldChartTime = GetPosition().ChartTime;
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

		var focusedChartData = GetFocusedChartData();
		focusedChartData.Position.ChartTime = oldChartTime;
		focusedChartData.Position.SetDesiredPositionToCurrent();
		focusedChartData.SetFocused(true);
		CleanUpActiveChartsWithoutDedicatedTabs(focusedChartData);

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

		// Window title depends on the active chart.
		UpdateWindowTitle();

		// Start loading music for this Chart.
		OnMusicChanged();
		OnMusicPreviewChanged();
	}

	public EditorChart AddChart(ChartType chartType, bool selectNewChart)
	{
		Debug.Assert(IsOnMainThread());

		if (ActiveSong == null)
			return null;
		var chart = ActiveSong.AddChart(chartType);
		chart?.AddObserver(this);
		if (selectNewChart)
			SetChartFocused(chart);
		return chart;
	}

	public EditorChart AddChart(EditorChart chart, bool selectNewChart)
	{
		Debug.Assert(IsOnMainThread());

		if (ActiveSong == null)
			return null;
		ActiveSong.AddChart(chart);
		chart.AddObserver(this);
		if (selectNewChart)
			SetChartFocused(chart);
		return chart;
	}

	public void DeleteChart(EditorChart chart, EditorChart chartToSelect)
	{
		Debug.Assert(IsOnMainThread());

		if (ActiveSong == null)
			return;

		CloseChart(chart);
		chart.RemoveObserver(this);
		ActiveSong.DeleteChart(chart);

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
		if (files?.Length != 1)
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

	public UIPatternComparer GetPatternComparer()
	{
		return PatternComparer;
	}

	public UIPerformedChartComparer GetPerformedChartComparer()
	{
		return PerformedChartComparer;
	}

	#endregion Patterns

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
			case EditorChart.NotificationPatternRequestEdit:
				OnSelectPattern((EditorPatternEvent)payload);
				break;
		}
	}

	public void OnNotify(string eventId, PreferencesOptions options, object payload)
	{
		switch (eventId)
		{
			case PreferencesOptions.NotificationUndoHistorySizeChanged:
				OnUndoHistorySizeChanged();
				break;
		}
	}

	public void OnNotify(string eventId, PreferencesAudio audio, object payload)
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
