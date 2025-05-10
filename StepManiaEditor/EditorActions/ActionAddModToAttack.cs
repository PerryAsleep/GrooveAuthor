using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor;

internal sealed class ActionAddModToAttack : EditorAction
{
	private readonly EditorAttackEvent Attack;
	private readonly Modifier Mod;

	public ActionAddModToAttack(EditorAttackEvent attack) : base(false, false)
	{
		Attack = attack;
		Mod = new Modifier()
		{
			Level = 1,
			Speed = 1,
			LengthSeconds = 1.0,
		};
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Add modifier to attack at row {Attack.GetRow()}: {SMCommon.GetModString(Mod, false, true)}";
	}

	protected override void DoImplementation()
	{
		Attack.GetAttack().Modifiers.Add(Mod);
		Attack.OnModifiersChanged();
	}

	protected override void UndoImplementation()
	{
		Attack.GetAttack().Modifiers.Remove(Mod);
		Attack.OnModifiersChanged();
	}
}
