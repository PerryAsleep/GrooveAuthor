using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing options UI.
	/// </summary>
	public class UIOptions
	{
		private static readonly int TitleColumnWidth = UiScaled(160);

		public void Draw()
		{
			var p = Preferences.Instance.PreferencesOptions;
			if (!p.ShowOptionsWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			if (ImGui.Begin("Options", ref p.ShowOptionsWindow, ImGuiWindowFlags.None))
			{

				if (ImGuiLayoutUtils.BeginTable("Options Step Type", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowSelectableTree<SMCommon.ChartType>(true, "Startup Pad Data", p,
						nameof(PreferencesOptions.StartupChartTypesBools), false,
						"Pad data will be loaded for the selected charts when the application starts." +
						"\nPad data is used to generate patterns and convert charts from one type to another.");

					ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartType>(true, "Default Type", p, nameof(PreferencesOptions.DefaultStepsType), false,
						"When opening a song the default chart type will be used for selecting an initial chart.");
					ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartDifficultyType>(true, "Default Difficulty", p, nameof(PreferencesOptions.DefaultDifficultyType), false,
						"When opening a song the default difficulty will be used for selecting an initial chart.");

					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("Options File History", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowCheckbox(true, "Open Last File On Launch", p, nameof(PreferencesOptions.OpenLastOpenedFileOnLaunch), false,
						"Whether or not to open the last opened file when launching the application.");
					ImGuiLayoutUtils.DrawRowSliderInt(true, "File History Size", p,
						nameof(PreferencesOptions.RecentFilesHistorySize), 0, 50, false,
						"Number of files to remember in the history used for opening recent files.");

					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("Options Preview", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Fade In", p, nameof(PreferencesOptions.PreviewFadeInTime), false,
						"Time over which the preview should fade in when previewing the song.", 0.001f, "%.3f seconds", 0.0);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Fade Out", p, nameof(PreferencesOptions.PreviewFadeOutTime), false,
						"Time over which the preview should fade out when previewing the song.", 0.001f, "%.3f seconds", 0.0);
					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("Options Restore", TitleColumnWidth))
				{
					if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
							"Restore all options to their default values."))
					{
						p.RestoreDefaults();
					}
					ImGuiLayoutUtils.EndTable();
				}
			}
			ImGui.End();
		}
	}
}
