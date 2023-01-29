using Fumen;
using Fumen.ChartDefinition;
using System.Collections.Generic;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor
{
	/// <summary>
	/// Configuration class for constructing a new EditorEvent.
	/// Encapsulates creation of raw Stepmania Events.
	/// Encapsulates the need to use Lists to  hold potentially multiple Events.
	/// </summary>
	internal sealed class EventConfig
	{
		public readonly EditorChart EditorChart;
		public readonly List<Event> ChartEvents;
		public readonly bool UseDoubleChartPosition;
		public readonly double ChartPosition;
		public readonly bool IsDummyEvent;
		public bool IsBeingEdited;

		private EventConfig(
			EditorChart editorChart,
			List<Event> chartEvents = null,
			bool useDoubleChartPosition = false,
			double chartPosition = 0.0,
			bool isDummyEvent = false,
			bool isBeingEdited = false)
		{
			EditorChart = editorChart;
			ChartEvents = chartEvents;
			UseDoubleChartPosition = useDoubleChartPosition;
			ChartPosition = chartPosition;
			IsDummyEvent = isDummyEvent;
			IsBeingEdited = isBeingEdited;
		}

		public static EventConfig CreateCloneEventConfig(EditorEvent editorEvent)
		{
			var clonedEvents = new List<Event>();
			foreach (var originalEvent in editorEvent.GetEvents())
				clonedEvents.Add(originalEvent.Clone());

			return new EventConfig(
				editorEvent.GetEditorChart(),
				clonedEvents,
				false,
				editorEvent.GetChartPosition(),
				editorEvent.IsDummyEvent(),
				editorEvent.IsBeingEdited());
		}

		public static EventConfig CreateConfig(EditorChart chart, Event chartEvent)
		{
			return new EventConfig(chart, new List<Event> { chartEvent });
		}

		public static EventConfig CreateConfigNoEvent(EditorChart chart, double chartPosition)
		{
			return new EventConfig(chart, null, true, chartPosition);
		}

		public static EventConfig CreateHoldConfig(EditorChart chart, LaneHoldStartNote start, LaneHoldEndNote end)
		{
			return new EventConfig(chart, new List<Event> { start, end } );
		}

		public static EventConfig CreateTapConfig(
			EditorChart chart, double chartPosition, double chartTime, int lane)
		{
			return new EventConfig(chart, new List<Event> { new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = (int)chartPosition,
				TimeSeconds = chartTime,
			} }, true, chartPosition);
		}

		public static EventConfig CreateTapConfig(
			EditorChart chart, int row, double chartTime, int lane)
		{
			return new EventConfig(chart, new List<Event> { new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateMineConfig(
			EditorChart chart, double chartPosition, double chartTime, int lane)
		{
			return new EventConfig(chart, new List<Event> { new LaneNote
			{
				Lane = lane,
				IntegerPosition = (int)chartPosition,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Mine].ToString(),
			} }, true, chartPosition);
		}

		public static EventConfig CreateMineConfig(
			EditorChart chart, int row, double chartTime, int lane)
		{
			return new EventConfig(chart, new List<Event> { new LaneNote
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Mine].ToString(),
			} }, false, row);
		}

		public static EventConfig CreateStopConfig(
			EditorChart chart, int row, double chartTime, double stopTime)
		{
			return new EventConfig(chart, new List<Event> { new Stop(stopTime, false)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateDelayConfig(
			EditorChart chart, int row, double chartTime, double stopTime)
		{
			return new EventConfig(chart, new List<Event> { new Stop(stopTime, true)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateWarpConfig(
			EditorChart chart, int row, double chartTime, int warpLength = MaxValidDenominator)
		{
			return new EventConfig(chart, new List<Event> { new Warp(warpLength)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateFakeConfig(
			EditorChart chart, int row, double chartTime, double fakeLength)
		{
			return new EventConfig(chart, new List<Event> { new FakeSegment(fakeLength)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateTickCountConfig(
			EditorChart chart, int row, double chartTime, int ticks = EditorChart.DefaultTickCount)
		{
			return new EventConfig(chart, new List<Event> { new TickCount(ticks)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateMultipliersConfig(
			EditorChart chart, int row, double chartTime, int hitMultiplier = EditorChart.DefaultHitMultiplier, int missMultiplier = EditorChart.DefaultMissMultiplier)
		{
			return new EventConfig(chart, new List<Event> { new Multipliers(hitMultiplier, missMultiplier)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateTimeSignatureConfig(
			EditorChart chart, int row, double chartTime, Fraction timeSignature)
		{
			return new EventConfig(chart, new List<Event> { new TimeSignature(timeSignature)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateLabelConfig(
			EditorChart chart, int row, double chartTime, string text = "New Label")
		{
			return new EventConfig(chart, new List<Event> { new Label(text)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateTempoConfig(
			EditorChart chart,
			int row,
			double chartTime,
			double tempo = EditorChart.DefaultTempo)
		{
			return new EventConfig(chart, new List<Event> { new Tempo(tempo)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateScrollRateConfig(
			EditorChart chart,
			int row,
			double chartTime,
			double scrollRate = EditorChart.DefaultScrollRate)
		{
			return new EventConfig(chart, new List<Event> { new ScrollRate(scrollRate)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateScrollRateInterpolationConfig(
			EditorChart chart,
			int row,
			double chartTime,
			double rate = EditorChart.DefaultScrollRate,
			int periodLen = MaxValidDenominator,
			double periodTime = 0.0,
			bool preferPeriodAsTime = false)
		{
			return new EventConfig(chart, new List<Event> { new ScrollRateInterpolation(rate, periodLen, periodTime, preferPeriodAsTime)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			} }, false, row);
		}

		public static EventConfig CreateDummyConfig(EditorChart chart, double chartPosition)
		{
			// The dummy event will not equal any other event in the tree when compared to it.
			return new EventConfig(chart, new List<Event> { CreateDummyFirstEventForRow((int)chartPosition) }, true, chartPosition, true);
		}

		public static EventConfig CreateDummyRateAlteringEventConfig(EditorChart chart)
		{
			return new EventConfig(chart, null, false, 0.0, true);
		}

		public static EventConfig CreateDummyRateAlteringEventConfigWithRow(EditorChart chart, double row)
		{
			return new EventConfig(chart, null, true, row, true);
		}
	}
}
