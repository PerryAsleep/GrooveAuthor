using System;

namespace StepManiaEditor;

/// <summary>
/// Action to delete a PerformedChart configuration.
/// </summary>
internal sealed class ActionDeletePerformedChartConfig : EditorAction
{
	private readonly Guid ConfigGuid;
	private readonly PreferencesPerformedChartConfig.NamedConfig NamedConfig;
	private bool LastSelectedAutogenPerformedChartConfigUsedDeletedConfig;

	public ActionDeletePerformedChartConfig(Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
		NamedConfig = Preferences.Instance.PreferencesPerformedChartConfig.GetNamedConfig(ConfigGuid);
	}

	public override string ToString()
	{
		return $"Delete {NamedConfig.Name} Performed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		LastSelectedAutogenPerformedChartConfigUsedDeletedConfig =
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig == ConfigGuid;
		Preferences.Instance.PreferencesPerformedChartConfig.DeleteConfig(ConfigGuid);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig =
				PreferencesPerformedChartConfig.DefaultConfigGuid;
	}

	protected override void UndoImplementation()
	{
		Preferences.Instance.PreferencesPerformedChartConfig.AddConfig(NamedConfig);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig = ConfigGuid;
	}
}
