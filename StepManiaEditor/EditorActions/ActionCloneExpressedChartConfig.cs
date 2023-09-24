using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to clone an EditorExpressedChartConfig.
/// </summary>
internal sealed class ActionCloneExpressedChartConfig : EditorAction
{
	private readonly Guid ExistingConfigGuid;
	private EditorExpressedChartConfig ClonedConfig;

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
		// Only clone once. We want a consistent new Guid across undo and redo.
		ClonedConfig ??= ExpressedChartConfigManager.Instance.CloneConfig(ExistingConfigGuid);

		if (ClonedConfig == null)
			return;
		ExpressedChartConfigManager.Instance.AddConfig(ClonedConfig);
		EditorExpressedChartConfig.ShowEditUI(ClonedConfig.Guid);
	}

	protected override void UndoImplementation()
	{
		if (ClonedConfig != null)
			ExpressedChartConfigManager.Instance.DeleteConfig(ClonedConfig.Guid);
	}
}
