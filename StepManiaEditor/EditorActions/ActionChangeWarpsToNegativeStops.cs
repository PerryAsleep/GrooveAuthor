using System;
using System.Collections.Generic;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Action to change warps to negative stops.
/// Note that overlapping warps do not stack and overlapping stops do.
/// This action will not take any steps to try and stack or unstack any potentially overlapping events.
/// </summary>
internal sealed class ActionChangeWarpsToNegativeStops : EditorAction
{
	private readonly List<EditorEvent> OriginalEvents;
	private readonly List<EditorEvent> NewEvents;
	private readonly Editor Editor;
	private readonly EditorChart Chart;

	/// <summary>
	/// Constructor for converting all of a chart's warps.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">EditorChart containing the warps.</param>
	public ActionChangeWarpsToNegativeStops(
		Editor editor,
		EditorChart chart) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		OriginalEvents = new List<EditorEvent>();
		NewEvents = new List<EditorEvent>();
		OriginalEvents.AddRange(chart.GetWarps());
	}

	/// <summary>
	/// Constructor for converting only the warps in the given events.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">EditorChart containing the warps.</param>
	/// <param name="events">Events containing warps to convert.</param>
	public ActionChangeWarpsToNegativeStops(
		Editor editor,
		EditorChart chart,
		IEnumerable<EditorEvent> events) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		OriginalEvents = new List<EditorEvent>();
		NewEvents = new List<EditorEvent>();
		foreach (var editorEvent in events)
		{
			if (editorEvent is EditorWarpEvent warp)
				OriginalEvents.Add(warp);
		}
	}

	public override string ToString()
	{
		return $"Convert {OriginalEvents.Count} Warps to Negative Stops.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	private bool CanWarpBeChangedToNegativeStop(EditorWarpEvent warp)
	{
		// Allow warps on rows without stops to be converted to negative stops.
		return Chart.GetRateAlteringEvents().FindEventAtRow<EditorStopEvent>(warp.GetRow()) == null;
	}

	protected override void DoImplementation()
	{
		Editor?.OnNoteTransformationBegin();

		// First delete the warps. We need to do this so we can see how much time they warp over.
		// If we were to try to determine that before deleting them they would cover 0.0 time.
		Chart.DeleteEvents(OriginalEvents);

		// Convert each warp one at a time.
		var previousWarpEnd = 0;
		foreach (var warp in OriginalEvents)
		{
			// This O(log(N)) but it is better to make sure we don't try to add
			// a stop on a row with another stop.
			if (!CanWarpBeChangedToNegativeStop((EditorWarpEvent)warp))
			{
				Logger.Warn(
					$"Warp at row {warp.GetRow()} cannot be replaced with a negative stop as there is already a stop present.");
				continue;
			}

			if (previousWarpEnd > warp.GetRow())
			{
				Logger.Warn($"Warp at row {warp.GetRow()} overlaps with a previous warp. " +
				            "Overlapping warps do not stock but overlapping stops do. " +
				            "You should manually inspect the negative stop and make needed adjustments to its time.");
			}

			// Convert the warp row length to a stop time.
			var startTime = warp.GetChartTime();
			var endRow = warp.GetEndRow();
			var stopEndTime = 0.0;
			Chart.TryGetTimeFromChartPosition(endRow, ref stopEndTime);
			var stopTime = -1 * (stopEndTime - startTime);

			// Create a negative stop from the time.
			var stop = EditorEvent.CreateEvent(EventConfig.CreateStopConfig(Chart, warp.GetRow(), stopTime));
			NewEvents.Add(stop);

			// Add the stop. Adding this will affect the time of future stops to be added so we do this
			// one at a time.
			Chart.AddEvent(stop);

			previousWarpEnd = Math.Max(endRow, previousWarpEnd);
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
