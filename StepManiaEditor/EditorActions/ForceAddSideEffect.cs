using System.Collections.Generic;
using static System.Diagnostics.Debug;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for holding the side effects of forcibly adding an event so that these side effects
	/// can be undone as part of EditorActions which move event posititons.
	/// </summary>
	internal sealed class ForceAddSideEffect
	{
		/// <summary>
		/// All events which were forcibly added as a side effect of transforming an event.
		/// </summary>
		private List<EditorEvent> Additions;
		/// <summary>
		/// All events which were forcibly deleted as a side effect of transforming an event.
		/// </summary>
		private List<EditorEvent> Deletions;

		public ForceAddSideEffect(List<EditorEvent> additions, List<EditorEvent> deletions)
		{
			Additions = additions;
			Deletions = deletions;
		}

		public void Undo(EditorChart chart)
		{
			if (Additions.Count > 0)
			{
				var deletedEvents = chart.DeleteEvents(Additions);
				Assert(deletedEvents.Count == Additions.Count);
			}
			if (Deletions.Count > 0)
			{
				// Reset the times of these events before adding them back.
				// It could be the case that the events being moved as part of this action contain
				// multiple rate altering events. If one altered this event's time when it moved and
				// then a subsequently moved event deleted this event, then it's time will be incorrect
				// at this point since it was not in the event tree when we re-added the unmodified event
				// which changed it's time originally.
				foreach (var deletedEvent in Deletions)
					deletedEvent.ResetTimeBasedOnRow();
				chart.AddEvents(Deletions);
			}
		}
	}
}
