namespace StepManiaEditor;

/// <summary>
/// Action to delete a PerformedChart configuration.
/// </summary>
internal sealed class ActionDeletePerformedChartConfig : EditorAction
{
	private readonly string ConfigName;
	private readonly PreferencesPerformedChartConfig.NamedConfig NamedConfig;
	private bool LastSelectedAutogenPerformedChartConfigUsedDeletedConfig = false;

	public ActionDeletePerformedChartConfig(string configName) : base(false, false)
	{
		ConfigName = configName;
		NamedConfig = Preferences.Instance.PreferencesPerformedChartConfig.GetNamedConfig(ConfigName);
	}

	public override string ToString()
	{
		return $"Delete {ConfigName} Performed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		LastSelectedAutogenPerformedChartConfigUsedDeletedConfig =
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig == ConfigName;
		Preferences.Instance.PreferencesPerformedChartConfig.DeleteConfig(ConfigName);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig =
				PreferencesPerformedChartConfig.DefaultConfigName;
	}

	protected override void UndoImplementation()
	{
		Preferences.Instance.PreferencesPerformedChartConfig.AddConfig(NamedConfig);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig = ConfigName;
	}
}
