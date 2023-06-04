using Fumen;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

/// <summary>
/// Data structure to enqueue EditorActions, and undo and redo those actions
/// in the order they were enqueued.
/// 
/// EditorActions may be asynchronous. If an asynchronous EditorAction is being
/// done or undone, no other actions may be done or undone until it is complete.
/// Call IsDoingOrUndoing to determine if an asynchronous EditorAction is being
/// done or undone. Notifications will be issued to Observers when asynchronous
/// operations begin. Doing or undoing actions while one is running asynchronously
/// results in undefined behavior.
/// 
/// Expected Usage:
///  Call Do or EnqueueWithoutDoing to add an action to the queue.
///  Call Undo and Redo as needed.
///  Call Clear to reset the queue.
///  Call OnSaved when the underlying file is saved so that the ActionQueue can
///   report whether or not there are unsaved changes in the queue.
///  Call HasUnsavedChanges to determine if there are unsaved changes in the queue.
/// </summary>
internal sealed class ActionQueue : Notifier<ActionQueue>
{
	private const int DefaultSize = 1024;

	public const string NotificationAsyncActionStarted = "AsyncActionStarted";

	/// <summary>
	/// The index at the last time the file was saved.
	/// </summary>
	private int LastSavedIndex = -1;

	/// <summary>
	/// The number of actions affecting the file at the last time the file was saved.
	/// </summary>
	private int LastSavedChangeCount;

	/// <summary>
	/// The number of actions affecting the file currently.
	/// </summary>
	private int CurrentChangeCount;

	/// <summary>
	/// Flag for recording when redo history is lost and the history contains actions
	/// which affect the save file.
	/// </summary>
	private bool LostSavedChanges;

	/// <summary>
	/// The last action either done or undone. Used for async tracking.
	/// </summary>
	private EditorAction LastAction;

	/// <summary>
	/// UndoStack of EditorActions for undo and redo.
	/// </summary>
	private readonly UndoStack<EditorAction> Actions;

	/// <summary>
	/// Static Instance.
	/// </summary>
	public static ActionQueue Instance { get; } = new();

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="size">Size of the ActionQueue.</param>
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
	/// Returns whether an action is currently being done or undone.
	/// </summary>
	/// <returns>True if an action is currently being done or undone and false otherwise.</returns>
	public bool IsDoingOrUndoing()
	{
		if (LastAction == null)
			return false;
		return LastAction.IsDoingOrUndoing();
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
		Assert(!IsDoingOrUndoing());

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
		Assert(!IsDoingOrUndoing());

		Logger.Info(editorAction.ToString());

		var lastAction = Actions.GetCurrent();
		if (lastAction != null)
			editorAction.SetNumPreviousActionsAffectingFile(lastAction.GetTotalNumActionsAffectingFile());

		// If doing this action will lose changes which affect the file, set a flag
		// that unsaved changes are lost.
		if (LastSavedIndex > Actions.GetAbsoluteIndex()
		    && lastAction != null
		    && LastSavedChangeCount > lastAction.GetTotalNumActionsAffectingFile())
		{
			LostSavedChanges = true;
			LastSavedIndex = -1;
		}

		// Add the action.
		Actions.Push(editorAction);
		CurrentChangeCount = editorAction.GetTotalNumActionsAffectingFile();

		UpdateLastAction(editorAction);
	}

	/// <summary>
	/// Helper method to update the LastAction and notify observers if
	/// an async operation is now in progress.
	/// </summary>
	/// <param name="lastAction">EditorAction to set as the new LastAction.</param>
	private void UpdateLastAction(EditorAction lastAction)
	{
		LastAction = lastAction;
		if (LastAction != null && LastAction.IsDoingOrUndoing())
			Notify(NotificationAsyncActionStarted, this);
	}

	/// <summary>
	/// Clear the queue of EditorActions.
	/// </summary>
	public void Clear()
	{
		LastSavedIndex = -1;
		LostSavedChanges = false;
		CurrentChangeCount = 0;
		UpdateLastAction(null);
		Actions.Reset();
	}

	/// <summary>
	/// Undo the last action.
	/// </summary>
	/// <returns>The action which was undone or null if no action is left to undo.</returns>
	public EditorAction Undo()
	{
		Assert(!IsDoingOrUndoing());

		if (!Actions.CanPop())
			return null;
		Actions.Pop(out var popped);
		if (popped != null)
		{
			popped.Undo();
			Logger.Info($"Undo {popped}");
			CurrentChangeCount = popped.GetTotalNumActionsAffectingFile() - (popped.AffectsFile() ? 1 : 0);
			UpdateLastAction(popped);
		}

		return popped;
	}

	/// <summary>
	/// Redo the next action.
	/// </summary>
	/// <returns>The action which was redone or null if no action is left to redo.</returns>
	public EditorAction Redo()
	{
		Assert(!IsDoingOrUndoing());

		if (!Actions.CanRepush())
			return null;
		Actions.Repush(out var repushed);
		if (repushed != null)
		{
			repushed.Do();
			Logger.Info($"Redo {repushed}");
			CurrentChangeCount = repushed.GetTotalNumActionsAffectingFile();
			UpdateLastAction(repushed);
		}

		return repushed;
	}
}
