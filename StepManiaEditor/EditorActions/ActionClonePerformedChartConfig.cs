using System;

namespace StepManiaEditor;

/// <summary>
/// Action to clone a PerformedChart configuration.
/// </summary>
internal sealed class ActionClonePerformedChartConfig : EditorAction
{
	private readonly Guid ExistingConfigGuid;
	private Guid NewConfigGuid = Guid.Empty;

	public ActionClonePerformedChartConfig(Guid existingConfigGuid) : base(false, false)
	{
		ExistingConfigGuid = existingConfigGuid;
	}

	public override string ToString()
	{
		return "Clone Performed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		var newConfig = Preferences.Instance.PreferencesPerformedChartConfig.CloneConfig(ExistingConfigGuid);
		if (newConfig == null)
			return;
		NewConfigGuid = newConfig.Guid;
		Preferences.Instance.PreferencesPerformedChartConfig.AddConfig(newConfig);
		PreferencesPerformedChartConfig.ShowEditUI(NewConfigGuid);
	}

	protected override void UndoImplementation()
	{
		if (NewConfigGuid != Guid.Empty)
			Preferences.Instance.PreferencesPerformedChartConfig.DeleteConfig(NewConfigGuid);
	}
}
