using System.Collections.Generic;
using System.Numerics;
using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor
{
	public class UIChartList
	{
		Editor Editor;

		public UIChartList(Editor editor)
		{
			Editor = editor;
		}

		private void DrawChartRow(EditorChart chart, bool active)
		{
			ImGui.TableNextRow();

			ImGui.TableSetColumnIndex(0);
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Utils.GetColorForDifficultyType(chart.ChartDifficultyType));
			if (ImGui.Selectable(chart.ChartDifficultyType.ToString(), active, ImGuiSelectableFlags.SpanAllColumns))
			{
				Editor.OnChartSelected(chart);
			}

			ImGui.TableSetColumnIndex(1);
			ImGui.Text(chart.Rating.ToString());

			ImGui.TableSetColumnIndex(2);
			ImGui.Text(chart.Description);
		}

		public void Draw(EditorChart editorChart)
		{
			if (!Preferences.Instance.ShowChartListWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Chart List", ref Preferences.Instance.ShowChartListWindow, ImGuiWindowFlags.NoScrollbar);

			if (editorChart == null)
			{
				Utils.PushDisabled();
			}

			var numCharts = 0;
			if (editorChart != null)
			{
				var song = editorChart.EditorSong;
				foreach (var kvp in song.Charts)
				{
					var chartType = kvp.Key;
	
					if (numCharts > 0)
					{
						ImGui.EndTable();
						ImGui.Separator();
					}

					ImGui.Text(Utils.GetPrettyEnumString(chartType));

					var ret = ImGui.BeginTable($"{chartType}Charts", 3,
						ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);
					if (ret)
					{
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60);
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 16);
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
					}

					foreach (var chart in kvp.Value)
						DrawChartRow(chart, chart == editorChart);

					numCharts++;
				}
				if (numCharts > 0)
				{
					ImGui.EndTable();
				}
			}

			if (numCharts == 0)
			{
				ImGui.Text("No Charts");
			}
				
			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("ChartAddTable", 100))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Add Chart", "Add Chart"))
				{

				}
				ImGuiLayoutUtils.EndTable();
			}

			if (editorChart == null)
			{
				Utils.PopDisabled();
			}
		}
	}
}
