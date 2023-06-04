namespace StepManiaEditor;

/// <summary>
/// Action to select a given chart as the new active chart for the active song.
/// </summary>
internal sealed class ActionSelectChart : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private readonly EditorChart PreviousChart;

	public ActionSelectChart(Editor editor, EditorChart chart) : base(false, false)
	{
		Editor = editor;
		PreviousChart = Editor.GetActiveChart();
		Chart = chart;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return
			$"Select {ImGuiUtils.GetPrettyEnumString(Chart.ChartType)} {ImGuiUtils.GetPrettyEnumString(Chart.ChartDifficultyType)} Chart.";
	}

	protected override void DoImplementation()
	{
		Editor.OnChartSelected(Chart, false);
	}

	protected override void UndoImplementation()
	{
		Editor.OnChartSelected(PreviousChart, false);
	}
}
