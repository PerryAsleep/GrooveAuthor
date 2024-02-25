using System;
using System.Text;
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
	public const PatternConfigStartFootChoice DefaultLeftFootStartChoice = PatternConfigStartFootChoice.AutomaticSameOrNewLane;
	public const int DefaultLeftFootStartLaneSpecified = 0;

	public const PatternConfigEndFootChoice DefaultLeftFootEndChoice =
		PatternConfigEndFootChoice.AutomaticSameOrNewLaneAsFollowing;

	public const int DefaultLeftFootEndLaneSpecified = 0;
	public const PatternConfigStartFootChoice DefaultRightFootStartChoice = PatternConfigStartFootChoice.AutomaticSameOrNewLane;
	public const int DefaultRightFootStartLaneSpecified = 0;

	public const PatternConfigEndFootChoice DefaultRightFootEndChoice =
		PatternConfigEndFootChoice.AutomaticSameOrNewLaneAsFollowing;

	public const int DefaultRightFootEndLaneSpecified = 0;
	public const int DefaultSameArrowStepWeight = 25;
	public const int DefaultNewArrowStepWeight = 75;
	public const int DefaultStepTypeCheckPeriod = 16;
	public const bool DefaultLimitSameArrowsInARowPerFoot = true;
	public const int DefaultMaxSameArrowsInARowPerFoot = 3;

	// Cached string representation.
	private string StringRepresentation = "";
	private string Abbreviation = "";
	private string NoteTypeString = "";
	private string StepTypeString = "";
	private string StepTypeCheckPeriodString = "";
	private string StartingFootString = "";
	private string StartingFootingString = "";
	private string EndingFootingString = "";

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
	/// <param name="isDefaultConfig">Whether or not this EditorConfig is a default configuration.</param>
	public EditorPatternConfig(Guid guid, bool isDefaultConfig) : base(guid, isDefaultConfig)
	{
	}

	#region String Representataion

	/// <summary>
	/// Returns a pretty string representation of the given SubdivisionType.
	/// </summary>
	/// <returns>Pretty string representation of the given SubdivisionType for displaying to the user.</returns>
	public static string GetPrettySubdivisionString(SubdivisionType type)
	{
		return $"1/{GetMeasureSubdivision(type)} Notes";
	}

	/// <summary>
	/// Returns whether or not the string representation of this EditorConfig should be
	/// rendered with color when possible. See also GetStringColor.
	/// </summary>
	/// <returns>
	/// True if the string representation of this EditorConfig should bre rendered with
	/// color when possible and false otherwise.
	/// </returns>
	public override bool ShouldUseColorForString()
	{
		// Patterns use colored text to indicate the subdivision type.
		return true;
	}

	/// <summary>
	/// The color of the string representation of this EditorConfig when it should be colored.
	/// See also ShouldUseColorForString.
	/// </summary>
	/// <returns>The color of the string representation of this EditorConfig.</returns>
	public override uint GetStringColor()
	{
		// Patterns should be colored based on the subdivision type.
		return ArrowGraphicManager.GetArrowColorForSubdivision(Config.BeatSubDivision);
	}

	/// <summary>
	/// Gets the string representation of this EditorPatternConfig.
	/// </summary>
	/// <returns>String representation of this EditorPatternConfig.</returns>
	public override string ToString()
	{
		return StringRepresentation;
	}

	public string GetAbbreviation()
	{
		return Abbreviation;
	}

	public string GetNoteTypeString()
	{
		return NoteTypeString;
	}

	public string GetStepTypeString()
	{
		return StepTypeString;
	}

	public string GetStepTypeCheckPeriodString()
	{
		return StepTypeCheckPeriodString;
	}

	public string GetStartingFootString()
	{
		return StartingFootString;
	}

	public string GetStartFootingString()
	{
		return StartingFootingString;
	}

	public string GetEndFootingString()
	{
		return EndingFootingString;
	}

	private void RebuildStringRepresentation()
	{
		RebuildNoteTypeString();
		RebuildStepTypeString();
		RebuildStepTypeCheckPeriodString();
		RebuildStartingFootString();
		RebuildStartFootingString();
		RebuildEndFootingString();

		// Update Abbreviation.
		var sb = new StringBuilder();

		// Note type.
		sb.Append(NoteTypeString);

		// Repetition Limit.
		if (Config.LimitSameArrowsInARowPerFoot)
			sb.Append($" {Config.MaxSameArrowsInARowPerFoot}");

		// Distribution and check period.
		sb.Append($" {StepTypeString}{StepTypeCheckPeriodString} ");

		// Starting foot.
		sb.Append(StartingFootString);

		// Starting and ending footing.
		sb.Append($" {StartingFootingString}->{EndingFootingString}");

		Abbreviation = sb.ToString();

		// Set StringRepresentation from Name and Abbreviation.
		if (!string.IsNullOrEmpty(Name))
			StringRepresentation = $"{Name}: {Abbreviation}";
		else
			StringRepresentation = Abbreviation;

		Notify(NotificationNameChanged, this);
	}

	private void RebuildNoteTypeString()
	{
		NoteTypeString = $"1/{GetMeasureSubdivision(PatternType)}";
	}

	private void RebuildStepTypeString()
	{
		StepTypeString = $"{Config.SameArrowStepWeight}/{Config.NewArrowStepWeight}";
	}

	private void RebuildStepTypeCheckPeriodString()
	{
		if (Config.StepTypeCheckPeriod > 1)
			StepTypeCheckPeriodString = $"x{Config.StepTypeCheckPeriod}";
		else
			StepTypeCheckPeriodString = "";
	}

	private void RebuildStartingFootString()
	{
		switch (Config.StartingFootChoice)
		{
			case PatternConfigStartingFootChoice.Specified:
				StartingFootString = Config.StartingFootSpecified == L ? "L" : "R";
				return;
			case PatternConfigStartingFootChoice.Automatic:
				StartingFootString = "A";
				return;
			case PatternConfigStartingFootChoice.Random:
				StartingFootString = "?";
				return;
		}

		StartingFootString = "";
	}

	private void RebuildStartFootingString()
	{
		var l = GetPatternConfigStartFootChoiceStr(Config.LeftFootStartChoice, Config.LeftFootStartLaneSpecified);
		var r = GetPatternConfigStartFootChoiceStr(Config.RightFootStartChoice, Config.RightFootStartLaneSpecified);
		StartingFootingString = $"[{l}|{r}]";
	}

	private void RebuildEndFootingString()
	{
		var l = GetPatternConfigEndFootChoiceStr(Config.LeftFootEndChoice, Config.LeftFootEndLaneSpecified);
		var r = GetPatternConfigEndFootChoiceStr(Config.RightFootEndChoice, Config.RightFootEndLaneSpecified);
		EndingFootingString = $"[{l}|{r}]";
	}

	/// <summary>
	/// Returns a short string representation of the given PatternConfigStartFootChoice.
	/// Helper for ToString.
	/// </summary>
	/// <param name="choice">PatternConfigStartFootChoice.</param>
	/// <param name="specified">Specified value for when the given choice is SpecifiedLane.</param>
	/// <returns>Short string representation of the given PatternConfigStartFootChoice</returns>
	private static string GetPatternConfigStartFootChoiceStr(PatternConfigStartFootChoice choice, int specified)
	{
		switch (choice)
		{
			case PatternConfigStartFootChoice.SpecifiedLane:
				return specified.ToString();
			case PatternConfigStartFootChoice.AutomaticNewLane:
				return "N";
			case PatternConfigStartFootChoice.AutomaticSameLane:
				return "S";
			case PatternConfigStartFootChoice.AutomaticSameOrNewLane:
				return "A";
			default:
				return "";
		}
	}

	/// <summary>
	/// Returns a short string representation of the given PatternConfigEndFootChoice.
	/// Helper for ToString.
	/// </summary>
	/// <param name="choice">PatternConfigEndFootChoice.</param>
	/// <param name="specified">Specified value for when the given choice is SpecifiedLane.</param>
	/// <returns>Short string representation of the given PatternConfigEndFootChoice</returns>
	private static string GetPatternConfigEndFootChoiceStr(PatternConfigEndFootChoice choice, int specified)
	{
		switch (choice)
		{
			case PatternConfigEndFootChoice.SpecifiedLane:
				return specified.ToString();
			case PatternConfigEndFootChoice.AutomaticIgnoreFollowingSteps:
				return "I";
			case PatternConfigEndFootChoice.AutomaticNewLaneToFollowing:
				return "N";
			case PatternConfigEndFootChoice.AutomaticSameLaneToFollowing:
				return "S";
			case PatternConfigEndFootChoice.AutomaticSameOrNewLaneAsFollowing:
				return "A";
			default:
				return "";
		}
	}

	#endregion String Representataion

	#region EditorConfig

	public override void Init()
	{
		RebuildStringRepresentation();
		base.Init();
	}

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
		return new EditorPatternConfig(snapshot ? Guid : Guid.NewGuid(), false)
		{
			StartingFootSpecified = StartingFootSpecified,
			PatternTypeInternal = PatternTypeInternal,
		};
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
		Config.SameArrowStepWeight = DefaultSameArrowStepWeight;
		Config.NewArrowStepWeight = DefaultNewArrowStepWeight;
		Config.StepTypeCheckPeriod = DefaultStepTypeCheckPeriod;
		Config.LimitSameArrowsInARowPerFoot = DefaultLimitSameArrowsInARowPerFoot;
		Config.MaxSameArrowsInARowPerFoot = DefaultMaxSameArrowsInARowPerFoot;
	}

	protected override bool EditorConfigEquals(EditorConfig<Config> other)
	{
		return Equals(other);
	}

	/// <summary>
	/// Returns the name newly created EditorPatternConfigs should use.
	/// </summary>
	/// <returns>The name newly created EditorPatternConfigs should use.</returns>
	public override string GetNewConfigName()
	{
		// EditorPatternConfigs by default don't use names and rely on the details exposed in their ToString representation.
		return null;
	}

	/// <summary>
	/// Notification handler for underlying StepManiaLibrary Config object changes.
	/// </summary>
	public override void OnNotify(string eventId, StepManiaLibrary.Config notifier, object payload)
	{
		// When anything about the config changes, rebuild the string representation.
		RebuildStringRepresentation();
		base.OnNotify(eventId, notifier, payload);
	}

	/// <summary>
	/// Called when this EditorConfig's Name changes.
	/// </summary>
	protected override void OnNameChanged()
	{
		RebuildStringRepresentation();
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
		       && Config.SameArrowStepWeight == DefaultSameArrowStepWeight
		       && Config.NewArrowStepWeight == DefaultNewArrowStepWeight
		       && Config.StepTypeCheckPeriod == DefaultStepTypeCheckPeriod
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
	private readonly EditorPatternConfig EditorConfig;
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
	private readonly int PreviousStepTypeCheckPeriod;
	private readonly bool PreviousLimitSameArrowsInARowPerFoot;
	private readonly int PreviousMaxSameArrowsInARowPerFoot;

	public ActionRestorePatternConfigDefaults(EditorPatternConfig editorConfig) : base(false, false)
	{
		EditorConfig = editorConfig;
		var config = EditorConfig.Config;

		PreviousPatternType = editorConfig.PatternType;
		PreviousStartingFootChoice = config.StartingFootChoice;
		PreviousStartingFootSpecified = editorConfig.StartingFootSpecified;
		PreviousLeftFootStartChoice = config.LeftFootStartChoice;
		PreviousLeftFootStartLaneSpecified = config.LeftFootStartLaneSpecified;
		PreviousLeftFootEndChoice = config.LeftFootEndChoice;
		PreviousLeftFootEndLaneSpecified = config.LeftFootEndLaneSpecified;
		PreviousRightFootStartChoice = config.RightFootStartChoice;
		PreviousRightFootStartLaneSpecified = config.RightFootStartLaneSpecified;
		PreviousRightFootEndChoice = config.RightFootEndChoice;
		PreviousRightFootEndLaneSpecified = config.RightFootEndLaneSpecified;
		PreviousSameArrowStepWeight = config.SameArrowStepWeight;
		PreviousNewArrowStepWeight = config.NewArrowStepWeight;
		PreviousStepTypeCheckPeriod = config.StepTypeCheckPeriod;
		PreviousLimitSameArrowsInARowPerFoot = config.LimitSameArrowsInARowPerFoot;
		PreviousMaxSameArrowsInARowPerFoot = config.MaxSameArrowsInARowPerFoot;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Restore \"{EditorConfig}\" Pattern Config to default values.";
	}

	protected override void DoImplementation()
	{
		var config = EditorConfig.Config;

		EditorConfig.PatternType = DefaultPatternType;
		config.StartingFootChoice = DefaultStartingFootChoice;
		EditorConfig.StartingFootSpecified = DefaultStartingFootSpecified;
		config.LeftFootStartChoice = DefaultLeftFootStartChoice;
		config.LeftFootStartLaneSpecified = DefaultLeftFootStartLaneSpecified;
		config.LeftFootEndChoice = DefaultLeftFootEndChoice;
		config.LeftFootEndLaneSpecified = DefaultLeftFootEndLaneSpecified;
		config.RightFootStartChoice = DefaultRightFootStartChoice;
		config.RightFootStartLaneSpecified = DefaultRightFootStartLaneSpecified;
		config.RightFootEndChoice = DefaultRightFootEndChoice;
		config.RightFootEndLaneSpecified = DefaultRightFootEndLaneSpecified;
		config.SameArrowStepWeight = DefaultSameArrowStepWeight;
		config.NewArrowStepWeight = DefaultNewArrowStepWeight;
		config.StepTypeCheckPeriod = DefaultStepTypeCheckPeriod;
		config.LimitSameArrowsInARowPerFoot = DefaultLimitSameArrowsInARowPerFoot;
		config.MaxSameArrowsInARowPerFoot = DefaultMaxSameArrowsInARowPerFoot;
	}

	protected override void UndoImplementation()
	{
		var config = EditorConfig.Config;

		EditorConfig.PatternType = PreviousPatternType;
		config.StartingFootChoice = PreviousStartingFootChoice;
		EditorConfig.StartingFootSpecified = PreviousStartingFootSpecified;
		config.LeftFootStartChoice = PreviousLeftFootStartChoice;
		config.LeftFootStartLaneSpecified = PreviousLeftFootStartLaneSpecified;
		config.LeftFootEndChoice = PreviousLeftFootEndChoice;
		config.LeftFootEndLaneSpecified = PreviousLeftFootEndLaneSpecified;
		config.RightFootStartChoice = PreviousRightFootStartChoice;
		config.RightFootStartLaneSpecified = PreviousRightFootStartLaneSpecified;
		config.RightFootEndChoice = PreviousRightFootEndChoice;
		config.RightFootEndLaneSpecified = PreviousRightFootEndLaneSpecified;
		config.SameArrowStepWeight = PreviousSameArrowStepWeight;
		config.NewArrowStepWeight = PreviousNewArrowStepWeight;
		config.StepTypeCheckPeriod = PreviousStepTypeCheckPeriod;
		config.LimitSameArrowsInARowPerFoot = PreviousLimitSameArrowsInARowPerFoot;
		config.MaxSameArrowsInARowPerFoot = PreviousMaxSameArrowsInARowPerFoot;
	}
}

#endregion ActionRestoreExpressedChartConfigDefaults
