using ImGuiNET;
using Microsoft.Xna.Framework;
using StepManiaEditor;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.Utils;
using Vector2 = System.Numerics.Vector2;

/// <summary>
/// Class for drawing headers above charts.
/// </summary>
internal sealed class UIChartHeader
{
	private readonly ActiveEditorChart Chart;
	private readonly Editor Editor;

	private const int NumButtons = 3;
	private static readonly int ItemSpacing = UiScaled(0);

	public UIChartHeader(Editor editor, ActiveEditorChart chart)
	{
		Editor = editor;
		Chart = chart;
	}

	/// <summary>
	/// Draw a background bar to go behind all chart headers.
	/// This helps improve the layout when some UI like the mini map and density graph
	/// are mounted to the window rather than the chart. We still want to draw them below
	/// the area for the headers. Having the entire top of the chart area occupied by a
	/// bar helps this layout read a little better.
	/// </summary>
	/// <param name="chartArea"></param>
	public static void DrawBackground(Rectangle chartArea)
	{
		var h = GetChartHeaderHeight();
		var originalWindowBorderSize = ImGui.GetStyle().WindowBorderSize;
		var originalMinWindowSize = ImGui.GetStyle().WindowMinSize;
		ImGui.GetStyle().WindowBorderSize = 0;

		ImGui.PushStyleColor(ImGuiCol.ChildBg, UIWindowColor);

		var size = new Vector2(chartArea.Width, h);
		ImGui.GetStyle().WindowMinSize = size;
		ImGui.SetNextWindowPos(new Vector2(chartArea.X, chartArea.Y));
		ImGui.SetNextWindowSize(size);
		ImGui.BeginChild("##ChartHeaderBG", size, ImGuiChildFlags.None, ChartAreaChildWindowFlags);
		ImGui.EndChild();

		ImGui.PopStyleColor(1);

		ImGui.GetStyle().WindowMinSize = originalMinWindowSize;
		ImGui.GetStyle().WindowBorderSize = originalWindowBorderSize;
	}

	/// <summary>
	/// Draw an individual chart header.
	/// This includes a button which may close the chart and affect the active chart list.
	/// </summary>
	public void Draw()
	{
		var editorChart = Chart.GetChart();
		var chartId = editorChart.GetId();
		Chart.GetEditor().GetChartAreaInScreenSpace(out var chartArea);
		var x = Chart.GetScreenSpaceXOfFullChartAreaStart();
		var w = Chart.GetScreenSpaceXOfFullChartAreaEnd() - x;
		var h = GetChartHeaderHeight();

		// Record window size and padding values so we can edit and restore them.
		var originalWindowPaddingY = ImGui.GetStyle().WindowPadding.Y;
		var originalMinWindowSize = ImGui.GetStyle().WindowMinSize;
		var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
		var originalInnerItemSpacingX = ImGui.GetStyle().ItemInnerSpacing.X;
		var originalFramePaddingX = ImGui.GetStyle().FramePadding.X;
		var originalSelectableTextAlignY = ImGui.GetStyle().SelectableTextAlign.Y;

		// Set the padding and spacing so we can draw a table with precise dimensions.
		ImGui.GetStyle().WindowPadding.Y = 1;
		ImGui.GetStyle().WindowMinSize = new Vector2(w, h);
		ImGui.GetStyle().ItemSpacing.X = ItemSpacing;
		ImGui.GetStyle().ItemInnerSpacing.X = 0;
		ImGui.GetStyle().FramePadding.X = 0;
		ImGui.GetStyle().SelectableTextAlign.Y = 0.25f;

		if (Chart.IsFocused())
		{
			ImGui.PushStyleColor(ImGuiCol.ChildBg,
				ColorRGBAMultiply(GetColorForDifficultyType(editorChart.ChartDifficultyType), UIFocusedChartColorMultiplier));
			ImGui.PushStyleColor(ImGuiCol.Border, UIFocusedTabBorderColor);
		}
		else
		{
			ImGui.PushStyleColor(ImGuiCol.ChildBg,
				ColorRGBAMultiply(GetColorForDifficultyType(editorChart.ChartDifficultyType), UIUnfocusedChartColorMultiplier));
			ImGui.PushStyleColor(ImGuiCol.Border, UIUnfocusedTabBorderColor);
		}

		var colorPushCount = 2;

		ImGui.SetNextWindowPos(new Vector2(x, chartArea.Y));
		ImGui.SetNextWindowSize(new Vector2(w, h));
		if (ImGui.BeginChild($"##ChartHeader{chartId}", new Vector2(w, h), ImGuiChildFlags.Border,
			    ChartAreaChildWindowFlags))
		{
			var buttonWidth = GetCloseWidth();
			var available = ImGui.GetContentRegionAvail().X + ImGui.GetStyle().WindowPadding.X;
			var textWidth = available - GetButtonAreaWidth();

			var useNonDedicatedTabColor = !Chart.HasDedicatedTab();

			// Title. Use a transparent Selectable to support single and double clicking.
			ImGui.SetNextItemWidth(textWidth);
			ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x00000000);
			ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x00000000);
			colorPushCount += 2;
			if (useNonDedicatedTabColor)
				ImGui.PushStyleColor(ImGuiCol.Text, UINonDedicatedTabTextColor);
			if (ImGui.Selectable(editorChart.GetDescriptiveName(), false, ImGuiSelectableFlags.AllowDoubleClick,
				    new Vector2(textWidth, h)))
			{
				if (!Editor.WasLastMouseUpUsedForMovingFocalPoint())
				{
					if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
					{
						Editor.SetChartHasDedicatedTab(editorChart, true);
					}
					else
					{
						Editor.SetChartFocused(editorChart);
					}
				}
			}

			if (useNonDedicatedTabColor)
				ImGui.PopStyleColor();

			// Fudge layout numbers for the buttons to get their text to look centered in Y.
			var originalFramePaddingY = ImGui.GetStyle().FramePadding.Y;
			var buttonHeight = h + originalFramePaddingY * 2;
			ImGui.GetStyle().FramePadding.Y = 0;
			var originalButtonTextAlign = ImGui.GetStyle().ButtonTextAlign.Y;
			ImGui.GetStyle().ButtonTextAlign.Y = 0.1f;

			// Left / Right buttons.
			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($"<##ChartHeader{chartId}", new Vector2(buttonWidth, buttonHeight)))
			{
				Editor.MoveActiveChartLeft(editorChart);
			}

			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($">##ChartHeader{chartId}", new Vector2(buttonWidth, buttonHeight)))
			{
				Editor.MoveActiveChartRight(editorChart);
			}

			// Close button.
			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($"X##ChartHeader{chartId}", new Vector2(buttonWidth, buttonHeight)))
				Editor.CloseChart(editorChart);

			ImGui.GetStyle().ButtonTextAlign.Y = originalButtonTextAlign;
			ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;
		}

		ImGui.EndChild();

		// Restore window size and padding values.
		ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
		ImGui.GetStyle().ItemInnerSpacing.X = originalInnerItemSpacingX;
		ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
		ImGui.GetStyle().WindowPadding.Y = originalWindowPaddingY;
		ImGui.GetStyle().WindowMinSize = originalMinWindowSize;
		ImGui.GetStyle().SelectableTextAlign.Y = originalSelectableTextAlignY;

		ImGui.PopStyleColor(colorPushCount);
	}

	private int GetButtonAreaWidth()
	{
		var buttonWidth = GetCloseWidth();
		return buttonWidth * NumButtons + ItemSpacing * (NumButtons - 1);
	}

	public bool IsOverDraggableArea(int screenSpaceX, int screenSpaceY)
	{
		if (!Chart.GetEditor().GetChartAreaInScreenSpace(out var draggableArea))
			return false;
		draggableArea.X = Chart.GetScreenSpaceXOfFullChartAreaStart();
		draggableArea.Width = Chart.GetScreenSpaceXOfFullChartAreaEnd() - draggableArea.X - GetButtonAreaWidth();
		draggableArea.Height = GetChartHeaderHeight();
		return draggableArea.Contains(screenSpaceX, screenSpaceY);
	}
}
