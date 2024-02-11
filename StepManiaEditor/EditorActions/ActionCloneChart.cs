namespace StepManiaEditor;

/// <summary>
/// Action to clone an existing chart to a new a chart.
/// </summary>
internal sealed class ActionCloneChart : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorChart BaseChart;
	private EditorChart AddedChart;
	private EditorChart PreviouslyActiveChart;

	public ActionCloneChart(Editor editor, EditorChart baseChart) : base(false, false)
	{
		Editor = editor;
		BaseChart = baseChart;
	}

	public override string ToString()
	{
		return $"Clone {BaseChart.GetDescriptiveName()} Chart.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		PreviouslyActiveChart = Editor.GetActiveChart();

		// Through undoing and redoing we may add the same chart multiple times.
		// Other actions like ActionAddEditorEvent reference specific charts.
		// For those actions to work as expected we should restore the same chart instance
		// rather than creating a new one when undoing and redoing.
		if (AddedChart != null)
		{
			Editor.AddChart(AddedChart, true);
			return;
		}

		AddedChart = new EditorChart(BaseChart);
		AddedChart = Editor.AddChart(AddedChart, true);
	}

	protected override void UndoImplementation()
	{
		Editor.DeleteChart(AddedChart, PreviouslyActiveChart);
	}
}
