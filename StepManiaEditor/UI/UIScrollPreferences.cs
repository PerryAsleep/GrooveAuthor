using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing scroll preferences UI.
/// </summary>
internal sealed class UIScrollPreferences
{
	private static readonly int TitleColumnWidth = UiScaled(120);

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesScroll;
		if (!p.ShowScrollControlPreferencesWindow)
			return;

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin("Scroll Preferences", ref p.ShowScrollControlPreferencesWindow, ImGuiWindowFlags.NoScrollbar))
		{
			if (ImGuiLayoutUtils.BeginTable("Scroll", TitleColumnWidth))
			{
				DrawSpacingModeRow("Spacing Mode");

				ImGuiLayoutUtils.DrawRowEnum<Editor.WaveFormScrollMode>(true, "Waveform Scroll Mode", p,
					nameof(PreferencesScroll.RowBasedWaveFormScrollMode), false,
					"How the wave form should scroll when the Chart does not scroll with Constant Time."
					+ "\nCurrent Tempo:          The wave form will match the current tempo, ignoring rate changes."
					+ "\n                        Best option for Charts which have legitimate musical tempo changes."
					+ "\n                        Bad option for sm file stutter gimmicks as they momentarily double the tempo."
					+ "\nCurrent Tempo And Rate: The wave form will match the current tempo and rate."
					+ "\n                        Rates that are less than or equal 0 will be ignored."
					+ "\n                        Best option to match ssc file scroll gimmicks."
					+ "\n                        Bad option for sm file stutter gimmicks as they momentarily double the tempo."
					+ "\nMost Common Tempo:      The wave form will match the most common tempo in the Chart, ignoring rate changes."
					+ "\n                        Best option to achieve smooth scrolling when the Chart is effectively one tempo"
					+ "\n                        but has brief scroll rate gimmicks.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Scroll Wheel", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Scroll Time Value", p, nameof(p.ScrollWheelTime), false,
					"When spacing by time, how much time should be advanced by the scroll wheel in seconds at the default zoom level.",
					0.001f, "%.6fs", 0.0, 10.0);
				ImGuiLayoutUtils.DrawRowDragInt(true, "Scroll Row Value", p, nameof(p.ScrollWheelRows), false,
					"When spacing by rows, how many rows should be advanced by the scroll wheel at the default zoom level.", 1.0f,
					"%i", 0, 384);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Scroll Zoom Factor", p, nameof(p.ZoomMultiplier), false,
					"How much to zoom in or out when using the scroll wheel to alter the zoom level.", 0.0001f, "%.6f", 1.0, 4.0);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Scroll Anim Time", p, nameof(p.ScrollInterpolationDuration), false,
					"The amount of time in seconds to spend animating from one position to another when scrolling with the scroll wheel.",
					0.0001f, "%.6f", 0.0, 1.0);
				ImGuiLayoutUtils.DrawRowCheckbox("Stop On Scroll", ref p.StopPlaybackWhenScrolling,
					"Stop song playback when manually scrolling the chart.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Spacing Options");
			ImGui.TextWrapped("Shift+Scroll while over the chart changes how the notes are spaced for the current Spacing mode.");
			if (ImGuiLayoutUtils.BeginTable("Spacing Options", TitleColumnWidth))
			{
				if (p.SpacingMode != Editor.SpacingMode.ConstantTime)
					PushDisabled();

				// Don't allow undo as it doesn't make sense with scroll wheel modifications to this value.
				ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
					false,
					"Constant Time Speed",
					p,
					nameof(PreferencesScroll.TimeBasedPixelsPerSecondFloat),
					(float)ZoomManager.MinConstantTimeSpeed,
					(float)ZoomManager.MaxConstantTimeSpeed,
					(float)PreferencesScroll.DefaultTimeBasedPixelsPerSecond,
					false,
					"Speed in pixels per second at default zoom level." +
					"\nOnly used for Constant Time Spacing Mode.",
					"%.3f",
					ImGuiSliderFlags.Logarithmic);

				if (p.SpacingMode != Editor.SpacingMode.ConstantTime)
					PopDisabled();

				if (p.SpacingMode != Editor.SpacingMode.ConstantRow)
					PushDisabled();

				// Don't allow undo as it doesn't make sense with scroll wheel modifications to this value.
				ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
					false,
					"Constant Row Spacing",
					p,
					nameof(PreferencesScroll.RowBasedPixelsPerRowFloat),
					(float)ZoomManager.MinConstantRowSpacing,
					(float)ZoomManager.MaxConstantRowSpacing,
					(float)PreferencesScroll.DefaultRowBasedPixelsPerRow,
					false,
					$"Spacing in pixels per row at default zoom level. A row is 1/{SMCommon.MaxValidDenominator} of a {SMCommon.NumBeatsPerMeasure}/{SMCommon.NumBeatsPerMeasure} beat." +
					"\nOnly used for Constant Row Spacing Mode.",
					"%.3f",
					ImGuiSliderFlags.Logarithmic);

				if (p.SpacingMode != Editor.SpacingMode.ConstantRow)
					PopDisabled();

				if (p.SpacingMode != Editor.SpacingMode.Variable)
					PushDisabled();

				// Don't allow undo as it doesn't make sense with scroll wheel modifications to this value.
				ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
					false,
					"Variable Speed",
					p,
					nameof(PreferencesScroll.VariablePixelsPerSecondAtDefaultBPMFloat),
					(float)ZoomManager.MinVariableSpeed,
					(float)ZoomManager.MaxVariableSpeed,
					(float)PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM,
					false,
					$"Speed in pixels per second at default zoom level at {PreferencesScroll.DefaultVariableSpeedBPM} BPM."+
					"\nOnly used for Variable Spacing Mode.",
					"%.3f",
					ImGuiSliderFlags.Logarithmic);

				if (p.SpacingMode != Editor.SpacingMode.Variable)
					PopDisabled();

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Scroll Restore", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all scroll preferences to their default values."))
				{
					p.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.End();
	}

	public static void DrawSpacingModeRow(string title)
	{
		ImGuiLayoutUtils.DrawRowEnum<Editor.SpacingMode>(true, title, Preferences.Instance.PreferencesScroll,
			nameof(PreferencesScroll.SpacingMode), false,
			"How events in the Chart should be spaced when rendering."
			+ "\nConstant Time: Events are spaced by their time."
			+ "\n               Equivalent to a CMOD when playing."
			+ "\nConstant Row:  Spacing is based on row and rows are treated as always the same distance apart."
			+ "\n               Scroll rate modifiers are ignored."
			+ "\n               Other rate altering events like stops and tempo changes affect the scroll rate."
			+ "\nVariable:      Spacing is based on tempo and is affected by all rate altering events."
			+ "\n               Equivalent to a XMOD when playing.");
	}
}
