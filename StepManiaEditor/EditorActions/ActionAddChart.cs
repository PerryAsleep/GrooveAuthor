using static Fumen.Converters.SMCommon;

namespace StepManiaEditor
{
	/// <summary>
	/// Action to add a chart to the active song.
	/// </summary>
	internal sealed class ActionAddChart : EditorAction
	{
		private Editor Editor;
		private ChartType ChartType;
		private EditorChart AddedChart;
		private EditorChart PreivouslyActiveChart;

		public ActionAddChart(Editor editor, ChartType chartType)
		{
			Editor = editor;
			ChartType = chartType;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Add {ImGuiUtils.GetPrettyEnumString(ChartType)} Chart.";
		}

		public override void Do()
		{
			PreivouslyActiveChart = Editor.GetActiveChart();

			// Through undoing and redoing we may add the same chart multiple times.
			// Other actions like ActionAddEditorEvent reference specific charts.
			// For those actions to work as expected we should restore the same chart instance
			// rather than creating a new one when undoing and redoing.
			if (AddedChart != null)
				Editor.AddChart(AddedChart, true);
			else
				AddedChart = Editor.AddChart(ChartType, true);
		}

		public override void Undo()
		{
			Editor.DeleteChart(AddedChart, PreivouslyActiveChart);
		}
	}
}
