using System.Collections.Generic;
using static System.Diagnostics.Debug;

namespace StepManiaEditor
{
	/// <summary>
	/// An action that can be done and undone.
	/// Meant to be used by ActionQueue.
	/// EditorActions may be synchronous or asynchronous.
	/// To do or undo an EditorAction, it must not currently be asynchronously being done or undone.
	/// Attempting to do or undo a currently running asynchronous EditorAction will have no effect.
	/// </summary>
	internal abstract class EditorAction
	{
		/// <summary>
		/// State of an EditorAction.
		/// </summary>
		internal enum State
		{
			/// <summary>
			/// The action is currently being done.
			/// </summary>
			Doing,
			/// <summary>
			/// The action is currently being undone.
			/// </summary>
			Undoing,
			/// <summary>
			/// The action is not currently being done or undone.
			/// </summary>
			None,
		}

		/// <summary>
		/// Number of previous actions which affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		protected int NumPreviousActionsAffectingFile = 0;

		/// <summary>
		/// The current state of this EditorAction.
		/// </summary>
		private State ActionState = State.None;
		/// <summary>
		/// Whether doing this action is asynchronous.
		/// </summary>
		private readonly bool IsDoAsyncInternal;
		/// <summary>
		/// Whether undoing this action is asynchronous.
		/// </summary>
		private readonly bool IsUndoAsyncInternal;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="isDoAsync">Whether doing this action is asynchronous.</param>
		/// <param name="isUndoAsync">Whether undoing this action is asynchronous.</param>
		public EditorAction(bool isDoAsync, bool isUndoAsync)
		{
			IsDoAsyncInternal = isDoAsync;
			IsUndoAsyncInternal = isUndoAsync;
		}

		/// <summary>
		/// Do the action.
		/// </summary>
		public void Do()
		{
			Assert(ActionState == State.None);
			if (ActionState != State.None)
				return;

			ActionState = State.Doing;
			DoImplementation();
			if (!IsDoAsync())
				OnDone();
		}

		/// <summary>
		/// Abstract implementation for doing the action.
		/// </summary>
		protected abstract void DoImplementation();

		/// <summary>
		/// Called when doing the action has completed.
		/// Asynchronous actions are expected to call this when they have completed doing their actions.
		/// Synchronous actions will have this called automatically by EditorAction.
		/// </summary>
		protected void OnDone()
		{
			Assert(ActionState == State.Doing);
			ActionState = State.None;
		}

		/// <summary>
		/// Undo the action.
		/// </summary>
		public void Undo()
		{
			Assert(ActionState == State.None);
			if (ActionState != State.None)
				return;

			ActionState = State.Undoing;
			UndoImplementation();
			if (!IsUndoAsync())
				OnUndone();
		}

		/// <summary>
		/// Abstract implementation for undoing the action.
		/// </summary>
		protected abstract void UndoImplementation();

		/// <summary>
		/// Called when undoing the action has completed.
		/// Asynchronous actions are expected to call this when they have completed undoing their actions.
		/// Synchronous actions will have this called automatically by EditorAction.
		/// </summary>
		protected void OnUndone()
		{
			Assert(ActionState == State.Undoing);
			ActionState = State.None;
		}

		/// <summary>
		/// Returns whether or not this action represents a change to the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		public abstract bool AffectsFile();

		/// <summary>
		/// Returns whether or not this action is currently being done or undone.
		/// </summary>
		/// <returns>True if this action is curently being done or undone and false otherwise.</returns>
		public bool IsDoingOrUndoing()
		{
			return ActionState != State.None;
		}

		/// <summary>
		/// Returns whether or not doing this action is asynchronous.
		/// </summary>
		/// <returns>True if doing this action is asynchronous and false otherwise.</returns>
		public bool IsDoAsync()
		{
			return IsDoAsyncInternal;
		}

		/// <summary>
		/// Returns whether or not undoing this action is asynchronous.
		/// </summary>
		/// <returns>True if undoing this action is asynchronous and false otherwise.</returns>
		public bool IsUndoAsync()
		{
			return IsUndoAsyncInternal;
		}

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
	}

	internal sealed class ActionAddEditorEvent : EditorAction
	{
		private EditorEvent EditorEvent;

		public ActionAddEditorEvent(EditorEvent editorEvent) : base(false, false)
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

		public override string ToString()
		{
			// TODO: Nice strings
			return $"Add {EditorEvent.GetType()}.";
		}

		public override bool AffectsFile()
		{
			return true;
		}

		protected override void DoImplementation()
		{
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}

		protected override void UndoImplementation()
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

		public ActionDeleteEditorEvents(EditorEvent editorEvent) : base(false, false)
		{
			EditorEvents.Add(editorEvent);
		}

		public ActionDeleteEditorEvents(List<EditorEvent> editorEvents, bool copy) : base(false, false)
		{
			if (copy)
				EditorEvents.AddRange(editorEvents);
			else
				EditorEvents = editorEvents;
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

		public override bool AffectsFile()
		{
			return true;
		}

		protected override void DoImplementation()
		{
			AllDeletedEvents = EditorEvents[0].GetEditorChart().DeleteEvents(EditorEvents);
		}

		protected override void UndoImplementation()
		{
			EditorEvents[0].GetEditorChart().AddEvents(AllDeletedEvents);
		}
	}

	internal sealed class ActionChangeHoldLength : EditorAction
	{
		private EditorHoldNoteEvent Hold;
		private int OriginalLength;
		private int NewLength;

		public ActionChangeHoldLength(EditorHoldNoteEvent hold, int length) : base(false, false)
		{
			Hold = hold;
			OriginalLength = Hold.GetLength();
			NewLength = length;
		}

		public override string ToString()
		{
			var typeStr = Hold.IsRoll() ? "roll" : "hold";
			return $"Change {typeStr} length from to {OriginalLength} to {NewLength}.";
		}

		public override bool AffectsFile()
		{
			return true;
		}

		protected override void DoImplementation()
		{
			Hold.SetLength(NewLength);
		}

		protected override void UndoImplementation()
		{
			Hold.SetLength(OriginalLength);
		}
	}

	internal sealed class ActionAddHoldEvent : EditorAction
	{
		private EditorHoldNoteEvent Hold;

		public ActionAddHoldEvent(EditorChart chart, int lane, int row, int length, bool roll, bool isBeingEdited) : base(false, false)
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

		public override string ToString()
		{
			var typeStr = Hold.IsRoll() ? "roll" : "hold";
			return $"Add {typeStr}.";
		}

		public override bool AffectsFile()
		{
			return true;
		}

		protected override void DoImplementation()
		{
			Hold.GetEditorChart().AddEvent(Hold);
		}

		protected override void UndoImplementation()
		{
			Hold.GetEditorChart().DeleteEvent(Hold);
		}
	}

	internal sealed class ActionChangeHoldType : EditorAction
	{
		private bool Roll;
		private EditorHoldNoteEvent Hold;

		public ActionChangeHoldType(EditorHoldNoteEvent hold, bool roll) : base(false, false)
		{
			Hold = hold;
			Roll = roll;
		}

		public override string ToString()
		{
			var originalType = Roll ? "hold" : "roll";
			var newType = Roll ? "roll" : "hold";
			return $"Change {originalType} to {newType}.";
		}

		public override bool AffectsFile()
		{
			return true;
		}

		protected override void DoImplementation()
		{
			Hold.SetIsRoll(Roll);
		}

		protected override void UndoImplementation()
		{
			Hold.SetIsRoll(!Roll);
		}
	}
}
