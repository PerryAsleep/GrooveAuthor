using System;
using StepManiaEditor.AutogenConfig;

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
		ConfigManager.Instance.AddPerformedChartConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		ConfigManager.Instance.DeletePerformedChartConfig(ConfigGuid);
	}
}
