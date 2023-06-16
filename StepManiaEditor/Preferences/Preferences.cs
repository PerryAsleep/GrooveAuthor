using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Miscellaneous preferences to save to disk.
/// </summary>
internal sealed class Preferences
{
	/// <summary>
	/// File to use for deserializing Preferences.
	/// </summary>
	private const string PreferencesFileName = "Preferences.json";

	public const string DefaultBalancedPerformedChartConfigName = "Default Balanced";
	public const string DefaultStaminaPerformedChartConfigName = "Default Stamina";

	/// <summary>
	/// Serialization options.
	/// </summary>
	private static JsonSerializerOptions SerializationOptions = new()
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
		},
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
		IncludeFields = true,
		WriteIndented = true,
	};

	public class SavedSongInformation
	{
		public void UpdateChart(ChartType chartType, ChartDifficultyType difficultyType)
		{
			LastChartType = chartType;
			LastChartDifficultyType = difficultyType;
		}

		public void UpdatePosition(double spacingZoom, double chartPosition)
		{
			SpacingZoom = spacingZoom;
			ChartPosition = chartPosition;
		}

		[JsonInclude] public string FileName;
		[JsonInclude] public ChartType LastChartType;
		[JsonInclude] public ChartDifficultyType LastChartDifficultyType;
		[JsonInclude] public double SpacingZoom = 1.0;
		[JsonInclude] public double ChartPosition;
	}

	/// <summary>
	/// Static Config instance.
	/// </summary>
	public static Preferences Instance { get; private set; } = new();

	private Editor Editor;

	// Window preferences
	[JsonInclude] public int WindowWidth = 1920;
	[JsonInclude] public int WindowHeight = 1080;
	[JsonInclude] public bool WindowFullScreen;
	[JsonInclude] public bool WindowMaximized;

	// Waveform preferences
	[JsonInclude] public PreferencesWaveForm PreferencesWaveForm = new();

	// Scroll control preferences
	[JsonInclude] public PreferencesScroll PreferencesScroll = new();

	// Selection preferences
	[JsonInclude] public PreferencesSelection PreferencesSelection = new();

	// MiniMap preferences
	[JsonInclude] public PreferencesMiniMap PreferencesMiniMap = new();

	// Option preferences
	[JsonInclude] public PreferencesOptions PreferencesOptions = new();

	// Animations preferences
	[JsonInclude] public PreferencesReceptors PreferencesReceptors = new();

	// ExpressedChart preferences
	[JsonInclude] public PreferencesExpressedChartConfig PreferencesExpressedChartConfig = new();

	// PerformedChart preferences
	[JsonInclude] public PreferencesPerformedChartConfig PreferencesPerformedChartConfig = new();

	// Log preferences
	[JsonInclude] public bool ShowLogWindow = true;
	[JsonInclude] public int LogWindowDateDisplay = 1;
	[JsonInclude] public LogLevel LogWindowLevel = LogLevel.Info;
	[JsonInclude] public bool LogWindowLineWrap;

	// Misc
	[JsonInclude] public bool ShowSongPropertiesWindow;
	[JsonInclude] public bool ShowChartPropertiesWindow;
	[JsonInclude] public bool ShowAutogenConfigsWindow;
	[JsonInclude] public bool ShowChartListWindow;
	[JsonInclude] public string OpenFileDialogInitialDirectory = @"C:\Games\StepMania 5\Songs\";
	[JsonInclude] public List<SavedSongInformation> RecentFiles = new();
	[JsonInclude] public Editor.NoteEntryMode NoteEntryMode = Editor.NoteEntryMode.Normal;
	[JsonInclude] public int SnapIndex;

	// Debug
	[JsonInclude] public double DebugSongTime;
	[JsonInclude] public double DebugZoom = 1.0;

	/// <summary>
	/// Public Constructor.
	/// This should be private but it needs to be public for JSON deserialization.
	/// </summary>
	public Preferences()
	{
		//PostLoad();
	}

	private void PostLoad()
	{
		PreferencesReceptors.SetEditor(Editor);
		PreferencesOptions.PostLoad();
		PreferencesExpressedChartConfig.PostLoad();
		PreferencesPerformedChartConfig.PostLoad();
	}

	private void PreSave()
	{
		PreferencesOptions.PreSave();
	}

	/// <summary>
	/// Loads the Preferences from the preferences json file.
	/// </summary>
	/// <returns>Preferences Instance.</returns>
	public static async Task<Preferences> LoadAsync(Editor editor)
	{
		Logger.Info($"Loading {PreferencesFileName}...");

		try
		{
			await using var openStream =
				File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreferencesFileName));
			Instance = await JsonSerializer.DeserializeAsync<Preferences>(openStream, SerializationOptions);
			Instance.Editor = editor;
			Instance.PostLoad();
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {PreferencesFileName}. {e}");
			return Instance;
		}

		Logger.Info($"Loaded {PreferencesFileName}.");
		return Instance;
	}

	/// <summary>
	/// Loads the Preferences from the preferences json file.
	/// </summary>
	/// <returns>Preferences Instance.</returns>
	public static Preferences Load(Editor editor)
	{
		Logger.Info($"Loading {PreferencesFileName}...");

		try
		{
			using var openStream = File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreferencesFileName));
			Instance = JsonSerializer.Deserialize<Preferences>(openStream, SerializationOptions);
			Instance.Editor = editor;
			Instance.PostLoad();
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {PreferencesFileName}. {e}");
			return Instance;
		}

		Logger.Info($"Loaded {PreferencesFileName}.");
		return Instance;
	}

	/// <summary>
	/// Save the Preferences to the preferences json file.
	/// </summary>
	public static async Task SaveAsync()
	{
		Logger.Info($"Saving {PreferencesFileName}...");

		try
		{
			Instance.PreSave();
			var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
			await File.WriteAllTextAsync(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreferencesFileName),
				jsonString);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to save {PreferencesFileName}. {e}");
			return;
		}

		Logger.Info($"Saved {PreferencesFileName}.");
	}

	/// <summary>
	/// Save the Preferences to the preferences json file.
	/// </summary>
	public static void Save()
	{
		Logger.Info($"Saving {PreferencesFileName}...");

		try
		{
			Instance.PreSave();
			var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
			File.WriteAllText(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreferencesFileName), jsonString);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to save {PreferencesFileName}. {e}");
			return;
		}

		Logger.Info($"Saved {PreferencesFileName}.");
	}
}
