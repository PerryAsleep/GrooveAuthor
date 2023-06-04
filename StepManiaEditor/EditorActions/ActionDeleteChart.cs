namespace StepManiaEditor;

/// <summary>
/// Action to delete a chart from the active song.
/// </summary>
internal sealed class ActionDeleteChart : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private bool DeletedActiveChart;

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
		DeletedActiveChart = Editor.GetActiveChart() == Chart;
		Editor.DeleteChart(Chart, null);
	}

	protected override void UndoImplementation()
	{
		Editor.AddChart(Chart, DeletedActiveChart);
	}
}
