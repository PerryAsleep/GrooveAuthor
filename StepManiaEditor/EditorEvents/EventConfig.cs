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
	/// An explicit double ChartPosition to use when UseDoubleChartPosition is true.
	/// </summary>
	public readonly double ChartPosition;

	/// <summary>
	/// Whether or not the ChartTime should be automatically computed from the Event's row.
	/// In most cases this should be true. This is because the time of an event at a given row
	/// cannot technically be known until after the event type is known. Two events at the same
	/// row may have different times due to events like delays. Only once we have an EditorEvent
	/// can we then sort it against other events in the chart to determine the time.
	/// Note that if DetermineRowBasedDependencies is true, the time will be computed by doing
	/// an O(log(N)) search of the rate altering events in the chart.
	/// In some situations it may be safe to pass in an explicit ChartTime as a performance optimization,
	/// such as shifting a lane note left or right, or converting steps to mines.
	/// </summary>
	public readonly bool DetermineRowBasedDependencies;

	/// <summary>
	/// An explicit double ChartTime to use when DetermineRowBasedDependencies is false.
	/// </summary>
	public readonly double ChartTime;

	/// <summary>
	/// An explicit value for this event's row relative to its measure start to use
	/// when DetermineRowBasedDependencies is false.
	/// </summary>
	public readonly short RowRelativeToMeasureStart;

	/// <summary>
	/// An explicit value for the denominator of this event's time signature to use
	/// when DetermineRowBasedDependencies is false.
	/// </summary>
	public readonly short TimeSignatureDenominator;

	/// <summary>
	/// An explicit fake value to use when DetermineRowBasedDependencies is false.
	/// </summary>
	public bool IsFakeDueToRow;

	/// <summary>
	/// Private constructor taking parameters for all configuration values.
	/// </summary>
	private EventConfig(
		EditorChart editorChart,
		Event chartEvent = null,
		bool useDoubleChartPosition = false,
		double chartPosition = 0.0,
		double chartTime = 0.0,
		SpecialType specialType = SpecialType.None,
		bool isBeingEdited = false,
		bool determineRowBasedDependencies = true,
		Event additionalChartEvent = null,
		short rowRelativeToMeasureStart = 0,
		short timeSignatureDenominator = 0,
		bool isFakeDueToRow = false)
	{
		EditorChart = editorChart;
		ChartEvent = chartEvent;
		AdditionalChartEvent = additionalChartEvent;
		UseDoubleChartPosition = useDoubleChartPosition;
		ChartPosition = chartPosition;
		ChartTime = chartTime;
		SpecialEventType = specialType;
		IsBeingEdited = isBeingEdited;
		DetermineRowBasedDependencies = determineRowBasedDependencies;
		RowRelativeToMeasureStart = rowRelativeToMeasureStart;
		TimeSignatureDenominator = timeSignatureDenominator;
		IsFakeDueToRow = isFakeDueToRow;
	}

	/// <summary>
	/// Creates an EventConfig to use for cloning the given EditorEvent to the given EditorChart.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to create a clone EventConfig for.</param>
	/// <param name="editorChart">EditorChart to own the EventConfig and the new EditorEvent.</param>
	/// <returns>EventConfig for the new EditorEvent.</returns>
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
			clonedAdditionalEvent,
			editorEvent.GetRowRelativeToMeasureStart(),
			editorEvent.GetTimeSignatureDenominator(),
			editorEvent.IsFakeDueToRow());
	}

	/// <summary>
	/// Create an EventConfig based off of a Stepmania Event.
	/// </summary>
	/// <param name="chart">EditorChart to own the new EditorEvent.</param>
	/// <param name="smEvent">Stepmania Event.</param>
	/// <param name="determineRowDependencies">
	/// Whether or not to automatically determine row-based dependencies. Note that determining row-based
	/// dependencies is an O(log(N)) operation on the number of rate altering events in the chart.
	/// If this is false then some row-dependent values will be incorrect. It is assumed the caller will
	/// be setting the row-based dependencies later.
	/// </param>
	/// <returns>EventConfig for the new EditorEvent.</returns>
	public static EventConfig CreateConfig(EditorChart chart, Event smEvent, bool determineRowDependencies)
	{
		if (determineRowDependencies)
			return new EventConfig(chart, smEvent);
		return new EventConfig(chart, smEvent, false, 0.0, smEvent.TimeSeconds, SpecialType.None, false, false);
	}

	/// <summary>
	/// Create an EventConfig for a hold note based off of Stepmania Events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new EditorEvent.</param>
	/// <param name="start">LaneHoldStartNote for the hold.</param>
	/// <param name="end">LaneHoldEndNote for the hold.</param>
	/// <param name="determineRowDependencies">
	/// Whether or not to automatically determine row-based dependencies. Note that determining row-based
	/// dependencies is an O(log(N)) operation on the number of rate altering events in the chart.
	/// If this is false then some row-dependent values will be incorrect. It is assumed the caller will
	/// be setting the row-based dependencies later.
	/// </param>
	/// <returns>EventConfig for the new EditorHoldNoteEvent.</returns>
	public static EventConfig CreateHoldConfig(EditorChart chart, LaneHoldStartNote start, LaneHoldEndNote end,
		bool determineRowDependencies)
	{
		return new EventConfig(chart, start, false, 0.0, start.TimeSeconds, SpecialType.None, false, determineRowDependencies,
			end);
	}

	/// <summary>
	/// Create an EventConfig for a hold note based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new hold.</param>
	/// <param name="row">Hold row.</param>
	/// <param name="lane">Hold lane.</param>
	/// <param name="length">Hold length.</param>
	/// <param name="roll">Whether or not the hold is a roll.</param>
	/// <returns>EventConfig for the new EditorHoldNoteEvent.</returns>
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

	/// <summary>
	/// Create an EventConfig for a tap note based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new hold.</param>
	/// <param name="row">Tap row.</param>
	/// <param name="lane">Tap lane.</param>
	/// <returns>EventConfig for the new EditorTapNoteEvent.</returns>
	public static EventConfig CreateTapConfig(EditorChart chart, int row, int lane)
	{
		return new EventConfig(chart, new LaneTapNote
		{
			Lane = lane,
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a tap note based off of another base EditorEvent.
	/// Will automatically copy row-based dependencies from the given base EditorEvent.
	/// </summary>
	/// <param name="baseEvent">EditorEvent to copy needed parameters from.</param>
	/// <returns>EventConfig for the new EditorTapNoteEvent.</returns>
	public static EventConfig CreateTapConfig(EditorEvent baseEvent)
	{
		return new EventConfig(
			baseEvent.GetEditorChart(),
			new LaneTapNote
			{
				Lane = baseEvent.GetLane(),
				IntegerPosition = baseEvent.GetRow(),
				TimeSeconds = baseEvent.GetChartTime(),
			},
			false,
			baseEvent.GetRow(),
			baseEvent.GetChartTime(),
			SpecialType.None,
			false,
			false,
			null,
			baseEvent.GetRowRelativeToMeasureStart(),
			baseEvent.GetTimeSignatureDenominator(),
			baseEvent.IsFakeDueToRow());
	}

	/// <summary>
	/// Create an EventConfig for a mine note based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new hold.</param>
	/// <param name="row">Mine row.</param>
	/// <param name="lane">Mine lane.</param>
	/// <returns>EventConfig for the new EditorMineNoteEvent.</returns>
	public static EventConfig CreateMineConfig(EditorChart chart, int row, int lane)
	{
		return new EventConfig(chart, new LaneNote
		{
			Lane = lane,
			IntegerPosition = row,
			SourceType = NoteStrings[(int)NoteType.Mine],
		});
	}

	/// <summary>
	/// Create an EventConfig for a mine note based off of another base EditorEvent.
	/// Will automatically copy row-based dependencies from the given base EditorEvent.
	/// </summary>
	/// <param name="baseEvent">EditorEvent to copy needed parameters from.</param>
	/// <returns>EventConfig for the new EditorMineNoteEvent.</returns>
	public static EventConfig CreateMineConfig(EditorEvent baseEvent)
	{
		return new EventConfig(
			baseEvent.GetEditorChart(),
			new LaneNote
			{
				Lane = baseEvent.GetLane(),
				IntegerPosition = baseEvent.GetRow(),
				TimeSeconds = baseEvent.GetChartTime(),
				SourceType = NoteStrings[(int)NoteType.Mine],
			},
			false,
			baseEvent.GetRow(),
			baseEvent.GetChartTime(),
			SpecialType.None,
			false,
			false,
			null,
			baseEvent.GetRowRelativeToMeasureStart(),
			baseEvent.GetTimeSignatureDenominator(),
			baseEvent.IsFakeDueToRow());
	}

	/// <summary>
	/// Create an EventConfig for an explicit fake note based off of another base EditorEvent.
	/// Will automatically copy row-based dependencies from the given base EditorEvent.
	/// </summary>
	/// <param name="baseEvent">EditorEvent to copy needed parameters from.</param>
	/// <returns>EventConfig for the new EditorFakeNoteEvent.</returns>
	public static EventConfig CreateFakeNoteConfig(EditorEvent baseEvent)
	{
		return new EventConfig(
			baseEvent.GetEditorChart(),
			new LaneTapNote
			{
				Lane = baseEvent.GetLane(),
				IntegerPosition = baseEvent.GetRow(),
				TimeSeconds = baseEvent.GetChartTime(),
				SourceType = NoteStrings[(int)NoteType.Fake],
			},
			false,
			baseEvent.GetRow(),
			baseEvent.GetChartTime(),
			SpecialType.None,
			false,
			false,
			null,
			baseEvent.GetRowRelativeToMeasureStart(),
			baseEvent.GetTimeSignatureDenominator(),
			baseEvent.IsFakeDueToRow());
	}

	/// <summary>
	/// Create an EventConfig for a lift note based off of another base EditorEvent.
	/// Will automatically copy row-based dependencies from the given base EditorEvent.
	/// </summary>
	/// <param name="baseEvent">EditorEvent to copy needed parameters from.</param>
	/// <returns>EventConfig for the new EditorLiftNoteEvent.</returns>
	public static EventConfig CreateLiftNoteConfig(EditorEvent baseEvent)
	{
		return new EventConfig(
			baseEvent.GetEditorChart(),
			new LaneTapNote
			{
				Lane = baseEvent.GetLane(),
				IntegerPosition = baseEvent.GetRow(),
				TimeSeconds = baseEvent.GetChartTime(),
				SourceType = NoteStrings[(int)NoteType.Lift],
			},
			true,
			baseEvent.GetRow(),
			baseEvent.GetChartTime(),
			SpecialType.None,
			false,
			false,
			null,
			baseEvent.GetRowRelativeToMeasureStart(),
			baseEvent.GetTimeSignatureDenominator(),
			baseEvent.IsFakeDueToRow());
	}

	/// <summary>
	/// Create an EventConfig for a stop based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new stop.</param>
	/// <param name="row">Stop row.</param>
	/// <param name="stopTime">Stop length in seconds.</param>
	/// <returns>EventConfig for the new EditorStopNoteEvent.</returns>
	public static EventConfig CreateStopConfig(EditorChart chart, int row, double stopTime)
	{
		return new EventConfig(chart, new Stop(stopTime)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a delay based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new delay.</param>
	/// <param name="row">Delay row.</param>
	/// <param name="stopTime">Delay length in seconds.</param>
	/// <returns>EventConfig for the new EditorDelayNoteEvent.</returns>
	public static EventConfig CreateDelayConfig(EditorChart chart, int row, double stopTime)
	{
		return new EventConfig(chart, new Stop(stopTime, true)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a warp based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new warp.</param>
	/// <param name="row">Warp row.</param>
	/// <param name="warpLength">Warp length in rows.</param>
	/// <returns>EventConfig for the new EditorWarpNoteEvent.</returns>
	public static EventConfig CreateWarpConfig(EditorChart chart, int row, int warpLength = MaxValidDenominator)
	{
		return new EventConfig(chart, new Warp(warpLength)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a fake segment based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new fake segment.</param>
	/// <param name="row">Fake segment row.</param>
	/// <param name="fakeTime">Fake segment length in seconds.</param>
	/// <returns>EventConfig for the new EditorFakeSegmentEvent.</returns>
	public static EventConfig CreateFakeConfig(EditorChart chart, int row, double fakeTime)
	{
		return new EventConfig(chart, new FakeSegment(fakeTime)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a tick count based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new tick count.</param>
	/// <param name="row">Tick count row.</param>
	/// <param name="ticks">Tick count ticks value.</param>
	/// <returns>EventConfig for the new EditorTickCountSegmentEvent.</returns>
	public static EventConfig CreateTickCountConfig(EditorChart chart, int row, int ticks = EditorChart.DefaultTickCount)
	{
		return new EventConfig(chart, new TickCount(ticks)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a multipliers event based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new multipliers event.</param>
	/// <param name="row">Multipliers event row.</param>
	/// <param name="hitMultiplier">Hit multiplier value.</param>
	/// <param name="missMultiplier">Miss multiplier value.</param>
	/// <returns>EventConfig for the new EditorMultipliersEvent.</returns>
	public static EventConfig CreateMultipliersConfig(EditorChart chart, int row,
		int hitMultiplier = EditorChart.DefaultHitMultiplier, int missMultiplier = EditorChart.DefaultMissMultiplier)
	{
		return new EventConfig(chart, new Multipliers(hitMultiplier, missMultiplier)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a time signature based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new time signature.</param>
	/// <param name="row">Time signature row.</param>
	/// <param name="timeSignature">Time signature value.</param>
	/// <returns>EventConfig for the new EditorTimeSignatureEvent.</returns>
	public static EventConfig CreateTimeSignatureConfig(EditorChart chart, int row, Fraction timeSignature)
	{
		var measure = chart.GetMeasureForNewTimeSignatureAtRow(row);
		return new EventConfig(chart, new TimeSignature(timeSignature)
		{
			IntegerPosition = row,
			// Pass the measure in through the MetricPosition.
			MetricPosition = new MetricPosition(measure, 0, 0, timeSignature.Denominator),
		});
	}

	/// <summary>
	/// Create an EventConfig for a label based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new label.</param>
	/// <param name="row">Label row.</param>
	/// <param name="text">Label text.</param>
	/// <returns>EventConfig for the new EditorLabelEvent.</returns>
	public static EventConfig CreateLabelConfig(EditorChart chart, int row, string text = "New Label")
	{
		return new EventConfig(chart, new Label(text)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a tempo based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new tempo.</param>
	/// <param name="row">Tempo row.</param>
	/// <param name="tempo">Tempo value in beats per minute.</param>
	/// <returns>EventConfig for the new EditorTempoEvent.</returns>
	public static EventConfig CreateTempoConfig(EditorChart chart, int row, double tempo = EditorChart.DefaultTempo)
	{
		return new EventConfig(chart, new Tempo(tempo)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for a scroll rate event based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new scroll rate event.</param>
	/// <param name="row">Scroll rate event row.</param>
	/// <param name="scrollRate">Scroll rate event value.</param>
	/// <returns>EventConfig for the new EditorScrollRateEvent.</returns>
	public static EventConfig CreateScrollRateConfig(EditorChart chart, int row,
		double scrollRate = EditorChart.DefaultScrollRate)
	{
		return new EventConfig(chart, new ScrollRate(scrollRate)
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for an interpolated scroll rate event based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new interpolated scroll rate event.</param>
	/// <param name="row">Interpolated scroll rate event row.</param>
	/// <param name="rate">Scroll rate event value.</param>
	/// <param name="periodLen">Scroll rate interpolation period in rows.</param>
	/// <param name="periodTime">Scroll rate interpolation period in seconds.</param>
	/// <param name="preferPeriodAsTime">Whether to prefer time over rows for scroll rate interpolation period.</param>
	/// <returns>EventConfig for the new EditorInterpolatedRateAlteringEvent.</returns>
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

	/// <summary>
	/// Create an EventConfig for a pattern based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the new pattern.</param>
	/// <param name="row">Pattern row.</param>
	/// <returns>EventConfig for the new EditorPatternEvent.</returns>
	public static EventConfig CreatePatternConfig(EditorChart chart, int row)
	{
		return new EventConfig(chart, new Pattern
		{
			IntegerPosition = row,
		});
	}

	/// <summary>
	/// Create an EventConfig for the song preview based off of the given parameters.
	/// Will automatically determine the row with a O(log(N)) search on the rate altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the preview event.</param>
	/// <param name="chartTime">Chart time of the preview.</param>
	/// <returns>EventConfig for the new EditorPreviewRegionEvent.</returns>
	public static EventConfig CreatePreviewConfig(EditorChart chart, double chartTime)
	{
		var chartPosition = 0.0;
		chart.TryGetChartPositionFromTime(chartTime, ref chartPosition);
		return new EventConfig(chart, null, true, chartPosition, chartTime, SpecialType.Preview, false, false);
	}

	/// <summary>
	/// Create an EventConfig for the last second hint based off of the given parameters.
	/// Will automatically determine the row with a O(log(N)) search on the rate altering events.
	/// </summary>
	/// <param name="chart">EditorChart to own the last second hint event.</param>
	/// <param name="chartTime">Chart time of the last second hint.</param>
	/// <returns>EventConfig for the new EditorLastSecondHintEvent.</returns>
	public static EventConfig CreateLastSecondHintConfig(EditorChart chart, double chartTime)
	{
		var chartPosition = 0.0;
		chart.TryGetChartPositionFromTime(chartTime, ref chartPosition);
		return new EventConfig(chart, null, true, chartPosition, chartTime, SpecialType.LastSecondHint, false, false);
	}

	/// <summary>
	/// Create an EventConfig for a search event based off of the given parameters.
	/// Will automatically determine row-based dependencies with an O(log(N)) search on the rate
	/// altering events.
	/// </summary>
	/// <param name="chart">EditorChart to search.</param>
	/// <param name="chartPosition">Chart position for searching.</param>
	/// <returns>EventConfig for the new EditorSearchEvent.</returns>
	public static EventConfig CreateSearchEventConfig(EditorChart chart, double chartPosition)
	{
		return new EventConfig(chart, new SearchEvent
		{
			IntegerPosition = (int)chartPosition,
		}, true, chartPosition);
	}

	/// <summary>
	/// Create an EventConfig for a search event based only on time.
	/// </summary>
	/// <param name="chart">EditorChart to search.</param>
	/// <param name="chartTime">Chart time for searching.</param>
	/// <returns>EventConfig for the new EditorSearchRateAlteringEventWithTime.</returns>
	public static EventConfig CreateSearchEventConfigWithOnlyTime(EditorChart chart, double chartTime)
	{
		return new EventConfig(chart, null, false, 0.0, chartTime, SpecialType.TimeOnlySearch, false, false);
	}

	/// <summary>
	/// Create an EventConfig for a search event based only on row.
	/// </summary>
	/// <param name="chart">EditorChart to search.</param>
	/// <param name="row">Row for searching.</param>
	/// <returns>EventConfig for the new EditorSearchRateAlteringEventWithRow.</returns>
	public static EventConfig CreateSearchEventConfigWithOnlyRow(EditorChart chart, double row)
	{
		return new EventConfig(chart, null, true, row, 0.0, SpecialType.RowSearch, false, false);
	}

	/// <summary>
	/// Returns whether or not this EventConfig is for a search event.
	/// Search events can be created even if the owning EditorChart is being edited while other events
	/// cannot be created while the owning EditorChart is being edited.
	/// </summary>
	/// <returns>True if this EventConfig is for a search event and false otherwise.</returns>
	public bool IsSearchEvent()
	{
		if (SpecialEventType == SpecialType.TimeOnlySearch
		    || SpecialEventType == SpecialType.RowSearch)
			return true;

		if (ChartEvent != null && AdditionalChartEvent == null && ChartEvent is SearchEvent)
			return true;

		return false;
	}
}
