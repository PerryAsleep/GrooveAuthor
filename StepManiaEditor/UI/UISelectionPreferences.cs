using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing selection preferences UI.
/// </summary>
internal sealed class UISelectionPreferences : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static UISelectionPreferences Instance { get; } = new();

	private UISelectionPreferences() : base("Selection Preferences")
	{
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesSelection.ShowSelectionControlPreferencesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesSelection.ShowSelectionControlPreferencesWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesSelection;
		if (!p.ShowSelectionControlPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowSelectionControlPreferencesWindow, DefaultWidth))
		{
			if (ImGuiLayoutUtils.BeginTable("Selection", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowEnum<PreferencesSelection.SelectionMode>(true, "Drag Mode", p,
					nameof(PreferencesSelection.Mode), false,
					"How notes should be selected when dragging the cursor to select a region."
					+ "\nOverlap Any:    If the region overlaps any part of a note, it will be selected."
					+ "\nOverlap Center: If the region overlaps the center of a note, it will be selected."
					+ "\nOverlap All:    If the region overlaps the entire note, it will be selected.");

				ImGuiLayoutUtils.DrawRowEnum<PreferencesSelection.SelectionRegionMode>(true, "Click Mode", p,
					nameof(PreferencesSelection.RegionMode), false,
					"How notes should be selected when clicking a subsequent note while holding shift."
					+ "\nFor all options notes are selected by time when the Spacing Mode is Constant Time and Row is used when"
					+ "\nthe Spacing Mode is Constant Row or Variable."
					+ "\nTime Or Position:          Select all notes in between the previously selected note and the newly"
					+ "\n                           selected note by time or row."
					+ "\nTime Or Position And Lane: Select all notes in between the previously selected note and the newly"
					+ "\n                           selected note by time or row, and additionally restrict the selection to"
					+ "\n                           notes whose lanes are within the lanes in between the lanes of the"
					+ "\n                           previously selected note and the newly selected note.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Spacing Restore", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all spacing preferences to their default values."))
				{
					p.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.End();
	}
}
