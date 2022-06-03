using System.Collections.Generic;
using Fumen;

namespace StepManiaEditor
{
	/// <summary>
	/// Data structure to enqueue EditorActions, and undo and redo those actions
	/// in the order they were enqueued.
	/// </summary>
	public class ActionQueue
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
		/// Do the given EditorAction and add it to the queue of EditorAction for undo and redo.
		/// </summary>
		/// <param name="editorAction">EditorAction to do.</param>
		public void Do(EditorAction editorAction)
		{
			// Do the action.
			Logger.Info(editorAction.ToString());
			editorAction.Do();

			// Add the action to the list, overwriting any old future action.
			if (Index == Actions.Count - 1)
				Actions.Add(editorAction);
			else
				Actions[Index + 1] = editorAction;
			Index++;

			// When an action is added, even if there were future actions now they can
			// no longer be redone.
			MaxRedoIndex = Index;
		}

		/// <summary>
		/// Clear the queue of EditorActions.
		/// </summary>
		public void Clear()
		{
			Index = -1;
			MaxRedoIndex = -1;
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
