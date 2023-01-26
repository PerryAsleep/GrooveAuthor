using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// Action to perform multiple other actions together as a single action.
	/// </summary>
	internal sealed class ActionMultiple : EditorAction
	{
		private readonly List<EditorAction> Actions;

		public ActionMultiple()
		{
			Actions = new List<EditorAction>();
		}

		public ActionMultiple(List<EditorAction> actions)
		{
			Actions = actions;
		}

		public void EnqueueAndDo(EditorAction action)
		{
			action.Do();
			Actions.Add(action);
		}

		public void EnqueueWithoutDoing(EditorAction action)
		{
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

		public override void Do()
		{
			foreach (var action in Actions)
			{
				action.Do();
			}
		}

		public override void Undo()
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
}
