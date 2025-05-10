using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor;

internal sealed class ActionDeleteModFromAttack : EditorAction
{
	private readonly EditorAttackEvent Attack;
	private readonly Modifier Mod;
	private readonly int ModIndex;

	public ActionDeleteModFromAttack(EditorAttackEvent attack, Modifier mod) : base(false, false)
	{
		Attack = attack;
		Mod = mod;
		ModIndex = Attack.GetAttack().Modifiers.IndexOf(Mod);
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Delete modifier from attack at row {Attack.GetRow()}: {SMCommon.GetModString(Mod, false, true)}";
	}

	protected override void DoImplementation()
	{
		Attack.GetAttack().Modifiers.Remove(Mod);
		Attack.OnModifiersChanged();
	}

	protected override void UndoImplementation()
	{
		Attack.GetAttack().Modifiers.Insert(ModIndex, Mod);
		Attack.OnModifiersChanged();
	}
}
