using System;
using System.Collections.Generic;
using StepManiaLibrary;

namespace StepManiaEditor;

/// <summary>
/// Abstract action to transform the lanes of the given events.
/// </summary>
internal abstract class ActionTransformSelectionLanes : EditorAction
{
	private readonly Editor Editor;
	private readonly List<EditorEvent> TransformableEvents;
	protected readonly EditorChart Chart;

	private List<EditorEvent> RemainingOriginalEventsAfterTransform;
	private List<EditorEvent> DeletedFromAlteration;
	private List<EditorEvent> AddedFromAlteration;

	protected ActionTransformSelectionLanes(
		Editor editor,
		EditorChart chart,
		IEnumerable<EditorEvent> events,
		Func<EditorEvent, PadData, bool> canTransform) : base(false, false)
	{
		Editor = editor;
		Chart = chart;

		// Copy the given events so we can operate on them without risk of the caller
		// modifying the provided data structure. We also only want to attempt to transform
		// events which can have their lanes altered. Certain events (like rate altering
		// events) we just ignore.
		var padData = Editor.GetPadData(Chart.ChartType);
		TransformableEvents = [];
		if (padData != null)
		{
			foreach (var chartEvent in events)
			{
				if (!canTransform(chartEvent, padData))
					continue;
				TransformableEvents.Add(chartEvent);
			}

			TransformableEvents.Sort();
		}
	}

	/// <summary>
	/// Transform an event.
	/// Subclasses must implement this method. Implementations should return true
	/// only if the event is still valid for the chart. If the event is no longer
	/// valid for the chart after transformation then implementations should return
	/// false to indicate the event should be deleted. In this scenario it is expected
	/// that implementations to not mutate the event.
	/// </summary>
	/// <param name="e">Event to transform.</param>
	/// <param name="padData">PadData for the event's chart.</param>
	/// <returns>
	/// True if the note was altered and is still valid for the chart.
	/// False if the note was not altered and should be removed from the chart.
	/// </returns>
	protected abstract bool DoTransform(EditorEvent e, PadData padData);

	/// <summary>
	/// Undo the transform for an event.
	/// This will be invoked on every event for which DoTransform returned true.
	/// </summary>
	/// <param name="e">Event to undo the transform of.</param>
	/// <param name="padData">PadData for the event's chart.</param>
	protected abstract void UndoTransform(EditorEvent e, PadData padData);

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		var padData = Editor.GetPadData(Chart.ChartType);
		if (padData == null)
			return;

		// When starting a transformation let the Editor know.
		Editor.OnNoteTransformationBegin();

		// Remove all events to be transformed.
		Chart.DeleteEvents(TransformableEvents);

		// Transform events.
		RemainingOriginalEventsAfterTransform = [];
		foreach (var editorEvent in TransformableEvents)
		{
			if (DoTransform(editorEvent, padData))
			{
				RemainingOriginalEventsAfterTransform.Add(editorEvent);
			}
		}

		RemainingOriginalEventsAfterTransform.Sort();

		// Add the events back, storing the side effects.
		(AddedFromAlteration, DeletedFromAlteration) = Chart.ForceAddEvents(RemainingOriginalEventsAfterTransform);
		Chart.ForceAddEventsComplete(RemainingOriginalEventsAfterTransform);

		// Notify the Editor the transformation is complete.
		Editor.OnNoteTransformationEnd(RemainingOriginalEventsAfterTransform);
	}

	protected override void UndoImplementation()
	{
		// When starting a transformation let the Editor know.
		Editor.OnNoteTransformationBegin();

		// Remove the transformed events.
		Chart.DeleteEvents(RemainingOriginalEventsAfterTransform);

		// While the transformed events are removed, delete the events which
		// were added as a side effect.
		if (AddedFromAlteration?.Count > 0)
		{
			Chart.DeleteEvents(AddedFromAlteration);
		}

		// While the transformed events are removed, add the events which
		// were deleted as a side effect.
		if (DeletedFromAlteration?.Count > 0)
		{
			Chart.AddEvents(DeletedFromAlteration);
		}

		// Undo the transformation on each event.
		var padData = Editor.GetPadData(Chart.ChartType);
		foreach (var editorEvent in RemainingOriginalEventsAfterTransform)
		{
			UndoTransform(editorEvent, padData);
		}

		// Add the events back.
		Chart.AddEvents(TransformableEvents);

		// Notify the Editor the transformation is complete.
		Editor.OnNoteTransformationEnd(TransformableEvents);
	}
}
