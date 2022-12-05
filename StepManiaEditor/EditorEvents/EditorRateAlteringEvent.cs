using System;
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
		/// ChartTime of this rate altering event.
		/// </summary>
		public double ChartTime;

		/// <summary>
		/// SongTime to use for events which follow this event.
		/// Some events (Stops) cause this value to differ from this Event's SongTime.
		/// </summary>
		public double ChartTimeForFollowingEvents;
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

		public override int GetRow()
		{
			return (int)Row;
		}

		public override double GetChartTime()
		{
			return ChartTime;
		}

		public int CompareTo(EditorRateAlteringEvent other)
		{
			var comparison = Row.CompareTo(other.Row);
			if (comparison != 0)
				return comparison;
			comparison = ChartTime.CompareTo(other.ChartTime);
			if (comparison != 0)
				return comparison;
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}
	}

	public class EditorDummyRateAlteringEvent : EditorRateAlteringEvent
	{
		public EditorDummyRateAlteringEvent(EditorChart editorChart, Event chartEvent) : base(editorChart, chartEvent)
		{
		}
	}
}
