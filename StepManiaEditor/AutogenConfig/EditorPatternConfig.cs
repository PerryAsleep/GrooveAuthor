using System;
using System.Text.Json.Serialization;
using Fumen.Converters;
using ImGuiNET;
using StepManiaLibrary.PerformedChart;
using static StepManiaEditor.AutogenConfig.EditorPatternConfig;
using static StepManiaLibrary.Constants;
using Config = StepManiaLibrary.PerformedChart.PatternConfig;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// EditorPatternConfig is a wrapper around a PatternConfig with additional
/// data and functionality for the editor.
/// TODO: Improve clarity on which fields should be used for edits.
/// Currently most fields are to be edited directly on the wrapped Config object, but
/// some need to be edited through this class's properties and this distinction is not
/// clear or enforced. Config needs to be public for json deserialization.
/// </summary>
internal sealed class EditorPatternConfig : EditorConfig<Config>, IEquatable<EditorPatternConfig>
{
	public enum SubdivisionType
	{
		QuarterNotes,
		EighthNotes,
		EighthNoteTriplets,
		SixteenthNotes,
		SixteenthNoteTriplets,
		ThirtySecondNotes,
		ThirtySecondNoteTriplets,
		SixtyFourthNotes,
		OneHundredNinetySecondNotes,
	}

	public const string NotificationPatternTypeChanged = "PatternTypeChanged";

	public static int GetBeatSubdivision(SubdivisionType subdivisionType)
	{
		return SMCommon.ValidDenominators[(int)subdivisionType];
	}

	public static int GetMeasureSubdivision(SubdivisionType subdivisionType)
	{
		return GetBeatSubdivision(subdivisionType) * SMCommon.NumBeatsPerMeasure;
	}

	// Default values.
	public const SubdivisionType DefaultPatternType = SubdivisionType.SixteenthNotes;
	public const PatternConfigStartingFootChoice DefaultStartingFootChoice = PatternConfigStartingFootChoice.Automatic;
	public const Editor.Foot DefaultStartingFootSpecified = Editor.Foot.Left;
	public const PatternConfigStartFootChoice DefaultLeftFootStartChoice = PatternConfigStartFootChoice.AutomaticNewLane;
	public const int DefaultLeftFootStartLaneSpecified = 0;

	public const PatternConfigEndFootChoice DefaultLeftFootEndChoice =
		PatternConfigEndFootChoice.AutomaticSameOrNewLaneAsFollowing;

	public const int DefaultLeftFootEndLaneSpecified = 0;
	public const PatternConfigStartFootChoice DefaultRightFootStartChoice = PatternConfigStartFootChoice.AutomaticNewLane;
	public const int DefaultRightFootStartLaneSpecified = 0;

	public const PatternConfigEndFootChoice DefaultRightFootEndChoice =
		PatternConfigEndFootChoice.AutomaticSameOrNewLaneAsFollowing;

	public const int DefaultRightFootEndLaneSpecified = 0;
	public const int DefaultSameArrowStepWeight = 25;
	public const int DefaultNewArrowStepWeight = 75;
	public const bool DefaultLimitSameArrowsInARowPerFoot = true;
	public const int DefaultMaxSameArrowsInARowPerFoot = 4;

	[JsonInclude]
	public SubdivisionType PatternType
	{
		get => PatternTypeInternal;
		set
		{
			PatternTypeInternal = value;
			Config.BeatSubDivision = GetBeatSubdivision(PatternTypeInternal);

			Notify(NotificationPatternTypeChanged, this);
		}
	}

	private SubdivisionType PatternTypeInternal;

	public Editor.Foot StartingFootSpecified
	{
		get => Config.StartingFootSpecified == L ? Editor.Foot.Left : Editor.Foot.Right;
		set => Config.StartingFootSpecified = value == Editor.Foot.Left ? L : R;
	}

	public int SameArrowStepWeight
	{
		get => Config.SameArrowStepWeight;
		set
		{
			Config.SameArrowStepWeight = value;
			Config.RefreshStepWeightsNormalized();
		}
	}

	public int NewArrowStepWeight
	{
		get => Config.NewArrowStepWeight;
		set
		{
			Config.NewArrowStepWeight = value;
			Config.RefreshStepWeightsNormalized();
		}
	}

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

	/// <summary>
	/// Returns a pretty string representation of the given SubdivisionType.
	/// </summary>
	/// <returns>Pretty string representation of the given SubdivisionType for displaying to the user.</returns>
	public static string GetPrettyString(SubdivisionType type)
	{
		return $"1/{GetMeasureSubdivision(type)} Notes";
	}

	/// <summary>
	/// Returns a pretty string representation of this EditorPatternConfig for displaying to the user.
	/// </summary>
	/// <returns>Pretty string representation of this EditorPatternConfig for displaying to the user.</returns>
	public string GetPrettyString()
	{
		return GetPrettyString(PatternType);
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
		PatternType = DefaultPatternType;
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
		SameArrowStepWeight = DefaultSameArrowStepWeight;
		NewArrowStepWeight = DefaultNewArrowStepWeight;
		Config.LimitSameArrowsInARowPerFoot = DefaultLimitSameArrowsInARowPerFoot;
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
		return PatternType == DefaultPatternType
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
		       && SameArrowStepWeight == DefaultSameArrowStepWeight
		       && NewArrowStepWeight == DefaultNewArrowStepWeight
		       && Config.LimitSameArrowsInARowPerFoot == DefaultLimitSameArrowsInARowPerFoot
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

	public static ActionAddPatternConfig GetCreateNewConfigAction()
	{
		return new ActionAddPatternConfig(Guid.NewGuid());
	}

	public static void CreateNewConfigAndShowEditUI()
	{
		var createAction = GetCreateNewConfigAction();
		ActionQueue.Instance.Do(createAction);
		ShowEditUI(createAction.GetGuid());
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
	private readonly SubdivisionType PreviousPatternType;
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
	private readonly bool PreviousLimitSameArrowsInARowPerFoot;
	private readonly int PreviousMaxSameArrowsInARowPerFoot;

	public ActionRestorePatternConfigDefaults(EditorPatternConfig config) : base(false, false)
	{
		Config = config;
		PreviousPatternType = Config.PatternType;
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
		PreviousSameArrowStepWeight = Config.SameArrowStepWeight;
		PreviousNewArrowStepWeight = Config.NewArrowStepWeight;
		PreviousLimitSameArrowsInARowPerFoot = Config.Config.LimitSameArrowsInARowPerFoot;
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
		Config.PatternType = DefaultPatternType;
		Config.Config.StartingFootChoice = DefaultStartingFootChoice;
		Config.StartingFootSpecified = DefaultStartingFootSpecified;
		Config.Config.LeftFootStartChoice = DefaultLeftFootStartChoice;
		Config.Config.LeftFootStartLaneSpecified = DefaultLeftFootStartLaneSpecified;
		Config.Config.LeftFootEndChoice = DefaultLeftFootEndChoice;
		Config.Config.LeftFootEndLaneSpecified = DefaultLeftFootEndLaneSpecified;
		Config.Config.RightFootStartChoice = DefaultRightFootStartChoice;
		Config.Config.RightFootStartLaneSpecified = DefaultRightFootStartLaneSpecified;
		Config.Config.RightFootEndChoice = DefaultRightFootEndChoice;
		Config.Config.RightFootEndLaneSpecified = DefaultRightFootEndLaneSpecified;
		Config.SameArrowStepWeight = DefaultSameArrowStepWeight;
		Config.NewArrowStepWeight = DefaultNewArrowStepWeight;
		Config.Config.LimitSameArrowsInARowPerFoot = DefaultLimitSameArrowsInARowPerFoot;
		Config.Config.MaxSameArrowsInARowPerFoot = DefaultMaxSameArrowsInARowPerFoot;
	}

	protected override void UndoImplementation()
	{
		Config.PatternType = PreviousPatternType;
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
		Config.SameArrowStepWeight = PreviousSameArrowStepWeight;
		Config.NewArrowStepWeight = PreviousNewArrowStepWeight;
		Config.Config.LimitSameArrowsInARowPerFoot = PreviousLimitSameArrowsInARowPerFoot;
		Config.Config.MaxSameArrowsInARowPerFoot = PreviousMaxSameArrowsInARowPerFoot;
	}
}

#endregion ActionRestoreExpressedChartConfigDefaults
