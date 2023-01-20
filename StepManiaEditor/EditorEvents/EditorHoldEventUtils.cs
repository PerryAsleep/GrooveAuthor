using Fumen.ChartDefinition;
using static StepManiaEditor.EditorEvent;

namespace StepManiaEditor
{
	internal sealed class EditorHoldEventUtils
	{
		public static (EditorHoldStartNoteEvent, EditorHoldEndNoteEvent) CreateHold(EditorChart chart, int lane, int row, int length, bool roll)
		{
			EditorHoldStartNoteEvent holdStart;
			EditorHoldEndNoteEvent holdEnd;

			var holdStartTime = 0.0;
			chart.TryGetTimeFromChartPosition(row, ref holdStartTime);
			var holdStartNote = new LaneHoldStartNote()
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = holdStartTime
			};
			var config = new EventConfig
			{
				EditorChart = chart,
				ChartEvent = holdStartNote,
			};
			holdStart = new EditorHoldStartNoteEvent(config, holdStartNote);
			holdStart.SetIsRoll(roll);

			var holdEndTime = 0.0;
			chart.TryGetTimeFromChartPosition(row + length, ref holdEndTime);
			var holdEndNote = new LaneHoldEndNote()
			{
				Lane = lane,
				IntegerPosition = row + length,
				TimeSeconds = holdEndTime
			};
			config = new EventConfig
			{
				EditorChart = chart,
				ChartEvent = holdEndNote,
			};
			holdEnd = new EditorHoldEndNoteEvent(config, holdEndNote);

			holdStart.SetHoldEndNote(holdEnd);
			holdEnd.SetHoldStartNote(holdStart);

			return (holdStart, holdEnd);
		}
	}
}
