using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fumen;
using ImGuiNET;
using StepManiaLibrary;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// EditorPerformedChartConfig is a wrapper around a PerformedChart Config with additional
/// data and functionality for the editor.
/// TODO: Improve clarity on which fields should be used for edits.
/// Currently most fields are to be edited directly on the wrapped Config object, but
/// some need to be edited through this class's properties and this distinction is not
/// clear or enforced. Config needs to be public for json deserialization.
/// </summary>
internal sealed class EditorPerformedChartConfig : EditorConfig<Config>, IEquatable<EditorPerformedChartConfig>
{
	// Default values.
	public const double DefaultFacingMaxInwardPercentage = 1.0;
	public const double DefaultFacingInwardPercentageCutoff = 0.34;
	public const double DefaultFacingMaxOutwardPercentage = 1.0;
	public const double DefaultFacingOutwardPercentageCutoff = 0.34;
	public const bool DefaultStepTighteningTravelSpeedEnabled = true;
	public const int DefaultStepTighteningTravelSpeedNoteDenominatorIndex = 3;
	public const int DefaultStepTighteningTravelSpeedMinBPM = 125;
	public const int DefaultStepTighteningTravelSpeedMaxBPM = 170;
	public const double DefaultStepTighteningTravelSpeedTighteningMinDistance = 0.0;
	public const bool DefaultStepTighteningTravelDistanceEnabled = true;
	public const double DefaultStepTighteningTravelDistanceMin = 1.4;
	public const double DefaultStepTighteningTravelDistanceMax = 2.333333;
	public const bool DefaultStepTighteningStretchEnabled = true;
	public const double DefaultStepTighteningStretchDistanceMin = 2.333333;
	public const double DefaultStepTighteningStretchDistanceMax = 3.333333;
	public const double DefaultStepTighteningLateralMinPanelDistance = 0.166667;
	public const double DefaultStepTighteningLongitudinalMinPanelDistance = -0.125;
	public const bool DefaultLateralTighteningEnabled = true;
	public const double DefaultLateralTighteningRelativeNPS = 1.65;
	public const double DefaultLateralTighteningAbsoluteNPS = 12.0;
	public const double DefaultLateralTighteningSpeed = 3.0;
	public const bool DefaultTransitionsEnabled = false;
	public const int DefaultTransitionsStepsPerTransitionMin = 0;
	public const int DefaultTransitionsStepsPerTransitionMax = 1024;
	public const int DefaultTransitionsMinimumPadWidth = 5;
	public const double DefaultTransitionsCutoffPercentage = 0.5;

	public static readonly Dictionary<string, List<int>> DefaultArrowWeights;

	/// <summary>
	/// Static initializer.
	/// </summary>
	static EditorPerformedChartConfig()
	{
		// Initialize default arrow weights.
		DefaultArrowWeights = new Dictionary<string, List<int>>
		{
			[ChartTypeString(ChartType.dance_single)] = new() { 25, 25, 25, 25 },
			[ChartTypeString(ChartType.dance_double)] = new() { 6, 12, 10, 22, 22, 12, 10, 6 },
			[ChartTypeString(ChartType.dance_solo)] = new() { 13, 12, 25, 25, 12, 13 },
			[ChartTypeString(ChartType.dance_threepanel)] = new() { 25, 50, 25 },
			[ChartTypeString(ChartType.pump_single)] = new() { 17, 16, 34, 16, 17 },
			[ChartTypeString(ChartType.pump_halfdouble)] = new() { 25, 12, 13, 13, 12, 25 },
			[ChartTypeString(ChartType.pump_double)] = new() { 4, 4, 17, 12, 13, 13, 12, 17, 4, 4 },
			[ChartTypeString(ChartType.smx_beginner)] = new() { 25, 50, 25 },
			[ChartTypeString(ChartType.smx_single)] = new() { 25, 21, 8, 21, 25 },
			[ChartTypeString(ChartType.smx_dual)] = new() { 8, 17, 25, 25, 17, 8 },
			[ChartTypeString(ChartType.smx_full)] = new() { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 },
		};
	}

	[JsonInclude]
	public int TravelSpeedNoteTypeDenominatorIndex
	{
		get => TravelSpeedNoteTypeDenominatorIndexInternal;
		set
		{
			TravelSpeedNoteTypeDenominatorIndexInternal = MathUtils.Clamp(value, 0, ValidDenominators.Length - 1);
			UpdateStepTighteningSpeed();
		}
	}

	private int TravelSpeedNoteTypeDenominatorIndexInternal;

	[JsonInclude]
	public int TravelSpeedMinBPM
	{
		get => TravelSpeedMinBPMInternal;
		set
		{
			TravelSpeedMinBPMInternal = value;
			UpdateStepTighteningSpeed();
		}
	}

	private int TravelSpeedMinBPMInternal = 1;

	[JsonInclude]
	public int TravelSpeedMaxBPM
	{
		get => TravelSpeedMaxBPMInternal;
		set
		{
			TravelSpeedMaxBPMInternal = value;
			UpdateStepTighteningSpeed();
		}
	}

	private int TravelSpeedMaxBPMInternal = 1;

	/// <summary>
	/// Constructor.
	/// </summary>
	public EditorPerformedChartConfig()
	{
	}

	/// <summary>
	/// Constructor taking a previously generated Guid.
	/// </summary>
	/// <param name="guid">Guid for this EditorPerformedChartConfig.</param>
	public EditorPerformedChartConfig(Guid guid) : base(guid)
	{
	}

	#region EditorConfig

	/// <summary>
	/// Returns a new EditorPerformedChartConfig that is a clone of this EditorPerformedChartConfig.
	/// </summary>
	/// <param name="snapshot">
	/// If true then everything on this EditorPerformedChartConfig will be cloned.
	/// If false then the Guid and Name will be changed.
	/// </param>
	/// <returns>Cloned EditorPerformedChartConfig.</returns>
	protected override EditorPerformedChartConfig CloneImplementation(bool snapshot)
	{
		return new EditorPerformedChartConfig(snapshot ? Guid : Guid.NewGuid())
		{
			TravelSpeedNoteTypeDenominatorIndex = TravelSpeedNoteTypeDenominatorIndex,
			TravelSpeedMinBPM = TravelSpeedMinBPM,
			TravelSpeedMaxBPM = TravelSpeedMaxBPM,
		};
	}

	public override bool IsDefault()
	{
		return Guid.Equals(PerformedChartConfigManager.DefaultPerformedChartConfigGuid)
		       || Guid.Equals(PerformedChartConfigManager.DefaultPerformedChartStaminaGuid);
	}

	public override void InitializeWithDefaultValues()
	{
		Config.Facing.MaxInwardPercentage = DefaultFacingMaxInwardPercentage;
		Config.Facing.InwardPercentageCutoff = DefaultFacingInwardPercentageCutoff;
		Config.Facing.MaxOutwardPercentage = DefaultFacingMaxOutwardPercentage;
		Config.Facing.OutwardPercentageCutoff = DefaultFacingOutwardPercentageCutoff;
		Config.StepTightening.SpeedTighteningEnabled = DefaultStepTighteningTravelSpeedEnabled;
		TravelSpeedNoteTypeDenominatorIndex = DefaultStepTighteningTravelSpeedNoteDenominatorIndex;
		TravelSpeedMinBPM = DefaultStepTighteningTravelSpeedMinBPM;
		TravelSpeedMaxBPM = DefaultStepTighteningTravelSpeedMaxBPM;
		Config.StepTightening.SpeedTighteningMinDistance = DefaultStepTighteningTravelSpeedTighteningMinDistance;
		Config.StepTightening.DistanceTighteningEnabled = DefaultStepTighteningTravelDistanceEnabled;
		Config.StepTightening.DistanceMin = DefaultStepTighteningTravelDistanceMin;
		Config.StepTightening.DistanceMax = DefaultStepTighteningTravelDistanceMax;
		Config.StepTightening.StretchTighteningEnabled = DefaultStepTighteningStretchEnabled;
		Config.StepTightening.StretchDistanceMin = DefaultStepTighteningStretchDistanceMin;
		Config.StepTightening.StretchDistanceMax = DefaultStepTighteningStretchDistanceMax;
		Config.StepTightening.LateralMinPanelDistance = DefaultStepTighteningLateralMinPanelDistance;
		Config.StepTightening.LongitudinalMinPanelDistance = DefaultStepTighteningLongitudinalMinPanelDistance;
		Config.LateralTightening.Enabled = DefaultLateralTighteningEnabled;
		Config.LateralTightening.RelativeNPS = DefaultLateralTighteningRelativeNPS;
		Config.LateralTightening.AbsoluteNPS = DefaultLateralTighteningAbsoluteNPS;
		Config.LateralTightening.Speed = DefaultLateralTighteningSpeed;
		Config.Transitions.Enabled = DefaultTransitionsEnabled;
		Config.Transitions.StepsPerTransitionMin = DefaultTransitionsStepsPerTransitionMin;
		Config.Transitions.StepsPerTransitionMax = DefaultTransitionsStepsPerTransitionMax;
		Config.Transitions.MinimumPadWidth = DefaultTransitionsMinimumPadWidth;
		Config.Transitions.TransitionCutoffPercentage = DefaultTransitionsCutoffPercentage;

		Config.ArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (chartType, weights) in DefaultArrowWeights)
		{
			var defaultWeights = new List<int>(weights.Count);
			foreach (var weight in weights)
				defaultWeights.Add(weight);
			Config.ArrowWeights[chartType] = defaultWeights;
		}
	}

	protected override bool EditorConfigEquals(EditorConfig<Config> other)
	{
		return Equals(other);
	}

	#endregion IEditorConfig

	/// <summary>
	/// Sets the arrow weight for a given lane of a given ChartType.
	/// Will update normalized weights.
	/// </summary>
	/// <param name="chartType">ChartType to set the arrow weight for.</param>
	/// <param name="laneIndex">Lane index to set the arrow weight for.</param>
	/// <param name="weight">New weight</param>
	public void SetArrowWeight(ChartType chartType, int laneIndex, int weight)
	{
		var chartTypeString = ChartTypeString(chartType);
		if (!Config.ArrowWeights.TryGetValue(chartTypeString, out var weights))
			return;
		if (laneIndex < 0 || laneIndex >= weights.Count)
			return;
		if (weights[laneIndex] == weight)
			return;
		weights[laneIndex] = weight;
		Config.RefreshArrowWeightsNormalized(chartTypeString);
	}

	/// <summary>
	/// Updates the Config's StepTightening speed values from this EditorPerformedChartConfig's BPM values.
	/// </summary>
	private void UpdateStepTighteningSpeed()
	{
		var notesPerBeat = ValidDenominators[TravelSpeedNoteTypeDenominatorIndex];
		Config.StepTightening.SpeedMinTimeSeconds = 60.0 / (notesPerBeat * TravelSpeedMaxBPM) * Constants.NumFeet;
		Config.StepTightening.SpeedMaxTimeSeconds = 60.0 / (notesPerBeat * TravelSpeedMinBPM) * Constants.NumFeet;
	}

	/// <summary>
	/// Gets the maximum number of weights for any ChartType in this PerformedChart Config.
	/// </summary>
	/// <returns>Maximum number of weights for any ChartType in this PerformedChart Config.</returns>
	public int GetMaxNumWeightsForAnyChartType()
	{
		var max = 0;
		foreach (var (_, weights) in Config.ArrowWeights)
			max = Math.Max(max, weights.Count);
		return max;
	}

	/// <summary>
	/// Returns whether or not this EditorPerformedChartConfig is using all default values.
	/// </summary>
	/// <returns>
	/// True if this EditorPerformedChartConfig is using all default values and false otherwise.
	/// </returns>
	public bool IsUsingDefaults()
	{
		if (!(Config.Facing.MaxInwardPercentage.DoubleEquals(DefaultFacingMaxInwardPercentage)
		      && Config.Facing.InwardPercentageCutoff.DoubleEquals(DefaultFacingInwardPercentageCutoff)
		      && Config.Facing.MaxOutwardPercentage.DoubleEquals(DefaultFacingMaxOutwardPercentage)
		      && Config.Facing.OutwardPercentageCutoff.DoubleEquals(DefaultFacingOutwardPercentageCutoff)
		      && Config.StepTightening.SpeedTighteningEnabled == DefaultStepTighteningTravelSpeedEnabled
		      && TravelSpeedNoteTypeDenominatorIndex == DefaultStepTighteningTravelSpeedNoteDenominatorIndex
		      && TravelSpeedMinBPM == DefaultStepTighteningTravelSpeedMinBPM
		      && TravelSpeedMaxBPM == DefaultStepTighteningTravelSpeedMaxBPM
		      && Config.StepTightening.SpeedTighteningMinDistance.DoubleEquals(
			      DefaultStepTighteningTravelSpeedTighteningMinDistance)
		      && Config.StepTightening.DistanceTighteningEnabled == DefaultStepTighteningTravelDistanceEnabled
		      && Config.StepTightening.DistanceMin.DoubleEquals(DefaultStepTighteningTravelDistanceMin)
		      && Config.StepTightening.DistanceMax.DoubleEquals(DefaultStepTighteningTravelDistanceMax)
		      && Config.StepTightening.StretchTighteningEnabled == DefaultStepTighteningStretchEnabled
		      && Config.StepTightening.StretchDistanceMin.DoubleEquals(DefaultStepTighteningStretchDistanceMin)
		      && Config.StepTightening.StretchDistanceMax.DoubleEquals(DefaultStepTighteningStretchDistanceMax)
		      && Config.StepTightening.LateralMinPanelDistance.DoubleEquals(DefaultStepTighteningLateralMinPanelDistance)
		      && Config.StepTightening.LongitudinalMinPanelDistance.DoubleEquals(
			      DefaultStepTighteningLongitudinalMinPanelDistance)
		      && Config.LateralTightening.Enabled == DefaultLateralTighteningEnabled
		      && Config.LateralTightening.RelativeNPS.DoubleEquals(DefaultLateralTighteningRelativeNPS)
		      && Config.LateralTightening.AbsoluteNPS.DoubleEquals(DefaultLateralTighteningAbsoluteNPS)
		      && Config.LateralTightening.Speed.DoubleEquals(DefaultLateralTighteningSpeed)
		      && Config.Transitions.Enabled == DefaultTransitionsEnabled
		      && Config.Transitions.StepsPerTransitionMin == DefaultTransitionsStepsPerTransitionMin
		      && Config.Transitions.StepsPerTransitionMax == DefaultTransitionsStepsPerTransitionMax
		      && Config.Transitions.MinimumPadWidth == DefaultTransitionsMinimumPadWidth
		      && Config.Transitions.TransitionCutoffPercentage.DoubleEquals(DefaultTransitionsCutoffPercentage)))
		{
			return false;
		}

		if (Config.ArrowWeights.Count != DefaultArrowWeights.Count)
			return false;

		foreach (var (chartType, weights) in Config.ArrowWeights)
		{
			if (!DefaultArrowWeights.TryGetValue(chartType, out var defaultWeights))
				return false;
			if (weights.Count != defaultWeights.Count)
				return false;
			for (var i = 0; i < weights.Count; i++)
			{
				if (weights[i] != defaultWeights[i])
					return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Restores this EditorPerformedChartConfig to its default values.
	/// </summary>
	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestorePerformedChartConfigDefaults(this));
	}

	public static ActionAddPerformedChartConfig GetCreateNewConfigAction()
	{
		return new ActionAddPerformedChartConfig(Guid.NewGuid());
	}

	public static void CreateNewConfigAndShowEditUI()
	{
		var createAction = GetCreateNewConfigAction();
		ActionQueue.Instance.Do(createAction);
		ShowEditUI(createAction.GetGuid());
	}

	public static void ShowEditUI(Guid configGuid)
	{
		Preferences.Instance.ActivePerformedChartConfigForWindow = configGuid;
		Preferences.Instance.ShowPerformedChartListWindow = true;
		ImGui.SetWindowFocus(UIPerformedChartConfig.WindowTitle);
	}

	#region IEquatable

	public bool Equals(EditorPerformedChartConfig other)
	{
		if (ReferenceEquals(null, other))
			return false;
		if (ReferenceEquals(this, other))
			return true;

		return Guid == other.Guid
		       && Name == other.Name
		       && Description == other.Description
		       && TravelSpeedNoteTypeDenominatorIndex == other.TravelSpeedNoteTypeDenominatorIndex
		       && TravelSpeedMinBPM == other.TravelSpeedMinBPM
		       && TravelSpeedMaxBPM == other.TravelSpeedMaxBPM
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
		return Equals((EditorPerformedChartConfig)obj);
	}

	public override int GetHashCode()
	{
		// ReSharper disable NonReadonlyMemberInGetHashCode
		return HashCode.Combine(
			Guid,
			Name,
			Description,
			TravelSpeedNoteTypeDenominatorIndex,
			TravelSpeedMinBPM,
			TravelSpeedMaxBPM,
			Config);
		// ReSharper restore NonReadonlyMemberInGetHashCode
	}

	#endregion IEquatable
}

#region ActionRestorePerformedChartConfigDefaults

/// <summary>
/// Action to restore a EditorPerformedChartConfig to its default values.
/// </summary>
internal sealed class ActionRestorePerformedChartConfigDefaults : EditorAction
{
	private readonly EditorPerformedChartConfig Config;

	private readonly double PreviousFacingMaxInwardPercentage;
	private readonly double PreviousFacingInwardPercentageCutoff;
	private readonly double PreviousFacingMaxOutwardPercentage;
	private readonly double PreviousFacingOutwardPercentageCutoff;
	private readonly bool PreviousStepTighteningTravelSpeedEnabled;
	private readonly int PreviousTravelSpeedNoteTypeDenominatorIndex;
	private readonly int PreviousTravelSpeedMinBPM;
	private readonly int PreviousTravelSpeedMaxBPM;
	private readonly double PreviousStepTighteningSpeedTighteningMinDistance;
	private readonly bool PreviousStepTighteningTravelDistanceEnabled;
	private readonly double PreviousStepTighteningTravelDistanceMin;
	private readonly double PreviousStepTighteningTravelDistanceMax;
	private readonly bool PreviousStepTighteningStretchEnabled;
	private readonly double PreviousStepTighteningStretchDistanceMin;
	private readonly double PreviousStepTighteningStretchDistanceMax;
	private readonly double PreviousStepTighteningLateralMinPanelDistance;
	private readonly double PreviousStepTighteningLongitudinalMinPanelDistance;
	private readonly bool PreviousLateralTighteningEnabled;
	private readonly double PreviousLateralTighteningRelativeNPS;
	private readonly double PreviousLateralTighteningAbsoluteNPS;
	private readonly double PreviousLateralTighteningSpeed;
	private readonly bool PreviousTransitionsEnabled;
	private readonly int PreviousTransitionsStepsPerTransitionMin;
	private readonly int PreviousTransitionsStepsPerTransitionMax;
	private readonly int PreviousTransitionsMinimumPadWidth;
	private readonly double PreviousTransitionsCutoffPercentage;

	private readonly Dictionary<string, List<int>> PreviousArrowWeights;

	public ActionRestorePerformedChartConfigDefaults(EditorPerformedChartConfig config) : base(false, false)
	{
		Config = config;

		PreviousFacingMaxInwardPercentage = Config.Config.Facing.MaxInwardPercentage;
		PreviousFacingInwardPercentageCutoff = Config.Config.Facing.InwardPercentageCutoff;
		PreviousFacingMaxOutwardPercentage = Config.Config.Facing.MaxOutwardPercentage;
		PreviousFacingOutwardPercentageCutoff = Config.Config.Facing.OutwardPercentageCutoff;
		PreviousStepTighteningTravelSpeedEnabled = Config.Config.StepTightening.IsSpeedTighteningEnabled();
		PreviousTravelSpeedNoteTypeDenominatorIndex = config.TravelSpeedNoteTypeDenominatorIndex;
		PreviousTravelSpeedMinBPM = config.TravelSpeedMinBPM;
		PreviousTravelSpeedMaxBPM = config.TravelSpeedMaxBPM;
		PreviousStepTighteningSpeedTighteningMinDistance = Config.Config.StepTightening.SpeedTighteningMinDistance;
		PreviousStepTighteningTravelDistanceEnabled = Config.Config.StepTightening.IsDistanceTighteningEnabled();
		PreviousStepTighteningTravelDistanceMin = Config.Config.StepTightening.DistanceMin;
		PreviousStepTighteningTravelDistanceMax = Config.Config.StepTightening.DistanceMax;
		PreviousStepTighteningStretchEnabled = Config.Config.StepTightening.IsStretchTighteningEnabled();
		PreviousStepTighteningStretchDistanceMin = Config.Config.StepTightening.StretchDistanceMin;
		PreviousStepTighteningStretchDistanceMax = Config.Config.StepTightening.StretchDistanceMax;
		PreviousStepTighteningLateralMinPanelDistance = Config.Config.StepTightening.LateralMinPanelDistance;
		PreviousStepTighteningLongitudinalMinPanelDistance = Config.Config.StepTightening.LongitudinalMinPanelDistance;
		PreviousLateralTighteningEnabled = Config.Config.LateralTightening.IsEnabled();
		PreviousLateralTighteningRelativeNPS = Config.Config.LateralTightening.RelativeNPS;
		PreviousLateralTighteningAbsoluteNPS = Config.Config.LateralTightening.AbsoluteNPS;
		PreviousLateralTighteningSpeed = Config.Config.LateralTightening.Speed;
		PreviousTransitionsEnabled = Config.Config.Transitions.IsEnabled();
		PreviousTransitionsStepsPerTransitionMin = Config.Config.Transitions.StepsPerTransitionMin;
		PreviousTransitionsStepsPerTransitionMax = Config.Config.Transitions.StepsPerTransitionMax;
		PreviousTransitionsMinimumPadWidth = Config.Config.Transitions.MinimumPadWidth;
		PreviousTransitionsCutoffPercentage = Config.Config.Transitions.TransitionCutoffPercentage;

		PreviousArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (chartType, weights) in Config.Config.ArrowWeights)
		{
			var previousWeights = new List<int>(weights.Count);
			foreach (var weight in weights)
				previousWeights.Add(weight);
			PreviousArrowWeights[chartType] = previousWeights;
		}
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Restore {Config.Name} Performed Chart Config to default values.";
	}

	protected override void DoImplementation()
	{
		Config.Config.Facing.MaxInwardPercentage =
			EditorPerformedChartConfig.DefaultFacingMaxInwardPercentage;
		Config.Config.Facing.InwardPercentageCutoff =
			EditorPerformedChartConfig.DefaultFacingInwardPercentageCutoff;
		Config.Config.Facing.MaxOutwardPercentage =
			EditorPerformedChartConfig.DefaultFacingMaxOutwardPercentage;
		Config.Config.Facing.OutwardPercentageCutoff =
			EditorPerformedChartConfig.DefaultFacingOutwardPercentageCutoff;
		Config.Config.StepTightening.SpeedTighteningEnabled =
			EditorPerformedChartConfig.DefaultStepTighteningTravelSpeedEnabled;
		Config.TravelSpeedMinBPM =
			EditorPerformedChartConfig.DefaultStepTighteningTravelSpeedMinBPM;
		Config.TravelSpeedMaxBPM =
			EditorPerformedChartConfig.DefaultStepTighteningTravelSpeedMaxBPM;
		Config.TravelSpeedNoteTypeDenominatorIndex =
			EditorPerformedChartConfig.DefaultStepTighteningTravelSpeedNoteDenominatorIndex;
		Config.Config.StepTightening.SpeedTighteningMinDistance =
			EditorPerformedChartConfig.DefaultStepTighteningTravelSpeedTighteningMinDistance;
		Config.Config.StepTightening.DistanceTighteningEnabled =
			EditorPerformedChartConfig.DefaultStepTighteningTravelDistanceEnabled;
		Config.Config.StepTightening.DistanceMin =
			EditorPerformedChartConfig.DefaultStepTighteningTravelDistanceMin;
		Config.Config.StepTightening.DistanceMax =
			EditorPerformedChartConfig.DefaultStepTighteningTravelDistanceMax;
		Config.Config.StepTightening.StretchTighteningEnabled =
			EditorPerformedChartConfig.DefaultStepTighteningStretchEnabled;
		Config.Config.StepTightening.StretchDistanceMin =
			EditorPerformedChartConfig.DefaultStepTighteningStretchDistanceMin;
		Config.Config.StepTightening.StretchDistanceMax =
			EditorPerformedChartConfig.DefaultStepTighteningStretchDistanceMax;
		Config.Config.StepTightening.LateralMinPanelDistance =
			EditorPerformedChartConfig.DefaultStepTighteningLateralMinPanelDistance;
		Config.Config.StepTightening.LongitudinalMinPanelDistance =
			EditorPerformedChartConfig.DefaultStepTighteningLongitudinalMinPanelDistance;
		Config.Config.LateralTightening.Enabled =
			EditorPerformedChartConfig.DefaultLateralTighteningEnabled;
		Config.Config.LateralTightening.RelativeNPS =
			EditorPerformedChartConfig.DefaultLateralTighteningRelativeNPS;
		Config.Config.LateralTightening.AbsoluteNPS =
			EditorPerformedChartConfig.DefaultLateralTighteningAbsoluteNPS;
		Config.Config.LateralTightening.Speed =
			EditorPerformedChartConfig.DefaultLateralTighteningSpeed;
		Config.Config.Transitions.Enabled =
			EditorPerformedChartConfig.DefaultTransitionsEnabled;
		Config.Config.Transitions.StepsPerTransitionMin =
			EditorPerformedChartConfig.DefaultTransitionsStepsPerTransitionMin;
		Config.Config.Transitions.StepsPerTransitionMax =
			EditorPerformedChartConfig.DefaultTransitionsStepsPerTransitionMax;
		Config.Config.Transitions.MinimumPadWidth =
			EditorPerformedChartConfig.DefaultTransitionsMinimumPadWidth;
		Config.Config.Transitions.TransitionCutoffPercentage =
			EditorPerformedChartConfig.DefaultTransitionsCutoffPercentage;

		Config.Config.ArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (defaultChartType, defaultWeights) in EditorPerformedChartConfig.DefaultArrowWeights)
		{
			var weights = new List<int>(defaultWeights.Count);
			foreach (var defaultWeight in defaultWeights)
				weights.Add(defaultWeight);
			Config.Config.ArrowWeights[defaultChartType] = weights;
		}
	}

	protected override void UndoImplementation()
	{
		Config.Config.Facing.MaxInwardPercentage = PreviousFacingMaxInwardPercentage;
		Config.Config.Facing.InwardPercentageCutoff = PreviousFacingInwardPercentageCutoff;
		Config.Config.Facing.MaxOutwardPercentage = PreviousFacingMaxOutwardPercentage;
		Config.Config.Facing.OutwardPercentageCutoff = PreviousFacingOutwardPercentageCutoff;
		Config.Config.StepTightening.SpeedTighteningEnabled = PreviousStepTighteningTravelSpeedEnabled;
		Config.TravelSpeedMinBPM = PreviousTravelSpeedMinBPM;
		Config.TravelSpeedMaxBPM = PreviousTravelSpeedMaxBPM;
		Config.TravelSpeedNoteTypeDenominatorIndex = PreviousTravelSpeedNoteTypeDenominatorIndex;
		Config.Config.StepTightening.SpeedTighteningMinDistance = PreviousStepTighteningSpeedTighteningMinDistance;
		Config.Config.StepTightening.DistanceTighteningEnabled = PreviousStepTighteningTravelDistanceEnabled;
		Config.Config.StepTightening.DistanceMin = PreviousStepTighteningTravelDistanceMin;
		Config.Config.StepTightening.DistanceMax = PreviousStepTighteningTravelDistanceMax;
		Config.Config.StepTightening.StretchTighteningEnabled = PreviousStepTighteningStretchEnabled;
		Config.Config.StepTightening.StretchDistanceMin = PreviousStepTighteningStretchDistanceMin;
		Config.Config.StepTightening.StretchDistanceMax = PreviousStepTighteningStretchDistanceMax;
		Config.Config.StepTightening.LateralMinPanelDistance = PreviousStepTighteningLateralMinPanelDistance;
		Config.Config.StepTightening.LongitudinalMinPanelDistance = PreviousStepTighteningLongitudinalMinPanelDistance;
		Config.Config.LateralTightening.Enabled = PreviousLateralTighteningEnabled;
		Config.Config.LateralTightening.RelativeNPS = PreviousLateralTighteningRelativeNPS;
		Config.Config.LateralTightening.AbsoluteNPS = PreviousLateralTighteningAbsoluteNPS;
		Config.Config.LateralTightening.Speed = PreviousLateralTighteningSpeed;
		Config.Config.Transitions.Enabled = PreviousTransitionsEnabled;
		Config.Config.Transitions.StepsPerTransitionMin = PreviousTransitionsStepsPerTransitionMin;
		Config.Config.Transitions.StepsPerTransitionMax = PreviousTransitionsStepsPerTransitionMax;
		Config.Config.Transitions.MinimumPadWidth = PreviousTransitionsMinimumPadWidth;
		Config.Config.Transitions.TransitionCutoffPercentage = PreviousTransitionsCutoffPercentage;

		Config.Config.ArrowWeights = new Dictionary<string, List<int>>();
		foreach (var (previousChartType, previousWeights) in PreviousArrowWeights)
		{
			var weights = new List<int>(previousWeights.Count);
			foreach (var previousWeight in previousWeights)
				weights.Add(previousWeight);
			Config.Config.ArrowWeights[previousChartType] = weights;
		}
	}
}

#endregion ActionRestorePerformedChartConfigDefaults
