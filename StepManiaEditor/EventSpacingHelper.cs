
namespace StepManiaEditor
{
	/// <summary>
	/// Class for encapsulating spacing logic that depends on the current SpacingMode.
	/// </summary>
	internal abstract class EventSpacingHelper
	{
		/// <summary>
		/// Current rate in pixels per second.
		/// </summary>
		protected double Pps = 1.0;
		/// <summary>
		/// Current rate in pixels per row.
		/// </summary>
		protected double Ppr = 1.0;
		/// <summary>
		/// Previous rate in pixels per second.
		/// </summary>
		protected double PreviousPps = 1.0;
		/// <summary>
		/// Previous rate in pixels per row.
		/// </summary>
		protected double PreviousPpr = 1.0;
		/// <summary>
		/// The active EditorChart.
		/// </summary>
		protected EditorChart ActiveChart;

		/// <summary>
		/// Factory method for creating an EventSpacingHelper appropriate for the current
		/// SpacingMode using the given active EditorChart.
		/// </summary>
		/// <param name="activeChart">The currently active EditorChart.</param>
		/// <returns>New EventSpacingHelper.</returns>
		public static EventSpacingHelper GetSpacingHelper(EditorChart activeChart)
		{
			switch (Preferences.Instance.PreferencesScroll.SpacingMode)
			{
				case Editor.SpacingMode.ConstantTime:
				default:
					return new EventSpacingHelperConstantTime(activeChart);
				case Editor.SpacingMode.ConstantRow:
					return new EventSpacingHelperConstantRow(activeChart);
				case Editor.SpacingMode.Variable:
					return new EventSpacingHelperVariable(activeChart);
			}
		}

		/// <summary>
		/// Protected constructor.
		/// </summary>
		/// <param name="activeChart">The currently active EditorChart.</param>
		protected EventSpacingHelper(EditorChart activeChart)
		{
			ActiveChart = activeChart;
		}

		/// <summary>
		/// Update the current pps and ppr values based on the given EditorRateAlteringEvent.
		/// </summary>
		/// <param name="rateEvent">The EditorRateAlteringEvent to use for determing the pps and ppr values.</param>
		/// <param name="interpolatedScrollRate">The current interpolated scroll rate value.</param>
		/// <param name="rateEvent">The current spacing zoom value.</param>
		public void UpdatePpsAndPpr(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate, double spacingZoom)
		{
			// Cache the previous values.
			PreviousPps = Pps;
			PreviousPpr = Ppr;
			// Update the current values.
			InternalUpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
		}

		/// <summary>
		/// Update the current pps and ppr values based on the given EditorRateAlteringEvent.
		/// Protected method for derived classes to implement.
		/// </summary>
		/// <param name="rateEvent">The EditorRateAlteringEvent to use for determing the pps and ppr values.</param>
		/// <param name="interpolatedScrollRate">The current interpolated scroll rate value.</param>
		/// <param name="rateEvent">The current spacing zoom value.</param>
		protected abstract void InternalUpdatePpsAndPpr(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate, double spacingZoom);

		/// <summary>
		/// Returns whether or not the current scroll rate is negative.
		/// </summary>
		/// <returns>Whether or not the current scroll rate is negative.</returns>
		public bool IsScrollRateNegative()
		{
			return Pps < 0.0 || Ppr < 0.0;
		}

		/// <summary>
		/// Gets the chart time and row for the given y position in screen space, relative to the given anchor information
		/// and the currently set pps and ppr values.
		/// </summary>
		/// <param name="y">Screen space y value to get the chart time and row for.</param>
		/// <param name="anchorY">
		/// Position in screen space of another event to use for relative positioning.
		/// In most cases this is the previous rate altering event.
		/// </param>
		/// <param name="anchorChartTime">The chart time of the anchor event.</param>
		/// <param name="anchorRow">The row of the achor event.</param>
		/// <returns>Tuple where the first value is the chart time and the second value is the row.</returns>
		public abstract (double, double) GetChartTimeAndRow(double y, double anchorY, double anchorChartTime, double anchorRow);

		/// <summary>
		/// Gets the chart time and row for the given y position in screen space, relative to the given anchor information
		/// and the previously set pps and ppr values.
		/// </summary>
		/// <param name="y">Screen space y value to get the chart time and row for.</param>
		/// <param name="anchorY">
		/// Position in screen space of another event to use for relative positioning.
		/// In most cases this is the previous rate altering event.
		/// </param>
		/// <param name="anchorChartTime">The chart time of the anchor event.</param>
		/// <param name="anchorRow">The row of the achor event.</param>
		/// <returns>Tuple where the first value is the chart time and the second value is the row.</returns>
		public abstract (double, double) GetChartTimeAndRowFromPreviousRate(double y, double anchorY, double anchorChartTime, double anchorRow);

		/// <summary>
		/// Gets the Y position in screen space of a given event.
		/// Assumes the event follows the given anchor event.
		/// </summary>
		/// <param name="e">Event to get the position of.</param>
		/// <param name="previousRateEventY">Position in screen space of the previous rate altering event.</param>
		/// <param name="previousRateEvent">The previous rate altering event.</param>
		/// <returns>Y position in screen space of the given event.</returns>
		public abstract double GetY(EditorEvent e, double previousRateEventY, EditorRateAlteringEvent previousRateEvent);

		/// <summary>
		/// Gets the Y position in screen space of a given event.
		/// Assumes the event follows the given anchor event.
		/// </summary>
		/// <param name="e">Event to get the position of.</param>
		/// <param name="anchorY">
		/// Position in screen space of another event to use for relative positioning.
		/// In most cases this is the previous rate altering event.
		/// </param>
		/// <param name="anchorChartTime">The chart time of the anchor event.</param>
		/// <param name="anchorRow">The row of the achor event.</param>
		/// <returns>Y position in screen space of the given event.</returns>
		public abstract double GetY(EditorEvent e, double anchorY, double anchorChartTime, double anchorRow);

		/// <summary>
		/// Gets the Y position in screen space of an event at the given chart time and row values.
		/// Assumes the event follows the given anchor event.
		/// </summary>
		/// <param name="chartTime">Chart time of the event to get the position of.</param>
		/// <param name="chartRow">Row of the event to get the position of.</param>
		/// <param name="anchorY">
		/// Position in screen space of another event to use for relative positioning.
		/// In most cases this is the previous rate altering event.
		/// </param>
		/// <param name="anchorChartTime">The chart time of the anchor event.</param>
		/// <param name="anchorRow">The row of the achor event.</param>
		/// <returns>Y position in screen space of the given event.</returns>
		public abstract double GetY(double chartTime, double chartRow, double anchorY, double anchorChartTime, double anchorRow);

		/// <summary>
		/// Gets the Y position in screen space of an event at the given chart time and row values.
		/// Assumes the event precedes the given anchor event.
		/// </summary>
		/// <param name="chartTime">Chart time of the event to get the position of.</param>
		/// <param name="chartRow">Row of the event to get the position of.</param>
		/// <param name="anchorY">
		/// Position in screen space of another event to use for relative positioning.
		/// In most cases this is the previous rate altering event.
		/// </param>
		/// <param name="anchorChartTime">The chart time of the anchor event.</param>
		/// <param name="anchorRow">The row of the achor event.</param>
		/// <returns>Y position in screen space of the given event.</returns>
		public abstract double GetYPreceding(double chartTime, double chartRow, double anchorY, double anchorChartTime, double anchorRow);

		/// <summary>
		/// Gets the Y position in screen space of an event at the given chart time and row values
		/// relative to the given anchor event.
		/// Assumes the event follows the given anchor event.
		/// </summary>
		/// <param name="relativeTime">Chart time of the event relative to the anchor.</param>
		/// <param name="relativeRow">Row of the event relative to the anchor.</param>
		/// <param name="anchorY">
		/// Position in screen space of another event to use for relative positioning.
		/// In most cases this is the previous rate altering event.
		/// </param>
		/// <returns>Y position in screen space of the given event.</returns>
		public abstract double GetY(double relativeTime, double relativeRow, double anchorY);

		/// <summary>
		/// Gets the position in screen space of the start of a given region.
		/// </summary>
		/// <param name="region">Region to get the position of.</param>
		/// <param name="anchorY">
		/// Position in screen space of another event to use for relative positioning.
		/// In most cases this is the previous rate altering event.
		/// </param>
		/// <param name="anchorChartTime">The chart time of the anchor event.</param>
		/// <param name="anchorRow">The row of the achor event.</param>
		/// <remarks>Expects UpdatePpsAndPpr to have been called to set the current pps and ppr.</remarks>
		/// <returns>Y position in screen space of the start of the given region.</returns>
		public double GetRegionY(IChartRegion region, double anchorY, double anchorChartTime, double anchorRow)
		{
			if (region.AreRegionUnitsTime())
				return anchorY + (region.GetRegionPosition() - anchorChartTime) * Pps;
			return anchorY + (region.GetRegionPosition() - anchorRow) * Ppr;
		}

		/// <summary>
		/// Gets the height in screen space of a given region.
		/// </summary>
		/// <param name="region">Region to get the height of.</param>
		/// <param name="previousRateEventY">Position in screen space of the previous rate altering event.</param>
		/// <param name="previousRateEvent">The previous rate altering event.</param>
		/// <remarks>
		/// Expects UpdatePpsAndPpr to have been called to set the current pps and ppr.
		/// Expects GetRegionY() to report the correct value for the region's y value.
		/// </remarks>
		/// <returns>Height in screen space of the given region.</returns>
		public double GetRegionH(IChartRegion region, double previousRateEventY, EditorRateAlteringEvent previousRateEvent)
		{
			var regionEnd = region.GetRegionPosition() + region.GetRegionDuration();
			if (region.AreRegionUnitsTime())
				return (previousRateEventY + (regionEnd - previousRateEvent.GetChartTime()) * Pps) - region.GetRegionY();
			return (previousRateEventY + (regionEnd - previousRateEvent.GetRow()) * Ppr) - region.GetRegionY();
		}
	}

	/// <summary>
	/// Constant time EventSpacingHelper.
	/// </summary>
	internal sealed class EventSpacingHelperConstantTime : EventSpacingHelper
	{
		public EventSpacingHelperConstantTime(EditorChart activeChart) : base(activeChart) { }

		public override (double, double) GetChartTimeAndRow(double y, double anchorY, double anchorChartTime, double anchorRow)
		{
			// Determine the chart time based on a screen space delta and the pixels per second.
			var chartTime = anchorChartTime + (y - anchorY) / Pps;
			// When deriving the chart position from time we need to take into account that rate
			// altering events may result in a range of positions equating to one time (e.g. during
			// a stop). Leverage the active EditorChart to determine the position from the time.
			var chartPosition = 0.0;
			ActiveChart.TryGetChartPositionFromTime(chartTime, ref chartPosition);
			return (chartTime, chartPosition);
		}

		public override (double, double) GetChartTimeAndRowFromPreviousRate(double y, double anchorY, double anchorChartTime, double anchorRow)
		{
			// Determine the chart time based on a screen space delta and the pixels per second.
			var chartTime = anchorChartTime + (y - anchorY) / PreviousPps;
			// When deriving the chart position from time we need to take into account that rate
			// altering events may result in a range of positions equating to one time (e.g. during
			// a stop). Leverage the active EditorChart to determine the position from the time.
			var chartPosition = 0.0;
			ActiveChart.TryGetChartPositionFromTime(chartTime, ref chartPosition);
			return (chartTime, chartPosition);
		}

		protected override void InternalUpdatePpsAndPpr(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate, double spacingZoom)
		{
			Pps = Preferences.Instance.PreferencesScroll.TimeBasedPixelsPerSecond * spacingZoom;
			Ppr = Pps * rateEvent.SecondsPerRow;
		}

		public override double GetY(EditorEvent e, double previousRateEventY, EditorRateAlteringEvent previousRateEvent)
		{
			return previousRateEventY + (e.GetChartTime() - previousRateEvent.GetChartTime()) * Pps;
		}

		public override double GetY(EditorEvent e, double anchorY, double anchorChartTime, double anchorRow)
		{
			return anchorY + (e.GetChartTime() - anchorChartTime) * Pps;
		}

		public override double GetY(double relativeTime, double relativeRow, double anchorY)
		{
			return anchorY + relativeTime * Pps;
		}

		public override double GetY(double chartTime, double chartRow, double anchorY, double anchorChartTime, double anchorRow)
		{
			return anchorY + (chartTime - anchorChartTime) * Pps;
		}

		public override double GetYPreceding(double chartTime, double chartRow, double anchorY, double anchorChartTime, double anchorRow)
		{
			return anchorY - (chartTime - anchorChartTime) * Pps;
		}
	}

	/// <summary>
	/// Abstract EventSpacingHelper for row-based spacing modes.
	/// </summary>
	internal abstract class EventSpacingHelperRow : EventSpacingHelper
	{
		public EventSpacingHelperRow(EditorChart activeChart) : base(activeChart) { }

		public override (double, double) GetChartTimeAndRow(double y, double anchorY, double anchorChartTime, double anchorRow)
		{
			// Determine the chart position based on a screen space delta and the pixels per row.
			var chartPosition = anchorRow + (y - anchorY) / Ppr;
			// When deriving the chart time from positionwe need to take into account that rate
			// altering events may result in a range of times equating to one position (e.g. during
			// a warp). Leverage the active EditorChart to determine the time from the position.
			var chartTime = 0.0;
			ActiveChart.TryGetTimeFromChartPosition(chartPosition, ref chartTime);
			return (chartTime, chartPosition);
		}

		public override (double, double) GetChartTimeAndRowFromPreviousRate(double y, double anchorY, double anchorChartTime, double anchorRow)
		{
			// Determine the chart position based on a screen space delta and the pixels per row.
			var chartPosition = anchorRow + (y - anchorY) / PreviousPpr;
			// When deriving the chart time from positionwe need to take into account that rate
			// altering events may result in a range of times equating to one position (e.g. during
			// a warp). Leverage the active EditorChart to determine the time from the position.
			var chartTime = 0.0;
			ActiveChart.TryGetTimeFromChartPosition(chartPosition, ref chartTime);
			return (chartTime, chartPosition);
		}

		public override double GetY(EditorEvent e, double previousRateEventY, EditorRateAlteringEvent previousRateEvent)
		{
			return previousRateEventY + (e.GetRow() - previousRateEvent.GetRow()) * Ppr;
		}

		public override double GetY(EditorEvent e, double anchorY, double anchorChartTime, double anchorRow)
		{
			return anchorY + (e.GetRow() - anchorRow) * Ppr;
		}

		public override double GetY(double relativeTime, double relativeRow, double anchorY)
		{
			return anchorY + relativeRow * Ppr;
		}

		public override double GetY(double chartTime, double chartRow, double anchorY, double anchorChartTime, double anchorRow)
		{
			return anchorY + (chartRow - anchorRow) * Ppr;
		}

		public override double GetYPreceding(double chartTime, double chartRow, double anchorY, double anchorChartTime, double anchorRow)
		{
			return anchorY - (chartRow - anchorRow) * Ppr;
		}
	}

	/// <summary>
	/// Constant row EventSpacingHelper.
	/// </summary>
	internal sealed class EventSpacingHelperConstantRow : EventSpacingHelperRow
	{
		public EventSpacingHelperConstantRow(EditorChart activeChart) : base(activeChart) { }

		protected override void InternalUpdatePpsAndPpr(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate, double spacingZoom)
		{
			Ppr = Preferences.Instance.PreferencesScroll.RowBasedPixelsPerRow * spacingZoom;
			Pps = Ppr * rateEvent.RowsPerSecond;
		}
	}

	/// <summary>
	/// Variable row EventSpacingHelper.
	/// </summary>
	internal sealed class EventSpacingHelperVariable : EventSpacingHelperRow
	{
		public EventSpacingHelperVariable(EditorChart activeChart) : base(activeChart) { }

		protected override void InternalUpdatePpsAndPpr(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate, double spacingZoom)
		{
			var scrollRateForThisSection = rateEvent.ScrollRate * interpolatedScrollRate;
			Pps = Preferences.Instance.PreferencesScroll.VariablePixelsPerSecondAtDefaultBPM
					* (rateEvent.Tempo / PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM)
					* scrollRateForThisSection
					* spacingZoom;
			Ppr = Pps * rateEvent.SecondsPerRow;
		}
	}
}
