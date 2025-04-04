﻿using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing MiniMap preferences UI.
/// </summary>
internal sealed class UIMiniMapPreferences : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static UIMiniMapPreferences Instance { get; } = new();

	private UIMiniMapPreferences() : base("MiniMap Preferences")
	{
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesMiniMap.ShowMiniMapPreferencesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesMiniMap.ShowMiniMapPreferencesWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesMiniMap;
		if (!p.ShowMiniMapPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowMiniMapPreferencesWindow, DefaultWidth))
			DrawContents();
		ImGui.End();
	}

	public void DrawContents()
	{
		var p = Preferences.Instance.PreferencesMiniMap;

		if (ImGuiLayoutUtils.BeginTable("Show MiniMap", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Mini Map", p, nameof(PreferencesMiniMap.ShowMiniMap), false,
				"Whether to show the mini map." +
				"\nDisabling the mini map will increase performance.");
			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("MiniMap Layout", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowEnum<MiniMap.Position>(true, "Position", p, nameof(PreferencesMiniMap.MiniMapPosition), false,
				"Where the mini map should be located.");

			ImGuiLayoutUtils.DrawRowDragInt(true, "Position Offset", p,
				nameof(PreferencesMiniMap.PositionOffset), false,
				"Position offset of the mini map.", 1.0f, "%i", -1024, 1024);

			ImGuiLayoutUtils.DrawRowSliderUInt(true, "Width", p, nameof(PreferencesMiniMap.MiniMapWidth), 2, 128, false,
				"%i pixels",
				ImGuiSliderFlags.None,
				"The width of the mini map in pixels.");

			ImGuiLayoutUtils.DrawRowSliderUInt(true, "Note Width", p, nameof(PreferencesMiniMap.MiniMapNoteWidth), 1, 32,
				false, "%i pixels", ImGuiSliderFlags.None,
				"The width of the notes in the mini map in pixels.");

			ImGuiLayoutUtils.DrawRowSliderUInt(true, "Note Spacing", p, nameof(PreferencesMiniMap.MiniMapNoteSpacing), 0,
				32, false, "%i pixels", ImGuiSliderFlags.None,
				"The spacing between notes in the mini map in pixels.");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Quantize Positions", p, nameof(PreferencesMiniMap.QuantizePositions), false,
				"If true then elements on the mini map will be quantized to pixel boundaries."
				+ " This will result in a crisp image that will have a choppier scroll."
				+ " If false then elements will blend smoothly between pixel boundaries."
				+ " This will result in a smooth scroll but colors may appear to pulse as they blend.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("MiniMap Extra Notes", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Patterns", p, nameof(PreferencesMiniMap.ShowPatterns), false,
				"Whether or not to show pattern regions in the mini map.");
			ImGuiLayoutUtils.DrawRowSliderUInt(true, "Pattern Width", p, nameof(PreferencesMiniMap.PatternsWidth), 1, 128,
				false, "%i pixels", ImGuiSliderFlags.None,
				"The width of the pattern regions in the mini map in pixels.");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Preview", p, nameof(PreferencesMiniMap.ShowPreview), false,
				"Whether or not to show the preview region in the mini map.");
			ImGuiLayoutUtils.DrawRowSliderUInt(true, "Preview Width", p, nameof(PreferencesMiniMap.PreviewWidth), 1, 128,
				false, "%i pixels", ImGuiSliderFlags.None,
				"The width of the preview region in the mini map in pixels.");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Labels", p, nameof(PreferencesMiniMap.ShowLabels), false,
				"Whether or not to show labels in the mini map.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("MiniMap Selection", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowEnum<MiniMap.SelectMode>(true, "Select Mode", p, nameof(PreferencesMiniMap.MiniMapSelectMode),
				false,
				"How the position should move when selecting an area outside of the editor range in the mini map."
				+ "\nMove To Cursor:         Move the editor position to the cursor, not to the area under the cursor."
				+ "\n                        This is the natural option if you consider the mini map like a scroll bar."
				+ "\nMove To Selected Area:  Move the editor position to the area under the cursor, not to the cursor."
				+ "\n                        This is the natural option if you consider the mini map like a map.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("MiniMap Spacing", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowEnum(true, "Variable Mode", p, nameof(PreferencesMiniMap.MiniMapSpacingModeForVariable),
				PreferencesMiniMap.MiniMapVariableSpacingModes, false,
				"The Spacing Mode the MiniMap should use when the Scroll Spacing Mode is Variable.");

			ImGuiLayoutUtils.DrawRowSliderUInt(true, "Constant Time Range", p, nameof(PreferencesMiniMap.MiniMapVisibleTimeRange),
				30, 300, false,
				"%i seconds", ImGuiSliderFlags.Logarithmic,
				"The amount of time visible on the mini map when using Constant Time spacing.");

			ImGuiLayoutUtils.DrawRowSliderUInt(true, "Constant Row Range", p, nameof(PreferencesMiniMap.MiniMapVisibleRowRange),
				3072, 28800, false,
				"%i rows", ImGuiSliderFlags.Logarithmic,
				"The amount of space visible on the mini map when using Constant Row spacing.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("MiniMap Restore", TitleColumnWidth))
		{
			if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
				    "Restore all mini map preferences to their default values."))
			{
				p.RestoreDefaults();
			}

			ImGuiLayoutUtils.EndTable();
		}
	}
}
