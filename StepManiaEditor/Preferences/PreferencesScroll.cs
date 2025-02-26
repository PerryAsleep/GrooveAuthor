﻿using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Preferences for scrolling.
/// </summary>
internal sealed class PreferencesScroll : Notifier<PreferencesScroll>
{
	public const double DefaultVariableSpeedBPM = 120.0;

	public const string NotificationTimeBasedPpsChanged = "TimeBasedPpsChanged";
	public const string NotificationRowBasedPprChanged = "RowBasedPprChanged";
	public const string NotificationVariablePpsChanged = "VariablePpsChanged";
	public const string NotificationSizeCapChanged = "SizeCapChanged";
	public const string NotificationLimitZoomToSizeChanged = "LimitZoomToSizeChanged";

	// Default values.
	public const Editor.SpacingMode DefaultSpacingMode = Editor.SpacingMode.ConstantTime;
	public const double DefaultTimeBasedPixelsPerSecond = 300.0;
	public const double DefaultRowBasedPixelsPerRow = 2.0;
	public const double DefaultVariablePixelsPerSecondAtDefaultBPM = 300.0;
	public const Editor.WaveFormScrollMode DefaultRowBasedWaveFormScrollMode = Editor.WaveFormScrollMode.MostCommonTempo;
	public const bool DefaultStopPlaybackWhenScrolling = false;
	public const bool DefaultStopPlaybackWhenDraggingScrollBars = false;
	public const double DefaultZoomMultiplier = 1.2;
	public const double DefaultScrollWheelTime = 0.25;
	public const int DefaultScrollWheelRows = 48;
	public const double DefaultScrollInterpolationDuration = 0.1;
	public const double DefaultSizeCap = 1.0;
	public const bool DefaultLimitZoomToSize = false;

	// Preferences.
	[JsonInclude] public bool ShowScrollControlPreferencesWindow;
	[JsonInclude] public Editor.SpacingMode SpacingMode = DefaultSpacingMode;

	[JsonInclude]
	public double TimeBasedPixelsPerSecond
	{
		get => TimeBasedPixelsPerSecondInternal;
		set
		{
			if (!TimeBasedPixelsPerSecondInternal.DoubleEquals(value))
			{
				TimeBasedPixelsPerSecondInternal = value;
				Notify(NotificationTimeBasedPpsChanged, this);
			}
		}
	}

	/// <summary>
	/// Float property for ImGui slider limitations.
	/// </summary>
	[JsonIgnore]
	public float TimeBasedPixelsPerSecondFloat
	{
		get => (float)TimeBasedPixelsPerSecond;
		set => TimeBasedPixelsPerSecond = value;
	}

	private double TimeBasedPixelsPerSecondInternal = DefaultTimeBasedPixelsPerSecond;

	[JsonInclude]
	public double RowBasedPixelsPerRow
	{
		get => RowBasedPixelsPerRowInternal;
		set
		{
			if (!RowBasedPixelsPerRowInternal.DoubleEquals(value))
			{
				RowBasedPixelsPerRowInternal = value;
				Notify(NotificationRowBasedPprChanged, this);
			}
		}
	}

	/// <summary>
	/// Float property for ImGui slider limitations.
	/// </summary>
	[JsonIgnore]
	public float RowBasedPixelsPerRowFloat
	{
		get => (float)RowBasedPixelsPerRow;
		set => RowBasedPixelsPerRow = value;
	}

	private double RowBasedPixelsPerRowInternal = DefaultRowBasedPixelsPerRow;

	[JsonInclude]
	public double VariablePixelsPerSecondAtDefaultBPM
	{
		get => VariablePixelsPerSecondAtDefaultBPMInternal;
		set
		{
			if (!VariablePixelsPerSecondAtDefaultBPMInternal.DoubleEquals(value))
			{
				VariablePixelsPerSecondAtDefaultBPMInternal = value;
				Notify(NotificationVariablePpsChanged, this);
			}
		}
	}

	/// <summary>
	/// Float property for ImGui slider limitations.
	/// </summary>
	[JsonIgnore]
	public float VariablePixelsPerSecondAtDefaultBPMFloat
	{
		get => (float)VariablePixelsPerSecondAtDefaultBPM;
		set => VariablePixelsPerSecondAtDefaultBPM = value;
	}

	private double VariablePixelsPerSecondAtDefaultBPMInternal = DefaultVariablePixelsPerSecondAtDefaultBPM;

	[JsonInclude]
	public double SizeCap
	{
		get => SizeCapInternal;
		set
		{
			if (!SizeCapInternal.DoubleEquals(value))
			{
				SizeCapInternal = value;
				Notify(NotificationSizeCapChanged, this);
			}
		}
	}

	private double SizeCapInternal = DefaultSizeCap;

	[JsonInclude]
	public bool LimitZoomToSize
	{
		get => LimitZoomToSizeInternal;
		set
		{
			if (LimitZoomToSizeInternal != value)
			{
				LimitZoomToSizeInternal = value;
				Notify(NotificationLimitZoomToSizeChanged, this);
			}
		}
	}

	private bool LimitZoomToSizeInternal = DefaultLimitZoomToSize;

	[JsonInclude] public Editor.WaveFormScrollMode RowBasedWaveFormScrollMode = DefaultRowBasedWaveFormScrollMode;
	[JsonInclude] public bool StopPlaybackWhenScrolling = DefaultStopPlaybackWhenScrolling;
	[JsonInclude] public bool StopPlaybackWhenDraggingScrollBars = DefaultStopPlaybackWhenDraggingScrollBars;
	[JsonInclude] public double ZoomMultiplier = DefaultZoomMultiplier;
	[JsonInclude] public double ScrollWheelTime = DefaultScrollWheelTime;
	[JsonInclude] public int ScrollWheelRows = DefaultScrollWheelRows;
	[JsonInclude] public double ScrollInterpolationDuration = DefaultScrollInterpolationDuration;

	public bool IsUsingDefaults()
	{
		return SpacingMode == DefaultSpacingMode
		       && TimeBasedPixelsPerSecond.DoubleEquals(DefaultTimeBasedPixelsPerSecond)
		       && RowBasedPixelsPerRow.DoubleEquals(DefaultRowBasedPixelsPerRow)
		       && VariablePixelsPerSecondAtDefaultBPM.DoubleEquals(DefaultVariablePixelsPerSecondAtDefaultBPM)
		       && RowBasedWaveFormScrollMode == DefaultRowBasedWaveFormScrollMode
		       && StopPlaybackWhenScrolling == DefaultStopPlaybackWhenScrolling
		       && StopPlaybackWhenDraggingScrollBars == DefaultStopPlaybackWhenDraggingScrollBars
		       && ZoomMultiplier.DoubleEquals(DefaultZoomMultiplier)
		       && ScrollWheelTime.DoubleEquals(DefaultScrollWheelTime)
		       && ScrollWheelRows == DefaultScrollWheelRows
		       && ScrollInterpolationDuration.DoubleEquals(DefaultScrollInterpolationDuration)
		       && SizeCap.DoubleEquals(SizeCap)
		       && LimitZoomToSize == DefaultLimitZoomToSize;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreScrollPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore scroll preferences to their default values.
/// </summary>
internal sealed class ActionRestoreScrollPreferenceDefaults : EditorAction
{
	private readonly Editor.SpacingMode PreviousSpacingMode;
	private readonly double PreviousTimeBasedPixelsPerSecond;
	private readonly double PreviousRowBasedPixelsPerRow;
	private readonly double PreviousVariablePixelsPerSecondAtDefaultBPM;
	private readonly Editor.WaveFormScrollMode PreviousRowBasedWaveFormScrollMode;
	private readonly bool PreviousStopPlaybackWhenScrolling;
	private readonly bool PreviousStopPlaybackWhenDraggingScrollBars;
	private readonly double PreviousZoomMultiplier;
	private readonly double PreviousScrollWheelTime;
	private readonly int PreviousScrollWheelRows;
	private readonly double PreviousScrollInterpolationDuration;
	private readonly double PreviousSizeCap;
	private readonly bool PreviousLimitZoomToSize;

	public ActionRestoreScrollPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesScroll;
		PreviousSpacingMode = p.SpacingMode;
		PreviousTimeBasedPixelsPerSecond = p.TimeBasedPixelsPerSecond;
		PreviousRowBasedPixelsPerRow = p.RowBasedPixelsPerRow;
		PreviousVariablePixelsPerSecondAtDefaultBPM = p.VariablePixelsPerSecondAtDefaultBPM;
		PreviousRowBasedWaveFormScrollMode = p.RowBasedWaveFormScrollMode;
		PreviousStopPlaybackWhenScrolling = p.StopPlaybackWhenScrolling;
		PreviousStopPlaybackWhenDraggingScrollBars = p.StopPlaybackWhenDraggingScrollBars;
		PreviousZoomMultiplier = p.ZoomMultiplier;
		PreviousScrollWheelTime = p.ScrollWheelTime;
		PreviousScrollWheelRows = p.ScrollWheelRows;
		PreviousScrollInterpolationDuration = p.ScrollInterpolationDuration;
		PreviousSizeCap = p.SizeCap;
		PreviousLimitZoomToSize = p.LimitZoomToSize;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Scroll Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesScroll;
		p.SpacingMode = PreferencesScroll.DefaultSpacingMode;
		p.TimeBasedPixelsPerSecond = PreferencesScroll.DefaultTimeBasedPixelsPerSecond;
		p.RowBasedPixelsPerRow = PreferencesScroll.DefaultRowBasedPixelsPerRow;
		p.VariablePixelsPerSecondAtDefaultBPM = PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM;
		p.RowBasedWaveFormScrollMode = PreferencesScroll.DefaultRowBasedWaveFormScrollMode;
		p.StopPlaybackWhenScrolling = PreferencesScroll.DefaultStopPlaybackWhenScrolling;
		p.StopPlaybackWhenDraggingScrollBars = PreferencesScroll.DefaultStopPlaybackWhenDraggingScrollBars;
		p.ZoomMultiplier = PreferencesScroll.DefaultZoomMultiplier;
		p.ScrollWheelTime = PreferencesScroll.DefaultScrollWheelTime;
		p.ScrollWheelRows = PreferencesScroll.DefaultScrollWheelRows;
		p.ScrollInterpolationDuration = PreferencesScroll.DefaultScrollInterpolationDuration;
		p.SizeCap = PreferencesScroll.DefaultSizeCap;
		p.LimitZoomToSize = PreferencesScroll.DefaultLimitZoomToSize;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesScroll;
		p.SpacingMode = PreviousSpacingMode;
		p.TimeBasedPixelsPerSecond = PreviousTimeBasedPixelsPerSecond;
		p.RowBasedPixelsPerRow = PreviousRowBasedPixelsPerRow;
		p.VariablePixelsPerSecondAtDefaultBPM = PreviousVariablePixelsPerSecondAtDefaultBPM;
		p.RowBasedWaveFormScrollMode = PreviousRowBasedWaveFormScrollMode;
		p.StopPlaybackWhenScrolling = PreviousStopPlaybackWhenScrolling;
		p.StopPlaybackWhenDraggingScrollBars = PreviousStopPlaybackWhenDraggingScrollBars;
		p.ZoomMultiplier = PreviousZoomMultiplier;
		p.ScrollWheelTime = PreviousScrollWheelTime;
		p.ScrollWheelRows = PreviousScrollWheelRows;
		p.ScrollInterpolationDuration = PreviousScrollInterpolationDuration;
		p.SizeCap = PreviousSizeCap;
		p.LimitZoomToSize = PreviousLimitZoomToSize;
	}
}
