using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing options UI.
/// </summary>
public class UIOptions
{
	public const string WindowTitle = "Options";

	private static readonly int TitleColumnWidth = UiScaled(160);
	private static readonly float ButtonSyncWidth = UiScaled(60);
	private static readonly float ButtonHelpWidth = UiScaled(32);
	private static readonly int DefaultWidth = UiScaled(606);

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesOptions;
		if (!p.ShowOptionsWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowOptionsWindow, DefaultWidth))
		{
			if (ImGuiLayoutUtils.BeginTable("Options Step Type", TitleColumnWidth))
			{
				DrawStartupStepGraphs(true);
				DrawDefaultType(true);
				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartDifficultyType>(true, "Default Difficulty", p,
					nameof(PreferencesOptions.DefaultDifficultyType), false,
					"When opening a song the default difficulty will be used for selecting an initial chart.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options File History", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Open Last File On Launch", p,
					nameof(PreferencesOptions.OpenLastOpenedFileOnLaunch), false,
					$"Whether or not to open the last opened file when launching {Editor.GetAppName()}.");
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
			if (ImGuiLayoutUtils.BeginTable("Options Sync", TitleColumnWidth))
			{
				DrawNewSongSync(true);
				DrawDefaultSongSync(true);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options Background", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Hide Song Background", p,
					nameof(PreferencesOptions.HideSongBackground), false,
					"Whether or not to hide the song's Background image in the editor.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options UI", TitleColumnWidth))
			{
				//var defaultDpiScale = GetDpiScaleSystemDefault();

				ImGuiLayoutUtils.DrawRowDragDoubleWithEnabledCheckbox(true, "Custom DPI Scale", p,
					nameof(PreferencesOptions.DpiScale),
					nameof(PreferencesOptions.UseCustomDpiScale),
					false,
					"Custom DPI scale to use for UI."
					//+ $"\nIf not specified, the default value for this computer ({defaultDpiScale}) will be used."
					+ "\nChanges to this value take effect on an application restart.",
					0.01f, "%.2f", 0.25, 8.0);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Suppress External Modification Notifications");
			if (ImGuiLayoutUtils.BeginTable("Options Notifications", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Song Modified", p,
					nameof(PreferencesOptions.SuppressExternalSongModificationNotification),
					false,
					"Whether to suppress notifications about the open song file being modified externally.");
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

	public static void DrawStartupStepGraphs(bool undoable)
	{
		ImGuiLayoutUtils.DrawRowStepGraphMultiSelection(undoable, "Startup Step Graphs", Preferences.Instance.PreferencesOptions,
			nameof(PreferencesOptions.StartupStepGraphs), false,
			$"Step Graphs will be created for the selected chart types when {Editor.GetAppName()} starts." +
			"\nStep Graphs are used to generate patterns and convert charts from one type to another.");
	}

	public static void DrawDefaultType(bool undoable)
	{
		ImGuiLayoutUtils.DrawRowEnum(undoable, "Default Type", Preferences.Instance.PreferencesOptions,
			nameof(PreferencesOptions.DefaultStepsType), Editor.SupportedChartTypes, false,
			"When opening a song the default chart type will be used for selecting an initial chart.");
	}

	public static void DrawNewSongSync(bool undoable)
	{
		ImGuiLayoutUtils.DrawRowDragDoubleWithThreeButtons(undoable, "New Song Sync", Preferences.Instance.PreferencesOptions,
			nameof(PreferencesOptions.NewSongSyncOffset), false,
			SetNewSongSyncItg, "9ms (ITG)", ButtonSyncWidth,
			SetNewSongSyncDdr, "0ms (DDR)", ButtonSyncWidth,
			() => Documentation.OpenDocumentation(Documentation.Page.SongSync), "Help", ButtonHelpWidth,
			"The song sync to use when creating new songs."
			+ "\nThis is an editor-only value used to visually compensate for songs with built-in offsets."
			+ "\nIf you tend to work with content synced for ITG2 with a 9ms offset, set this to 9ms."
			+ "\nIf you tend to work with content with a null sync value, set this to 0ms."
			+ "\nThe song sync value is configurable per song. This value is only used for setting the"
			+ "\nstarting song sync value when creating a new song.",
			0.0001f, "%.6f seconds", 0.0);
	}

	public static void DrawDefaultSongSync(bool undoable)
	{
		ImGuiLayoutUtils.DrawRowDragDoubleWithThreeButtons(undoable, "Default Song Sync", Preferences.Instance.PreferencesOptions,
			nameof(PreferencesOptions.OpenSongSyncOffset), false,
			SetDefaultSongSyncItg, "9ms (ITG)", ButtonSyncWidth,
			SetDefaultSongSyncDdr, "0ms (DDR)", ButtonSyncWidth,
			() => Documentation.OpenDocumentation(Documentation.Page.SongSync), "Help", ButtonHelpWidth,
			"The song sync to use when opening songs that don't have a specified sync offset."
			+ "\nThis is an editor-only value used to visually compensate for songs with built-in offsets."
			+ "\nIf you tend to work with content synced for ITG2 with a 9ms offset, set this to 9ms."
			+ "\nIf you tend to work with content with a null sync value, set this to 0ms."
			+ "\nThe song sync value is configurable per song. This value is only used for setting the"
			+ "\nsong sync value when opening songs that don't have a specified song sync offset.",
			0.0001f, "%.6f seconds", 0.0);
	}

	private static void SetNewSongSyncItg()
	{
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
			Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.NewSongSyncOffset), 0.009, false));
	}

	private static void SetNewSongSyncDdr()
	{
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
			Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.NewSongSyncOffset), 0.0, false));
	}

	private static void SetDefaultSongSyncItg()
	{
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
			Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.OpenSongSyncOffset), 0.009, false));
	}

	private static void SetDefaultSongSyncDdr()
	{
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(
			Preferences.Instance.PreferencesOptions, nameof(PreferencesOptions.OpenSongSyncOffset), 0.0, false));
	}
}
