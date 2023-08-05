using System;
using StepManiaEditor.AutogenConfig;

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
		var newConfig = ConfigManager.Instance.ClonePerformedChartConfig(ExistingConfigGuid);
		if (newConfig == null)
			return;
		NewConfigGuid = newConfig.Guid;
		ConfigManager.Instance.AddPerformedChartConfig(newConfig);
		EditorPerformedChartConfig.ShowEditUI(NewConfigGuid);
	}

	protected override void UndoImplementation()
	{
		if (NewConfigGuid != Guid.Empty)
			ConfigManager.Instance.DeletePerformedChartConfig(NewConfigGuid);
	}
}
