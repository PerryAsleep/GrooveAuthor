using System;
using System.Collections.Generic;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

/// <summary>
/// Action to move a selected group of events earlier or later by a given number of rows.
/// </summary>
internal sealed class ActionShiftSelectionRow : EditorAction
{
	/// <summary>
	/// Class to hold all events which were modified as a result of transforming a single event.
	/// </summary>
	private sealed class Transformation
	{
		/// <summary>
		/// The event whose row was transformed.
		/// </summary>
		public readonly EditorEvent Event;

		/// <summary>
		/// Side effect of adding the event.
		/// </summary>
		public readonly ForceAddSideEffect SideEffect;

		public Transformation(EditorEvent editorEvent, List<EditorEvent> additions, List<EditorEvent> deletions)
		{
			Event = editorEvent;
			SideEffect = new ForceAddSideEffect(additions, deletions);
		}
	}

	private readonly int Rows;
	private readonly Editor Editor;
	private readonly EditorChart Chart;

	/// <summary>
	/// Each individual event transformation.
	/// </summary>
	private List<Transformation> Transformations = new();

	/// <summary>
	/// All the events which this action will operate on. This is a subset of the provided
	/// events and only includes notes which can be repositioned.
	/// </summary>
	private readonly List<EditorEvent> TransformableEvents;

	/// <summary>
	/// Events which are normally able to be repositioned but could not be moved as part of
	/// this action (e.g. because they would move to an invalid position). These events will
	/// be deleted in Do and re-added in Undo.
	/// </summary>
	private List<EditorEvent> EventsWhichCouldNotBeTransformed;

	/// <summary>
	/// Events which were actually moved as part of this action. This is a subset of
	/// TransformableEvents. These events will be repositioned in Do and moved back to their
	/// original positions in Undo.
	/// </summary>
	private List<EditorEvent> RemainingOriginalEventsAfterTransform;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">The Chart containing the events.</param>
	/// <param name="events">The events to change.</param>
	/// <param name="rows">The number of rows to move the given events.</param>
	public ActionShiftSelectionRow(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events, int rows) : base(false,
		false)
	{
		Editor = editor;
		Chart = chart;
		Rows = rows;

		// Copy the given events so we can operate on them without risk of the caller
		// modifying the provided data structure. We also only want to attempt to move
		// events which can be repositioned. Certain events (like the first tempo) we
		// just ignore.
		TransformableEvents = new List<EditorEvent>();
		foreach (var chartEvent in events)
		{
			if (!chartEvent.CanBeRepositioned())
				continue;
			TransformableEvents.Add(chartEvent);
		}

		TransformableEvents.Sort();
	}

	public override string ToString()
	{
		var dir = Rows > 0 ? "Later" : "Earlier";
		return $"Shift Notes {Math.Abs(Rows)} Rows {dir}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		// When starting a transformation let the Editor know. Changing the position of notes
		// requires deleting them and re-adding them after they have been altered. The editor
		// may need to differentiate a normal deletion from a temporary deletion as part of a
		// move.
		Editor.OnNoteTransformationBegin();

		// Remove all events to be transformed.
		var allDeletedEvents = Chart.DeleteEvents(TransformableEvents);
		Assert(allDeletedEvents.Count == TransformableEvents.Count);

		// Set up lists to hold the events in various states to support undo and redo.
		Transformations = new List<Transformation>();
		RemainingOriginalEventsAfterTransform = new List<EditorEvent>();
		EventsWhichCouldNotBeTransformed = new List<EditorEvent>();

		// Update each event.
		foreach (var editorEvent in TransformableEvents)
		{
			// If shifting the row would put this event at an invalid position, then remove it.
			var newRow = editorEvent.GetRow() + Rows;
			if (!Chart.CanEventExistAtRow(editorEvent, newRow))
				continue;

			// If the event can be moved, update the position.
			editorEvent.SetNewPosition(newRow);

			// Re-add the event to complete the row transformation.
			var (addedFromAlteration, deletedFromAlteration) = Chart.ForceAddEvents(new List<EditorEvent> { editorEvent });

			// Record the transformation so that it can be undone.
			Transformations.Add(new Transformation(editorEvent, addedFromAlteration, deletedFromAlteration));

			// Record the transformed events. When we undo, we will need to know which events
			// were successfully transformed (as opposed to removed) so we can undo them.
			RemainingOriginalEventsAfterTransform.Add(editorEvent);
		}

		// Notify the Editor the transformation is complete. Only supply the transformed events
		// to the Editor. We do not want to supply deleted events.
		Editor.OnNoteTransformationEnd(RemainingOriginalEventsAfterTransform);
	}

	protected override void UndoImplementation()
	{
		// When starting a transformation let the Editor know. Changing the position of notes
		// requires deleting them and re-adding them after they have been altered. The editor
		// may need to differentiate a normal deletion from a temporary deletion as part of a
		// move.
		Editor.OnNoteTransformationBegin();

		// Remove the events that were successfully moved as part of doing the original action.
		var allDeletedEvents = Chart.DeleteEvents(RemainingOriginalEventsAfterTransform);
		Assert(allDeletedEvents.Count == RemainingOriginalEventsAfterTransform.Count);

		// Undo each transformation.
		foreach (var transformation in Transformations)
		{
			// Undo the side effects of transforming the event.
			transformation.SideEffect.Undo(Chart);

			// Undo the transformation.
			var newRow = transformation.Event.GetRow() - Rows;
			Assert(newRow >= 0);
			transformation.Event.SetNewPosition(newRow);

			// Re-add the transformed event.
			Chart.AddEvent(transformation.Event);
		}

		// Add back the original events which could not be transformed originally.
		Chart.AddEvents(EventsWhichCouldNotBeTransformed);

		// Notify the Editor the transformation is complete. Supply all events.
		Editor.OnNoteTransformationEnd(TransformableEvents);
	}
}
