using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to delete notes in the ranges of EditorPatternEvents.
/// </summary>
internal sealed class ActionDeletePatternNotes : EditorAction
{
	/// <summary>
	/// Class to hold all alterations from deleting the events in a pattern region.
	/// Includes deleted events and shortened holds.
	/// </summary>
	public class Alterations
	{
		/// <summary>
		/// Shortened hold information.
		/// </summary>
		public class ShortenedHold
		{
			public readonly int OldLength;
			public readonly int NewLength;
			public EditorHoldNoteEvent Hold;

			public ShortenedHold(int oldLength, int newLength, EditorHoldNoteEvent hold)
			{
				OldLength = oldLength;
				NewLength = newLength;
				Hold = hold;
			}

			public void Undo()
			{
				Hold.SetLength(OldLength);
			}

			public void Redo()
			{
				Hold.SetLength(NewLength);
			}
		}

		public readonly List<EditorEvent> DeletedEvents;
		public readonly List<ShortenedHold> ShortenedHolds;

		public Alterations(List<EditorEvent> deletedEvents, List<ShortenedHold> shortenedHolds)
		{
			DeletedEvents = deletedEvents;
			ShortenedHolds = shortenedHolds;
		}

		public void Undo(EditorChart editorChart)
		{
			if (DeletedEvents.Count > 0)
				editorChart.AddEvents(DeletedEvents);
			foreach (var hold in ShortenedHolds)
				hold.Undo();
		}

		public void Redo(EditorChart editorChart)
		{
			if (DeletedEvents.Count > 0)
				editorChart.DeleteEvents(DeletedEvents);
			foreach (var hold in ShortenedHolds)
				hold.Redo();
		}
	}

	private readonly EditorChart EditorChart;
	private readonly List<EditorPatternEvent> Patterns;
	private Alterations ActionAlterations;

	public ActionDeletePatternNotes(
		EditorChart editorChart,
		IEnumerable<EditorPatternEvent> allPatterns) : base(false, false)
	{
		EditorChart = editorChart;
		Patterns = new List<EditorPatternEvent>();
		Patterns.AddRange(allPatterns);
		Patterns.Sort();
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
		if (ActionAlterations != null)
		{
			ActionAlterations.Redo(EditorChart);
			return;
		}

		ActionAlterations = DeleteEventsOverlappingPatterns(EditorChart, Patterns);
	}

	protected override void UndoImplementation()
	{
		ActionAlterations.Undo(EditorChart);
	}

	/// <summary>
	/// Deletes all EditorEvents in the given EditorChart which intersect any of the given Patterns.
	/// Shortens holds which precede the patterns but overlap them.
	/// </summary>
	/// <returns>Alterations representing all changes.</returns>
	public static Alterations DeleteEventsOverlappingPatterns(
		EditorChart editorChart,
		IEnumerable<EditorPatternEvent> patterns)
	{
		var deletedEvents = new List<EditorEvent>();
		var shortenedHolds = new List<Alterations.ShortenedHold>();
		foreach (var pattern in patterns)
		{
			if (pattern.GetNumSteps() <= 0)
				continue;

			var deletedEventsForPattern = new List<EditorEvent>();
			var startRow = pattern.GetFirstStepRow();
			var endRow = pattern.GetLastStepRow();

			// Accumulate any holds which overlap the start of the pattern.
			var overlappingHolds = editorChart.GetHoldsOverlapping(startRow);
			foreach (var overlappingHold in overlappingHolds)
			{
				if (overlappingHold != null)
				{
					var holdStart = overlappingHold.GetRow();

					// This hold starts before the pattern and can be cut short.
					if (holdStart < startRow - 1)
					{
						var desiredEnd = startRow - pattern.GetStepSpacing();
						var newLength = desiredEnd - holdStart;
						if (newLength < 1)
							newLength = startRow - holdStart - 1;
						shortenedHolds.Add(new Alterations.ShortenedHold(overlappingHold.GetLength(), newLength,
							overlappingHold));
						overlappingHold.SetLength(newLength);
					}

					// This hold starts within the pattern and needs to be deleted.
					else
					{
						deletedEventsForPattern.Add(overlappingHold);
					}
				}
			}

			// Accumulate taps, holds, and mines which fall within the pattern region.
			var enumerator = editorChart.GetEvents().FindBestByPosition(startRow);
			if (enumerator != null && enumerator.MoveNext())
			{
				var row = enumerator.Current!.GetRow();
				while (row <= endRow)
				{
					if (row >= startRow && enumerator.Current.IsLaneNote())
						deletedEventsForPattern.Add(enumerator.Current);

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

		return new Alterations(deletedEvents, shortenedHolds);
	}
}
