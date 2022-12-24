using System.Numerics;
using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing Chart properties UI.
	/// </summary>
	internal sealed class UIChartProperties
	{
		private readonly Editor Editor;
		private readonly DisplayTempo DummyDisplayTempo;
		
		private static EditorChart EditorChart;


		public UIChartProperties(Editor editor)
		{
			Editor = editor;
			DummyDisplayTempo = new DisplayTempo(DisplayTempoMode.Specified, 0.0, 0.0);
		}

		public void Draw(EditorChart editorChart)
		{
			EditorChart = editorChart;

			if (!Preferences.Instance.ShowChartPropertiesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Chart Properties", ref Preferences.Instance.ShowChartPropertiesWindow, ImGuiWindowFlags.NoScrollbar);

			if (EditorChart == null)
				Utils.PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("ChartInfoTable", 100))
			{
				// The notes in the chart only make sense for one ChartType. Do not allow changing the ChartType.
				Utils.PushDisabled();
				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartType>(true, "Type", EditorChart, nameof(EditorChart.ChartType), true,
					"Chart type.");
				Utils.PopDisabled();

				ImGuiLayoutUtils.DrawRowEnum<SMCommon.ChartDifficultyType>(true, "Difficulty", EditorChart, nameof(EditorChart.ChartDifficultyType), true,
					"Chart difficulty type.");
				ImGuiLayoutUtils.DrawRowInputInt(true, "Rating", EditorChart, nameof(EditorChart.Rating), true,
					"Chart rating.", 1);
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", EditorChart, nameof(EditorChart.Name), true,
					"Chart name.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", EditorChart, nameof(EditorChart.Description), true,
					"Chart description.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Credit", EditorChart, nameof(EditorChart.Credit), true,
					"Who this chart should be credited to.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Style", EditorChart, nameof(EditorChart.Style), true,
					"(Uncommon) Originally meant to denote \"Pad\" versus \"Keyboard\" charts.");

				if (EditorChart != null)
					ImGuiLayoutUtils.DrawRowDisplayTempo(true, EditorChart.DisplayTempo, EditorChart.MinTempo, EditorChart.MaxTempo);
				else
					ImGuiLayoutUtils.DrawRowDisplayTempo(true, DummyDisplayTempo, 0.0, 0.0);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("ChartMusicTable", 100))
			{
				ImGuiLayoutUtils.DrawRowFileBrowse("Music", EditorChart, nameof(EditorChart.MusicPath), BrowseMusicFile, ClearMusicFile, true,
					"(Uncommon) The audio file to use for this chart, overriding the song music." +
					"\nIn most cases all charts use the same music and it is defined at the song level.");
				
				ImGuiLayoutUtils.DrawRowDragDoubleWithEnabledCheckbox(true, "Music Offset", EditorChart, nameof(EditorChart.MusicOffset), nameof(EditorChart.UsesChartMusicOffset), true,
					"(Uncommon) The music offset from the start of the chart." +
					"\nIn most cases all charts use the same music offset and it is defined at the song level.",
					0.0001f, "%.6f seconds");

				ImGuiLayoutUtils.EndTable();
			}

			if (EditorChart == null)
				Utils.PopDisabled();
		}

		private static void BrowseMusicFile()
		{
			var relativePath = Utils.BrowseFile(
				"Music",
				EditorChart.EditorSong.FileDirectory,
				EditorChart.MusicPath,
				Utils.FileOpenFilterForAudio("Music", true));
			if (relativePath != null && relativePath != EditorChart.MusicPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorChart, nameof(EditorChart.MusicPath), relativePath, true));
		}

		private static void ClearMusicFile()
		{
			if (!string.IsNullOrEmpty(EditorChart.MusicPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorChart, nameof(EditorChart.MusicPath), "", true));
		}
	}
}
