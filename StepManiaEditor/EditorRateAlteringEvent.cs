using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor
{
	public class EditorRateAlteringEvent : IComparable<EditorRateAlteringEvent>
	{
		public double RowsPerSecond;
		public double SecondsPerRow;
		public double Row;
		public double SongTime;
		public double SongTimeForFollowingEvents;
		public Event ChartEvent;

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
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}
	}
}
