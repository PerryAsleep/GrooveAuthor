using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to add an EditorPatternConfig.
/// </summary>
internal sealed class ActionAddPatternConfig : EditorAction
{
	private readonly Guid ConfigGuid;

	public ActionAddPatternConfig() : base(false, false)
	{
		ConfigGuid = Guid.NewGuid();
	}

	public ActionAddPatternConfig(Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
	}

	public Guid GetGuid()
	{
		return ConfigGuid;
	}

	public override string ToString()
	{
		return "Add Pattern Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		PatternConfigManager.Instance.AddConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		PatternConfigManager.Instance.DeleteConfig(ConfigGuid);
	}
}
