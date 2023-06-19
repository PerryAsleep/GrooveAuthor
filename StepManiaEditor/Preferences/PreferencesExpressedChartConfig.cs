using Fumen;
using ImGuiNET;
using StepManiaLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

/// <summary>
/// Preferences for the ExpressedChartConfigs.
/// Holds default configuration and custom user-made configurations.
/// </summary>
internal sealed class PreferencesExpressedChartConfig : Notifier<PreferencesExpressedChartConfig>
{
	// Default config names for configs which cannot be edited.
	public const string DefaultDynamicConfigName = "Dynamic";
	public const string DefaultAggressiveBracketsConfigName = "Aggressive Brackets";
	public const string DefaultNoBracketsConfigName = "No Brackets";

	// Notifications.
	public const string NotificationConfigRename = "ConfigRename";

	/// <summary>
	/// Config object with an associated string name.
	/// </summary>
	internal sealed class NamedConfig
	{
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
				var oldName = NameInternal;
				NameInternal = value;
				// Null check around OnNameUpdated because this property is set during deserialization.
				OnNameUpdated?.Invoke(oldName, NameInternal);
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
		private Action<string, string> OnNameUpdated;

		/// <summary>
		/// Sets functions to use for name validation and calling back to when the name is updated.
		/// </summary>
		/// <param name="isNewNameValid">Name validation function.</param>
		/// <param name="onNameUpdated">Callback function to invoke when the name is updated.</param>
		public void SetNameUpdateFunctions(Func<string, bool> isNewNameValid, Action<string, string> onNameUpdated)
		{
			IsNewNameValid = isNewNameValid;
			OnNameUpdated = onNameUpdated;
		}

		/// <summary>
		/// Returns whether this NamedConfig is a default config.
		/// </summary>
		/// <returns>True if this is a default config and false otherwise.</returns>
		public bool IsDefaultConfig()
		{
			return IsDefaultName(Name);
		}

		/// <summary>
		/// Returns whether the given name identifies a default config.
		/// </summary>
		/// <param name="name">Config name.</param>
		/// <returns>True if this name identifies a default config and false otherwise.</returns>
		public static bool IsDefaultName(string name)
		{
			return name.Equals(DefaultDynamicConfigName)
			       || name.Equals(DefaultAggressiveBracketsConfigName)
			       || name.Equals(DefaultNoBracketsConfigName);
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
	[JsonInclude] public string ActiveExpressedChartConfigForWindow;
	[JsonInclude] public bool ShowExpressedChartListWindow;
	[JsonInclude] public Dictionary<string, NamedConfig> Configs = new();

	/// <summary>
	/// Sorted array of Config names to use for UI.
	/// </summary>
	private string[] SortedConfigNames;

	public static void CreateNewConfigAndShowEditUI(EditorChart editorChart = null)
	{
		var newConfigName = Preferences.Instance.PreferencesExpressedChartConfig.GetNewConfigName();
		ActionQueue.Instance.Do(new ActionAddExpressedChartConfig(newConfigName, editorChart));
		ShowEditUI(newConfigName);
	}

	public static void ShowEditUI(string configName)
	{
		Preferences.Instance.PreferencesExpressedChartConfig.ActiveExpressedChartConfigForWindow = configName;
		Preferences.Instance.PreferencesExpressedChartConfig.ShowExpressedChartListWindow = true;
		ImGui.SetWindowFocus(UIExpressedChartConfig.WindowTitle);
	}

	/// <summary>
	/// Called by owning Preferences after deserialization.
	/// </summary>
	public void PostLoad()
	{
		// Set the default configs. These should never be modified so delete them if they exists and re-add it.
		Configs.Remove(DefaultDynamicConfigName);
		var defaultDynamicConfig = AddConfig(DefaultDynamicConfigName);
		defaultDynamicConfig.Description = "Default settings with dynamic bracket parsing";
		InitializeConfigWithDefaultValues(defaultDynamicConfig.Config);

		Configs.Remove(DefaultAggressiveBracketsConfigName);
		var defaultAggressiveConfig = AddConfig(DefaultAggressiveBracketsConfigName, false);
		defaultAggressiveConfig.Description = "Default settings with aggressive bracket parsing";
		defaultAggressiveConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultAggressiveConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.Aggressive;

		Configs.Remove(DefaultNoBracketsConfigName);
		var defaultNoBracketsConfig = AddConfig(DefaultNoBracketsConfigName, false);
		defaultNoBracketsConfig.Description = "Default settings that avoid brackets";
		defaultNoBracketsConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultNoBracketsConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.NoBrackets;

		// Ensure every NamedConfig is configured and valid.
		var invalidConfigNames = new List<string>();
		foreach (var kvp in Configs)
		{
			// Configure the NamedConfig will name update functions.
			kvp.Value.SetNameUpdateFunctions(IsNewConfigNameValid, OnConfigNameUpdated);

			// Validate the ExpressedChartConfig. If this fails, store it for removal.
			if (!kvp.Value.Config.Validate(kvp.Key))
			{
				invalidConfigNames.Add(kvp.Key);
			}
		}

		// Remove all invalid ExpressedChartConfig objects.
		foreach (var invalidConfigName in invalidConfigNames)
		{
			Configs.Remove(invalidConfigName);
		}

		// Ensure the variables for displaying an ExpressedChartConfig don't point to an unknown config.
		if (ActiveExpressedChartConfigForWindow != null)
		{
			if (!Configs.ContainsKey(ActiveExpressedChartConfigForWindow))
				ActiveExpressedChartConfigForWindow = null;
		}

		if (ActiveExpressedChartConfigForWindow == null)
			ShowExpressedChartListWindow = false;

		// Now that names are loaded, set the sorted array.
		UpdateSortedConfigNames();
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

	public string GetNewConfigName()
	{
		var configName = "New Config";
		if (Configs.ContainsKey(configName))
		{
			var index = 1;
			do
			{
				configName = $"New Config ({index})";
				if (!Configs.ContainsKey(configName))
					break;
				index++;
			} while (true);
		}

		return configName;
	}

	public void AddConfig(NamedConfig config)
	{
		Assert(IsNewConfigNameValid(config.Name));
		if (!IsNewConfigNameValid(config.Name))
			return;
		Configs[config.Name] = config;
		UpdateSortedConfigNames();
	}

	public NamedConfig AddConfig(string name)
	{
		return AddConfig(name, true);
	}

	private NamedConfig AddConfig(string name, bool useDefaultValues)
	{
		if (!IsNewConfigNameValid(name))
			return null;

		var config = new NamedConfig();
		config.SetNameUpdateFunctions(IsNewConfigNameValid, OnConfigNameUpdated);
		config.Name = name;
		if (useDefaultValues)
			InitializeConfigWithDefaultValues(config.Config);
		Configs[name] = config;

		UpdateSortedConfigNames();

		return config;
	}

	public void DeleteConfig(string name)
	{
		if (!Configs.TryGetValue(name, out var config))
			return;
		if (config.IsDefaultConfig())
			return;
		Configs.Remove(name);

		// If the actively displayed config is being deleted, remove the variable tracking 
		if (!string.IsNullOrEmpty(ActiveExpressedChartConfigForWindow)
		    && ActiveExpressedChartConfigForWindow.Equals(name))
		{
			ActiveExpressedChartConfigForWindow = null;
			ShowExpressedChartListWindow = false;
		}

		UpdateSortedConfigNames();
	}

	public NamedConfig GetNamedConfig(string name)
	{
		if (string.IsNullOrEmpty(name))
			return null;
		if (!Configs.TryGetValue(name, out var config))
			return null;
		return config;
	}

	public ExpressedChartConfig GetConfig(string name)
	{
		if (string.IsNullOrEmpty(name))
			return null;
		if (!Configs.TryGetValue(name, out var config))
			return null;
		return config.Config;
	}

	public bool DoesConfigExist(string name)
	{
		return !string.IsNullOrEmpty(name) && Configs.ContainsKey(name);
	}

	public bool IsNewConfigNameValid(string name)
	{
		if (string.IsNullOrEmpty(name))
			return false;
		foreach (var kvp in Configs)
		{
			if (name.Equals(kvp.Key))
				return false;
		}

		return true;
	}

	/// <summary>
	/// Called when a NamedConfig's name is updated to a new value.
	/// </summary>
	/// <param name="oldName">Old name.</param>
	/// <param name="newName">New name.</param>
	private void OnConfigNameUpdated(string oldName, string newName)
	{
		if (string.IsNullOrEmpty(oldName))
			return;
		if (!Configs.ContainsKey(oldName))
			return;

		// If the preference for the actively displayed config is holding on to the
		// old name, update it to the new name.
		if (!string.IsNullOrEmpty(ActiveExpressedChartConfigForWindow)
		    && ActiveExpressedChartConfigForWindow.Equals(oldName))
		{
			ActiveExpressedChartConfigForWindow = newName;
		}

		// Update the Configs Dictionary with the new name.
		var config = Configs[oldName];
		Configs.Remove(oldName);
		Configs[newName] = config;

		// Update the sort since the name has changed.
		UpdateSortedConfigNames();

		// Notify Observers.
		Notify(NotificationConfigRename, this, new Tuple<string, string>(oldName, newName));
	}

	private void UpdateSortedConfigNames()
	{
		var keyList = Configs.Keys.ToList();
		keyList.Sort((lhs, rhs) =>
		{
			// The default configs should be sorted first.
			var lhsDefault = NamedConfig.IsDefaultName(lhs);
			var rhsDefault = NamedConfig.IsDefaultName(rhs);
			if (lhsDefault != rhsDefault)
				return lhsDefault ? -1 : 1;

			// Configs should sort alphabetically.
			return string.Compare(lhs, rhs, StringComparison.CurrentCulture);
		});
		SortedConfigNames = keyList.ToArray();
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
