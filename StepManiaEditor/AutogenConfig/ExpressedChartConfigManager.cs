using System;
using StepManiaLibrary.ExpressedChart;

namespace StepManiaEditor.AutogenConfig;

internal sealed class ExpressedChartConfigManager : ConfigManager<EditorExpressedChartConfig, Config>
{
	// Default config names and guids for EditorExpressedChartConfigs which cannot be edited.
	public const string DefaultExpressedChartDynamicConfigName = "Dynamic";
	public static readonly Guid DefaultExpressedChartDynamicConfigGuid = new("a19d532e-b0ce-4759-ad1c-02ecbbdf2efd");
	public const string DefaultExpressedChartAggressiveBracketsConfigName = "Aggressive Brackets";
	public static readonly Guid DefaultExpressedChartAggressiveBracketsConfigGuid = new("da3f6e12-49d1-416b-8db6-0ab413f740b6");
	public const string DefaultExpressedChartNoBracketsConfigName = "No Brackets";
	public static readonly Guid DefaultExpressedChartNoBracketsConfigGuid = new("0c0ba200-8f90-4060-8912-e9ea65831ebc");

	/// <summary>
	/// Static instance.
	/// </summary>
	public static ExpressedChartConfigManager Instance { get; private set; } = new();

	/// <summary>
	/// Private constructor.
	/// </summary>
	private ExpressedChartConfigManager() : base("ecc-", "Expressed Chart")
	{
	}

	/// <summary>
	/// Creates a new EditorPerformedChartConfig object with the given Guid.
	/// </summary>
	/// <param name="guid">Guid for new EditorPerformedChartConfig object.</param>
	/// <param name="isDefaultConfig">Whether or not this EditorConfig is a default configuration.</param>
	/// <returns>New EditorPerformedChartConfig object.</returns>
	protected override EditorExpressedChartConfig NewEditorConfig(Guid guid, bool isDefaultConfig)
	{
		return new EditorExpressedChartConfig(guid, isDefaultConfig);
	}

	/// <summary>
	/// Adds all default EditorExpressedChartConfig objects.
	/// </summary>
	protected override void AddDefaultConfigs()
	{
		// Add the default dynamic config. This should never be modified so delete it if it exists and re-add it.
		DeleteConfig(DefaultExpressedChartDynamicConfigGuid);
		var defaultDynamicConfig =
			AddDefaultConfig(DefaultExpressedChartDynamicConfigGuid, DefaultExpressedChartDynamicConfigName);
		defaultDynamicConfig.Description = "Default settings with dynamic bracket parsing.";

		// Add the default aggressive bracket config. This should never be modified so delete it if it exists and re-add it.
		DeleteConfig(DefaultExpressedChartAggressiveBracketsConfigGuid);
		var defaultAggressiveConfig = AddDefaultConfig(DefaultExpressedChartAggressiveBracketsConfigGuid,
			DefaultExpressedChartAggressiveBracketsConfigName);
		defaultAggressiveConfig.Description = "Default settings with aggressive bracket parsing.";
		defaultAggressiveConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultAggressiveConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.Aggressive;
		defaultAggressiveConfig.Config.MinLevelForBrackets = 0;
		defaultAggressiveConfig.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets = false;
		defaultAggressiveConfig.Config.BalancedBracketsPerMinuteForAggressiveBrackets = 0.0;
		defaultAggressiveConfig.Config.BalancedBracketsPerMinuteForNoBrackets = 0.0;

		// Add the default no-brackets config. This should never be modified so delete it if it exists and re-add it.
		DeleteConfig(DefaultExpressedChartNoBracketsConfigGuid);
		var defaultNoBracketsConfig = AddDefaultConfig(DefaultExpressedChartNoBracketsConfigGuid,
			DefaultExpressedChartNoBracketsConfigName);
		defaultNoBracketsConfig.Description = "Default settings that avoid brackets.";
		defaultNoBracketsConfig.Config.BracketParsingDetermination = BracketParsingDetermination.UseDefaultMethod;
		defaultNoBracketsConfig.Config.DefaultBracketParsingMethod = BracketParsingMethod.NoBrackets;
		defaultNoBracketsConfig.Config.MinLevelForBrackets = 0;
		defaultNoBracketsConfig.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets = false;
		defaultNoBracketsConfig.Config.BalancedBracketsPerMinuteForAggressiveBrackets = 0.0;
		defaultNoBracketsConfig.Config.BalancedBracketsPerMinuteForNoBrackets = 0.0;
	}

	/// <summary>
	/// Called after all EditorConfig objects have been loaded, initialized, and validated.
	/// </summary>
	protected override void OnPostLoadComplete()
	{
		// Ensure the variables for displaying an EditorExpressedChartConfig don't point to an unknown config.
		if (Preferences.Instance.ActiveExpressedChartConfigForWindow != Guid.Empty)
		{
			if (ConfigData.GetConfig(Preferences.Instance.ActiveExpressedChartConfigForWindow) == null)
				Preferences.Instance.ActiveExpressedChartConfigForWindow = Guid.Empty;
		}

		if (Preferences.Instance.ActiveExpressedChartConfigForWindow == Guid.Empty)
			Preferences.Instance.ShowExpressedChartListWindow = false;
	}

	/// <summary>
	/// Called when an EditorConfig with the given Guid is deleted.
	/// </summary>
	/// <param name="guid">Guid of deleted EditorConfig.</param>
	protected override void OnConfigDeleted(Guid guid)
	{
		// If the actively displayed config is being deleted, remove the variable tracking 
		if (Preferences.Instance.ActiveExpressedChartConfigForWindow == guid)
		{
			Preferences.Instance.ActiveExpressedChartConfigForWindow = Guid.Empty;
			Preferences.Instance.ShowExpressedChartListWindow = false;
		}
	}
}
