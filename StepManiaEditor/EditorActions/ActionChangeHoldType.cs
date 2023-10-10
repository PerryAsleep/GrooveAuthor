namespace StepManiaEditor;

/// <summary>
/// Action to change an EditorHoldNoteEvent between a roll and a hold.
/// </summary>
internal sealed class ActionChangeHoldType : EditorAction
{
	private readonly bool Roll;
	private readonly EditorHoldNoteEvent Hold;

	public ActionChangeHoldType(EditorHoldNoteEvent hold, bool roll) : base(false, false)
	{
		Hold = hold;
		Roll = roll;
	}

	public override string ToString()
	{
		var originalType = Roll ? "hold" : "roll";
		var newType = Roll ? "roll" : "hold";
		return $"Change {originalType} to {newType}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		Hold.SetIsRoll(Roll);
	}

	protected override void UndoImplementation()
	{
		Hold.SetIsRoll(!Roll);
	}
}
