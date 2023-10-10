using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to delete EditorEvents.
/// </summary>
internal sealed class ActionDeleteEditorEvents : EditorAction
{
	private readonly List<EditorEvent> EditorEvents = new();

	/// <summary>
	/// Deleting an event may result in other events also being deleted.
	/// We store all deleted events as a result of the requested delete so
	/// that when we redo the action we can restore them all.
	/// </summary>
	private List<EditorEvent> AllDeletedEvents = new();

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
			return $"Delete {EditorEvents[0].GetType()}.";
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
