using System;
using Config = StepManiaLibrary.PerformedChart.PatternConfig;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// ConfigManager for EditorPatternConfig objects.
/// This class should be accessed through its static Instance member.
/// </summary>
internal sealed class PatternConfigManager : ConfigManager<EditorPatternConfig, Config>
{
	// Default config names and guids for EditorPatternConfigs which cannot be edited.
	public const string DefaultPatternConfigSixteenthsName = "Default 16ths";
	public static readonly Guid DefaultPatternConfigSixteenthsGuid = new("03de57bd-4329-4de6-a722-a171c11fdd16");
	public const string DefaultPatternConfigEighthsName = "Default 8ths";
	public static readonly Guid DefaultPatternConfigEighthsGuid = new("b3779494-f8c2-4a76-be45-b5f6bfcb8fc0");

	/// <summary>
	/// Static instance.
	/// </summary>
	public static PatternConfigManager Instance { get; private set; } = new();

	/// <summary>
	/// Private constructor.
	/// </summary>
	private PatternConfigManager() : base("pc-", "Pattern")
	{
	}

	/// <summary>
	/// Creates a new EditorPatternConfig object with the given Guid.
	/// </summary>
	/// <param name="guid">Guid for new EditorPatternConfig object.</param>
	/// <param name="isDefaultConfig">Whether or not this EditorConfig is a default configuration.</param>
	/// <returns>New EditorPatternConfig object.</returns>
	protected override EditorPatternConfig NewEditorConfig(Guid guid, bool isDefaultConfig)
	{
		return new EditorPatternConfig(guid, isDefaultConfig);
	}

	/// <summary>
	/// Adds all default EditorPatternConfig objects.
	/// </summary>
	protected override void AddDefaultConfigs()
	{
		// Add default 16th note config.
		DeleteConfig(DefaultPatternConfigSixteenthsGuid);
		AddDefaultConfig(DefaultPatternConfigSixteenthsGuid, DefaultPatternConfigSixteenthsName);

		// Add default 8th note config.
		DeleteConfig(DefaultPatternConfigEighthsGuid);
		var eighthsConfig = AddDefaultConfig(DefaultPatternConfigEighthsGuid, DefaultPatternConfigEighthsName);
		eighthsConfig.PatternType = SubdivisionType.EighthNotes;
	}

	/// <summary>
	/// Called after all EditorConfig objects have been loaded, initialized, and validated.
	/// </summary>
	protected override void OnPostLoadComplete()
	{
		// Ensure the variables for displaying an EditorPatternConfig don't point to an unknown config.
		if (Preferences.Instance.ActivePatternConfigForWindow != Guid.Empty)
		{
			if (ConfigData.GetConfig(Preferences.Instance.ActivePatternConfigForWindow) == null)
				Preferences.Instance.ActivePatternConfigForWindow = Guid.Empty;
		}

		if (Preferences.Instance.ActivePatternConfigForWindow == Guid.Empty)
			Preferences.Instance.ShowPatternListWindow = false;
	}

	/// <summary>
	/// Called when an EditorConfig with the given Guid is deleted.
	/// </summary>
	/// <param name="guid">Guid of deleted EditorConfig.</param>
	protected override void OnConfigDeleted(Guid guid)
	{
		// If the actively displayed config is being deleted, remove the variable tracking 
		if (Preferences.Instance.ActivePatternConfigForWindow == guid)
		{
			Preferences.Instance.ActivePatternConfigForWindow = Guid.Empty;
			Preferences.Instance.ShowPatternListWindow = false;
		}
	}
}
