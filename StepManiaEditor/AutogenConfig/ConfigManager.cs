using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fumen;
using StepManiaLibrary.ExpressedChart;
using Exception = System.Exception;
using Path = Fumen.Path;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// Class for managing configurations related to automatically generating charts or patterns.
/// These configurations are persisted as individual files on disk to support sharing between users.
/// This class offers synchronous methods to load configurations and save configurations with changes.
/// This class should be accessed through its static Instance member.
/// </summary>
internal sealed class ConfigManager
{
	private const string ConfigExtension = "json";
	private const string PerformedChartConfigPrefix = "pc-";
	private const string ExpressedChartConfigPrefix = "ec-";

	// Default config names and guids for EditorPerformedChartConfigs which cannot be edited.
	public const string DefaultPerformedChartConfigName = "Default";
	public static readonly Guid DefaultPerformedChartConfigGuid = new("6276c906-ea8f-43b3-9500-0ddeac7bdc22");
	public const string DefaultPerformedChartStaminaConfigName = "Default Stamina";
	public static readonly Guid DefaultPerformedChartStaminaGuid = new("c0334922-6105-4703-add2-3de261b2ff19");

	// Default config names and guids for EditorExpressedChartConfigs which cannot be edited.
	public const string DefaultExpressedChartDynamicConfigName = "Dynamic";
	public static readonly Guid DefaultExpressedChartDynamicConfigGuid = new("a19d532e-b0ce-4759-ad1c-02ecbbdf2efd");
	public const string DefaultExpressedChartAggressiveBracketsConfigName = "Aggressive Brackets";
	public static readonly Guid DefaultExpressedChartAggressiveBracketsConfigGuid = new("da3f6e12-49d1-416b-8db6-0ab413f740b6");
	public const string DefaultExpressedChartNoBracketsConfigName = "No Brackets";
	public static readonly Guid DefaultExpressedChartNoBracketsConfigGuid = new("0c0ba200-8f90-4060-8912-e9ea65831ebc");

	/// <summary>
	/// Static Config instance.
	/// </summary>
	public static ConfigManager Instance { get; private set; } = new();

	/// <summary>
	/// Directory for saving and loading configuration files.
	/// </summary>
	private readonly string ConfigDirectory;

	/// <summary>
	/// JsonSerializerOptions to use for reading and writing configuration files.
	/// </summary>
	private readonly JsonSerializerOptions SerializationOptions;

	/// <summary>
	/// ConfigData for all EditorPerformedChartConfig objects.
	/// </summary>
	private readonly ConfigData<EditorPerformedChartConfig> PerformedChartConfigData = new();

	/// <summary>
	/// ConfigData for all EditorExpressedChartConfig objects.
	/// </summary>
	private readonly ConfigData<EditorExpressedChartConfig> ExpressedChartConfigData = new();

	/// <summary>
	/// Private constructor.
	/// </summary>
	private ConfigManager()
	{
		ConfigDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutogenConfigs");
		SerializationOptions = new JsonSerializerOptions
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
	}

	#region Save

	/// <summary>
	/// Synchronously save all config files that have unsaved changes.
	/// </summary>
	public void SaveConfigs()
	{
		// Aggregate all configs.
		var configsAndSaveFiles = new List<Tuple<string, IEditorConfig>>();
		foreach (var kvp in PerformedChartConfigData.GetConfigs())
			configsAndSaveFiles.Add(new Tuple<string, IEditorConfig>($"{PerformedChartConfigPrefix}{kvp.Key}.{ConfigExtension}",
				kvp.Value));
		foreach (var kvp in ExpressedChartConfigData.GetConfigs())
			configsAndSaveFiles.Add(new Tuple<string, IEditorConfig>($"{ExpressedChartConfigPrefix}{kvp.Key}.{ConfigExtension}",
				kvp.Value));

		// Save all configs.
		foreach (var configAndSaveFile in configsAndSaveFiles)
		{
			// Don't save configs which don't have unsaved changes.
			if (!configAndSaveFile.Item2.HasUnsavedChanges())
				continue;

			var saveFileName = Path.Combine(ConfigDirectory, configAndSaveFile.Item1);
			SaveConfig(saveFileName, configAndSaveFile.Item2);
		}
	}

	/// <summary>
	/// Synchronously saves an individual IEditorConfig to disk.
	/// </summary>
	/// <typeparam name="T">Type of IEditorConfig to save.</typeparam>
	/// <param name="fileName">Filename to save to.</param>
	/// <param name="config">IEditorConfig object to save.</param>
	private void SaveConfig<T>(string fileName, T config) where T : IEditorConfig
	{
		Logger.Info($"Saving {fileName}...");

		try
		{
			using var openStream = File.Open(fileName, FileMode.Create);
			JsonSerializer.Serialize(openStream, config, SerializationOptions);
			config.UpdateLastSavedState();
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to save {fileName}: {e}");
			return;
		}


		Logger.Info($"Saved {fileName}.");
	}

	#endregion Save

	#region Load

	/// <summary>
	/// Synchronously loads all configuration files from disk.
	/// </summary>
	public void LoadConfigs()
	{
		Logger.Info("Loading autogen configs...");

		// Create the config directory if it doesn't exist.
		try
		{
			Directory.CreateDirectory(ConfigDirectory);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to check for and create {ConfigDirectory}. {e}");
			// Continue. The directory may exist.
		}

		// Find all files in the directory.
		string[] files;
		try
		{
			files = Directory.GetFiles(ConfigDirectory);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to search for autogen config files in {ConfigDirectory}. {e}");
			return;
		}

		// Loop over every file in the config directory and load each config.
		foreach (var file in files)
		{
			FileInfo fi;
			try
			{
				fi = new FileInfo(file);
			}
			catch (Exception e)
			{
				Logger.Warn($"Could not get file info for \"{file}\". {e}");
				continue;
			}

			if (fi.Extension == $".{ConfigExtension}")
			{
				if (fi.Name.StartsWith(PerformedChartConfigPrefix))
				{
					var config = (EditorPerformedChartConfig)LoadConfig<EditorPerformedChartConfig>(fi.FullName);
					if (config != null)
						PerformedChartConfigData.AddConfig(config);
				}
				else if (fi.Name.StartsWith(ExpressedChartConfigPrefix))
				{
					var config = (EditorExpressedChartConfig)LoadConfig<EditorExpressedChartConfig>(fi.FullName);
					if (config != null)
						ExpressedChartConfigData.AddConfig(config);
				}
			}
		}

		if (PerformedChartConfigData.GetConfigs().Count == 0 && ExpressedChartConfigData.GetConfigs().Count == 0)
		{
			Logger.Info("No autogen configs found.");
		}

		// Perform any post-load setup.
		PostLoadPerformedChartConfigs();
		PostLoadExpressedChartConfigs();

		// Update the last saved state on all configs.
		var numPerformedChartConfigs = 0;
		var numDefaultPerformedChartConfigs = 0;
		foreach (var config in PerformedChartConfigData.GetConfigs())
		{
			numPerformedChartConfigs++;
			if (config.Value.IsDefault())
				numDefaultPerformedChartConfigs++;
			config.Value.UpdateLastSavedState();
		}

		var numExpressedChartConfigs = 0;
		var numDefaultExpressedChartConfigs = 0;
		foreach (var config in ExpressedChartConfigData.GetConfigs())
		{
			numExpressedChartConfigs++;
			if (config.Value.IsDefault())
				numDefaultExpressedChartConfigs++;
			config.Value.UpdateLastSavedState();
		}

		Logger.Info($"Loaded {numExpressedChartConfigs} Expressed configs ({numDefaultExpressedChartConfigs} default).");
		Logger.Info($"Loaded {numPerformedChartConfigs} PerformedChart configs ({numDefaultPerformedChartConfigs} default).");
	}

	/// <summary>
	/// Synchronously loads an individual IEditorConfig from disk.
	/// </summary>
	/// <typeparam name="T">Type of IEditorConfig to load.</typeparam>
	/// <param name="fileName">Filename to load from.</param>
	/// <returns>Loaded IEditorConfig object or null if it failed to load.</returns>
	private IEditorConfig LoadConfig<T>(string fileName) where T : IEditorConfig
	{
		try
		{
			using var openStream = File.OpenRead(fileName);
			var config = JsonSerializer.Deserialize<T>(openStream, SerializationOptions);
			return config;
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {fileName}: {e}");
		}

		return default;
	}

	/// <summary>
	/// Performs post-load initialization of all PerformedChart configurations.
	/// Creates and adds default configurations.
	/// Removes any invalid configurations.
	/// </summary>
	private void PostLoadPerformedChartConfigs()
	{
		// Add default balanced config. This should never be modified so delete it if it exists and re-add it.
		PerformedChartConfigData.RemoveConfig(DefaultPerformedChartConfigGuid);
		var defaultConfig = AddPerformedChartConfig(DefaultPerformedChartConfigGuid, DefaultPerformedChartConfigName);
		defaultConfig.Description = "Default balanced settings";
		defaultConfig.InitializeWithDefaultValues();

		// Add default stamina config. This should never be modified so delete it if it exists and re-add it.
		PerformedChartConfigData.RemoveConfig(DefaultPerformedChartStaminaGuid);
		var defaultStaminaConfig =
			AddPerformedChartConfig(DefaultPerformedChartStaminaGuid, DefaultPerformedChartStaminaConfigName);
		defaultStaminaConfig.Description = "Default stamina settings.";
		defaultStaminaConfig.TravelSpeedMinBPM = 99;
		defaultStaminaConfig.Config.Transitions.Enabled = true;
		defaultStaminaConfig.Config.Transitions.StepsPerTransitionMin = 32;
		defaultStaminaConfig.Config.LateralTightening.AbsoluteNPS = 26.666667;

		// Ensure every EditorPerformedChartConfig is configured and valid.
		var invalidConfigGuids = new List<Guid>();
		foreach (var kvp in PerformedChartConfigData.GetConfigs())
		{
			// Configure the EditorPerformedChartConfig will name update functions.
			kvp.Value.SetNameUpdatedFunction(OnPerformedChartConfigNameUpdated);

			// Perform post-load initialization on the config object.
			kvp.Value.Config.Init();

			// Validate the Config. If this fails, store it for removal.
			if (!kvp.Value.Validate())
			{
				invalidConfigGuids.Add(kvp.Key);
			}
		}

		// Remove all invalid EditorPerformedChartConfig objects.
		foreach (var invalidConfigGuid in invalidConfigGuids)
		{
			PerformedChartConfigData.RemoveConfig(invalidConfigGuid);
		}

		// Ensure the variables for displaying an EditorPerformedChartConfig don't point to an unknown config.
		if (Preferences.Instance.ActivePerformedChartConfigForWindow != Guid.Empty)
		{
			if (PerformedChartConfigData.GetConfig(Preferences.Instance.ActivePerformedChartConfigForWindow) == null)
				Preferences.Instance.ActivePerformedChartConfigForWindow = Guid.Empty;
		}

		if (Preferences.Instance.ActivePerformedChartConfigForWindow == Guid.Empty)
			Preferences.Instance.ShowPerformedChartListWindow = false;

		// Now that names are loaded, refresh the sorted lists of guids of names.
		PerformedChartConfigData.UpdateSortedConfigs();
	}

	/// <summary>
	/// Performs post-load initialization of all ExpressedChart configurations.
	/// Creates and adds default configurations.
	/// Removes any invalid configurations.
	/// </summary>
	private void PostLoadExpressedChartConfigs()
	{
		// Add the default dynamic config. This should never be modified so delete it if it exists and re-add it.
		ExpressedChartConfigData.RemoveConfig(DefaultExpressedChartDynamicConfigGuid);
		var defaultDynamicConfig =
			AddExpressedChartConfig(DefaultExpressedChartDynamicConfigGuid, DefaultExpressedChartDynamicConfigName);
		defaultDynamicConfig.Description = "Default settings with dynamic bracket parsing";
		defaultDynamicConfig.InitializeWithDefaultValues();

		// Add the default aggressive bracket config. This should never be modified so delete it if it exists and re-add it.
		ExpressedChartConfigData.RemoveConfig(DefaultExpressedChartAggressiveBracketsConfigGuid);
		var defaultAggressiveConfig = AddExpressedChartConfig(DefaultExpressedChartAggressiveBracketsConfigGuid,
			DefaultExpressedChartAggressiveBracketsConfigName, false);
		defaultAggressiveConfig.Description = "Default settings with aggressive bracket parsing";
		defaultAggressiveConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultAggressiveConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.Aggressive;

		// Add the default no-brackets config. This should never be modified so delete it if it exists and re-add it.
		ExpressedChartConfigData.RemoveConfig(DefaultExpressedChartNoBracketsConfigGuid);
		var defaultNoBracketsConfig = AddExpressedChartConfig(DefaultExpressedChartNoBracketsConfigGuid,
			DefaultExpressedChartNoBracketsConfigName, false);
		defaultNoBracketsConfig.Description = "Default settings that avoid brackets";
		defaultNoBracketsConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultNoBracketsConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.NoBrackets;

		// Ensure every EditorExpressedChartConfig is configured and valid.
		var invalidConfigGuids = new List<Guid>();
		foreach (var kvp in ExpressedChartConfigData.GetConfigs())
		{
			// Configure the EditorExpressedChartConfig will name update functions.
			kvp.Value.SetNameUpdatedFunction(OnExpressedChartConfigNameUpdated);

			// Validate the ExpressedChartConfig. If this fails, store it for removal.
			if (!kvp.Value.Validate())
			{
				invalidConfigGuids.Add(kvp.Key);
			}
		}

		// Remove all invalid ExpressedChartConfig objects.
		foreach (var invalidConfigGuid in invalidConfigGuids)
		{
			ExpressedChartConfigData.RemoveConfig(invalidConfigGuid);
		}

		// Ensure the variables for displaying an EditorExpressedChartConfig don't point to an unknown config.
		if (Preferences.Instance.ActiveExpressedChartConfigForWindow != Guid.Empty)
		{
			if (ExpressedChartConfigData.GetConfig(Preferences.Instance.ActiveExpressedChartConfigForWindow) == null)
				Preferences.Instance.ActiveExpressedChartConfigForWindow = Guid.Empty;
		}

		if (Preferences.Instance.ActiveExpressedChartConfigForWindow == Guid.Empty)
			Preferences.Instance.ShowExpressedChartListWindow = false;

		// Now that names are loaded, refresh the sorted lists of guids of names.
		ExpressedChartConfigData.UpdateSortedConfigs();
	}

	#endregion Load

	#region PerformedChart

	public void AddPerformedChartConfig(EditorPerformedChartConfig config)
	{
		PerformedChartConfigData.AddConfig(config);
	}

	public EditorPerformedChartConfig AddPerformedChartConfig(Guid guid, string name)
	{
		return AddPerformedChartConfig(guid, name, true);
	}

	public EditorPerformedChartConfig AddPerformedChartConfig(Guid guid)
	{
		return AddPerformedChartConfig(guid, EditorPerformedChartConfig.NewConfigName, true);
	}

	public EditorPerformedChartConfig AddPerformedChartConfig(string name)
	{
		return AddPerformedChartConfig(Guid.NewGuid(), name, true);
	}

	private EditorPerformedChartConfig AddPerformedChartConfig(Guid guid, string name, bool useDefaultValues)
	{
		var config = new EditorPerformedChartConfig(guid);
		config.SetNameUpdatedFunction(OnPerformedChartConfigNameUpdated);
		config.Name = name;
		if (useDefaultValues)
			config.InitializeWithDefaultValues();
		config.Config.Init();
		AddPerformedChartConfig(config);
		return config;
	}

	public void DeletePerformedChartConfig(Guid guid)
	{
		PerformedChartConfigData.RemoveConfig(guid);

		// If the actively displayed config is being deleted, remove the variable tracking 
		if (Preferences.Instance.ActivePerformedChartConfigForWindow == guid)
		{
			Preferences.Instance.ActivePerformedChartConfigForWindow = Guid.Empty;
			Preferences.Instance.ShowPerformedChartListWindow = false;
		}
	}

	public EditorPerformedChartConfig ClonePerformedChartConfig(Guid guid)
	{
		return PerformedChartConfigData.CloneConfig(guid);
	}

	public EditorPerformedChartConfig GetPerformedChartConfig(Guid guid)
	{
		return PerformedChartConfigData.GetConfig(guid);
	}

	public Guid[] GetSortedPerformedChartConfigGuids()
	{
		return PerformedChartConfigData.SortedConfigGuids;
	}

	public string[] GetSortedPerformedChartConfigNames()
	{
		return PerformedChartConfigData.SortedConfigNames;
	}

	/// <summary>
	/// Called when a EditorPerformedChartConfig's name is updated to a new value.
	/// </summary>
	private void OnPerformedChartConfigNameUpdated()
	{
		// Update the sort since the name has changed.
		PerformedChartConfigData.UpdateSortedConfigs();
	}

	#endregion PerformedChart

	#region ExpressedChart

	public void AddExpressedChartConfig(EditorExpressedChartConfig config)
	{
		ExpressedChartConfigData.AddConfig(config);
	}

	public EditorExpressedChartConfig AddExpressedChartConfig(Guid guid, string name)
	{
		return AddExpressedChartConfig(guid, name, true);
	}

	public EditorExpressedChartConfig AddExpressedChartConfig(Guid guid)
	{
		return AddExpressedChartConfig(guid, EditorExpressedChartConfig.NewConfigName, true);
	}

	public EditorExpressedChartConfig AddExpressedChartConfig(string name)
	{
		return AddExpressedChartConfig(Guid.NewGuid(), name, true);
	}

	private EditorExpressedChartConfig AddExpressedChartConfig(Guid guid, string name, bool useDefaultValues)
	{
		var config = new EditorExpressedChartConfig(guid);
		config.SetNameUpdatedFunction(OnExpressedChartConfigNameUpdated);
		config.Name = name;
		if (useDefaultValues)
			config.InitializeWithDefaultValues();
		AddExpressedChartConfig(config);
		return config;
	}

	public void DeleteExpressedChartConfig(Guid guid)
	{
		ExpressedChartConfigData.RemoveConfig(guid);

		// If the actively displayed config is being deleted, remove the variable tracking 
		if (Preferences.Instance.ActiveExpressedChartConfigForWindow == guid)
		{
			Preferences.Instance.ActiveExpressedChartConfigForWindow = Guid.Empty;
			Preferences.Instance.ShowExpressedChartListWindow = false;
		}
	}

	public EditorExpressedChartConfig CloneExpressedChartConfig(Guid guid)
	{
		return ExpressedChartConfigData.CloneConfig(guid);
	}

	public EditorExpressedChartConfig GetExpressedChartConfig(Guid guid)
	{
		return ExpressedChartConfigData.GetConfig(guid);
	}

	public Guid[] GetSortedExpressedChartConfigGuids()
	{
		return ExpressedChartConfigData.SortedConfigGuids;
	}

	public string[] GetSortedExpressedChartConfigNames()
	{
		return ExpressedChartConfigData.SortedConfigNames;
	}

	/// <summary>
	/// Called when a EditorExpressedChartConfig's name is updated to a new value.
	/// </summary>
	private void OnExpressedChartConfigNameUpdated()
	{
		// Update the sort since the name has changed.
		ExpressedChartConfigData.UpdateSortedConfigs();
	}

	#endregion ExpressedChart
}
