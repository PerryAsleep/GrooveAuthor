using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor
{
	internal sealed class UIChartList
	{
		Editor Editor;

		private static readonly int TypeWidth = UiScaled(60);
		private static readonly int RatingWidth = UiScaled(16);
		private static readonly int AddChartWidth = UiScaled(86);

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
				var disabled = !chart.CanBeEdited();
				if (disabled)
					PushDisabled();
				if (ImGui.Selectable($"Delete {chart.ChartDifficultyType} Chart"))
				{
					deleteChart = true;
				}
				if (ImGui.BeginMenu($"Autogenerate"))
				{
					// TODO
					//Editor.DrawAutogenerateChartSelectableList(chart);
					ImGui.EndMenu();
				}
				if (disabled)
					PopDisabled();
				ImGui.EndPopup();
			}

			return deleteChart;
		}

		public void Draw(EditorSong editorSong, EditorChart editorChart)
		{
			if (!Preferences.Instance.ShowChartListWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			if (ImGui.Begin("Chart List", ref Preferences.Instance.ShowChartListWindow, ImGuiWindowFlags.NoScrollbar))
			{
				var disabled = editorSong == null || !editorSong.CanBeEdited();
				if (disabled)
					PushDisabled();

				var numCharts = 0;
				if (editorChart != null)
				{
					var song = editorChart.GetEditorSong();
					foreach (var chartType in Editor.SupportedChartTypes)
					{
						var charts = song.GetCharts(chartType);
						if (charts != null && charts.Count > 0)
						{
							if (numCharts > 0)
							{
								ImGui.EndTable();
								ImGui.Separator();
							}

							ImGui.Text(GetPrettyEnumString(chartType));

							var ret = ImGui.BeginTable($"{chartType}Charts", 3,
								ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);
							if (ret)
							{
								ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, TypeWidth);
								ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, RatingWidth);
								ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
							}

							var index = 0;
							EditorChart pendingDeleteChart = null;
							foreach (var chart in charts)
								if (DrawChartRow(chart, index++, chart == editorChart))
									pendingDeleteChart = chart;

							if (pendingDeleteChart != null)
							{
								ActionQueue.Instance.Do(new ActionDeleteChart(Editor, pendingDeleteChart));
							}

							numCharts++;
						}
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
				if (ImGuiLayoutUtils.BeginTable("ChartAddTable", AddChartWidth))
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
							if (ImGui.Selectable(GetPrettyEnumString(chartType)))
							{
								ActionQueue.Instance.Do(new ActionAddChart(Editor, chartType));
							}
						}
						ImGui.EndPopup();
					}

					ImGuiLayoutUtils.EndTable();
				}

				if (disabled)
					PopDisabled();
			}
			ImGui.End();
		}
	}
}
