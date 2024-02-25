using System;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary.PerformedChart;
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
		Name,
		Clone,
		Delete,
	}

	private readonly ConfigManager<EditorPatternConfig, PatternConfig> ConfigManager;
	private readonly Editor Editor;
	private readonly UIPatternComparer Comparer;
	private bool HasSorted;

	/// <summary>
	/// Constructor.
	/// </summary>
	public UIPatternConfigTable(
		Editor editor,
		ConfigManager<EditorPatternConfig, PatternConfig> configManager)
	{
		Editor = editor;
		ConfigManager = configManager;
		Comparer = Editor.GetPatternComparer();
	}

	/// <summary>
	/// Draw UI.
	/// </summary>
	public void Draw()
	{
		// Section title.
		ImGui.Text("Pattern Configs");
		ImGui.SameLine();
		HelpMarker(UIPatternConfig.HelpText);

		// EditorConfig table setup.
		if (ImGui.BeginTable("Pattern Configs", 10,
			    ImGuiTableFlags.RowBg
			    | ImGuiTableFlags.Borders
			    | ImGuiTableFlags.Resizable
			    | ImGuiTableFlags.Reorderable
			    | ImGuiTableFlags.Hideable
			    | ImGuiTableFlags.Sortable
			    | ImGuiTableFlags.SortMulti))
		{
			ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthFixed, 0.0f, (uint)Column.NoteType);
			ImGui.TableSetupColumn("Limit", ImGuiTableColumnFlags.WidthFixed, 0.0f, (uint)Column.RepetitionLimit);
			ImGui.TableSetupColumn("Same/New", ImGuiTableColumnFlags.WidthFixed, 0.0f, (uint)Column.StepType);
			ImGui.TableSetupColumn("Period", ImGuiTableColumnFlags.WidthFixed, 0.0f, (uint)Column.StepTypeCheckPeriod);
			ImGui.TableSetupColumn("Foot", ImGuiTableColumnFlags.WidthFixed, 0.0f, (uint)Column.StartingFoot);
			ImGui.TableSetupColumn("Start", ImGuiTableColumnFlags.WidthFixed, 0.0f, (uint)Column.StartingFooting);
			ImGui.TableSetupColumn("End", ImGuiTableColumnFlags.WidthFixed, 0.0f, (uint)Column.EndingFooting);
			ImGui.TableSetupColumn("Custom Name", ImGuiTableColumnFlags.WidthStretch, 0.0f, (uint)Column.Name);
			ImGui.TableSetupColumn("Clone", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 0.0f,
				(uint)Column.Clone);
			ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 0.0f,
				(uint)Column.Delete);
			ImGui.TableSetupScrollFreeze(0, 1);
			ImGui.TableHeadersRow();

			// Sort the list if the table is dirty due to user manipulation.
			var sortSpecsPtr = ImGui.TableGetSortSpecs();
			if (!HasSorted || sortSpecsPtr.SpecsDirty)
			{
				Comparer.SetSortSpecs(sortSpecsPtr);
				ConfigManager.SortConfigs();
				sortSpecsPtr.SpecsDirty = false;
				HasSorted = true;
			}

			// Draw each config row.
			var index = 0;
			var configToDelete = Guid.Empty;
			var configToClone = Guid.Empty;
			foreach (var config in ConfigManager.GetSortedConfigs())
			{
				ImGui.TableNextRow();

				var configGuid = config.Guid;

				// Note
				ImGui.TableNextColumn();
				ImGui.PushStyleColor(ImGuiCol.Text, config.GetStringColor());
				if (ImGui.Selectable($"{config.GetNoteTypeString()}##{index}", false,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
				{
					Preferences.Instance.ActivePatternConfigForWindow = configGuid;
					Preferences.Instance.ShowPatternListWindow = true;
					ImGui.SetWindowFocus(UIPatternConfig.WindowTitle);
				}

				ImGui.PopStyleColor();

				// Repeat
				ImGui.TableNextColumn();
				ImGui.Text(config.Config.MaxSameArrowsInARowPerFoot.ToString());

				// Step type distribution
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStepTypeString());

				// Distribution period
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStepTypeCheckPeriodString());

				// Starting foot
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStartingFootString());

				// Starting footing
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStartFootingString());

				// Ending footing
				ImGui.TableNextColumn();
				ImGui.Text(config.GetEndFootingString());

				// Name
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

		// Section to add a new EditorConfig.
		if (ImGuiLayoutUtils.BeginTable("AddEditorPatternTable", AddConfigTitleWidth))
		{
			if (ImGuiLayoutUtils.DrawRowButton("New", "New", "Add a new Pattern Config."))
			{
				EditorPatternConfig.CreateNewConfigAndShowEditUI();
			}

			ImGuiLayoutUtils.EndTable();
		}
	}
}
