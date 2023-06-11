namespace StepManiaEditor;

internal sealed class ActionDeletePerformedChartConfig : EditorAction
{
	private readonly string ConfigName;

	public ActionDeletePerformedChartConfig(string configName) : base(false, false)
	{
		ConfigName = configName;
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
		Preferences.Instance.PreferencesPerformedChartConfig.DeleteConfig(ConfigName);
	}

	protected override void UndoImplementation()
	{
		Preferences.Instance.PreferencesPerformedChartConfig.AddConfig(ConfigName);
	}
}
