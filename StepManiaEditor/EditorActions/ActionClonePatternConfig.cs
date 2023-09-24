using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to clone an EditorPatternConfig.
/// </summary>
internal sealed class ActionClonePatternConfig : EditorAction
{
	private readonly Guid ExistingConfigGuid;
	private EditorPatternConfig ClonedConfig;

	public ActionClonePatternConfig(Guid existingConfigGuid) : base(false, false)
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
		ClonedConfig ??= PatternConfigManager.Instance.CloneConfig(ExistingConfigGuid);

		if (ClonedConfig == null)
			return;
		PatternConfigManager.Instance.AddConfig(ClonedConfig);
		EditorPatternConfig.ShowEditUI(ClonedConfig.Guid);
	}

	protected override void UndoImplementation()
	{
		if (ClonedConfig != null)
			PatternConfigManager.Instance.DeleteConfig(ClonedConfig.Guid);
	}
}
