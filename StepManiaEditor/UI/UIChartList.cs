using System.Collections.Generic;
using System.Numerics;
using Fumen.ChartDefinition;
using Fumen.Converters;
using ImGuiNET;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor
{
	public class UIChartList
	{
		Editor Editor;

		public UIChartList(Editor editor)
		{
			Editor = editor;
		}

		private bool DrawChartRow(EditorChart chart, int index, bool active)
		{
			var deleteChart = false;
			ImGui.TableNextRow();

			var color = Utils.GetColorForDifficultyType(chart.ChartDifficultyType);

			ImGui.TableSetColumnIndex(0);
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);
			if (ImGui.Selectable($"{chart.ChartDifficultyType}##{index}", active, ImGuiSelectableFlags.SpanAllColumns))
			{
				Editor.OnChartSelected(chart);
			}
			if(ImGui.IsItemClicked(ImGuiMouseButton.Right))
			{
				ImGui.OpenPopup($"ChartRightClickPopup##{index}");
			}

			ImGui.TableSetColumnIndex(1);
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);
			ImGui.Text(chart.Rating.ToString());

			ImGui.TableSetColumnIndex(2);
			ImGui.Text(chart.Description);

			if (ImGui.BeginPopup($"ChartRightClickPopup##{index}"))
			{
				if (ImGui.Selectable($"Delete {chart.ChartDifficultyType} Chart"))
				{
					deleteChart = true;
				}
				ImGui.EndPopup();
			}

			return deleteChart;
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
					EditorChart pendingDeleteChart = null;
					foreach (var chart in kvp.Value)
						if (DrawChartRow(chart, index++, chart == editorChart))
							pendingDeleteChart = chart;

					if (pendingDeleteChart != null)
					{
						ActionQueue.Instance.Do(new ActionDeleteChart(Editor, pendingDeleteChart));
					}

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
