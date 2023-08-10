using System;
using System.Text.Json.Serialization;
using ImGuiNET;
using StepManiaLibrary.PerformedChart;
using static StepManiaLibrary.Constants;
using Config = StepManiaLibrary.PerformedChart.PatternConfig;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// EditorPatternConfig is a wrapper around a PatternConfig with additional
/// data and functionality for the editor.
/// </summary>
internal sealed class EditorPatternConfig : EditorConfig<Config>, IEquatable<EditorPatternConfig>
{
	// Default values.
	public const int DefaultBeatSubDivision = 4;
	public const PatternConfigStartingFootChoice DefaultStartingFootChoice = PatternConfigStartingFootChoice.Automatic;
	public const Editor.Foot DefaultStartingFootSpecified = Editor.Foot.Left;
	public const PatternConfigStartFootChoice DefaultLeftFootStartChoice = PatternConfigStartFootChoice.AutomaticNewLane;
	public const int DefaultLeftFootStartLaneSpecified = 0;
	public const PatternConfigEndFootChoice DefaultLeftFootEndChoice = PatternConfigEndFootChoice.AutomaticNewLaneToFollowing;
	public const int DefaultLeftFootEndLaneSpecified = 0;
	public const PatternConfigStartFootChoice DefaultRightFootStartChoice = PatternConfigStartFootChoice.AutomaticNewLane;
	public const int DefaultRightFootStartLaneSpecified = 0;
	public const PatternConfigEndFootChoice DefaultRightFootEndChoice = PatternConfigEndFootChoice.AutomaticNewLaneToFollowing;
	public const int DefaultRightFootEndLaneSpecified = 0;
	public const int DefaultSameArrowStepWeight = 50;
	public const int DefaultNewArrowStepWeight = 50;
	public const int DefaultMaxSameArrowsInARowPerFoot = 4;

	[JsonInclude]
	[JsonPropertyName("EditorStartingFootSpecified")]
	public Editor.Foot StartingFootSpecified
	{
		get => StartingFootSpecifiedInternal;
		set
		{
			StartingFootSpecifiedInternal = value;
			Config.StartingFootSpecified = StartingFootSpecifiedInternal == Editor.Foot.Left ? L : R;
		}
	}

	private Editor.Foot StartingFootSpecifiedInternal;

	/// <summary>
	/// Constructor.
	/// </summary>
	public EditorPatternConfig()
	{
	}

	/// <summary>
	/// Constructor taking a previously generated Guid.
	/// </summary>
	/// <param name="guid">Guid for this EditorPatternConfig.</param>
	public EditorPatternConfig(Guid guid) : base(guid)
	{
	}

	#region EditorConfig

	/// <summary>
	/// Returns a new EditorPatternConfig that is a clone of this EditorPatternConfig.
	/// </summary>
	/// <param name="snapshot">
	/// If true then everything on this EditorPatternConfig will be cloned.
	/// If false then the Guid and Name will be changed.
	/// </param>
	/// <returns>Cloned EditorPatternConfig.</returns>
	protected override EditorPatternConfig CloneImplementation(bool snapshot)
	{
		return new EditorPatternConfig(snapshot ? Guid : Guid.NewGuid())
		{
			StartingFootSpecified = StartingFootSpecified,
		};
	}

	public override bool IsDefault()
	{
		return Guid.Equals(PatternConfigManager.DefaultPatternConfigSixteenthsGuid)
		       || Guid.Equals(PatternConfigManager.DefaultPatternConfigEighthsGuid);
	}

	public override void InitializeWithDefaultValues()
	{
		Config.BeatSubDivision = DefaultBeatSubDivision;
		Config.StartingFootChoice = DefaultStartingFootChoice;
		StartingFootSpecified = DefaultStartingFootSpecified;
		Config.LeftFootStartChoice = DefaultLeftFootStartChoice;
		Config.LeftFootStartLaneSpecified = DefaultLeftFootStartLaneSpecified;
		Config.LeftFootEndChoice = DefaultLeftFootEndChoice;
		Config.LeftFootEndLaneSpecified = DefaultLeftFootEndLaneSpecified;
		Config.RightFootStartChoice = DefaultRightFootStartChoice;
		Config.RightFootStartLaneSpecified = DefaultRightFootStartLaneSpecified;
		Config.RightFootEndChoice = DefaultRightFootEndChoice;
		Config.RightFootEndLaneSpecified = DefaultRightFootEndLaneSpecified;
		Config.SameArrowStepWeight = DefaultSameArrowStepWeight;
		Config.NewArrowStepWeight = DefaultNewArrowStepWeight;
		Config.MaxSameArrowsInARowPerFoot = DefaultMaxSameArrowsInARowPerFoot;
	}

	protected override bool EditorConfigEquals(EditorConfig<Config> other)
	{
		return Equals(other);
	}

	#endregion EditorConfig

	/// <summary>
	/// Returns whether or not this EditorPatternConfig is using all default values.
	/// </summary>
	/// <returns>
	/// True if this EditorPatternConfig is using all default values and false otherwise.
	/// </returns>
	public bool IsUsingDefaults()
	{
		return Config.BeatSubDivision == DefaultBeatSubDivision
		       && Config.StartingFootChoice == DefaultStartingFootChoice
		       && StartingFootSpecified == DefaultStartingFootSpecified
		       && Config.LeftFootStartChoice == DefaultLeftFootStartChoice
		       && Config.LeftFootStartLaneSpecified == DefaultLeftFootStartLaneSpecified
		       && Config.LeftFootEndChoice == DefaultLeftFootEndChoice
		       && Config.LeftFootEndLaneSpecified == DefaultLeftFootEndLaneSpecified
		       && Config.RightFootStartChoice == DefaultRightFootStartChoice
		       && Config.RightFootStartLaneSpecified == DefaultRightFootStartLaneSpecified
		       && Config.RightFootEndChoice == DefaultRightFootEndChoice
		       && Config.RightFootEndLaneSpecified == DefaultRightFootEndLaneSpecified
		       && Config.SameArrowStepWeight == DefaultSameArrowStepWeight
		       && Config.NewArrowStepWeight == DefaultNewArrowStepWeight
		       && Config.MaxSameArrowsInARowPerFoot == DefaultMaxSameArrowsInARowPerFoot;
	}

	/// <summary>
	/// Restores this EditorPatternConfig to its default values.
	/// </summary>
	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestorePatternConfigDefaults(this));
	}

	public static void CreateNewConfigAndShowEditUI()
	{
		var newConfigGuid = Guid.NewGuid();
		ActionQueue.Instance.Do(new ActionAddPatternConfig(newConfigGuid));
		ShowEditUI(newConfigGuid);
	}

	public static void ShowEditUI(Guid configGuid)
	{
		Preferences.Instance.ActivePatternConfigForWindow = configGuid;
		Preferences.Instance.ShowPatternListWindow = true;
		ImGui.SetWindowFocus(UIPatternConfig.WindowTitle);
	}

	#region IEquatable

	public bool Equals(EditorPatternConfig other)
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
		return Equals((EditorPatternConfig)obj);
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
/// Action to restore an EditorPatternConfig to its default values.
/// </summary>
internal sealed class ActionRestorePatternConfigDefaults : EditorAction
{
	private readonly EditorPatternConfig Config;
	private readonly int PreviousBeatSubDivision;
	private readonly PatternConfigStartingFootChoice PreviousStartingFootChoice;
	private readonly Editor.Foot PreviousStartingFootSpecified;
	private readonly PatternConfigStartFootChoice PreviousLeftFootStartChoice;
	private readonly int PreviousLeftFootStartLaneSpecified;
	private readonly PatternConfigEndFootChoice PreviousLeftFootEndChoice;
	private readonly int PreviousLeftFootEndLaneSpecified;
	private readonly PatternConfigStartFootChoice PreviousRightFootStartChoice;
	private readonly int PreviousRightFootStartLaneSpecified;
	private readonly PatternConfigEndFootChoice PreviousRightFootEndChoice;
	private readonly int PreviousRightFootEndLaneSpecified;
	private readonly int PreviousSameArrowStepWeight;
	private readonly int PreviousNewArrowStepWeight;
	private readonly int PreviousMaxSameArrowsInARowPerFoot;

	public ActionRestorePatternConfigDefaults(EditorPatternConfig config) : base(false, false)
	{
		Config = config;
		PreviousBeatSubDivision = Config.Config.BeatSubDivision;
		PreviousStartingFootChoice = Config.Config.StartingFootChoice;
		PreviousStartingFootSpecified = Config.StartingFootSpecified;
		PreviousLeftFootStartChoice = Config.Config.LeftFootStartChoice;
		PreviousLeftFootStartLaneSpecified = Config.Config.LeftFootStartLaneSpecified;
		PreviousLeftFootEndChoice = Config.Config.LeftFootEndChoice;
		PreviousLeftFootEndLaneSpecified = Config.Config.LeftFootEndLaneSpecified;
		PreviousRightFootStartChoice = Config.Config.RightFootStartChoice;
		PreviousRightFootStartLaneSpecified = Config.Config.RightFootStartLaneSpecified;
		PreviousRightFootEndChoice = Config.Config.RightFootEndChoice;
		PreviousRightFootEndLaneSpecified = Config.Config.RightFootEndLaneSpecified;
		PreviousSameArrowStepWeight = Config.Config.SameArrowStepWeight;
		PreviousNewArrowStepWeight = Config.Config.NewArrowStepWeight;
		PreviousMaxSameArrowsInARowPerFoot = Config.Config.MaxSameArrowsInARowPerFoot;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Restore {Config.Name} Pattern Config to default values.";
	}

	protected override void DoImplementation()
	{
		Config.Config.BeatSubDivision = EditorPatternConfig.DefaultBeatSubDivision;
		Config.Config.StartingFootChoice = EditorPatternConfig.DefaultStartingFootChoice;
		Config.StartingFootSpecified = EditorPatternConfig.DefaultStartingFootSpecified;
		Config.Config.LeftFootStartChoice = EditorPatternConfig.DefaultLeftFootStartChoice;
		Config.Config.LeftFootStartLaneSpecified = EditorPatternConfig.DefaultLeftFootStartLaneSpecified;
		Config.Config.LeftFootEndChoice = EditorPatternConfig.DefaultLeftFootEndChoice;
		Config.Config.LeftFootEndLaneSpecified = EditorPatternConfig.DefaultLeftFootEndLaneSpecified;
		Config.Config.RightFootStartChoice = EditorPatternConfig.DefaultRightFootStartChoice;
		Config.Config.RightFootStartLaneSpecified = EditorPatternConfig.DefaultRightFootStartLaneSpecified;
		Config.Config.RightFootEndChoice = EditorPatternConfig.DefaultRightFootEndChoice;
		Config.Config.RightFootEndLaneSpecified = EditorPatternConfig.DefaultRightFootEndLaneSpecified;
		Config.Config.SameArrowStepWeight = EditorPatternConfig.DefaultSameArrowStepWeight;
		Config.Config.NewArrowStepWeight = EditorPatternConfig.DefaultNewArrowStepWeight;
		Config.Config.MaxSameArrowsInARowPerFoot = EditorPatternConfig.DefaultMaxSameArrowsInARowPerFoot;
	}

	protected override void UndoImplementation()
	{
		Config.Config.BeatSubDivision = PreviousBeatSubDivision;
		Config.Config.StartingFootChoice = PreviousStartingFootChoice;
		Config.StartingFootSpecified = PreviousStartingFootSpecified;
		Config.Config.LeftFootStartChoice = PreviousLeftFootStartChoice;
		Config.Config.LeftFootStartLaneSpecified = PreviousLeftFootStartLaneSpecified;
		Config.Config.LeftFootEndChoice = PreviousLeftFootEndChoice;
		Config.Config.LeftFootEndLaneSpecified = PreviousLeftFootEndLaneSpecified;
		Config.Config.RightFootStartChoice = PreviousRightFootStartChoice;
		Config.Config.RightFootStartLaneSpecified = PreviousRightFootStartLaneSpecified;
		Config.Config.RightFootEndChoice = PreviousRightFootEndChoice;
		Config.Config.RightFootEndLaneSpecified = PreviousRightFootEndLaneSpecified;
		Config.Config.SameArrowStepWeight = PreviousSameArrowStepWeight;
		Config.Config.NewArrowStepWeight = PreviousNewArrowStepWeight;
		Config.Config.MaxSameArrowsInARowPerFoot = PreviousMaxSameArrowsInARowPerFoot;
	}
}

#endregion ActionRestoreExpressedChartConfigDefaults
