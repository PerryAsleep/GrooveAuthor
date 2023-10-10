namespace StepManiaEditor;

/// <summary>
/// Action to add a single EditorEvent.
/// </summary>
internal sealed class ActionAddEditorEvent : EditorAction
{
	private EditorEvent EditorEvent;

	public ActionAddEditorEvent(EditorEvent editorEvent) : base(false, false)
	{
		EditorEvent = editorEvent;
	}

	public void UpdateEvent(EditorEvent editorEvent)
	{
		EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
		EditorEvent = editorEvent;
		EditorEvent.GetEditorChart().AddEvent(EditorEvent);
	}

	public void SetIsBeingEdited(bool isBeingEdited)
	{
		EditorEvent.SetIsBeingEdited(isBeingEdited);
	}

	public override string ToString()
	{
		return $"Add {EditorEvent.GetType()}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		EditorEvent.GetEditorChart().AddEvent(EditorEvent);
	}

	protected override void UndoImplementation()
	{
		EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
	}
}
