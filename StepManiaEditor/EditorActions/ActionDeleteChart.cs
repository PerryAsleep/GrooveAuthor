
namespace StepManiaEditor
{
	/// <summary>
	/// Action to delete a chart from the active song.
	/// </summary>
	internal sealed class ActionDeleteChart : EditorAction
	{
		private Editor Editor;
		private EditorChart Chart;
		private bool DeletedActiveChart;

		public ActionDeleteChart(Editor editor, EditorChart chart)
		{
			Editor = editor;
			Chart = chart;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Delete {Utils.GetPrettyEnumString(Chart.ChartType)} Chart.";
		}

		public override void Do()
		{
			DeletedActiveChart = Editor.GetActiveChart() == Chart;
			Editor.DeleteChart(Chart, null);
		}

		public override void Undo()
		{
			Editor.AddChart(Chart, DeletedActiveChart);
		}
	}
}
