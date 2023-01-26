using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// An action that can be done and undone.
	/// Meant to be used by ActionQueue.
	/// </summary>
	internal abstract class EditorAction
	{
		/// <summary>
		/// Do the action.
		/// </summary>
		public abstract void Do();

		/// <summary>
		/// Undo the action.
		/// </summary>
		public abstract void Undo();

		/// <summary>
		/// Returns whether or not this action represents a change to the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		public abstract bool AffectsFile();

		/// <summary>
		/// Returns how many actions up to and including this action affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		/// <returns></returns>
		public int GetTotalNumActionsAffectingFile()
		{
			return NumPreviousActionsAffectingFile + (AffectsFile() ? 1 : 0);
		}

		/// <summary>
		/// Sets the number of previous actions which affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		public void SetNumPreviousActionsAffectingFile(int actions)
		{
			NumPreviousActionsAffectingFile = actions;
		}

		/// <summary>
		/// Number of previous actions which affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		protected int NumPreviousActionsAffectingFile = 0;
	}

	internal sealed class ActionAddEditorEvent : EditorAction
	{
		private EditorEvent EditorEvent;

		public ActionAddEditorEvent(EditorEvent editorEvent)
		{
			EditorEvent = editorEvent;
		}

		public void UpdateEvent(EditorEvent editorEvent)
		{
			EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
			EditorEvent = editorEvent;
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}

		public void SetIsBeingEdited(bool isBeingEdited)
		{
			EditorEvent.SetIsBeingEdited(isBeingEdited);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			// TODO: Nice strings
			return $"Add {EditorEvent.GetType()}.";
		}

		public override void Do()
		{
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}

		public override void Undo()
		{
			EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
		}
	}

	internal sealed class ActionDeleteEditorEvents : EditorAction
	{
		private readonly List<EditorEvent> EditorEvents = new List<EditorEvent>();

		/// <summary>
		/// Deleting an event may result in other events also being deleted.
		/// We store all deleted events as a result of the requested delete so
		/// that when we redo the action we can restore them all.
		/// </summary>
		private List<EditorEvent> AllDeletedEvents = new List<EditorEvent>();

		public ActionDeleteEditorEvents(EditorEvent editorEvent)
		{
			EditorEvents.Add(editorEvent);
		}

		public ActionDeleteEditorEvents(List<EditorEvent> editorEvents, bool copy)
		{
			if (copy)
				EditorEvents.AddRange(editorEvents);
			else
				EditorEvents = editorEvents;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			// TODO: Nice strings
			var count = EditorEvents.Count;
			if (count == 1)
			{
				return $"Delete {EditorEvents[0].GetType()}.";
			}
			return $"Delete {count} events.";
		}

		public override void Do()
		{
			AllDeletedEvents = EditorEvents[0].GetEditorChart().DeleteEvents(EditorEvents);
		}

		public override void Undo()
		{
			EditorEvents[0].GetEditorChart().AddEvents(AllDeletedEvents);
		}
	}

	internal sealed class ActionChangeHoldLength : EditorAction
	{
		private EditorHoldNoteEvent Hold;
		private int OriginalLength;
		private int NewLength;

		public ActionChangeHoldLength(EditorHoldNoteEvent hold, int length)
		{
			Hold = hold;
			OriginalLength = Hold.GetLength();
			NewLength = length;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = Hold.IsRoll() ? "roll" : "hold";
			return $"Change {typeStr} length from to {OriginalLength} to {NewLength}.";
		}

		public override void Do()
		{
			Hold.SetLength(NewLength);
		}

		public override void Undo()
		{
			Hold.SetLength(OriginalLength);
		}
	}

	internal sealed class ActionAddHoldEvent : EditorAction
	{
		private EditorHoldNoteEvent Hold;

		public ActionAddHoldEvent(EditorChart chart, int lane, int row, int length, bool roll, bool isBeingEdited)
		{
			Hold = EditorHoldNoteEvent.CreateHold(chart, lane, row, length, roll);
			Hold.SetIsBeingEdited(isBeingEdited);
		}

		public EditorHoldNoteEvent GetHoldEvent()
		{
			return Hold;
		}

		public void SetIsRoll(bool roll)
		{
			Hold.SetIsRoll(roll);
		}

		public void SetIsBeingEdited(bool isBeingEdited)
		{
			Hold.SetIsBeingEdited(isBeingEdited);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = Hold.IsRoll() ? "roll" : "hold";
			return $"Add {typeStr}.";
		}

		public override void Do()
		{
			Hold.GetEditorChart().AddEvent(Hold);
		}

		public override void Undo()
		{
			Hold.GetEditorChart().DeleteEvent(Hold);
		}
	}

	internal sealed class ActionChangeHoldType : EditorAction
	{
		private bool Roll;
		private EditorHoldNoteEvent Hold;

		public ActionChangeHoldType(EditorHoldNoteEvent hold, bool roll)
		{
			Hold = hold;
			Roll = roll;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var originalType = Roll ? "hold" : "roll";
			var newType = Roll ? "roll" : "hold";
			return $"Change {originalType} to {newType}.";
		}

		public override void Do()
		{
			Hold.SetIsRoll(Roll);
		}

		public override void Undo()
		{
			Hold.SetIsRoll(!Roll);
		}
	}
}
