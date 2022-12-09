using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public abstract class EditorEvent : IComparable<EditorEvent>, IPlaceable
	{
		// Foot, expression, etc.

		private bool BeingEdited = false;

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
			// Events being edited come after events not being edited.
			// This sort order is being relied on in EditorChart.FindNoteAt.
			if (IsBeingEdited() != other.IsBeingEdited())
				return IsBeingEdited() ? 1 : -1;
			return 0;
		}

		public virtual bool InSelection(double x, double y, double w, double h)
		{
			return X < x + w && X + W > x && Y < y + h && Y + H > y;
		}

		public virtual void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
		}
	}
}
