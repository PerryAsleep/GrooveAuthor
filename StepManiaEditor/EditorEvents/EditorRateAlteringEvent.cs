using System;
using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor
{
	internal abstract class EditorRateAlteringEvent : EditorEvent, IComparable<EditorRateAlteringEvent>
	{
		/// <summary>
		/// ChartTime to use for events which follow this event.
		/// Some events (Stops) cause this value to differ from this Event's ChartTime.
		/// </summary>
		public double ChartTimeForFollowingEvents;
		/// <summary>
		/// Row to use for events which follow this event.
		/// Some events (Warps) cause this value to differ from this Event's Row.
		/// </summary>
		public int RowForFollowingEvents;
		/// <summary>
		/// Current constant scroll rate multiplier during this event. Defaults to 1.
		/// </summary>
		public double ScrollRate;
		/// <summary>
		/// Current tempo during this event.
		/// </summary>
		public double Tempo;
		/// <summary>
		/// The rate that the chart should scroll after this event in rows per second.
		/// </summary>
		public double RowsPerSecond;
		/// <summary>
		/// The rate that the chart shoulc scroll after this event in seconds per row.
		/// </summary>
		public double SecondsPerRow;
		/// <summary>
		/// The most recent TimeSignature event that precedes this event.
		/// </summary>
		public TimeSignature LastTimeSignature;

		protected EditorRateAlteringEvent(EditorChart editorChart, Event chartEvent) : base(editorChart, chartEvent)
		{
		}

		public int CompareTo(EditorRateAlteringEvent other)
		{
			var comparison = GetRow().CompareTo(other.GetRow());
			if (comparison != 0)
				return comparison;
			comparison = GetChartTime().CompareTo(other.GetChartTime());
			if (comparison != 0)
				return comparison;
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}
	}

	/// <summary>
	/// Dummy EditorRateAlteringEvent to use when needing to search for EditorRateAlteringEvent
	/// in data structures which require comparing to an input event.
	/// </summary>
	internal sealed class EditorDummyRateAlteringEvent : EditorRateAlteringEvent
	{
		private int Row;
		private double ChartTime;

		public EditorDummyRateAlteringEvent(EditorChart editorChart, int row, double chartTime) : base(editorChart, null)
		{
			Row = row;
			ChartTime = chartTime;
			IsDummyEvent = true;
		}

		public override int GetRow()
		{
			return Row;
		}
		public override double GetChartTime()
		{
			return ChartTime;
		}

		public override void SetRow(int row)
		{
			Row = row;
		}
		public override void SetTimeMicros(long timeMicros)
		{
			ChartTime = Fumen.Utils.ToSeconds(timeMicros);
		}
		public override void SetChartTime(double chartTime)
		{
			ChartTime = chartTime;
		}

		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return false; }
	}
}
