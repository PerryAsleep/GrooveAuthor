namespace StepManiaEditor;

/// <summary>
/// Action to add an EditorHoldNoteEvent.
/// </summary>
internal sealed class ActionAddHoldEvent : EditorAction
{
	private readonly EditorHoldNoteEvent Hold;

	public ActionAddHoldEvent(EditorChart chart, int lane, int row, int length, bool roll, bool isBeingEdited) : base(false,
		false)
	{
		Hold = EditorHoldNoteEvent.CreateHold(chart, lane, row, length, roll);
		Hold.SetIsBeingEdited(isBeingEdited);
	}

	public EditorHoldNoteEvent GetHoldEvent()
	{
		return Hold;
	}

	public void SetIsRoll(bool roll)
	{
		Hold.SetIsRoll(roll);
	}

	public void SetIsBeingEdited(bool isBeingEdited)
	{
		Hold.SetIsBeingEdited(isBeingEdited);
	}

	public override string ToString()
	{
		return $"Add {Hold.GetShortTypeName()} to lane {Hold.GetLane()} at row {Hold.GetRow()}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		Hold.GetEditorChart().AddEvent(Hold);
	}

	protected override void UndoImplementation()
	{
		Hold.GetEditorChart().DeleteEvent(Hold);
	}
}
