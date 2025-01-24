using System;
using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to duplicate a set of events and paste them at a specified location.
/// </summary>
internal class ActionPasteEvents : EditorAction
{
	private readonly int Rows;
	private readonly Editor Editor;
	private readonly EditorChart Chart;

	/// <summary>
	/// Whether or not the original events have been cloned and transformed.
	/// </summary>
	private bool EventsHaveBeenTransformed;

	/// <summary>
	/// Side effects for pasting each event.
	/// </summary>
	private List<ForceAddSideEffect> SideEffects = new();

	/// <summary>
	/// All the events which this action will operate on.
	/// </summary>
	private readonly List<EditorEvent> OriginalEvents;

	/// <summary>
	/// Events which were actually pasted as part of this action. This is a subset of OriginalEvents.
	/// </summary>
	private List<EditorEvent> PastedEvents;

	/// <summary>
	/// Whether or not all original events are for the same player.
	/// </summary>
	private readonly bool AllOriginalEventsForSamePlayer;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">The Chart containing the events.</param>
	/// <param name="events">The copied events to paste.</param>
	/// <param name="rows">The number of rows to move the copied events by.</param>
	public ActionPasteEvents(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events, int rows) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		Rows = rows;

		// Copy the given events so we can operate on them without risk of the caller
		// modifying the provided data structure.
		OriginalEvents = new List<EditorEvent>();
		int? previousPlayer = null;
		AllOriginalEventsForSamePlayer = true;
		foreach (var chartEvent in events)
		{
			if (AllOriginalEventsForSamePlayer && chartEvent.IsLaneNote())
			{
				var player = chartEvent.GetPlayer();
				if (previousPlayer != null && previousPlayer != player)
					AllOriginalEventsForSamePlayer = false;
				previousPlayer = player;
			}

			OriginalEvents.Add(chartEvent);
		}

		OriginalEvents.Sort();
	}

	public override string ToString()
	{
		var dir = Rows > 0 ? "Later" : "Earlier";
		return $"Paste Notes {Math.Abs(Rows)} Rows {dir}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		// When starting a transformation let the Editor know.
		Editor.OnNoteTransformationBegin();

		if (!EventsHaveBeenTransformed)
			PasteUntransformedEvents();
		else
			PasteTransformedEvents();

		// Notify the Editor the transformation is complete. Only supply the pasted events.
		Editor.OnNoteTransformationEnd(PastedEvents);
	}

	/// <summary>
	/// Paste the untransformed events by transforming them, then adding them.
	/// We cannot simply transform them all ahead of time, because one pasted event
	/// may affect the timing of a later pasted event, and we leverage the EditorChart
	/// for determining event timing, which requires the having updated events in its
	/// tree for calculating times. Once we transform and paste, we can simply remove
	/// and re-add the transformed events on future undos and redos.
	/// </summary>
	private void PasteUntransformedEvents()
	{
		// Set up lists to hold the events in various states to support undo and redo.
		SideEffects = new List<ForceAddSideEffect>();
		PastedEvents = new List<EditorEvent>(OriginalEvents.Count);

		var destinationChartPlayer = Editor.GetPlayer(Chart);

		// Update each event.
		foreach (var editorEvent in OriginalEvents)
		{
			// Do not paste any events which cannot exist at the pasted row.
			var newRow = editorEvent.GetRow() + Rows;
			if (!Chart.CanEventExistAtRow(editorEvent, newRow))
				continue;

			// Clone the event, and set the new position. Cloned events are not selected.
			var newEvent = editorEvent.Clone(Chart);
			newEvent.SetRow(newRow);

			// Ensure the player is valid for the destination chart.
			if (newEvent.IsLaneNote())
			{
				// If all the original charts are for the same player, then use the destination chart's current player.
				if (AllOriginalEventsForSamePlayer)
					newEvent.SetPlayer(destinationChartPlayer);
				// Otherwise clamp the player.
				else if (newEvent.GetPlayer() >= Chart.MaxPlayers)
					newEvent.SetPlayer(0);
			}

			// Add the new event and record the side effects so they can be undone.
			var (addedFromAlteration, deletedFromAlteration) = Chart.ForceAddEvent(newEvent);
			if (addedFromAlteration?.Count > 0 || deletedFromAlteration?.Count > 0)
				SideEffects.Add(new ForceAddSideEffect(addedFromAlteration, deletedFromAlteration));

			// Record the new event. When we undo, we will need to know which events
			// were successfully transformed (as opposed to removed) so we can undo them.
			PastedEvents.Add(newEvent);
		}

		Chart.ForceAddEventsComplete(PastedEvents);

		// Record that the pasted events have been transformed so we can avoid re-transforming them when
		// redoing the action.
		EventsHaveBeenTransformed = true;
	}

	/// <summary>
	/// If the events have already been transformed, we can simply add them again.
	/// </summary>
	private void PasteTransformedEvents()
	{
		// Set up a new Transformations list to hold the results of re-adding the transformed events.
		SideEffects = new List<ForceAddSideEffect>();

		// Re-add each already transformed event.
		foreach (var editorEvent in PastedEvents)
		{
			var (addedFromAlteration, deletedFromAlteration) = Chart.ForceAddEvent(editorEvent);

			// Record the side effects so that they can be undone.
			if (addedFromAlteration?.Count > 0 || deletedFromAlteration?.Count > 0)
				SideEffects.Add(new ForceAddSideEffect(addedFromAlteration, deletedFromAlteration));
		}

		Chart.ForceAddEventsComplete(PastedEvents);
	}

	protected override void UndoImplementation()
	{
		// When starting a transformation let the Editor know.
		Editor.OnNoteTransformationBegin();

		// Remove the events that were successfully pasted as part of doing the original action.
		Chart.DeleteEvents(PastedEvents);

		// Undo each of the side effects.
		foreach (var sideEffect in SideEffects)
			sideEffect.Undo(Chart);

		// Notify the Editor the transformation is complete. Supply all events.
		Editor.OnNoteTransformationEnd(OriginalEvents);
	}
}
