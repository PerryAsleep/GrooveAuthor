using static System.Diagnostics.Debug;

namespace StepManiaEditor.EditorActions;

/// <summary>
/// Action to move an EditorEvent to a new position.
/// </summary>
internal sealed class ActionMoveEditorEvent : EditorAction
{
	private readonly EditorEvent EditorEvent;
	private readonly int Row;
	private readonly int PreviousRow;

	public ActionMoveEditorEvent(EditorEvent editorEvent, int row, int previousRow) : base(false, false)
	{
		Assert(!EditorChart.CanEventResultInExtraDeletionsWhenMoved(editorEvent));
		EditorEvent = editorEvent;
		Row = row;
		PreviousRow = previousRow;
	}

	public override string ToString()
	{
		return $"Move {EditorEvent.GetType()} to {Row}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		EditorEvent.GetEditorChart().MoveEvent(EditorEvent, Row);
	}

	protected override void UndoImplementation()
	{
		EditorEvent.GetEditorChart().MoveEvent(EditorEvent, PreviousRow);
	}
}
