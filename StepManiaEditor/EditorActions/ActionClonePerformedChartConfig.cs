using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to clone an EditorPerformedChartConfig.
/// </summary>
internal sealed class ActionClonePerformedChartConfig : EditorAction
{
	private readonly Guid ExistingConfigGuid;
	private EditorPerformedChartConfig ClonedConfig;

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
		// Only clone once. We want a consistent new Guid across undo and redo.
		ClonedConfig ??= PerformedChartConfigManager.Instance.CloneConfig(ExistingConfigGuid);

		if (ClonedConfig == null)
			return;
		PerformedChartConfigManager.Instance.AddConfig(ClonedConfig);
		EditorPerformedChartConfig.ShowEditUI(ClonedConfig.Guid);
	}

	protected override void UndoImplementation()
	{
		if (ClonedConfig != null)
			PerformedChartConfigManager.Instance.DeleteConfig(ClonedConfig.Guid);
	}
}
