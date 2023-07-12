using System;

namespace StepManiaEditor;

/// <summary>
/// Action to clone an ExpressedChart configuration.
/// </summary>
internal sealed class ActionCloneExpressedChartConfig : EditorAction
{
	private readonly Guid ExistingConfigGuid;
	private Guid NewConfigGuid = Guid.Empty;

	public ActionCloneExpressedChartConfig(Guid existingConfigGuid) : base(false, false)
	{
		ExistingConfigGuid = existingConfigGuid;
	}

	public override string ToString()
	{
		return "Clone Expressed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		var newConfig = Preferences.Instance.PreferencesExpressedChartConfig.CloneConfig(ExistingConfigGuid);
		if (newConfig == null)
			return;
		NewConfigGuid = newConfig.Guid;
		Preferences.Instance.PreferencesExpressedChartConfig.AddConfig(newConfig);
		PreferencesExpressedChartConfig.ShowEditUI(NewConfigGuid);
	}

	protected override void UndoImplementation()
	{
		if (NewConfigGuid != Guid.Empty)
			Preferences.Instance.PreferencesExpressedChartConfig.DeleteConfig(NewConfigGuid);
	}
}
