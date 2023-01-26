
namespace StepManiaEditor
{
	/// <summary>
	/// Action to select a given chart as the new active chart for the active song.
	/// </summary>
	internal sealed class ActionSelectChart : EditorAction
	{
		private Editor Editor;
		private EditorChart Chart;
		private EditorChart PreviousChart;

		public ActionSelectChart(Editor editor, EditorChart chart)
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
			return $"Select {Utils.GetPrettyEnumString(Chart.ChartType)} {Utils.GetPrettyEnumString(Chart.ChartDifficultyType)} Chart.";
		}

		public override void Do()
		{
			Editor.OnChartSelected(Chart, false);
		}

		public override void Undo()
		{
			Editor.OnChartSelected(PreviousChart, false);
		}
	}
}
