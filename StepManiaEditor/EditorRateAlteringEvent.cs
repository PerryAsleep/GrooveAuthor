using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor
{
	public class EditorRateAlteringEvent : IComparable<EditorRateAlteringEvent>
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
		/// ChartEvent corresponding to this rate altering event.
		/// </summary>
		public Event ChartEvent;

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

		public static int CompareByRow(double row, EditorRateAlteringEvent editorEvent)
		{
			return row.CompareTo(editorEvent.Row);
		}

		public static int CompareBySongTime(double songTime, EditorRateAlteringEvent editorEvent)
		{
			return songTime.CompareTo(editorEvent.SongTime);
		}
	}

	public class EditorInterpolatedRateAlteringEvent : IComparable<EditorInterpolatedRateAlteringEvent>
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
		/// <summary>
		/// ChartEvent corresponding to this rate altering event.
		/// </summary>
		public ScrollRateInterpolation ChartEvent;

		public bool InterpolatesByTime()
		{
			return ChartEvent.PreferPeriodAsTimeMicros;
		}

		public double GetInterpolatedScrollRateFromTime(double time)
		{
			return Fumen.Interpolation.Lerp(
				PreviousScrollRate,
				ChartEvent.Rate,
				SongTime,
				SongTime + ChartEvent.PeriodTimeMicros / 1000000.0,
				time);
		}

		public double GetInterpolatedScrollRateFromRow(double row)
		{
			return Fumen.Interpolation.Lerp(
				PreviousScrollRate,
				ChartEvent.Rate,
				Row,
				Row + ChartEvent.PeriodLengthIntegerPosition,
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
	}
}
