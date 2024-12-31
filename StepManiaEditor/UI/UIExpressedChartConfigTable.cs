using System;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing a table of all the EditorExpressedChartConfig objects.
/// </summary>
internal sealed class UIExpressedChartConfigTable
{
	private static readonly int AddConfigTitleWidth = UiScaled(220);
	private static readonly int NameWidth = UiScaled(220);

	private readonly Editor Editor;

	/// <summary>
	/// Constructor.
	/// </summary>
	public UIExpressedChartConfigTable(Editor editor)
	{
		Editor = editor;
	}

	/// <summary>
	/// Draw UI.
	/// </summary>
	public void Draw()
	{
		var configManager = ExpressedChartConfigManager.Instance;

		// Title table.
		if (ImGuiLayoutUtils.BeginTable("EditorExpressedChartConfigTitleTable", AddConfigTitleWidth))
		{
			ImGuiLayoutUtils.DrawRowTwoButtons("Expressed Chart Configs",
				"Help", () => Documentation.OpenDocumentation(Documentation.Page.ExpressedChartConfigs), true,
				"New", () => EditorExpressedChartConfig.CreateNewConfigAndShowEditUI(), true,
				UIExpressedChartConfig.HelpText);

			ImGuiLayoutUtils.EndTable();
		}

		// EditorConfig table setup.
		if (ImGui.BeginTable("Expressed Chart Configs", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
		{
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, NameWidth);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 0.0f);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 0.0f);

			var index = 0;
			var configToDelete = Guid.Empty;
			var configToClone = Guid.Empty;
			foreach (var config in configManager.GetSortedConfigs())
			{
				ImGui.TableNextRow();

				var configGuid = config.Guid;

				// Name.
				ImGui.TableNextColumn();
				if (config.ShouldUseColorForString())
					ImGui.PushStyleColor(ImGuiCol.Text, config.GetStringColor());
				if (ImGui.Selectable(config.ToString(), false,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowOverlap))
				{
					Preferences.Instance.ActiveExpressedChartConfigForWindow = configGuid;
					UIExpressedChartConfig.Instance.Open(true);
				}

				if (config.ShouldUseColorForString())
					ImGui.PopStyleColor();

				// Description.
				ImGui.TableNextColumn();
				ImGui.Text(config.Description ?? "");

				// Clone button.
				ImGui.TableNextColumn();
				if (ImGui.SmallButton($"Clone##ExpressedChartConfig{index}"))
				{
					configToClone = configGuid;
				}

				// Delete button.
				// ReSharper disable once RedundantAssignment
				ImGui.TableNextColumn();
				var disabled = config.IsDefault();
				if (disabled)
					PushDisabled();
				if (ImGui.SmallButton($"Delete##ExpressedChartConfig{index}"))
				{
					configToDelete = configGuid;
				}

				if (disabled)
					PopDisabled();

				index++;
			}

			if (configToClone != Guid.Empty)
				ActionQueue.Instance.Do(new ActionCloneExpressedChartConfig(configToClone));
			if (configToDelete != Guid.Empty)
				ActionQueue.Instance.Do(new ActionDeleteExpressedChartConfig(Editor, configToDelete));

			ImGui.EndTable();
		}
	}
}
