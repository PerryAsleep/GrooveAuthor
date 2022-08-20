using System;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public abstract class EditorEvent : IComparable<EditorEvent>
	{
		// Foot, expression, etc.

		private double X;
		private double Y;
		private double W;
		private double H;
		private double Scale = 1.0;
		private float Alpha = 1.0f;
		private bool BeingEdited = false;

		protected readonly Event ChartEvent;
		protected readonly EditorChart EditorChart;

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

		public virtual void SetX(double x)
		{
			X = x;
		}

		public virtual void SetY(double y)
		{
			Y = y;
		}

		public virtual void SetW(double w)
		{
			W = w;
		}

		public virtual void SetH(double h)
		{
			H = h;
		}

		public virtual void SetAlpha(float a)
		{
			Alpha = a;
		}

		public virtual double GetX()
		{
			return X;
		}

		public virtual double GetY()
		{
			return Y;
		}

		public virtual double GetW()
		{
			return W;
		}

		public virtual double GetH()
		{
			return H;
		}

		public double GetScale()
		{
			return Scale;
		}

		public float GetAlpha()
		{
			return Alpha;
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
			if (ChartEvent is LaneNote ln)
				return ln.Lane;
			return -1;
		}

		public virtual int GetRow()
		{
			return ChartEvent.IntegerPosition;
		}

		public virtual int GetLength()
		{
			return 0;
		}

		protected string GetImGuiId()
		{
			return $"{ChartEvent.GetType()}{GetLane()}{ChartEvent.IntegerPosition}";
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
