using System;
using System.Collections.Generic;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using StepManiaEditor.EditorEvents;
using StepManiaLibrary;
using static StepManiaLibrary.Constants;
using static System.Diagnostics.Debug;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// Representation of an event in a chart for the Editor.
/// EditorEvents can have their screen-space position set, and render themselves through the Draw() method.
/// Each frame the Editor will update positions of EditorEvents that are on screen, and then Draw them.
/// As such, the positions on an EditorEvent may be stale or unset if it was not on screen recently.
/// Most, but not all EditorEvents contain one underlying Event from the Stepmania chart.
/// EditorEvents are mutable, but they are also stored in ordered containers. It is important to be
/// careful about mutating an EditorEvent in a way which affects sorting while the EditorEvent is
/// in an ordered container.
/// </summary>
internal abstract class EditorEvent : IComparable<EditorEvent>
{
	/// <summary>
	/// Static SMEventComparer for comparing EditorEvents.
	/// </summary>
	private static readonly SMCommon.SMEventComparer EventComparer;

	/// <summary>
	/// The underlying Event for this EditorEvent.
	/// Most EditorEvents have one Event.
	/// Holds have two Events. For Holds this is LaneHoldStartNote. 
	/// Some events have no Events.
	/// </summary>
	protected readonly Event ChartEvent;

	/// <summary>
	/// The EditorChart which owns this EditorEvent.
	/// </summary>
	protected readonly EditorChart EditorChart;

	/// <summary>
	/// Whether or not this EditorEvent has an immutable position.
	/// Certain events, like the first tempo and time signature cannot be deleted or moved.
	/// </summary>
	public bool IsPositionImmutable;

	/// <summary>
	/// Whether or not this EditorEvent is currently selected by the user.
	/// </summary>
	private bool Selected;

	/// <summary>
	/// Whether or not this EditorEvent is in a temporary state where it is being edited
	/// but not actually committed to the EditorChart yet.
	/// </summary>
	private bool BeingEdited;

	/// <summary>
	/// The ChartPosition as a double. EditorEvents with a ChartEvent are expected to have
	/// integer values for their ChartPosition/Row/IntegerPosition. EditorEvents created
	/// from properties which only include time values may have non-integer ChartPosition
	/// values.
	/// </summary>
	protected double ChartPosition;

	/// <summary>
	/// This event's row relative to it's measure start. This is used for time signature based
	/// note coloring.
	/// </summary>
	private short RowRelativeToMeasureStart;

	/// <summary>
	/// The denominator of the time signature for this event. This is used for time signature
	/// based note coloring.
	/// </summary>
	private short TimeSignatureDenominator;

	/// <summary>
	/// Debug flag for whether or not the event's time is valid for comparison.
	/// </summary>
	protected bool IsChartTimeValid;

	/// <summary>
	/// Whether or not this EditorEvent should be considered a fake due to its row.
	/// Fakes are steps which are skipped either due to being an explicit fake note, or being
	/// in a fake segment, or being impossible to hit due to being warped over
	/// </summary>
	protected bool FakeDueToRow;

	/// <summary>
	/// Whether or not this event is finished initializing after construction.
	/// Initialization is complete at the end of CreateEvent.
	/// </summary>
	protected bool Initialized;

	/// <summary>
	/// Whether or not this event is added to its EditorChart.
	/// </summary>
	private bool AddedToChart;

	protected static uint ScreenHeight;

	public static void SetScreenHeight(uint screenHeight)
	{
		ScreenHeight = screenHeight;
	}

	#region Initialization

	/// <summary>
	/// Static constructor.
	/// </summary>
	static EditorEvent()
	{
		// Set up the EventComparer.
		var libraryEventOrder = EventOrder.Order;
		// The editor-specific SearchEvent should come first.
		var editorEventOrder = new List<string> { nameof(SearchEvent) };
		// Then, add all StepManiaLibrary Events, which includes Patterns.
		editorEventOrder.AddRange(libraryEventOrder);
		EventComparer = new SMCommon.SMEventComparer(editorEventOrder);
	}

	/// <summary>
	/// Creates an EditorEvent from the given EventConfig.
	/// </summary>
	/// <param name="config">EventConfig struct for configuring the EditorEvent.</param>
	/// <returns>New EditorEvent.</returns>
	public static EditorEvent CreateEvent(EventConfig config)
	{
		if (config.EditorChart != null && !config.IsSearchEvent())
		{
			Assert(config.EditorChart.CanBeEdited());
			if (!config.EditorChart.CanBeEdited())
				return null;
		}

		EditorEvent newEvent = null;

		if (config.ChartEvent != null)
		{
			// Intentional modification of DestType to preserve StepMania types like mines.
			config.ChartEvent.DestType = config.ChartEvent.SourceType;
			if (config.AdditionalChartEvent != null)
				config.AdditionalChartEvent.DestType = config.AdditionalChartEvent.SourceType;

			if (config.AdditionalChartEvent != null)
			{
				if (config.ChartEvent is LaneHoldStartNote lhsn
				    && config.AdditionalChartEvent is LaneHoldEndNote lhen)
				{
					newEvent = new EditorHoldNoteEvent(config, lhsn, lhen);
				}
			}
			else
			{
				switch (config.ChartEvent)
				{
					case LaneNote ln when ln.SourceType == SMCommon.NoteStrings[(int)SMCommon.NoteType.Mine]:
						newEvent = new EditorMineNoteEvent(config, ln);
						break;
					case LaneTapNote ltn when ltn.SourceType == SMCommon.NoteStrings[(int)SMCommon.NoteType.Fake]:
						newEvent = new EditorFakeNoteEvent(config, ltn);
						break;
					case LaneTapNote ltn when ltn.SourceType == SMCommon.NoteStrings[(int)SMCommon.NoteType.Lift]:
						newEvent = new EditorLiftNoteEvent(config, ltn);
						break;
					case LaneTapNote ltn:
						newEvent = new EditorTapNoteEvent(config, ltn);
						break;
					case TimeSignature ts:
						newEvent = new EditorTimeSignatureEvent(config, ts);
						break;
					case Tempo t:
						newEvent = new EditorTempoEvent(config, t);
						break;
					case Stop s:
						newEvent = s.IsDelay ? new EditorDelayEvent(config, s) : new EditorStopEvent(config, s);
						break;
					case Warp w:
						newEvent = new EditorWarpEvent(config, w);
						break;
					case ScrollRate sr:
						newEvent = new EditorScrollRateEvent(config, sr);
						break;
					case ScrollRateInterpolation sri:
						newEvent = new EditorInterpolatedRateAlteringEvent(config, sri);
						break;
					case TickCount tc:
						newEvent = new EditorTickCountEvent(config, tc);
						break;
					case Multipliers m:
						newEvent = new EditorMultipliersEvent(config, m);
						break;
					case Label l:
						newEvent = new EditorLabelEvent(config, l);
						break;
					case FakeSegment fs:
						newEvent = new EditorFakeSegmentEvent(config, fs);
						break;
					case Pattern:
						newEvent = new EditorPatternEvent(config);
						break;
					case SearchEvent:
						newEvent = new EditorSearchEvent(config);
						break;
				}
			}
		}
		else
		{
			switch (config.SpecialEventType)
			{
				case EventConfig.SpecialType.TimeOnlySearch:
				{
					newEvent = new EditorSearchRateAlteringEventWithTime(config);
					break;
				}
				case EventConfig.SpecialType.RowSearch:
				{
					newEvent = new EditorSearchRateAlteringEventWithRow(config);
					break;
				}
				case EventConfig.SpecialType.Preview:
				{
					newEvent = new EditorPreviewRegionEvent(config);
					break;
				}
				case EventConfig.SpecialType.LastSecondHint:
				{
					newEvent = new EditorLastSecondHintEvent(config);
					break;
				}
			}
		}

		Assert(newEvent != null);
		if (config.DetermineRowBasedDependencies)
		{
			newEvent.RefreshRowDependencies();
		}
		else
		{
			newEvent.SetRowDependencies(
				config.ChartTime,
				config.RowRelativeToMeasureStart,
				config.TimeSignatureDenominator,
				config.IsFakeDueToRow);
		}

		newEvent.Initialized = true;

		return newEvent;
	}

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="config">EventConfig for configuring this EditorEvent.</param>
	protected EditorEvent(EventConfig config)
	{
		EditorChart = config.EditorChart;
		ChartEvent = config.ChartEvent;
		BeingEdited = config.IsBeingEdited;
		if (config.UseDoubleChartPosition)
			ChartPosition = config.ChartPosition;
		else if (ChartEvent != null)
			ChartPosition = ChartEvent.IntegerPosition;
		// We cannot refresh row dependencies in the constructor due to virtual methods.
		IsChartTimeValid = !config.DetermineRowBasedDependencies;
	}

	/// <summary>
	/// Clones this event.
	/// </summary>
	/// <param name="chart">
	/// Optional EditorChart to assign to the cloned event.
	/// If null, this EditorEvent's EditorChart will be used.
	/// </param>
	/// <returns>Newly cloned EditorEvent.</returns>
	public virtual EditorEvent Clone(EditorChart chart = null)
	{
		var editorChartForNewEvent = chart ?? EditorChart;
		var newEvent = CreateEvent(EventConfig.CreateCloneEventConfig(this, editorChartForNewEvent));
		newEvent.IsPositionImmutable = IsPositionImmutable;
		newEvent.ChartPosition = ChartPosition;
		newEvent.RowRelativeToMeasureStart = RowRelativeToMeasureStart;
		newEvent.TimeSignatureDenominator = TimeSignatureDenominator;
		newEvent.FakeDueToRow = FakeDueToRow;
		// Do not set IsAddedToChart. Cloned events may not be in a chart.
		return newEvent;
	}

	#endregion Initialization

	/// <summary>
	/// Whether or not this Event can be deleted. Events with immutable positions cannot be deleted.
	/// </summary>
	/// <returns>Whether or not this Event can be deleted.</returns>
	public bool CanBeDeleted()
	{
		return !IsPositionImmutable;
	}

	/// <summary>
	/// Whether or not this Event can be re-positioned.
	/// Events with immutable positions cannot be re-positioned.
	/// </summary>
	/// <returns>Whether or not this Event can be re-positioned.</returns>
	public bool CanBeRepositioned()
	{
		return !IsPositionImmutable;
	}

	/// <summary>
	/// Returns whether or not this event draws with a misc. ImGui event that is positioned through
	/// MiscEventWidgetLayoutManager.
	/// </summary>
	/// <returns>
	/// Whether or not this event draws with a misc. ImGui event that is positioned through
	/// MiscEventWidgetLayoutManager.
	/// </returns>
	public abstract bool IsMiscEvent();

	/// <summary>
	/// Gets the EditorChart that owns this EditorEvent.
	/// </summary>
	/// <returns>The EditorChart that owns this EditorEvent.</returns>
	public EditorChart GetEditorChart()
	{
		return EditorChart;
	}

	/// <summary>
	/// Gets the underlying Event for this EditorEvent.
	/// This may be null.
	/// </summary>
	/// <returns>Underlying Event for this EditorEvent.</returns>
	public Event GetEvent()
	{
		return ChartEvent;
	}

	/// <summary>
	/// Gets the underlying optional additional Event for this EditorEvent.
	/// Most EditorEvents do not have an additional Event.
	/// Some EditorEvents like holds do have an additional EditorEvent.
	/// </summary>
	/// <returns>Underlying optional additional Event for this EditorEvent.</returns>
	public virtual Event GetAdditionalEvent()
	{
		return null;
	}

	/// <summary>
	/// Gets the lane of the event. Notes have lanes. Many events have no lane.
	/// </summary>
	/// <returns>The lane of the event or InvalidArrowIndex if if this event has no lane.</returns>
	public virtual int GetLane()
	{
		if (ChartEvent == null)
			return InvalidArrowIndex;
		if (ChartEvent is LaneNote ln)
			return ln.Lane;
		return InvalidArrowIndex;
	}

	/// <summary>
	/// Sets the lane of the event. Asserts that this Event is for a LaneNote.
	/// </summary>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	public virtual void SetLane(int lane)
	{
		Assert(lane >= 0 && lane < EditorChart.NumInputs);
		Assert(ChartEvent is LaneNote);
		if (ChartEvent is LaneNote ln)
			ln.Lane = lane;
	}

	/// <summary>
	/// Sets a new row for this event.
	/// This also updates all row dependencies.
	/// </summary>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	/// <param name="row">New row to set.</param>
	public virtual void SetRow(int row)
	{
		ChartPosition = row;
		if (ChartEvent != null)
			ChartEvent.IntegerPosition = row;
		RefreshRowDependencies();
	}

	/// <summary>
	/// Gets the player index associated with this event.
	/// </summary>
	/// <returns>Player index associated with this event.</returns>
	public int GetPlayer()
	{
		if (ChartEvent is Note n)
			return n.Player;
		return 0;
	}

	/// <summary>
	/// Sets the player index associated with this event.
	/// </summary>
	/// <param name="player">Player index to set.</param>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	public virtual void SetPlayer(int player)
	{
		Assert(ChartEvent is Note);
		if (ChartEvent is Note n)
			n.Player = player;
	}

	/// <summary>
	/// Updates all information dependent on the row.
	/// </summary>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	/// <param name="activeRateAlteringEvent">
	/// Optional active rate altering event for this EditorEvent.
	/// If this is null, then the active rate altering event will be found. Passing in an explicit
	/// event can be a performance optimization to avoid N*log(N) behavior when updating N events.
	/// </param>
	public void RefreshRowDependencies(EditorRateAlteringEvent activeRateAlteringEvent = null)
	{
		// Update everything which is derived from position, but requires knowing the active rate altering event.
		// First look up the rate altering event, then provide it to everything which needs it.
		activeRateAlteringEvent ??= EditorChart.GetRateAlteringEvents().FindActiveRateAlteringEvent(this);
		// It is possible when undoing pasting to be in a momentary state where there are
		// no rate altering events because we have deleted them to restore the originals.
		if (activeRateAlteringEvent == null)
			return;
		// Time is computed from row using the active rate altering event.
		RefreshTimeBasedOnRow(activeRateAlteringEvent);
		// Measure information is computed from row using the active rate altering event.
		RefreshMeasurePosition(activeRateAlteringEvent.GetTimeSignature());
		// Fake status depends on row.
		RefreshFakeStatus();
	}

	/// <summary>
	/// Set all information dependent on row. Normally, calling SetRow is sufficient as it will refresh
	/// all row dependent information. This public method though allows for setting this information
	/// explicitly when it is known in situations like chart loading.
	/// </summary>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	/// <param name="chartTime">The chart time of the event.</param>
	/// <param name="rowRelativeToMeasureStart">The event's row relative to its measure start.</param>
	/// <param name="timeSignatureDenominator">The denominator of the time signature which is active for this event.</param>
	/// <param name="isFakeDueToRow">Whether or not this event should be considered fake or not.</param>
	public void SetRowDependencies(double chartTime, short rowRelativeToMeasureStart, short timeSignatureDenominator,
		bool isFakeDueToRow)
	{
		SetChartTime(chartTime);
		SetMeasurePosition(rowRelativeToMeasureStart, timeSignatureDenominator);
		SetIsFakeDueToRow(isFakeDueToRow);
	}

	/// <summary>
	/// Updates the chart time of the event to match its row.
	/// </summary>
	/// <param name="activeRateAlteringEvent">The active rate altering event for this event.</param>
	private void RefreshTimeBasedOnRow(EditorRateAlteringEvent activeRateAlteringEvent)
	{
		// If we are updating the time we should assume it is currently incorrect.
		IsChartTimeValid = false;
		RefreshTimeBasedOnRowImplementation(activeRateAlteringEvent);
		IsChartTimeValid = true;
	}

	/// <summary>
	/// Virtual implementation for resetting time based on row.
	/// </summary>
	/// <param name="activeRateAlteringEvent">The active rate altering event for this event.</param>
	protected virtual void RefreshTimeBasedOnRowImplementation(EditorRateAlteringEvent activeRateAlteringEvent)
	{
		SetChartTime(activeRateAlteringEvent.GetChartTimeFromPosition(GetChartPosition()));
	}

	/// <summary>
	/// Updates the measure position information of this event.
	/// This includes the event's row relative to its measure start and the denominator
	/// of the time signature which is active for this event.
	/// </summary>
	/// <param name="ts">The active time signature for this event.</param>
	private void RefreshMeasurePosition(EditorTimeSignatureEvent ts)
	{
		if (ChartEvent == null)
			return;

		// When pasting a time signature over the first time signature we will momentarily
		// have deleted the first time signature while we replace it. In this scenario the
		// LastTimeSignature associated with a rate altering event may be null. However we
		// will immediately add the new time signature and correct this by calling
		// RefreshEventTimingData. We should ignore a null time signature here to avoid
		// crashing during this scenario.
		if (ts == null)
			return;

		SetMeasurePosition((short)ts.GetRowRelativeToMeasureStart(GetRow()), (short)ts.GetDenominator());
	}

	/// <summary>
	/// Updates the measure position information of this event.
	/// </summary>
	/// <param name="rowRelativeToMeasureStart">The event's row relative to its measure start.</param>
	/// <param name="timeSignatureDenominator">The denominator of the time signature which is active for this event.</param>
	private void SetMeasurePosition(short rowRelativeToMeasureStart, short timeSignatureDenominator)
	{
		RowRelativeToMeasureStart = rowRelativeToMeasureStart;
		TimeSignatureDenominator = timeSignatureDenominator;
	}

	/// <summary>
	/// Gets the event's row relative to its measure start.
	/// </summary>
	/// <returns>The event's row relative to its measure start.</returns>
	public short GetRowRelativeToMeasureStart()
	{
		return RowRelativeToMeasureStart;
	}

	/// <summary>
	/// Gets the denominator of the time signature which is active for this event.
	/// </summary>
	/// <returns>The denominator of the time signature which is active for this event.</returns>
	public short GetTimeSignatureDenominator()
	{
		return TimeSignatureDenominator;
	}

	/// <summary>
	/// Refreshes whether or not this event is fake due to its row.
	/// </summary>
	public void RefreshFakeStatus()
	{
		SetIsFakeDueToRow(EditorChart.IsEventInFake(this));
	}

	/// <summary>
	/// Gets the row/ChartPosition of the event as an integer.
	/// </summary>
	/// <returns>Integer row of the event.</returns>
	public virtual int GetRow()
	{
		return (int)GetChartPosition();
	}

	/// <summary>
	/// Gets the row/ChartPosition of the end of the event as an integer.
	/// </summary>
	/// <returns>Integer row of the end of the event.</returns>
	public virtual int GetEndRow()
	{
		// By default an event has no length and its end row is its start row.
		return GetRow();
	}

	/// <summary>
	/// Gets the row to use for step coloring based on the color preferences.
	/// The row will be a value between 0 and the Stepmania MaxValidDenominator.
	/// </summary>
	/// <returns>Row to use for note coloring.</returns>
	public int GetStepColorRow()
	{
		switch (Preferences.Instance.PreferencesOptions.StepColorMethodValue)
		{
			case PreferencesOptions.StepColorMethod.Stepmania:
			default:
			{
				return GetRow() % SMCommon.MaxValidDenominator;
			}
			case PreferencesOptions.StepColorMethod.Note:
			{
				return RowRelativeToMeasureStart % SMCommon.MaxValidDenominator;
			}
			case PreferencesOptions.StepColorMethod.Beat:
			{
				return RowRelativeToMeasureStart * TimeSignatureDenominator / SMCommon.NumBeatsPerMeasure %
				       SMCommon.MaxValidDenominator;
			}
		}
	}

	/// <summary>
	/// Gets the row/ChartPosition of the event as a double.
	/// </summary>
	/// <returns>Double row/ChartPosition of the event.</returns>
	public virtual double GetChartPosition()
	{
		return ChartPosition;
	}

	/// <summary>
	/// Gets the row/ChartPosition of the end of the event as a double.
	/// </summary>
	/// <returns>Double row/ChartPosition of the end of the event.</returns>
	public virtual double GetEndChartPosition()
	{
		// By default an event has no length and its end chart position is its
		// start chart position.
		return GetChartPosition();
	}

	/// <summary>
	/// Sets the chart time of this event.
	/// </summary>
	/// <param name="chartTime">Chart time of this event.</param>
	protected virtual void SetChartTime(double chartTime)
	{
		if (ChartEvent == null)
			return;
		ChartEvent.TimeSeconds = chartTime;
	}

	/// <summary>
	/// Gets the chart time in seconds of the event.
	/// </summary>
	/// <returns>Chart time in seconds of the event.</returns>
	public virtual double GetChartTime()
	{
		if (ChartEvent == null)
			return 0.0;
		return ChartEvent.TimeSeconds;
	}

	/// <summary>
	/// Gets the chart time in seconds of the end of this event.
	/// </summary>
	/// <returns>Chart time in seconds of the end of this event.</returns>
	public virtual double GetEndChartTime()
	{
		// By default an event has no length and its end chart time is its
		// start chart time.
		return GetChartTime();
	}

	/// <summary>
	/// The length of the event in rows. Most events have no length. Hold notes have length.
	/// </summary>
	/// <returns>The length of the event.</returns>
	public int GetRowDuration()
	{
		return GetEndRow() - GetRow();
	}

	/// <summary>
	/// The length of the event in rows. Most events have no length. Hold notes have length.
	/// </summary>
	/// <returns>The length of the event.</returns>
	public double GetChartPositionDuration()
	{
		return GetEndChartPosition() - GetChartPosition();
	}

	/// <summary>
	/// The duration of the event in seconds. Most events have no time duration.
	/// Events like Stops have time durations.
	/// </summary>
	/// <returns>The duration of the event in seconds.</returns>
	public double GetChartTimeDuration()
	{
		return GetEndChartTime() - GetChartTime();
	}

	/// <summary>
	/// Gets a unique identifier for this event to use for ImGui widgets that draw this event.
	/// </summary>
	/// <returns>Unique identifier for this event to use for ImGui widgets that draw this event.</returns>
	protected virtual string GetImGuiId()
	{
		return $"{EditorChart.GetIndexInSong()}{GetType()}{GetLane()}{GetRow()}";
	}

	/// <summary>
	/// Sets whether or not this event is being edited.
	/// </summary>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	public void SetIsBeingEdited(bool beingEdited)
	{
		BeingEdited = beingEdited;
	}

	/// <summary>
	/// Returns whether or not this event is being edited.
	/// </summary>
	/// <returns>Whether or not this event is being edited.</returns>
	public bool IsBeingEdited()
	{
		return BeingEdited;
	}

	/// <summary>
	/// Sets whether or not this event should be considered fake due to its row.
	/// </summary>
	/// <param name="fake">
	/// Whether or not this event should be considered fake due to its row.
	/// </param>
	public void SetIsFakeDueToRow(bool fake)
	{
		if (FakeDueToRow == fake)
			return;
		var wasFake = IsFake();
		FakeDueToRow = fake;
		if (wasFake != IsFake())
			EditorChart.OnFakeChanged(this);
	}

	/// <summary>
	/// Returns whether or not this event should be considered fake due to its row.
	/// </summary>
	/// <returns>
	/// True if this event should be considered fake due to its row and false otherwise.
	/// </returns>
	public bool IsFakeDueToRow()
	{
		return FakeDueToRow;
	}

	/// <summary>
	/// Returns whether or not this event should be considered fake.
	/// </summary>
	/// <returns>True if this event should be considered fake and false otherwise.</returns>
	public virtual bool IsFake()
	{
		return FakeDueToRow;
	}

	/// <summary>
	/// Returns whether or not this event is a search event that can be compared by only ChartTime.
	/// Search events are not added to the EditorChart and are just used for performing
	/// searches in data structures which require comparisons.
	/// </summary>
	/// <returns>
	/// Whether or not this event is a search event that can be compared by only ChartTime.
	/// </returns>
	public virtual bool IsTimeOnlySearchEvent()
	{
		return false;
	}

	/// <summary>
	/// Returns whether or not this event is a search event that can be compared by only ChartPosition.
	/// Search events are not added to the EditorChart and are just used for performing
	/// searches in data structures which require comparisons.
	/// </summary>
	/// <returns>
	/// Whether or not this event is a search event that can be compared by only ChartPosition.
	/// </returns>
	public virtual bool IsRowOnlySearchEvent()
	{
		return false;
	}

	/// <summary>
	/// Returns whether or not this event is a step.
	/// Steps are taps, holds/rolls, lifts, and fakes.
	/// </summary>
	/// <returns>True if this event is a step and false otherwise.</returns>
	public virtual bool IsStep()
	{
		return false;
	}

	/// <summary>
	/// Returns whether or not this event is a visible note in a lane.
	/// Lane notes are taps, holds/rolls, lifts, fakes, and mines.
	/// </summary>
	/// <returns>True if this event is a lane note and false otherwise.</returns>
	public virtual bool IsLaneNote()
	{
		return false;
	}

	/// <summary>
	/// Returns whether or not this note becomes consumed by the receptors during play.
	/// Taps, holds, and lifts are consumed by the receptors
	/// </summary>
	/// <returns>True if this event is consumed by the receptors and false otherwise.</returns>
	public virtual bool IsConsumedByReceptors()
	{
		return false;
	}

	/// <summary>
	/// Returns a short string representation of this type of EditorEvent.
	/// </summary>
	/// <returns>Short string representation of this type of EditorEvent</returns>
	public abstract string GetShortTypeName();

	/// <summary>
	/// Called when this event is added to its EditorChart.
	/// An event may be added and removed repeatedly with undoing and redoing actions.
	/// </summary>
	public virtual void OnAddedToChart()
	{
		AddedToChart = true;
	}

	/// <summary>
	/// Called when this event is removed from its EditorChart.
	/// An event may be added and removed repeatedly with undoing and redoing actions.
	/// </summary>
	public virtual void OnRemovedFromChart()
	{
		AddedToChart = false;
	}

	/// <summary>
	/// Returns whether or not this EditorEvent is added to its EditorChart.
	/// </summary>
	/// <returns>True if this EditorEvent is added to its EditorChart and false otherwise.</returns>
	public bool IsAddedToChart()
	{
		return AddedToChart;
	}

	public virtual bool Matches(EditorEvent other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;
		if (ChartEvent == null != (other.ChartEvent == null))
			return false;
		if (!ChartPosition.DoubleEquals(other.ChartPosition))
			return false;
		if (ChartEvent == null)
			return true;
		return ChartEvent.Matches(other.ChartEvent);
	}

	#region IComparable

	private static readonly Dictionary<string, int> CustomEventOrder = new()
	{
		{ "EditorPreviewRegionEvent", 0 },
		{ "EditorLastSecondHintEvent", 1 },
	};

	private const int DefaultCustomEventOrder = 2;

	/// <summary>
	/// Comparer between this EditorEvent and another EditorEvent.
	/// </summary>
	/// <param name="other">EditorEvent to compare to.</param>
	/// <returns>
	/// Negative number if this event is less than the given event.
	/// Zero if this event is equal to the given event.
	/// Positive number if this event is greater than the given event.
	/// </returns>
	public virtual int CompareTo(EditorEvent other)
	{
		if (this == other)
			return 0;

		// First, compare based on search events which are expected to only have either time set
		// or only have position set. These search events are considered to be equal to other events
		// if their times or positions match.
		// Note that this means there may be multiple Events which while not equal to each other will
		// all be considered equal when compared to these events. As such, these should be used carefully.
		// They are convenient for comparisons, but inappropriate for inserting into data structures
		// which require predictable sorting.
		if (IsRowOnlySearchEvent() || other.IsRowOnlySearchEvent())
		{
			return GetChartPosition().CompareTo(other.GetChartPosition());
		}

		if (IsTimeOnlySearchEvent() || other.IsTimeOnlySearchEvent())
		{
			// If we are searching with a time-only search event, the other events must have a valid time.
			Assert(IsChartTimeValid && other.IsChartTimeValid);
			return GetChartTime().CompareTo(other.GetChartTime());
		}

		// Sort by position as a double first. We position certain EditorEvents that
		// don't correspond to a Stepmania event (like the preview) at non-integer
		// positions. Under normal circumstances (using only integer rows), this check
		// would be redundant with the SMCommon comparison below.
		var comparison = GetChartPosition().CompareTo(other.GetChartPosition());
		if (comparison != 0)
			return comparison;

		// Sort by types which only exist in the editor and aren't represented as Events
		// in Stepmania, like the preview and the last second hint.
		if (!CustomEventOrder.TryGetValue(GetType().Name, out var thisOrder))
			thisOrder = DefaultCustomEventOrder;
		if (!CustomEventOrder.TryGetValue(other.GetType().Name, out var otherOrder))
			otherOrder = DefaultCustomEventOrder;
		comparison = thisOrder.CompareTo(otherOrder);
		if (comparison != 0)
			return comparison;

		// Compare using the common Stepmania logic.
		comparison = EventComparer.Compare(ChartEvent, other.ChartEvent);
		if (comparison != 0)
			return comparison;

		// Events being edited come after events not being edited.
		// This sort order is being relied on in EditorChart.FindNoteAt.
		if (IsBeingEdited() != other.IsBeingEdited())
			return IsBeingEdited() ? 1 : -1;
		return 0;
	}

	/// <summary>
	/// Comparer method which compares an EditorEvent to a Stepmania Event. This is useful
	/// when needing to recompute the time of an Event with no EditorEvent (like a hold end).
	/// In these circumstances we need to search the rate altering event tree to get the active
	/// rate altering event for the Event, to compute its time.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to compare.</param>
	/// <param name="smEvent">Event to compare.</param>
	/// <returns>
	/// Negative number if the EditorEvent is less than the Stepmania Event.
	/// Zero if EditorEvent is equal to the Stepmania Event.
	/// Positive number if EditorEvent is greater than the Stepmania Event.
	/// </returns>
	public static int CompareEditorEventToSmEvent(EditorEvent editorEvent, Event smEvent)
	{
		if (editorEvent.GetEvent() == smEvent)
			return 0;

		// Stepmania events are sorted by row, lane, and type. We do not need to use time.
		// Furthermore, using time would be problematic in the primary use case of this
		// comparison, which is to recompute the time of an Event when the chart changes,
		// and its current time is stale and potentially incorrect.
		Assert(!editorEvent.IsTimeOnlySearchEvent());

		// Sort by row.
		var comparison = editorEvent.GetChartPosition().CompareTo(smEvent.IntegerPosition);
		if (comparison != 0)
			return comparison;

		// Sort by types which only exist in the editor and aren't represented as Events
		// in Stepmania, like the preview and the last second hint.
		if (!CustomEventOrder.TryGetValue(editorEvent.GetType().Name, out var editorEventOrder))
			editorEventOrder = DefaultCustomEventOrder;
		comparison = editorEventOrder.CompareTo(DefaultCustomEventOrder);
		if (comparison != 0)
			return comparison;

		// Compare using the common Stepmania logic.
		return EventComparer.Compare(editorEvent.GetEvent(), smEvent);
	}

	#endregion IComparable

	#region Selection

	/// <summary>
	/// Returns whether this event should be selected when encompassed by a region that
	/// was released with no modifier keys held. This should be true for events like notes
	/// and mines, but false for miscellaneous events.
	/// </summary>
	public abstract bool IsSelectableWithoutModifiers();

	/// <summary>
	/// Returns whether this event should be selected when encompassed by a region that
	/// was released with modifier keys held. This should be false for events like notes
	/// and mines, but true for miscellaneous events.
	/// </summary>
	public abstract bool IsSelectableWithModifiers();

	public virtual bool DoesPointIntersect(double x, double y)
	{
		return x >= X && x <= X + W && y >= Y && y <= Y + H;
	}

	public virtual bool DoesSelectionIntersect(double x, double y, double w, double h)
	{
		return X < x + w && X + W > x && Y < y + h && Y + H > y;
	}

	public bool IsSelected()
	{
		return Selected;
	}

	/// <summary>
	/// Select this event, and all other events which should be selected together with it.
	/// </summary>
	public void Select()
	{
		Selected = true;
	}

	/// <summary>
	/// Deselect this event, and all other events which should be selected together with it.
	/// </summary>
	public void Deselect()
	{
		Selected = false;
	}

	#endregion Selection

	#region Positioning and Drawing

	public virtual double X { get; set; }
	public virtual double Y { get; set; }
	public virtual double W { get; set; }
	public virtual double H { get; set; }

	public virtual float Alpha { get; set; } = 1.0f;
	public virtual double Scale { get; set; } = 1.0;

	public virtual void SetDimensions(double x, double y, double w, double h, double scale)
	{
		X = x;
		Y = y;
		W = w;
		H = h;
		Scale = scale;
	}

	public virtual void SetDimensions(double x, double y, double w, double h)
	{
		X = x;
		Y = y;
		W = w;
		H = h;
	}

	public virtual void SetPosition(double x, double y)
	{
		X = x;
		Y = y;
	}

	public virtual void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
	}

	protected float GetRenderAlpha()
	{
		return IsBeingEdited() ? ActiveEditEventAlpha : Alpha;
	}

	protected void DrawFakeMarker(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		var (textureId, _) = arrowGraphicManager.GetArrowTexture(GetRow(), GetLane(), IsSelected());
		DrawFakeMarker(textureAtlas, spriteBatch, textureId, X, Y);
	}

	protected void DrawFakeMarker(TextureAtlas textureAtlas, SpriteBatch spriteBatch, string arrowTextureId)
	{
		DrawFakeMarker(textureAtlas, spriteBatch, arrowTextureId, X, Y);
	}

	protected void DrawFakeMarker(TextureAtlas textureAtlas, SpriteBatch spriteBatch, string arrowTextureId, double arrowX,
		double arrowY)
	{
		// Draw the fake marker. Do not draw it with the selection overlay as it looks weird.
		var fakeTextureId = ArrowGraphicManager.GetFakeMarkerTexture(GetRow(), GetLane(), false);
		var (arrowW, arrowH) = textureAtlas.GetDimensions(arrowTextureId);
		var (markerW, markerH) = textureAtlas.GetDimensions(fakeTextureId);
		var markerX = arrowX + (arrowW - markerW) * 0.5 * Scale;
		var markerY = arrowY + (arrowH - markerH) * 0.5 * Scale;
		textureAtlas.Draw(
			fakeTextureId,
			spriteBatch,
			new Vector2((float)markerX, (float)markerY),
			Scale,
			0.0f,
			GetRenderAlpha());
	}

	protected void DrawTap(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		DrawTap(textureAtlas, spriteBatch, arrowGraphicManager, X, Y);
	}

	protected void DrawTap(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager, double x,
		double y)
	{
		var alpha = GetRenderAlpha();
		var pos = new Vector2((float)x, (float)y);
		var selected = IsSelected();
		string textureId;
		float rot;
		var row = GetStepColorRow();
		var lane = GetLane();

		// Draw the routine note.
		if (EditorChart.IsMultiPlayer())
		{
			// If the multiplayer overlay has alpha draw the normal note below it.
			var player = GetPlayer();
			var p = Preferences.Instance.PreferencesMultiplayer;
			if (p.RoutineNoteColorAlpha < 1.0f)
			{
				(textureId, rot) = arrowGraphicManager.GetArrowTexture(row, lane, selected);
				textureAtlas.Draw(textureId, spriteBatch, pos, Scale, rot, alpha);
			}

			// Draw fill.
			(textureId, rot, var c) = arrowGraphicManager.GetPlayerArrowTextureFill(row, lane, selected, player);
			c.A = (byte)(c.A * alpha);
			textureAtlas.Draw(textureId, spriteBatch, pos, Scale, rot, c);

			// Draw rim.
			(textureId, rot) = arrowGraphicManager.GetPlayerArrowTextureRim(lane, selected);
			textureAtlas.Draw(textureId, spriteBatch, pos, Scale, rot, alpha);
		}

		// Draw a normal note.
		else
		{
			(textureId, rot) =
				arrowGraphicManager.GetArrowTexture(row, lane, selected);
			textureAtlas.Draw(textureId, spriteBatch, pos, Scale, rot, alpha);
		}
	}

	#endregion Positioning and Drawing
}
