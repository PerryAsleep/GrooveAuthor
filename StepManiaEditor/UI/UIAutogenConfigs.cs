using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing lists of ExpressedChartConfigs and PerformedChartConfigs.
/// </summary>
internal sealed class UIAutogenConfigs
{
	public const string WindowTitle = "Autogen Configs";

	private static readonly int AddConfigTitleWidth = UiScaled(120);
	private static readonly int NameWidth = UiScaled(120);
	private static readonly int DeleteWidth = UiScaled(43);

	private readonly Editor Editor;

	public UIAutogenConfigs(Editor editor)
	{
		Editor = editor;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowAutogenConfigsWindow)
			return;

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref p.ShowAutogenConfigsWindow, ImGuiWindowFlags.NoScrollbar))
		{
			// Expressed Chart section title.
			ImGui.Text("Expressed Chart Configs");
			ImGui.SameLine();
			HelpMarker(UIExpressedChartConfig.HelpText);

			// Expressed Chart table setup.
			var ret = ImGui.BeginTable("Expressed Chart Configs", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);
			if (ret)
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, NameWidth);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, DeleteWidth);
			}

			var sortedConfigNames = p.PreferencesExpressedChartConfig.GetSortedConfigNames();
			var index = 0;
			foreach (var configName in sortedConfigNames)
			{
				ImGui.TableNextRow();

				// Name.
				var config = p.PreferencesExpressedChartConfig.GetNamedConfig(configName);
				ImGui.TableSetColumnIndex(0);
				if (ImGui.Selectable(config.Name, false,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
				{
					Preferences.Instance.PreferencesExpressedChartConfig.ActiveExpressedChartConfigForWindow = config.Name;
					Preferences.Instance.PreferencesExpressedChartConfig.ShowExpressedChartListWindow = true;
					ImGui.SetWindowFocus(UIExpressedChartConfig.WindowTitle);
				}

				// Description.
				ImGui.TableSetColumnIndex(1);
				ImGui.Text(config.Description ?? "");

				// Delete button.
				ImGui.TableSetColumnIndex(2);
				var disabled = config.IsDefaultConfig();
				if (disabled)
					PushDisabled();
				if (ImGui.SmallButton($"Delete##ExpressedChartConfig{index}"))
				{
					ActionQueue.Instance.Do(new ActionDeleteExpressedChartConfig(Editor, config.Name));
				}

				if (disabled)
					PopDisabled();

				index++;
			}

			ImGui.EndTable();

			// Section to add Expressed Chart Configs.
			if (ImGuiLayoutUtils.BeginTable("AddExpressedConfigTable", AddConfigTitleWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("New", "New", "Add a new Expressed Chart Config."))
				{
					PreferencesExpressedChartConfig.CreateNewConfigAndShowEditUI();
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.Text("Performed Chart Configs");
			ImGui.SameLine();
			HelpMarker(UIPerformedChartConfig.HelpText);

			// Performed Chart table setup.
			ret = ImGui.BeginTable("Performed Chart Configs", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);
			if (ret)
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, NameWidth);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, DeleteWidth);
			}

			sortedConfigNames = p.PreferencesPerformedChartConfig.GetSortedConfigNames();
			index = 0;
			foreach (var configName in sortedConfigNames)
			{
				ImGui.TableNextRow();

				// Name.
				var config = p.PreferencesPerformedChartConfig.GetNamedConfig(configName);
				ImGui.TableSetColumnIndex(0);
				if (ImGui.Selectable(config.Name, false,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
				{
					PreferencesPerformedChartConfig.ShowEditUI(config.Name);
				}

				// Description.
				ImGui.TableSetColumnIndex(1);
				ImGui.Text(config.Description ?? "");

				// Delete button.
				ImGui.TableSetColumnIndex(2);
				var disabled = config.IsDefaultConfig();
				if (disabled)
					PushDisabled();
				if (ImGui.SmallButton($"Delete##PerformedChartConfig{index}"))
				{
					ActionQueue.Instance.Do(new ActionDeletePerformedChartConfig(config.Name));
				}

				if (disabled)
					PopDisabled();

				index++;
			}

			ImGui.EndTable();

			// Section to add Performed Chart Configs.
			if (ImGuiLayoutUtils.BeginTable("AddPerformedConfigTable", AddConfigTitleWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("New", "New", "Add a new Performed Chart Config."))
				{
					PreferencesPerformedChartConfig.CreateNewConfigAndShowEditUI();
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.End();
	}
}
