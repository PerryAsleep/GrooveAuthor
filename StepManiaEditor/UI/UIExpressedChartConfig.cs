using System.Numerics;
using ImGuiNET;
using StepManiaLibrary;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.PreferencesExpressedChartConfig;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI to edit an ExpressedChartConfig.
/// </summary>
internal sealed class UIExpressedChartConfig
{
	private static readonly int TitleColumnWidth = UiScaled(240);

	public const string HelpText = "Expressed Chart Configs are settings used by the Editor to interpret Charts."
	                               + " This is used for autogenerating patterns and new Charts as those actions require understanding how the body moves to perform a Chart."
	                               + " An Expressed Chart Config can be assigned to a Chart in the Chart Properties window."
	                               + " Charts will default to using the Dynamic Expressed Chart Config."
	                               + " Charts reference Expressed Chart Configs by name. Altering an Expressed Chart Config alters it for every Chart which references it."
	                               + " The default Expressed Chart Configs cannot be edited.";

	private readonly Editor Editor;

	public UIExpressedChartConfig(Editor editor)
	{
		Editor = editor;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesExpressedChartConfig;
		if (!p.ShowExpressedChartListWindow)
			return;

		if (!p.Configs.ContainsKey(p.ActiveExpressedChartConfigForWindow))
			return;

		var namedConfig = p.Configs[p.ActiveExpressedChartConfigForWindow];

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin("Expressed Chart Config", ref p.ShowExpressedChartListWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanEdit() || namedConfig.IsDefaultConfig();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("ExpressedChartConfigTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", namedConfig, nameof(NamedConfig.Name), true,
					Preferences.Instance.PreferencesExpressedChartConfig.IsNewConfigNameValid,
					"Configuration name." +
					"\nEditing the name will update any loaded Chart that references the old name to reference the new name." +
					"\nAny unloaded Charts referencing the old name will not be updated and they will default back to the" +
					"\nDefault config the next time they are loaded.");

				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", namedConfig, nameof(NamedConfig.Description), true,
					"Configuration description.");

				var config = namedConfig.Config;

				var usingDefaultMethod = config.BracketParsingDetermination == BracketParsingDetermination.UseDefaultMethod;
				if (!usingDefaultMethod)
					PushDisabled();

				ImGuiLayoutUtils.DrawRowEnum<BracketParsingMethod>(true, "Default Bracket Parsing Method", config,
					nameof(ExpressedChartConfig.DefaultBracketParsingMethod), false,
					"The default method to use for parsing brackets." +
					"\nThis is used if the Bracket Parsing Determination below is set to Use Default Method." +
					"\nAggressive:  Aggressively interpret steps as brackets. In most cases brackets will" +
					"\n             be preferred but in some cases jumps will still be preferred." +
					"\nBalanced:    Use a balanced method of interpreting brackets." +
					"\nNo Brackets: Never use brackets unless there is no other option.");

				if (!usingDefaultMethod)
					PopDisabled();

				ImGuiLayoutUtils.DrawRowEnum<BracketParsingDetermination>(true, "Bracket Parsing Determination", config,
					nameof(ExpressedChartConfig.BracketParsingDetermination), false,
					"How to make the determination of which Bracket Parsing Method to use." +
					"\nChoose Method Dynamically: The Bracket Parsing Method will be determined based on the other" +
					"\n                           values defined below in this configuration." +
					"\nUse Default Method:        The Default Bracket Parsing Method will be used.");

				usingDefaultMethod = config.BracketParsingDetermination == BracketParsingDetermination.UseDefaultMethod;
				if (usingDefaultMethod)
					PushDisabled();

				ImGuiLayoutUtils.DrawRowInputInt(true, "Min Level for Brackets", config,
					nameof(ExpressedChartConfig.MinLevelForBrackets), false,
					"When using Choose Method Dynamically for Bracket Parsing Determination, Charts with a" +
					"\ndifficulty rating under this level will use the No Brackets Bracket Parsing Method.", 0);

				ImGuiLayoutUtils.DrawRowCheckbox(true, "Aggressive Brackets When Unambiguous", config,
					nameof(ExpressedChartConfig.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets),
					false,
					"When using Choose Method Dynamically for Bracket Parsing Determination, Charts with more" +
					"\nsimultaneous steps than can be performed without bracketing will use the Aggressive" +
					"\nBracket Parsing Method.");

				ImGuiLayoutUtils.DrawRowDragDouble(true, "Balanced Bracket Rate For Aggressive", config,
					nameof(ExpressedChartConfig.BalancedBracketsPerMinuteForAggressiveBrackets), false,
					"When using Choose Method Dynamically for Bracket Parsing Determination, and the above" +
					"\nvalues have still not determined which Bracket Parsing Method to use, then the Chart" +
					"\nwill be parsed with the Balanced Bracket Parsing Method to determine the number of" +
					"\nbrackets per minute. If the brackets per minute is above this value, then the" +
					"\nAggressive Bracket Parsing Method will be used.",
					0.01f, "%.3f", config.BalancedBracketsPerMinuteForNoBrackets);

				ImGuiLayoutUtils.DrawRowDragDouble(true, "Balanced Bracket Rate For No Brackets", config,
					nameof(ExpressedChartConfig.BalancedBracketsPerMinuteForNoBrackets), false,
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
					ActionQueue.Instance.Do(new ActionDeleteExpressedChartConfig(Editor, namedConfig.Name));
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Expressed Chart Config Restore", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Help", HelpText);

				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults", 
					    "Restore config values to their defaults."))
				{
					namedConfig.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}
}
