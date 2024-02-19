using System;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing lists of EditorConfigs.
/// </summary>
internal sealed class UIAutogenConfigs
{
	public const string WindowTitle = "Autogen Configs";

	private static readonly int AddConfigTitleWidth = UiScaled(220);
	private static readonly int NameWidth = UiScaled(220);
	private static readonly int CloneWidth = UiScaled(39);
	private static readonly int DeleteWidth = UiScaled(45);
	private static readonly int DefaultWidth = UiScaled(780);

	/// <summary>
	/// Class for storing data per EditorConfig type and drawing a list of EditorConfigs
	/// for that type.
	/// </summary>
	/// <typeparam name="TEditorConfig">Type of EditorConfig.</typeparam>
	/// <typeparam name="TConfig">Type of IConfig wrapped by the EditorConfig.</typeparam>
	private class ConfigData<TEditorConfig, TConfig>
		where TEditorConfig : EditorConfig<TConfig>
		where TConfig : IConfig<TConfig>, new()
	{
		private readonly ConfigManager<TEditorConfig, TConfig> ConfigManager;
		private readonly string Title;
		private readonly string HumanReadableConfigType;
		private readonly string HelpText;
		private readonly bool ShowDescription;
		private readonly Action NewAction;
		private readonly Action<Guid> ClickAction;
		private readonly Action<Guid> CloneAction;
		private readonly Action<Guid> DeleteAction;

		/// <summary>
		/// Constructor.
		/// </summary>
		public ConfigData(
			ConfigManager<TEditorConfig, TConfig> configManager,
			string title,
			string humanReadableConfigType,
			string helpText,
			bool showDescription,
			Action newAction,
			Action<Guid> clickAction,
			Action<Guid> cloneAction,
			Action<Guid> deleteAction)
		{
			ConfigManager = configManager;
			Title = title;
			HumanReadableConfigType = humanReadableConfigType;
			HelpText = helpText;
			ShowDescription = showDescription;
			NewAction = newAction;
			ClickAction = clickAction;
			CloneAction = cloneAction;
			DeleteAction = deleteAction;
		}

		/// <summary>
		/// Draw UI.
		/// </summary>
		public void Draw()
		{
			// Section title.
			ImGui.Text(Title);
			ImGui.SameLine();
			HelpMarker(HelpText);

			// EditorConfig table setup.
			if (ShowDescription)
			{
				var ret = ImGui.BeginTable(Title, 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);
				if (ret)
				{
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, NameWidth);
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, CloneWidth);
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, DeleteWidth);
				}
			}
			else
			{
				var ret = ImGui.BeginTable(Title, 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders);
				if (ret)
				{
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, CloneWidth);
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, DeleteWidth);
				}
			}

			var typeName = typeof(TEditorConfig).FullName;

			var sortedConfigGuids = ConfigManager.GetSortedConfigGuids();
			var index = 0;
			foreach (var configGuid in sortedConfigGuids)
			{
				ImGui.TableNextRow();

				var col = 0;
				var config = ConfigManager.GetConfig(configGuid);

				// Name.
				if (config.ShouldUseColorForString())
					ImGui.PushStyleColor(ImGuiCol.Text, config.GetStringColor());
				ImGui.TableSetColumnIndex(col++);
				if (ImGui.Selectable(config.ToString(), false,
					    ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
				{
					ClickAction(configGuid);
				}

				if (config.ShouldUseColorForString())
					ImGui.PopStyleColor();

				// Description.
				if (ShowDescription)
				{
					ImGui.TableSetColumnIndex(col++);
					ImGui.Text(config.Description ?? "");
				}

				// Clone button.
				ImGui.TableSetColumnIndex(col++);
				if (ImGui.SmallButton($"Clone##{typeName}Config{index}"))
				{
					CloneAction(configGuid);
				}

				// Delete button.
				// ReSharper disable once RedundantAssignment
				ImGui.TableSetColumnIndex(col++);
				var disabled = config.IsDefault();
				if (disabled)
					PushDisabled();
				if (ImGui.SmallButton($"Delete##{typeName}Config{index}"))
				{
					DeleteAction(configGuid);
				}

				if (disabled)
					PopDisabled();

				index++;
			}

			ImGui.EndTable();

			// Section to add a new EditorConfig.
			if (ImGuiLayoutUtils.BeginTable($"Add{typeName}Table", AddConfigTitleWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("New", "New", $"Add a new {HumanReadableConfigType}."))
				{
					NewAction();
				}

				ImGuiLayoutUtils.EndTable();
			}
		}
	}

	/// <summary>
	/// ConfigData for drawing list of all EditorExpressedChartConfig data.
	/// </summary>
	private readonly ConfigData<EditorExpressedChartConfig, StepManiaLibrary.ExpressedChart.Config> ExpressedChartData;

	/// <summary>
	/// ConfigData for drawing list of all EditorPerformedChartConfig data.
	/// </summary>
	private readonly ConfigData<EditorPerformedChartConfig, StepManiaLibrary.PerformedChart.Config> PerformedChartData;

	/// <summary>
	/// ConfigData for drawing list of all EditorPatternConfig data.
	/// </summary>
	private readonly ConfigData<EditorPatternConfig, StepManiaLibrary.PerformedChart.PatternConfig> PatternData;

	/// <summary>
	/// Constructor
	/// </summary>
	public UIAutogenConfigs(Editor editor)
	{
		// Set up EditorExpressedChartConfig data for drawing.
		ExpressedChartData = new ConfigData<EditorExpressedChartConfig, StepManiaLibrary.ExpressedChart.Config>(
			ExpressedChartConfigManager.Instance,
			"Expressed Chart Configs",
			"Expressed Chart Config",
			UIExpressedChartConfig.HelpText,
			true,
			() => { EditorExpressedChartConfig.CreateNewConfigAndShowEditUI(); },
			(guid) =>
			{
				Preferences.Instance.ActiveExpressedChartConfigForWindow = guid;
				Preferences.Instance.ShowExpressedChartListWindow = true;
				ImGui.SetWindowFocus(UIExpressedChartConfig.WindowTitle);
			},
			(guid) => { ActionQueue.Instance.Do(new ActionCloneExpressedChartConfig(guid)); },
			(guid) => { ActionQueue.Instance.Do(new ActionDeleteExpressedChartConfig(editor, guid)); });

		// Set up EditorPerformedChartConfig data for drawing.
		PerformedChartData = new ConfigData<EditorPerformedChartConfig, StepManiaLibrary.PerformedChart.Config>(
			PerformedChartConfigManager.Instance,
			"Performed Chart Configs",
			"Performed Chart Config",
			UIPerformedChartConfig.HelpText,
			true,
			EditorPerformedChartConfig.CreateNewConfigAndShowEditUI,
			(guid) =>
			{
				Preferences.Instance.ActivePerformedChartConfigForWindow = guid;
				Preferences.Instance.ShowPerformedChartListWindow = true;
				ImGui.SetWindowFocus(UIPerformedChartConfig.WindowTitle);
			},
			(guid) => { ActionQueue.Instance.Do(new ActionClonePerformedChartConfig(guid)); },
			(guid) => { ActionQueue.Instance.Do(new ActionDeletePerformedChartConfig(editor, guid)); });

		// Set up EditorPatternConfig data for drawing.
		PatternData = new ConfigData<EditorPatternConfig, StepManiaLibrary.PerformedChart.PatternConfig>(
			PatternConfigManager.Instance,
			"Pattern Configs",
			"Pattern Config",
			UIPatternConfig.HelpText,
			false,
			EditorPatternConfig.CreateNewConfigAndShowEditUI,
			(guid) =>
			{
				Preferences.Instance.ActivePatternConfigForWindow = guid;
				Preferences.Instance.ShowPatternListWindow = true;
				ImGui.SetWindowFocus(UIPatternConfig.WindowTitle);
			},
			(guid) => { ActionQueue.Instance.Do(new ActionClonePatternConfig(guid)); },
			(guid) => { ActionQueue.Instance.Do(new ActionDeletePatternConfig(editor, guid)); });
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowAutogenConfigsWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowAutogenConfigsWindow, DefaultWidth))
		{
			ExpressedChartData.Draw();
			PerformedChartData.Draw();
			PatternData.Draw();
		}

		ImGui.End();
	}
}
