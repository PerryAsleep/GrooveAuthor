using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	internal abstract class EditorEvent : IComparable<EditorEvent>
	{
		// Foot, expression, etc.

		public virtual double X { get; set; }
		public virtual double Y { get; set; }
		public virtual double W { get; set; }
		public virtual double H { get; set; }

		public virtual float Alpha { get; set; } = 1.0f;
		public virtual double Scale { get; set; } = 1.0;

		/// <summary>
		/// The underlying Event for this EditorEvent. Most EditorEvents have an Event, but some do not.
		/// </summary>
		protected readonly Event ChartEvent;
		protected readonly EditorChart EditorChart;

		protected static uint ScreenHeight;

		/// <summary>
		/// Whether or not this EditorEvent can be deleted from its EditorChart.
		/// </summary>
		public bool CanBeDeleted = true;
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
		/// other EditorEvents in sorted data structures. Dummy eventsd always sort before other
		/// events that would otherwise be equal.
		/// </summary>
		protected bool IsDummyEvent;
		/// <summary>
		/// The ChartPosition as a double. EditorEvents with a ChartEvent are expected to have
		/// integer values for their ChartPosition/Row/IntegerPosition. EditorEvents created
		/// from properties which only include time values may have non-integer ChartPosition
		/// values.
		/// </summary>
		protected double ChartPosition; // TODO: Readonly

		/// <summary>
		/// Creates an EditorEvent from the given Event for the given EditorChart.
		/// </summary>
		/// <param name="editorChart">The EditorChart owning this EditorEvent.</param>
		/// <param name="chartEvent">Event backing this EditorEvent.</param>
		/// <returns>New EditorEvent.</returns>
		public static EditorEvent CreateEvent(EditorChart editorChart, Event chartEvent)
		{
			// Intentional modification of DestType to preserve StepMania types like mines.
			chartEvent.DestType = chartEvent.SourceType;

			EditorEvent newEvent = null;

			if (chartEvent is LaneTapNote ltn)
				newEvent = new EditorTapNoteEvent(editorChart, ltn);
			if (chartEvent is LaneHoldStartNote lhsn)
				newEvent = new EditorHoldStartNoteEvent(editorChart, lhsn);
			if (chartEvent is LaneHoldEndNote lhen)
				newEvent = new EditorHoldEndNoteEvent(editorChart, lhen);
			if (chartEvent is LaneNote ln && ln.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.Mine].ToString())
				newEvent = new EditorMineNoteEvent(editorChart, ln);
			if (chartEvent is TimeSignature ts)
				newEvent = new EditorTimeSignatureEvent(editorChart, ts);
			if (chartEvent is Tempo t)
				newEvent = new EditorTempoEvent(editorChart, t);
			if (chartEvent is Stop s)
				newEvent = s.IsDelay ? new EditorDelayEvent(editorChart, s) : new EditorStopEvent(editorChart, s);
			if (chartEvent is Warp w)
				newEvent = new EditorWarpEvent(editorChart, w);
			if (chartEvent is ScrollRate sr)
				newEvent = new EditorScrollRateEvent(editorChart, sr);
			if (chartEvent is ScrollRateInterpolation sri)
				newEvent = new EditorInterpolatedRateAlteringEvent(editorChart, sri);
			if (chartEvent is TickCount tc)
				newEvent = new EditorTickCountEvent(editorChart, tc);
			if (chartEvent is Multipliers m)
				newEvent = new EditorMultipliersEvent(editorChart, m);
			if (chartEvent is Label l)
				newEvent = new EditorLabelEvent(editorChart, l);
			if (chartEvent is FakeSegment fs)
				newEvent = new EditorFakeSegmentEvent(editorChart, fs);

			if (newEvent != null)
				newEvent.ChartPosition = chartEvent.IntegerPosition;

			return newEvent;
		}

		/// <summary>
		/// Creates a dummy EditorEvent used for comparing against other EditorEvents in sorted data structures.
		/// </summary>
		/// <param name="editorChart">
		/// The EditorChart owning this EditorEvent. It is expected that dummy EditorEvents aren't are not
		/// present in the events owned by the EditorChart.
		/// </param>
		/// <param name="chartEvent">
		/// Dummy Event backing this EditorEvent.
		/// See SMCommon CreateDummyFirstEventForRow.</param>
		/// <param name="chartPosition">Chart position of this event as a double.</param>
		/// <returns>New dummy EditorEvent.</returns>
		public static EditorEvent CreateDummyEvent(EditorChart editorChart, Event chartEvent, double chartPosition)
		{
			var newEvent = CreateEvent(editorChart, chartEvent);
			if (newEvent != null)
			{
				// TODO: These fields should not be settable and should part of the constructor.
				newEvent.IsDummyEvent = true;
				newEvent.ChartPosition = chartPosition;
				var chartTime = 0.0;
				editorChart.TryGetTimeFromChartPosition(chartPosition, ref chartTime);
				newEvent.SetChartTime(chartTime);
			}
			return newEvent;
		}

		protected EditorEvent(EditorChart editorChart, Event chartEvent)
		{
			EditorChart = editorChart;
			ChartEvent = chartEvent;
			if (ChartEvent != null)
				ChartPosition = chartEvent.IntegerPosition;
		}

		protected EditorEvent(EditorChart editorChart, Event chartEvent, bool beingEdited)
		{
			EditorChart = editorChart;
			ChartEvent = chartEvent;
			BeingEdited = beingEdited;
			if (ChartEvent != null)
				ChartPosition = chartEvent.IntegerPosition;
		}

		public abstract bool IsMiscEvent();

		// TODO: Is this doing anything in practice? I think we should remove it.
		public virtual void SetChartTime(double chartTime)
		{
			if (ChartEvent != null)
			{
				ChartEvent.TimeMicros = Fumen.Utils.ToMicrosRounded(chartTime);
			}
		}

		public static void SetScreenHeight(uint screenHeight)
		{
			ScreenHeight = screenHeight;
		}

		/// <summary>
		/// Set this carefully. This changes how events are sorted.
		/// This cannot be changed while this event is in a sorted list without resorting.
		/// </summary>
		/// <returns></returns>
		public void SetIsBeingEdited(bool beingEdited)
		{
			BeingEdited = beingEdited;
		}

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

		public bool IsBeingEdited()
		{
			return BeingEdited;
		}

		public EditorChart GetEditorChart()
		{
			return EditorChart;
		}

		public Event GetEvent()
		{
			return ChartEvent;
		}

		public virtual int GetLane()
		{
			if (ChartEvent == null)
				return -1;
			if (ChartEvent is LaneNote ln)
				return ln.Lane;
			return -1;
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

		public virtual double GetChartTime()
		{
			if (ChartEvent == null)
				return 0.0;
			return Fumen.Utils.ToSeconds(ChartEvent.TimeMicros);
		}

		/// <summary>
		/// The length of the event in rows. Most events have no length. Hold notes have length.
		/// </summary>
		/// <returns>The length of the event.</returns>
		public virtual int GetLength()
		{
			return 0;
		}

		protected string GetImGuiId()
		{
			return $"{ChartEvent.GetType()}{GetLane()}{ChartEvent.IntegerPosition}";
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
			comparison = GetChartTime().CompareTo(other.GetChartTime());
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

		public virtual void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
		}
	}
}
