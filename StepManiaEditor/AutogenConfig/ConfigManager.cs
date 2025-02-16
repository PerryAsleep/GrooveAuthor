using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using StepManiaLibrary;
using Path = Fumen.Path;

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
/// Type of StepManiaLibrary Config objects  wrapped by the EditorConfig objects that
/// this class manages.
/// </typeparam>
internal abstract class ConfigManager<TEditorConfig, TConfig> : Notifier<ConfigManager<TEditorConfig, TConfig>>,
	Fumen.IObserver<EditorConfig<TConfig>>
	where TEditorConfig : EditorConfig<TConfig>
	where TConfig : Config, new()
{
	private const string ConfigExtension = "json";

	public const string NotificationConfigAdded = "ConfigAdded";
	public const string NotificationConfigDeleted = "ConfigDeleted";

	/// <summary>
	/// Prefix used on save files for the EditorConfig objects managed by this class.
	/// </summary>
	private readonly string ConfigPrefix;

	/// <summary>
	/// Human-readable string for identifying the type of EditorConfig objects managed by this class.
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
	/// ConfigData for all EditorConfig objects.
	/// </summary>
	protected readonly ConfigData<TEditorConfig, TConfig> ConfigData = new();

	/// <summary>
	/// All explicitly deleted configs.
	/// </summary>
	private readonly HashSet<Guid> DeletedConfigs = new();

	/// <summary>
	/// Whether the configs have finished loading.
	/// </summary>
	private bool FinishedLoading;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="configPrefix">
	/// Prefix used on save files for the EditorConfig objects managed by this class.
	/// </param>
	/// <param name="configTypeReadableName">
	/// Human-readable string for identifying the type of EditorConfig objects managed by this class.
	/// </param>
	protected ConfigManager(string configPrefix, string configTypeReadableName)
	{
		ConfigTypeReadableName = configTypeReadableName;
		ConfigPrefix = configPrefix;
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
	/// Synchronously synchronize all config changes to disk.
	/// This will save changed configs and delete removed configs.
	/// </summary>
	public void SynchronizeToDisk()
	{
		SaveConfigs();
		DeleteRemovedConfigs();
	}

	/// <summary>
	/// Synchronously save all EditorConfig files that have unsaved changes.
	/// </summary>
	private void SaveConfigs()
	{
		// Save all configs.
		foreach (var kvp in ConfigData.GetConfigs())
		{
			// Don't save configs which don't have unsaved changes.
			if (!kvp.Value.HasUnsavedChanges())
				continue;

			var saveFileName = Path.Combine(ConfigDirectory, $"{ConfigPrefix}{kvp.Key}.{ConfigExtension}");
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

	/// <summary>
	/// Synchronously delete any config files from disk which were deleted this session.
	/// </summary>
	private void DeleteRemovedConfigs()
	{
		foreach (var guid in DeletedConfigs)
		{
			var fileName = $"{ConfigPrefix}{guid}.{ConfigExtension}";
			try
			{
				var filePath = Path.Combine(ConfigDirectory, fileName);
				Logger.Info($"Deleting {fileName}...");
				File.Delete(filePath);
				Logger.Info($"Deleted {fileName}.");
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to delete {fileName}. {e}");
			}
		}
	}

	#endregion Save

	#region Load

	/// <summary>
	/// Asynchronously loads all EditorConfig files from disk.
	/// </summary>
	public void LoadConfigsAsync()
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
			FinishedLoading = true;
			return;
		}

		// Loop over every file in the config directory and load each config.
		var tasks = new List<Task<TEditorConfig>>();
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
				tasks.Add(LoadConfigAsync(fi.FullName));
			}
		}

		if (tasks.Count == 0)
		{
			FinishLoadingConfigs();
			return;
		}

		_ = MainThreadDispatcher.RunContinuationOnMainThread(Task.WhenAll(tasks), () =>
		{
			foreach (var task in tasks)
			{
				var config = task.Result;
				if (config != null)
					ConfigData.AddConfig(config);
			}

			FinishLoadingConfigs();
		});
	}

	private void FinishLoadingConfigs()
	{
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

		FinishedLoading = true;

		Logger.Info($"Loaded {numConfigs} {ConfigTypeReadableName} configs ({numDefaultConfigs} default).");
	}

	/// <summary>
	/// Asynchronously loads an individual EditorConfig from disk.
	/// </summary>
	/// <param name="fileName">Filename to load from.</param>
	/// <returns>Loaded EditorConfig object or null if it failed to load.</returns>
	private async Task<TEditorConfig> LoadConfigAsync(string fileName)
	{
		try
		{
			await using var openStream = File.OpenRead(fileName);
			var config = await JsonSerializer.DeserializeAsync<TEditorConfig>(openStream, SerializationOptions);
			return config;
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {fileName}: {e}");
		}

		return null;
	}

	/// <summary>
	/// Performs post-load initialization of all EditorConfigs.
	/// Creates and adds default EditorConfigs.
	/// Removes any invalid EditorConfigs.
	/// </summary>
	private void PostLoadConfigs()
	{
		// First, add this as an observer to all configs.
		// As part of validating and adding default configs below we will add
		// and delete EditorConfigs, and the act of adding or deleting configs
		// will add and remove this as an Observer. We want the observation
		// state to be correct throughout this process.
		foreach (var kvp in ConfigData.GetConfigs())
		{
			// Observe the EditorConfig so we can resort on name changes.
			kvp.Value.AddObserver(this);
		}

		// Add default configurations.
		AddDefaultConfigs();

		// Ensure every EditorConfig is configured and valid.
		var invalidConfigGuids = new List<Guid>();
		foreach (var kvp in ConfigData.GetConfigs())
		{
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
			DeleteConfig(invalidConfigGuid);
		}

		// Do not treat any invalid configs as intentionally deleted. We want to err on
		// keeping these files around when we close.
		DeletedConfigs.Clear();

		// Now that names are loaded, refresh the sorted lists of guids of names.
		ConfigData.UpdateSortedConfigs();

		OnPostLoadComplete();
	}

	public bool HasFinishedLoading()
	{
		return FinishedLoading;
	}

	#endregion Load

	protected TEditorConfig AddDefaultConfig(Guid guid, string name)
	{
		return AddConfig(guid, name, true, true);
	}

	public void AddConfig(TEditorConfig config)
	{
		config.AddObserver(this);
		ConfigData.AddConfig(config);
		Notify(NotificationConfigAdded, this, config.Guid);

		// Remove any record of this config being deleted.
		DeletedConfigs.Remove(config.Guid);
	}

	public TEditorConfig AddConfig(Guid guid, string name)
	{
		return AddConfig(guid, name, true, false);
	}

	public TEditorConfig AddConfig(Guid guid)
	{
		return AddConfig(guid, null, true, false);
	}

	public TEditorConfig AddConfig(string name)
	{
		return AddConfig(Guid.NewGuid(), name, true, false);
	}

	protected TEditorConfig AddConfig(Guid guid, string name, bool useDefaultValues, bool isDefaultConfig)
	{
		var config = NewEditorConfig(guid, isDefaultConfig);
		config.Name = name ?? config.GetNewConfigName();
		if (useDefaultValues)
			config.InitializeWithDefaultValues();
		config.Init();
		AddConfig(config);
		return config;
	}

	public void DeleteConfig(Guid guid)
	{
		var config = GetConfig(guid);
		config?.RemoveObserver(this);
		ConfigData.RemoveConfig(guid);
		OnConfigDeleted(guid);
		Notify(NotificationConfigAdded, this, guid);

		// Record this config as being intentionally deleted so we can consider it
		// for deletion from disk later.
		DeletedConfigs.Add(guid);
	}

	public TEditorConfig CloneConfig(Guid guid)
	{
		return ConfigData.CloneConfig(guid);
	}

	public TEditorConfig GetConfig(Guid guid)
	{
		return ConfigData.GetConfig(guid);
	}

	public IEnumerable<TEditorConfig> GetSortedConfigs()
	{
		return ConfigData.GetSortedConfigs();
	}

	public string[] GetSortedConfigNames()
	{
		return ConfigData.GetSortedConfigNames();
	}

	public Guid[] GetSortedConfigGuids()
	{
		return ConfigData.GetSortedConfigGuids();
	}

	public void SetConfigComparer(IComparer<TEditorConfig> comparer)
	{
		ConfigData.SetComparer(comparer);
	}

	public void SortConfigs()
	{
		ConfigData.UpdateSortedConfigs();
	}

	/// <summary>
	/// Creates a new EditorConfig object with the given Guid.
	/// </summary>
	/// <param name="guid">Guid for new EditorConfig object.</param>
	/// <returns>New EditorConfig object.</returns>
	/// <param name="isDefaultConfig">Whether or not this EditorConfig is a default configuration.</param>
	protected abstract TEditorConfig NewEditorConfig(Guid guid, bool isDefaultConfig);

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

	public void OnNotify(string eventId, EditorConfig<TConfig> config, object payload)
	{
		switch (eventId)
		{
			case EditorConfig<TConfig>.NotificationNameChanged:
			{
				// Update the sort since the name has changed.
				ConfigData.UpdateSortedConfigs();
				break;
			}
		}
	}
}
