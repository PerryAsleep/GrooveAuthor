using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fumen;
using StepManiaLibrary;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// Class for managing all EditorConfig objects of a single type.
/// Offers synchronous methods for saving and loading EditorConfig objects.
/// Offers methods for adding and deleting EditorConfig objects.
/// EditorConfig are persisted as individual files on disk to support sharing between users.
/// </summary>
/// <typeparam name="TEditorConfig">
/// Type of EditorConfig objects managed by this class.
/// </typeparam>
/// <typeparam name="TConfig">
/// Type of configuration objects implementing the IConfig interface that are
/// wrapped by the EditorConfig objects that this class manages.
/// </typeparam>
internal abstract class ConfigManager<TEditorConfig, TConfig>
	where TEditorConfig : EditorConfig<TConfig>
	where TConfig : IConfig<TConfig>, new()
{
	private const string ConfigExtension = "json";

	/// <summary>
	/// Prefix used on save files for the EditorConfig objects managed by this class.
	/// </summary>
	private readonly string ConfigPrefix;

	/// <summary>
	/// Human readable string for identifying the type of EditorConfig objects managed by this class.
	/// </summary>
	private readonly string ConfigTypeReadableName;

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
	protected readonly ConfigData<TEditorConfig, TConfig> ConfigData = new();

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="configPrefix">
	/// Prefix used on save files for the EditorConfig objects managed by this class.
	/// </param>
	/// <param name="configTypeReadableName">
	/// Human readable string for identifying the type of EditorConfig objects managed by this class.
	/// </param>
	protected ConfigManager(string configPrefix, string configTypeReadableName)
	{
		ConfigTypeReadableName = configTypeReadableName;
		ConfigPrefix = configPrefix;
		ConfigDirectory = Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AutogenConfigs");
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
	/// Synchronously save all EditorConfig files that have unsaved changes.
	/// </summary>
	public void SaveConfigs()
	{
		// Save all configs.
		foreach (var kvp in ConfigData.GetConfigs())
		{
			// Don't save configs which don't have unsaved changes.
			if (!kvp.Value.HasUnsavedChanges())
				continue;

			var saveFileName = Fumen.Path.Combine(ConfigDirectory, $"{ConfigPrefix}{kvp.Key}.{ConfigExtension}");
			SaveConfig(saveFileName, kvp.Value);
		}
	}

	/// <summary>
	/// Synchronously saves an individual EditorConfig to disk.
	/// </summary>
	/// <param name="fileName">Filename to save to.</param>
	/// <param name="config">EditorConfig object to save.</param>
	private void SaveConfig(string fileName, TEditorConfig config)
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
	/// Synchronously loads all EditorConfig files from disk.
	/// </summary>
	public void LoadConfigs()
	{
		Logger.Info($"Loading {ConfigTypeReadableName} files...");

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
			Logger.Error($"Failed to search for {ConfigTypeReadableName} files in {ConfigDirectory}. {e}");
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

			if (fi.Extension == $".{ConfigExtension}" && fi.Name.StartsWith(ConfigPrefix))
			{
				var config = LoadConfig(fi.FullName);
				if (config != null)
					ConfigData.AddConfig(config);
			}
		}

		if (ConfigData.GetConfigs().Count == 0)
		{
			Logger.Info($"No {ConfigTypeReadableName} configs found.");
		}

		// Perform any post-load setup.
		PostLoadConfigs();

		// Update the last saved state on all EditorConfigs.
		var numConfigs = 0;
		var numDefaultConfigs = 0;
		foreach (var config in ConfigData.GetConfigs())
		{
			numConfigs++;
			if (config.Value.IsDefault())
				numDefaultConfigs++;
			config.Value.UpdateLastSavedState();
		}

		Logger.Info($"Loaded {numConfigs} {ConfigTypeReadableName} configs ({numDefaultConfigs} default).");
	}

	/// <summary>
	/// Synchronously loads an individual EditorConfig from disk.
	/// </summary>
	/// <param name="fileName">Filename to load from.</param>
	/// <returns>Loaded EditorConfig object or null if it failed to load.</returns>
	private TEditorConfig LoadConfig(string fileName)
	{
		try
		{
			using var openStream = File.OpenRead(fileName);
			var config = JsonSerializer.Deserialize<TEditorConfig>(openStream, SerializationOptions);
			return config;
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {fileName}: {e}");
		}

		return default;
	}

	/// <summary>
	/// Performs post-load initialization of all EditorConfigs.
	/// Creates and adds default EditorConfigs.
	/// Removes any invalid EditorConfigs.
	/// </summary>
	private void PostLoadConfigs()
	{
		// Add default configurations.
		AddDefaultConfigs();

		// Ensure every EditorConfig is configured and valid.
		var invalidConfigGuids = new List<Guid>();
		foreach (var kvp in ConfigData.GetConfigs())
		{
			// Configure the EditorConfig will name update functions.
			kvp.Value.SetNameUpdatedFunction(OnConfigNameUpdated);

			// Perform post-load initialization on the config object.
			kvp.Value.Init();

			// Validate the EditorConfig. If this fails, store it for removal.
			if (!kvp.Value.Validate())
			{
				invalidConfigGuids.Add(kvp.Key);
			}
		}

		// Remove all invalid EditorConfig objects.
		foreach (var invalidConfigGuid in invalidConfigGuids)
		{
			ConfigData.RemoveConfig(invalidConfigGuid);
		}

		// Now that names are loaded, refresh the sorted lists of guids of names.
		ConfigData.UpdateSortedConfigs();

		OnPostLoadComplete();
	}

	#endregion Load

	public void AddConfig(TEditorConfig config)
	{
		ConfigData.AddConfig(config);
	}

	public TEditorConfig AddConfig(Guid guid, string name)
	{
		return AddConfig(guid, name, true);
	}

	public TEditorConfig AddConfig(Guid guid)
	{
		return AddConfig(guid, EditorConfig<TConfig>.NewConfigName, true);
	}

	public TEditorConfig AddConfig(string name)
	{
		return AddConfig(Guid.NewGuid(), name, true);
	}

	protected TEditorConfig AddConfig(Guid guid, string name, bool useDefaultValues)
	{
		var config = NewEditorConfig(guid);
		config.SetNameUpdatedFunction(OnConfigNameUpdated);
		config.Name = name;
		if (useDefaultValues)
			config.InitializeWithDefaultValues();
		config.Config.Init();
		AddConfig(config);
		return config;
	}

	public void DeleteConfig(Guid guid)
	{
		ConfigData.RemoveConfig(guid);
		OnConfigDeleted(guid);
	}

	public TEditorConfig CloneConfig(Guid guid)
	{
		return ConfigData.CloneConfig(guid);
	}

	public TEditorConfig GetConfig(Guid guid)
	{
		return ConfigData.GetConfig(guid);
	}

	public Guid[] GetSortedConfigGuids()
	{
		return ConfigData.SortedConfigGuids;
	}

	public string[] GetSortedConfigNames()
	{
		return ConfigData.SortedConfigNames;
	}

	/// <summary>
	/// Called when an EditorConfig's name is updated to a new value.
	/// </summary>
	private void OnConfigNameUpdated()
	{
		// Update the sort since the name has changed.
		ConfigData.UpdateSortedConfigs();
	}

	/// <summary>
	/// Creates a new EditorConfig object with the given Guid.
	/// </summary>
	/// <param name="guid">Guid for new EditorConfig object.</param>
	/// <returns>New EditorConfig object.</returns>
	protected abstract TEditorConfig NewEditorConfig(Guid guid);

	/// <summary>
	/// Called when an EditorConfig with the given Guid is deleted so derived
	/// classes have an opportunity to respond.
	/// </summary>
	/// <param name="guid">Guid of deleted EditorConfig.</param>
	protected abstract void OnConfigDeleted(Guid guid);

	/// <summary>
	/// Adds all default EditorConfig objects.
	/// </summary>
	protected abstract void AddDefaultConfigs();

	/// <summary>
	/// Called after all EditorConfig objects have been loaded, initialized, and validated.
	/// Gives derived classes an opportunity to respond.
	/// </summary>
	protected abstract void OnPostLoadComplete();
}
