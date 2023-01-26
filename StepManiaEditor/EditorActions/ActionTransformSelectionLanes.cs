using StepManiaLibrary;
using System.Collections.Generic;
using static System.Diagnostics.Debug;

namespace StepManiaEditor
{
	/// <summary>
	/// Absctract action to transform the lanes of the given events.
	/// </summary>
	internal abstract class ActionTransformSelectionLanes : EditorAction
	{
		private Editor Editor;
		protected EditorChart Chart;

		private List<EditorEvent> TransformableEvents;
		private List<EditorEvent> RemainingOriginalEventsAfterTransform;
		private List<EditorEvent> DeletedFromAlteration;
		private List<EditorEvent> AddedFromAlteration;

		public ActionTransformSelectionLanes(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
		{
			Editor = editor;
			Chart = chart;

			// Copy the given events so we can operate on them without risk of the caller
			// modifying the provided data structure. We also only want to attempt to transform
			// events which can have their lanes altered. Certain events (like rate altering
			// events) we just ignore.
			var padData = Editor.GetPadData(Chart.ChartType);
			TransformableEvents = new List<EditorEvent>();
			if (padData != null)
			{
				foreach (var chartEvent in events)
				{
					if (!CanTransform(chartEvent, padData))
						continue;
					TransformableEvents.Add(chartEvent);
				}
				TransformableEvents.Sort();
			}
		}

		public override bool AffectsFile()
		{
			return true;
		}

		/// <summary>
		/// Returns whether or not the given event can be transformed.
		/// This returns true if the transformation makes sense for the given event, even if
		/// it would have no effect. For example, mirroring an event makes sense for a lane
		/// note, even if there are an odd number of lanes and the event in question is in
		/// the middle lane and will end up in the same spot. Mirroring does not make sense
		/// for events which are not lane notes because mirroring only changes event lanes.
		/// </summary>
		/// <param name="e">Event to check.</param>
		/// <param name="padData">PadData for the event's chart.</param>
		/// <returns>Whether or not the given event can be transformed.</returns>
		protected abstract bool CanTransform(EditorEvent e, PadData padData);

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

		public override void Do()
		{
			var padData = Editor.GetPadData(Chart.ChartType);
			if (padData == null)
				return;

			// When starting a transformation let the Editor know.
			Editor.OnNoteTransformationBegin();

			// Remove all events to be transformed.
			var allDeletedEvents = Chart.DeleteEvents(TransformableEvents);
			Assert(allDeletedEvents.Count == TransformableEvents.Count);

			// Transform events.
			RemainingOriginalEventsAfterTransform = new List<EditorEvent>();
			foreach (var editorEvent in TransformableEvents)
			{
				if (DoTransform(editorEvent, padData))
				{
					RemainingOriginalEventsAfterTransform.Add(editorEvent);
				}
			}

			// Add the events back, storing the side effects.
			(AddedFromAlteration, DeletedFromAlteration) = Chart.ForceAddEvents(RemainingOriginalEventsAfterTransform);

			// Notify the Editor the transformation is complete.
			Editor.OnNoteTransformationEnd(RemainingOriginalEventsAfterTransform);
		}

		public override void Undo()
		{
			// When starting a transformation let the Editor know.
			Editor.OnNoteTransformationBegin();

			// Remove the transformed events.
			var allDeletedEvents = Chart.DeleteEvents(RemainingOriginalEventsAfterTransform);
			Assert(allDeletedEvents.Count == RemainingOriginalEventsAfterTransform.Count);

			// While the transformed events are removed, delete the events which
			// were added as a side effect.
			if (AddedFromAlteration.Count > 0)
			{
				allDeletedEvents = Chart.DeleteEvents(AddedFromAlteration);
				Assert(allDeletedEvents.Count == AddedFromAlteration.Count);
			}

			// While the transformed events are removed, add the events which
			// were deleted as a side effect.
			if (DeletedFromAlteration.Count > 0)
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
}
