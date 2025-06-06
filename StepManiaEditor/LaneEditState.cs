﻿using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// State for one lane when editing a note.
/// </summary>
internal sealed class LaneEditState
{
	/// <summary>
	/// Whether or not a note in the lane is being actively edited.
	/// </summary>
	private bool Active;

	/// <summary>
	/// The row that editing began on.
	/// </summary>
	private int StartingRow;

	/// <summary>
	/// The event being edited.
	/// This is a tap or a mine while input is held.
	/// If dragged while holding, this is a hold or a roll.
	/// If tapping over an existing event, this event will be null.
	/// </summary>
	private EditorEvent EventBeingEdited;

	/// <summary>
	/// All actions to perform as part of this edit.
	/// Usually this is just one action to add a new event.
	/// In some cases it contains multiple events, like when creating a hold
	/// over existing notes, and we need to delete old notes.
	/// </summary>
	private ActionMultiple Actions = new();

	/// <summary>
	/// When starting to edit over an existing note, this holds the action
	/// for deleting the existing note.
	/// </summary>
	private EditorAction InitialDeleteAction;

	public bool IsActive()
	{
		return Active;
	}

	public int GetStartingRow()
	{
		return StartingRow;
	}

	public EditorEvent GetEventBeingEdited()
	{
		return EventBeingEdited;
	}

	public bool IsOnlyDelete()
	{
		return Active
		       && InitialDeleteAction != null
		       && EventBeingEdited == null
		       && Actions.GetActions().Count == 1;
	}

	/// <summary>
	/// Sets the edit state to be deleting an existing event.
	/// </summary>
	public void StartEditingWithDelete(
		int startingRow,
		EditorAction deleteAction)
	{
		Clear(false);
		InitialDeleteAction = deleteAction;
		Actions = new ActionMultiple();
		Actions.EnqueueAndDo(InitialDeleteAction);
		EventBeingEdited = null;
		StartingRow = startingRow;
		Active = true;
	}

	/// <summary>
	/// Sets the edit state to be editing a tap or a mine.
	/// Can be called to start editing, or to reset an existing edit state to be editing a tap or mine.
	/// </summary>
	public void SetEditingTapOrMine(
		EditorEvent note,
		List<EditorAction> firstActions = null)
	{
		Clear(false);

		if (firstActions != null)
		{
			foreach (var editorAction in firstActions)
			{
				Actions.EnqueueAndDo(editorAction);
			}
		}

		Actions.EnqueueAndDo(new ActionAddEditorEvent(note));
		EventBeingEdited = note;
		StartingRow = note.GetRow();
		Active = true;
	}

	/// <summary>
	/// Sets the edit state to be editing a hold or roll.
	/// Can be called to start editing, or to reset an existing edit state to be editing a tap or mine.
	/// </summary>
	public void SetEditingHold(
		EditorChart activeChart,
		int row,
		int lane,
		int player,
		int startingRow,
		int length,
		bool roll,
		List<EditorAction> firstActions = null)
	{
		Clear(false);

		if (firstActions != null)
		{
			foreach (var editorAction in firstActions)
			{
				Actions.EnqueueAndDo(editorAction);
			}
		}

		var addHoldEvent = new ActionAddHoldEvent(activeChart, row, lane, player, length, roll, true);
		EventBeingEdited = addHoldEvent.GetHoldEvent();
		Actions.EnqueueAndDo(addHoldEvent);
		Active = true;
		StartingRow = startingRow;
	}

	/// <summary>
	/// Swaps note state based on the arrow modification key (e.g. shift).
	/// For taps, this will alternate between mines and taps.
	/// For holds, this will alternate between holds and rolls.
	/// </summary>
	/// <param name="up">Whether the arrow modification key is up or down.</param>
	public void ArrowModificationKeyPressed(bool up)
	{
		switch (EventBeingEdited)
		{
			// Switch between a tap and mine.
			case EditorTapNoteEvent:
			case EditorMineNoteEvent:
			{
				if (up)
				{
					var config = EventConfig.CreateTapConfig(EventBeingEdited);
					config.IsBeingEdited = true;
					EventBeingEdited = EditorEvent.CreateEvent(config);
				}
				else
				{
					var config = EventConfig.CreateMineConfig(EventBeingEdited);
					config.IsBeingEdited = true;
					EventBeingEdited = EditorEvent.CreateEvent(config);
				}

				foreach (var editorAction in Actions.GetActions())
				{
					if (editorAction is ActionAddEditorEvent addAction)
					{
						addAction.UpdateEvent(EventBeingEdited);
					}
				}

				break;
			}
			// Switch between a hold and roll.
			case EditorHoldNoteEvent:
			{
				foreach (var editorAction in Actions.GetActions())
				{
					if (editorAction is ActionAddHoldEvent addAction)
					{
						addAction.SetIsRoll(!up);
					}
				}

				break;
			}
		}
	}

	/// <summary>
	/// Commits the edit.
	/// Will undo a re-apply all actions after converting all enqueued events to events without
	/// their edit flags set.
	/// </summary>
	public void Commit()
	{
		// Convert all actions to use events which aren't being edited.
		// We need to destroy and recreate these so they sort properly in the event tree.
		Actions.Undo();
		MarkActionsAsNotBeingEdited(Actions);

		// Perform the converted actions.
		ActionQueue.Instance.Do(Actions);

		// Clear state.
		Actions = new ActionMultiple();
		InitialDeleteAction = null;
		EventBeingEdited = null;
		Active = false;
		StartingRow = 0;
	}

	private void MarkActionsAsNotBeingEdited(EditorAction action)
	{
		switch (action)
		{
			case ActionMultiple m:
			{
				foreach (var subAction in m.GetActions())
					MarkActionsAsNotBeingEdited(subAction);
				break;
			}
			case ActionAddHoldEvent ahe:
				ahe.SetIsBeingEdited(false);
				break;
			case ActionAddEditorEvent aee:
				aee.SetIsBeingEdited(false);
				break;
		}
	}

	/// <summary>
	/// Clears the state, undoing all actions.
	/// </summary>
	/// <param name="doneEditing">
	/// Whether or note editing is complete.
	/// If editing is not complete, then we will maintain and re-apply the InitialDeleteAction.
	/// If editing is complete, all state will be reset.
	/// </param>
	public void Clear(bool doneEditing)
	{
		if (!Active)
			return;
		if (doneEditing)
			InitialDeleteAction = null;
		Actions.Undo();
		Actions = new ActionMultiple();
		if (!doneEditing && InitialDeleteAction != null)
			Actions.EnqueueAndDo(InitialDeleteAction);
		EventBeingEdited = null;
		if (doneEditing)
		{
			Active = false;
			StartingRow = 0;
		}
	}
}
