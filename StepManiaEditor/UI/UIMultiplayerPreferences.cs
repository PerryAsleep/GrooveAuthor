using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

internal sealed class UIMultiplayerPreferences : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static UIMultiplayerPreferences Instance { get; } = new();

	private UIMultiplayerPreferences() : base("Multiplayer Preferences")
	{
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesMultiplayer.ShowMultiplayerPreferencesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesMultiplayer.ShowMultiplayerPreferencesWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesMultiplayer;
		if (!p.ShowMultiplayerPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowMultiplayerPreferencesWindow, DefaultWidth))
		{
			if (ImGuiLayoutUtils.BeginTable("Options Multiplayer", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Color Holds & Rolls", p,
					nameof(PreferencesMultiplayer.ColorHoldsAndRolls), false,
					"Whether to color holds and rolls based on the per-player colors for chart types"
					+ " which normally use note-agnostic colors for these notes.");

				ImGuiLayoutUtils.DrawRowDragFloat(true, "Note Alpha", p,
					nameof(PreferencesMultiplayer.RoutineNoteColorAlpha), false,
					"Alpha value for multiplayer note overlays. This affects transparency of the multiplayer notes"
					+ " to allow for normal note color to blend in.", 0.001f, "%.6f", 0.0f, 1.0f);

				DrawNoteColorRow(0, nameof(PreferencesMultiplayer.Player0Color));
				DrawNoteColorRow(1, nameof(PreferencesMultiplayer.Player1Color));
				DrawNoteColorRow(2, nameof(PreferencesMultiplayer.Player2Color));
				DrawNoteColorRow(3, nameof(PreferencesMultiplayer.Player3Color));
				DrawNoteColorRow(4, nameof(PreferencesMultiplayer.Player4Color));
				DrawNoteColorRow(5, nameof(PreferencesMultiplayer.Player5Color));
				DrawNoteColorRow(6, nameof(PreferencesMultiplayer.Player6Color));
				DrawNoteColorRow(7, nameof(PreferencesMultiplayer.Player7Color));
				DrawNoteColorRow(8, nameof(PreferencesMultiplayer.Player8Color));
				DrawNoteColorRow(9, nameof(PreferencesMultiplayer.Player9Color));

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Multiplayer Restore", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all multiplayer preferences to their default values."))
				{
					p.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.End();
	}

	private void DrawNoteColorRow(int player, string fieldName)
	{
		var p = Preferences.Instance.PreferencesMultiplayer;
		ImGuiLayoutUtils.DrawRowColorEdit3(true, $"Player {player + 1} Note Color", p,
			fieldName, ImGuiColorEditFlags.NoAlpha, false,
			$"Color for player {player + 1}'s notes.");
	}
}
