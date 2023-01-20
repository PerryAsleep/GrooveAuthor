using System;
using System.Collections.Generic;
using static System.Diagnostics.Debug;

namespace StepManiaEditor
{
	/// <summary>
	/// Action to move a selected group of events earlier or later by a given number of rows.
	/// </summary>
	internal sealed class ActionShiftSelectionRow : EditorAction
	{
		/// <summary>
		/// Class to hold all events which were modified as a result of transforming a single event.
		/// </summary>
		private class Transformation
		{
			/// <summary>
			/// The event whose row was transformed.
			/// </summary>
			public EditorEvent Event;
			/// <summary>
			/// All events which were removed and added as a unit for the transformed event.
			/// This is normally just once event, but in the case of holds it is the start and end.
			/// </summary>
			public List<EditorEvent> EventsReAdded;
			/// <summary>
			/// All events which were forcibly deleted as a side effect of moving this event.
			/// </summary>
			public List<EditorEvent> SideEffectDeletions;
			/// <summary>
			/// All events which were forcibly added as a side effect of moving this event.
			/// </summary>
			public List<EditorEvent> SideEffectAdditions;
		}

		private int Rows;
		private Editor Editor;
		private EditorChart Chart;

		/// <summary>
		/// Each individual event transformation.
		/// </summary>
		private List<Transformation> Transformations = new List<Transformation>();
		/// <summary>
		/// All the events which this action will operate on. This is a subset of the provided
		/// events and only includes notes which can be repositioned.
		/// </summary>
		private List<EditorEvent> TransformableEvents;
		/// <summary>
		/// Events which are normally able to be repositioned but could not be moved as part of
		/// this action (e.g. because they would move to an invalid postion). These events will
		/// be deleted in Do and re-added in Undo.
		/// </summary>
		private List<EditorEvent> EventsWhichCouldNotBeTransformed;
		/// <summary>
		/// Events which were actually moved as part of this action. This is a subset of
		/// TransformableEvents. These events will be repositioned in Do and moved back to their
		/// orginal positions in Undo.
		/// </summary>
		private List<EditorEvent> RemainingOriginalEventsAfterTransform;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="editor">Editor instance.</param>
		/// <param name="chart">The Chart containing the events.</param>
		/// <param name="events">The events to change.</param>
		/// <param name="rows">The number of rows to move the given events.</param>
		public ActionShiftSelectionRow(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events, int rows)
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

		public override void Do()
		{
			// When starting a transformation let the Editor know. Changing the positon of notes
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
				// Hold ends are handled through their starts.
				if (editorEvent is EditorHoldEndNoteEvent)
					continue;

				// In the case of holds, setting the new position will update both the hold
				// start and end. We need to treat these two events as a unit for adding or
				// removing.
				var eventTransformedTogether = editorEvent.GetEventsSelectedTogether();

				// If shifting the row would put this event at an invalid position, then
				// remove it.
				var newRow = editorEvent.GetRow() + Rows;
				if (newRow < 0)
				{
					EventsWhichCouldNotBeTransformed.AddRange(eventTransformedTogether);
					continue;
				}

				// Do not allow time signatures to move to non-measure boundaries.
				if (editorEvent is EditorTimeSignatureEvent && !Chart.IsRowOnMeasureBoundary(newRow))
				{
					EventsWhichCouldNotBeTransformed.AddRange(eventTransformedTogether);
					continue;
				}

				// If the event can be moved, update the position.
				editorEvent.SetNewPosition(newRow);

				// Re-add the event to complete the row transformation.
				var (addedFromAlteration, deletedFromAlteration) = Chart.ForceAddEvents(eventTransformedTogether);

				// Record the transformation so that it can be undone.
				Transformations.Add(new Transformation
				{
					Event = editorEvent,
					EventsReAdded = eventTransformedTogether,
					SideEffectAdditions = addedFromAlteration,
					SideEffectDeletions = deletedFromAlteration
				});

				// Record the transformed events. When we undo, we will need to know which events
				// were successfully transformed (as opposed to removed) so we can undo them.
				RemainingOriginalEventsAfterTransform.AddRange(eventTransformedTogether);
			}

			// Notify the Editor the transformation is complete. Only supply the transformed events
			// to the Editor. We do not want to supply deleted events.
			Editor.OnNoteTransformationEnd(RemainingOriginalEventsAfterTransform);
		}

		public override void Undo()
		{
			// When starting a transformation let the Editor know. Changing the positon of notes
			// requires deleting them and re-adding them after they have been altered. The editor
			// may need to differentiate a normal deletion from a temporary deletion as part of a
			// move.
			Editor.OnNoteTransformationBegin();

			// Remove the events that were successfully moved as part of doing the original action.
			var allDeletedEvents = Chart.DeleteEvents(RemainingOriginalEventsAfterTransform);
			Assert(allDeletedEvents.Count == RemainingOriginalEventsAfterTransform.Count);

			// Undo each transformation.
			foreach(var transformation in Transformations)
			{
				if (transformation.SideEffectAdditions.Count > 0)
				{
					var deletedEvents = Chart.DeleteEvents(transformation.SideEffectAdditions);
					Assert(deletedEvents.Count == transformation.SideEffectAdditions.Count);
				}
				if (transformation.SideEffectDeletions.Count > 0)
				{
					// Reset the times of these events before adding them back.
					// It could be the case that the events being moved as part of this action contain
					// multiple rate altering events. If one altered this event's time when it moved and
					// then a subsequently moved event deleted this event, then it's time will be incorrect
					// at this point since it was not in the event tree when we re-added the unmodified event
					// which changed it's time originally.
					foreach (var deletedEvent in transformation.SideEffectDeletions)
						deletedEvent.ResetTimeBasedOnRow();
					Chart.AddEvents(transformation.SideEffectDeletions);
				}

				// Undo the transformation.
				var newRow = transformation.Event.GetRow() - Rows;
				Assert(newRow >= 0);
				transformation.Event.SetNewPosition(newRow);

				// Re-add the transformed event.
				Chart.AddEvents(transformation.EventsReAdded);
			}

			// Add back the original events which could not be transformed originally.
			Chart.AddEvents(EventsWhichCouldNotBeTransformed);

			// Notify the Editor the transformation is complete. Supply all events.
			Editor.OnNoteTransformationEnd(TransformableEvents);
		}
	}
}
