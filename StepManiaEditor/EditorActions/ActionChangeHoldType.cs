namespace StepManiaEditor;

/// <summary>
/// Action to change an EditorHoldNoteEvent between a roll and a hold.
/// </summary>
internal sealed class ActionChangeHoldType : EditorAction
{
	private readonly bool Roll;
	private readonly int Player;
	private readonly int OldPlayer;
	private readonly EditorHoldNoteEvent Hold;

	public ActionChangeHoldType(EditorHoldNoteEvent hold, bool roll, int player) : base(false, false)
	{
		Hold = hold;
		Roll = roll;
		Player = player;
		OldPlayer = hold.GetPlayer();
	}

	public override string ToString()
	{
		var originalType = Roll ? "Hold" : "Roll";
		var newType = Roll ? "Roll" : "Hold";
		return $"Change {originalType} to {newType}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		Hold.SetPlayer(Player);
		Hold.SetIsRoll(Roll);
	}

	protected override void UndoImplementation()
	{
		Hold.SetIsRoll(!Roll);
		Hold.SetPlayer(OldPlayer);
	}
}
