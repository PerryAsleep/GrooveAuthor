using System;
using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing Chart properties UI.
/// </summary>
internal sealed class UIChartProperties : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(90);
	private static readonly Vector2 DefaultPosition = new(UiScaled(0), UiScaled(631));
	public static readonly Vector2 DefaultSize = new(UiScaled(622), UiScaled(535));
	public static readonly Vector2 DefaultSizeSmall = new(UiScaled(622), UiScaled(442));
	private static readonly int UseStreamButtonWidth = UiScaled(80);
	private static readonly int NpsNameWidth = UiScaled(60);
	private static readonly int StepTotalNameWidth = UiScaled(40);

	private ImGuiArrowWeightsWidget ArrowWeightsWidget;
	private Editor Editor;

	public static UIChartProperties Instance { get; } = new();

	private UIChartProperties() : base("Chart Properties")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
		ArrowWeightsWidget = new ImGuiArrowWeightsWidget();
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowChartPropertiesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowChartPropertiesWindow = false;
	}

	public void Draw(EditorChart editorChart)
	{
		if (!Preferences.Instance.ShowChartPropertiesWindow)
			return;

		ImGui.SetNextWindowPos(DefaultPosition, ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(DefaultSize, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowChartPropertiesWindow,
			    ImGuiWindowFlags.AlwaysVerticalScrollbar))
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
			if (ImGuiLayoutUtils.BeginTable("UncommonChartProperties", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Style", editorChart, nameof(EditorChart.Style), true,
					"Originally meant to denote \"Pad\" versus \"Keyboard\" charts.");

				ImGuiLayoutUtils.DrawRowFileBrowse("Music", editorChart, nameof(EditorChart.MusicPath),
					() => BrowseMusicFile(Editor.GetPlatformInterface(), editorChart),
					() => ClearMusicFile(editorChart),
					true,
					"The audio file to use for this chart, overriding the song music." +
					"\nIn most cases all charts use the same music and it is defined at the song level.");

				ImGuiLayoutUtils.DrawRowDragDoubleWithEnabledCheckbox(true, "Music Offset", editorChart,
					nameof(EditorChart.MusicOffset), nameof(EditorChart.UsesChartMusicOffset), true,
					"The music offset from the start of the chart." +
					"\nIn most cases all charts use the same music offset and it is defined at the song level.",
					0.0001f, "%.6f seconds");

				// Draw either the multiplayer player count or the expression. Multiplayer charts don't support
				// all the autogen features so expression is meaningless for them.
				if (editorChart?.IsMultiPlayer() ?? false)
				{
					ImGuiLayoutUtils.DrawRowDragInt(true, "Players", editorChart, nameof(EditorChart.MaxPlayers), true,
						$"(Editor Only) The maximum number of players for this chart. Setting the maximum will prevent {GetAppName()}"
						+ " from cycling through more players than the chart should support. Please note that while multiplayer charts"
						+ " support an unbounded number of players most Stepmania themes only support two.",
						0.1f, "%i players", Math.Max(2, editorChart.GetStepTotals().GetNumPlayersWithNotes()), 128);
				}
				else
				{
					ImGuiLayoutUtils.DrawExpressedChartConfigCombo(editorChart, "Expression",
						"(Editor Only) Expressed Chart Configuration."
						+ $"\nThis configuration is used by {GetAppName()} to parse the Chart and interpret its steps."
						+ "\nThis interpretation is used for autogenerating patterns and other Charts.");
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("ChartDetailsTable", TitleColumnWidth))
			{
				var noteType = GetSubdivisionTypeString(Preferences.Instance.PreferencesStream.NoteType);
				var steps = GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
				ImGuiLayoutUtils.DrawRowStream("Stream", editorChart?.GetStreamBreakdown() ?? "",
					Editor,
					$"Breakdown of {noteType} note stream."
					+ $"\nThis follows ITGmania / Simply Love rules where a measure is {SMCommon.RowsPerMeasure} rows and a measure"
					+ $"\nwith at least {steps} steps is considered stream regardless of if the individual steps are {noteType} notes.");

				ImGuiLayoutUtils.DrawTitle("Peak NPS", "Peak notes per second.");
				var width = ImGuiLayoutUtils.GetTableWidth();
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
						ImGui.TextUnformatted("Step NPS");
						ToolTip("Multiple notes on the same row are considered distinct notes.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{Editor.GetActiveChartPeakNPS():F2}n/s");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted("Row NPS");
						ToolTip("Multiple notes on the same row are considered one note.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{Editor.GetActiveChartPeakRPS():F2}n/s");

						ImGui.EndTable();
					}

					ImGuiLayoutUtils.EndTable();
				}

				ImGuiLayoutUtils.DrawTitle("Step Counts", "Counts for various step types in the chart.");
				width = ImGuiLayoutUtils.GetTableWidth();
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
						ImGui.TextUnformatted("Notes");
						ToolTip(
							"Total note count. Multiple notes on the same row are counted as distinct notes. Fakes are not counted as notes.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{stepTotals?.GetStepCount() ?? 0:N0}");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted("Holds");
						ToolTip("Total hold count. Fakes are not counted as holds.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{stepTotals?.GetHoldCount() ?? 0:N0}");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted("Lifts");
						ToolTip("Total lift count. Fakes are not counted as lifts.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{stepTotals?.GetLiftCount() ?? 0:N0}");
						ImGui.TableNextRow();
						ImGui.TableNextColumn();
						ImGui.TextUnformatted("Steps");
						ToolTip(
							"Total step count. Multiple notes on the same row are counted as one step. Fakes are not counted as steps.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{stepTotals?.GetNumRowsWithSteps() ?? 0:N0}");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted("Rolls");
						ToolTip("Total roll count. Fakes are not counted as rolls.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{stepTotals?.GetRollCount() ?? 0:N0}");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted("Fakes");
						ToolTip("Total fake count.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{stepTotals?.GetFakeCount() ?? 0:N0}");
						ImGui.TableNextRow();
						ImGui.TableNextColumn();
						ImGui.TextUnformatted("Mines");
						ToolTip("Total mine count.");
						ImGui.TableNextColumn();
						ImGui.TextUnformatted($"{stepTotals?.GetMineCount() ?? 0:N0}");

						ImGui.EndTable();
					}

					ImGui.EndTable();
				}

				ImGuiLayoutUtils.DrawTitle("Distribution", "Distribution of steps across lanes.");
				width = ImGuiLayoutUtils.GetTableWidth();
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

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}

	private static void BrowseMusicFile(IEditorPlatform platformInterface, EditorChart editorChart)
	{
		var relativePath = platformInterface.BrowseFile(
			"Music",
			editorChart.GetEditorSong().GetFileDirectory(),
			editorChart.MusicPath,
			GetExtensionsForAudio(), true);
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
