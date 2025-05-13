using Fumen.Converters;

namespace StepManiaEditor;

internal sealed class ActionDeleteModFromAttack : EditorAction
{
	private readonly EditorAttackEvent Attack;
	private readonly EditorAttackEvent.EditorModifier Mod;
	private readonly int ModIndex;

	public ActionDeleteModFromAttack(EditorAttackEvent attack, int index) : base(false, false)
	{
		Attack = attack;
		ModIndex = index;
		Mod = Attack.GetModifiers()[ModIndex];
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Delete modifier from attack at row {Attack.GetRow()}: {SMCommon.GetModString(Mod.Modifier, false, true)}";
	}

	protected override void DoImplementation()
	{
		Attack.RemoveModifier(Mod);
	}

	protected override void UndoImplementation()
	{
		Attack.InsertModifier(ModIndex, Mod);
	}
}
