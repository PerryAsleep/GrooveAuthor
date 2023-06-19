namespace StepManiaEditor;

/// <summary>
/// Action to add a PerformedChart configuration.
/// </summary>
internal sealed class ActionAddPerformedChartConfig : EditorAction
{
	private readonly string ConfigName;

	public ActionAddPerformedChartConfig() : base(false, false)
	{
		ConfigName = Preferences.Instance.PreferencesExpressedChartConfig.GetNewConfigName();
	}

	public ActionAddPerformedChartConfig(string configName) : base(false, false)
	{
		ConfigName = configName;
	}

	public override string ToString()
	{
		return "Add Performed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		Preferences.Instance.PreferencesPerformedChartConfig.AddConfig(ConfigName);
	}

	protected override void UndoImplementation()
	{
		Preferences.Instance.PreferencesPerformedChartConfig.DeleteConfig(ConfigName);
	}
}
