using System;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorEvent : IComparable<EditorEvent>
	{
		// Foot, expression, etc.

		private double X;
		private double Y;
		private double W;
		private double H;
		private double Scale = 1.0;

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

			// TODO: More event types
			// For now, using what should be an abstract class
			return new EditorEvent(editorChart, chartEvent);

			return null;
		}

		public EditorEvent(EditorChart editorChart, Event chartEvent)
		{
			EditorChart = editorChart;
			ChartEvent = chartEvent;
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

		public virtual int CompareTo(EditorEvent other)
		{
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}

		public virtual bool InSelection(double x, double y, double w, double h)
		{
			return X < x + w && X + W > x && Y < y + h && Y + H > y;
		}

		public virtual void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
		}

		public static int CompareToRow(double row, EditorEvent editorEvent)
		{
			return row.CompareTo(editorEvent.ChartEvent.IntegerPosition);
		}

		public static int CompareToTime(double time, EditorEvent editorEvent)
		{
			return time.CompareTo(editorEvent.ChartEvent.TimeMicros / 1000000.0);
		}
	}
}
