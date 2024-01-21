using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing Dark background preferences UI.
/// </summary>
internal sealed class UIDarkPreferences
{
	public const string WindowTitle = "Dark Preferences";

	private static readonly int TitleColumnWidth = UiScaled(120);

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesDark;
		if (!p.ShowDarkPreferencesWindow)
			return;

		ImGui.SetNextWindowSize(Vector2.Zero, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref p.ShowDarkPreferencesWindow, ImGuiWindowFlags.NoScrollbar))
			DrawContents();
		ImGui.End();
	}

	public void DrawContents()
	{
		var p = Preferences.Instance.PreferencesDark;

		if (ImGuiLayoutUtils.BeginTable("Dark Preferences Table", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Dark Background", p, nameof(PreferencesDark.ShowDarkBg), false,
				"Whether to show the dark background.");

			ImGuiLayoutUtils.DrawRowEnum<PreferencesDark.DrawOrder>(true, "Draw Order", p,
				nameof(PreferencesDark.DarkBgDrawOrder), false,
				"When to draw the dark background relative to other elements.");

			ImGuiLayoutUtils.DrawRowEnum<PreferencesDark.SizeMode>(true, "Size", p, nameof(PreferencesDark.Size), false,
				"How to size the dark background area.");

			ImGuiLayoutUtils.DrawRowColorEdit4(true, "Color", p,
				nameof(PreferencesDark.Color),
				ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
				"Color of the dark background.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("Dark Preferences Restore", TitleColumnWidth))
		{
			if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
				    "Restore all dark background preferences to their default values."))
			{
				p.RestoreDefaults();
			}

			ImGuiLayoutUtils.EndTable();
		}
	}
}
