using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing Chart properties UI.
/// </summary>
internal sealed class UIChartProperties
{
	public const string WindowTitle = "Chart Properties";

	private static readonly int TitleColumnWidth = UiScaled(100);

	public void Draw(EditorChart editorChart)
	{
		if (!Preferences.Instance.ShowChartPropertiesWindow)
			return;

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowChartPropertiesWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanChartBeEdited(editorChart);
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("ChartInfoTable", TitleColumnWidth))
			{
				// The notes in the chart only make sense for one ChartType. Do not allow changing the ChartType.
				PushDisabled();
				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartType>(true, "Type", editorChart, nameof(EditorChart.ChartType), true,
					"Chart type.");
				PopDisabled();

				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartDifficultyType>(true, "Difficulty", editorChart,
					nameof(EditorChart.ChartDifficultyType), true,
					"Chart difficulty type.");
				ImGuiLayoutUtils.DrawRowInputInt(true, "Rating", editorChart, nameof(EditorChart.Rating), true,
					"Chart rating.", 1);
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", editorChart, nameof(EditorChart.Name), true,
					"Chart name.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", editorChart, nameof(EditorChart.Description), true,
					"Chart description.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Credit", editorChart, nameof(EditorChart.Credit), true,
					"Who this chart should be credited to.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Style", editorChart, nameof(EditorChart.Style), true,
					"(Uncommon) Originally meant to denote \"Pad\" versus \"Keyboard\" charts.");

				if (editorChart != null)
					ImGuiLayoutUtils.DrawRowDisplayTempo(true, editorChart, editorChart.GetMinTempo(), editorChart.GetMaxTempo());
				else
					ImGuiLayoutUtils.DrawRowDisplayTempo(true, null, 0.0, 0.0);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("ChartMusicTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowFileBrowse("Music", editorChart, nameof(EditorChart.MusicPath),
					() => BrowseMusicFile(editorChart),
					() => ClearMusicFile(editorChart),
					true,
					"(Uncommon) The audio file to use for this chart, overriding the song music." +
					"\nIn most cases all charts use the same music and it is defined at the song level.");

				ImGuiLayoutUtils.DrawRowDragDoubleWithEnabledCheckbox(true, "Music Offset", editorChart,
					nameof(EditorChart.MusicOffset), nameof(EditorChart.UsesChartMusicOffset), true,
					"(Uncommon) The music offset from the start of the chart." +
					"\nIn most cases all charts use the same music offset and it is defined at the song level.",
					0.0001f, "%.6f seconds");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("ChartExpressionTable", TitleColumnWidth))
			{
				DrawChartExpressedChartConfigWidget(editorChart, "Expression",
					"(Editor Only) Expressed Chart Configuration."
					+ "\nThis configuration is used by the editor to parse the Chart and interpret its steps."
					+ "\nThis interpretation is used for autogenerating patterns and other Charts.");
				ImGuiLayoutUtils.EndTable();
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}

	public static void DrawChartExpressedChartConfigWidget(EditorChart editorChart, string title, string help)
	{
		var configValues = Preferences.Instance.PreferencesExpressedChartConfig.GetSortedConfigNames();
		ImGuiLayoutUtils.DrawSelectableConfigFromList(true,
			title,
			editorChart,
			nameof(EditorChart.ExpressedChartConfig), configValues,
			() => PreferencesExpressedChartConfig.ShowEditUI(editorChart.ExpressedChartConfig),
			ViewAllExpressedChartConfigs,
			() => PreferencesExpressedChartConfig.CreateNewConfigAndShowEditUI(editorChart),
			true,
			help);
	}

	private static void ViewAllExpressedChartConfigs()
	{
		Preferences.Instance.ShowAutogenConfigsWindow = true;
		ImGui.SetWindowFocus(UIAutogenConfigs.WindowTitle);
	}

	private static void BrowseMusicFile(EditorChart editorChart)
	{
		var relativePath = Utils.BrowseFile(
			"Music",
			editorChart.GetEditorSong().GetFileDirectory(),
			editorChart.MusicPath,
			Utils.FileOpenFilterForAudio("Music", true));
		if (relativePath != null && relativePath != editorChart.MusicPath)
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(editorChart,
				nameof(editorChart.MusicPath), relativePath, true));
	}

	private static void ClearMusicFile(EditorChart editorChart)
	{
		if (!string.IsNullOrEmpty(editorChart.MusicPath))
			ActionQueue.Instance.Do(
				new ActionSetObjectFieldOrPropertyReference<string>(editorChart, nameof(EditorChart.MusicPath), "", true));
	}
}
