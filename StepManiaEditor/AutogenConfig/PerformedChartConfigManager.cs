using System;
using Config = StepManiaLibrary.PerformedChart.Config;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// ConfigManager for EditorPerformedChartConfig objects.
/// This class should be accessed through its static Instance member.
/// </summary>
internal sealed class PerformedChartConfigManager : ConfigManager<EditorPerformedChartConfig, Config>
{
	// Default config names and guids for EditorPerformedChartConfigs which cannot be edited.
	public const string DefaultPerformedChartConfigName = "Default";
	public static readonly Guid DefaultPerformedChartConfigGuid = new("6276c906-ea8f-43b3-9500-0ddeac7bdc22");
	public const string DefaultPerformedChartStaminaConfigName = "Default Stamina";
	public static readonly Guid DefaultPerformedChartStaminaGuid = new("c0334922-6105-4703-add2-3de261b2ff19");

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
	/// <returns>New EditorPerformedChartConfig object.</returns>
	protected override EditorPerformedChartConfig NewEditorConfig(Guid guid)
	{
		return new EditorPerformedChartConfig(guid);
	}

	/// <summary>
	/// Adds all default EditorPerformedChartConfig objects.
	/// </summary>
	protected override void AddDefaultConfigs()
	{
		// Add default balanced config. This should never be modified so delete it if it exists and re-add it.
		ConfigData.RemoveConfig(DefaultPerformedChartConfigGuid);
		var defaultConfig = AddConfig(DefaultPerformedChartConfigGuid, DefaultPerformedChartConfigName);
		defaultConfig.Description = "Default balanced settings";
		defaultConfig.InitializeWithDefaultValues();

		// Add default stamina config. This should never be modified so delete it if it exists and re-add it.
		ConfigData.RemoveConfig(DefaultPerformedChartStaminaGuid);
		var defaultStaminaConfig = AddConfig(DefaultPerformedChartStaminaGuid, DefaultPerformedChartStaminaConfigName);
		defaultStaminaConfig.Description = "Default stamina settings.";
		defaultStaminaConfig.TravelSpeedMinBPM = 99;
		defaultStaminaConfig.Config.Transitions.Enabled = true;
		defaultStaminaConfig.Config.Transitions.StepsPerTransitionMin = 32;
		defaultStaminaConfig.Config.LateralTightening.AbsoluteNPS = 26.666667;
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
