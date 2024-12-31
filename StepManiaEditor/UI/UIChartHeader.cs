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

	private const int NumNavButtons = 3;
	private static readonly int ItemSpacing = UiScaled(0);
	private static readonly int KeepTabOpenButtonSpacing = UiScaled(28);
	private const uint ButtonColor = 0xAAFA9642;

	private bool DraggableAreaHovered;
	private float OriginalFramePaddingY;
	private float OriginalButtonTextAlign;
	private float ButtonHeight;

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
		var chartId = editorChart.GetIndexInSong();
		Chart.GetEditor().GetChartAreaInScreenSpace(out var chartArea);
		var x = Chart.GetScreenSpaceXOfFullChartAreaStart();
		var w = Chart.GetChartScreenSpaceWidth();
		var h = GetChartHeaderHeight();
		var buttonAreaWidth = GetButtonAreaWidth();

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

		var originalWindowPadding = ImGui.GetStyle().WindowPadding.X;
		var nonDedicatedTab = !Chart.HasDedicatedTab();
		if (nonDedicatedTab)
			ImGui.GetStyle().WindowPadding.X = 0;

		ImGui.SetNextWindowPos(new Vector2(x, chartArea.Y));
		ImGui.SetNextWindowSize(new Vector2(w, h));
		if (ImGui.BeginChild($"##ChartHeader{chartId}", new Vector2(w, h), ImGuiChildFlags.Border,
			    ChartAreaChildWindowFlags))
		{
			var buttonWidth = GetCloseWidth();
			var available = ImGui.GetContentRegionAvail().X + ImGui.GetStyle().WindowPadding.X;
			var textWidth = available - buttonAreaWidth;

			// Pin button.
			if (nonDedicatedTab)
			{
				ImGui.GetStyle().WindowPadding.X = 0;
				PushButtonStyle();
				ImGui.SetNextItemWidth(KeepTabOpenButtonSpacing);
				if (ImGui.Button($"Pin##ChartHeader{chartId}", new Vector2(KeepTabOpenButtonSpacing, ButtonHeight)))
				{
					Editor.SetChartFocused(editorChart);
					Editor.SetChartHasDedicatedTab(editorChart, true);
				}

				PopButtonStyle();
				ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
				ImGui.SameLine();
			}

			// Title. Use a transparent Selectable to support single and double clicking.
			ImGui.SetNextItemWidth(textWidth);
			ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x00000000);
			ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x00000000);
			colorPushCount += 2;
			if (nonDedicatedTab)
				ImGui.PushStyleColor(ImGuiCol.Text, UINonDedicatedTabTextColor);

			ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(nonDedicatedTab ? 0.0f : 0.5f, 0.25f));
			if (ImGui.Selectable(editorChart.GetDescriptiveName(), false, ImGuiSelectableFlags.AllowDoubleClick,
				    new Vector2(textWidth, h)))
			{
				if (!Editor.WasLastMouseUpUsedForMovingFocalPoint())
				{
					if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
					{
						Editor.SetChartFocused(editorChart);
						Editor.SetChartHasDedicatedTab(editorChart, true);
					}
					else
					{
						Editor.SetChartFocused(editorChart);
					}
				}
			}

			ImGui.PopStyleVar();
			ImGui.GetStyle().ItemSpacing.X = ItemSpacing;

			DraggableAreaHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.None);

			if (nonDedicatedTab)
				ImGui.PopStyleColor();

			PushButtonStyle();

			// Left / Right buttons.
			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($"<##ChartHeader{chartId}", new Vector2(buttonWidth, ButtonHeight)))
			{
				Editor.MoveActiveChartLeft(editorChart);
			}

			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($">##ChartHeader{chartId}", new Vector2(buttonWidth, ButtonHeight)))
			{
				Editor.MoveActiveChartRight(editorChart);
			}

			// Close button.
			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($"X##ChartHeader{chartId}", new Vector2(buttonWidth, ButtonHeight)))
				Editor.CloseChart(editorChart);

			PopButtonStyle();
		}

		ImGui.EndChild();

		// Restore window size and padding values.
		ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
		ImGui.GetStyle().ItemInnerSpacing.X = originalInnerItemSpacingX;
		ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
		ImGui.GetStyle().WindowPadding.Y = originalWindowPaddingY;
		ImGui.GetStyle().WindowMinSize = originalMinWindowSize;
		ImGui.GetStyle().SelectableTextAlign.Y = originalSelectableTextAlignY;
		ImGui.GetStyle().WindowPadding.X = originalWindowPadding;

		ImGui.PopStyleColor(colorPushCount);
	}

	private void PushButtonStyle()
	{
		// Fudge layout numbers for the buttons to get their text to look centered in Y.
		var h = GetChartHeaderHeight();
		OriginalFramePaddingY = ImGui.GetStyle().FramePadding.Y;
		ButtonHeight = h + OriginalFramePaddingY * 2;
		ImGui.GetStyle().FramePadding.Y = 0;
		OriginalButtonTextAlign = ImGui.GetStyle().ButtonTextAlign.Y;
		ImGui.GetStyle().ButtonTextAlign.Y = 0.1f;
		ImGui.PushStyleColor(ImGuiCol.Button, ButtonColor);
	}

	private void PopButtonStyle()
	{
		ImGui.GetStyle().ButtonTextAlign.Y = OriginalButtonTextAlign;
		ImGui.GetStyle().FramePadding.Y = OriginalFramePaddingY;
		ImGui.PopStyleColor();
	}

	private int GetButtonAreaWidth()
	{
		var buttonWidth = GetCloseWidth();
		var width = buttonWidth * NumNavButtons + ItemSpacing * (NumNavButtons - 1);
		if (!Chart.HasDedicatedTab())
			width += (int)ImGui.GetStyle().ItemSpacing.X + KeepTabOpenButtonSpacing;
		return width;
	}

	public bool IsDraggableAreaHovered()
	{
		return DraggableAreaHovered;
	}
}
