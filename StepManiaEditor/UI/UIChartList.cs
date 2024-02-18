using System;
using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

internal sealed class UIChartList
{
	public const string WindowTitle = "Chart List";

	private readonly Editor Editor;

	private static readonly int TypeWidth = UiScaled(60);
	private static readonly int RatingWidth = UiScaled(16);
	private static readonly int AddChartWidth = UiScaled(86);
	private static readonly float DefaultPositionX = UiScaled(0);
	private static readonly float DefaultPositionY = UiScaled(872);
	private static readonly Vector2 DefaultSize = new(UiScaled(622), UiScaled(208));

	private EditorChart ChartPendingDelete;
	private EditorChart ChartPendingClone;

	public UIChartList(Editor editor)
	{
		Editor = editor;
	}

	public void Draw(EditorSong editorSong, EditorChart editorChart)
	{
		if (!Preferences.Instance.ShowChartListWindow)
			return;

		// We always try to show UIChartList on first launch. If showing it would put it off the screen, then don't show it.
		if (Preferences.Instance.FirstTimeTryingToShowChartListWindow)
		{
			Preferences.Instance.FirstTimeTryingToShowChartListWindow = false;
			if (DefaultPositionY > UiScaled(Editor.GetViewportHeight()))
			{
				Preferences.Instance.ShowChartListWindow = false;
				return;
			}
		}

		var defaultPosition = new Vector2(DefaultPositionX,
			Math.Max(0, Math.Min(UiScaled(Editor.GetViewportHeight()) - DefaultSize.Y, DefaultPositionY)));
		ImGui.SetNextWindowPos(defaultPosition, ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(DefaultSize, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowChartListWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanSongBeEdited(editorSong);
			if (disabled)
				PushDisabled();

			ChartPendingDelete = null;
			ChartPendingClone = null;

			var numCharts = DrawChartList(
				editorSong,
				editorChart,
				ChartRightClickMenu,
				selectedChart => { Editor.OnChartSelected(selectedChart); },
				false,
				null);

			if (ChartPendingDelete != null)
			{
				ActionQueue.Instance.Do(new ActionDeleteChart(Editor, ChartPendingDelete));
				ChartPendingDelete = null;
			}

			if (ChartPendingClone != null)
			{
				ActionQueue.Instance.Do(new ActionCloneChart(Editor, ChartPendingClone));
				ChartPendingClone = null;
			}

			if (numCharts == 0)
			{
				ImGui.Text("No Charts");
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("ChartAddTable", AddChartWidth))
			{
				var cloneEnabled = editorChart != null;

				ImGuiLayoutUtils.DrawRowThreeButtons("Add New Chart",
					"New Chart...",
					() => { ImGui.OpenPopup("AddChartPopup"); },
					true,
					"Clone Current Chart",
					() => { ActionQueue.Instance.Do(new ActionCloneChart(Editor, editorChart)); },
					cloneEnabled,
					"Autogen New Chart...",
					() => { Editor.ShowAutogenChartUI(editorChart); },
					cloneEnabled,
					"Add a new blank chart or create a new chart using an existing chart as a starting point.");

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
	/// <param name="activeSong">The active Song to derive the Chart list from.</param>
	/// <param name="activeChart">The currently active Chart.</param>
	/// <param name="onRightClick">(Selectable only) Action to invoke when right-clicked.</param>
	/// <param name="onSelected">(Selectable only) Action to invoke when selected.</param>
	/// <param name="asBeginMenuItems">
	/// If true then draw rows as BeginMenu items.
	/// If false then draw rows as Selectable items.
	/// </param>
	/// <param name="onMenu">(BeginMenu only) Action to invoke when selected in menu.</param>
	/// <returns>Number of charts drawn.</returns>
	public static int DrawChartList(
		EditorSong activeSong,
		EditorChart activeChart,
		Action<EditorChart> onRightClick,
		Action<EditorChart> onSelected,
		bool asBeginMenuItems,
		Action<EditorChart> onMenu)
	{
		var numCharts = 0;

		if (activeSong != null)
		{
			foreach (var chartType in Editor.SupportedChartTypes)
			{
				var charts = activeSong.GetCharts(chartType);
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

	private static void DrawChartRow(
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

			if (onRightClick != null && ImGui.IsItemClicked(ImGuiMouseButton.Right))
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
		if (onRightClick != null && ImGui.BeginPopup($"ChartRightClickPopup##{index}"))
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

		if (ImGui.MenuItem($"Delete {chart.ChartDifficultyType} Chart"))
		{
			ChartPendingDelete = chart;
		}

		if (ImGui.MenuItem($"Clone {chart.ChartDifficultyType} Chart"))
		{
			ChartPendingClone = chart;
		}

		if (ImGui.MenuItem($"Autogen New Chart From {chart.ChartDifficultyType} Chart..."))
		{
			Editor.ShowAutogenChartUI(chart);
		}

		Editor.DrawCopyChartEventsMenuItems(chart);

		if (disabled)
			PopDisabled();
	}
}
