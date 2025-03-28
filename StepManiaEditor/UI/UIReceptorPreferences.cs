﻿using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing receptor preferences UI.
/// </summary>
internal sealed class UIReceptorPreferences : UIWindow
{
	private Editor Editor;

	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static UIReceptorPreferences Instance { get; } = new();

	private UIReceptorPreferences() : base("Receptor Preferences")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesReceptors.ShowReceptorPreferencesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesReceptors.ShowReceptorPreferencesWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesReceptors;
		if (!p.ShowReceptorPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowReceptorPreferencesWindow, DefaultWidth))
			DrawContents();
		ImGui.End();
	}

	public void DrawContents()
	{
		var p = Preferences.Instance.PreferencesReceptors;

		ImGui.TextUnformatted("Position");
		if (ImGuiLayoutUtils.BeginTable("Receptor Placement", TitleColumnWidth))
		{
			var pushDisabled = p.LockPosition;
			if (pushDisabled)
				PushDisabled();

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Center Horizontally", p, nameof(PreferencesReceptors.CenterHorizontally),
				false,
				"Whether to keep the receptors centered horizontally in the window.");

			var keybind = UIControls.GetCommandString(Preferences.Instance.PreferencesKeyBinds.LockReceptorMoveAxis);
			ImGuiLayoutUtils.DrawRowDragInt2(true, "Position", p, nameof(PreferencesReceptors.ChartSpacePositionX),
				nameof(PreferencesReceptors.ChartSpacePositionY), false, !p.CenterHorizontally, true,
				"Position of the receptors."
				+ "\nThe receptors can also be moved by dragging them with the left mouse button."
				+ $"\nHold {keybind} while dragging to limit movement to one dimension.", 1.0f, "%i", 0,
				Editor.GetViewportWidth() - 1,
				0, Editor.GetViewportHeight() - 1);

			if (pushDisabled)
				PopDisabled();

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Lock Position", p, nameof(PreferencesReceptors.LockPosition), false,
				"Whether to lock the receptors to the current position and prevent position changes.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		ImGui.TextUnformatted("Animation Misc");
		if (ImGuiLayoutUtils.BeginTable("Receptor Animation Misc", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Pulse Receptors", p, nameof(PreferencesReceptors.PulseReceptorsWithTempo),
				false,
				"Whether to pulse the receptors to the chart tempo.");
			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		ImGui.TextUnformatted("Autoplay Animations");
		if (ImGuiLayoutUtils.BeginTable("Receptor Animation Autoplay", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Hide Arrows", p, nameof(PreferencesReceptors.AutoPlayHideArrows), false,
				"When playing, whether to hide the arrows after they pass the receptors.");
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Light Holds", p, nameof(PreferencesReceptors.AutoPlayLightHolds), false,
				"When playing, whether to highlight hold and roll notes when they would be active.");
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Rim Effect", p, nameof(PreferencesReceptors.AutoPlayRimEffect), false,
				"When playing, whether to show a rim effect on the receptors from simulated input.");
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Glow Effect", p, nameof(PreferencesReceptors.AutoPlayGlowEffect), false,
				"When playing, whether to show a glow effect on the receptors from simulated input.");
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Shrink Effect", p, nameof(PreferencesReceptors.AutoPlayShrinkEffect), false,
				"When playing, whether to shrink the receptors from simulated input.");
			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		ImGui.TextUnformatted("Input Animations");
		if (ImGuiLayoutUtils.BeginTable("Receptor Animation Input", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Rim Effect", p, nameof(PreferencesReceptors.TapRimEffect), false,
				"When tapping an arrow, whether to show a rim effect on the receptors.");
			ImGuiLayoutUtils.DrawRowCheckbox(true, "Shrink Effect", p, nameof(PreferencesReceptors.TapShrinkEffect), false,
				"When tapping an arrow, whether to shrink the receptors.");
			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("Receptor Animation Restore", TitleColumnWidth))
		{
			if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
				    "Restore all animation preferences to their default values."))
			{
				p.RestoreDefaults();
			}

			ImGuiLayoutUtils.EndTable();
		}
	}
}
