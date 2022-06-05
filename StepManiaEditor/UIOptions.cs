using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor
{
	public class UIOptions
	{
		public void Draw()
		{
			var p = Preferences.Instance.PreferencesOptions;
			if (!p.ShowOptionsWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Options", ref p.ShowOptionsWindow, ImGuiWindowFlags.None);

			if (ImGuiLayoutUtils.BeginTable("Options Step Type", 160))
			{
				ImGuiLayoutUtils.DrawRowSelectableTree<SMCommon.ChartType>(true, "Startup Pad Data", p,
					nameof(PreferencesOptions.StartupChartTypesBools),
					"Pad data will be loaded for the selected charts when the application starts." +
					"\nPad data is used to generate patterns and convert charts from one type to another.");

				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartType>(true, "Default Type", p, nameof(PreferencesOptions.DefaultStepsType),
					"When opening a song the default chart type will be used for selecting an initial chart.");
				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartDifficultyType>(true, "Default Difficulty", p, nameof(PreferencesOptions.DefaultDifficultyType),
					"When opening a song the default difficulty will be used for selecting an initial chart.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options File History", 160))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Open Last File On Launch", p, nameof(PreferencesOptions.OpenLastOpenedFileOnLaunch),
					"");
				ImGuiLayoutUtils.DrawRowSliderInt(true, "File History Size", p,
					nameof(PreferencesOptions.RecentFilesHistorySize), 0, 50,
					"");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options Preview", 160))
			{
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Fade In", p, nameof(PreferencesOptions.PreviewFadeInTime),
					"Time over which the preview should fade in when previewing the song.", 0.001f, "%.3f seconds", true, 0.0f);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Fade Out", p, nameof(PreferencesOptions.PreviewFadeOutTime),
					"Time over which the preview should fade out when previewing the song.", 0.001f, "%.3f seconds", true, 0.0f);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options Restore", 160))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all options to their default values."))
				{
					p.RestoreDefaults();
				}
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.End();
		}
	}
}
