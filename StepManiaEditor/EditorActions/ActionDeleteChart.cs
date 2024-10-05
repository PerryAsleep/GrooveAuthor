namespace StepManiaEditor;

/// <summary>
/// Action to delete a chart from the active song.
/// </summary>
internal sealed class ActionDeleteChart : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private bool DeletedFocusedChart;
	private bool DeletedTimingChart;

	public ActionDeleteChart(Editor editor, EditorChart chart) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
	}

	public override string ToString()
	{
		return $"Delete {ImGuiUtils.GetPrettyEnumString(Chart.ChartType)} Chart.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		DeletedFocusedChart = Editor.GetFocusedChart() == Chart;
		DeletedTimingChart = Editor.GetActiveSong().TimingChart == Chart;
		Editor.DeleteChart(Chart, null);
	}

	protected override void UndoImplementation()
	{
		Editor.AddChart(Chart, DeletedFocusedChart);
		if (DeletedTimingChart)
			Editor.GetActiveSong().TimingChart = Chart;
	}
}
