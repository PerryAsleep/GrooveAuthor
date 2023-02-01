using System.Collections.Generic;
using Fumen;

namespace StepManiaEditor
{
	/// <summary>
	/// Data structure to enqueue EditorActions, and undo and redo those actions
	/// in the order they were enqueued.
	/// 
	/// Expected Usage:
	///  Call Do or EnqueueWithoutDoing to add an action to the queue.
	///  Call Undo and Redo as needed.
	///  Call Clear to reset the queue.
	///  Call OnSaved when the underlying file is saved so that the ActionQueue can
	///   report whether or not there are unsaved changes in the queue.
	///  Call HasUnsavedChanges to determine if there are unsaved changes in the queue.
	/// </summary>
	internal sealed class ActionQueue
	{
		/// <summary>
		/// Index of the current action.
		/// In other words, the index of the last action which was done.
		/// If no action was done or we have undone to the beginning, then Index will be -1.
		/// </summary>
		private int Index = -1;

		/// <summary>
		/// The maximum index that can be redone to.
		/// When undoing actions and then doing new actions, the MaxRedoAction will
		/// decrease in order to cut off old future actions which can no longer be
		/// redone.
		/// </summary>
		private int MaxRedoIndex = -1;

		/// <summary>
		/// The index at the last time the file was saved.
		/// </summary>
		private int LastSavedIndex = -1;

		/// <summary>
		/// Flag for recording when redo history is lost and the history contains actions
		/// which affect the save file.
		/// </summary>
		private bool LostSavedChanges = false;

		/// <summary>
		/// List of EditorActions for undo and redo.
		/// This list may be longer than the actual set of EditorActions which can
		/// be redone in situations where actions are undone and then new actions
		/// are performed.
		/// </summary>
		private readonly List<EditorAction> Actions = new List<EditorAction>();

		public static ActionQueue Instance { get; } = new ActionQueue();

		private ActionQueue()
		{

		}

		/// <summary>
		/// Returns whether or not there are unsaved changes based on the actions performed
		/// since the last time the underlying file was saved.
		/// </summary>
		public bool HasUnsavedChanges()
		{
			if (LostSavedChanges)
				return true;
			return AreUnsavedChangesPresentBetweenIndexes(LastSavedIndex, Index);
		}

		/// <summary>
		/// Helper to determine if there are differences to the underlying file between actions
		/// at the given indexes.
		/// </summary>
		private bool AreUnsavedChangesPresentBetweenIndexes(int indexA, int indexB)
		{
			if (indexA == indexB)
				return false;

			var numActionsAffectingFileA = 0;
			var numActionsAffectingFileB = 0;

			if (indexA + 1 >= 0 && indexA < MaxRedoIndex)
				numActionsAffectingFileA = Actions[indexA + 1].GetTotalNumActionsAffectingFile();
			if (indexB + 1 >= 0 && indexB < MaxRedoIndex)
				numActionsAffectingFileB = Actions[indexB + 1].GetTotalNumActionsAffectingFile();

			var numUnsavedChanges = numActionsAffectingFileB - numActionsAffectingFileA;
			return numUnsavedChanges != 0;
		}

		/// <summary>
		/// Called when the underlying file is saved.
		/// </summary>
		public void OnSaved()
		{
			// Update state for tracking unsaved changes.
			LastSavedIndex = Index;
			LostSavedChanges = false;
		}

		/// <summary>
		/// Do the given EditorAction and add it to the queue of EditorAction for undo and redo.
		/// </summary>
		/// <param name="editorAction">EditorAction to do.</param>
		public void Do(EditorAction editorAction)
		{
			// Do the action.
			editorAction.Do();

			EnqueueWithoutDoing(editorAction);
		}

		/// <summary>
		/// Enqueue given EditorAction and add it to the queue of EditorAction for undo and redo without doing the action.
		/// </summary>
		/// <param name="editorAction">EditorAction to enqueue.</param>
		public void EnqueueWithoutDoing(EditorAction editorAction)
		{
			Logger.Info(editorAction.ToString());

			if (Index >= 0)
			{
				editorAction.SetNumPreviousActionsAffectingFile(Actions[Index].GetTotalNumActionsAffectingFile());
			}

			// Add the action to the list, overwriting any old future action.
			if (Index == Actions.Count - 1)
				Actions.Add(editorAction);
			else
				Actions[Index + 1] = editorAction;
			Index++;

			// When an action is added, even if there were future actions now they can
			// no longer be redone.
			if (MaxRedoIndex >= Index)
			{
				// If bringing back the MaxRedoHistory will lose changes which affect the file,
				// set a flag that we have unsaved changes.
				if (AreUnsavedChangesPresentBetweenIndexes(MaxRedoIndex, Index) && LastSavedIndex >= Index)
				{
					LostSavedChanges = true;
					LastSavedIndex = -1;
				}
			}
			MaxRedoIndex = Index;
		}

		/// <summary>
		/// Clear the queue of EditorActions.
		/// </summary>
		public void Clear()
		{
			Index = -1;
			MaxRedoIndex = -1;
			LastSavedIndex = -1;
			LostSavedChanges = false;
		}

		/// <summary>
		/// Undo the last action.
		/// </summary>
		/// <returns>The action which was undone or null if no action is left to undo.</returns>
		public EditorAction Undo()
		{
			if (Index < 0)
				return null;

			// Undo the action.
			Logger.Info($"Undo [{Index + 1}/{MaxRedoIndex + 1}]: {Actions[Index]}");
			var action = Actions[Index--];
			action.Undo();

			return action;
		}

		/// <summary>
		/// Redo the next action.
		/// </summary>
		/// <returns>The action which was redone or null if no action is left to redo.</returns>
		public EditorAction Redo()
		{
			if (Index >= MaxRedoIndex)
				return null;

			// Redo the action.
			Logger.Info($"Redo [{Index + 2}/{MaxRedoIndex + 1}]: {Actions[Index + 1]}");
			var action = Actions[++Index];
			action.Do();

			return action;
		}
	}
}
