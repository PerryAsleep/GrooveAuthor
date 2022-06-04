using System.Numerics;
using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing scroll preferences UI.
	/// </summary>
	public class UIScrollPreferences
	{
		public void Draw()
		{
			var p = Preferences.Instance.PreferencesScroll;
			if (!p.ShowScrollControlPreferencesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Scroll Preferences", ref p.ShowScrollControlPreferencesWindow, ImGuiWindowFlags.NoScrollbar);

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Scroll", 120))
			{
				ImGuiLayoutUtils.DrawRowEnum<Editor.ScrollMode>(true, "Scroll Mode", p, nameof(PreferencesScroll.ScrollMode),
					"The Scroll Mode to use when editing. When playing the Scroll Mode is always Time."
					+ "\nTime: Scrolling moves time."
					+ "\nRow:  Scrolling moves rows.");

				ImGuiLayoutUtils.DrawRowEnum<Editor.SpacingMode>(true, "Spacing Mode", p, nameof(PreferencesScroll.SpacingMode),
					"How events in the Chart should be spaced when rendering."
					+ "\nConstant Time: Events are spaced by their time."
					+ "\n               Equivalent to a CMOD when scrolling by time."
					+ "\nConstant Row:  Spacing is based on row and rows are treated as always the same distance apart."
					+ "\n               Scroll rate modifiers are ignored."
					+ "\n               Other rate altering events like stops and tempo changes affect the scroll rate."
					+ "\nVariable:      Spacing is based on tempo and is affected by all rate altering events."
					+ "\n               Equivalent to a XMOD when scrolling by time.");

				ImGuiLayoutUtils.DrawRowEnum<Editor.WaveFormScrollMode>(true, "Waveform Scroll Mode", p, nameof(PreferencesScroll.RowBasedWaveFormScrollMode),
					"How events in the Chart should be spaced when rendering."
					+ "\nConstant Time: Events are spaced by their time."
					+ "\n               Equivalent to a CMOD when scrolling by time."
					+ "\nConstant Row:  Spacing is based on row and rows are treated as always the same distance apart."
					+ "\n               Scroll rate modifiers are ignored."
					+ "\n               Other rate altering events like stops and tempo changes affect the scroll rate."
					+ "\nVariable:      Spacing is based on tempo and is affected by all rate altering events."
					+ "\n               Equivalent to a XMOD when scrolling by time.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Scroll Stop", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox("Stop On Scroll", ref p.StopPlaybackWhenScrolling,
					"Stop song playback when manually scrolling the chart.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Constant Time Spacing Options");
			if (ImGuiLayoutUtils.BeginTable("Scroll Constant Time", 120))
			{
				ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
					true,
					"Speed",
					p,
					nameof(PreferencesScroll.TimeBasedPixelsPerSecond),
					1.0f,
					100000.0f,
					PreferencesScroll.DefaultTimeBasedPixelsPerSecond,
					"Speed in pixels per second at default zoom level.",
					"%.3f",
					ImGuiSliderFlags.Logarithmic);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Constant Row Spacing Options");
			if (ImGuiLayoutUtils.BeginTable("Scroll Constant Row", 120))
			{
				ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
					true,
					"Spacing",
					p,
					nameof(PreferencesScroll.RowBasedPixelsPerRow),
					1.0f,
					100000.0f,
					PreferencesScroll.DefaultRowBasedPixelsPerRow,
					$"Spacing in pixels per row at default zoom level. A row is 1/{SMCommon.MaxValidDenominator} of a {SMCommon.NumBeatsPerMeasure}/{SMCommon.NumBeatsPerMeasure} beat.",
					"%.3f",
					ImGuiSliderFlags.Logarithmic);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Variable Spacing Options");
			if (ImGuiLayoutUtils.BeginTable("Scroll Variable", 120))
			{
				ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
					true,
					"Speed",
					p,
					nameof(PreferencesScroll.VariablePixelsPerSecondAtDefaultBPM),
					1.0f,
					100000.0f,
					PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM,
					$"Speed in pixels per second at default zoom level at {PreferencesScroll.DefaultVariableSpeedBPM} BPM.",
					"%.3f",
					ImGuiSliderFlags.Logarithmic);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Scroll Restore", 120))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
						"Restore all scroll preferences to their default values."))
				{
					p.RestoreDefaults();
				}
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.End();
		}
	}
}
