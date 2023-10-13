using System.Collections.Generic;
using Fumen;
using Fumen.ChartDefinition;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Configuration class for constructing a new EditorEvent.
/// Encapsulates creation of raw Stepmania Events.
/// Encapsulates the need to use Lists to hold potentially multiple Events.
/// </summary>
internal sealed class EventConfig
{
	public readonly EditorChart EditorChart;
	public readonly List<Event> ChartEvents;
	public readonly bool UseDoubleChartPosition;
	public readonly double ChartPosition;
	public readonly double ChartTime;
	public readonly bool IsStandardSearchEvent;
	public readonly bool IsTimeOnlySearchEvent;
	public readonly bool IsRowOnlySearchEvent;

	public bool IsBeingEdited;

	private EventConfig(
		EditorChart editorChart,
		List<Event> chartEvents = null,
		bool useDoubleChartPosition = false,
		double chartPosition = 0.0,
		double chartTime = 0.0,
		bool isStandardSearchEvent = false,
		bool isTimeOnlySearchEvent = false,
		bool isRowOnlySearchEvent = false,
		bool isBeingEdited = false)
	{
		EditorChart = editorChart;
		ChartEvents = chartEvents;
		UseDoubleChartPosition = useDoubleChartPosition;
		ChartPosition = chartPosition;
		ChartTime = chartTime;
		IsStandardSearchEvent = isStandardSearchEvent;
		IsTimeOnlySearchEvent = isTimeOnlySearchEvent;
		IsRowOnlySearchEvent = isRowOnlySearchEvent;
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
			editorEvent.GetChartTime(),
			editorEvent.IsStandardSearchEvent(),
			editorEvent.IsTimeOnlySearchEvent(),
			editorEvent.IsRowOnlySearchEvent(),
			editorEvent.IsBeingEdited());
	}

	public bool IsSearchEvent()
	{
		return IsStandardSearchEvent || IsTimeOnlySearchEvent || IsRowOnlySearchEvent;
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
		return new EventConfig(chart, new List<Event> { start, end });
	}

	public static EventConfig CreateTapConfig(
		EditorChart chart, double chartPosition, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = (int)chartPosition,
				TimeSeconds = chartTime,
			},
		}, true, chartPosition, chartTime);
	}

	public static EventConfig CreateTapConfig(
		EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateMineConfig(
		EditorChart chart, double chartPosition, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneNote
			{
				Lane = lane,
				IntegerPosition = (int)chartPosition,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Mine].ToString(),
			},
		}, true, chartPosition, chartTime);
	}

	public static EventConfig CreateMineConfig(
		EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneNote
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Mine].ToString(),
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateFakeNoteConfig(
		EditorChart chart, double chartPosition, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = (int)chartPosition,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Fake].ToString(),
			},
		}, true, chartPosition, chartTime);
	}

	public static EventConfig CreateFakeNoteConfig(
		EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Fake].ToString(),
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateLiftNoteConfig(
		EditorChart chart, double chartPosition, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = (int)chartPosition,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Lift].ToString(),
			},
		}, true, chartPosition, chartTime);
	}

	public static EventConfig CreateLiftNoteConfig(
		EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new List<Event>
		{
			new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = chartTime,
				SourceType = NoteChars[(int)NoteType.Lift].ToString(),
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateStopConfig(
		EditorChart chart, int row, double chartTime, double stopTime)
	{
		return new EventConfig(chart, new List<Event>
		{
			new Stop(stopTime)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateDelayConfig(
		EditorChart chart, int row, double chartTime, double stopTime)
	{
		return new EventConfig(chart, new List<Event>
		{
			new Stop(stopTime, true)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateWarpConfig(
		EditorChart chart, int row, double chartTime, int warpLength = MaxValidDenominator)
	{
		return new EventConfig(chart, new List<Event>
		{
			new Warp(warpLength)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row);
	}

	public static EventConfig CreateFakeConfig(
		EditorChart chart, int row, double chartTime, double fakeLength)
	{
		return new EventConfig(chart, new List<Event>
		{
			new FakeSegment(fakeLength)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateTickCountConfig(
		EditorChart chart, int row, double chartTime, int ticks = EditorChart.DefaultTickCount)
	{
		return new EventConfig(chart, new List<Event>
		{
			new TickCount(ticks)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateMultipliersConfig(
		EditorChart chart, int row, double chartTime, int hitMultiplier = EditorChart.DefaultHitMultiplier,
		int missMultiplier = EditorChart.DefaultMissMultiplier)
	{
		return new EventConfig(chart, new List<Event>
		{
			new Multipliers(hitMultiplier, missMultiplier)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row);
	}

	public static EventConfig CreateTimeSignatureConfig(
		EditorChart chart, int row, double chartTime, Fraction timeSignature)
	{
		return new EventConfig(chart, new List<Event>
		{
			new TimeSignature(timeSignature)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateLabelConfig(
		EditorChart chart, int row, double chartTime, string text = "New Label")
	{
		return new EventConfig(chart, new List<Event>
		{
			new Label(text)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateTempoConfig(
		EditorChart chart,
		int row,
		double chartTime,
		double tempo = EditorChart.DefaultTempo)
	{
		return new EventConfig(chart, new List<Event>
		{
			new Tempo(tempo)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateScrollRateConfig(
		EditorChart chart,
		int row,
		double chartTime,
		double scrollRate = EditorChart.DefaultScrollRate)
	{
		return new EventConfig(chart, new List<Event>
		{
			new ScrollRate(scrollRate)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
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
		return new EventConfig(chart, new List<Event>
		{
			new ScrollRateInterpolation(rate, periodLen, periodTime, preferPeriodAsTime)
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreatePatternConfig(EditorChart chart, int row, double chartTime)
	{
		return new EventConfig(chart, new List<Event>
		{
			new Pattern
			{
				IntegerPosition = row,
				TimeSeconds = chartTime,
			},
		}, false, row, chartTime);
	}

	public static EventConfig CreateSearchEventConfig(EditorChart chart, double chartPosition)
	{
		return new EventConfig(chart, new List<Event>
		{
			new SearchEvent
			{
				IntegerPosition = (int)chartPosition,
			},
		}, true, chartPosition, 0.0, true);
	}

	public static EventConfig CreateSearchEventConfigWithOnlyTime(EditorChart chart, double chartTime)
	{
		return new EventConfig(chart, null, false, 0.0, chartTime, false, true);
	}

	public static EventConfig CreateSearchEventConfigWithOnlyRow(EditorChart chart, double row)
	{
		return new EventConfig(chart, null, true, row, 0.0, false, false, true);
	}
}
