using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to add an ExpressedChart configuration.
/// </summary>
internal sealed class ActionAddExpressedChartConfig : EditorAction
{
	private readonly Guid ConfigGuid;
	private readonly EditorChart EditorChart;
	private readonly Guid EditorChartOldConfigGuid;

	public ActionAddExpressedChartConfig() : base(false, false)
	{
		ConfigGuid = Guid.NewGuid();
	}

	public ActionAddExpressedChartConfig(Guid configGuid, EditorChart editorChart) : base(false, false)
	{
		ConfigGuid = configGuid;
		EditorChart = editorChart;
		if (EditorChart != null)
		{
			EditorChartOldConfigGuid = EditorChart.ExpressedChartConfig;
		}
	}

	public override string ToString()
	{
		return "Add Expressed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return EditorChart != null;
	}

	protected override void DoImplementation()
	{
		ConfigManager.Instance.AddExpressedChartConfig(ConfigGuid);
		if (EditorChart != null)
		{
			EditorChart.ExpressedChartConfig = ConfigGuid;
		}
	}

	protected override void UndoImplementation()
	{
		ConfigManager.Instance.DeleteExpressedChartConfig(ConfigGuid);
		if (EditorChart != null)
		{
			EditorChart.ExpressedChartConfig = EditorChartOldConfigGuid;
		}
	}
}
