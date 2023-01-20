using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaLibrary.Constants;
using static System.Diagnostics.Debug;
using static Fumen.FumenExtensions;

namespace StepManiaEditor
{
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
		/// Configuration struct for constructing a new EditorEvent.
		/// </summary>
		internal struct EventConfig
		{
			public EditorChart EditorChart;
			public Event ChartEvent;
			public bool UseDoubleChartPosition;
			public double ChartPosition;
			public bool IsDummyEvent;
			public bool IsBeingEdited;
		}

		/// <summary>
		/// The underlying Event for this EditorEvent. Most EditorEvents have an Event, but some do not.
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
		public bool IsPositionImmutable = false;
		/// <summary>
		/// Whether or not this EditorEvent is currently selected by the user.
		/// </summary>
		private bool Selected;
		/// <summary>
		/// Whether or not this EditorEvent is in a temporary state where it is being edited
		/// but not actually committed to the EditorChart yet.
		/// </summary>
		private bool BeingEdited = false;
		/// <summary>
		/// Whether or not this is a dummy EditorEvent. Dummy events used for comparing against
		/// other EditorEvents in sorted data structures. Dummy events always sort before other
		/// events that would otherwise be equal.
		/// </summary>
		protected readonly bool IsDummyEvent;
		/// <summary>
		/// The ChartPosition as a double. EditorEvents with a ChartEvent are expected to have
		/// integer values for their ChartPosition/Row/IntegerPosition. EditorEvents created
		/// from properties which only include time values may have non-integer ChartPosition
		/// values.
		/// </summary>
		protected double ChartPosition;

		protected static uint ScreenHeight;
		public static void SetScreenHeight(uint screenHeight)
		{
			ScreenHeight = screenHeight;
		}

		/// <summary>
		/// Creates an EditorEvent from the given EventConfig.
		/// </summary>
		/// <param name="config">EventConfig struct for configuring the EditorEvent.</param>
		/// <returns>New EditorEvent.</returns>
		public static EditorEvent CreateEvent(EventConfig config)
		{
			EditorEvent newEvent = null;

			if (config.ChartEvent != null)
			{
				// Intentional modification of DestType to preserve StepMania types like mines.
				config.ChartEvent.DestType = config.ChartEvent.SourceType;

				if (config.ChartEvent is LaneTapNote ltn)
					newEvent = new EditorTapNoteEvent(config, ltn);
				else if (config.ChartEvent is LaneHoldStartNote lhsn)
					newEvent = new EditorHoldStartNoteEvent(config, lhsn);
				else if (config.ChartEvent is LaneHoldEndNote lhen)
					newEvent = new EditorHoldEndNoteEvent(config, lhen);
				else if (config.ChartEvent is LaneNote ln && ln.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.Mine].ToString())
					newEvent = new EditorMineNoteEvent(config, ln);
				else if (config.ChartEvent is TimeSignature ts)
					newEvent = new EditorTimeSignatureEvent(config, ts);
				else if (config.ChartEvent is Tempo t)
					newEvent = new EditorTempoEvent(config, t);
				else if (config.ChartEvent is Stop s)
					newEvent = s.IsDelay ? new EditorDelayEvent(config, s) : new EditorStopEvent(config, s);
				else if (config.ChartEvent is Warp w)
					newEvent = new EditorWarpEvent(config, w);
				else if (config.ChartEvent is ScrollRate sr)
					newEvent = new EditorScrollRateEvent(config, sr);
				else if (config.ChartEvent is ScrollRateInterpolation sri)
					newEvent = new EditorInterpolatedRateAlteringEvent(config, sri);
				else if (config.ChartEvent is TickCount tc)
					newEvent = new EditorTickCountEvent(config, tc);
				else if (config.ChartEvent is Multipliers m)
					newEvent = new EditorMultipliersEvent(config, m);
				else if (config.ChartEvent is Label l)
					newEvent = new EditorLabelEvent(config, l);
				else if (config.ChartEvent is FakeSegment fs)
					newEvent = new EditorFakeSegmentEvent(config, fs);
			}

			return newEvent;
		}

		/// <summary>
		/// Constuctor.
		/// </summary>
		/// <param name="config">EventConfig for configuring this EditorEvent.</param>
		protected EditorEvent(EventConfig config)
		{
			EditorChart = config.EditorChart;
			ChartEvent = config.ChartEvent;
			IsDummyEvent = config.IsDummyEvent;
			BeingEdited = config.IsBeingEdited;
			if (config.UseDoubleChartPosition)
				ChartPosition = config.ChartPosition;
			else if (ChartEvent != null)
				ChartPosition = ChartEvent.IntegerPosition;
		}

		/// <summary>
		/// Whether or not this Event can be deleted. Events with immutable positons cannot be deleted.
		/// </summary>
		/// <returns>Whether or not this Event can be deleted.</returns>
		public bool CanBeDeleted()
		{
			return !IsPositionImmutable;
		}

		/// <summary>
		/// Whether or not this Event can be re-positioned.
		/// Events with immutable positons cannot be re-positioned.
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
		/// Gets the underlying Event for this EditorEvent. This may be null.
		/// </summary>
		/// <returns>The underlying Event for this EditorEvent.</returns>
		public Event GetEvent()
		{
			return ChartEvent;
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
			if (ChartEvent == null)
				return;
			var chartTime = 0.0;
			EditorChart.TryGetTimeFromChartPosition(GetRow(), ref chartTime);
			ChartEvent.TimeSeconds = chartTime;
		}

		/// <summary>
		/// Gets the row/ChartPosition as an integer.
		/// </summary>
		/// <returns>Integer row.</returns>
		public virtual int GetRow()
		{
			return (int)ChartPosition;
		}

		/// <summary>
		/// Gets the row/ChartPosition as a double.
		/// </summary>
		/// <returns>Double row.</returns>
		public virtual double GetChartPosition()
		{	
			return ChartPosition;
		}

		/// <summary>
		/// Gets the chart time of the event in seconds.
		/// </summary>
		/// <returns>Chart time of the event in seconds.</returns>
		public virtual double GetChartTime()
		{
			if (ChartEvent == null)
				return 0.0;
			return ChartEvent.TimeSeconds;
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
		protected string GetImGuiId()
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

		#region IComparable

		private static readonly Dictionary<string, int> CustomEventOrder = new Dictionary<string, int>
		{
			{"EditorPreviewRegionEvent", 0},
			{"EditorLastSecondHintEvent", 1},
		};
		private const int DefaultCustomEventOrder = 2;

		public virtual int CompareTo(EditorEvent other)
		{
			if (this == other)
				return 0;

			// First, compare based on dummy events which are expected to only have either time set
			// or only have position set. These dummy events are considered to be equal to other events
			// if their times or positions match.
			// Note that this means there may be multiple Events which while not equal to each other will
			// all be considered equal when compared to these events. As such, these should be used carefully.
			// They are convenient for comparisons, but inappropriate for inserting into data structures
			// which require predictable sorting.
			if (this is EditorDummyRateAlteringEventWithRow || other is EditorDummyRateAlteringEventWithRow)
				return GetChartPosition().CompareTo(other.GetChartPosition());
			if (this is EditorDummyRateAlteringEventWithTime || other is EditorDummyRateAlteringEventWithTime)
				return GetChartTime().CompareTo(other.GetChartTime());

			// Sort by position as a double first. We position certain EditorEvents that
			// don't correspond to a Stepmania event (like the preview) at non-integer
			// positions. Under normal circumstances (using only integer rows), this check
			// would be redundant with the SMCommon comparison below.
			var comparison = GetChartPosition().CompareTo(other.GetChartPosition());
			if (comparison != 0)
				return comparison;

			// Compare by times.
			// EditorEvents with no Event backing them have rows which are inferred from their times.
			// In some situations (e.g. during a Stop time range), this can cause these event's to have
			// a row which occurs much earlier than other events in the "same" row.
			// This comparison relies on time values not changing unexpectedly.
			// See SMCommon.SetEventTimeAndMetricPositionsFromRows and
			// EditorRateAlteringEvent.GetChartTimeFromPosition for time calculations.
			if (!GetChartTime().DoubleEquals(other.GetChartTime()))
				return GetChartTime() - other.GetChartTime() > 0.0 ? 1 : -1;

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
			comparison = SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
			if (comparison != 0)
				return comparison;

			// Dummy events come before other events at the same location.
			if (IsDummyEvent != other.IsDummyEvent)
				return IsDummyEvent ? -1 : 1;

			// Events being edited come after events not being edited.
			// This sort order is being relied on in EditorChart.FindNoteAt.
			if (IsBeingEdited() != other.IsBeingEdited())
				return IsBeingEdited() ? 1 : -1;
			return 0;
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
		/// Returns all events which should be selected when this event is selected. For most
		/// events, that is just this event. For holds, the start and end are selected together.
		/// </summary>
		public virtual List<EditorEvent> GetEventsSelectedTogether()
		{
			return new List<EditorEvent>() { this };
		}

		/// <summary>
		/// Select this event, and all other events which should be selected together with it.
		/// </summary>
		public void Select()
		{
			foreach (var e in GetEventsSelectedTogether())
				e.Selected = true;
		}

		/// <summary>
		/// Deselect this event, and all other events which should be selected together with it.
		/// </summary>
		public void Deselect()
		{
			foreach (var e in GetEventsSelectedTogether())
				e.Selected = false;
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
}
