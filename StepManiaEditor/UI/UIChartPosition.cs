using System;
using System.Numerics;
using System.Text;
using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing information about the current Position within the current Chart.
	/// </summary>
	class UIChartPosition
	{
		private struct TextSegment
		{
			public string Text;
			public uint Color;
		}

		private const uint White = 0xFFFFFFFF;
		private const uint Grey = 0xFF777777;

		public const int Width = 800;
		public const int Height = 32;

		private static TextSegment[] Segments;
		private static readonly int SnapValueIndex;
		private static readonly int SongTimeValueIndex;
		private static readonly int ChartTimeValueIndex;
		private static readonly int MeasureValueIndex;
		private static readonly int BeatValueIndex;
		private static readonly int RowValueIndex;

		static UIChartPosition()
		{
			Segments = new TextSegment[12];
			var i = 0;
			Segments[i++] = new TextSegment() { Color = Grey, Text = "Snap: " };
			SnapValueIndex = i;
			Segments[i++] = new TextSegment() { Color = White, Text = "None " };
			Segments[i++] = new TextSegment() { Color = Grey, Text = "Song Time: " };
			SongTimeValueIndex = i;
			Segments[i++] = new TextSegment() { Color = White, Text = "0.0 " };
			Segments[i++] = new TextSegment() { Color = Grey, Text = "Chart Time: " };
			ChartTimeValueIndex = i;
			Segments[i++] = new TextSegment() { Color = White, Text = "0.0 " };
			Segments[i++] = new TextSegment() { Color = Grey, Text = "Measure: " };
			MeasureValueIndex = i;
			Segments[i++] = new TextSegment() { Color = White, Text = "0.0 " };
			Segments[i++] = new TextSegment() { Color = Grey, Text = "Beat: " };
			BeatValueIndex = i;
			Segments[i++] = new TextSegment() { Color = White, Text = "0.0 " };
			Segments[i++] = new TextSegment() { Color = Grey, Text = "Row: " };
			RowValueIndex = i;
			Segments[i++] = new TextSegment() { Color = White, Text = "0 " };
		}

		/// <summary>
		/// Draws the chart position information.
		/// </summary>
		/// <param name="x">Desired X position of the center of the window.</param>
		/// <param name="y">Desired Y position of the center of the window.</param>
		/// <param name="position">EditorPosition.</param>
		/// <param name="snapData">SnapData.</param>
		/// <param name="arrowGraphicManager">ArrowGraphicManager to use for coloring position information.</param>
		public static void Draw(int x, int y, EditorPosition position, Editor.SnapData snapData, ArrowGraphicManager arrowGraphicManager)
		{
			ImGui.SetNextWindowPos(new Vector2(x - Width / 2, y - Height / 2));
			ImGui.SetNextWindowSize(new Vector2(Width, Height));

			var originalWindowPaddingX = ImGui.GetStyle().WindowPadding.X;
			var originalInnerItemSpacingX = ImGui.GetStyle().ItemInnerSpacing.X;
			var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
			var originalFramePaddingX = ImGui.GetStyle().FramePadding.X;

			// Set the padding and spacing so we can draw a table with precise dimensions.
			ImGui.GetStyle().WindowPadding.X = 0;
			ImGui.GetStyle().ItemInnerSpacing.X = 0;
			ImGui.GetStyle().ItemSpacing.X = 0;
			ImGui.GetStyle().FramePadding.X = 0;

			ImGui.Begin("UIChartPosition",
				ImGuiWindowFlags.NoMove
				| ImGuiWindowFlags.NoDecoration
				| ImGuiWindowFlags.NoSavedSettings
				| ImGuiWindowFlags.NoDocking
				| ImGuiWindowFlags.NoBringToFrontOnFocus
				| ImGuiWindowFlags.NoFocusOnAppearing);

			// Update the text values.
			if (snapData.Rows == 0)
			{
				Segments[SnapValueIndex].Color = White;
				Segments[SnapValueIndex].Text = "None ";
			}
			else
			{
				Segments[SnapValueIndex].Color = arrowGraphicManager?.GetArrowColorABGRForSubdivision(SMCommon.MaxValidDenominator / snapData.Rows) ?? White;
				Segments[SnapValueIndex].Text = $"1/{(SMCommon.MaxValidDenominator / snapData.Rows) * SMCommon.NumBeatsPerMeasure} ";
			}
			Segments[SongTimeValueIndex].Text = $"{FormatTime(position.SongTime)} ";
			Segments[ChartTimeValueIndex].Text = $"{FormatTime(position.ChartTime)} ";
			Segments[MeasureValueIndex].Text = $"{GetMeasure(position):N3} ";
			Segments[BeatValueIndex].Text = $"{(position.ChartPosition / SMCommon.MaxValidDenominator):N3} ";
			Segments[RowValueIndex].Text = $"{position.ChartPosition:N3} ";

			// Compute total width and create a dummy element to center all the text.
			var sb = new StringBuilder();
			foreach (var segment in Segments)
			{
				sb.Append(segment.Text);
			}
			var textWidth = ImGui.CalcTextSize(sb.ToString()).X;
			ImGui.Dummy(new Vector2((Width - textWidth) * 0.5f, 1));
			ImGui.SameLine();

			// Draw each segment.
			foreach (var segment in Segments)
			{
				ImGui.PushStyleColor(ImGuiCol.Text, segment.Color);
				ImGui.Text(segment.Text);
				ImGui.SameLine();
				ImGui.PopStyleColor();
			}

			ImGui.End();

			// Restore the padding and spacing values.
			ImGui.GetStyle().WindowPadding.X = originalWindowPaddingX;
			ImGui.GetStyle().ItemInnerSpacing.X = originalInnerItemSpacingX;
			ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
			ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
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

		private static string FormatTime(double seconds)
		{
			const string formatMinutes = @"mm\:ss\:ffffff";
			const string formatHours = @"hh\:mm\:ss\:ffffff";
			const string formatDays = @"d\.hh\:mm\:ss\:ffffff";

			var formatString = formatMinutes;
			if (seconds >= 86400.0)
			{
				formatString = formatDays;
			}
			else if (seconds >= 3600.0)
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
