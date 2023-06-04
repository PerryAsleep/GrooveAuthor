using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Action to add a chart to the active song.
/// </summary>
internal sealed class ActionAddChart : EditorAction
{
	private readonly Editor Editor;
	private readonly ChartType ChartType;
	private EditorChart AddedChart;
	private EditorChart PreviouslyActiveChart;

	public ActionAddChart(Editor editor, ChartType chartType) : base(false, false)
	{
		Editor = editor;
		ChartType = chartType;
	}

	public override string ToString()
	{
		return $"Add {ImGuiUtils.GetPrettyEnumString(ChartType)} Chart.";
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
			Editor.AddChart(AddedChart, true);
		else
			AddedChart = Editor.AddChart(ChartType, true);
	}

	protected override void UndoImplementation()
	{
		Editor.DeleteChart(AddedChart, PreviouslyActiveChart);
	}
}
