namespace StepManiaEditor;

/// <summary>
/// Action to select a given chart as the new focused chart for the active song.
/// </summary>
internal sealed class ActionSelectChart : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private readonly EditorChart PreviousChart;

	public ActionSelectChart(Editor editor, EditorChart chart) : base(false, false)
	{
		Editor = editor;
		PreviousChart = Editor.GetFocusedChart();
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
		Editor.SetChartFocused(Chart);
	}

	protected override void UndoImplementation()
	{
		Editor.SetChartFocused(PreviousChart);
	}
}
