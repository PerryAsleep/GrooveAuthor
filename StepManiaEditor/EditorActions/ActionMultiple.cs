using System.Collections.Generic;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

/// <summary>
/// Action to perform multiple other actions together as a single action.
/// </summary>
internal sealed class ActionMultiple : EditorAction
{
	private readonly List<EditorAction> Actions;

	public ActionMultiple() : base(false, false)
	{
		Actions = new List<EditorAction>();
	}

	public ActionMultiple(List<EditorAction> actions) : base(false, false)
	{
		foreach (var action in actions)
		{
			Assert(!action.IsDoAsync() && !action.IsUndoAsync());
		}

		Actions = actions;
	}

	public void EnqueueAndDo(EditorAction action)
	{
		Assert(!action.IsDoAsync() && !action.IsUndoAsync());

		action.Do();
		Actions.Add(action);
	}

	public void EnqueueWithoutDoing(EditorAction action)
	{
		Assert(!action.IsDoAsync() && !action.IsUndoAsync());

		Actions.Add(action);
	}

	public List<EditorAction> GetActions()
	{
		return Actions;
	}

	public override bool AffectsFile()
	{
		foreach (var action in Actions)
		{
			if (action.AffectsFile())
				return true;
		}

		return false;
	}

	public override string ToString()
	{
		return string.Join(' ', Actions);
	}

	protected override void DoImplementation()
	{
		foreach (var action in Actions)
		{
			action.Do();
		}
	}

	protected override void UndoImplementation()
	{
		var i = Actions.Count - 1;
		while (i >= 0)
		{
			Actions[i--].Undo();
		}
	}

	public void Clear()
	{
		Actions.Clear();
	}
}
