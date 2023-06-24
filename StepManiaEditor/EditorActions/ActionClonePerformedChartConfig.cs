namespace StepManiaEditor;

/// <summary>
/// Action to clone a PerformedChart configuration.
/// </summary>
internal sealed class ActionClonePerformedChartConfig : EditorAction
{
	private readonly string ExistingConfigName;
	private string NewConfigName;

	public ActionClonePerformedChartConfig(string existingConfigName) : base(false, false)
	{
		ExistingConfigName = existingConfigName;
	}

	public override string ToString()
	{
		return $"Clone {ExistingConfigName} Performed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		var newConfig = Preferences.Instance.PreferencesPerformedChartConfig.CloneConfig(ExistingConfigName);
		if (newConfig == null)
			return;
		NewConfigName = newConfig.Name;
		Preferences.Instance.PreferencesPerformedChartConfig.AddConfig(newConfig);
		PreferencesPerformedChartConfig.ShowEditUI(NewConfigName);
	}

	protected override void UndoImplementation()
	{
		if (!string.IsNullOrEmpty(NewConfigName))
			Preferences.Instance.PreferencesPerformedChartConfig.DeleteConfig(NewConfigName);
	}
}
