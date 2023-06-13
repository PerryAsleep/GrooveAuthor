using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Fumen;
using ImGuiNET;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Preferences for the PerformedChartConfigs.
/// Holds default configuration and custom user-made configurations.
/// </summary>
internal sealed class PreferencesPerformedChartConfig : Notifier<PreferencesPerformedChartConfig>
{
	// Default config names for configs which cannot be edited.
	public const string DefaultConfigName = "Default";
	public const string DefaultStaminaConfigName = "Default Stamina";

	// Notifications.
	public const string NotificationConfigRename = "ConfigRename";

	/// <summary>
	/// Config object with an associated string name.
	/// </summary>
	internal sealed class NamedConfig
	{
		static NamedConfig()
		{
			DefaultArrowWeights = new Dictionary<string, List<int>>
			{
				[ChartTypeString(ChartType.dance_single)] = new() { 25, 25, 25, 25 },
				[ChartTypeString(ChartType.dance_double)] = new() { 6, 12, 10, 22, 22, 12, 10, 6 },
				[ChartTypeString(ChartType.dance_solo)] = new() { 13, 12, 25, 25, 12, 13 },
				[ChartTypeString(ChartType.dance_threepanel)] = new() { 25, 50, 25 },
				[ChartTypeString(ChartType.pump_single)] = new() { 17, 16, 34, 16, 17 },
				[ChartTypeString(ChartType.pump_halfdouble)] = new() { 25, 12, 13, 13, 12, 25 },
				[ChartTypeString(ChartType.pump_double)] = new() { 4, 4, 17, 12, 13, 13, 12, 17, 4, 4 },
				[ChartTypeString(ChartType.smx_beginner)] = new() { 25, 50, 25 },
				[ChartTypeString(ChartType.smx_single)] = new() { 25, 21, 8, 21, 25 },
				[ChartTypeString(ChartType.smx_dual)] = new() { 8, 17, 25, 25, 17, 8 },
				[ChartTypeString(ChartType.smx_full)] = new() { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 },
			};
		}

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
		[JsonInclude] public Config Config = new();

		// Default values.
		public const double DefaultFacingMaxInwardPercentage = 1.0;
		public const double DefaultFacingMaxOutwardPercentage = 1.0;
		public const double DefaultStepTighteningTravelSpeedMinTimeSeconds = 0.176471;
		public const double DefaultStepTighteningTravelSpeedMaxTimeSeconds = 0.24;
		public const double DefaultStepTighteningTravelDistanceMin = 2.0;
		public const double DefaultStepTighteningTravelDistanceMax = 3.0;
		public const double DefaultStepTighteningStretchDistanceMin = 3.0;
		public const double DefaultStepTighteningStretchDistanceMax = 4.0;
		public const double DefaultLateralTighteningRelativeNPS = 1.65;
		public const double DefaultLateralTighteningAbsoluteNPS = 12.0;
		public const double DefaultLateralTighteningSpeed = 3.0;
		public static readonly Dictionary<string, List<int>> DefaultArrowWeights;

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
		/// Sets the arrow weight for a given lane of a given ChartType.
		/// Will update normalized weights.
		/// </summary>
		/// <param name="chartType">ChartType to set the arrow weight for.</param>
		/// <param name="laneIndex">Lane index to set the arrow weight for.</param>
		/// <param name="weight">New weight</param>
		public void SetArrowWeight(ChartType chartType, int laneIndex, int weight)
		{
			var chartTypeString = ChartTypeString(chartType);
			if (!Config.ArrowWeights.TryGetValue(chartTypeString, out var weights))
				return;
			if (laneIndex < 0 || laneIndex >= weights.Count)
				return;
			if (weights[laneIndex] == weight)
				return;
			weights[laneIndex] = weight;
			Config.RefreshArrowWeightsNormalized();
		}

		/// <summary>
		/// Gets the maximum number of weights for any ChartType in this PerformedChart Config.
		/// </summary>
		/// <returns>Maximum number of weights for any ChartType in this PerformedChart Config</returns>
		public int GetMaxNumWeightsForAnyChartType()
		{
			var max = 0;
			foreach (var (_, weights) in Config.ArrowWeights)
				max = Math.Max(max, weights.Count);
			return max;
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
			return name.Equals(DefaultConfigName)
			       || name.Equals(DefaultStaminaConfigName);
		}

		public bool IsUsingDefaults()
		{
			if (Config.Facing.MaxInwardPercentage.DoubleEquals(DefaultFacingMaxInwardPercentage)
			    && Config.Facing.MaxOutwardPercentage.DoubleEquals(DefaultFacingMaxOutwardPercentage)
			    && Config.StepTightening.TravelSpeedMinTimeSeconds.DoubleEquals(DefaultStepTighteningTravelSpeedMinTimeSeconds)
			    && Config.StepTightening.TravelSpeedMaxTimeSeconds.DoubleEquals(DefaultStepTighteningTravelSpeedMaxTimeSeconds)
			    && Config.StepTightening.TravelDistanceMin.DoubleEquals(DefaultStepTighteningTravelDistanceMin)
			    && Config.StepTightening.TravelDistanceMax.DoubleEquals(DefaultStepTighteningTravelDistanceMax)
			    && Config.StepTightening.StretchDistanceMin.DoubleEquals(DefaultStepTighteningStretchDistanceMin)
			    && Config.StepTightening.StretchDistanceMax.DoubleEquals(DefaultStepTighteningStretchDistanceMax)
			    && Config.LateralTightening.RelativeNPS.DoubleEquals(DefaultLateralTighteningRelativeNPS)
			    && Config.LateralTightening.AbsoluteNPS.DoubleEquals(DefaultLateralTighteningAbsoluteNPS)
			    && Config.LateralTightening.Speed.DoubleEquals(DefaultLateralTighteningSpeed))
			{
				return true;
			}

			if (Config.ArrowWeights.Count != DefaultArrowWeights.Count)
				return false;
			foreach (var (chartType, weights) in Config.ArrowWeights)
			{
				if (!DefaultArrowWeights.TryGetValue(chartType, out var defaultWeights))
					return false;
				if (weights.Count != defaultWeights.Count)
					return false;
				for (var i = 0; i < weights.Count; i++)
				{
					if (weights[i] != defaultWeights[i])
						return false;
				}
			}

			return true;
		}

		public void RestoreDefaults()
		{
			// Don't enqueue an action if it would not have any effect.
			if (IsUsingDefaults())
				return;
			ActionQueue.Instance.Do(new ActionRestorePerformedChartConfigDefaults(this));
		}
	}

	// Preferences.
	[JsonInclude] public string ActivePerformedChartConfigForWindow;
	[JsonInclude] public bool ShowPerformedChartListWindow;
	[JsonInclude] public Dictionary<string, NamedConfig> Configs = new();

	/// <summary>
	/// Sorted array of Config names to use for UI.
	/// </summary>
	private string[] SortedConfigNames;

	public static void CreateNewConfigAndShowEditUI()
	{
		var newConfigName = Preferences.Instance.PreferencesPerformedChartConfig.GetNewConfigName();
		ActionQueue.Instance.Do(new ActionAddPerformedChartConfig(newConfigName));
		ShowEditUI(newConfigName);
	}

	public static void ShowEditUI(string configName)
	{
		Preferences.Instance.PreferencesPerformedChartConfig.ActivePerformedChartConfigForWindow = configName;
		Preferences.Instance.PreferencesPerformedChartConfig.ShowPerformedChartListWindow = true;
		ImGui.SetWindowFocus(UIPerformedChartConfig.WindowTitle);
	}

	/// <summary>
	/// Called by owning Preferences after deserialization.
	/// </summary>
	public void PostLoad()
	{
		// Set the default configs. These should never be modified so delete them if they exists and re-add it.
		Configs.Remove(DefaultConfigName);
		var defaultConfig = AddConfig(DefaultConfigName);
		defaultConfig.Description = "Default balanced settings";
		InitializeConfigWithDefaultValues(defaultConfig.Config);

		Configs.Remove(DefaultStaminaConfigName);
		var defaultAggressiveConfig = AddConfig(DefaultStaminaConfigName);
		defaultAggressiveConfig.Description = "Default settings with more aggressive step tightening";
		defaultAggressiveConfig.Config.StepTightening.TravelSpeedMaxTimeSeconds = 0.303;

		// Ensure every NamedConfig is configured and valid.
		var invalidConfigNames = new List<string>();
		foreach (var kvp in Configs)
		{
			// Configure the NamedConfig will name update functions.
			kvp.Value.SetNameUpdateFunctions(IsNewConfigNameValid, OnConfigNameUpdated);

			// Validate the Config. If this fails, store it for removal.
			if (!kvp.Value.Config.Validate(kvp.Key))
			{
				invalidConfigNames.Add(kvp.Key);
			}
		}

		// Remove all invalid PerformedChartConfig objects.
		foreach (var invalidConfigName in invalidConfigNames)
		{
			Configs.Remove(invalidConfigName);
		}

		// Ensure the variables for displaying a Config don't point to an unknown config.
		if (ActivePerformedChartConfigForWindow != null)
		{
			if (!Configs.ContainsKey(ActivePerformedChartConfigForWindow))
				ActivePerformedChartConfigForWindow = null;
		}

		if (ActivePerformedChartConfigForWindow == null)
			ShowPerformedChartListWindow = false;

		// Now that names are loaded, set the sorted array.
		UpdateSortedConfigNames();
	}

	private void InitializeConfigWithDefaultValues(Config config)
	{
		config.Facing.MaxInwardPercentage = NamedConfig.DefaultFacingMaxInwardPercentage;
		config.Facing.MaxOutwardPercentage = NamedConfig.DefaultFacingMaxOutwardPercentage;
		config.StepTightening.TravelSpeedMinTimeSeconds = NamedConfig.DefaultStepTighteningTravelSpeedMinTimeSeconds;
		config.StepTightening.TravelSpeedMaxTimeSeconds = NamedConfig.DefaultStepTighteningTravelSpeedMaxTimeSeconds;
		config.StepTightening.TravelDistanceMin = NamedConfig.DefaultStepTighteningTravelDistanceMin;
		config.StepTightening.TravelDistanceMax = NamedConfig.DefaultStepTighteningTravelDistanceMax;
		config.StepTightening.StretchDistanceMin = NamedConfig.DefaultStepTighteningStretchDistanceMin;
		config.StepTightening.StretchDistanceMax = NamedConfig.DefaultStepTighteningStretchDistanceMax;
		config.LateralTightening.RelativeNPS = NamedConfig.DefaultLateralTighteningRelativeNPS;
		config.LateralTightening.AbsoluteNPS = NamedConfig.DefaultLateralTighteningAbsoluteNPS;
		config.LateralTightening.Speed = NamedConfig.DefaultLateralTighteningSpeed;

		config.ArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (chartType, weights) in NamedConfig.DefaultArrowWeights)
		{
			var defaultWeights = new List<int>(weights.Count);
			foreach (var weight in weights)
				defaultWeights.Add(weight);
			config.ArrowWeights[chartType] = defaultWeights;
		}
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
		config.Config.Init();

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
		if (!string.IsNullOrEmpty(ActivePerformedChartConfigForWindow)
		    && ActivePerformedChartConfigForWindow.Equals(name))
		{
			ActivePerformedChartConfigForWindow = null;
			ShowPerformedChartListWindow = false;
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

	public Config GetConfig(string name)
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
		if (!string.IsNullOrEmpty(ActivePerformedChartConfigForWindow)
		    && ActivePerformedChartConfigForWindow.Equals(oldName))
		{
			ActivePerformedChartConfigForWindow = newName;
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
/// Action to restore a Performed Chart Config to its default values.
/// </summary>
internal sealed class ActionRestorePerformedChartConfigDefaults : EditorAction
{
	private readonly PreferencesPerformedChartConfig.NamedConfig Config;

	private readonly double PreviousFacingMaxInwardPercentage;
	private readonly double PreviousFacingMaxOutwardPercentage;
	private readonly double PreviousStepTighteningTravelSpeedMinTimeSeconds;
	private readonly double PreviousStepTighteningTravelSpeedMaxTimeSeconds;
	private readonly double PreviousStepTighteningTravelDistanceMin;
	private readonly double PreviousStepTighteningTravelDistanceMax;
	private readonly double PreviousStepTighteningStretchDistanceMin;
	private readonly double PreviousStepTighteningStretchDistanceMax;
	private readonly double PreviousLateralTighteningRelativeNPS;
	private readonly double PreviousLateralTighteningAbsoluteNPS;
	private readonly double PreviousLateralTighteningSpeed;
	private readonly Dictionary<string, List<int>> PreviousArrowWeights;

	public ActionRestorePerformedChartConfigDefaults(PreferencesPerformedChartConfig.NamedConfig config) : base(false, false)
	{
		Config = config;

		PreviousFacingMaxInwardPercentage = Config.Config.Facing.MaxInwardPercentage;
		PreviousFacingMaxOutwardPercentage = Config.Config.Facing.MaxOutwardPercentage;
		PreviousStepTighteningTravelSpeedMinTimeSeconds = Config.Config.StepTightening.TravelSpeedMinTimeSeconds;
		PreviousStepTighteningTravelSpeedMaxTimeSeconds = Config.Config.StepTightening.TravelSpeedMaxTimeSeconds;
		PreviousStepTighteningTravelDistanceMin = Config.Config.StepTightening.TravelDistanceMin;
		PreviousStepTighteningTravelDistanceMax = Config.Config.StepTightening.TravelDistanceMax;
		PreviousStepTighteningStretchDistanceMin = Config.Config.StepTightening.StretchDistanceMin;
		PreviousStepTighteningStretchDistanceMax = Config.Config.StepTightening.StretchDistanceMax;
		PreviousLateralTighteningRelativeNPS = Config.Config.LateralTightening.RelativeNPS;
		PreviousLateralTighteningAbsoluteNPS = Config.Config.LateralTightening.AbsoluteNPS;
		PreviousLateralTighteningSpeed = Config.Config.LateralTightening.Speed;
		PreviousArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (chartType, weights) in Config.Config.ArrowWeights)
		{
			var previousWeights = new List<int>(weights.Count);
			foreach (var weight in weights)
				previousWeights.Add(weight);
			PreviousArrowWeights[chartType] = previousWeights;
		}
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Restore {Config.Name} Performed Chart Config to default values.";
	}

	protected override void DoImplementation()
	{
		Config.Config.Facing.MaxInwardPercentage = PreferencesPerformedChartConfig.NamedConfig.DefaultFacingMaxInwardPercentage;
		Config.Config.Facing.MaxOutwardPercentage = PreferencesPerformedChartConfig.NamedConfig.DefaultFacingMaxOutwardPercentage;
		Config.Config.StepTightening.TravelSpeedMinTimeSeconds =
			PreferencesPerformedChartConfig.NamedConfig.DefaultStepTighteningTravelSpeedMinTimeSeconds;
		Config.Config.StepTightening.TravelSpeedMaxTimeSeconds =
			PreferencesPerformedChartConfig.NamedConfig.DefaultStepTighteningTravelSpeedMaxTimeSeconds;
		Config.Config.StepTightening.TravelDistanceMin =
			PreferencesPerformedChartConfig.NamedConfig.DefaultStepTighteningTravelDistanceMin;
		Config.Config.StepTightening.TravelDistanceMax =
			PreferencesPerformedChartConfig.NamedConfig.DefaultStepTighteningTravelDistanceMax;
		Config.Config.StepTightening.StretchDistanceMin =
			PreferencesPerformedChartConfig.NamedConfig.DefaultStepTighteningStretchDistanceMin;
		Config.Config.StepTightening.StretchDistanceMax =
			PreferencesPerformedChartConfig.NamedConfig.DefaultStepTighteningStretchDistanceMax;
		Config.Config.LateralTightening.RelativeNPS =
			PreferencesPerformedChartConfig.NamedConfig.DefaultLateralTighteningRelativeNPS;
		Config.Config.LateralTightening.AbsoluteNPS =
			PreferencesPerformedChartConfig.NamedConfig.DefaultLateralTighteningAbsoluteNPS;
		Config.Config.LateralTightening.Speed = PreferencesPerformedChartConfig.NamedConfig.DefaultLateralTighteningSpeed;

		Config.Config.ArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (defaultChartType, defaultWeights) in PreferencesPerformedChartConfig.NamedConfig.DefaultArrowWeights)
		{
			var weights = new List<int>(defaultWeights.Count);
			foreach (var defaultWeight in defaultWeights)
				weights.Add(defaultWeight);
			Config.Config.ArrowWeights[defaultChartType] = weights;
		}
	}

	protected override void UndoImplementation()
	{
		Config.Config.Facing.MaxInwardPercentage = PreviousFacingMaxInwardPercentage;
		Config.Config.Facing.MaxOutwardPercentage = PreviousFacingMaxOutwardPercentage;
		Config.Config.StepTightening.TravelSpeedMinTimeSeconds = PreviousStepTighteningTravelSpeedMinTimeSeconds;
		Config.Config.StepTightening.TravelSpeedMaxTimeSeconds = PreviousStepTighteningTravelSpeedMaxTimeSeconds;
		Config.Config.StepTightening.TravelDistanceMin = PreviousStepTighteningTravelDistanceMin;
		Config.Config.StepTightening.TravelDistanceMax = PreviousStepTighteningTravelDistanceMax;
		Config.Config.StepTightening.StretchDistanceMin = PreviousStepTighteningStretchDistanceMin;
		Config.Config.StepTightening.StretchDistanceMax = PreviousStepTighteningStretchDistanceMax;
		Config.Config.LateralTightening.RelativeNPS = PreviousLateralTighteningRelativeNPS;
		Config.Config.LateralTightening.AbsoluteNPS = PreviousLateralTighteningAbsoluteNPS;
		Config.Config.LateralTightening.Speed = PreviousLateralTighteningSpeed;

		Config.Config.ArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (previousChartType, previousWeights) in PreviousArrowWeights)
		{
			var weights = new List<int>(previousWeights.Count);
			foreach (var previousWeight in previousWeights)
				weights.Add(previousWeight);
			Config.Config.ArrowWeights[previousChartType] = weights;
		}
	}
}
