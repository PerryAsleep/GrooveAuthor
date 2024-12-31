using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary.ExpressedChart;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI to edit an EditorExpressedChartConfig.
/// </summary>
internal sealed class UIExpressedChartConfig : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(240);
	private static readonly int DefaultWidth = UiScaled(460);

	public static readonly string HelpText =
		$"Expressed Chart Configs are settings used by {Utils.GetAppName()} to interpret Charts."
		+ " Autogenerating new charts from existing charts requires interpreting the existing chart."
		+ " Autogenerating patterns requires interpreting surrounding steps so the pattern can integrate nicely."
		+ " An Expressed Chart Config can be assigned to a Chart in the Chart Properties window."
		+ " Full details can be found in the documentation.";

	private Editor Editor;

	public static UIExpressedChartConfig Instance { get; } = new();

	private UIExpressedChartConfig() : base("Expressed Chart Config")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowExpressedChartListWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowExpressedChartListWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowExpressedChartListWindow)
			return;

		var editorConfig = ExpressedChartConfigManager.Instance.GetConfig(p.ActiveExpressedChartConfigForWindow);
		if (editorConfig == null)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowExpressedChartListWindow, DefaultWidth))
		{
			var disabled = !Editor.CanEdit() || editorConfig.IsDefault();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("ExpressedChartConfigTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", editorConfig, nameof(EditorExpressedChartConfig.Name), false,
					"Configuration name." +
					"\nEditing the name will update any loaded Chart that references the old name to reference the new name." +
					"\nAny unloaded Charts referencing the old name will not be updated and they will default back to the" +
					"\nDefault config the next time they are loaded.");

				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", editorConfig,
					nameof(EditorExpressedChartConfig.Description), false,
					"Configuration description.");

				var config = editorConfig.Config;

				var usingDefaultMethod = config.BracketParsingDetermination == BracketParsingDetermination.UseDefaultMethod;
				if (!usingDefaultMethod)
					PushDisabled();

				ImGuiLayoutUtils.DrawRowEnum<BracketParsingMethod>(true, "Default Bracket Parsing Method", config,
					nameof(Config.DefaultBracketParsingMethod), false,
					"The default method to use for parsing brackets." +
					"\nThis is used if the Bracket Parsing Determination below is set to Use Default Method." +
					"\nAggressive:  Aggressively interpret steps as brackets. In most cases brackets will" +
					"\n             be preferred but in some cases jumps will still be preferred." +
					"\nBalanced:    Use a balanced method of interpreting brackets." +
					"\nNo Brackets: Never use brackets unless there is no other option.");

				if (!usingDefaultMethod)
					PopDisabled();

				ImGuiLayoutUtils.DrawRowEnum<BracketParsingDetermination>(true, "Bracket Parsing Determination", config,
					nameof(Config.BracketParsingDetermination), false,
					"How to make the determination of which Bracket Parsing Method to use." +
					"\nChoose Method Dynamically: The Bracket Parsing Method will be determined based on the other" +
					"\n                           values defined below in this configuration." +
					"\nUse Default Method:        The Default Bracket Parsing Method will be used.");

				usingDefaultMethod = config.BracketParsingDetermination == BracketParsingDetermination.UseDefaultMethod;
				if (usingDefaultMethod)
					PushDisabled();

				ImGuiLayoutUtils.DrawRowInputInt(true, "Min Level for Brackets", config,
					nameof(Config.MinLevelForBrackets), false,
					"When using Choose Method Dynamically for Bracket Parsing Determination, Charts with a" +
					"\ndifficulty rating under this level will use the No Brackets Bracket Parsing Method.", 0);

				ImGuiLayoutUtils.DrawRowCheckbox(true, "Aggressive Brackets When Unambiguous", config,
					nameof(Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets),
					false,
					"When using Choose Method Dynamically for Bracket Parsing Determination, Charts with more" +
					"\nsimultaneous steps than can be performed without bracketing will use the Aggressive" +
					"\nBracket Parsing Method.");

				ImGuiLayoutUtils.DrawRowDragDouble(true, "Balanced Bracket Rate For Aggressive", config,
					nameof(Config.BalancedBracketsPerMinuteForAggressiveBrackets), false,
					"When using Choose Method Dynamically for Bracket Parsing Determination, and the above" +
					"\nvalues have still not determined which Bracket Parsing Method to use, then the Chart" +
					"\nwill be parsed with the Balanced Bracket Parsing Method to determine the number of" +
					"\nbrackets per minute. If the brackets per minute is above this value, then the" +
					"\nAggressive Bracket Parsing Method will be used.",
					0.01f, "%.3f", config.BalancedBracketsPerMinuteForNoBrackets);

				ImGuiLayoutUtils.DrawRowDragDouble(true, "Balanced Bracket Rate For No Brackets", config,
					nameof(Config.BalancedBracketsPerMinuteForNoBrackets), false,
					"When using Choose Method Dynamically for Bracket Parsing Determination, and the above" +
					"\nvalues have still not determined which Bracket Parsing Method to use, then the Chart" +
					"\nwill be parsed with the Balanced Bracket Parsing Method to determine the number of" +
					"\nbrackets per minute. If the brackets per minute is below this value, then the" +
					"\nNo Brackets Bracket Parsing Method will be used.",
					0.01f, "%.3f", 0.0, config.BalancedBracketsPerMinuteForAggressiveBrackets);

				if (usingDefaultMethod)
					PopDisabled();

				if (ImGuiLayoutUtils.DrawRowButton("Delete Expressed Chart Config", "Delete",
					    "Delete this Expressed Chart Config."
					    + "\nAny Charts using a deleted Expressed Chart Config will be updated to use the Default config."))
				{
					ActionQueue.Instance.Do(new ActionDeleteExpressedChartConfig(Editor, editorConfig.Guid));
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Expressed Chart Config Restore", TitleColumnWidth))
			{
				// Never disabled the documentation button.
				if (disabled)
					PopDisabled();
				if (ImGuiLayoutUtils.DrawRowButton("Help", "Open Documentation", HelpText))
				{
					Documentation.OpenDocumentation(Documentation.Page.ExpressedChartConfigs);
				}

				if (disabled)
					PushDisabled();

				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore config values to their defaults."))
				{
					editorConfig.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}
}
