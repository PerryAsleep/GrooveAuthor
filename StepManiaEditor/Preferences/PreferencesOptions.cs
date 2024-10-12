using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fumen;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.PreferencesOptions;

namespace StepManiaEditor;

internal sealed class PreferencesOptions : Notifier<PreferencesOptions>
{
	public const string NotificationUndoHistorySizeChanged = "UndoHistorySizeChanged";

	/// <summary>
	/// How to color steps.
	/// </summary>
	public enum StepColorMethod
	{
		/// <summary>
		/// Color the same way Stepmania does, assuming 4/4 and 48 rows per beat.
		/// </summary>
		Stepmania,

		/// <summary>
		/// Color based on note type.
		/// </summary>
		Note,

		/// <summary>
		/// Color based on beat.
		/// </summary>
		Beat,
	}

	// Default values.
	public const int DefaultRecentFilesHistorySize = 20;
	public const ChartType DefaultDefaultStepsType = ChartType.dance_single;
	public const ChartDifficultyType DefaultDefaultDifficultyType = ChartDifficultyType.Challenge;

	public static readonly HashSet<ChartType> DefaultStartupStepGraphs = new()
	{
		ChartType.dance_single,
		ChartType.dance_double,
	};

	public const bool DefaultOpenLastOpenedFileOnLaunch = true;
	public const double DefaultNewSongSyncOffset = 0.009;
	public const double DefaultOpenSongSyncOffset = 0.009;
	public const int DefaultUndoHistorySize = 1024;
	public const bool DefaultUseCustomDpiScale = false;
	public const double DefaultDpiScale = 1.0;
	public const bool DefaultSuppressExternalSongModificationNotification = false;
	public const bool DefaultHideSongBackground = false;
	public const StepColorMethod DefaultStepColorMethodValue = StepColorMethod.Stepmania;
	public const bool DefaultResetWindows = true;

	// Preferences.
	[JsonInclude] public bool ShowOptionsWindow;
	[JsonInclude] public int RecentFilesHistorySize = DefaultRecentFilesHistorySize;
	[JsonInclude] public ChartType DefaultStepsType = DefaultDefaultStepsType;
	[JsonInclude] public ChartDifficultyType DefaultDifficultyType = DefaultDefaultDifficultyType;
	[JsonInclude] public HashSet<ChartType> StartupStepGraphs = new(DefaultStartupStepGraphs);
	[JsonInclude] public bool OpenLastOpenedFileOnLaunch = DefaultOpenLastOpenedFileOnLaunch;
	[JsonInclude] public double NewSongSyncOffset = DefaultNewSongSyncOffset;
	[JsonInclude] public double OpenSongSyncOffset = DefaultOpenSongSyncOffset;
	[JsonInclude] public bool UseCustomDpiScale = DefaultUseCustomDpiScale;
	[JsonInclude] public double DpiScale = DefaultDpiScale;
	[JsonInclude] public bool SuppressExternalSongModificationNotification = DefaultSuppressExternalSongModificationNotification;
	[JsonInclude] public bool HideSongBackground = DefaultHideSongBackground;
	[JsonInclude] public StepColorMethod StepColorMethodValue = DefaultStepColorMethodValue;
	[JsonInclude] public bool ResetWindows = DefaultResetWindows;

	[JsonInclude]
	public int UndoHistorySize
	{
		get => UndoHistorySizeInternal;
		set
		{
			if (UndoHistorySizeInternal != value)
			{
				UndoHistorySizeInternal = value;
				Notify(NotificationUndoHistorySizeChanged, this);
			}
		}
	}

	private int UndoHistorySizeInternal = DefaultUndoHistorySize;

	public static void RegisterDefaultsForInvalidEnumValues(PermissiveEnumJsonConverterFactory factory)
	{
		factory.RegisterDefault(DefaultStepColorMethodValue);
	}

	public bool IsUsingDefaults()
	{
		return RecentFilesHistorySize == DefaultRecentFilesHistorySize
		       && DefaultStepsType == DefaultDefaultStepsType
		       && DefaultDifficultyType == DefaultDefaultDifficultyType
		       && StartupStepGraphs.SetEquals(DefaultStartupStepGraphs)
		       && OpenLastOpenedFileOnLaunch == DefaultOpenLastOpenedFileOnLaunch
		       && NewSongSyncOffset.DoubleEquals(DefaultNewSongSyncOffset)
		       && OpenSongSyncOffset.DoubleEquals(DefaultOpenSongSyncOffset)
		       && UndoHistorySize == DefaultUndoHistorySize
		       && UseCustomDpiScale == DefaultUseCustomDpiScale
		       && DpiScale.DoubleEquals(DefaultDpiScale)
		       && SuppressExternalSongModificationNotification == DefaultSuppressExternalSongModificationNotification
		       && HideSongBackground == DefaultHideSongBackground
		       && StepColorMethodValue == DefaultStepColorMethodValue
		       && ResetWindows == DefaultResetWindows;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreOptionPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore Options preferences to their default values.
/// </summary>
internal sealed class ActionRestoreOptionPreferenceDefaults : EditorAction
{
	private readonly int PreviousRecentFilesHistorySize;
	private readonly ChartType PreviousDefaultStepsType;
	private readonly ChartDifficultyType PreviousDefaultDifficultyType;
	private readonly HashSet<ChartType> PreviousStartupStepGraphs;
	private readonly bool PreviousOpenLastOpenedFileOnLaunch;
	private readonly double PreviousNewSongSyncOffset;
	private readonly double PreviousOpenSongSyncOffset;
	private readonly int PreviousUndoHistorySize;
	private readonly bool PreviousUseCustomDpiScale;
	private readonly double PreviousDpiScale;
	private readonly bool PreviousSuppressExternalSongModificationNotification;
	private readonly bool PreviousHideSongBackground;
	private readonly StepColorMethod PreviousStepColorMethodValue;
	//private readonly bool PreviousResetWindows;

	public ActionRestoreOptionPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesOptions;

		PreviousRecentFilesHistorySize = p.RecentFilesHistorySize;
		PreviousDefaultStepsType = p.DefaultStepsType;
		PreviousDefaultDifficultyType = p.DefaultDifficultyType;
		PreviousStartupStepGraphs = new HashSet<ChartType>(p.StartupStepGraphs);
		PreviousOpenLastOpenedFileOnLaunch = p.OpenLastOpenedFileOnLaunch;
		PreviousNewSongSyncOffset = p.NewSongSyncOffset;
		PreviousOpenSongSyncOffset = p.OpenSongSyncOffset;
		PreviousUndoHistorySize = p.UndoHistorySize;
		PreviousUseCustomDpiScale = p.UseCustomDpiScale;
		PreviousDpiScale = p.DpiScale;
		PreviousSuppressExternalSongModificationNotification = p.SuppressExternalSongModificationNotification;
		PreviousHideSongBackground = p.HideSongBackground;
		PreviousStepColorMethodValue = p.StepColorMethodValue;
		//PreviousResetWindows = p.ResetWindows;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Options to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesOptions;
		p.RecentFilesHistorySize = DefaultRecentFilesHistorySize;
		p.DefaultStepsType = DefaultDefaultStepsType;
		p.DefaultDifficultyType = DefaultDefaultDifficultyType;
		p.StartupStepGraphs = new HashSet<ChartType>(DefaultStartupStepGraphs);
		p.OpenLastOpenedFileOnLaunch = DefaultOpenLastOpenedFileOnLaunch;
		p.NewSongSyncOffset = DefaultNewSongSyncOffset;
		p.OpenSongSyncOffset = DefaultOpenSongSyncOffset;
		p.UndoHistorySize = DefaultUndoHistorySize;
		p.UseCustomDpiScale = DefaultUseCustomDpiScale;
		p.DpiScale = DefaultDpiScale;
		p.SuppressExternalSongModificationNotification = DefaultSuppressExternalSongModificationNotification;
		p.HideSongBackground = DefaultHideSongBackground;
		p.StepColorMethodValue = DefaultStepColorMethodValue;
		//p.ResetWindows = DefaultResetWindows;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesOptions;
		p.RecentFilesHistorySize = PreviousRecentFilesHistorySize;
		p.DefaultStepsType = PreviousDefaultStepsType;
		p.DefaultDifficultyType = PreviousDefaultDifficultyType;
		p.StartupStepGraphs = new HashSet<ChartType>(PreviousStartupStepGraphs);
		p.OpenLastOpenedFileOnLaunch = PreviousOpenLastOpenedFileOnLaunch;
		p.NewSongSyncOffset = PreviousNewSongSyncOffset;
		p.OpenSongSyncOffset = PreviousOpenSongSyncOffset;
		p.UndoHistorySize = PreviousUndoHistorySize;
		p.UseCustomDpiScale = PreviousUseCustomDpiScale;
		p.DpiScale = PreviousDpiScale;
		p.SuppressExternalSongModificationNotification = PreviousSuppressExternalSongModificationNotification;
		p.HideSongBackground = PreviousHideSongBackground;
		p.StepColorMethodValue = PreviousStepColorMethodValue;
		//p.ResetWindows = PreviousResetWindows;
	}
}
