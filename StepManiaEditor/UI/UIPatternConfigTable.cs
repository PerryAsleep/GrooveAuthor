using System;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing a table of all the EditorPatternConfig objects.
/// </summary>
internal sealed class UIPatternConfigTable
{
	private static readonly int AddConfigTitleWidth = UiScaled(220);

	/// <summary>
	/// The columns of the pattern config table.
	/// </summary>
	public enum Column
	{
		NoteType,
		RepetitionLimit,
		StepType,
		StepTypeCheckPeriod,
		StartingFoot,
		StartingFooting,
		EndingFooting,
		Abbreviation,
		Name,
		Clone,
		Delete,
	}

	private readonly Editor Editor;
	private readonly UIPatternComparer Comparer;
	private bool HasSorted;

	private static readonly ColumnData[] TableColumnData;

	/// <summary>
	/// Constructor.
	/// </summary>
	public UIPatternConfigTable(Editor editor)
	{
		Editor = editor;
		Comparer = Editor.GetPatternComparer();
	}

	static UIPatternConfigTable()
	{
		var count = Enum.GetNames(typeof(Column)).Length;
		TableColumnData = new ColumnData[count];
		TableColumnData[(int)Column.NoteType] = new ColumnData("Note", "Note Type", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.RepetitionLimit] =
			new ColumnData("Limit", "Step Repetition Limit", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.StepType] = new ColumnData("Same/New", "Step Type Weights",
			ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultHide);
		TableColumnData[(int)Column.StepTypeCheckPeriod] =
			new ColumnData("Period", "Step Type Check Period", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.StartingFoot] = new ColumnData("Foot", "Starting Foot", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.StartingFooting] =
			new ColumnData("Start", "Starting Footing For Each Foot", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.EndingFooting] =
			new ColumnData("End", "Ending Footing For Each Foot", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.Abbreviation] =
			new ColumnData("Abbreviation", "Abbreviation Of Configuration", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.Name] = new ColumnData("Custom Name", null, ImGuiTableColumnFlags.WidthStretch);
		TableColumnData[(int)Column.Clone] =
			new ColumnData("Clone", null, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort);
		TableColumnData[(int)Column.Delete] =
			new ColumnData("Delete", null, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort);
	}

	/// <summary>
	/// Draw UI.
	/// </summary>
	public void Draw()
	{
		var configManager = PatternConfigManager.Instance;

		// Title table.
		if (ImGuiLayoutUtils.BeginTable("EditorPatternConfigTitleTable", AddConfigTitleWidth))
		{
			ImGuiLayoutUtils.DrawRowTwoButtons("Pattern Configs",
				"Help", () => Documentation.OpenDocumentation(Documentation.Page.PatternConfigs), true,
				"New", EditorPatternConfig.CreateNewConfigAndShowEditUI, true,
				UIPatternConfig.HelpText);

			ImGuiLayoutUtils.EndTable();
		}

		// Config table.
		if (ImGui.BeginTable("Pattern Configs", TableColumnData.Length,
			    ImGuiTableFlags.RowBg
			    | ImGuiTableFlags.Borders
			    | ImGuiTableFlags.Resizable
			    | ImGuiTableFlags.Reorderable
			    | ImGuiTableFlags.Hideable
			    | ImGuiTableFlags.Sortable
			    | ImGuiTableFlags.SortMulti))
		{
			BeginTable(TableColumnData);

			// Sort the list if the table is dirty due to user manipulation.
			var sortSpecsPtr = ImGui.TableGetSortSpecs();
			if (!HasSorted || sortSpecsPtr.SpecsDirty)
			{
				Comparer.SetSortSpecs(sortSpecsPtr);
				configManager.SortConfigs();
				sortSpecsPtr.SpecsDirty = false;
				HasSorted = true;
			}

			// Draw each config row.
			var index = 0;
			var configToDelete = Guid.Empty;
			var configToClone = Guid.Empty;
			foreach (var config in configManager.GetSortedConfigs())
			{
				ImGui.TableNextRow();

				var configGuid = config.Guid;

				// Note
				ImGui.TableNextColumn();
				ImGui.PushStyleColor(ImGuiCol.Text, config.GetStringColor());
				if (ImGui.Selectable($"{config.GetNoteTypeString()}##{index}", false,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
				{
					Preferences.Instance.ActivePatternConfigForWindow = configGuid;
					UIPatternConfig.Instance.Open(true);
				}

				ImGui.PopStyleColor();

				ImGui.TableNextColumn();
				ImGui.Text(config.Config.MaxSameArrowsInARowPerFoot.ToString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStepTypeString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStepTypeCheckPeriodString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStartingFootString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStartFootingString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetEndFootingString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetAbbreviation() ?? "");
				ImGui.TableNextColumn();
				ImGui.Text(config.Name ?? "");

				// Clone button.
				ImGui.TableNextColumn();
				if (ImGui.SmallButton($"Clone##EditorPatternConfig{index}"))
				{
					configToClone = configGuid;
				}

				// Delete button.
				ImGui.TableNextColumn();
				var disabled = config.IsDefault();
				if (disabled)
					PushDisabled();
				if (ImGui.SmallButton($"Delete##EditorPatternConfig{index}"))
				{
					configToDelete = configGuid;
				}

				if (disabled)
					PopDisabled();

				index++;
			}

			if (configToClone != Guid.Empty)
				ActionQueue.Instance.Do(new ActionClonePatternConfig(configToClone));
			if (configToDelete != Guid.Empty)
				ActionQueue.Instance.Do(new ActionDeletePatternConfig(Editor, configToDelete));

			ImGui.EndTable();
		}
	}
}
