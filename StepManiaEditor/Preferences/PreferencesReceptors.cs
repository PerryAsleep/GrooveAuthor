using System.Text.Json.Serialization;

namespace StepManiaEditor;

/// <summary>
/// Preferences for receptors.
/// </summary>
internal sealed class PreferencesReceptors
{
	private Editor Editor;

	// Default values.
	public const bool DefaultAutoPlayHideArrows = true;
	public const bool DefaultAutoPlayLightHolds = true;
	public const bool DefaultAutoPlayRimEffect = true;
	public const bool DefaultAutoPlayGlowEffect = true;
	public const bool DefaultAutoPlayShrinkEffect = true;
	public const bool DefaultTapRimEffect = true;
	public const bool DefaultTapShrinkEffect = true;
	public const bool DefaultPulseReceptorsWithTempo = true;
	public const bool DefaultCenterHorizontally = false;
	public const bool DefaultLockPosition = false;
	public const int DefaultChartSpacePositionX = 1134;
	public const int DefaultChartSpacePositionY = 110;

	// Preferences.
	[JsonInclude] public bool ShowReceptorPreferencesWindow;
	[JsonInclude] public bool AutoPlayHideArrows = DefaultAutoPlayHideArrows;
	[JsonInclude] public bool AutoPlayLightHolds = DefaultAutoPlayLightHolds;
	[JsonInclude] public bool AutoPlayRimEffect = DefaultAutoPlayRimEffect;
	[JsonInclude] public bool AutoPlayGlowEffect = DefaultAutoPlayGlowEffect;
	[JsonInclude] public bool AutoPlayShrinkEffect = DefaultAutoPlayShrinkEffect;
	[JsonInclude] public bool TapRimEffect = DefaultTapRimEffect;
	[JsonInclude] public bool TapShrinkEffect = DefaultTapShrinkEffect;
	[JsonInclude] public bool PulseReceptorsWithTempo = DefaultPulseReceptorsWithTempo;
	[JsonInclude] public bool CenterHorizontally = DefaultCenterHorizontally;
	[JsonInclude] public bool LockPosition = DefaultLockPosition;

	[JsonInclude]
	[JsonPropertyName("PositionX")]
	public int ChartSpacePositionX
	{
		get => ChartSpacePositionXInternal;
		set
		{
			ChartSpacePositionXInternal = value;
			if (Editor != null && Editor.GetChartAreaInChartSpaceWithoutHeader(out var chartArea))
			{
				if (ChartSpacePositionXInternal < chartArea.X)
					ChartSpacePositionXInternal = chartArea.X;
				if (ChartSpacePositionXInternal >= chartArea.Width)
					ChartSpacePositionXInternal = chartArea.Width - 1;
			}
			else
			{
				if (ChartSpacePositionXInternal < 0)
					ChartSpacePositionXInternal = 0;
			}
		}
	}

	[JsonInclude]
	[JsonPropertyName("PositionY")]
	public int ChartSpacePositionY
	{
		get => ChartSpacePositionYInternal;
		set
		{
			ChartSpacePositionYInternal = value;
			if (ChartSpacePositionYInternal < 0)
				ChartSpacePositionYInternal = 0;
			if (Editor != null && Editor.GetChartAreaInChartSpaceWithoutHeader(out var chartArea))
			{
				if (ChartSpacePositionYInternal < chartArea.Y)
					ChartSpacePositionYInternal = chartArea.Y;
				if (ChartSpacePositionYInternal >= chartArea.Height)
					ChartSpacePositionYInternal = chartArea.Height - 1;
			}
			else
			{
				if (ChartSpacePositionYInternal < 0)
					ChartSpacePositionYInternal = 0;
			}
		}
	}

	private int ChartSpacePositionXInternal = DefaultChartSpacePositionX;
	private int ChartSpacePositionYInternal = DefaultChartSpacePositionY;

	public void SetEditor(Editor editor)
	{
		Editor = editor;
	}

	public void ClampPositions()
	{
#pragma warning disable CA2245
		ChartSpacePositionX = ChartSpacePositionX;
		ChartSpacePositionY = ChartSpacePositionY;
#pragma warning restore CA2245
	}

	public bool IsUsingDefaults()
	{
		return AutoPlayHideArrows == DefaultAutoPlayHideArrows
		       && AutoPlayLightHolds == DefaultAutoPlayLightHolds
		       && AutoPlayRimEffect == DefaultAutoPlayRimEffect
		       && AutoPlayGlowEffect == DefaultAutoPlayGlowEffect
		       && AutoPlayShrinkEffect == DefaultAutoPlayShrinkEffect
		       && TapRimEffect == DefaultTapRimEffect
		       && TapShrinkEffect == DefaultTapShrinkEffect
		       && PulseReceptorsWithTempo == DefaultPulseReceptorsWithTempo
		       && CenterHorizontally == DefaultCenterHorizontally
		       && ChartSpacePositionX == DefaultChartSpacePositionX
		       && ChartSpacePositionY == DefaultChartSpacePositionY
		       && LockPosition == DefaultLockPosition;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreAnimationsPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore animation preferences to their default values.
/// </summary>
internal sealed class ActionRestoreAnimationsPreferenceDefaults : EditorAction
{
	private readonly bool PreviousAutoPlayHideArrows;
	private readonly bool PreviousAutoPlayLightHolds;
	private readonly bool PreviousAutoPlayRimEffect;
	private readonly bool PreviousAutoPlayGlowEffect;
	private readonly bool PreviousAutoPlayShrinkEffect;
	private readonly bool PreviousTapRimEffect;
	private readonly bool PreviousTapShrinkEffect;
	private readonly bool PreviousPulseReceptorsWithTempo;
	private readonly bool PreviousCenterHorizontally;
	private readonly bool PreviousLockPosition;
	private readonly int PreviousChartSpacePositionX;
	private readonly int PreviousChartSpacePositionY;

	public ActionRestoreAnimationsPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesReceptors;
		PreviousAutoPlayHideArrows = p.AutoPlayHideArrows;
		PreviousAutoPlayLightHolds = p.AutoPlayLightHolds;
		PreviousAutoPlayRimEffect = p.AutoPlayRimEffect;
		PreviousAutoPlayGlowEffect = p.AutoPlayGlowEffect;
		PreviousAutoPlayShrinkEffect = p.AutoPlayShrinkEffect;
		PreviousTapRimEffect = p.TapRimEffect;
		PreviousTapShrinkEffect = p.TapShrinkEffect;
		PreviousPulseReceptorsWithTempo = p.PulseReceptorsWithTempo;
		PreviousCenterHorizontally = p.CenterHorizontally;
		PreviousLockPosition = p.LockPosition;
		PreviousChartSpacePositionX = p.ChartSpacePositionX;
		PreviousChartSpacePositionY = p.ChartSpacePositionY;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Animation Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesReceptors;

		p.AutoPlayHideArrows = PreferencesReceptors.DefaultAutoPlayHideArrows;
		p.AutoPlayLightHolds = PreferencesReceptors.DefaultAutoPlayLightHolds;
		p.AutoPlayRimEffect = PreferencesReceptors.DefaultAutoPlayRimEffect;
		p.AutoPlayGlowEffect = PreferencesReceptors.DefaultAutoPlayGlowEffect;
		p.AutoPlayShrinkEffect = PreferencesReceptors.DefaultAutoPlayShrinkEffect;
		p.TapRimEffect = PreferencesReceptors.DefaultTapRimEffect;
		p.TapShrinkEffect = PreferencesReceptors.DefaultTapShrinkEffect;
		p.PulseReceptorsWithTempo = PreferencesReceptors.DefaultPulseReceptorsWithTempo;
		p.CenterHorizontally = PreferencesReceptors.DefaultCenterHorizontally;
		p.LockPosition = PreferencesReceptors.DefaultLockPosition;
		p.ChartSpacePositionX = PreferencesReceptors.DefaultChartSpacePositionX;
		p.ChartSpacePositionY = PreferencesReceptors.DefaultChartSpacePositionY;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesReceptors;
		p.AutoPlayHideArrows = PreviousAutoPlayHideArrows;
		p.AutoPlayLightHolds = PreviousAutoPlayLightHolds;
		p.AutoPlayRimEffect = PreviousAutoPlayRimEffect;
		p.AutoPlayGlowEffect = PreviousAutoPlayGlowEffect;
		p.AutoPlayShrinkEffect = PreviousAutoPlayShrinkEffect;
		p.TapRimEffect = PreviousTapRimEffect;
		p.TapShrinkEffect = PreviousTapShrinkEffect;
		p.PulseReceptorsWithTempo = PreviousPulseReceptorsWithTempo;
		p.CenterHorizontally = PreviousCenterHorizontally;
		p.LockPosition = PreviousLockPosition;
		p.ChartSpacePositionX = PreviousChartSpacePositionX;
		p.ChartSpacePositionY = PreviousChartSpacePositionY;
	}
}
