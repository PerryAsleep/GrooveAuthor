using System;
using Fumen.Converters;
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
	private static readonly float ButtonSetWidth = UiScaled(108);
	private static readonly float ButtonGoWidth = UiScaled(20);

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

		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowAttackEventWindow, DefaultWidth))
		{
			var disabled = !Editor.CanEdit();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("AttackEventTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowChartPosition("Position", Editor, attackEvent,
					"The position of the attack.");

				if (ImGuiLayoutUtils.DrawRowButton("Add Modifier", "Add Modifier", "Add a new modifier to this attack."))
				{
					double modLength;
					var existingMods = attackEvent!.GetModifiers();
					if (existingMods != null && existingMods.Count > 0)
					{
						modLength = existingMods[0].LengthSeconds;
					}
					else
					{
						var currentRateAlteringEvent = attackEvent.GetEditorChart()?.GetRateAlteringEvents()
							?.FindActiveRateAlteringEventForPosition(attackEvent.GetRow());
						modLength = currentRateAlteringEvent!.GetSecondsPerRow() * SMCommon.MaxValidDenominator;
					}

					ActionQueue.Instance.Do(new ActionAddModToAttack(attackEvent, modLength));
				}

				ImGuiLayoutUtils.EndTable();
			}

			var mods = attackEvent!.GetModifiers();
			for (var i = 0; i < mods.Count; i++)
			{
				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable($"ModTable{i}", TitleColumnWidth))
				{
					var mod = mods[i];

					ImGuiLayoutUtils.DrawRowModifier(mod, nameof(EditorAttackEvent.EditorModifier.Name), true,
						EditorAttackEvent.ModifierTypes);

					ImGuiLayoutUtils.DrawRowDragDouble(true, "Level", mod, nameof(EditorAttackEvent.EditorModifier.Level), true,
						"Modifier level. Sometimes referred to as strength. 100% is the default level. 0% will disable a modifier. Negative values will invert some modifiers.",
						1.0f, "%.6f%%");

					ImGuiLayoutUtils.DrawRowDragDouble(true, "Speed", mod, nameof(EditorAttackEvent.EditorModifier.Speed), true,
						"Speed at which the modifier is applied, represented as a multiplier. 1.0x is the default speed of 1 second.",
						0.01f, "%.6fx");

					ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "Length", mod,
						nameof(EditorAttackEvent.EditorModifier.LengthSeconds), true,
						() => SetModLengthFromCurrentTime(attackEvent, mod), "Use Current Time", ButtonSetWidth,
						() => JumpToModEnd(attackEvent, mod), "Go", ButtonGoWidth,
						"Length of the modifier.",
						0.01f, "%.6fs", 0.0);

					if (ImGuiLayoutUtils.DrawRowButton("Delete", "Delete Modifier", "Delete this Modifier."))
					{
						ActionQueue.Instance.Do(new ActionDeleteModFromAttack(attackEvent, i));
					}

					ImGuiLayoutUtils.EndTable();
				}
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}

	private void SetModLengthFromCurrentTime(EditorAttackEvent attack, EditorAttackEvent.EditorModifier mod)
	{
		var currentTime = Math.Max(0.0, Editor.GetPosition().ChartTime);
		var startTime = attack.GetChartTime();
		var modLength = Math.Max(0.0, currentTime - startTime);
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyValue<double>(mod, nameof(EditorAttackEvent.EditorModifier.LengthSeconds),
				modLength, true));
	}

	private void JumpToModEnd(EditorAttackEvent attack, EditorAttackEvent.EditorModifier mod)
	{
		Editor.SetChartTime(attack.GetChartTime() + mod.LengthSeconds);
	}
}
