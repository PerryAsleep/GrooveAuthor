using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to delete EditorEvents.
/// </summary>
internal sealed class ActionDeleteEditorEvents : EditorAction
{
	private readonly List<EditorEvent> EditorEvents = [];

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
		var count = EditorEvents.Count;
		if (EditorEvents.Count == 1)
		{
			var editorEvent = EditorEvents[0];
			if (editorEvent.IsLaneNote())
				return $"Delete {editorEvent.GetShortTypeName()} on lane {editorEvent.GetLane()} at row {editorEvent.GetRow()}.";
			return $"Delete {editorEvent.GetShortTypeName()} at row {editorEvent.GetRow()}.";
		}

		return $"Delete {count} events.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		EditorEvents[0].GetEditorChart().DeleteEvents(EditorEvents);
	}

	protected override void UndoImplementation()
	{
		EditorEvents[0].GetEditorChart().AddEvents(EditorEvents);
	}
}
