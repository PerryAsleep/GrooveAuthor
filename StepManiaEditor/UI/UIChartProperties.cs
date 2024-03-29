﻿using System.Numerics;
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
	private static readonly Vector2 DefaultPosition = new(UiScaled(0), UiScaled(631));
	private static readonly Vector2 DefaultSize = new(UiScaled(622), UiScaled(241));

	private readonly ImGuiArrowWeightsWidget ArrowWeightsWidget;
	private readonly Editor Editor;

	public UIChartProperties(Editor editor)
	{
		Editor = editor;
		ArrowWeightsWidget = new ImGuiArrowWeightsWidget();
	}

	public void Draw(EditorChart editorChart)
	{
		if (!Preferences.Instance.ShowChartPropertiesWindow)
			return;

		ImGui.SetNextWindowPos(DefaultPosition, ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(DefaultSize, ImGuiCond.FirstUseEver);
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

				if (editorChart != null)
					ImGuiLayoutUtils.DrawRowDisplayTempo(true, editorChart, editorChart.GetMinTempo(), editorChart.GetMaxTempo());
				else
					ImGuiLayoutUtils.DrawRowDisplayTempo(true, null, 0.0, 0.0);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGui.CollapsingHeader("Uncommon Properties"))
			{
				if (ImGuiLayoutUtils.BeginTable("UncommonChartProperties", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowTextInput(true, "Style", editorChart, nameof(EditorChart.Style), true,
						"Originally meant to denote \"Pad\" versus \"Keyboard\" charts.");
					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("MusicProperties", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowFileBrowse("Music", editorChart, nameof(EditorChart.MusicPath),
						() => BrowseMusicFile(editorChart),
						() => ClearMusicFile(editorChart),
						true,
						"The audio file to use for this chart, overriding the song music." +
						"\nIn most cases all charts use the same music and it is defined at the song level.");

					ImGuiLayoutUtils.DrawRowDragDoubleWithEnabledCheckbox(true, "Music Offset", editorChart,
						nameof(EditorChart.MusicOffset), nameof(EditorChart.UsesChartMusicOffset), true,
						"The music offset from the start of the chart." +
						"\nIn most cases all charts use the same music offset and it is defined at the song level.",
						0.0001f, "%.6f seconds");

					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("ChartExpressionTable", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawExpressedChartConfigCombo(editorChart, "Expression",
						"(Editor Only) Expressed Chart Configuration."
						+ $"\nThis configuration is used by {Utils.GetAppName()} to parse the Chart and interpret its steps."
						+ "\nThis interpretation is used for autogenerating patterns and other Charts.");
					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("ChartDetailsTable", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawTitle("Distribution", "Distribution of steps across lanes.");
					if (editorChart != null)
					{
						var width = ImGui.GetContentRegionAvail().X;
						if (ImGui.BeginTable("DistributionInnerTable", 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
						{
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
							ImGui.TableNextRow();
							ImGui.TableSetColumnIndex(0);
							ArrowWeightsWidget.DrawChartStepCounts(Editor, editorChart);
							ImGui.EndTable();
						}
					}

					ImGuiLayoutUtils.EndTable();
				}
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
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
