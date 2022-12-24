using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	internal abstract class EditorEvent : IComparable<EditorEvent>, IPlaceable
	{
		// Foot, expression, etc.

		#region IPlaceable
		public virtual double X { get; set; }
		public virtual double Y { get; set; }
		public virtual double W { get; set; }
		public virtual double H { get; set; }
		#endregion IPlaceable

		public virtual float Alpha { get; set; } = 1.0f;
		public virtual double Scale { get; set; } = 1.0;

		protected readonly Event ChartEvent;
		protected readonly EditorChart EditorChart;

		protected static uint ScreenHeight;

		/// <summary>
		/// Whether or not this EditorEvent can be deleted from its EditorChart.
		/// </summary>
		public bool CanBeDeleted;
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

		public static EditorEvent CreateEvent(EditorChart editorChart, Event chartEvent)
		{
			// Intentional modification of DestType to preserve StepMania types like mines.
			chartEvent.DestType = chartEvent.SourceType;

			if (chartEvent is LaneTapNote ltn)
				return new EditorTapNoteEvent(editorChart, ltn);
			if (chartEvent is LaneHoldStartNote lhsn)
				return new EditorHoldStartNoteEvent(editorChart, lhsn);
			if (chartEvent is LaneHoldEndNote lhen)
				return new EditorHoldEndNoteEvent(editorChart, lhen);
			if (chartEvent is LaneNote ln && ln.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.Mine].ToString())
				return new EditorMineNoteEvent(editorChart, ln);
			if (chartEvent is TimeSignature ts)
				return new EditorTimeSignatureEvent(editorChart, ts);
			if (chartEvent is Tempo t)
				return new EditorTempoEvent(editorChart, t);
			if (chartEvent is Stop s)
			{
				if (s.IsDelay)
					return new EditorDelayEvent(editorChart, s);
				return new EditorStopEvent(editorChart, s);
			}
			if (chartEvent is Warp w)
				return new EditorWarpEvent(editorChart, w);
			if (chartEvent is ScrollRate sr)
				return new EditorScrollRateEvent(editorChart, sr);
			if (chartEvent is ScrollRateInterpolation sri)
				return new EditorInterpolatedRateAlteringEvent(editorChart, sri);
			if (chartEvent is TickCount tc)
				return new EditorTickCountEvent(editorChart, tc);
			if (chartEvent is Multipliers m)
				return new EditorMultipliersEvent(editorChart, m);
			if (chartEvent is Label l)
				return new EditorLabelEvent(editorChart, l);
			if (chartEvent is FakeSegment fs)
				return new EditorFakeSegmentEvent(editorChart, fs);

			return null;
		}

		public static EditorEvent CreateDummyEvent(EditorChart editorChart, Event chartEvent)
		{
			var newEvent = CreateEvent(editorChart, chartEvent);
			if (newEvent != null)
				newEvent.IsDummyEvent = true;
			return newEvent;
		}

		protected EditorEvent(EditorChart editorChart, Event chartEvent)
		{
			EditorChart = editorChart;
			ChartEvent = chartEvent;
		}

		protected EditorEvent(EditorChart editorChart, Event chartEvent, bool beingEdited)
		{
			EditorChart = editorChart;
			ChartEvent = chartEvent;
			BeingEdited = beingEdited;
		}

		public virtual void SetRow(int row)
		{
			ChartEvent.IntegerPosition = row;
		}
		public virtual void SetTimeMicros(long timeMicros)
		{
			ChartEvent.TimeMicros = timeMicros;
		}
		public virtual void SetChartTime(double chartTime)
		{
			ChartEvent.TimeMicros = Fumen.Utils.ToMicros(chartTime);
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

		public virtual int GetRow()
		{
			return ChartEvent?.IntegerPosition ?? 0;
		}

		public virtual double GetChartTime()
		{
			if (ChartEvent == null)
				return 0.0;
			return Fumen.Utils.ToSeconds(ChartEvent.TimeMicros);
		}

		public virtual int GetLength()
		{
			return 0;
		}

		protected string GetImGuiId()
		{
			return $"{ChartEvent.GetType()}{GetLane()}{ChartEvent.IntegerPosition}";
		}

		private class SortChartTimeHelper : IComparer<EditorEvent>
		{
			int IComparer<EditorEvent>.Compare(EditorEvent e1, EditorEvent e2)
			{
				var c = e1.GetChartTime().CompareTo(e2.GetChartTime());
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorEvent> SortChartTime()
		{
			return new SortChartTimeHelper();
		}

		private class SortRowHelper : IComparer<EditorEvent>
		{
			int IComparer<EditorEvent>.Compare(EditorEvent e1, EditorEvent e2)
			{
				var c = e1.GetRow().CompareTo(e2.GetRow());
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorEvent> SortRow()
		{
			return new SortRowHelper();
		}

		public virtual int CompareTo(EditorEvent other)
		{
			var comparison = SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
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

		public virtual bool DoesPointIntersect(double x, double y)
		{
			return x >= X && x <= X + W && y >= Y && y <= Y + H;
		}

		public bool IsSelected()
		{
			return Selected;
		}

		public virtual List<EditorEvent> GetEventsSelectedTogether()
		{
			return new List<EditorEvent>() { this };
		}

		public void Select()
		{
			foreach (var e in GetEventsSelectedTogether())
				e.Selected = true;
		}

		public void DeSelect()
		{
			foreach (var e in GetEventsSelectedTogether())
				e.Selected = false;
		}

		public virtual void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
		}
	}
}
