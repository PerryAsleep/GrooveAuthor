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
		Preview,
		LastSecondHint,
	}

	/// <summary>
	/// The EditorChart that the new EditorEvent will be in.
	/// </summary>
	public readonly EditorChart EditorChart;

	/// <summary>
	/// The underlying Event that this EditorEvent wraps.
	/// Some EditorEvents have no Events, like the song preview.
	/// </summary>
	public readonly Event ChartEvent;

	/// <summary>
	/// A second underlying Event that this EditorEvent wraps.
	/// Most EditorEvents only have at most one underlying Event.
	/// Holds have two.
	/// </summary>
	public readonly Event AdditionalChartEvent;

	/// <summary>
	/// The SpecialType of the EditorEvent. Some EditorEvents do not correspond to Stepmania
	/// Events.
	/// </summary>
	public readonly SpecialType SpecialEventType;

	/// <summary>
	/// Whether or not to use an explicit double ChartPosition value for the EditorEvent's ChartPosition.
	/// Most EditorEvents use integer row positions and do not want double ChartPosition values, but
	/// some events like the song preview and last second hint don't occur on integer rows.
	/// </summary>
	public readonly bool UseDoubleChartPosition;

	/// <summary>
	/// Flag for whether or not the EditorEvent should be set to being edited or not.
	/// </summary>
	public bool IsBeingEdited;

	/// <summary>
	/// Whether or not the ChartTime should be automatically computed from the Event's row.
	/// In most cases this should be true. This is because the time of an event at a given row
	/// cannot technically be known until after the event type is known. Two events at the same
	/// row may have different times due to events like delays. Only once we have an EditorEvent
	/// can we then sort it against other events in the chart to determine the time.
	/// Note that if DetermineChartTimeFromPosition is true, the time will be computed by doing
	/// an O(log(N)) search of the rate altering events in the chart.
	/// In some situations it may be safe to pass in an explicit ChartTime as a performance optimization,
	/// such as shifting a lane note left or right, or converting steps to mines.
	/// </summary>
	public readonly bool DetermineChartTimeFromPosition;

	/// <summary>
	/// An explicit double ChartPosition to use when UseDoubleChartPosition is true.
	/// </summary>
	public readonly double ChartPosition;

	/// <summary>
	/// An explicit double ChartTime to use when DetermineChartTimeFromPosition is false.
	/// </summary>
	public readonly double ChartTime;

	private EventConfig(
		EditorChart editorChart,
		Event chartEvent = null,
		bool useDoubleChartPosition = false,
		double chartPosition = 0.0,
		double chartTime = 0.0,
		SpecialType specialType = SpecialType.None,
		bool isBeingEdited = false,
		bool determineChartTimeFromPosition = true,
		Event additionalChartEvent = null)
	{
		EditorChart = editorChart;
		ChartEvent = chartEvent;
		AdditionalChartEvent = additionalChartEvent;
		UseDoubleChartPosition = useDoubleChartPosition;
		ChartPosition = chartPosition;
		ChartTime = chartTime;
		SpecialEventType = specialType;
		IsBeingEdited = isBeingEdited;
		DetermineChartTimeFromPosition = determineChartTimeFromPosition;
	}

	public static EventConfig CreateCloneEventConfig(EditorEvent editorEvent, EditorChart editorChart)
	{
		var clonedEvent = editorEvent.GetEvent()?.Clone();
		var clonedAdditionalEvent = editorEvent.GetAdditionalEvent()?.Clone();

		var specialType = SpecialType.None;
		switch (editorEvent)
		{
			case EditorSearchRateAlteringEventWithTime:
				specialType = SpecialType.TimeOnlySearch;
				break;
			case EditorSearchRateAlteringEventWithRow:
				specialType = SpecialType.RowSearch;
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
			clonedEvent,
			false,
			editorEvent.GetChartPosition(),
			editorEvent.GetChartTime(),
			specialType,
			editorEvent.IsBeingEdited(),
			false,
			clonedAdditionalEvent);
	}

	public bool IsSearchEvent()
	{
		if (SpecialEventType == SpecialType.TimeOnlySearch
		    || SpecialEventType == SpecialType.RowSearch)
			return true;

		if (ChartEvent != null && AdditionalChartEvent == null && ChartEvent is SearchEvent)
			return true;

		return false;
	}

	public static EventConfig CreateConfig(EditorChart chart, Event smEvent)
	{
		// When creating an EditorEvent from a Stepmania Event, do not attempt to determine the
		// chart time automatically. Assume the Stepmania Event is fully configured and includes
		// a correct time. This avoids the unnecessary time lookup and it useful when loading
		// charts and we may not have all timing events available to search yet.
		return new EventConfig(chart, smEvent, false, 0.0, 0.0, SpecialType.None, false, false);
	}

	public static EventConfig CreateHoldConfig(EditorChart chart, LaneHoldStartNote start, LaneHoldEndNote end)
	{
		// When creating an EditorEvent from a Stepmania Event, do not attempt to determine the
		// chart time automatically. Assume the Stepmania Event is fully configured and includes
		// a correct time. This avoids the unnecessary time lookup and it useful when loading
		// charts and we may not have all timing events available to search yet.
		return new EventConfig(chart, start, false, 0.0, 0.0, SpecialType.None, false, false, end);
	}

	public static EventConfig CreateHoldConfig(EditorChart chart, int row, int lane, int length, bool roll)
	{
		var holdStartNote = new LaneHoldStartNote
		{
			Lane = lane,
			IntegerPosition = row,
			SourceType = roll ? NoteStrings[(int)NoteType.RollStart] : null,
		};
		var holdEndNote = new LaneHoldEndNote
		{
			Lane = lane,
			IntegerPosition = row + length,
		};
		return new EventConfig(chart, holdStartNote, false, 0.0, 0.0, SpecialType.None, false, true, holdEndNote);
	}

	public static EventConfig CreateTapConfig(EditorChart chart, int row, int lane)
	{
		return new EventConfig(chart, new LaneTapNote
		{
			Lane = lane,
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateTapConfigWithExplicitTime(EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new LaneTapNote
		{
			Lane = lane,
			IntegerPosition = row,
			TimeSeconds = chartTime,
		}, false, row, chartTime, SpecialType.None, false, false);
	}

	public static EventConfig CreateMineConfig(EditorChart chart, int row, int lane)
	{
		return new EventConfig(chart, new LaneNote
		{
			Lane = lane,
			IntegerPosition = row,
			SourceType = NoteStrings[(int)NoteType.Mine],
		});
	}

	public static EventConfig CreateMineConfigWithExplicitTime(EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new LaneNote
		{
			Lane = lane,
			IntegerPosition = row,
			TimeSeconds = chartTime,
			SourceType = NoteStrings[(int)NoteType.Mine],
		}, false, row, chartTime, SpecialType.None, false, false);
	}

	public static EventConfig CreateFakeNoteConfig(EditorChart chart, int row, int lane)
	{
		return new EventConfig(chart, new LaneTapNote
		{
			Lane = lane,
			IntegerPosition = row,
			SourceType = NoteStrings[(int)NoteType.Fake],
		});
	}

	public static EventConfig CreateFakeNoteConfigWithExplicitTime(EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new LaneTapNote
		{
			Lane = lane,
			IntegerPosition = row,
			TimeSeconds = chartTime,
			SourceType = NoteStrings[(int)NoteType.Fake],
		}, false, row, chartTime, SpecialType.None, false, false);
	}

	public static EventConfig CreateLiftNoteConfig(EditorChart chart, int row, int lane)
	{
		return new EventConfig(chart, new LaneTapNote
		{
			Lane = lane,
			IntegerPosition = row,
			SourceType = NoteStrings[(int)NoteType.Lift],
		});
	}

	public static EventConfig CreateLiftNoteConfigWithExplicitTime(EditorChart chart, int row, double chartTime, int lane)
	{
		return new EventConfig(chart, new LaneTapNote
		{
			Lane = lane,
			IntegerPosition = row,
			TimeSeconds = chartTime,
			SourceType = NoteStrings[(int)NoteType.Lift],
		}, true, row, chartTime, SpecialType.None, false, false);
	}

	public static EventConfig CreateStopConfig(EditorChart chart, int row, double stopTime)
	{
		return new EventConfig(chart, new Stop(stopTime)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateDelayConfig(EditorChart chart, int row, double stopTime)
	{
		return new EventConfig(chart, new Stop(stopTime, true)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateWarpConfig(EditorChart chart, int row, int warpLength = MaxValidDenominator)
	{
		return new EventConfig(chart, new Warp(warpLength)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateFakeConfig(EditorChart chart, int row, double fakeTime)
	{
		return new EventConfig(chart, new FakeSegment(fakeTime)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateTickCountConfig(EditorChart chart, int row, int ticks = EditorChart.DefaultTickCount)
	{
		return new EventConfig(chart, new TickCount(ticks)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateMultipliersConfig(EditorChart chart, int row,
		int hitMultiplier = EditorChart.DefaultHitMultiplier, int missMultiplier = EditorChart.DefaultMissMultiplier)
	{
		return new EventConfig(chart, new Multipliers(hitMultiplier, missMultiplier)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateTimeSignatureConfig(EditorChart chart, int row, Fraction timeSignature)
	{
		return new EventConfig(chart, new TimeSignature(timeSignature)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateLabelConfig(EditorChart chart, int row, string text = "New Label")
	{
		return new EventConfig(chart, new Label(text)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateTempoConfig(EditorChart chart, int row, double tempo = EditorChart.DefaultTempo)
	{
		return new EventConfig(chart, new Tempo(tempo)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateScrollRateConfig(EditorChart chart, int row,
		double scrollRate = EditorChart.DefaultScrollRate)
	{
		return new EventConfig(chart, new ScrollRate(scrollRate)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreateScrollRateInterpolationConfig(
		EditorChart chart,
		int row,
		double rate = EditorChart.DefaultScrollRate,
		int periodLen = MaxValidDenominator,
		double periodTime = 0.0,
		bool preferPeriodAsTime = false)
	{
		return new EventConfig(chart, new ScrollRateInterpolation(rate, periodLen, periodTime, preferPeriodAsTime)
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreatePatternConfig(EditorChart chart, int row)
	{
		return new EventConfig(chart, new Pattern
		{
			IntegerPosition = row,
		});
	}

	public static EventConfig CreatePreviewConfig(EditorChart chart, double chartTime)
	{
		var chartPosition = 0.0;
		chart.TryGetChartPositionFromTime(chartTime, ref chartPosition);
		return new EventConfig(chart, null, true, chartPosition, chartTime, SpecialType.Preview, false, false);
	}

	public static EventConfig CreateLastSecondHintConfig(EditorChart chart, double chartTime)
	{
		var chartPosition = 0.0;
		chart.TryGetChartPositionFromTime(chartTime, ref chartPosition);
		return new EventConfig(chart, null, true, chartPosition, chartTime, SpecialType.LastSecondHint, false, false);
	}

	public static EventConfig CreateSearchEventConfig(EditorChart chart, double chartPosition)
	{
		return new EventConfig(chart, new SearchEvent
		{
			IntegerPosition = (int)chartPosition,
		}, true, chartPosition);
	}

	public static EventConfig CreateSearchEventConfig(EditorChart chart, double chartPosition, double chartTime)
	{
		return new EventConfig(chart, new SearchEvent
		{
			IntegerPosition = (int)chartPosition,
			TimeSeconds = chartTime,
		}, true, chartPosition, chartTime, SpecialType.None, false, false);
	}

	public static EventConfig CreateSearchEventConfigWithOnlyTime(EditorChart chart, double chartTime)
	{
		return new EventConfig(chart, null, false, 0.0, chartTime, SpecialType.TimeOnlySearch, false, false);
	}

	public static EventConfig CreateSearchEventConfigWithOnlyRow(EditorChart chart, double row)
	{
		return new EventConfig(chart, null, true, row, 0.0, SpecialType.RowSearch, false, false);
	}
}
