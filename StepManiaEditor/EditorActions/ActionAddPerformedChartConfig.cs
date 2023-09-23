using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to add an EditorPerformedChartConfig.
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

	public Guid GetGuid()
	{
		return ConfigGuid;
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
		PerformedChartConfigManager.Instance.AddConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		PerformedChartConfigManager.Instance.DeleteConfig(ConfigGuid);
	}
}
