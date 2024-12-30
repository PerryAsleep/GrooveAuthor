using System;
using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class UIChartList : UIWindow
{
	private Editor Editor;

	private static readonly int TypeWidth = UiScaled(60);
	private static readonly int RatingWidth = UiScaled(16);
	private static readonly int AddChartWidth = UiScaled(90);
	private static readonly int RowHeight = UiScaled(19);
	private static readonly float DefaultPositionX = UiScaled(0);
	private static readonly float DefaultPositionY = UiScaled(901);
	private static readonly Vector2 DefaultSize = new(UiScaled(622), UiScaled(179));

	private EditorChart ChartPendingDelete;
	private EditorChart ChartPendingClone;

	public static UIChartList Instance { get; } = new();

	private UIChartList() : base("Chart List")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowChartListWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowChartListWindow = false;
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
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowChartListWindow))
		{
			var disabled = !Editor.CanSongBeEdited(editorSong);
			if (disabled)
				PushDisabled();

			ChartPendingDelete = null;
			ChartPendingClone = null;

			var numCharts = DrawChartList(
				Editor,
				editorSong,
				editorChart,
				ChartRightClickMenu,
				selectedChart => { Editor.SetChartFocused(selectedChart); },
				selectedChart =>
				{
					Editor.SetChartFocused(selectedChart);
					Editor.SetChartHasDedicatedTab(selectedChart, true);
				},
				true);

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
					"Clone Current",
					() => { ActionQueue.Instance.Do(new ActionCloneChart(Editor, editorChart)); },
					cloneEnabled,
					"Autogen...",
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
	/// Public static method for drawing a simple chart list.
	/// </summary>
	/// <param name="activeSong">The active Song to derive the Chart list from.</param>
	/// <param name="selectedChart">The currently selected Chart.</param>
	/// <param name="onSelected">Action to invoke when selected.</param>
	/// <returns>Number of charts drawn.</returns>
	public static int DrawChartList(
		EditorSong activeSong,
		EditorChart selectedChart,
		Action<EditorChart> onSelected)
	{
		return DrawChartList(null, activeSong, selectedChart, null, onSelected, null, false);
	}

	/// <summary>
	/// Draws all charts for the Song associated with the given Chart as a list.
	/// The charts are grouped and sorted and formatted nicely.
	/// </summary>
	/// <param name="editor">Editor Instance, needed for selection checkboxes.</param>
	/// <param name="activeSong">The active Song to derive the Chart list from.</param>
	/// <param name="selectedChart">The currently selected Chart.</param>
	/// <param name="onRightClick">Action to invoke when right-clicked.</param>
	/// <param name="onSelected">Action to invoke when selected.</param>
	/// <param name="onDoubleClick">Action to invoke when double-clicked.</param>
	/// <param name="primaryChartList">
	/// If true, this is the primary chart list which uses different coloration and has
	/// a close button.
	/// </param>
	/// <returns>Number of charts drawn.</returns>
	private static int DrawChartList(
		Editor editor,
		EditorSong activeSong,
		EditorChart selectedChart,
		Action<EditorChart> onRightClick,
		Action<EditorChart> onSelected,
		Action<EditorChart> onDoubleClick,
		bool primaryChartList)
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
						ImGui.Separator();
					}

					ImGui.Text(GetPrettyEnumString(chartType));

					var width = ImGui.GetContentRegionAvail().X;
					if (primaryChartList)
					{
						width -= GetCloseWidth(); // + ImGui.GetStyle().CellPadding.X * 2;
					}

					if (ImGui.BeginTable($"{chartType}Charts", 3, ImGuiTableFlags.Borders, new Vector2(width, 0)))
					{
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, TypeWidth);
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, RatingWidth);
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
					}

					var index = 0;
					foreach (var chart in charts)
					{
						DrawChartRow(
							editor,
							chart,
							index++,
							chart == selectedChart,
							onRightClick,
							onSelected,
							onDoubleClick,
							primaryChartList);
					}

					ImGui.EndTable();

					if (primaryChartList)
					{
						var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
						var originalItemSpacingY = ImGui.GetStyle().ItemSpacing.Y;
						var originalFramePaddingX = ImGui.GetStyle().FramePadding.X;
						var originalFramePaddingY = ImGui.GetStyle().FramePadding.Y;
						var originalCellPaddingX = ImGui.GetStyle().CellPadding.X;
						var originalCellPaddingY = ImGui.GetStyle().CellPadding.Y;

						ImGui.GetStyle().ItemSpacing.X = 0;
						ImGui.GetStyle().ItemSpacing.Y = 0;
						ImGui.GetStyle().FramePadding.X = 0;
						ImGui.GetStyle().FramePadding.Y = 0;
						ImGui.GetStyle().CellPadding.X = 0;
						ImGui.GetStyle().CellPadding.Y = 0;

						ImGui.SameLine();

						width = GetCloseWidth() + ImGui.GetStyle().CellPadding.X * 2;
						if (ImGui.BeginTable($"{chartType}Charts Close Buttons", 1,
							    ImGuiTableFlags.Borders, new Vector2(width, 0)))
						{
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
						}

						index = 0;
						foreach (var chart in charts)
						{
							DrawChartRowCloseButton(editor, index++, chart);
						}

						// Restore the padding and spacing values.
						ImGui.GetStyle().CellPadding.X = originalCellPaddingX;
						ImGui.GetStyle().CellPadding.Y = originalCellPaddingY;
						ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
						ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;
						ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
						ImGui.GetStyle().ItemSpacing.Y = originalItemSpacingY;

						ImGui.EndTable();
					}

					numCharts++;
				}
			}
		}

		return numCharts;
	}

	private static void DrawChartRow(
		Editor editor,
		EditorChart chart,
		int index,
		bool selected,
		Action<EditorChart> onRightClick,
		Action<EditorChart> onSelected,
		Action<EditorChart> onDoubleClick,
		bool primaryChartList)
	{
		ImGui.TableNextRow();

		var color = GetColorForDifficultyType(chart.ChartDifficultyType);
		var activeChartData = editor?.GetActiveChartData(chart);
		if (primaryChartList)
		{
			if (activeChartData != null)
				color = ColorRGBAMultiply(color, UIFocusedChartColorMultiplier);
			else
				color = ColorRGBAMultiply(color, UIUnfocusedChartColorMultiplier);
		}

		ImGui.TableSetColumnIndex(0);
		ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);

		// Difficulty type.
		var flags = ImGuiSelectableFlags.SpanAllColumns;
		if (onDoubleClick != null)
			flags |= ImGuiSelectableFlags.AllowDoubleClick;
		if (ImGui.Selectable($"{chart.ChartDifficultyType}##{index}", selected, flags))
		{
			if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && onDoubleClick != null)
				onDoubleClick(chart);
			else
				onSelected(chart);
		}

		if (onRightClick != null && ImGui.IsItemClicked(ImGuiMouseButton.Right))
		{
			ImGui.OpenPopup($"ChartRightClickPopup##{index}");
		}

		// Rating.
		ImGui.TableSetColumnIndex(1);
		ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, color);
		ImGui.Text(chart.Rating.ToString());

		// Description.
		ImGui.TableSetColumnIndex(2);
		if (primaryChartList)
		{
			var colorPushCount = 0;
			if (activeChartData != null)
			{
				ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, UITableRowBgActiveChartColor);
				if (!activeChartData.HasDedicatedTab())
				{
					ImGui.PushStyleColor(ImGuiCol.Text, UINonDedicatedTabTextColor);
					colorPushCount++;
				}
			}

			ImGui.Text(chart.Description);
			ImGui.PopStyleColor(colorPushCount);
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

	private static void DrawChartRowCloseButton(Editor editor, int index, EditorChart chart)
	{
		ImGui.TableNextRow(ImGuiTableRowFlags.None, RowHeight);
		ImGui.TableSetColumnIndex(0);
		if (editor?.GetActiveChartData(chart) == null)
			return;
		if (ImGui.Button($"X##Close{index}", new Vector2(GetCloseWidth(), RowHeight)))
			editor.CloseChart(chart);
	}

	private void ChartRightClickMenu(EditorChart chart)
	{
		var disabled = !Editor.CanChartBeEdited(chart);
		if (disabled)
			PushDisabled();

		if (ImGui.MenuItem($"Delete {chart.GetShortName()} Chart"))
		{
			ChartPendingDelete = chart;
		}

		if (ImGui.MenuItem($"Clone {chart.GetShortName()} Chart"))
		{
			ChartPendingClone = chart;
		}

		if (ImGui.MenuItem($"Autogen New Chart From {chart.GetShortName()} Chart..."))
		{
			Editor.ShowAutogenChartUI(chart);
		}

		ImGui.Separator();
		Editor.DrawCopyChartEventsMenuItems(chart);

		if (disabled)
			PopDisabled();
	}
}
