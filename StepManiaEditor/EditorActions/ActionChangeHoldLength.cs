namespace StepManiaEditor;

/// <summary>
/// Action to change an EditorHoldNoteEvent's length.
/// </summary>
internal sealed class ActionChangeHoldLength : EditorAction
{
	private readonly EditorHoldNoteEvent Hold;
	private readonly int OriginalLength;
	private readonly int NewLength;

	public ActionChangeHoldLength(EditorHoldNoteEvent hold, int length) : base(false, false)
	{
		Hold = hold;
		OriginalLength = Hold.GetLength();
		NewLength = length;
	}

	public override string ToString()
	{
		var typeStr = Hold.IsRoll() ? "roll" : "hold";
		return $"Change {typeStr} length from to {OriginalLength} to {NewLength}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		Hold.SetLength(NewLength);
	}

	protected override void UndoImplementation()
	{
		Hold.SetLength(OriginalLength);
	}
}
