using System.Collections.Generic;
using System.Numerics;
using Fumen.ChartDefinition;
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

		private void DrawChartRow(EditorChart chart, int index, bool active)
		{
			ImGui.TableNextRow();

			var color = Utils.GetColorForDifficultyType(chart.ChartDifficultyType);

			ImGui.TableSetColumnIndex(0);
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);
			if (ImGui.Selectable($"{chart.ChartDifficultyType}##{index}", active, ImGuiSelectableFlags.SpanAllColumns))
			{
				Editor.OnChartSelected(chart);
			}

			ImGui.TableSetColumnIndex(1);
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);
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

					var index = 0;
					foreach (var chart in kvp.Value)
						DrawChartRow(chart, index++, chart == editorChart);

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
					ImGui.OpenPopup("AddChartPopup");
				}
				if (ImGui.BeginPopup("AddChartPopup"))
				{
					ImGui.Text("Type");
					ImGui.Separator();
					foreach (var chartType in Editor.SupportedChartTypes)
					{
						if(ImGui.Selectable(Utils.GetPrettyEnumString(chartType)))
						{
							ActionQueue.Instance.Do(new ActionAddChart(Editor, chartType));
						}
					}
					ImGui.EndPopup();
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
