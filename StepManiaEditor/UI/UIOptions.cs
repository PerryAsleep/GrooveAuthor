using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.PreferencesOptions;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing options UI.
/// </summary>
internal class UIOptions : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(160);
	private static readonly float ButtonSyncWidth = UiScaled(60);
	private static readonly float ButtonHelpWidth = UiScaled(32);
	private static readonly int DefaultWidth = UiScaled(606);

	private Editor Editor;

	public static UIOptions Instance { get; } = new();

	private UIOptions() : base("Options")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesOptions.ShowOptionsWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesOptions.ShowOptionsWindow = false;
	}

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
					$"Whether or not to open the last opened file when launching {Utils.GetAppName()}.");
				ImGuiLayoutUtils.DrawRowDragInt(true, "File History Size", p,
					nameof(PreferencesOptions.RecentFilesHistorySize), false,
					"Number of files to remember in the history used for opening recent files.", 1.0f, "%i", 0, 100);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options Undo", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowDragInt(true, "Undo History Size", p,
					nameof(PreferencesOptions.UndoHistorySize), false,
					"Number of actions which can be stored in the undo history.", 1.0f, "%i", 1, 32768);

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
			if (ImGuiLayoutUtils.BeginTable("Options Time Signature", TitleColumnWidth))
			{
				DrawStepColoring();
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options Background", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Hide Song Background", p,
					nameof(PreferencesOptions.HideSongBackground), false,
					"Whether or not to hide the song's Background image in the editor.");

				ImGuiLayoutUtils.DrawRowEnum<BackgroundImageSizeMode>(true, "Song Background Size", p,
					nameof(PreferencesOptions.BackgroundImageSize), false,
					"How to scale the song background image in the editor." +
					"\nChartArea: Fill the area reserved for displaying the charts when using docked UI." +
					"\nWindow:    Fill the entire window.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Options UI", TitleColumnWidth))
			{
				var defaultDpiScale = Editor.GetMonitorDpiScale();

				ImGuiLayoutUtils.DrawRowDragDoubleWithEnabledCheckbox(true, "Custom DPI Scale", p,
					nameof(PreferencesOptions.DpiScale),
					nameof(PreferencesOptions.UseCustomDpiScale),
					false,
					"Custom DPI scale to use for UI."
					+ $"\nIf not specified, the default value for this monitor ({defaultDpiScale}) will be used."
					+ "\nChanges to this value take effect on an application restart.",
					0.01f, "%.2f", 0.25, 8.0);

				ImGuiLayoutUtils.DrawRowDragInt(true, "Misc. Event Area Width", p, nameof(PreferencesOptions.MiscEventAreaWidth),
					false,
					"Width of the area on the sides of the focused chart for drawing miscellaneous events.", 0.1f, "%i pixels", 0,
					200);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Suppress Notifications");
			if (ImGuiLayoutUtils.BeginTable("Options Notifications", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Song Modified", p,
					nameof(PreferencesOptions.SuppressExternalSongModificationNotification),
					false,
					"Whether to suppress notifications about the open song file being modified externally.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Update Available", p,
					nameof(PreferencesOptions.SuppressUpdateNotification),
					false,
					$"Whether to suppress notifications that an update is available to {Utils.GetAppName()}.");
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
			$"Step Graphs will be created for the selected chart types when {Utils.GetAppName()} starts." +
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

	public static void DrawStepColoring(string title = "Step Coloring")
	{
		ImGuiLayoutUtils.DrawRowEnum<StepColorMethod>(true, title, Preferences.Instance.PreferencesOptions,
			nameof(PreferencesOptions.StepColorMethodValue), false,
			"How to color steps for chart types which color steps based on rhythm." +
			"\nStepmania: Use the same logic for coloring notes as Stepmania." +
			$"\n           Stepmania always effectively uses a {SMCommon.NumBeatsPerMeasure}/{SMCommon.NumBeatsPerMeasure} time signature." +
			"\n           Steps are colored by their note type." +
			$"\n           Quarter notes will occur every {SMCommon.MaxValidDenominator} rows." +
			"\nNote:      Use the time signature to color steps." +
			$"\n           Quarter notes will occur every {SMCommon.MaxValidDenominator} rows within a measure regardless of" +
			"\n           the time signature denominator." +
			"\n           Steps are colored by their note type." +
			"\n           For example a 7/8 measure of eighth notes will be red blue red blue red blue red." +
			"\n           WARNING: Step coloring will not match Stepmania for odd time signatures." +
			"\nBeat:      Use the time signature to color steps." +
			"\n           Steps are colored based on the time signature denominator." +
			"\n           For example a 7/8 measure of eighth notes will be all red." +
			"\n           WARNING: Step coloring will not match Stepmania for odd time signatures.");
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
