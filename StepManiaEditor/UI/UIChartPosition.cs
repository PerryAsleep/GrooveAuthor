using System;
using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static Fumen.FumenExtensions;
using static StepManiaEditor.Editor;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing information about the current Position within the current Chart.
/// </summary>
internal sealed class UIChartPosition
{
	private const uint ColorTextWhite = 0xFFFFFFFF;
	private const uint ColorTextGrey = 0xFF777777;
	private const uint ColorBgDarkGrey = 0xF0222222;

	public static readonly int Width = UiScaled(800);
	public static readonly int Height = UiScaled(63);
	public static readonly int HalfWidth = Width / 2;
	public static readonly int HalfHeight = Height / 2;
	private static readonly int MiscTableTitleColumnWidth = UiScaled(40);
	private static readonly int MiscTableSnapWidth = UiScaled(32);
	private static readonly int TableNameColWidth = UiScaled(37);
	private static readonly int TablePositionColWidth = UiScaled(73);
	private static readonly int TableSongTimeColWidth = UiScaled(104);
	private static readonly int TableChartTimeColWidth = UiScaled(104);
	private static readonly int TableMeasureColWidth = UiScaled(61);
	private static readonly int TableBeatColWidth = UiScaled(67);
	private static readonly int TableRowColWidth = UiScaled(78);
	private static readonly int TableWidth = UiScaled(588);
	private static readonly int TableHeight = UiScaled(46);

	private readonly Editor Editor;

	public UIChartPosition(Editor editor)
	{
		Editor = editor;
	}

	/// <summary>
	/// Draws the chart position information.
	/// </summary>
	/// <param name="x">Desired X position of the center of the window.</param>
	/// <param name="y">Desired Y position of the center of the window.</param>
	/// <param name="snapData">SnapData.</param>
	public void Draw(int x, int y, SnapData snapData)
	{
		ImGui.SetNextWindowPos(new Vector2(x - HalfWidth, y - HalfHeight));
		ImGui.SetNextWindowSize(new Vector2(Width, Height));

		// Remove the cell and frame padding.
		var originalCellPaddingY = ImGui.GetStyle().CellPadding.Y;
		var originalFramePaddingY = ImGui.GetStyle().FramePadding.Y;
		ImGui.GetStyle().CellPadding.Y = 0;
		ImGui.GetStyle().FramePadding.Y = 0;

		if (ImGui.Begin("UIChartPosition",
			    ImGuiWindowFlags.NoMove
			    | ImGuiWindowFlags.NoDecoration
			    | ImGuiWindowFlags.NoSavedSettings
			    | ImGuiWindowFlags.NoDocking
			    | ImGuiWindowFlags.NoBringToFrontOnFocus
			    | ImGuiWindowFlags.NoFocusOnAppearing))
		{
			// Draw the left table with the spacing, zoom, and snap display.
			var preTableWidth = Width - TableWidth - ImGui.GetStyle().WindowPadding.X * 2 - ImGui.GetStyle().ItemSpacing.X;
			if (ImGuiLayoutUtils.BeginTable("UIChartMiscTable", MiscTableTitleColumnWidth,
				    preTableWidth - MiscTableTitleColumnWidth))
			{
				// Spacing mode.
				UIScrollPreferences.DrawSpacingModeRow("Spacing");

				// Zoom level.
				// TODO: Use a double for this control.
				// ImGUI.NET does not currently support passing ImGuiSliderFlags to double controls.
				// Casting to a float to allow use of ImGuiSliderFlags.Logarithmic.
				var zoom = (float)Editor.GetSpacingZoom();
				var originalZoom = zoom;
				ImGuiLayoutUtils.DrawRowDragFloat("Zoom", ref zoom,
					"Chart zoom level." +
					"\nCtrl+Scroll while over the chart changes the zoom level." +
					"\nShift+Scroll while over the chart changes how the notes are spaced for the current Spacing mode.",
					100.0f, "%.6f", (float)ZoomManager.MinZoom,
					(float)ZoomManager.MaxZoom, ImGuiSliderFlags.Logarithmic);
				if (!zoom.FloatEquals(originalZoom))
				{
					Editor.SetSpacingZoom(zoom);
				}

				// Snap level.
				ImGuiLayoutUtils.DrawRowTitleAndAdvanceColumn("Snap");
				ImGuiLayoutUtils.DrawHelp(
					"Snap:     Current note type being snapped to." +
					"\n          Use the left and right arrow keys to change the snap." +
					"\nAutomove: If checked, when entering a new note the editor position will automatically" +
					"\n          advance by the current Snap level." +
					"\n          Automove can also be toggled with the M key.",
					ImGui.GetContentRegionAvail().X);
				if (snapData.Rows == 0)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, ColorTextWhite);
					Text("None", MiscTableSnapWidth);
				}
				else
				{
					ImGui.PushStyleColor(ImGuiCol.Text,
						ArrowGraphicManager.GetArrowColorForSubdivision(SMCommon.MaxValidDenominator / snapData.Rows));
					Text($"1/{SMCommon.MaxValidDenominator / snapData.Rows * SMCommon.NumBeatsPerMeasure}", MiscTableSnapWidth);
				}

				ImGui.PopStyleColor();

				// Checkbox for NoteEntryMode.
				ImGui.SetNextItemWidth(ImGuiLayoutUtils.CheckBoxWidth);
				ImGui.SameLine();
				var autoAdvance = Preferences.Instance.NoteEntryMode == NoteEntryMode.AdvanceBySnap;
				ImGui.Checkbox("", ref autoAdvance);
				Preferences.Instance.NoteEntryMode = autoAdvance ? NoteEntryMode.AdvanceBySnap : NoteEntryMode.Normal;

				// Text for NoteEntryMode.
				ImGui.SameLine();
				ImGui.Text("Automove");

				ImGuiLayoutUtils.EndTable();
			}

			// Draw the right table with position information.
			ImGui.SameLine();
			DrawPositionTable();
		}

		ImGui.End();

		// Restore the cell and frame padding.
		ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;
		ImGui.GetStyle().CellPadding.Y = originalCellPaddingY;
	}

	private void DrawPositionTable()
	{
		var originalInnerItemSpacingX = ImGui.GetStyle().ItemInnerSpacing.X;
		var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
		ImGui.GetStyle().ItemInnerSpacing.X = 0;
		ImGui.GetStyle().ItemSpacing.X = 0;

		if (ImGui.BeginTable("UIChartPositionTable", 7,
			    ImGuiTableFlags.Borders, new Vector2(TableWidth, TableHeight)))
		{
			ImGui.TableSetupColumn("UIChartPositionTableC1", ImGuiTableColumnFlags.WidthFixed, TableNameColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC2", ImGuiTableColumnFlags.WidthFixed, TablePositionColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC3", ImGuiTableColumnFlags.WidthFixed, TableSongTimeColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC4", ImGuiTableColumnFlags.WidthFixed, TableChartTimeColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC5", ImGuiTableColumnFlags.WidthFixed, TableMeasureColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC6", ImGuiTableColumnFlags.WidthFixed, TableBeatColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC7", ImGuiTableColumnFlags.WidthFixed, TableRowColWidth);

			// Header
			DrawPositionTableHeader();

			// Chart Position
			DrawPositionTableRow("Chart", Editor.GetFocalPointX(), Editor.GetFocalPointY(), Editor.GetPosition());

			// Cursor Position
			var mouseState = Editor.GetMouseState();
			DrawPositionTableRow("Cursor", mouseState.X(), mouseState.Y(), mouseState.GetEditorPosition());

			ImGui.EndTable();
		}

		ImGui.GetStyle().ItemInnerSpacing.X = originalInnerItemSpacingX;
		ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
	}

	private static void DrawPositionTableHeader()
	{
		ImGui.TableNextRow();

		var colIndex = 0;
		DrawPositionTableHeaderCell(TableNameColWidth, "", ref colIndex);
		DrawPositionTableHeaderCell(TablePositionColWidth, "Position", ref colIndex);
		DrawPositionTableHeaderCell(TableSongTimeColWidth, "Song Time", ref colIndex);
		DrawPositionTableHeaderCell(TableChartTimeColWidth, "Chart Time", ref colIndex);
		DrawPositionTableHeaderCell(TableMeasureColWidth, "Measure", ref colIndex);
		DrawPositionTableHeaderCell(TableBeatColWidth, "Beat", ref colIndex);
		DrawPositionTableHeaderCell(TableRowColWidth, "Row", ref colIndex);
	}

	private static void DrawPositionTableHeaderCell(int width, string text, ref int index)
	{
		ImGui.TableSetColumnIndex(index++);
		if (index > 1)
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ColorBgDarkGrey);

		var textWidth = ImGui.CalcTextSize(text).X;
		if (width - textWidth > 0)
		{
			ImGui.Dummy(new Vector2(width - textWidth, 1));
			ImGui.SameLine();
		}

		ImGui.Text(text);
	}

	private static void DrawPositionTableRow(string label, int x, int y, EditorPosition position)
	{
		ImGui.TableNextRow();
		var colIndex = 0;

		ImGui.TableSetColumnIndex(colIndex++);
		ImGui.Text(label);

		DrawPositionTableCell(TablePositionColWidth, $"({x}, {y})", ref colIndex);
		DrawPositionTableCell(TableSongTimeColWidth, FormatTime(position.SongTime), ref colIndex);
		DrawPositionTableCell(TableChartTimeColWidth, FormatTime(position.ChartTime), ref colIndex);
		DrawPositionTableCell(TableMeasureColWidth, FormatDouble(GetMeasure(position)), ref colIndex);
		DrawPositionTableCell(TableBeatColWidth, FormatDouble(position.ChartPosition / SMCommon.MaxValidDenominator),
			ref colIndex);
		DrawPositionTableCell(TableRowColWidth, FormatDouble(position.ChartPosition), ref colIndex);
	}

	private static void DrawPositionTableCell(int width, string text, ref int index)
	{
		ImGui.TableSetColumnIndex(index++);

		var textWidth = ImGui.CalcTextSize(text).X;
		if (width - textWidth > 0)
		{
			ImGui.Dummy(new Vector2(width - textWidth, 1));
			ImGui.SameLine();
		}

		var delimiters = new[] { '.', ':' };
		var delimiterIndex = text.LastIndexOfAny(delimiters);
		if (delimiterIndex < 0)
		{
			ImGui.Text(text);
		}
		else
		{
			var firstText = text.Substring(0, delimiterIndex);
			var secondText = text.Substring(delimiterIndex);

			ImGui.Text(firstText);
			ImGui.SameLine();

			ImGui.PushStyleColor(ImGuiCol.Text, ColorTextGrey);
			ImGui.Text(secondText);
			ImGui.PopStyleColor();
		}
	}

	private static double GetMeasure(EditorPosition position)
	{
		var rateEvent = position.ActiveChart?.FindActiveRateAlteringEventForPosition(position.ChartPosition);
		if (rateEvent == null)
			return 0.0;
		var timeSigEvent = rateEvent.GetTimeSignature();
		var rowDifference = position.ChartPosition - timeSigEvent.IntegerPosition;
		var rowsPerMeasure = timeSigEvent.Signature.Numerator *
		                     (SMCommon.MaxValidDenominator * SMCommon.NumBeatsPerMeasure / timeSigEvent.Signature.Denominator);
		var measures = rowDifference / rowsPerMeasure;
		measures += timeSigEvent.MetricPosition.Measure;
		return measures;
	}

	private static string FormatDouble(double value)
	{
		if (double.IsNegativeInfinity(value))
		{
			return "-Infinity";
		}

		if (double.IsPositiveInfinity(value))
		{
			return "Infinity";
		}

		if (double.IsNaN(value))
		{
			return "NaN";
		}

		return $"{value:N3}";
	}

	private static string FormatTime(double seconds)
	{
		const string formatMinutes = @"mm\:ss\:ffffff";
		const string formatHours = @"hh\:mm\:ss\:ffffff";
		const string formatDays = @"d\.hh\:mm\:ss\:ffffff";

		if (seconds < TimeSpan.MinValue.TotalSeconds || double.IsNegativeInfinity(seconds))
		{
			return "-Infinity";
		}

		if (seconds > TimeSpan.MaxValue.TotalSeconds || double.IsPositiveInfinity(seconds))
		{
			return "Infinity";
		}

		if (double.IsNaN(seconds))
		{
			return "NaN";
		}

		var formatString = formatMinutes;
		var abs = Math.Abs(seconds);
		if (abs >= 86400.0)
		{
			formatString = formatDays;
		}
		else if (abs >= 3600.0)
		{
			formatString = formatHours;
		}

		var str = TimeSpan.FromSeconds(seconds).ToString(formatString);

		if (seconds < 0.0)
		{
			return "-" + str;
		}

		return str;
	}
}
