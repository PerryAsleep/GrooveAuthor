using System;
using Fumen;
using ImGuiNET;
using StepManiaLibrary.ExpressedChart;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// EditorExpressedChartConfig is a wrapper around an ExpressedChartConfig with additional
/// data and functionality for the editor.
/// </summary>
internal sealed class EditorExpressedChartConfig : EditorConfig<Config>, IEquatable<EditorExpressedChartConfig>
{
	// Default values.
	public const BracketParsingMethod DefaultDefaultBracketParsingMethod = BracketParsingMethod.Balanced;

	public const BracketParsingDetermination DefaultBracketParsingDetermination =
		BracketParsingDetermination.ChooseMethodDynamically;

	public const int DefaultMinLevelForBrackets = 7;
	public const bool DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets = true;
	public const double DefaultBalancedBracketsPerMinuteForAggressiveBrackets = 3.0;
	public const double DefaultBalancedBracketsPerMinuteForNoBrackets = 1.0;

	/// <summary>
	/// Constructor.
	/// </summary>
	public EditorExpressedChartConfig()
	{
	}

	/// <summary>
	/// Constructor taking a previously generated Guid.
	/// </summary>
	/// <param name="guid">Guid for this EditorExpressedChartConfig.</param>
	/// <param name="isDefaultConfig">Whether or not this EditorConfig is a default configuration.</param>
	public EditorExpressedChartConfig(Guid guid, bool isDefaultConfig) : base(guid, isDefaultConfig)
	{
	}

	#region EditorConfig

	/// <summary>
	/// Returns a new EditorExpressedChartConfig that is a clone of this EditorExpressedChartConfig.
	/// </summary>
	/// <param name="snapshot">
	/// If true then everything on this EditorExpressedChartConfig will be cloned.
	/// If false then the Guid and Name will be changed.
	/// </param>
	/// <returns>Cloned EditorExpressedChartConfig.</returns>
	protected override EditorExpressedChartConfig CloneImplementation(bool snapshot)
	{
		return new EditorExpressedChartConfig(snapshot ? Guid : Guid.NewGuid(), false);
	}

	public override void InitializeWithDefaultValues()
	{
		Config.DefaultBracketParsingMethod = DefaultDefaultBracketParsingMethod;
		Config.BracketParsingDetermination = DefaultBracketParsingDetermination;
		Config.MinLevelForBrackets = DefaultMinLevelForBrackets;
		Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets =
			DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		Config.BalancedBracketsPerMinuteForAggressiveBrackets = DefaultBalancedBracketsPerMinuteForAggressiveBrackets;
		Config.BalancedBracketsPerMinuteForNoBrackets = DefaultBalancedBracketsPerMinuteForNoBrackets;
	}

	protected override bool EditorConfigEquals(EditorConfig<Config> other)
	{
		return Equals(other);
	}

	#endregion EditorConfig

	/// <summary>
	/// Returns whether or not this EditorExpressedChartConfig is using all default values.
	/// </summary>
	/// <returns>
	/// True if this EditorExpressedChartConfig is using all default values and false otherwise.
	/// </returns>
	public bool IsUsingDefaults()
	{
		return Config.DefaultBracketParsingMethod == DefaultDefaultBracketParsingMethod
		       && Config.BracketParsingDetermination == DefaultBracketParsingDetermination
		       && Config.MinLevelForBrackets == DefaultMinLevelForBrackets
		       && Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets ==
		       DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets
		       && Config.BalancedBracketsPerMinuteForAggressiveBrackets.DoubleEquals(
			       DefaultBalancedBracketsPerMinuteForAggressiveBrackets)
		       && Config.BalancedBracketsPerMinuteForNoBrackets.DoubleEquals(DefaultBalancedBracketsPerMinuteForNoBrackets);
	}

	/// <summary>
	/// Restores this EditorExpressedChartConfig to its default values.
	/// </summary>
	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreExpressedChartConfigDefaults(this));
	}

	public static void CreateNewConfigAndShowEditUI(EditorChart editorChart = null)
	{
		var newConfigGuid = Guid.NewGuid();
		ActionQueue.Instance.Do(new ActionAddExpressedChartConfig(newConfigGuid, editorChart));
		ShowEditUI(newConfigGuid);
	}

	public static void ShowEditUI(Guid configGuid)
	{
		Preferences.Instance.ActiveExpressedChartConfigForWindow = configGuid;
		Preferences.Instance.ShowExpressedChartListWindow = true;
		ImGui.SetWindowFocus(UIExpressedChartConfig.WindowTitle);
	}

	#region IEquatable

	public bool Equals(EditorExpressedChartConfig other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		return Guid == other.Guid
		       && Name == other.Name
		       && Description == other.Description
		       && Config.Equals(other.Config);
	}

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj))
			return false;
		if (ReferenceEquals(this, obj))
			return true;
		if (obj.GetType() != GetType())
			return false;
		return Equals((EditorExpressedChartConfig)obj);
	}

	public override int GetHashCode()
	{
		// ReSharper disable NonReadonlyMemberInGetHashCode
		return HashCode.Combine(
			Guid,
			Name,
			Description,
			Config);
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	#endregion IEquatable
}

#region ActionRestoreExpressedChartConfigDefaults

/// <summary>
/// Action to restore an EditorExpressedChartConfig to its default values.
/// </summary>
internal sealed class ActionRestoreExpressedChartConfigDefaults : EditorAction
{
	private readonly EditorExpressedChartConfig Config;
	private readonly BracketParsingMethod PreviousDefaultBracketParsingMethod;
	private readonly BracketParsingDetermination PreviousBracketParsingDetermination;
	private readonly int PreviousMinLevelForBrackets;
	private readonly bool PreviousUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
	private readonly double PreviousBalancedBracketsPerMinuteForAggressiveBrackets;
	private readonly double PreviousBalancedBracketsPerMinuteForNoBrackets;

	public ActionRestoreExpressedChartConfigDefaults(EditorExpressedChartConfig config) : base(false, false)
	{
		Config = config;
		PreviousDefaultBracketParsingMethod = Config.Config.DefaultBracketParsingMethod;
		PreviousBracketParsingDetermination = Config.Config.BracketParsingDetermination;
		PreviousMinLevelForBrackets = Config.Config.MinLevelForBrackets;
		PreviousUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets =
			Config.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		PreviousBalancedBracketsPerMinuteForAggressiveBrackets = Config.Config.BalancedBracketsPerMinuteForAggressiveBrackets;
		PreviousBalancedBracketsPerMinuteForNoBrackets = Config.Config.BalancedBracketsPerMinuteForNoBrackets;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Restore {Config} Expressed Chart Config to default values.";
	}

	protected override void DoImplementation()
	{
		Config.Config.DefaultBracketParsingMethod =
			EditorExpressedChartConfig.DefaultDefaultBracketParsingMethod;
		Config.Config.BracketParsingDetermination =
			EditorExpressedChartConfig.DefaultBracketParsingDetermination;
		Config.Config.MinLevelForBrackets = EditorExpressedChartConfig.DefaultMinLevelForBrackets;
		Config.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets =
			EditorExpressedChartConfig
				.DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		Config.Config.BalancedBracketsPerMinuteForAggressiveBrackets = EditorExpressedChartConfig
			.DefaultBalancedBracketsPerMinuteForAggressiveBrackets;
		Config.Config.BalancedBracketsPerMinuteForNoBrackets =
			EditorExpressedChartConfig.DefaultBalancedBracketsPerMinuteForNoBrackets;
	}

	protected override void UndoImplementation()
	{
		Config.Config.DefaultBracketParsingMethod = PreviousDefaultBracketParsingMethod;
		Config.Config.BracketParsingDetermination = PreviousBracketParsingDetermination;
		Config.Config.MinLevelForBrackets = PreviousMinLevelForBrackets;
		Config.Config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets =
			PreviousUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets;
		Config.Config.BalancedBracketsPerMinuteForAggressiveBrackets = PreviousBalancedBracketsPerMinuteForAggressiveBrackets;
		Config.Config.BalancedBracketsPerMinuteForNoBrackets = PreviousBalancedBracketsPerMinuteForNoBrackets;
	}
}

#endregion ActionRestoreExpressedChartConfigDefaults
