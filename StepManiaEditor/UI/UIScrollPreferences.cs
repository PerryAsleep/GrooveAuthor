using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
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

					ImGuiLayoutUtils.DrawRowEnum<Editor.WaveFormScrollMode>(true, "Waveform Scroll Mode", p, nameof(PreferencesScroll.RowBasedWaveFormScrollMode), false,
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
				if (ImGuiLayoutUtils.BeginTable("Scroll Stop", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowCheckbox("Stop On Scroll", ref p.StopPlaybackWhenScrolling,
						"Stop song playback when manually scrolling the chart.");
					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				ImGui.Text("Constant Time Spacing Options");
				if (ImGuiLayoutUtils.BeginTable("Scroll Constant Time", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
						true,
						"Speed",
						p,
						nameof(PreferencesScroll.TimeBasedPixelsPerSecond),
						1.0f,
						100000.0f,
						PreferencesScroll.DefaultTimeBasedPixelsPerSecond,
						false,
						"Speed in pixels per second at default zoom level.",
						"%.3f",
						ImGuiSliderFlags.Logarithmic);
					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				ImGui.Text("Constant Row Spacing Options");
				if (ImGuiLayoutUtils.BeginTable("Scroll Constant Row", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
						true,
						"Spacing",
						p,
						nameof(PreferencesScroll.RowBasedPixelsPerRow),
						1.0f,
						100000.0f,
						PreferencesScroll.DefaultRowBasedPixelsPerRow,
						false,
						$"Spacing in pixels per row at default zoom level. A row is 1/{SMCommon.MaxValidDenominator} of a {SMCommon.NumBeatsPerMeasure}/{SMCommon.NumBeatsPerMeasure} beat.",
						"%.3f",
						ImGuiSliderFlags.Logarithmic);
					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				ImGui.Text("Variable Spacing Options");
				if (ImGuiLayoutUtils.BeginTable("Scroll Variable", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
						true,
						"Speed",
						p,
						nameof(PreferencesScroll.VariablePixelsPerSecondAtDefaultBPM),
						1.0f,
						100000.0f,
						PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM,
						false,
						$"Speed in pixels per second at default zoom level at {PreferencesScroll.DefaultVariableSpeedBPM} BPM.",
						"%.3f",
						ImGuiSliderFlags.Logarithmic);
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
			ImGuiLayoutUtils.DrawRowEnum<Editor.SpacingMode>(true, title, Preferences.Instance.PreferencesScroll, nameof(PreferencesScroll.SpacingMode), false,
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
}
