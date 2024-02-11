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
	/// <summary>
	/// Types of EditorEvents which don't map to Stepmania Events.
	/// </summary>
	public enum SpecialType
	{
		None,
		TimeOnlySearch,
		RowSearch,
		InterpolatedRateAlteringSearch,
		Preview,
		LastSecondHint,
	}

	public readonly EditorChart EditorChart;
	public readonly List<Event> ChartEvents;
	public readonly bool UseDoubleChartPosition;
	public readonly double ChartPosition;
	public readonly double ChartTime;
	public readonly SpecialType SpecialEventType;
	public bool IsBeingEdited;

	private EventConfig(
		EditorChart editorChart,
		List<Event> chartEvents = null,
		bool useDoubleChartPosition = false,
		double chartPosition = 0.0,
		double chartTime = 0.0,
		SpecialType specialType = SpecialType.None,
		bool isBeingEdited = false)
	{
		EditorChart = editorChart;
		ChartEvents = chartEvents;
		UseDoubleChartPosition = useDoubleChartPosition;
		ChartPosition = chartPosition;
		ChartTime = chartTime;
		SpecialEventType = specialType;
		IsBeingEdited = isBeingEdited;
	}

	public static EventConfig CreateCloneEventConfig(EditorEvent editorEvent, EditorChart editorChart)
	{
		var clonedEvents = new List<Event>();
		foreach (var originalEvent in editorEvent.GetEvents())
		{
			if (originalEvent != null)
				clonedEvents.Add(originalEvent.Clone());
		}

		var specialType = SpecialType.None;
		switch (editorEvent)
		{
			case EditorSearchRateAlteringEventWithTime:
				specialType = SpecialType.TimeOnlySearch;
				break;
			case EditorSearchRateAlteringEventWithRow:
				specialType = SpecialType.RowSearch;
				break;
			case EditorSearchInterpolatedRateAlteringEvent:
				specialType = SpecialType.InterpolatedRateAlteringSearch;
				break;
			case EditorPreviewRegionEvent:
				specialType = SpecialType.Preview;
				break;
			case EditorLastSecondHintEvent:
				specialType = SpecialType.LastSecondHint;
				break;
		}

		return new EventConfig(
			editorChart,
			clonedEvents,
			false,
			editorEvent.GetChartPosition(),
			editorEvent.GetChartTime(),
			specialType,
			editorEvent.IsBeingEdited());
	}

	public bool IsSearchEvent()
	{
		if (SpecialEventType == SpecialType.TimeOnlySearch
		    || SpecialEventType == SpecialType.RowSearch
		    || SpecialEventType == SpecialType.InterpolatedRateAlteringSearch)
			return true;

		if (ChartEvents != null && ChartEvents.Count == 1 && ChartEvents[0] is SearchEvent)
			return true;

		return false;
	}

	public static EventConfig CreateConfig(EditorChart chart, Event chartEvent)
	{
		return new EventConfig(chart, new List<Event> { chartEvent });
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
				SourceType = NoteStrings[(int)NoteType.Mine],
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
				SourceType = NoteStrings[(int)NoteType.Mine],
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
				SourceType = NoteStrings[(int)NoteType.Fake],
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
				SourceType = NoteStrings[(int)NoteType.Fake],
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
				SourceType = NoteStrings[(int)NoteType.Lift],
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
				SourceType = NoteStrings[(int)NoteType.Lift],
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

	public static EventConfig CreatePreviewConfig(EditorChart chart, double chartTime)
	{
		var chartPosition = 0.0;
		chart.TryGetChartPositionFromTime(chartTime, ref chartPosition);
		return new EventConfig(chart, null, true, chartPosition, chartTime, SpecialType.Preview);
	}

	public static EventConfig CreateLastSecondHintConfig(EditorChart chart, double chartPosition)
	{
		return new EventConfig(chart, null, true, chartPosition, 0.0, SpecialType.LastSecondHint);
	}

	public static EventConfig CreateSearchEventConfig(EditorChart chart, double chartPosition)
	{
		return new EventConfig(chart, new List<Event>
		{
			new SearchEvent
			{
				IntegerPosition = (int)chartPosition,
			},
		}, true, chartPosition);
	}

	public static EventConfig CreateSearchEventConfigWithOnlyTime(EditorChart chart, double chartTime)
	{
		return new EventConfig(chart, null, false, 0.0, chartTime, SpecialType.TimeOnlySearch);
	}

	public static EventConfig CreateSearchEventConfigWithOnlyRow(EditorChart chart, double row)
	{
		return new EventConfig(chart, null, true, row, 0.0, SpecialType.RowSearch);
	}

	public static EventConfig CreateInterpolatedRateAlteringSearchEvent(EditorChart chart, double row, double chartTime)
	{
		return new EventConfig(chart, null, true, row, chartTime, SpecialType.InterpolatedRateAlteringSearch);
	}
}
