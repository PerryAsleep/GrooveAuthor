using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to delete notes in the ranges of EditorPatternEvents.
/// </summary>
internal sealed class ActionDeletePatternNotes : EditorAction
{
	private readonly EditorChart EditorChart;
	private readonly List<EditorPatternEvent> Patterns;
	private readonly List<EditorEvent> DeletedEvents = new();

	public ActionDeletePatternNotes(
		EditorChart editorChart,
		IEnumerable<EditorPatternEvent> allPatterns) : base(false, false)
	{
		EditorChart = editorChart;
		Patterns = new List<EditorPatternEvent>();
		Patterns.AddRange(allPatterns);
	}

	public override string ToString()
	{
		if (Patterns.Count == 1)
			return $"Delete notes from Pattern at row {Patterns[0].ChartRow}.";
		return $"Delete notes from {Patterns.Count} Patterns.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		if (DeletedEvents.Count > 0)
		{
			EditorChart.DeleteEvents(DeletedEvents);
			return;
		}

		DeletedEvents.AddRange(DeleteEventsOverlappingPatterns(EditorChart, Patterns));
	}

	protected override void UndoImplementation()
	{
		EditorChart.AddEvents(DeletedEvents);
	}

	/// <summary>
	/// Deletes all EditorEvents in the given EditorChart which intersect any of the given Patterns.
	/// </summary>
	/// <returns>All deleted EditorEvents.</returns>
	public static List<EditorEvent> DeleteEventsOverlappingPatterns(
		EditorChart editorChart,
		IEnumerable<EditorPatternEvent> patterns)
	{
		var deletedEvents = new List<EditorEvent>();
		foreach (var pattern in patterns)
		{
			var deletedEventsForPattern = new List<EditorEvent>();
			var startRow = pattern.GetRow();
			var endRow = pattern.GetEndRow();

			// Accumulate any holds which overlap the start of the pattern.
			var overlappingHolds = editorChart.GetHoldsOverlapping(startRow);
			foreach (var overlappingHold in overlappingHolds)
			{
				if (overlappingHold != null)
				{
					deletedEventsForPattern.Add(overlappingHold);
				}
			}

			// Accumulate taps, holds, and mines which fall within the pattern region.
			var enumerator = editorChart.GetEvents().FindBestByPosition(startRow);
			if (enumerator != null && enumerator.MoveNext())
			{
				var row = enumerator.Current!.GetRow();
				while (row <= endRow)
				{
					if (row >= startRow &&
					    enumerator.Current is EditorTapNoteEvent or EditorMineNoteEvent or EditorHoldNoteEvent
						    or EditorFakeNoteEvent or EditorLiftNoteEvent)
					{
						if (row < endRow || (pattern.EndPositionInclusive && row == endRow))
						{
							deletedEventsForPattern.Add(enumerator.Current);
						}
					}

					if (!enumerator.MoveNext())
						break;
					row = enumerator.Current.GetRow();
				}
			}

			// Delete the events now rather than waiting to accumulate all events.
			// These prevents accidentally trying to delete the same event more than once
			// when patterns overlap.
			deletedEvents.AddRange(editorChart.DeleteEvents(deletedEventsForPattern));
		}

		return deletedEvents;
	}
}
