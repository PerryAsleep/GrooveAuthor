using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor.UI;

/// <summary>
/// Class for drawing information about an EditorPatternEvent in a chart.
/// </summary>
internal sealed class UIPatternEvent
{
	private static readonly int TitleColumnWidth = UiScaled(140);

	public const string WindowTitle = "Pattern Event Properties";

	private readonly Editor Editor;

	public UIPatternEvent(Editor editor)
	{
		Editor = editor;
	}

	public void Draw(EditorPatternEvent patternEvent)
	{
		if (patternEvent == null)
		{
			Preferences.Instance.ShowPatternEventWindow = false;
		}

		if (!Preferences.Instance.ShowPatternEventWindow)
			return;

		// TODO: should we show more than one of these at a time? Might need to push the id.

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowPatternEventWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanEdit();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("PatternEventTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowChartPosition("Start", Editor, patternEvent,
					"The start position of the pattern.");
				ImGuiLayoutUtils.DrawRowChartPositionFromLength("End", Editor, patternEvent, nameof(EditorPatternEvent.Length),
					"The end position of the pattern.");
				ImGuiLayoutUtils.DrawRowChartPositionLength(true, "Length", patternEvent, nameof(EditorPatternEvent.Length),
					nameof(EditorPatternEvent.EndPositionInclusive),
					"The length of the pattern.");
				ImGuiLayoutUtils.DrawRowRandomSeed(true, "Fixed Seed", patternEvent, nameof(EditorPatternEvent.RandomSeed), true,
					"Random seed to use when generating this Pattern.");

				ImGuiLayoutUtils.DrawPatternConfigCombo(true, "Pattern Config", patternEvent,
					nameof(EditorPatternEvent.PatternConfigGuid),
					"The Pattern Configuration.");
				ImGuiLayoutUtils.DrawPerformedChartConfigCombo(true, "Performed Chart Config", patternEvent,
					nameof(EditorPatternEvent.PerformedChartConfigGuid),
					"The Performed Chart Configuration.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternEventButtons", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTwoButtons("Generate",
					"Generate (Fixed Seed)",
					() =>
					{
						ActionQueue.Instance.Do(new ActionAutoGeneratePatterns(
							Editor,
							patternEvent.GetEditorChart(),
							new List<EditorPatternEvent> { patternEvent },
							false));
					},
					"Generate (New Seed)",
					() =>
					{
						ActionQueue.Instance.Do(new ActionAutoGeneratePatterns(
							Editor,
							patternEvent.GetEditorChart(),
							new List<EditorPatternEvent> { patternEvent },
							true));
					},
					"Generates the pattern. Will regenerate the pattern each time. Using the fixed seed will" +
					" produce the same pattern each time the pattern is generated, assuming no dependencies" +
					" have changed. Using a new seed will produce different results each time.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternNavigationButtons", TitleColumnWidth))
			{
				var multiplePatterns = patternEvent!.GetEditorChart().GetPatterns().GetCount() > 1;
				if (!multiplePatterns)
					PushDisabled();

				ImGuiLayoutUtils.DrawRowTwoButtons("Navigate",
					"Previous Pattern",
					() => { Editor.OnMoveToPreviousPattern(patternEvent); },
					"Next Pattern",
					() => { Editor.OnMoveToNextPattern(patternEvent); },
					"Navigate to other patterns." +
					"\nCtrl+P will also navigate to the next pattern." +
					"\nCtrl+Shift+P will also navigate to the previous pattern.");

				if (!multiplePatterns)
					PopDisabled();

				ImGuiLayoutUtils.EndTable();
			}

			var imGuiId = $"PatternEvent{patternEvent!.GetChartPosition()}";
			var patternConfig = patternEvent.GetPatternConfig();
			ImGui.Separator();
			if (ImGui.CollapsingHeader("Pattern Config"))
			{
				// TODO: need to pass id in because this is conflicting with other window.
				// This might be a big general issue we need to fix - look for any ui class that lets you draw the guts.

				UIPatternConfig.DrawConfig(imGuiId, Editor, patternConfig, patternEvent.GetEditorChart().ChartType, false);
			}

			var performedChartConfig = patternEvent.GetPerformedChartConfig();
			ImGui.Separator();
			if (ImGui.CollapsingHeader("Performed Chart Config"))
			{
				UIPerformedChartConfig.DrawConfig(imGuiId, Editor, performedChartConfig, false);
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}
}
