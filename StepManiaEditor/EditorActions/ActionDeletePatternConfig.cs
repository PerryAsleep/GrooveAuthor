using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to delete an EditorPatternConfig.
/// </summary>
internal sealed class ActionDeletePatternConfig : EditorAction
{
	private readonly Guid ConfigGuid;
	private readonly EditorPatternConfig Config;

	public ActionDeletePatternConfig(Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
		Config = PatternConfigManager.Instance.GetConfig(ConfigGuid);
	}

	public override string ToString()
	{
		return $"Delete {Config.Name} Pattern Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		PatternConfigManager.Instance.DeleteConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		PatternConfigManager.Instance.AddConfig(Config);
	}
}
