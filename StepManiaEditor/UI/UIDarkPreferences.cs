using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing Dark background preferences UI.
/// </summary>
internal sealed class UIDarkPreferences : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static UIDarkPreferences Instance { get; } = new();

	private UIDarkPreferences() : base("Dark Preferences")
	{
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesDark.ShowDarkPreferencesWindow = false;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesDark.ShowDarkPreferencesWindow = true;
		if (focus)
			Focus();
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesDark;
		if (!p.ShowDarkPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowDarkPreferencesWindow, DefaultWidth))
			DrawContents();
		ImGui.End();
	}

	public void DrawContents()
	{
		var p = Preferences.Instance.PreferencesDark;

		if (ImGuiLayoutUtils.BeginTable("Dark Preferences Table", TitleColumnWidth))
		{
			var keyBind = UIControls.GetCommandString(Preferences.Instance.PreferencesKeyBinds.ToggleDark);
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Dark Background", p, nameof(PreferencesDark.ShowDarkBg), false,
				"Whether to show the dark background."
				+ $"\n\nThe dark background can be toggled on and off with {keyBind}.");

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
