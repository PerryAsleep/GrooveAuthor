using System;
using System.Collections.Generic;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public abstract class EditorRateAlteringEvent : EditorEvent, IComparable<EditorRateAlteringEvent>
	{
		/// <summary>
		/// Row of this rate altering event.
		/// </summary>
		public double Row;
		/// <summary>
		/// SongTime of this rate altering event.
		/// </summary>
		public double SongTime;

		/// <summary>
		/// SongTime to use for events which follow this event.
		/// Some events (Stops) cause this value to differ from this Event's SongTime.
		/// </summary>
		public double SongTimeForFollowingEvents;
		/// <summary>
		/// Row to use for events which follow this event.
		/// Some events (Warps) cause this value to differ from this Event's Row.
		/// </summary>
		public int RowForFollowingEvents;

		/// <summary>
		/// Constant scroll rate multiplier. Defaults to 1.
		/// </summary>
		public double ScrollRate;

		public double Tempo;
		public double RowsPerSecond;
		public double SecondsPerRow;

		public TimeSignature LastTimeSignature;

		public bool CanBeDeleted;

		protected EditorRateAlteringEvent(EditorChart editorChart, Event chartEvent) : base(editorChart, chartEvent)
		{
		}

		private class SortSongTimeHelper : IComparer<EditorRateAlteringEvent>
		{
			int IComparer<EditorRateAlteringEvent>.Compare(EditorRateAlteringEvent e1, EditorRateAlteringEvent e2)
			{
				var c = e1.SongTime.CompareTo(e2.SongTime);
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorRateAlteringEvent> SortSongTime()
		{
			return new SortSongTimeHelper();
		}

		private class SortRowHelper : IComparer<EditorRateAlteringEvent>
		{
			int IComparer<EditorRateAlteringEvent>.Compare(EditorRateAlteringEvent e1, EditorRateAlteringEvent e2)
			{
				var c = e1.Row.CompareTo(e2.Row);
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorRateAlteringEvent> SortRow()
		{
			return new SortRowHelper();
		}

		public int CompareTo(EditorRateAlteringEvent other)
		{
			var comparison = Row.CompareTo(other.Row);
			if (comparison != 0)
				return comparison;
			comparison = SongTime.CompareTo(other.SongTime);
			if (comparison != 0)
				return comparison;
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}

		public static int CompareToRow(double row, EditorRateAlteringEvent editorEvent)
		{
			return row.CompareTo(editorEvent.Row);
		}

		public static int CompareToTime(double songTime, EditorRateAlteringEvent editorEvent)
		{
			return songTime.CompareTo(editorEvent.SongTime);
		}

		protected string GetImGuiId()
		{
			return $"{ChartEvent.GetType()}{Row}";
		}

		public static implicit operator Event(EditorRateAlteringEvent e) => e.ChartEvent;
	}

	public class DummyEditorRateAlteringEvent : EditorRateAlteringEvent
	{
		public DummyEditorRateAlteringEvent(EditorChart editorChart, Event chartEvent) : base(editorChart, chartEvent)
		{
		}
	}

	public class EditorTempoEvent : EditorRateAlteringEvent
	{
		public Tempo TempoEvent;

		public double DoubleValue
		{
			get => TempoEvent.TempoBPM;
			set
			{
				if (!TempoEvent.TempoBPM.DoubleEquals(value))
				{
					TempoEvent.TempoBPM = value;
					EditorChart.OnRateAlteringEventModified(this);
				}
			}
		}

		public EditorTempoEvent(EditorChart editorChart, Tempo chartEvent) : base(editorChart, chartEvent)
		{
			TempoEvent = chartEvent;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UITempoColorABGR,
				false,
				CanBeDeleted,
				"%f b/m");
		}

		public static implicit operator Event(EditorTempoEvent e) => e.ChartEvent;
	}

	public class EditorTimeSignatureEvent : EditorRateAlteringEvent
	{
		public TimeSignature TimeSignatureEvent;

		public string TimeSignatureValue
		{
			get => TimeSignatureEvent.Signature.ToString();
			set
			{
				var newSignature = Fraction.FromString(value);
				if (newSignature != null && !TimeSignatureEvent.Signature.Equals(newSignature))
				{
					TimeSignatureEvent.Signature = newSignature;
					EditorChart.OnRateAlteringEventModified(this);
				}
			}
		}

		public EditorTimeSignatureEvent(EditorChart editorChart, TimeSignature chartEvent) : base(editorChart, chartEvent)
		{
			TimeSignatureEvent = chartEvent;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventTimeSignatureWidget(
				GetImGuiId(),
				this,
				nameof(TimeSignatureValue),
				(int)X, (int)Y, (int)W,
				Utils.UITimeSignatureColorABGR,
				false,
				CanBeDeleted);
		}

		public static explicit operator Event(EditorTimeSignatureEvent e) => e.ChartEvent;
		//public static implicit operator Event(EditorTimeSignatureEvent e) => e.ChartEvent;
	}

	public class EditorStopEvent : EditorRateAlteringEvent
	{
		public Stop StopEvent;

		public double DoubleValue
		{
			get => StopEvent.LengthMicros / 1000000.0;
			set
			{
				var newMicros = (long)(value * 1000000);
				if (StopEvent.LengthMicros != newMicros)
				{
					StopEvent.LengthMicros = newMicros;
					EditorChart.OnRateAlteringEventModified(this);
				}
			}
		}

		public EditorStopEvent(EditorChart editorChart, Stop chartEvent) : base(editorChart, chartEvent)
		{
			StopEvent = chartEvent;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UIStopColorABGR,
				false,
				CanBeDeleted,
				"%f s");
		}

		public static implicit operator Event(EditorStopEvent e) => e.ChartEvent;
	}

	public class EditorDelayEvent : EditorRateAlteringEvent
	{
		public Stop StopEvent;

		public double DoubleValue
		{
			get => StopEvent.LengthMicros / 1000000.0;
			set
			{
				var newMicros = (long)(value * 1000000);
				if (StopEvent.LengthMicros != newMicros)
				{
					StopEvent.LengthMicros = newMicros;
					EditorChart.OnRateAlteringEventModified(this);
				}
			}
		}

		public EditorDelayEvent(EditorChart editorChart, Stop chartEvent) : base(editorChart, chartEvent)
		{
			StopEvent = chartEvent;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UIDelayColorABGR,
				false,
				CanBeDeleted,
				"%f s");
		}

		public static implicit operator Event(EditorDelayEvent e) => e.ChartEvent;
	}

	public class EditorWarpEvent : EditorRateAlteringEvent
	{
		public Warp WarpEvent;

		//public double DoubleValue
		//{
		//	get => WarpEvent.LengthIntegerPosition / 1000000.0;
		//	set
		//	{
		//		var newMicros = (long)(value * 1000000);
		//		if (WarpEvent.LengthMicros != newMicros)
		//		{
		//			WarpEvent.LengthMicros = newMicros;
		//			EditorChart.OnRateAlteringEventModified(this);
		//		}
		//	}
		//}

		public EditorWarpEvent(EditorChart editorChart, Warp chartEvent) : base(editorChart, chartEvent)
		{
			WarpEvent = chartEvent;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			//ImGuiLayoutUtils.Widget(GetImGuiId(), (int)X, (int)Y, (int)W, Utils.UIWarpColorABGR, false);
		}

		public static implicit operator Event(EditorWarpEvent e) => e.ChartEvent;
	}

	public class EditorScrollRateEvent : EditorRateAlteringEvent
	{
		public ScrollRate ScrollRateEvent;

		public double DoubleValue
		{
			get => ScrollRateEvent.Rate;
			set
			{
				var f = (float)value;
				if (!ScrollRateEvent.Rate.FloatEquals(f) )
				{
					ScrollRateEvent.Rate = f;
					EditorChart.OnRateAlteringEventModified(this);
				}
			}
		}

		public EditorScrollRateEvent(EditorChart editorChart, ScrollRate chartEvent) : base(editorChart, chartEvent)
		{
			ScrollRateEvent = chartEvent;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UIScrollsColorABGR,
				false,
				CanBeDeleted,
				"%f");
		}

		public static implicit operator Event(EditorScrollRateEvent e) => e.ChartEvent;
	}


	public class EditorInterpolatedRateAlteringEvent : EditorEvent, IComparable<EditorInterpolatedRateAlteringEvent>
	{
		/// <summary>
		/// Row of this rate altering event.
		/// </summary>
		public double Row;
		/// <summary>
		/// SongTime of this rate altering event.
		/// </summary>
		public double SongTime;

		public double PreviousScrollRate = 1.0;

		public ScrollRateInterpolation ScrollRateInterpolationEvent;

		public EditorInterpolatedRateAlteringEvent(EditorChart editorChart, ScrollRateInterpolation chartEvent) : base(editorChart, chartEvent)
		{
			ScrollRateInterpolationEvent = chartEvent;
		}

		public bool InterpolatesByTime()
		{
			return ScrollRateInterpolationEvent.PreferPeriodAsTimeMicros;
		}

		public double GetInterpolatedScrollRateFromTime(double time)
		{
			return Fumen.Interpolation.Lerp(
				PreviousScrollRate,
				ScrollRateInterpolationEvent.Rate,
				SongTime,
				SongTime + ScrollRateInterpolationEvent.PeriodTimeMicros / 1000000.0,
				time);
		}

		public double GetInterpolatedScrollRateFromRow(double row)
		{
			return Fumen.Interpolation.Lerp(
				PreviousScrollRate,
				ScrollRateInterpolationEvent.Rate,
				Row,
				Row + ScrollRateInterpolationEvent.PeriodLengthIntegerPosition,
				row);
		}

		private class SortSongTimeHelper : IComparer<EditorInterpolatedRateAlteringEvent>
		{
			int IComparer<EditorInterpolatedRateAlteringEvent>.Compare(EditorInterpolatedRateAlteringEvent e1, EditorInterpolatedRateAlteringEvent e2)
			{
				var c = e1.SongTime.CompareTo(e2.SongTime);
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorInterpolatedRateAlteringEvent> SortSongTime()
		{
			return new SortSongTimeHelper();
		}

		private class SortRowHelper : IComparer<EditorInterpolatedRateAlteringEvent>
		{
			int IComparer<EditorInterpolatedRateAlteringEvent>.Compare(EditorInterpolatedRateAlteringEvent e1, EditorInterpolatedRateAlteringEvent e2)
			{
				var c = e1.Row.CompareTo(e2.Row);
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorInterpolatedRateAlteringEvent> SortRow()
		{
			return new SortRowHelper();
		}

		public int CompareTo(EditorInterpolatedRateAlteringEvent other)
		{
			var comparison = Row.CompareTo(other.Row);
			if (comparison != 0)
				return comparison;
			comparison = SongTime.CompareTo(other.SongTime);
			if (comparison != 0)
				return comparison;
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			//ImGuiLayoutUtils.Widget($"{ChartEvent.GetType()}{Row}", (int)X, (int)Y, (int)W, Utils.UISpeedsColorABGR, false);
		}

		public static implicit operator Event(EditorInterpolatedRateAlteringEvent e) => e.ChartEvent;
	}
}
