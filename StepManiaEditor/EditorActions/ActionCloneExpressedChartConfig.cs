namespace StepManiaEditor;

/// <summary>
/// Action to clone an ExpressedChart configuration.
/// </summary>
internal sealed class ActionCloneExpressedChartConfig : EditorAction
{
	private readonly string ExistingConfigName;
	private string NewConfigName;


	public ActionCloneExpressedChartConfig(string existingConfigName) : base(false, false)
	{
		ExistingConfigName = existingConfigName;
	}

	public override string ToString()
	{
		return $"Clone {ExistingConfigName} Expressed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		var newConfig = Preferences.Instance.PreferencesExpressedChartConfig.CloneConfig(ExistingConfigName);
		if (newConfig == null)
			return;
		NewConfigName = newConfig.Name;
		Preferences.Instance.PreferencesExpressedChartConfig.AddConfig(newConfig);
		PreferencesExpressedChartConfig.ShowEditUI(NewConfigName);
	}

	protected override void UndoImplementation()
	{
		if (!string.IsNullOrEmpty(NewConfigName))
			Preferences.Instance.PreferencesExpressedChartConfig.DeleteConfig(NewConfigName);
	}
}
