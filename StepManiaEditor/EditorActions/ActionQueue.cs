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
		const int DefaultSize = 1024;

		/// <summary>
		/// The index at the last time the file was saved.
		/// </summary>
		private int LastSavedIndex = -1;
		/// <summary>
		/// The number of actions affecting the file at the last time the file was saved.
		/// </summary>
		private int LastSavedChangeCount = 0;
		/// <summary>
		/// The number of actions affecting the file currently.
		/// </summary>
		private int CurrentChangeCount = 0;
		/// <summary>
		/// Flag for recording when redo history is lost and the history contains actions
		/// which affect the save file.
		/// </summary>
		private bool LostSavedChanges = false;

		/// <summary>
		/// UndoStack of EditorActions for undo and redo.
		/// </summary>
		private readonly UndoStack<EditorAction> Actions;

		public static ActionQueue Instance { get; } = new ActionQueue();

		private ActionQueue(int size = DefaultSize)
		{
			Actions = new UndoStack<EditorAction>(size, true);
		}

		/// <summary>
		/// Resizes the ActionQueue.
		/// May result in history loss if reducing the size.
		/// </summary>
		/// <param name="size">New size.</param>
		public void Resize(int size)
		{
			Actions.Resize(size);
		}

		/// <summary>
		/// Returns whether or not there are unsaved changes based on the actions performed
		/// since the last time the underlying file was saved.
		/// </summary>
		public bool HasUnsavedChanges()
		{
			if (LostSavedChanges)
				return true;
			return CurrentChangeCount != LastSavedChangeCount;
		}

		/// <summary>
		/// Called when the underlying file is saved.
		/// </summary>
		public void OnSaved()
		{
			// Update state for tracking unsaved changes.
			LastSavedIndex = Actions.GetAbsoluteIndex();
			LastSavedChangeCount = Actions.GetCurrent()?.GetTotalNumActionsAffectingFile() ?? 0;
			LostSavedChanges = false;
		}

		/// <summary>
		/// Do the given EditorAction and add it to the queue of EditorAction for undo and redo.
		/// </summary>
		/// <param name="editorAction">EditorAction to do.</param>
		public void Do(EditorAction editorAction)
		{
			// Do the action and enqueue it.
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

			var lastAction = Actions.GetCurrent();
			if (lastAction != null)
				editorAction.SetNumPreviousActionsAffectingFile(lastAction.GetTotalNumActionsAffectingFile());

			// If doing this action will lose changes which affect the file, set a flag
			// that unsaved changes are lost.
			if (LastSavedIndex > Actions.GetAbsoluteIndex()
				&& LastSavedChangeCount > lastAction.GetTotalNumActionsAffectingFile())
			{
				LostSavedChanges = true;
				LastSavedIndex = -1;
			}

			// Add the action.
			Actions.Push(editorAction);
			CurrentChangeCount = editorAction.GetTotalNumActionsAffectingFile();
		}

		/// <summary>
		/// Clear the queue of EditorActions.
		/// </summary>
		public void Clear()
		{
			LastSavedIndex = -1;
			LostSavedChanges = false;
			CurrentChangeCount = 0;
			Actions.Reset();
		}

		/// <summary>
		/// Undo the last action.
		/// </summary>
		/// <returns>The action which was undone or null if no action is left to undo.</returns>
		public EditorAction Undo()
		{
			if (!Actions.CanPop())
				return null;
			Actions.Pop(out var popped);
			if (popped != null)
			{
				popped.Undo();
				Logger.Info($"Undo {popped}");
				CurrentChangeCount = popped.GetTotalNumActionsAffectingFile() - (popped.AffectsFile() ? 1 : 0);
			}
			return popped;
		}

		/// <summary>
		/// Redo the next action.
		/// </summary>
		/// <returns>The action which was redone or null if no action is left to redo.</returns>
		public EditorAction Redo()
		{
			if (!Actions.CanRepush())
				return null;
			Actions.Repush(out var repushed);
			if (repushed != null)
			{
				repushed.Do();
				Logger.Info($"Redo {repushed}");
				CurrentChangeCount = repushed.GetTotalNumActionsAffectingFile();
			}
			return repushed;
		}
	}
}
