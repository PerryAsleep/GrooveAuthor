using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.PreferencesNoteColor;

namespace StepManiaEditor;

internal sealed class UINoteColorPreferences : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(190);
	private static readonly int DefaultWidth = UiScaled(460);

	public static UINoteColorPreferences Instance { get; } = new();

	private UINoteColorPreferences() : base("Note Color Preferences")
	{
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesNoteColor.ShowNoteColorPreferencesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesNoteColor.ShowNoteColorPreferencesWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesNoteColor;
		if (!p.ShowNoteColorPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowNoteColorPreferencesWindow, DefaultWidth))
		{
			if (ImGuiLayoutUtils.BeginTable("Options Note Color", TitleColumnWidth))
			{
				DrawNoteColorRow("1/4", nameof(PreferencesNoteColor.QuarterColor));
				DrawNoteColorRow("1/8", nameof(PreferencesNoteColor.EighthColor));
				DrawNoteColorRow("1/12", nameof(PreferencesNoteColor.TwelfthColor));
				DrawNoteColorRow("1/16", nameof(PreferencesNoteColor.SixteenthColor));
				DrawNoteColorRow("1/24", nameof(PreferencesNoteColor.TwentyForthColor));
				DrawNoteColorRow("1/32", nameof(PreferencesNoteColor.ThirtySecondColor));
				DrawNoteColorRow("1/48", nameof(PreferencesNoteColor.FortyEighthColor));
				DrawNoteColorRow("1/64", nameof(PreferencesNoteColor.SixtyForthColor));
				DrawNoteColorRow("1/192", nameof(PreferencesNoteColor.OneHundredNinetySecondColor));

				ImGuiLayoutUtils.DrawRowColorEdit3(true, "Hold Body Color", p,
					nameof(PreferencesNoteColor.HoldColor), ImGuiColorEditFlags.NoAlpha, false,
					"Hold body color for chart types which use lane agnostic hold colors.");
				ImGuiLayoutUtils.DrawRowColorEdit3(true, "Roll Body Color", p,
					nameof(PreferencesNoteColor.RollColor), ImGuiColorEditFlags.NoAlpha, false,
					"Roll body color for chart types which use lane agnostic roll colors.");
				ImGuiLayoutUtils.DrawRowColorEdit3(true, "Mine Color", p,
					nameof(PreferencesNoteColor.MineColor), ImGuiColorEditFlags.NoAlpha, false,
					"Mine color.");

				var index = -1;
				if (ImGuiLayoutUtils.DrawRowEnum<ColorSet>("Presets", "NoteColorPresetCombo", ref index,
					    "Presets for common note colors.") && index >= 0)
				{
					var colorSet = (ColorSet)index;
					ActionQueue.Instance.Do(new ActionSetNoteColorSet(colorSet));
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options Note Color Multipliers", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowDragFloat(true, "Selected Color Multiplier", p,
					nameof(PreferencesNoteColor.SelectionColorMultiplier), false,
					"Color multiplier for notes when selected.", 0.001f, "%.6fx", 0.0f, 10.0f);
				ImGuiLayoutUtils.DrawRowDragFloat(true, "Held Color Multiplier", p,
					nameof(PreferencesNoteColor.HeldColorMultiplier), false,
					"Color multiplier for holds and rolls when they are held due to autoplay.", 0.001f, "%.6fx", 0.0f, 10.0f);
				ImGuiLayoutUtils.DrawRowDragFloat(true, "UI Color Multiplier", p,
					nameof(PreferencesNoteColor.ArrowUIColorMultiplier), false,
					"Color multiplier when note colors are displayed in UI, like when coloring text and notes in the minimap.",
					0.001f, "%.6fx", 0.0f, 10.0f);
				ImGuiLayoutUtils.DrawRowDragFloat(true, "UI Selected Color Multiplier", p,
					nameof(PreferencesNoteColor.ArrowUISelectedColorMultiplier), false,
					"Color multiplier when note colors are displayed in UI when they are selected, like in the mini map. " +
					"It is intentional that by default this value is large as it is more important to represent selected " +
					"status than it is to represent note color in these contexts.", 0.001f, "%.6fx", 0.0f, 10.0f);
				UIOptions.DrawStepColoring("Time Signature Coloring");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options PIU Note Color", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowColorEdit3(true, "Pump Top Row Color", p,
					nameof(PreferencesNoteColor.PiuTopColor), ImGuiColorEditFlags.NoAlpha, false,
					"Note color for the top row in pump charts.");

				ImGuiLayoutUtils.DrawRowColorEdit3(true, "Pump Middle Color", p,
					nameof(PreferencesNoteColor.PiuMiddleColor), ImGuiColorEditFlags.NoAlpha, false,
					"Note color for the middle panel in pump charts.");

				ImGuiLayoutUtils.DrawRowColorEdit3(true, "Pump Bottom Row Color", p,
					nameof(PreferencesNoteColor.PiuBottomColor), ImGuiColorEditFlags.NoAlpha, false,
					"Note color for the bottom row in pump charts.");

				ImGuiLayoutUtils.DrawRowDragFloat(true, "Pump Hold Color Saturation", p,
					nameof(PreferencesNoteColor.PiuHoldSaturationMultiplier), false,
					"Saturation multiplier for hold and roll colors in pump charts.", 0.001f, "%.6fx", 0.0f, 8.0f);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options Multiplayer", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Color Multiplayer Holds & Rolls", p,
					nameof(PreferencesNoteColor.ColorMultiplayerHoldsAndRolls), false,
					"Whether to color holds and rolls based on the per-player colors for chart types"
					+ " which normally use note-agnostic colors for these notes.");

				ImGuiLayoutUtils.DrawRowDragFloat(true, "Multiplayer Color Alpha", p,
					nameof(PreferencesNoteColor.RoutineNoteColorAlpha), false,
					"Alpha value for multiplayer note colors. This affects transparency of the multiplayer color"
					+ " to allow for normal note color to blend in.", 0.001f, "%.6fx", 0.0f, 1.0f);

				DrawPlayerNoteColorRow(0, nameof(PreferencesNoteColor.Player0Color));
				DrawPlayerNoteColorRow(1, nameof(PreferencesNoteColor.Player1Color));
				DrawPlayerNoteColorRow(2, nameof(PreferencesNoteColor.Player2Color));
				DrawPlayerNoteColorRow(3, nameof(PreferencesNoteColor.Player3Color));
				DrawPlayerNoteColorRow(4, nameof(PreferencesNoteColor.Player4Color));
				DrawPlayerNoteColorRow(5, nameof(PreferencesNoteColor.Player5Color));
				DrawPlayerNoteColorRow(6, nameof(PreferencesNoteColor.Player6Color));
				DrawPlayerNoteColorRow(7, nameof(PreferencesNoteColor.Player7Color));
				DrawPlayerNoteColorRow(8, nameof(PreferencesNoteColor.Player8Color));
				DrawPlayerNoteColorRow(9, nameof(PreferencesNoteColor.Player9Color));

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Note Color Restore", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all note color preferences to their default values."))
				{
					p.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.End();
	}

	private void DrawNoteColorRow(string noteType, string fieldName)
	{
		var p = Preferences.Instance.PreferencesNoteColor;
		ImGuiLayoutUtils.DrawRowColorEdit3(true, $"{noteType} Note Color", p,
			fieldName, ImGuiColorEditFlags.NoAlpha, false,
			$"Color for {noteType} notes in chart types which color notes based on row.");
	}

	private void DrawPlayerNoteColorRow(int player, string fieldName)
	{
		var p = Preferences.Instance.PreferencesNoteColor;
		ImGuiLayoutUtils.DrawRowColorEdit3(true, $"Player {player + 1} Note Color", p,
			fieldName, ImGuiColorEditFlags.NoAlpha, false,
			$"Color for player {player + 1}'s notes.");
	}
}
