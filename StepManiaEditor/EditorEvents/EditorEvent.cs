using System;
using System.Collections.Generic;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using StepManiaEditor.EditorEvents;
using StepManiaLibrary;
using static StepManiaLibrary.Constants;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

/// <summary>
/// Representation of an event in a chart for the Editor.
/// EditorEvents can have their screen-space position set, and render themselves through the Draw() method.
/// Most, but not all EditorEvents contain one underlying Event from the Stepmania chart.
/// Each frame the Editor will update positions of EditorEvents that are on screen, and then Draw them.
/// As such, the positions on an EditorEvent may be stale or unset if it was not on screen recently.
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
	/// Debug flag for whether or not the event's time is valid for comparison.
	/// </summary>
	protected bool IsChartTimeValid;

	protected static uint ScreenHeight;

	public static void SetScreenHeight(uint screenHeight)
	{
		ScreenHeight = screenHeight;
	}

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
				case EventConfig.SpecialType.InterpolatedRateAlteringSearch:
				{
					newEvent = new EditorSearchInterpolatedRateAlteringEvent(config);
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
		if (config.DetermineChartTimeFromPosition)
			newEvent.ResetTimeBasedOnRow();
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
		// We cannot check DetermineChartTimeFromPosition and call ResetTimeBasedOnRow here
		// because this is a virtual constructor. We need to do it after construction in CreateEvent.
		IsChartTimeValid = !config.DetermineChartTimeFromPosition;
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
		return newEvent;
	}

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
	/// <returns></returns>
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
	/// Sets a new row position for this event.
	/// This also updates the event's time.
	/// </summary>
	/// <param name="row">New row to set.</param>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	public virtual void SetNewPosition(int row)
	{
		ChartPosition = row;
		if (ChartEvent != null)
			ChartEvent.IntegerPosition = row;
		ResetTimeBasedOnRow();
	}

	/// <summary>
	/// Updates the chart time of the event to match its row.
	/// This also updates the event's time.
	/// </summary>
	/// <remarks>
	/// Call this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	public void ResetTimeBasedOnRow()
	{
		// If we are updating the time we should assume it is currently incorrect.
		IsChartTimeValid = false;
		ResetTimeBasedOnRowImplementation();
		IsChartTimeValid = true;
	}

	/// <summary>
	/// Virtual implementation for resetting time based on row.
	/// </summary>
	protected virtual void ResetTimeBasedOnRowImplementation()
	{
		if (ChartEvent == null)
			return;
		var chartTime = 0.0;
		EditorChart.TryGetTimeOfEvent(this, ref chartTime);
		ChartEvent.TimeSeconds = chartTime;
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
	public virtual int GetLength()
	{
		return 0;
	}

	/// <summary>
	/// Gets a unique identifier for this event to use for ImGui widgets that draw this event.
	/// </summary>
	/// <returns>Unique identifier for this event to use for ImGui widgets that draw this event.</returns>
	protected virtual string GetImGuiId()
	{
		return $"{ChartEvent.GetType()}{GetLane()}{ChartEvent.IntegerPosition}";
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
	/// Returns whether or not this event is a standard search event.
	/// Search events are not added to the EditorChart and are just used for performing
	/// searches in data structures which require comparisons.
	/// Standard search events have an underlying Event and can be compared normally against
	/// other EditorEvents.
	/// </summary>
	/// <returns>Whether or not this event is a standard search event.</returns>
	public virtual bool IsStandardSearchEvent()
	{
		return false;
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
	}

	/// <summary>
	/// Called when this event is removed from its EditorChart.
	/// An event may be added and removed repeatedly with undoing and redoing actions.
	/// </summary>
	public virtual void OnRemovedFromChart()
	{
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

		// Compare by times.
		// EditorEvents with no Event backing them have rows which are inferred from their times.
		// In some situations (e.g. during a Stop time range), this can cause these events to have
		// a row which occurs much earlier than other events in the "same" row.
		// This comparison relies on time values not changing unexpectedly.
		// See SMCommon.SetEventTimeAndMetricPositionsFromRows and
		// EditorRateAlteringEvent.GetChartTimeFromPosition for time calculations.
		if (IsChartTimeValid && other.IsChartTimeValid)
		{
			if (!GetChartTime().DoubleEquals(other.GetChartTime()))
				return GetChartTime() - other.GetChartTime() > 0.0 ? 1 : -1;
		}

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

		// Search events come before other events at the same location.
		if (IsStandardSearchEvent() != other.IsStandardSearchEvent())
			return IsStandardSearchEvent() ? -1 : 1;

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

	#endregion Positioning and Drawing
}
