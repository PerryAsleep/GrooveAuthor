using Fumen;
using Fumen.ChartDefinition;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor.UI;

/// <summary>
/// Class for drawing information about an EditorAttackEvent in a chart.
/// </summary>
internal sealed class UIAttackEvent : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(80);
	private static readonly int DefaultWidth = UiScaled(460);

	private Editor Editor;

	public static UIAttackEvent Instance { get; } = new();

	private UIAttackEvent() : base("Attack Event Properties")
	{
	}


	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowAttackEventWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowAttackEventWindow = false;
	}

	public void Draw(EditorAttackEvent attackEvent)
	{
		if (attackEvent == null)
		{
			Preferences.Instance.ShowAttackEventWindow = false;
		}

		if (!Preferences.Instance.ShowAttackEventWindow)
			return;

		var attack = attackEvent!.GetAttack();

		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowAttackEventWindow, DefaultWidth))
		{
			var disabled = !Editor.CanEdit();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("AttackEventTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowChartPosition("Position", Editor, attackEvent,
					"The position of the attack.");

				if (ImGuiLayoutUtils.DrawRowButton("Add Mod", "Add Modifier", "Add a new modifier to this attack."))
				{
					ActionQueue.Instance.Do(new ActionAddModToAttack(attackEvent));
				}

				ImGuiLayoutUtils.EndTable();
			}

			for (var i = 0; i < attack.Modifiers.Count; i++)
			{
				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable($"ModTable{i}", TitleColumnWidth))
				{
					var mod = attack.Modifiers[i];

					var oldName = mod.Name;
					ImGuiLayoutUtils.DrawRowTextInput(true, "Modifier", mod, nameof(Modifier.Name), true,
						"Modifier to apply. Stepmania supports a number of modifiers but they vary by fork.");
					if (mod.Name != oldName)
						attackEvent.OnModifiersChanged();

					var oldLevel = mod.Level;
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Level", mod, nameof(Modifier.Level), true,
						"Modifier level. Sometimes referred to as strength. 100% is the default level. 0% will disable a modifier. Negative values will invert some modifiers.",
						1.0f, "%.6f%%");
					if (!mod.Level.DoubleEquals(oldLevel))
						attackEvent.OnModifiersChanged();

					var oldSpeed = mod.Speed;
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Speed", mod, nameof(Modifier.Speed), true,
						"Speed at which the modifier is applied.",
						0.01f, "%.6fs");
					if (!mod.Speed.DoubleEquals(oldSpeed))
						attackEvent.OnModifiersChanged();

					var oldLength = mod.LengthSeconds;
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Length", mod, nameof(Modifier.LengthSeconds), true,
						"Length of the modifier.",
						0.01f, "%.6fs");
					if (!mod.LengthSeconds.DoubleEquals(oldLength))
						attackEvent.OnModifiersChanged();

					if (ImGuiLayoutUtils.DrawRowButton("Delete", "Delete Modifier", "Delete this Modifier."))
					{
						ActionQueue.Instance.Do(new ActionDeleteModFromAttack(attackEvent, mod));
					}

					ImGuiLayoutUtils.EndTable();
				}
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}
}
