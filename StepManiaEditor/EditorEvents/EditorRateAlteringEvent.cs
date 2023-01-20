using System;
using Fumen.ChartDefinition;

namespace StepManiaEditor
{
	/// <summary>
	/// An EditorEvent which can alter the scroll rate or spacing of the chart.
	/// </summary>
	internal abstract class EditorRateAlteringEvent : EditorEvent, IComparable<EditorRateAlteringEvent>
	{
		/// <summary>
		/// How many rows after this event are in a warp region.
		/// Any event on a row in a warp region has a time that equals the end of the warp.
		/// Warps cause rows to be skipped.
		/// </summary>
		private double WarpRowsRemaining;
		/// <summary>
		/// How much time in seconds after this event are in a stop region.
		/// For normal stops, this will be a positive number. For negative stops which function
		/// similarly to warps, it will be negative.
		/// </summary>
		private double StopTimeRemaining;
		/// <summary>
		/// Current constant scroll rate multiplier during this event. Defaults to 1.
		/// </summary>
		private double ScrollRate;
		/// <summary>
		/// Current tempo during this event.
		/// </summary>
		private double Tempo;
		/// <summary>
		/// The rate that the chart should scroll after this event in rows per second.
		/// </summary>
		private double RowsPerSecond;
		/// <summary>
		/// The rate that the chart should scroll after this event in seconds per row.
		/// </summary>
		private double SecondsPerRow;
		/// <summary>
		/// The most recent TimeSignature event that precedes this event.
		/// </summary>
		private TimeSignature LastTimeSignature;

		protected EditorRateAlteringEvent(EventConfig config) : base(config)
		{
		}

		/// <summary>
		/// Initiliaze rate altering event values.
		/// </summary>
		public void Init(
			int warpRowsRemaining,
			double stopTimeRemaining,
			double scrollRate,
			double tempo,
			double rowsPerSecond,
			double secondsPerRow,
			TimeSignature lastTimeSignature,
			bool isPositionImmutable)
		{
			WarpRowsRemaining = warpRowsRemaining;
			StopTimeRemaining = stopTimeRemaining;
			ScrollRate = scrollRate;
			Tempo = tempo;
			RowsPerSecond = rowsPerSecond;
			SecondsPerRow = secondsPerRow;
			LastTimeSignature = lastTimeSignature;
			IsPositionImmutable = isPositionImmutable;
		}

		/// <summary>
		/// Updates this event's tempo.
		/// When intializing rate altering events some events' tempos may not be known
		/// until a future event defines the first tempo.
		/// </summary>
		public void UpdateTempo(double tempo, double rowsPerSecond, double secondsPerRow)
		{
			Tempo = tempo;
			RowsPerSecond = rowsPerSecond;
			SecondsPerRow = secondsPerRow;
		}

		/// <summary>
		/// Updates this event's scroll rate.
		/// When intializing rate altering events some events' scroll rates may not be known
		/// until a future event defines the first rate.
		/// </summary>
		public void UpdateScrollRate(double scrollRate)
		{
			ScrollRate = scrollRate;
		}

		public double GetScrollRate() { return ScrollRate; }
		public double GetTempo() { return Tempo; }
		public double GetRowsPerSecond() { return RowsPerSecond; }
		public double GetSecondsPerRow() { return SecondsPerRow; }
		public TimeSignature GetTimeSignature() { return LastTimeSignature; }

		/// <summary>
		/// Given a chart position which occurs at or after this event, return the chart time at that position.
		/// </summary>
		/// <param name="chartPosition">Chart position to get the time of.</param>
		/// <returns>Chart time of the given position.</returns>
		public double GetChartTimeFromPosition(double chartPosition)
		{
			// Note that this math matches the math used in SetEventTimeAndMetricPositionsFromRows.
			// This is important as we expect the time of an event computed here and in SMCommon to
			// be equal. Specifically the times calculated by both functions should return true when
			// compared by DoubleEquals. This is important because when rate altering events are added
			// or removed we use the SMCommon SetEventTimeAndMetricPositionsFromRows function to recalculate
			// the times for all events. If those times were to shift as part of that recalculation then
			// the events may compare differently and any data structure holding the events that relies
			// on comparisions (like a RedBlackTree) would then fail to find events.
			// See also EditorEvent.CompareTo.

			// Only cap values if the position isn't before 0
			var relativePosition = chartPosition - (GetRow() + WarpRowsRemaining);
			if (chartPosition >= 0.0)
				relativePosition = Math.Max(0.0, relativePosition);
			var relativeTime = relativePosition * SecondsPerRow + StopTimeRemaining;
			if (chartPosition >= 0.0)
				relativeTime = Math.Max(0.0, relativeTime);
			return GetChartTime() + relativeTime;
		}

		/// <summary>
		/// Given a chart time which occurs at or after this event, return the chart position at that time.
		/// </summary>
		/// <param name="chartTime">Chart time to get the position of.</param>
		/// <returns>Chart position of the given time.</returns>
		public double GetChartPositionFromTime(double chartTime)
		{
			// Only cap values if the time isn't before 0.0
			var relativeTime = chartTime - (GetChartTime() + StopTimeRemaining);
			if (chartTime >= 0.0)
				relativeTime = Math.Max(0.0, relativeTime);
			return GetRow() + relativeTime * RowsPerSecond + WarpRowsRemaining;
		}

		#region IComparable
		public int CompareTo(EditorRateAlteringEvent other)
		{
			return ((EditorEvent)this).CompareTo(other);
		}
		#endregion IComparable
	}

	/// <summary>
	/// Dummy EditorRateAlteringEvent to use when needing to search for EditorRateAlteringEvent
	/// in data structures which require comparing to an input time.
	/// </summary>
	internal sealed class EditorDummyRateAlteringEventWithTime : EditorRateAlteringEvent
	{
		private double ChartTime;

		public EditorDummyRateAlteringEventWithTime(EditorChart editorChart, double chartTime)
			: base(new EventConfig { EditorChart = editorChart, IsDummyEvent = true })
		{
			ChartTime = chartTime;
		}

		public override double GetChartTime()
		{
			return ChartTime;
		}

		public override bool IsMiscEvent() { return false; }
		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return false; }
	}

	/// <summary>
	/// Dummy EditorRateAlteringEvent to use when needing to search for EditorRateAlteringEvent
	/// in data structures which require comparing to an input row.
	/// </summary>
	internal sealed class EditorDummyRateAlteringEventWithRow : EditorRateAlteringEvent
	{
		public EditorDummyRateAlteringEventWithRow(EditorChart editorChart, double row)
			: base(new EventConfig { EditorChart = editorChart, IsDummyEvent = true, ChartPosition = row, UseDoubleChartPosition = true })
		{
		}

		public override bool IsMiscEvent() { return false; }
		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return false; }
	}
}
