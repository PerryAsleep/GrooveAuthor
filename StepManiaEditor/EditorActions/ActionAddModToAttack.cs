using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor;

internal sealed class ActionAddModToAttack : EditorAction
{
	private readonly EditorAttackEvent Attack;
	private readonly Modifier Mod;

	public ActionAddModToAttack(EditorAttackEvent attack, double modLength) : base(false, false)
	{
		Attack = attack;
		Mod = EventConfig.CreateDefaultModifier(modLength);
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
		Attack.AddModifier(Mod);
	}

	protected override void UndoImplementation()
	{
		Attack.RemoveModifier(Mod);
	}
}
