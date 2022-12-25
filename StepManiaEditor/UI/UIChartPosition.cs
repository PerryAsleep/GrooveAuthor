using System;
using System.Numerics;
using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing information about the current Position within the current Chart.
	/// </summary>
	internal sealed class UIChartPosition
	{
		private const uint ColorTextWhite = 0xFFFFFFFF;
		private const uint ColorTextGrey = 0xFF777777;
		private const uint ColorBgDarkGrey = 0xF0222222;

		public const int Width = 800;
		public const int Height = 63;

		private const int TableNameColWidth = 37;
		private const int TablePositionColWidth = 73;
		private const int TableSongTimeColWidth = 104;
		private const int TableChartTimeColWidth = 104;
		private const int TableMeasureColWidth = 61;
		private const int TableBeatColWidth = 67;
		private const int TableRowColWidth = 78;
		private const int TableWidth = 588;
		private const int TableHeight = 46;

		private Editor Editor;
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
		public void Draw(int x, int y, Editor.SnapData snapData)
		{
			ImGui.SetNextWindowPos(new Vector2(x - Width / 2, y - Height / 2));
			ImGui.SetNextWindowSize(new Vector2(Width, Height));

			// Remove the cell and frame padding.
			var originalCellPaddingY = ImGui.GetStyle().CellPadding.Y;
			var originalFramePaddingY = ImGui.GetStyle().FramePadding.Y;
			ImGui.GetStyle().CellPadding.Y = 0;
			ImGui.GetStyle().FramePadding.Y = 0;

			ImGui.Begin("UIChartPosition",
				ImGuiWindowFlags.NoMove
				| ImGuiWindowFlags.NoDecoration
				| ImGuiWindowFlags.NoSavedSettings
				| ImGuiWindowFlags.NoDocking
				| ImGuiWindowFlags.NoBringToFrontOnFocus
				| ImGuiWindowFlags.NoFocusOnAppearing);

			// Draw the left table with the spacing, zoom, and snap display.
			var preTableWidth = Width - TableWidth - ImGui.GetStyle().WindowPadding.X * 2 - ImGui.GetStyle().ItemSpacing.X;
			if (ImGuiLayoutUtils.BeginTable("UIChartMiscTable", 40, preTableWidth - 40))
			{
				// Spacing mode.
				UIScrollPreferences.DrawSpacingModeRow("Spacing");

				// Zoom level.
				// TODO: Use a double for this control.
				// ImGUI.NET does not currently support passing ImGuiSliderFlags to double controls.
				// Casting to a float to allow use of ImGuiSliderFlags.Logarithmic.
				var zoom = (float)Editor.GetSpacingZoom();
				var originalZoom = zoom;
				ImGuiLayoutUtils.DrawRowDragFloat("Zoom", ref zoom, "Chart zoom level.", 100.0f, "%.6f", (float)Editor.MinZoom, (float)Editor.MaxZoom, ImGuiSliderFlags.Logarithmic);
				if (!zoom.FloatEquals(originalZoom))
				{
					Editor.SetZoom(zoom, true);
				}

				// Snap level.
				ImGuiLayoutUtils.DrawRowTitleAndAdvanceColumn("Snap");
				ImGui.SetNextItemWidth(ImGuiLayoutUtils.DrawHelp("Current note type being snapped to.\nUse the left and right arrow keys to change the snap.", ImGui.GetContentRegionAvail().X));
				if (snapData.Rows == 0)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, ColorTextWhite);
					ImGui.Text("None");
				}
				else
				{
					ImGui.PushStyleColor(ImGuiCol.Text, ArrowGraphicManager.GetArrowColorRGBAForSubdivision(SMCommon.MaxValidDenominator / snapData.Rows));
					ImGui.Text($"1/{(SMCommon.MaxValidDenominator / snapData.Rows) * SMCommon.NumBeatsPerMeasure}");
				}
				ImGui.PopStyleColor();

				ImGuiLayoutUtils.EndTable();
			}
			
			// Draw the right rable with position information.
			ImGui.SameLine();
			DrawPositionTable();

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
			int colIndex = 0;
			
			ImGui.TableSetColumnIndex(colIndex++);
			ImGui.Text(label);

			DrawPositionTableCell(TablePositionColWidth, $"({x}, {y})", ref colIndex);
			DrawPositionTableCell(TableSongTimeColWidth, FormatTime(position.SongTime), ref colIndex);
			DrawPositionTableCell(TableChartTimeColWidth, FormatTime(position.ChartTime), ref colIndex);
			DrawPositionTableCell(TableMeasureColWidth, FormatDouble(GetMeasure(position)), ref colIndex);
			DrawPositionTableCell(TableBeatColWidth, FormatDouble(position.ChartPosition / SMCommon.MaxValidDenominator), ref colIndex);
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

			var delimiters = new char[] { '.', ':' };
			var delimIndex = text.LastIndexOfAny(delimiters);
			if (delimIndex < 0)
			{
				ImGui.Text(text);
			}
			else
			{
				var firstText = text.Substring(0, delimIndex);
				var secondText = text.Substring(delimIndex);

				ImGui.Text(firstText);
				ImGui.SameLine();

				ImGui.PushStyleColor(ImGuiCol.Text, ColorTextGrey);
				ImGui.Text(secondText);
				ImGui.PopStyleColor();
			}
		}

		private static double GetMeasure(EditorPosition position)
		{
			var rateEvent = position.ActiveChart?.GetActiveRateAlteringEventForPosition(position.ChartPosition);
			if (rateEvent == null)
				return 0.0;
			var timeSigEvent = rateEvent.LastTimeSignature;
			var rowDifference = position.ChartPosition - timeSigEvent.IntegerPosition;
			var rowsPerMeasure = timeSigEvent.Signature.Numerator * (SMCommon.MaxValidDenominator * SMCommon.NumBeatsPerMeasure / timeSigEvent.Signature.Denominator);
			var measures = rowDifference / rowsPerMeasure;
			measures += timeSigEvent.MetricPosition.Measure;
			return measures;
		}

		private static string FormatDouble(double value)
		{
			if (value == double.NegativeInfinity)
			{
				return "-Infinity";
			}
			if (value == double.PositiveInfinity)
			{
				return "Infinity";
			}
			if (value == double.NaN)
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

			if (seconds < TimeSpan.MinValue.TotalSeconds || seconds == double.NegativeInfinity)
			{
				return "-Infinity";
			}
			if (seconds > TimeSpan.MaxValue.TotalSeconds || seconds == double.PositiveInfinity)
			{
				return "Infinity";
			}
			if (seconds == double.NaN)
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
}
