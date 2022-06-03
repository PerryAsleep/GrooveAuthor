using System.Numerics;
using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing Chart properties UI.
	/// </summary>
	public class UIChartProperties
	{
		private readonly Editor Editor;
		private static EditorChart EditorChart;

		public UIChartProperties(Editor editor)
		{
			Editor = editor;
		}

		public void Draw(EditorChart editorChart)
		{
			EditorChart = editorChart;

			if (!Preferences.Instance.ShowChartPropertiesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Chart Properties", ref Preferences.Instance.ShowChartPropertiesWindow, ImGuiWindowFlags.NoScrollbar);

			if (EditorChart == null)
				Utils.PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("ChartInfoTable", 100))
			{
				// The notes in the chart only make sense for one ChartType. Do not allow changing the ChartType.
				Utils.PushDisabled();
				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartType>(true, "Type", EditorChart, nameof(EditorChart.ChartType),
					"Chart type.");
				Utils.PopDisabled();

				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartDifficultyType>(true, "Difficulty", EditorChart, nameof(EditorChart.ChartDifficultyType),
					"Chart difficulty type.");
				ImGuiLayoutUtils.DrawRowInputInt(true, "Rating", EditorChart, nameof(EditorChart.Rating),
					"Chart rating.", true, 1);
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", EditorChart, nameof(EditorChart.Name),
					"Chart name.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", EditorChart, nameof(EditorChart.Description),
					"Chart description.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Credit", EditorChart, nameof(EditorChart.Credit),
					"Who this chart should be credited to.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Style", EditorChart, nameof(EditorChart.Style),
					"(Uncommon) Originally meant to denote \"Pad\" versus \"Keyboard\" charts.");
				
				ImGuiLayoutUtils.EndTable();
			}

			if (EditorChart == null)
				Utils.PopDisabled();
		}
	}
}
