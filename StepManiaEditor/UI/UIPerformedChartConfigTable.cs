using System;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing a table of all the EditorPerformedChartConfig objects.
/// </summary>
internal sealed class UIPerformedChartConfigTable
{
	private static readonly int AddConfigTitleWidth = UiScaled(220);

	/// <summary>
	/// The columns of the pattern config table.
	/// </summary>
	public enum Column
	{
		StepSpeedMin,
		StepDistanceMin,
		StepStretchMin,
		LateralSpeed,
		LateralRelativeNPS,
		LateralAbsoluteNPS,
		TransitionMin,
		TransitionMax,
		FacingInwardLimit,
		FacingOutwardLimit,
		Name,
		Clone,
		Delete,
	}

	private readonly Editor Editor;
	private readonly UIPerformedChartComparer Comparer;
	private bool HasSorted;

	private static readonly ColumnData[] TableColumnData;

	static UIPerformedChartConfigTable()
	{
		var count = Enum.GetNames(typeof(Column)).Length;
		TableColumnData = new ColumnData[count];
		TableColumnData[(int)Column.StepSpeedMin] =
			new ColumnData("S Spd", "Step Tightening Individual Step Speed", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.StepDistanceMin] = new ColumnData("S Dst", "Step Tightening Individual Step Distance",
			ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.StepStretchMin] = new ColumnData("S Str", "Step Tightening Individual Step Stretch Distance",
			ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultHide);
		TableColumnData[(int)Column.LateralSpeed] =
			new ColumnData("L Spd", "Lateral Movement Tightening Speed", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.LateralRelativeNPS] = new ColumnData("L Rel", "Lateral Movement Tightening Relative NPS",
			ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.LateralAbsoluteNPS] = new ColumnData("L Abs", "Lateral Movement Tightening Absolute NPS",
			ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.TransitionMin] =
			new ColumnData("Tr Min", "Min Steps Per Transition", ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.TransitionMax] = new ColumnData("Tr Max", "Max Steps Per Transition",
			ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultHide);
		TableColumnData[(int)Column.FacingInwardLimit] = new ColumnData("F In", "Max Percentage Of Inward Facing Steps",
			ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.FacingOutwardLimit] = new ColumnData("F Out", "Max Percentage Of Outward Facing Steps",
			ImGuiTableColumnFlags.WidthFixed);
		TableColumnData[(int)Column.Name] = new ColumnData("Custom Name", null, ImGuiTableColumnFlags.WidthStretch);
		TableColumnData[(int)Column.Clone] =
			new ColumnData("Clone", null, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort);
		TableColumnData[(int)Column.Delete] =
			new ColumnData("Delete", null, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort);
	}

	/// <summary>
	/// Constructor.
	/// </summary>
	public UIPerformedChartConfigTable(Editor editor)
	{
		Editor = editor;
		Comparer = Editor.GetPerformedChartComparer();
	}

	/// <summary>
	/// Draw UI.
	/// </summary>
	public void Draw()
	{
		var configManager = PerformedChartConfigManager.Instance;

		// Title table.
		if (ImGuiLayoutUtils.BeginTable("EditorPerformedChartConfigTitleTable", AddConfigTitleWidth))
		{
			ImGuiLayoutUtils.DrawRowTwoButtons("Performed Chart Configs",
				"Help", () => Documentation.OpenDocumentation(Documentation.Page.PerformedChartConfigs), true,
				"New", EditorPerformedChartConfig.CreateNewConfigAndShowEditUI, true,
				UIPerformedChartConfig.HelpText);

			ImGuiLayoutUtils.EndTable();
		}

		// Config table.
		if (ImGui.BeginTable("Performed Chart Configs", 13,
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

				// Step tightening speed is represented as a colored BPM value.
				// The first column is a selectable spanning all columns to allow easy clicking to open the edit window.
				ImGui.PushStyleColor(ImGuiCol.Text, config.GetSpeedStringColor());
				ImGui.TableNextColumn();
				if (ImGui.Selectable($"{config.GetStepSpeedMinString()}##{index}", false,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
				{
					Preferences.Instance.ActivePerformedChartConfigForWindow = configGuid;
					Preferences.Instance.ShowPerformedChartListWindow = true;
					ImGui.SetWindowFocus(UIPerformedChartConfig.WindowTitle);
				}

				ImGui.PopStyleColor();

				ImGui.TableNextColumn();
				ImGui.Text(config.GetStepDistanceMinString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetStepStretchMinString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetLateralSpeedString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetLateralRelativeNPSString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetLateralAbsoluteNPSString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetTransitionMinString());
				ImGui.TableNextColumn();
				ImGui.Text(config.GetTransitionMaxString());
				ImGui.TableNextColumn();
				ImGui.TextUnformatted(config.GetFacingInwardLimitString());
				ImGui.TableNextColumn();
				ImGui.TextUnformatted(config.GetFacingOutwardLimitString());
				ImGui.TableNextColumn();
				ImGui.Text(config.Name ?? "");
				if (!string.IsNullOrEmpty(config.Description))
					ToolTip(config.Description);

				// Clone button.
				ImGui.TableNextColumn();
				if (ImGui.SmallButton($"Clone##EditorPerformedChartConfig{index}"))
				{
					configToClone = configGuid;
				}

				// Delete button.
				ImGui.TableNextColumn();
				var disabled = config.IsDefault();
				if (disabled)
					PushDisabled();
				if (ImGui.SmallButton($"Delete##EditorPerformedChartConfig{index}"))
				{
					configToDelete = configGuid;
				}

				if (disabled)
					PopDisabled();

				index++;
			}

			if (configToClone != Guid.Empty)
				ActionQueue.Instance.Do(new ActionClonePerformedChartConfig(configToClone));
			if (configToDelete != Guid.Empty)
				ActionQueue.Instance.Do(new ActionDeletePerformedChartConfig(Editor, configToDelete));

			ImGui.EndTable();
		}
	}
}
