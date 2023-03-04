using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing options UI.
	/// </summary>
	public class UIOptions
	{
		private static readonly int TitleColumnWidth = UiScaled(160);
		private static readonly float ButtonSyncWidth = UiScaled(60);

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
					ImGuiLayoutUtils.DrawRowSelectableTree<SMCommon.ChartType>(true, "Startup Step Graphs", p,
						nameof(PreferencesOptions.StartupChartTypesBools), false, Editor.SupportedChartTypes,
						"Step graphs will be created for the selected charts when the application starts." +
						"\nStep graphs are used to generate patterns and convert charts from one type to another.");

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
				if (ImGuiLayoutUtils.BeginTable("Options Undo", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowInputInt(true, "Undo History Size", p,
						nameof(PreferencesOptions.UndoHistorySize), false,
						"Number of actions which can be stored in the undo history.", 1, 32768);

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
				if (ImGuiLayoutUtils.BeginTable("Options Sync", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "New Song Sync", p, nameof(PreferencesOptions.NewSongSyncOffset), false,
						SetNewSongSyncItg, "9ms (ITG)", ButtonSyncWidth,
						SetNewSongSyncDdr, "0ms (DDR)", ButtonSyncWidth,
						"The song sync to use when creating new songs."
						+ "\nThis is an editor-only value used to visually compensate for songs with built-in offsets."
						+ "\nIf you tend to work with content synced for ITG2 with a 9ms offset, set this to 9ms."
						+ "\nIf you tend to work with content with a null sync value, set this to 0ms."
						+ "\nThe song sync value is configurable per song. This value is only used for setting the"
						+ "\nstarting song sync value when creating a new song.",
						0.0001f, "%.6f seconds", 0.0);

					ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "Default Song Sync", p, nameof(PreferencesOptions.OpenSongSyncOffset), false,
						SetDefaultSongSyncItg, "9ms (ITG)", ButtonSyncWidth,
						SetDefaultSongSyncDdr, "0ms (DDR)", ButtonSyncWidth,
						"The song sync to use when opening songs that don't have a specified sync offset."
						+ "\nThis is an editor-only value used to visually compensate for songs with built-in offsets."
						+ "\nIf you tend to work with content synced for ITG2 with a 9ms offset, set this to 9ms."
						+ "\nIf you tend to work with content with a null sync value, set this to 0ms."
						+ "\nThe song sync value is configurable per song. This value is only used for setting the"
						+ "\nsong sync value when opening songs that don't have a specified song sync offset.",
						0.0001f, "%.6f seconds", 0.0);

					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("Options Audio", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Audio Offset", p, nameof(PreferencesOptions.AudioOffset), false,
						"Offset used when playing songs through the editor."
						+ "\nIf the audio and visuals appear out of sync when playing a song, adjusting this value can"
						+ "\ncompensate for this lag and bring the two in sync."
						+ "\nNote that setting this to a nonzero value will cause the audio to play at an earlier or later"
						+ "\nposition for a given time, which can result in audible differences when starting playback. For"
						+ "\nexample, if the chart's position is exactly at the start of a beat and then playback begins, a"
						+ "\npositive offset will result in the sound starting later than may be expected. As another example,"
						+ "\nif the chart's position is at the start of the preview, and then the chart is played, what is"
						+ "\nheard will not match the actual preview audio precisely when using a nonzero offset."
						+ "\nPlease do note however that playing the preview through the Song Properties window or with the P"
						+ "\nkey will always result in an accurate preview playback, even if there is an Audio Offset set."
						+ "\nIncreasing this value will cause the audio to play earlier."
						+ "\nDecreasing this value will cause the audio to play later.",
						0.0001f, "%.6f seconds");

					ImGuiLayoutUtils.DrawRowSliderFloat(true, "Volume", p, nameof(PreferencesOptions.Volume), 0.0f, 1.0f, false,
						"Volume of all audio.");

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

		private void SetNewSongSyncItg()
		{
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
				Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.NewSongSyncOffset), 0.009, false));
		}

		private void SetNewSongSyncDdr()
		{
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
				Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.NewSongSyncOffset), 0.0, false));
		}

		private void SetDefaultSongSyncItg()
		{
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
				Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.OpenSongSyncOffset), 0.009, false));
		}

		private void SetDefaultSongSyncDdr()
		{
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
				Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.OpenSongSyncOffset), 0.0, false));
		}
	}
}
