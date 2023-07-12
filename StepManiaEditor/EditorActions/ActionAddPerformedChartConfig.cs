using System;

namespace StepManiaEditor;

/// <summary>
/// Action to add a PerformedChart configuration.
/// </summary>
internal sealed class ActionAddPerformedChartConfig : EditorAction
{
	private readonly Guid ConfigGuid;

	public ActionAddPerformedChartConfig() : base(false, false)
	{
		ConfigGuid = Guid.NewGuid();
	}

	public ActionAddPerformedChartConfig(Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
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
		Preferences.Instance.PreferencesPerformedChartConfig.AddConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		Preferences.Instance.PreferencesPerformedChartConfig.DeleteConfig(ConfigGuid);
	}
}
