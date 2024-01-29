using System;
using StepManiaLibrary.PerformedChart;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// ConfigManager for EditorPerformedChartConfig objects.
/// This class should be accessed through its static Instance member.
/// </summary>
internal sealed class PerformedChartConfigManager : ConfigManager<EditorPerformedChartConfig, Config>
{
	// Default config names and guids for EditorPerformedChartConfigs which cannot be edited.
	public const string DefaultPerformedChartConfigName = "Default Chart: Balanced";
	public static readonly Guid DefaultPerformedChartConfigGuid = new("6276c906-ea8f-43b3-9500-0ddeac7bdc22");
	public const string DefaultPerformedChartStaminaConfigName = "Default Chart: Stamina";
	public static readonly Guid DefaultPerformedChartStaminaGuid = new("c0334922-6105-4703-add2-3de261b2ff19");

	public const string DefaultPerformedChartPatternBalancedConfigName = "Default Pattern: Balanced";
	public static readonly Guid DefaultPerformedChartPatternBalancedGuid = new("41a7f76e-596a-4fbe-bf6e-6a77a898e4a1");
	public const string DefaultPerformedChartPatternNoCandleConfigName = "Default Pattern: No Candles";
	public static readonly Guid DefaultPerformedChartPatternNoCandleGuid = new("69d041be-ef12-4609-815e-b591d512e259");
	public const string DefaultPerformedChartPatternNoInwardConfigName = "Default Pattern: No Inward";
	public static readonly Guid DefaultPerformedChartPatternNoInwardGuid = new("eba3802f-66ec-42d4-87cf-66e9dadc9d0a");
	public const string DefaultPerformedChartPatternNoTransitionLimitConfigName = "Default Pattern: No Transition Limit";
	public static readonly Guid DefaultPerformedChartPatternNoTransitionLimitGuid = new("0dde2261-90c5-4413-82a6-9034dad80aa1");

	/// <summary>
	/// Static instance.
	/// </summary>
	public static PerformedChartConfigManager Instance { get; private set; } = new();

	/// <summary>
	/// Private constructor.
	/// </summary>
	private PerformedChartConfigManager() : base("pcc-", "Performed Chart")
	{
	}

	/// <summary>
	/// Creates a new EditorPerformedChartConfig object with the given Guid.
	/// </summary>
	/// <param name="guid">Guid for new EditorPerformedChartConfig object.</param>
	/// <param name="isDefaultConfig">Whether or not this EditorConfig is a default configuration.</param>
	/// <returns>New EditorPerformedChartConfig object.</returns>
	protected override EditorPerformedChartConfig NewEditorConfig(Guid guid, bool isDefaultConfig)
	{
		return new EditorPerformedChartConfig(guid, isDefaultConfig);
	}

	/// <summary>
	/// Adds all default EditorPerformedChartConfig objects.
	/// </summary>
	protected override void AddDefaultConfigs()
	{
		DeleteConfig(DefaultPerformedChartConfigGuid);
		var defaultConfig = AddDefaultConfig(DefaultPerformedChartConfigGuid, DefaultPerformedChartConfigName);
		defaultConfig.ShortName = "Balanced";
		defaultConfig.Description = "Default chart generation settings. Balanced.";

		DeleteConfig(DefaultPerformedChartStaminaGuid);
		var defaultStaminaConfig = AddDefaultConfig(DefaultPerformedChartStaminaGuid, DefaultPerformedChartStaminaConfigName);
		defaultStaminaConfig.Description = "Default chart generation settings. Good for stamina.";
		defaultStaminaConfig.ShortName = "Stamina";
		defaultStaminaConfig.TravelSpeedMinBPM = 99;
		defaultStaminaConfig.Config.Transitions.Enabled = true;
		defaultStaminaConfig.Config.Transitions.StepsPerTransitionMin = 32;
		defaultStaminaConfig.Config.LateralTightening.AbsoluteNPS = 26.666667;

		DeleteConfig(DefaultPerformedChartPatternBalancedGuid);
		var defaultPatternBalancedConfig = AddDefaultConfig(DefaultPerformedChartPatternBalancedGuid,
			DefaultPerformedChartPatternBalancedConfigName);
		defaultPatternBalancedConfig.ShortName = "Balanced";
		defaultPatternBalancedConfig.Description = "Default pattern generation settings. Balanced.";
		defaultPatternBalancedConfig.Config.Transitions.Enabled = true;
		defaultPatternBalancedConfig.Config.Transitions.StepsPerTransitionMin = 32;
		defaultPatternBalancedConfig.Config.LateralTightening.Enabled = false;
		defaultPatternBalancedConfig.Config.StepTightening.SpeedTighteningEnabled = false;
		defaultPatternBalancedConfig.Config.StepTightening.DistanceTighteningEnabled = true;
		defaultPatternBalancedConfig.Config.StepTightening.DistanceMin = 1.2;

		DeleteConfig(DefaultPerformedChartPatternNoCandleGuid);
		var defaultPatternNoCandleConfig = AddDefaultConfig(DefaultPerformedChartPatternNoCandleGuid,
			DefaultPerformedChartPatternNoCandleConfigName);
		defaultPatternNoCandleConfig.ShortName = "No Candles";
		defaultPatternNoCandleConfig.Description = "Default pattern generation settings. No candles.";
		defaultPatternNoCandleConfig.Config.Transitions.Enabled = true;
		defaultPatternNoCandleConfig.Config.Transitions.StepsPerTransitionMin = 32;
		defaultPatternNoCandleConfig.Config.LateralTightening.Enabled = false;
		defaultPatternNoCandleConfig.Config.StepTightening.SpeedTighteningEnabled = false;
		defaultPatternNoCandleConfig.Config.StepTightening.DistanceTighteningEnabled = true;
		defaultPatternNoCandleConfig.Config.StepTightening.DistanceMin = 0.74;

		DeleteConfig(DefaultPerformedChartPatternNoInwardGuid);
		var defaultPatternNoInwardConfig = AddDefaultConfig(DefaultPerformedChartPatternNoInwardGuid,
			DefaultPerformedChartPatternNoInwardConfigName);
		defaultPatternNoInwardConfig.ShortName = "No Inward";
		defaultPatternNoInwardConfig.Description = "Default pattern generation settings. No inward-facing orientations.";
		defaultPatternNoInwardConfig.Config.Transitions.Enabled = true;
		defaultPatternNoInwardConfig.Config.Transitions.StepsPerTransitionMin = 32;
		defaultPatternNoInwardConfig.Config.LateralTightening.Enabled = false;
		defaultPatternNoInwardConfig.Config.StepTightening.SpeedTighteningEnabled = false;
		defaultPatternNoInwardConfig.Config.StepTightening.DistanceTighteningEnabled = true;
		defaultPatternNoInwardConfig.Config.StepTightening.DistanceMin = 1.2;
		defaultPatternNoInwardConfig.Config.Facing.MaxInwardPercentage = 0.0;

		DeleteConfig(DefaultPerformedChartPatternNoTransitionLimitGuid);
		var defaultPatternNoTransitionLimitsConfig = AddDefaultConfig(DefaultPerformedChartPatternNoTransitionLimitGuid,
			DefaultPerformedChartPatternNoTransitionLimitConfigName);
		defaultPatternNoTransitionLimitsConfig.ShortName = "No Transition Limits";
		defaultPatternNoTransitionLimitsConfig.Description = "Default pattern generation settings. No transition limits.";
		defaultPatternNoTransitionLimitsConfig.Config.Transitions.Enabled = false;
		defaultPatternNoTransitionLimitsConfig.Config.LateralTightening.Enabled = false;
		defaultPatternNoTransitionLimitsConfig.Config.StepTightening.SpeedTighteningEnabled = false;
		defaultPatternNoTransitionLimitsConfig.Config.StepTightening.DistanceTighteningEnabled = true;
		defaultPatternNoTransitionLimitsConfig.Config.StepTightening.DistanceMin = 1.2;
	}

	/// <summary>
	/// Called after all EditorConfig objects have been loaded, initialized, and validated.
	/// </summary>
	protected override void OnPostLoadComplete()
	{
		// Ensure the variables for displaying an EditorPerformedChartConfig don't point to an unknown config.
		if (Preferences.Instance.ActivePerformedChartConfigForWindow != Guid.Empty)
		{
			if (ConfigData.GetConfig(Preferences.Instance.ActivePerformedChartConfigForWindow) == null)
				Preferences.Instance.ActivePerformedChartConfigForWindow = Guid.Empty;
		}

		if (Preferences.Instance.ActivePerformedChartConfigForWindow == Guid.Empty)
			Preferences.Instance.ShowPerformedChartListWindow = false;
	}

	/// <summary>
	/// Called when an EditorConfig with the given Guid is deleted.
	/// </summary>
	/// <param name="guid">Guid of deleted EditorConfig.</param>
	protected override void OnConfigDeleted(Guid guid)
	{
		// If the actively displayed config is being deleted, remove the variable tracking 
		if (Preferences.Instance.ActivePerformedChartConfigForWindow == guid)
		{
			Preferences.Instance.ActivePerformedChartConfigForWindow = Guid.Empty;
			Preferences.Instance.ShowPerformedChartListWindow = false;
		}
	}
}
