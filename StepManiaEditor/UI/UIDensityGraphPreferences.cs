using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.PreferencesDensityGraph;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing density graph preferences UI.
/// </summary>
internal sealed class UIDensityGraphPreferences
{
	public const string WindowTitle = "Density Graph Preferences";

	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static void Draw()
	{
		var p = Preferences.Instance.PreferencesDensityGraph;
		if (!p.ShowDensityGraphPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowDensityGraphPreferencesWindow, DefaultWidth))
			DrawContents();
		ImGui.End();
	}

	public static void DrawContents()
	{
		var p = Preferences.Instance.PreferencesDensityGraph;

		if (ImGuiLayoutUtils.BeginTable("Density", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Density Graph", p, nameof(PreferencesDensityGraph.ShowDensityGraph),
				false,
				"Whether or not to show the density graph.");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Stream", p, nameof(PreferencesDensityGraph.ShowStream),
				false,
				"Whether or not to show the stream breakdown on the density graph.");

			ImGuiLayoutUtils.DrawRowEnum<StepAccumulationType>(true, "Accumulation Type", p,
				nameof(PreferencesDensityGraph.AccumulationType), false,
				"How to count steps for the density graph." +
				"\nStep: Each individual note is counted once. Two notes on the same row count as two events." +
				"\nRow:  Multiple notes on the same row are counted as one. Two notes on the same row count as one event.");

			ImGuiLayoutUtils.DrawRowEnum<DensityGraphPosition>(true, "Position", p,
				nameof(PreferencesDensityGraph.DensityGraphPositionValue), false,
				"Position of the density graph.");

			ImGuiLayoutUtils.DrawRowDragInt(true, "Position Offset", p,
				nameof(PreferencesDensityGraph.DensityGraphPositionOffset), false,
				"Position offset of the density graph.", 1.0f, "%i", -1024, 1024);

			ImGuiLayoutUtils.DrawRowDragInt(true, "Width Offset", p,
				nameof(PreferencesDensityGraph.DensityGraphWidthOffset), false,
				"Width offset of the density graph.", 1.0f, "%i", -1024, 1024);

			ImGuiLayoutUtils.DrawRowDragInt(true, "Height", p,
				nameof(PreferencesDensityGraph.DensityGraphHeight), false,
				"Height of the density graph.", 1.0f, "%i", 16, 1024);

			ImGuiLayoutUtils.DrawRowEnum<DensityGraphColorMode>(true, "Color Mode", p,
				nameof(PreferencesDensityGraph.DensityGraphColorModeValue), false,
				"How to color the density graph.");

			ImGuiLayoutUtils.DrawRowColorEdit4(true, "High Color", p,
				nameof(PreferencesDensityGraph.DensityGraphHighColor),
				ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
				"High color for the density graph.");

			ImGuiLayoutUtils.DrawRowColorEdit4(true, "Low Color", p,
				nameof(PreferencesDensityGraph.DensityGraphLowColor),
				ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
				"Low color for the density graph.");

			ImGuiLayoutUtils.DrawRowColorEdit4(true, "Background Color", p,
				nameof(PreferencesDensityGraph.DensityGraphBackgroundColor),
				ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
				"Background color for the density graph.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("Density Graph Restore", TitleColumnWidth))
		{
			if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
				    "Restore all density graph preferences to their default values."))
			{
				p.RestoreDefaults();
			}

			ImGuiLayoutUtils.EndTable();
		}
	}
}
