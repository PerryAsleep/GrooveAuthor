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
	private static readonly Vector2 DefaultPosition = new(UiScaled(0), UiScaled(631));
	private static readonly Vector2 DefaultSize = new(UiScaled(622), UiScaled(241));
	private static readonly int UseStreamButtonWidth = UiScaled(80);
	private static readonly int NpsNameWidth = UiScaled(60);
	private static readonly int StepTotalNameWidth = UiScaled(40);

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
				ImGuiLayoutUtils.DrawRowTextInputWithOneButton(true, "Description", editorChart, nameof(EditorChart.Description),
					true,
					() => CopyChartStreamToDescription(editorChart), "Use Stream", UseStreamButtonWidth,
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
			}

			ImGui.Separator();
			if (ImGui.CollapsingHeader("Chart Stats"))
			{
				if (ImGuiLayoutUtils.BeginTable("ChartDetailsTable", TitleColumnWidth))
				{
					var noteType = GetSubdivisionTypeString(Preferences.Instance.PreferencesStream.NoteType);
					var steps = Utils.GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
					ImGuiLayoutUtils.DrawRowStream("Stream", editorChart?.GetStreamBreakdown() ?? "",
						$"Breakdown of {noteType} note stream."
						+ $"\nThis follows ITGmania / Simply Love rules where a measure is {SMCommon.RowsPerMeasure} rows and a measure"
						+ $"\nwith at least {steps} steps is considered stream regardless of if the individual steps are {noteType} notes.");

					ImGuiLayoutUtils.DrawTitle("Peak NPS", "Peak notes per second.");
					var width = ImGui.GetContentRegionAvail().X;
					if (ImGui.BeginTable("NpsTable", 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
					{
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);

						if (ImGui.BeginTable("NpsTableInner", 4, ImGuiTableFlags.Borders))
						{
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, NpsNameWidth);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, NpsNameWidth);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);

							ImGui.TableNextRow();
							ImGui.TableNextColumn();
							ImGui.Text("Step NPS");
							ToolTip("Multiple notes on the same row are considered distinct notes.");
							ImGui.TableNextColumn();
							ImGui.Text($"{Editor.GetActiveChartPeakNPS():F2}n/s");
							ImGui.TableNextColumn();
							ImGui.Text("Row NPS");
							ToolTip("Multiple notes on the same row are considered one note.");
							ImGui.TableNextColumn();
							ImGui.Text($"{Editor.GetActiveChartPeakRPS():F2}n/s");

							ImGui.EndTable();
						}

						ImGuiLayoutUtils.EndTable();
					}

					ImGuiLayoutUtils.DrawTitle("Step Counts", "Counts for various step types in the chart.");
					width = ImGui.GetContentRegionAvail().X;
					if (ImGui.BeginTable("StepCountsTable", 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
					{
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);

						if (ImGui.BeginTable("StepCountsTableInner", 6, ImGuiTableFlags.Borders))
						{
							var stepTotals = editorChart?.GetStepTotals();

							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, StepTotalNameWidth);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, StepTotalNameWidth);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, StepTotalNameWidth);
							ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);

							ImGui.TableNextRow();
							ImGui.TableNextColumn();
							ImGui.Text("Notes");
							ToolTip(
								"Total note count. Multiple notes on the same row are counted as distinct notes. Fakes are not counted as notes.");
							ImGui.TableNextColumn();
							ImGui.Text($"{stepTotals?.GetStepCount() ?? 0:N0}");
							ImGui.TableNextColumn();
							ImGui.Text("Holds");
							ToolTip("Total hold count. Fakes are not counted as holds.");
							ImGui.TableNextColumn();
							ImGui.Text($"{stepTotals?.GetHoldCount() ?? 0:N0}");
							ImGui.TableNextColumn();
							ImGui.Text("Lifts");
							ToolTip("Total lift count. Fakes are not counted as lifts.");
							ImGui.TableNextColumn();
							ImGui.Text($"{stepTotals?.GetLiftCount() ?? 0:N0}");
							ImGui.TableNextRow();
							ImGui.TableNextColumn();
							ImGui.Text("Steps");
							ToolTip(
								"Total step count. Multiple notes on the same row are counted as one step. Fakes are not counted as steps.");
							ImGui.TableNextColumn();
							ImGui.Text($"{stepTotals?.GetNumRowsWithSteps() ?? 0:N0}");
							ImGui.TableNextColumn();
							ImGui.Text("Rolls");
							ToolTip("Total roll count. Fakes are not counted as rolls.");
							ImGui.TableNextColumn();
							ImGui.Text($"{stepTotals?.GetRollCount() ?? 0:N0}");
							ImGui.TableNextColumn();
							ImGui.Text("Fakes");
							ToolTip("Total fake count.");
							ImGui.TableNextColumn();
							ImGui.Text($"{stepTotals?.GetFakeCount() ?? 0:N0}");
							ImGui.TableNextRow();
							ImGui.TableNextColumn();
							ImGui.Text("Mines");
							ToolTip("Total mine count.");
							ImGui.TableNextColumn();
							ImGui.Text($"{stepTotals?.GetMineCount() ?? 0:N0}");

							ImGui.EndTable();
						}

						ImGui.EndTable();
					}

					ImGuiLayoutUtils.DrawTitle("Distribution", "Distribution of steps across lanes.");
					width = ImGui.GetContentRegionAvail().X;
					if (editorChart != null)
					{
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

	private static void CopyChartStreamToDescription(EditorChart editorChart)
	{
		var streamBreakdown = editorChart.GetStreamBreakdown();
		var description = editorChart.Description;
		if (streamBreakdown == description)
			return;
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyReference<string>(editorChart, nameof(EditorChart.Description),
				editorChart.GetStreamBreakdown(), true));
	}
}
