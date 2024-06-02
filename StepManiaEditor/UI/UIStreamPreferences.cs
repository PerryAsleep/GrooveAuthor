using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.PreferencesStream;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing stream preferences UI.
/// </summary>
internal sealed class UIStreamPreferences
{
	public const string WindowTitle = "Stream Preferences";

	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static void Draw()
	{
		var p = Preferences.Instance.PreferencesStream;
		if (!p.ShowStreamPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowStreamPreferencesWindow, DefaultWidth))
			DrawContents();
		ImGui.End();
	}

	public static void DrawContents()
	{
		var p = Preferences.Instance.PreferencesStream;

		if (ImGuiLayoutUtils.BeginTable("Stream", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowSubdivisions(true, "Note Type", p, nameof(PreferencesStream.NoteType), false,
				"The note type to use when considering whether a measure is part of a stream.");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Break Lengths", p, nameof(PreferencesStream.ShowBreakLengths), false,
				"If true then breaks will show with full lengths. If false then breaks will show with abbreviated notation.");

			ImGuiLayoutUtils.DrawRowDragInt(true, "Min Stream Length", p,
				nameof(PreferencesStream.MinimumLengthToConsiderStream), false,
				"The minimum length in measures for stream to be counted.", 0.1F, "%i measures", 0, 8);

			ImGuiLayoutUtils.DrawRowDragInt(true, "Short Break Length", p,
				nameof(PreferencesStream.ShortBreakCutoff), false,
				"Breaks at or under this many measures will be considered short breaks for stream notation.", 0.1F,
				"%i measures", 0, 64);

			ImGuiLayoutUtils.DrawRowCharacterInput(true, "Short Break Mark", p, nameof(PreferencesStream.ShortBreakCharacter),
				false,
				"Character to use to represent short breaks in stream notation.");

			ImGuiLayoutUtils.DrawRowCharacterInput(true, "Long Break Mark", p, nameof(PreferencesStream.LongBreakCharacter),
				false,
				"Character to use to represent long breaks in stream notation.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("Density", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Density Graph", p, nameof(PreferencesStream.ShowDensityGraph), false,
				"Whether or not to show the density graph.");

			ImGuiLayoutUtils.DrawRowEnum<DensityGraphPosition>(true, "Position", p,
				nameof(PreferencesStream.DensityGraphPositionValue), false,
				"Position of the density graph.");

			ImGuiLayoutUtils.DrawRowDragInt(true, "Position Offset", p,
				nameof(PreferencesStream.DensityGraphPositionOffset), false,
				"Position offset of the density graph.", 1.0f, "%i", -1024, 1024);

			ImGuiLayoutUtils.DrawRowDragInt(true, "Width Offset", p,
				nameof(PreferencesStream.DensityGraphWidthOffset), false,
				"Width offset of the density graph.", 1.0f, "%i", -1024, 1024);

			ImGuiLayoutUtils.DrawRowDragInt(true, "Height", p,
				nameof(PreferencesStream.DensityGraphHeight), false,
				"Height of the density graph.", 1.0f, "%i", 16, 1024);

			ImGuiLayoutUtils.DrawRowEnum<DensityGraphColorMode>(true, "Color Mode", p,
				nameof(PreferencesStream.DensityGraphColorModeValue), false,
				"How to color the density graph.");

			ImGuiLayoutUtils.DrawRowColorEdit4(true, "High Color", p,
				nameof(PreferencesStream.DensityGraphHighColor),
				ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
				"High color for the density graph.");

			ImGuiLayoutUtils.DrawRowColorEdit4(true, "Low Color", p,
				nameof(PreferencesStream.DensityGraphLowColor),
				ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
				"Low color for the density graph.");

			ImGuiLayoutUtils.DrawRowColorEdit4(true, "Background Color", p,
				nameof(PreferencesStream.DensityGraphBackgroundColor),
				ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
				"Background color for the density graph.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("Stream Restore", TitleColumnWidth))
		{
			if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
				    "Restore all stream preferences to their default values."))
			{
				p.RestoreDefaults();
			}

			ImGuiLayoutUtils.EndTable();
		}
	}
}
