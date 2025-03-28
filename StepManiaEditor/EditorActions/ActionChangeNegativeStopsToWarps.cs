using System;
using System.Collections.Generic;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Action to change negative stops to warps.
/// Note that overlapping warps do not stack and overlapping stops do.
/// This action will not take any steps to try and stack or unstack any potentially overlapping events.
/// </summary>
internal sealed class ActionChangeNegativeStopsToWarps : EditorAction
{
	private readonly List<EditorEvent> OriginalEvents;
	private readonly List<EditorEvent> NewEvents;
	private readonly Editor Editor;
	private readonly EditorChart Chart;

	/// <summary>
	/// Constructor for converting all of a chart's negative stops.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">EditorChart containing the negative stops.</param>
	public ActionChangeNegativeStopsToWarps(
		Editor editor,
		EditorChart chart) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		OriginalEvents = [];
		NewEvents = [];

		foreach (var stop in chart.GetStops())
		{
			if (stop.GetStopLengthSeconds() < 0.0)
			{
				OriginalEvents.Add(stop);
			}
		}
	}

	/// <summary>
	/// Constructor for converting only the negative stops in the given events.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">EditorChart containing the negative stops.</param>
	/// <param name="events">Events containing negative stops to convert.</param>
	public ActionChangeNegativeStopsToWarps(
		Editor editor,
		EditorChart chart,
		IEnumerable<EditorEvent> events) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		OriginalEvents = [];
		NewEvents = [];
		foreach (var editorEvent in events)
		{
			if (editorEvent is EditorStopEvent stop && stop.GetStopLengthSeconds() < 0.0)
				OriginalEvents.Add(stop);
		}
	}

	public override string ToString()
	{
		return $"Convert {OriginalEvents.Count} Negative Stops to Warps.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	private bool CanNegativeStopBeChangedToWarp(EditorStopEvent stop)
	{
		// Allow negative stops on rows without warps to be converted to warps.
		return Chart.GetRateAlteringEvents().FindEventAtRow<EditorWarpEvent>(stop.GetRow()) == null;
	}

	protected override void DoImplementation()
	{
		Editor?.OnNoteTransformationBegin();

		// First delete the stops.
		Chart.DeleteEvents(OriginalEvents);

		// Convert each warp one at a time.
		var previousWarpEndRow = 0;
		foreach (var stopEvent in OriginalEvents)
		{
			var stop = (EditorStopEvent)stopEvent;
			// This O(log(N)) but it is better to make sure we don't try to add
			// a warp on a row with another warp.
			if (!CanNegativeStopBeChangedToWarp(stop))
			{
				Logger.Warn(
					$"Negative stop at row {stop.GetRow()} cannot be replaced with a warp as there is already a warp present.");
				continue;
			}

			if (previousWarpEndRow > stop.GetRow())
			{
				Logger.Warn($"Negative stop at row {stop.GetRow()} overlaps with a previous stop. " +
				            "Overlapping warps do not stock but overlapping stops do. " +
				            "You should manually inspect the warp and make needed adjustments to its length.");
			}

			// Convert stop time to warp length in rows.
			var startTime = stop.GetChartTime();
			var endTime = startTime + -1 * stop.GetStopLengthSeconds();
			var endChartPosition = 0.0;
			Chart.TryGetChartPositionFromTime(endTime, ref endChartPosition);
			var warpEndRow = (int)Math.Round(endChartPosition);
			var warpLength = Math.Max(0, warpEndRow - stop.GetRow());

			// Create a warp from the length.
			var warp = EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(Chart, stop.GetRow(), warpLength));
			NewEvents.Add(warp);

			// Add the stop. Adding this will affect the time of future stops to be added so we do this
			// one at a time.
			Chart.AddEvent(warp);

			previousWarpEndRow = Math.Max(warpEndRow, previousWarpEndRow);
		}

		Editor?.OnNoteTransformationEnd(NewEvents);
	}

	protected override void UndoImplementation()
	{
		Editor?.OnNoteTransformationBegin();
		Chart.DeleteEvents(NewEvents);
		Chart.AddEvents(OriginalEvents);
		Editor?.OnNoteTransformationEnd(OriginalEvents);
	}
}
