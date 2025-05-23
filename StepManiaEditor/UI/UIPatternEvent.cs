﻿using System.Collections.Generic;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor.UI;

/// <summary>
/// Class for drawing information about an EditorPatternEvent in a chart.
/// </summary>
internal sealed class UIPatternEvent : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(180);
	private static readonly int DefaultWidth = UiScaled(460);

	private Editor Editor;

	public static UIPatternEvent Instance { get; } = new();

	private UIPatternEvent() : base("Pattern Event Properties")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowPatternEventWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowPatternEventWindow = false;
	}

	public void Draw(EditorPatternEvent patternEvent)
	{
		if (patternEvent == null)
		{
			Preferences.Instance.ShowPatternEventWindow = false;
		}

		if (!Preferences.Instance.ShowPatternEventWindow)
			return;

		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowPatternEventWindow, DefaultWidth))
		{
			var disabled = !Editor.CanEdit();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("PatternEventPositionTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowChartPosition("Start", Editor, patternEvent,
					"The start position of the pattern.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Start Inclusive", patternEvent,
					nameof(EditorPatternEvent.StartPositionInclusive), false,
					"Whether or not the start position of the pattern is inclusive.");
				ImGuiLayoutUtils.DrawRowChartPositionFromLength("End", Editor, patternEvent, nameof(EditorPatternEvent.Length),
					"The end position of the pattern.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "End Inclusive", patternEvent,
					nameof(EditorPatternEvent.EndPositionInclusive), false,
					"Whether or not the end position of the pattern is inclusive.");
				ImGuiLayoutUtils.DrawRowChartPositionLength(true, "Length", patternEvent, nameof(EditorPatternEvent.Length),
					"The length of the pattern.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Ignore Preceding Distribution", patternEvent,
					nameof(EditorPatternEvent.IgnorePrecedingDistribution), false,
					"When patterns are generated, at every step the distribution of all preceding steps is considered so"
					+ "\nthat the Arrow Weights of the Performed Chart Config can be matched."
					+ "\nIf this option is checked, steps preceding the pattern will not be considered.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternEventConfigTable", TitleColumnWidth))
			{
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
				ImGuiLayoutUtils.DrawRowRandomSeed(true, "Seed", patternEvent, nameof(EditorPatternEvent.RandomSeed), true,
					patternEvent, Editor,
					"Random seed to use when generating this Pattern.");

				if (ImGuiLayoutUtils.DrawRowButton("Generate Pattern",
					    "Generate Pattern", "Generate the pattern using the current seed."))
				{
					ActionQueue.Instance.Do(new ActionAutoGeneratePatterns(
						Editor,
						patternEvent!.GetEditorChart(),
						new List<EditorPatternEvent> { patternEvent }));
				}

				if (ImGuiLayoutUtils.DrawRowButton("Clear Pattern", "Clear Pattern",
					    "Delete all the notes in this pattern's region."))
				{
					ActionQueue.Instance.Do(new ActionDeletePatternNotes(
						patternEvent!.GetEditorChart(),
						new List<EditorPatternEvent> { patternEvent }));
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternNavigationButtons", TitleColumnWidth))
			{
				var multiplePatterns = patternEvent!.GetEditorChart().GetPatterns().GetCount() > 1;
				if (!multiplePatterns)
					PushDisabled();

				var nextKeybind = UIControls.GetCommandString(Preferences.Instance.PreferencesKeyBinds.MoveToNextPattern);
				var prevKeybind = UIControls.GetCommandString(Preferences.Instance.PreferencesKeyBinds.MoveToPreviousPattern);
				ImGuiLayoutUtils.DrawRowTwoButtons("Navigate",
					"Previous Pattern",
					() => { Editor.OnMoveToPreviousPattern(patternEvent); },
					"Next Pattern",
					() => { Editor.OnMoveToNextPattern(patternEvent); },
					"Navigate to other patterns." +
					$"\n{nextKeybind} will also navigate to the next pattern." +
					$"\n{prevKeybind} will also navigate to the previous pattern.");

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
