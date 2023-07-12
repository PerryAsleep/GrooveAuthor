using Fumen;
using ImGuiNET;
using StepManiaLibrary;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StepManiaEditor;

/// <summary>
/// Preferences for the ExpressedChartConfigs.
/// Holds default configuration and custom user-made configurations.
/// </summary>
internal sealed class PreferencesExpressedChartConfig
{
	// Default config names and guids for configs which cannot be edited.
	public const string DefaultDynamicConfigName = "Dynamic";
	public static readonly Guid DefaultDynamicConfigGuid = new("a19d532e-b0ce-4759-ad1c-02ecbbdf2efd");
	public const string DefaultAggressiveBracketsConfigName = "Aggressive Brackets";
	public static readonly Guid DefaultAggressiveBracketsConfigGuid = new("da3f6e12-49d1-416b-8db6-0ab413f740b6");
	public const string DefaultNoBracketsConfigName = "No Brackets";
	public static readonly Guid DefaultNoBracketsConfigGuid = new("0c0ba200-8f90-4060-8912-e9ea65831ebc");

	private const string NewConfigName = "New Config";

	/// <summary>
	/// Config object with an associated string name.
	/// </summary>
	internal sealed class NamedConfig
	{
		public NamedConfig()
		{
			Guid = Guid.NewGuid();
		}

		public NamedConfig(Guid guid)
		{
			Guid = guid;
		}

		/// <summary>
		/// Guid for this NamedConfig. Not readonly so that it can be set from deserialization.
		/// </summary>
		[JsonInclude] public Guid Guid;

		// Preferences.
		[JsonInclude]
		public string Name
		{
			get => NameInternal;
			set
			{
				// Null check around IsNewNameValid because this property is set during deserialization.
				if (!(IsNewNameValid?.Invoke(value) ?? true))
					return;
				if (!string.IsNullOrEmpty(NameInternal) && NameInternal.Equals(value))
					return;
				NameInternal = value;
				// Null check around OnNameUpdated because this property is set during deserialization.
				OnNameUpdated?.Invoke();
			}
		}

		private string NameInternal;
		[JsonInclude] public string Description;
		[JsonInclude] public ExpressedChartConfig Config = new();

		// Default values.
		public const BracketParsingMethod DefaultDefaultBracketParsingMethod = BracketParsingMethod.Balanced;

		public const BracketParsingDetermination DefaultBracketParsingDetermination =
			BracketParsingDetermination.ChooseMethodDynamically;

		public const int DefaultMinLevelForBrackets = 7;
		public const bool DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets = true;
		public const double DefaultBalancedBracketsPerMinuteForAggressiveBrackets = 3.0;
		public const double DefaultBalancedBracketsPerMinuteForNoBrackets = 1.0;

		/// <summary>
		/// Function to determine if a new name is valid.
		/// </summary>
		private Func<string, bool> IsNewNameValid;

		/// <summary>
		/// Callback function to invoke when the name is updated.
		/// </summary>
		private Action OnNameUpdated;

		/// <summary>
		/// Returns a new NamedConfig that is a clone of this NamedConfig.
		/// </summary>
		public NamedConfig Clone(string newConfigName)
		{
			return new NamedConfig
			{
				Config = Config.Clone(),
				Name = newConfigName,
				Description = Description,
				IsNewNameValid = IsNewNameValid,
				OnNameUpdated = OnNameUpdated,
			};
		}

		/// <summary>
		/// Sets function to use for calling back to when the name is updated.
		/// </summary>
		/// <param name="onNameUpdated">Callback function to invoke when the name is updated.</param>
		public void SetNameUpdatedFunction(Action onNameUpdated)
		{
			OnNameUpdated = onNameUpdated;
		}

		/// <summary>
		/// Returns whether this NamedConfig is a default config.
		/// </summary>
		/// <returns>True if this is a default config and false otherwise.</returns>
		public bool IsDefaultConfig()
		{
			return IsDefaultGuid(Guid);
		}

		/// <summary>
		/// Returns whether the given guid identifies a default config.
		/// </summary>
		/// <param name="guid">Config Guid.</param>
		/// <returns>True if this guid identifies a default config and false otherwise.</returns>
		public static bool IsDefaultGuid(Guid guid)
		{
			return guid.Equals(DefaultDynamicConfigGuid)
			       || guid.Equals(DefaultAggressiveBracketsConfigGuid)
			       || guid.Equals(DefaultNoBracketsConfigGuid);
		}

		public bool IsUsingDefaults()
		{
			return Config.DefaultBracketParsingMethod == DefaultDefaultBracketParsingMethod
			       && Config.BracketParsingDetermination == DefaultBracketParsingDetermination
			       && Config.MinLevelForBrackets == DefaultMinLevelForBrackets
			       && Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets ==
			       DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets
			       && Config.BalancedBracketsPerMinuteForAggressiveBrackets.DoubleEquals(
				       DefaultBalancedBracketsPerMinuteForAggressiveBrackets)
			       && Config.BalancedBracketsPerMinuteForNoBrackets.DoubleEquals(DefaultBalancedBracketsPerMinuteForNoBrackets);
		}

		public void RestoreDefaults()
		{
			// Don't enqueue an action if it would not have any effect.
			if (IsUsingDefaults())
				return;
			ActionQueue.Instance.Do(new ActionRestoreExpressedChartConfigDefaults(this));
		}
	}

	// Preferences.
	[JsonInclude] public Guid ActiveExpressedChartConfigForWindow;
	[JsonInclude] public bool ShowExpressedChartListWindow;
	[JsonInclude] public Dictionary<Guid, NamedConfig> Configs = new();

	/// <summary>
	/// Sorted array of Config guids to use for UI.
	/// </summary>
	private Guid[] SortedConfigGuids;
	/// <summary>
	/// Sorted array of Config names to use for UI.
	/// </summary>
	private string[] SortedConfigNames;

	public static void CreateNewConfigAndShowEditUI(EditorChart editorChart = null)
	{
		var newConfigGuid = Guid.NewGuid();
		ActionQueue.Instance.Do(new ActionAddExpressedChartConfig(newConfigGuid, editorChart));
		ShowEditUI(newConfigGuid);
	}

	public static void ShowEditUI(Guid configGuid)
	{
		Preferences.Instance.PreferencesExpressedChartConfig.ActiveExpressedChartConfigForWindow = configGuid;
		Preferences.Instance.PreferencesExpressedChartConfig.ShowExpressedChartListWindow = true;
		ImGui.SetWindowFocus(UIExpressedChartConfig.WindowTitle);
	}

	/// <summary>
	/// Called by owning Preferences after deserialization.
	/// </summary>
	public void PostLoad()
	{
		// Set the default configs. These should never be modified so delete them if they exists and re-add it.
		Configs.Remove(DefaultDynamicConfigGuid);
		var defaultDynamicConfig = AddConfig(DefaultDynamicConfigGuid, DefaultDynamicConfigName);
		defaultDynamicConfig.Description = "Default settings with dynamic bracket parsing";
		InitializeConfigWithDefaultValues(defaultDynamicConfig.Config);

		Configs.Remove(DefaultAggressiveBracketsConfigGuid);
		var defaultAggressiveConfig = AddConfig(DefaultAggressiveBracketsConfigGuid, DefaultAggressiveBracketsConfigName, false);
		defaultAggressiveConfig.Description = "Default settings with aggressive bracket parsing";
		defaultAggressiveConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultAggressiveConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.Aggressive;

		Configs.Remove(DefaultNoBracketsConfigGuid);
		var defaultNoBracketsConfig = AddConfig(DefaultNoBracketsConfigGuid, DefaultNoBracketsConfigName, false);
		defaultNoBracketsConfig.Description = "Default settings that avoid brackets";
		defaultNoBracketsConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultNoBracketsConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.NoBrackets;

		// Ensure every NamedConfig is configured and valid.
		var invalidConfigGuids = new List<Guid>();
		foreach (var kvp in Configs)
		{
			// Configure the NamedConfig will name update functions.
			kvp.Value.SetNameUpdatedFunction(OnConfigNameUpdated);

			// Validate the ExpressedChartConfig. If this fails, store it for removal.
			if (!kvp.Value.Config.Validate(kvp.Value.Name))
			{
				invalidConfigGuids.Add(kvp.Key);
			}
		}

		// Remove all invalid ExpressedChartConfig objects.
		foreach (var invalidConfigGuid in invalidConfigGuids)
		{
			Configs.Remove(invalidConfigGuid);
		}

		// Ensure the variables for displaying an ExpressedChartConfig don't point to an unknown config.
		if (ActiveExpressedChartConfigForWindow != Guid.Empty)
		{
			if (!Configs.ContainsKey(ActiveExpressedChartConfigForWindow))
				ActiveExpressedChartConfigForWindow = Guid.Empty;
		}

		if (ActiveExpressedChartConfigForWindow == Guid.Empty)
			ShowExpressedChartListWindow = false;

		// Now that names are loaded, set the sorted array.
		UpdateSortedConfigs();
	}

	private void InitializeConfigWithDefaultValues(ExpressedChartConfig config)
	{
		config.DefaultBracketParsingMethod = NamedConfig.DefaultDefaultBracketParsingMethod;
		config.BracketParsingDetermination = NamedConfig.DefaultBracketParsingDetermination;
		config.MinLevelForBrackets = NamedConfig.DefaultMinLevelForBrackets;
		config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets = NamedConfig
			.DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		config.BalancedBracketsPerMinuteForAggressiveBrackets = NamedConfig.DefaultBalancedBracketsPerMinuteForAggressiveBrackets;
		config.BalancedBracketsPerMinuteForNoBrackets = NamedConfig.DefaultBalancedBracketsPerMinuteForNoBrackets;
	}

	public void AddConfig(NamedConfig config)
	{
		Configs[config.Guid] = config;
		UpdateSortedConfigs();
	}

	public NamedConfig AddConfig(Guid guid, string name)
	{
		return AddConfig(guid, name, true);
	}

	public NamedConfig AddConfig(Guid guid)
	{
		return AddConfig(guid, NewConfigName, true);
	}

	public NamedConfig AddConfig(string name)
	{
		return AddConfig(Guid.NewGuid(), name, true);
	}

	private NamedConfig AddConfig(Guid guid, string name, bool useDefaultValues)
	{
		var config = new NamedConfig(guid);
		config.SetNameUpdatedFunction(OnConfigNameUpdated);
		config.Name = name;
		if (useDefaultValues)
			InitializeConfigWithDefaultValues(config.Config);
		Configs[config.Guid] = config;

		UpdateSortedConfigs();

		return config;
	}

	public void DeleteConfig(Guid guid)
	{
		if (!Configs.TryGetValue(guid, out var config))
			return;
		if (config.IsDefaultConfig())
			return;
		Configs.Remove(guid);

		// If the actively displayed config is being deleted, remove the variable tracking 
		if (ActiveExpressedChartConfigForWindow == guid)
		{
			ActiveExpressedChartConfigForWindow = Guid.Empty;
			ShowExpressedChartListWindow = false;
		}

		UpdateSortedConfigs();
	}

	public NamedConfig CloneConfig(Guid guid)
	{
		var existingConfig = GetNamedConfig(guid);
		return existingConfig?.Clone(NewConfigName);
	}

	public NamedConfig GetNamedConfig(Guid guid)
	{
		if (!Configs.TryGetValue(guid, out var config))
			return null;
		return config;
	}

	public ExpressedChartConfig GetConfig(Guid guid)
	{
		if (!Configs.TryGetValue(guid, out var config))
			return null;
		return config.Config;
	}

	public bool DoesConfigExist(Guid guid)
	{
		return Configs.ContainsKey(guid);
	}

	/// <summary>
	/// Called when a NamedConfig's name is updated to a new value.
	/// </summary>
	private void OnConfigNameUpdated()
	{
		// Update the sort since the name has changed.
		UpdateSortedConfigs();
	}

	private void UpdateSortedConfigs()
	{
		var guidsAndNames = new List<Tuple<Guid, string>>();
		foreach (var kvp in Configs)
		{
			guidsAndNames.Add(new Tuple<Guid, string>(kvp.Key, kvp.Value.Name));
		}

		guidsAndNames.Sort((lhs, rhs) =>
		{
			// The default configs should be sorted first.
			var lhsDefault = NamedConfig.IsDefaultGuid(lhs.Item1);
			var rhsDefault = NamedConfig.IsDefaultGuid(rhs.Item1);
			if (lhsDefault != rhsDefault)
				return lhsDefault ? -1 : 1;

			// Configs should sort alphabetically.
			var comparison = string.Compare(lhs.Item2, rhs.Item2, StringComparison.CurrentCulture);
			if (comparison != 0)
				return comparison;

			// Finally sort by Guid.
			return lhs.Item1.CompareTo(rhs.Item1);
		});

		SortedConfigGuids = new Guid[guidsAndNames.Count];
		SortedConfigNames = new string[guidsAndNames.Count];
		for (var i = 0; i < guidsAndNames.Count; i++)
		{
			SortedConfigGuids[i] = guidsAndNames[i].Item1;
			SortedConfigNames[i] = guidsAndNames[i].Item2;
		}
	}

	public Guid[] GetSortedConfigGuids()
	{
		return SortedConfigGuids;
	}

	public string[] GetSortedConfigNames()
	{
		return SortedConfigNames;
	}
}

/// <summary>
/// Action to restore an Expressed Chart Config to its default values.
/// </summary>
internal sealed class ActionRestoreExpressedChartConfigDefaults : EditorAction
{
	private readonly PreferencesExpressedChartConfig.NamedConfig Config;
	private readonly BracketParsingMethod PreviousDefaultBracketParsingMethod;
	private readonly BracketParsingDetermination PreviousBracketParsingDetermination;
	private readonly int PreviousMinLevelForBrackets;
	private readonly bool PreviousUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
	private readonly double PreviousBalancedBracketsPerMinuteForAggressiveBrackets;
	private readonly double PreviousBalancedBracketsPerMinuteForNoBrackets;

	public ActionRestoreExpressedChartConfigDefaults(PreferencesExpressedChartConfig.NamedConfig config) : base(false, false)
	{
		Config = config;
		PreviousDefaultBracketParsingMethod = Config.Config.DefaultBracketParsingMethod;
		PreviousBracketParsingDetermination = Config.Config.BracketParsingDetermination;
		PreviousMinLevelForBrackets = Config.Config.MinLevelForBrackets;
		PreviousUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets =
			Config.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		PreviousBalancedBracketsPerMinuteForAggressiveBrackets = Config.Config.BalancedBracketsPerMinuteForAggressiveBrackets;
		PreviousBalancedBracketsPerMinuteForNoBrackets = Config.Config.BalancedBracketsPerMinuteForNoBrackets;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Restore {Config.Name} Expressed Chart Config to default values.";
	}

	protected override void DoImplementation()
	{
		Config.Config.DefaultBracketParsingMethod =
			PreferencesExpressedChartConfig.NamedConfig.DefaultDefaultBracketParsingMethod;
		Config.Config.BracketParsingDetermination =
			PreferencesExpressedChartConfig.NamedConfig.DefaultBracketParsingDetermination;
		Config.Config.MinLevelForBrackets = PreferencesExpressedChartConfig.NamedConfig.DefaultMinLevelForBrackets;
		Config.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets =
			PreferencesExpressedChartConfig.NamedConfig
				.DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		Config.Config.BalancedBracketsPerMinuteForAggressiveBrackets = PreferencesExpressedChartConfig.NamedConfig
			.DefaultBalancedBracketsPerMinuteForAggressiveBrackets;
		Config.Config.BalancedBracketsPerMinuteForNoBrackets =
			PreferencesExpressedChartConfig.NamedConfig.DefaultBalancedBracketsPerMinuteForNoBrackets;
	}

	protected override void UndoImplementation()
	{
		Config.Config.DefaultBracketParsingMethod = PreviousDefaultBracketParsingMethod;
		Config.Config.BracketParsingDetermination = PreviousBracketParsingDetermination;
		Config.Config.MinLevelForBrackets = PreviousMinLevelForBrackets;
		Config.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets =
			PreviousUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		Config.Config.BalancedBracketsPerMinuteForAggressiveBrackets = PreviousBalancedBracketsPerMinuteForAggressiveBrackets;
		Config.Config.BalancedBracketsPerMinuteForNoBrackets = PreviousBalancedBracketsPerMinuteForNoBrackets;
	}
}
