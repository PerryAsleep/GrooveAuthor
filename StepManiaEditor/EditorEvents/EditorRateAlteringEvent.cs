using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;

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
	}

	public class EditorDummyRateAlteringEvent : EditorRateAlteringEvent
	{
		public EditorDummyRateAlteringEvent(EditorChart editorChart, Event chartEvent) : base(editorChart, chartEvent)
		{
		}
	}
}
