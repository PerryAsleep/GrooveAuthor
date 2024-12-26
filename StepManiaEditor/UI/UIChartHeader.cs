using ImGuiNET;
using Microsoft.Xna.Framework;
using StepManiaEditor;
using static StepManiaEditor.ImGuiUtils;
using Vector2 = System.Numerics.Vector2;

/// <summary>
/// Class for drawing headers above charts.
/// </summary>
internal sealed class UIChartHeader
{
	private readonly ActiveEditorChart Chart;
	private readonly Editor Editor;

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

		ImGui.PushStyleColor(ImGuiCol.ChildBg, Utils.UIWindowColor);
		
		var size = new Vector2(chartArea.Width, h);
		ImGui.GetStyle().WindowMinSize = size;
		ImGui.SetNextWindowPos(new Vector2(chartArea.X, chartArea.Y));
		ImGui.SetNextWindowSize(size);
		ImGui.BeginChild("##ChartHeaderBG", size, ImGuiChildFlags.None, Utils.ChartAreaChildWindowFlags);
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
		Chart.GetEditor().GetChartArea(out var chartArea);
		var x = Chart.GetScreenSpaceXOfFullChartAreaStart();
		var w = Chart.GetScreenSpaceXOfFullChartAreaEnd() - x;
		var h = GetChartHeaderHeight();

		var allCharts = editorChart.GetEditorSong().GetSortedCharts();
		var index = 0;
		for (var i = 0; i < allCharts.Count; i++)
		{
			if (allCharts[i] == editorChart)
			{
				index = i;
				break;
			}
		}

		// Record window size and padding values so we can edit and restore them.
		var originalWindowPaddingY = ImGui.GetStyle().WindowPadding.Y;
		var originalMinWindowSize = ImGui.GetStyle().WindowMinSize;
		var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
		var originalInnerItemSpacingX = ImGui.GetStyle().ItemInnerSpacing.X;
		var originalFramePaddingX = ImGui.GetStyle().FramePadding.X;

		// Set the padding and spacing so we can draw a table with precise dimensions.
		ImGui.GetStyle().WindowPadding.Y = 1;
		ImGui.GetStyle().WindowMinSize = new Vector2(w, h);
		ImGui.GetStyle().ItemSpacing.X = 0;
		ImGui.GetStyle().ItemInnerSpacing.X = 0;
		ImGui.GetStyle().FramePadding.X = 0;

		var color = Utils.GetColorForDifficultyType(editorChart.ChartDifficultyType);

		var colorPushCount = 0;
		var focused = Chart.IsFocused();
		if (focused)
		{
			ImGui.PushStyleColor(ImGuiCol.ChildBg, color);
			ImGui.PushStyleColor(ImGuiCol.Border, 0xFFFFFFFF);
			colorPushCount += 2;
		}

		ImGui.SetNextWindowPos(new Vector2(x, chartArea.Y));
		ImGui.SetNextWindowSize(new Vector2(w, h));
		if (ImGui.BeginChild($"##ChartHeader{index}", new Vector2(w, h), ImGuiChildFlags.Border, Utils.ChartAreaChildWindowFlags))
		{
			var buttonWidth = GetCloseWidth();
			var spacing = ImGui.GetStyle().ItemSpacing.X;
			var available = ImGui.GetContentRegionAvail().X + ImGui.GetStyle().WindowPadding.X;
			var textWidth = available - (buttonWidth * 3 + spacing * 2);

			// Title
			ImGui.SetNextItemWidth(textWidth);
			Text(editorChart.GetDescriptiveName(), textWidth);

			// Left / Right buttons
			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($"<##ChartHeader{index}", new Vector2(buttonWidth, h)))
			{
				// TODO: Left/Right chart nav.
			}
			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($">##ChartHeader{index}", new Vector2(buttonWidth, 0.0f)))
			{
				// TODO: Left/Right chart nav.
			}

			// Close button.
			ImGui.SameLine();
			ImGui.SetNextItemWidth(buttonWidth);
			if (ImGui.Button($"X##ChartHeader{index}", new Vector2(buttonWidth, 0.0f)))
				Editor.CloseChart(editorChart);
		}

		ImGui.EndChild();

		// Restore window size and padding values.
		ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
		ImGui.GetStyle().ItemInnerSpacing.X = originalInnerItemSpacingX;
		ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
		ImGui.GetStyle().WindowPadding.Y = originalWindowPaddingY;
		ImGui.GetStyle().WindowMinSize = originalMinWindowSize;

		ImGui.PopStyleColor(colorPushCount);
	}
}
