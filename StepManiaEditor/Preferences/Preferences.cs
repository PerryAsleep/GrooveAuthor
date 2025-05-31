using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fumen;
using StepManiaEditor.AutogenConfig;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Miscellaneous preferences to save to disk.
/// </summary>
internal sealed class Preferences
{
	public const string DefaultBalancedPerformedChartConfigName = "Default Balanced";
	public const string DefaultStaminaPerformedChartConfigName = "Default Stamina";

	/// <summary>
	/// Serialization options.
	/// </summary>
	private static readonly JsonSerializerOptions SerializationOptions;

	static Preferences()
	{
		// Collect default values from Enums which may have changed between versions.
		// We want to allow deserialization of now invalid values and fallback to good defaults.
		var factory = new PermissiveEnumJsonConverterFactory();
		PreferencesDark.RegisterDefaultsForInvalidEnumValues(factory);
		PreferencesDensityGraph.RegisterDefaultsForInvalidEnumValues(factory);
		PreferencesMiniMap.RegisterDefaultsForInvalidEnumValues(factory);
		PreferencesOptions.RegisterDefaultsForInvalidEnumValues(factory);
		PreferencesPerformance.RegisterDefaultsForInvalidEnumValues(factory);
		PreferencesSelection.RegisterDefaultsForInvalidEnumValues(factory);
		PreferencesStream.RegisterDefaultsForInvalidEnumValues(factory);
		PreferencesWaveForm.RegisterDefaultsForInvalidEnumValues(factory);

		SerializationOptions = new JsonSerializerOptions
		{
			Converters = { factory },
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			IncludeFields = true,
			WriteIndented = true,
		};
	}

	/// <summary>
	/// Static Config instance.
	/// </summary>
	public static Preferences Instance { get; private set; } = new();

	private Editor Editor;

	// Window preferences
	[JsonInclude] [JsonPropertyName("WindowWidth")]
	public int ViewportWidth = 1920;

	[JsonInclude] [JsonPropertyName("WindowHeight")]
	public int ViewportHeight = 1080;

	[JsonInclude] public bool WindowMaximized = true;

	// FTUE state
	[JsonInclude] public Version LastCompletedFtueVersion;
	[JsonInclude] public int FtueIndex;

	// Waveform preferences
	[JsonInclude] public PreferencesWaveForm PreferencesWaveForm = new();

	// Dark background preferences
	[JsonInclude] public PreferencesDark PreferencesDark = new();

	// Scroll control preferences
	[JsonInclude] public PreferencesScroll PreferencesScroll = new();

	// Selection preferences
	[JsonInclude] public PreferencesSelection PreferencesSelection = new();

	// MiniMap preferences
	[JsonInclude] public PreferencesMiniMap PreferencesMiniMap = new();

	// Option preferences
	[JsonInclude] public PreferencesOptions PreferencesOptions = new();

	// Audio preferences
	[JsonInclude] public PreferencesAudio PreferencesAudio = new();

	// Animations preferences
	[JsonInclude] public PreferencesReceptors PreferencesReceptors = new();

	// PerformedChart preferences
	[JsonInclude] public Guid ActivePerformedChartConfigForWindow;
	[JsonInclude] public bool ShowPerformedChartListWindow;

	// ExpressedChart preferences
	[JsonInclude] public Guid ActiveExpressedChartConfigForWindow;
	[JsonInclude] public bool ShowExpressedChartListWindow;

	// PatternConfig preferences
	[JsonInclude] public Guid ActivePatternConfigForWindow;
	[JsonInclude] public bool ShowPatternListWindow;
	[JsonInclude] public bool ShowPatternEventWindow;

	// Attack window preferences
	[JsonInclude] public bool ShowAttackEventWindow;

	// Performance monitoring preferences
	[JsonInclude] public PreferencesPerformance PreferencesPerformance = new();

	// Stream breakdown preferences
	[JsonInclude] public PreferencesStream PreferencesStream = new();
	[JsonInclude] public PreferencesDensityGraph PreferencesDensityGraph = new();

	// Key Binds
	[JsonInclude] public PreferencesKeyBinds PreferencesKeyBinds = new();

	[JsonInclude] [JsonPropertyName("PreferencesMultiplayer")]
	public PreferencesNoteColor PreferencesNoteColor = new();

	// Log preferences
	[JsonInclude] public bool ShowLogWindow = true;
	[JsonInclude] public int LogWindowDateDisplay = 1;
	[JsonInclude] public LogLevel LogWindowLevel = LogLevel.Info;
	[JsonInclude] public bool LogWindowLineWrap = true;

	// Save Options
	[JsonInclude] public bool RequireIdenticalTimingInSmFiles = true;
	[JsonInclude] public bool OmitChartTimingData;
	[JsonInclude] public bool OmitCustomSaveData;
	[JsonInclude] public bool AnonymizeSaveData;
	[JsonInclude] public bool UseStepF2ForPumpRoutine;

	// Misc
	[JsonInclude] public bool ShowSongPropertiesWindow = true;
	[JsonInclude] public bool ShowChartPropertiesWindow = true;
	[JsonInclude] public bool ShowPackPropertiesWindow;
	[JsonInclude] public bool ShowAutogenConfigsWindow;
	[JsonInclude] public bool ShowChartListWindow = true;
	[JsonInclude] public bool ShowAboutWindow;
	[JsonInclude] public bool ShowDebugWindow;
	[JsonInclude] public bool ShowControlsWindow;
	[JsonInclude] public bool ShowHotbar = true;
	[JsonInclude] public string OpenFileDialogInitialDirectory;
	[JsonInclude] public List<SavedSongInformation> RecentFiles = [];
	[JsonInclude] public Editor.NoteEntryMode NoteEntryMode = Editor.NoteEntryMode.Normal;
	[JsonInclude] public int SnapIndex;
	[JsonInclude] public int SnapLockIndex;
	[JsonInclude] public int Player;
	[JsonInclude] public ChartType LastSelectedAutogenChartType = ChartType.dance_single;

	[JsonInclude] public Guid LastSelectedAutogenPerformedChartConfig =
		PerformedChartConfigManager.DefaultPerformedChartConfigGuid;

	// Debug
	[JsonInclude] public double DebugSongTime;
	[JsonInclude] public double DebugZoom = 1.0;

	/// <summary>
	/// Public Constructor.
	/// This should be private, but it needs to be public for JSON deserialization.
	/// </summary>
	public Preferences()
	{
		//PostLoad();
	}

	private void PostLoad()
	{
		if (string.IsNullOrEmpty(OpenFileDialogInitialDirectory))
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				// On Windows, use the default install location for StepMania.
				OpenFileDialogInitialDirectory = @"C:\Games\StepMania 5\Songs\";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// On Linux there is no default install location. It is just an archive that
				// the user can put where they like. ITGMania has a default install location
				// though, so prefer that.
				OpenFileDialogInitialDirectory = "/opt/itgmania/Songs/";
			}
		}

		foreach (var savedSongData in RecentFiles)
			savedSongData.PostLoad();
		PreferencesNoteColor.PostLoad();
		PreferencesReceptors.SetEditor(Editor);
		PreferencesWaveForm.PostLoad();
		PreferencesMiniMap.PostLoad();
		PreferencesDensityGraph.PostLoad();
		PreferencesKeyBinds.PostLoad();
	}

	private void PreSave()
	{
	}

	/// <summary>
	/// Loads the Preferences from the given file.
	/// </summary>
	/// <param name="editor">Editor instance</param>
	/// <param name="fileName">Name of file to load from.</param>
	/// <returns>Preferences Instance.</returns>
	public static Preferences Load(Editor editor, string fileName)
	{
		Logger.Info($"Loading {fileName}...");

		try
		{
			using var openStream = File.OpenRead(fileName);
			Instance = JsonSerializer.Deserialize<Preferences>(openStream, SerializationOptions);
			Instance.Editor = editor;
			Instance.PostLoad();
			Logger.Info($"Loaded {fileName}.");
			return Instance;
		}
		catch (FileNotFoundException)
		{
			Logger.Info($"No preferences found at {fileName}. Using defaults.");
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {fileName}. {e}");
		}

		Instance = new Preferences();
		try
		{
			Instance.Editor = editor;
			Instance.PostLoad();
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to initialize Preferences. {e}");
		}

		return Instance;
	}

	/// <summary>
	/// Save the Preferences to the given file.
	/// </summary>
	/// <param name="fileName">Name of file to save to.</param>
	public static void Save(string fileName)
	{
		Logger.Info($"Saving {fileName}...");

		try
		{
			Instance.PreSave();
			var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
			File.WriteAllText(fileName, jsonString);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to save {fileName}. {e}");
			return;
		}

		Logger.Info($"Saved {fileName}.");
	}
}
