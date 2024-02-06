using Fumen;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

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
	protected int NumPreviousActionsAffectingFile;

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
	protected EditorAction(bool isDoAsync, bool isUndoAsync)
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

		if (IsDoAsync())
			Logger.Info($"Finished {this}");
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
	/// <returns>True if this action is currently being done or undone and false otherwise.</returns>
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
