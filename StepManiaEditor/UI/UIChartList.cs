using System;
using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

internal sealed class UIChartList
{
	private readonly Editor Editor;

	private static readonly int TypeWidth = UiScaled(60);
	private static readonly int RatingWidth = UiScaled(16);
	private static readonly int AddChartWidth = UiScaled(86);

	private EditorChart ChartPendingDelete;

	public UIChartList(Editor editor)
	{
		Editor = editor;
	}

	public void Draw(EditorSong editorSong, EditorChart editorChart)
	{
		if (!Preferences.Instance.ShowChartListWindow)
			return;

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin("Chart List", ref Preferences.Instance.ShowChartListWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanSongBeEdited(editorSong);
			if (disabled)
				PushDisabled();

			var numCharts = DrawChartList(
				editorChart,
				ChartRightClickMenu,
				selectedChart => { Editor.OnChartSelected(selectedChart); },
				false,
				null);
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

	/// <summary>
	/// Draws all charts for the Song associated with the given Chart as a list.
	/// The charts are grouped and sorted and formatted nicely.
	/// The list can be drawn as Selectable items or as BeginMenu items.
	/// </summary>
	/// <param name="activeChart">The chart to get the Song from.</param>
	/// <param name="onRightClick">(Selectable only) Action to invoke when right-clicked.</param>
	/// <param name="onSelected">(Selectable only) Action to invoke when selected.</param>
	/// <param name="asBeginMenuItems">
	/// If true then draw rows as BeginMenu items.
	/// If false then draw rows as Selectable items.
	/// </param>
	/// <param name="onMenu">(BeginMenu only) Action to invoke when selected in menu.</param>
	/// <returns>Number of charts drawn.</returns>
	public int DrawChartList(
		EditorChart activeChart,
		Action<EditorChart> onRightClick,
		Action<EditorChart> onSelected,
		bool asBeginMenuItems,
		Action<EditorChart> onMenu)
	{
		var numCharts = 0;
		ChartPendingDelete = null;

		if (activeChart != null)
		{
			var song = activeChart.GetEditorSong();
			foreach (var chartType in Editor.SupportedChartTypes)
			{
				var charts = song.GetCharts(chartType);
				if (charts?.Count > 0)
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
					foreach (var chart in charts)
					{
						DrawChartRow(
							chart,
							index++,
							chart == activeChart,
							onRightClick,
							onSelected,
							asBeginMenuItems,
							onMenu);
					}

					if (ChartPendingDelete != null)
					{
						ActionQueue.Instance.Do(new ActionDeleteChart(Editor, ChartPendingDelete));
					}
					ChartPendingDelete = null;

					numCharts++;
				}
			}

			if (numCharts > 0)
			{
				ImGui.EndTable();
			}
		}

		return numCharts;
	}

	private void DrawChartRow(
		EditorChart chart,
		int index,
		bool active,
		Action<EditorChart> onRightClick,
		Action<EditorChart> onSelected,
		bool asBeginMenuItems,
		Action<EditorChart> onMenu)
	{
		ImGui.TableNextRow();

		var color = Utils.GetColorForDifficultyType(chart.ChartDifficultyType);

		ImGui.TableSetColumnIndex(0);
		ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);

		// When drawing the row as a BeginMenu item, we can't put the three separate table columns
		// into one MenuItem control. As a workaround, treat every column as a MenuItem. This has
		// some undesirable effects:
		// 1) Only one column is highlighted at a time as opposed to the entire row.
		// 2) As the mouse slides between columns, the submenu flickers.

		// Difficulty type.
		if (asBeginMenuItems)
		{
			if (ImGui.BeginMenu($"{chart.ChartDifficultyType}##{index}", true))
			{
				onMenu(chart);
				ImGui.EndMenu();
			}
		}
		else
		{
			if (ImGui.Selectable($"{chart.ChartDifficultyType}##{index}", active, ImGuiSelectableFlags.SpanAllColumns))
			{
				onSelected(chart);
			}

			if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
			{
				ImGui.OpenPopup($"ChartRightClickPopup##{index}");
			}
		}

		// Rating
		ImGui.TableSetColumnIndex(1);
		ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);
		if (asBeginMenuItems)
		{
			if (ImGui.BeginMenu($"{chart.Rating}##{index}", true))
			{
				onMenu(chart);
				ImGui.EndMenu();
			}
		}
		else
		{
			ImGui.Text(chart.Rating.ToString());
		}

		// Description
		ImGui.TableSetColumnIndex(2);
		if (asBeginMenuItems)
		{
			if (ImGui.BeginMenu($"{chart.Description}##{index}", true))
			{
				onMenu(chart);
				ImGui.EndMenu();
			}
		}
		else
		{
			ImGui.Text(chart.Description);
		}

		// If the selectable was right clicked, invoke the right click action.
		if (ImGui.BeginPopup($"ChartRightClickPopup##{index}"))
		{
			onRightClick(chart);
			ImGui.EndPopup();
		}
	}

	private void ChartRightClickMenu(EditorChart chart)
	{
		var disabled = !Editor.CanChartBeEdited(chart);
		if (disabled)
			PushDisabled();
		if (ImGui.Selectable($"Delete {chart.ChartDifficultyType} Chart"))
		{
			ChartPendingDelete = chart;
		}

		if (ImGui.BeginMenu("Autogen"))
		{
			Editor.DrawAutogenerateChartSelectableList(chart);
			ImGui.EndMenu();
		}

		if (disabled)
			PopDisabled();
	}
}
