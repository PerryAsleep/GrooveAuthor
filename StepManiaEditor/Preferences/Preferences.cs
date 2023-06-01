using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using Fumen.Converters;
using StepManiaLibrary;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor
{
	/// <summary>
	/// Miscellaneous preferences to save to disk.
	/// </summary>
	internal sealed class Preferences
	{
		/// <summary>
		/// File to use for deserializing Preferences.
		/// </summary>
		private const string FileName = "Preferences.json";

		public const string DefaultBalancedPerformedChartConfigName = "Default Balanced";
		public const string DefaultStaminaPerformedChartConfigName = "Default Stamina";

		/// <summary>
		/// Serialization options.
		/// </summary>
		private static JsonSerializerOptions SerializationOptions = new JsonSerializerOptions()
		{
			Converters =
			{
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
			},
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			IncludeFields = true,
			WriteIndented = true
		};

		public class SavedSongInformation
		{
			public void UpdateChart(SMCommon.ChartType chartType, SMCommon.ChartDifficultyType difficultyType)
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
			[JsonInclude] public SMCommon.ChartType LastChartType;
			[JsonInclude] public SMCommon.ChartDifficultyType LastChartDifficultyType;
			[JsonInclude] public double SpacingZoom = 1.0;
			[JsonInclude] public double ChartPosition = 0.0;
		}

		/// <summary>
		/// Static Config instance.
		/// </summary>
		public static Preferences Instance { get; private set; } = new Preferences();
		private Editor Editor;

		// Window preferences
		[JsonInclude] public int WindowWidth = 1920;
		[JsonInclude] public int WindowHeight = 1080;
		[JsonInclude] public bool WindowFullScreen = false;
		[JsonInclude] public bool WindowMaximized = false;

		// Waveform preferences
		[JsonInclude] public PreferencesWaveForm PreferencesWaveForm = new PreferencesWaveForm();

		// Scroll control preferences
		[JsonInclude] public PreferencesScroll PreferencesScroll = new PreferencesScroll();

		// Selection preferences
		[JsonInclude] public PreferencesSelection PreferencesSelection = new PreferencesSelection();

		// MiniMap preferences
		[JsonInclude] public PreferencesMiniMap PreferencesMiniMap = new PreferencesMiniMap();

		// Option preferences
		[JsonInclude] public PreferencesOptions PreferencesOptions = new PreferencesOptions();

		// Animations preferences
		[JsonInclude] public PreferencesReceptors PreferencesReceptors = new PreferencesReceptors();

		// ExpressedChart preferences
		[JsonInclude] public PreferencesExpressedChartConfig PreferencesExpressedChartConfig = new PreferencesExpressedChartConfig();

		// PerformedChart preferences
		// TODO: Move these into their own class
		[JsonInclude] public Dictionary<string, StepManiaLibrary.PerformedChart.Config> PerformedChartConfigs = new Dictionary<string, StepManiaLibrary.PerformedChart.Config>();

		// Log preferences
		[JsonInclude] public bool ShowLogWindow = true;
		[JsonInclude] public int LogWindowDateDisplay = 1;
		[JsonInclude] public LogLevel LogWindowLevel = LogLevel.Info;
		[JsonInclude] public bool LogWindowLineWrap;

		// Misc
		[JsonInclude] public bool ShowSongPropertiesWindow = false;
		[JsonInclude] public bool ShowChartPropertiesWindow = false;
		[JsonInclude] public bool ShowChartListWindow = false;
		[JsonInclude] public string OpenFileDialogInitialDirectory = @"C:\Games\StepMania 5\Songs\";
		[JsonInclude] public List<SavedSongInformation> RecentFiles = new List<SavedSongInformation>();

		// Debug
		[JsonInclude] public double DebugSongTime = 0.0;
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

			AddDefaultPerformedChartConfigs();
		}

		private void AddDefaultPerformedChartConfigs()
		{
			// Default Balanced
			var balancedConfig = new StepManiaLibrary.PerformedChart.Config();

			var arrowWeights = new Dictionary<string, List<int>>();
			arrowWeights[ChartTypeString(ChartType.dance_single)] = new List<int> { 25, 25, 25, 25 };
			arrowWeights[ChartTypeString(ChartType.dance_double)] = new List<int> { 6, 12, 10, 22, 22, 12, 10, 6 };
			arrowWeights[ChartTypeString(ChartType.dance_solo)] = new List<int> { 13, 12, 25, 25, 12, 13 };
			arrowWeights[ChartTypeString(ChartType.dance_threepanel)] = new List<int> { 25, 50, 25 };
			arrowWeights[ChartTypeString(ChartType.pump_single)] = new List<int> { 17, 16, 34, 16, 17 };
			arrowWeights[ChartTypeString(ChartType.pump_halfdouble)] = new List<int> { 13, 12, 25, 25, 12, 13 }; // WRONG
			arrowWeights[ChartTypeString(ChartType.pump_double)] = new List<int> { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 }; // WRONG
			arrowWeights[ChartTypeString(ChartType.smx_beginner)] = new List<int> { 25, 50, 25 };
			arrowWeights[ChartTypeString(ChartType.smx_single)] = new List<int> { 25, 21, 8, 21, 25 };
			arrowWeights[ChartTypeString(ChartType.smx_dual)] = new List<int> { 8, 17, 25, 25, 17, 8 };
			arrowWeights[ChartTypeString(ChartType.smx_full)] = new List<int> { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 };
			balancedConfig.ArrowWeights = arrowWeights;

			var stepTighteningConfig = new StepManiaLibrary.PerformedChart.Config.StepTighteningConfig();
			stepTighteningConfig.TravelSpeedMinTimeSeconds = 0.176471;  // 16ths at 170
			stepTighteningConfig.TravelSpeedMaxTimeSeconds = 0.24;      // 16ths at 125
			stepTighteningConfig.TravelDistanceMin = 2.0;
			stepTighteningConfig.TravelDistanceMax = 3.0;
			stepTighteningConfig.StretchDistanceMin = 3.0;
			stepTighteningConfig.StretchDistanceMax = 4.0;
			balancedConfig.StepTightening = stepTighteningConfig;

			var lateralTighteningConfig = new StepManiaLibrary.PerformedChart.Config.LateralTighteningConfig();
			lateralTighteningConfig.RelativeNPS = 1.65;
			lateralTighteningConfig.AbsoluteNPS = 12.0;
			lateralTighteningConfig.Speed = 3.0;
			balancedConfig.LateralTightening = lateralTighteningConfig;

			var facingConfig = new StepManiaLibrary.PerformedChart.Config.FacingConfig();
			facingConfig.MaxInwardPercentage = 1.0;
			facingConfig.MaxOutwardPercentage = 1.0;
			balancedConfig.Facing = facingConfig;

			balancedConfig.Init();
			PerformedChartConfigs[DefaultBalancedPerformedChartConfigName] = balancedConfig;

			// Default Stamina
			var staminaConfig = new StepManiaLibrary.PerformedChart.Config();
			staminaConfig.StepTightening.TravelSpeedMaxTimeSeconds = 0.303; // 16ths at 99
			staminaConfig.SetAsOverrideOf(balancedConfig);
			PerformedChartConfigs[DefaultStaminaPerformedChartConfigName] = staminaConfig;
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
			Logger.Info($"Loading {FileName}...");

			try
			{
				using (FileStream openStream = File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)))
				{
					Instance = await JsonSerializer.DeserializeAsync<Preferences>(openStream, SerializationOptions);
					Instance.Editor = editor;
					Instance.PostLoad();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {FileName}. {e}");
				return Instance;
			}

			Logger.Info($"Loaded {FileName}.");
			return Instance;
		}

		/// <summary>
		/// Loads the Preferences from the preferences json file.
		/// </summary>
		/// <returns>Preferences Instance.</returns>
		public static Preferences Load(Editor editor)
		{
			Logger.Info($"Loading {FileName}...");

			try
			{
				using (FileStream openStream = File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)))
				{
					Instance = JsonSerializer.Deserialize<Preferences>(openStream, SerializationOptions);
					Instance.Editor = editor;
					Instance.PostLoad();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {FileName}. {e}");
				return Instance;
			}

			Logger.Info($"Loaded {FileName}.");
			return Instance;
		}

		/// <summary>
		/// Save the Preferences to the preferences json file.
		/// </summary>
		public static async Task SaveAsync()
		{
			Logger.Info($"Saving {FileName}...");

			try
			{
				Instance.PreSave();
				var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
				await File.WriteAllTextAsync(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName), jsonString);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to save {FileName}. {e}");
				return;
			}

			Logger.Info($"Saved {FileName}.");
		}

		/// <summary>
		/// Save the Preferences to the preferences json file.
		/// </summary>
		public static void Save()
		{
			Logger.Info($"Saving {FileName}...");

			try
			{
				Instance.PreSave();
				var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
				File.WriteAllText(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName), jsonString);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to save {FileName}. {e}");
				return;
			}

			Logger.Info($"Saved {FileName}.");
		}
	}
}
