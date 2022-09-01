using System.Numerics;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing animation preferences UI.
	/// </summary>
	public class UIAnimationsPreferences
	{
		public void Draw()
		{
			var p = Preferences.Instance.PreferencesAnimations;
			if (!p.ShowAnimationsPreferencesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Animation Preferences", ref p.ShowAnimationsPreferencesWindow, ImGuiWindowFlags.NoScrollbar);

			if (ImGuiLayoutUtils.BeginTable("Animation Misc", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Pulse Receptors", p, nameof(PreferencesAnimations.PulseReceptorsWithTempo),
					"Whether to pulse the receptors to the chart tempo.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Autoplay Options");
			if (ImGuiLayoutUtils.BeginTable("Animation Autoplay", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Hide Arrows", p, nameof(PreferencesAnimations.AutoPlayHideArrows),
					"When playing, whether to hide the arrows after they pass the receptors.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Light Holds", p, nameof(PreferencesAnimations.AutoPlayLightHolds),
					"When playing, whether to highlight hold and roll notes when they would be active.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Rim Effect", p, nameof(PreferencesAnimations.AutoPlayRimEffect),
					"When playing, whether to show a rim effect on the receptors from simulated input.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Glow Effect", p, nameof(PreferencesAnimations.AutoPlayGlowEffect),
					"When playing, whether to show a glow effect on the receptors from simulated input.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Shrink Effect", p, nameof(PreferencesAnimations.AutoPlayShrinkEffect),
					"When playing, whether to shrink the receptors from simulated input.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Input Options");
			if (ImGuiLayoutUtils.BeginTable("Animation Input", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Rim Effect", p, nameof(PreferencesAnimations.TapRimEffect),
					"When tapping an arrow, whether to show a rim effect on the receptors.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Shrink Effect", p, nameof(PreferencesAnimations.TapShrinkEffect),
					"When tapping an arrow, whether to shrink the receptors.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Animation Restore", 120))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all animation preferences to their default values."))
				{
					p.RestoreDefaults();
				}
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.End();
		}
	}
}
