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

	/// <summary>
	/// How to size the song background image.
	/// </summary>
	public enum BackgroundImageSizeMode
	{
		/// <summary>
		/// Fill the chart area.
		/// </summary>
		ChartArea,

		/// <summary>
		/// Fill the window.
		/// </summary>
		Window,
	}

	/// <summary>
	/// Layouts that can be reset to.
	/// </summary>
	public enum Layout
	{
		/// <summary>
		/// Reset the layout, and determine the best layout to use based on the screen aspect ratio.
		/// </summary>
		Automatic,

		/// <summary>
		/// Reset to the default layout.
		/// </summary>
		Default,

		/// <summary>
		/// Reset to the expanded layout.
		/// </summary>
		Expanded,

		/// <summary>
		/// Reset to the portrait layout.
		/// </summary>
		Portrait,

		/// <summary>
		/// Reset to the high-res portrait layout.
		/// </summary>
		PortraitHighRes,

		/// <summary>
		/// Do not reset the layout.
		/// </summary>
		None,
	}

	// Default values.
	public const int DefaultRecentFilesHistorySize = 20;
	public const ChartType DefaultDefaultStepsType = ChartType.dance_single;
	public const ChartDifficultyType DefaultDefaultDifficultyType = ChartDifficultyType.Challenge;

	public static readonly HashSet<ChartType> DefaultStartupStepGraphs =
	[
		ChartType.dance_single,
		ChartType.dance_double,
	];

	public const bool DefaultOpenLastOpenedFileOnLaunch = true;
	public const double DefaultNewSongSyncOffset = 0.009;
	public const double DefaultOpenSongSyncOffset = 0.009;
	public const bool DefaultUseCustomDpiScale = false;
	public const double DefaultDpiScale = 1.0;
	public const bool DefaultSuppressExternalSongModificationNotification = false;
	public const bool DefaultSuppressUpdateNotification = false;
	public const bool DefaultHideSongBackground = false;
	public const StepColorMethod DefaultStepColorMethodValue = StepColorMethod.Stepmania;
	public const Layout DefaultResetLayout = Layout.Automatic;
	public const BackgroundImageSizeMode DefaultBackgroundImageSize = BackgroundImageSizeMode.ChartArea;
	public const bool DefaultRenderNotes = true;
	public const bool DefaultRenderMarkers = true;
	public const bool DefaultRenderRegions = true;
	public const bool DefaultRenderMiscEvents = true;
	public const int DefaultMiscEventAreaWidth = 100;
	public const int DefaultMaxMarkersToDraw = 256;
	public const int DefaultMaxEventsToDraw = 2048;
	public const int DefaultMaxRateAlteringEventsToProcessPerFrame = 256;
	public const int DefaultMiniMapMaxNotesToDraw = 6144;
	public const int DefaultUndoHistorySize = 1024;

	// Preferences.
	[JsonInclude] public bool ShowOptionsWindow;
	[JsonInclude] public int RecentFilesHistorySize = DefaultRecentFilesHistorySize;
	[JsonInclude] public ChartType DefaultStepsType = DefaultDefaultStepsType;
	[JsonInclude] public ChartDifficultyType DefaultDifficultyType = DefaultDefaultDifficultyType;
	[JsonInclude] public HashSet<ChartType> StartupStepGraphs = [..DefaultStartupStepGraphs];
	[JsonInclude] public bool OpenLastOpenedFileOnLaunch = DefaultOpenLastOpenedFileOnLaunch;
	[JsonInclude] public double NewSongSyncOffset = DefaultNewSongSyncOffset;
	[JsonInclude] public double OpenSongSyncOffset = DefaultOpenSongSyncOffset;
	[JsonInclude] public bool UseCustomDpiScale = DefaultUseCustomDpiScale;
	[JsonInclude] public double DpiScale = DefaultDpiScale;
	[JsonInclude] public bool SuppressExternalSongModificationNotification = DefaultSuppressExternalSongModificationNotification;
	[JsonInclude] public bool SuppressUpdateNotification = DefaultSuppressUpdateNotification;
	[JsonInclude] public bool HideSongBackground = DefaultHideSongBackground;
	[JsonInclude] public StepColorMethod StepColorMethodValue = DefaultStepColorMethodValue;
	[JsonInclude] public Layout ResetLayout = DefaultResetLayout;
	[JsonInclude] public BackgroundImageSizeMode BackgroundImageSize = DefaultBackgroundImageSize;
	[JsonInclude] public bool RenderNotes = DefaultRenderNotes;
	[JsonInclude] public bool RenderMarkers = DefaultRenderMarkers;
	[JsonInclude] public bool RenderRegions = DefaultRenderRegions;
	[JsonInclude] public bool RenderMiscEvents = DefaultRenderMiscEvents;
	[JsonInclude] public int MiscEventAreaWidth = DefaultMiscEventAreaWidth;
	[JsonInclude] public int MaxMarkersToDraw = DefaultMaxMarkersToDraw;
	[JsonInclude] public int MaxEventsToDraw = DefaultMaxEventsToDraw;
	[JsonInclude] public int MaxRateAlteringEventsToProcessPerFrame = DefaultMaxRateAlteringEventsToProcessPerFrame;
	[JsonInclude] public int MiniMapMaxNotesToDraw = DefaultMiniMapMaxNotesToDraw;

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
		factory.RegisterDefault(DefaultBackgroundImageSize);
	}

	public bool IsUsingDefaults()
	{
		// Commented out lines are options which aren't resettable in UI.
		return RecentFilesHistorySize == DefaultRecentFilesHistorySize
		       && DefaultStepsType == DefaultDefaultStepsType
		       && DefaultDifficultyType == DefaultDefaultDifficultyType
		       && StartupStepGraphs.SetEquals(DefaultStartupStepGraphs)
		       && OpenLastOpenedFileOnLaunch == DefaultOpenLastOpenedFileOnLaunch
		       && NewSongSyncOffset.DoubleEquals(DefaultNewSongSyncOffset)
		       && OpenSongSyncOffset.DoubleEquals(DefaultOpenSongSyncOffset)
		       && UseCustomDpiScale == DefaultUseCustomDpiScale
		       && DpiScale.DoubleEquals(DefaultDpiScale)
		       && SuppressExternalSongModificationNotification == DefaultSuppressExternalSongModificationNotification
		       && SuppressUpdateNotification == DefaultSuppressUpdateNotification
		       && HideSongBackground == DefaultHideSongBackground
		       && StepColorMethodValue == DefaultStepColorMethodValue
		       // && ResetLayout == DefaultResetLayout
		       && BackgroundImageSize == DefaultBackgroundImageSize
		       && RenderNotes == DefaultRenderNotes
		       && RenderMarkers == DefaultRenderMarkers
		       && RenderRegions == DefaultRenderRegions
		       && RenderMiscEvents == DefaultRenderMiscEvents
		       && MiscEventAreaWidth == DefaultMiscEventAreaWidth
		       // && MaxMarkersToDraw == DefaultMaxMarkersToDraw
		       // && MaxEventsToDraw == DefaultMaxEventsToDraw
		       // && MaxRateAlteringEventsToProcessPerFrame == DefaultMaxRateAlteringEventsToProcessPerFrame
		       // && MiniMapMaxNotesToDraw == DefaultMiniMapMaxNotesToDraw
		       && UndoHistorySize == DefaultUndoHistorySize;
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
	private readonly bool PreviousUseCustomDpiScale;
	private readonly double PreviousDpiScale;
	private readonly bool PreviousSuppressExternalSongModificationNotification;
	private readonly bool PreviousSuppressUpdateNotification;
	private readonly bool PreviousHideSongBackground;

	private readonly StepColorMethod PreviousStepColorMethodValue;

	// private readonly Layout PreviousResetLayout;
	private readonly BackgroundImageSizeMode PreviousBackgroundImageSize;
	private readonly bool PreviousRenderNotes;
	private readonly bool PreviousRenderMarkers;
	private readonly bool PreviousRenderRegions;
	private readonly bool PreviousRenderMiscEvents;

	private readonly int PreviousMiscEventAreaWidth;

	// private readonly int PreviousMaxMarkersToDraw;
	// private readonly int PreviousMaxEventsToDraw;
	// private readonly int PreviousMaxRateAlteringEventsToProcessPerFrame;
	// private readonly int PreviousMiniMapMaxNotesToDraw;
	private readonly int PreviousUndoHistorySize;

	public ActionRestoreOptionPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesOptions;

		PreviousRecentFilesHistorySize = p.RecentFilesHistorySize;
		PreviousDefaultStepsType = p.DefaultStepsType;
		PreviousDefaultDifficultyType = p.DefaultDifficultyType;
		PreviousStartupStepGraphs = [..p.StartupStepGraphs];
		PreviousOpenLastOpenedFileOnLaunch = p.OpenLastOpenedFileOnLaunch;
		PreviousNewSongSyncOffset = p.NewSongSyncOffset;
		PreviousOpenSongSyncOffset = p.OpenSongSyncOffset;
		PreviousUseCustomDpiScale = p.UseCustomDpiScale;
		PreviousDpiScale = p.DpiScale;
		PreviousSuppressExternalSongModificationNotification = p.SuppressExternalSongModificationNotification;
		PreviousSuppressUpdateNotification = p.SuppressUpdateNotification;
		PreviousHideSongBackground = p.HideSongBackground;
		PreviousStepColorMethodValue = p.StepColorMethodValue;
		// PreviousResetLayout = p.ResetLayout;
		PreviousBackgroundImageSize = p.BackgroundImageSize;
		PreviousRenderNotes = p.RenderNotes;
		PreviousRenderMarkers = p.RenderMarkers;
		PreviousRenderRegions = p.RenderRegions;
		PreviousRenderMiscEvents = p.RenderMiscEvents;
		PreviousMiscEventAreaWidth = p.MiscEventAreaWidth;
		// PreviousMaxMarkersToDraw = p.MaxMarkersToDraw;
		// PreviousMaxEventsToDraw = p.MaxEventsToDraw;
		// PreviousMaxRateAlteringEventsToProcessPerFrame = p.MaxRateAlteringEventsToProcessPerFrame;
		// PreviousMiniMapMaxNotesToDraw = p.MiniMapMaxNotesToDraw;
		PreviousUndoHistorySize = p.UndoHistorySize;
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
		p.StartupStepGraphs = [..DefaultStartupStepGraphs];
		p.OpenLastOpenedFileOnLaunch = DefaultOpenLastOpenedFileOnLaunch;
		p.NewSongSyncOffset = DefaultNewSongSyncOffset;
		p.OpenSongSyncOffset = DefaultOpenSongSyncOffset;
		p.UseCustomDpiScale = DefaultUseCustomDpiScale;
		p.DpiScale = DefaultDpiScale;
		p.SuppressExternalSongModificationNotification = DefaultSuppressExternalSongModificationNotification;
		p.SuppressUpdateNotification = DefaultSuppressUpdateNotification;
		p.HideSongBackground = DefaultHideSongBackground;
		p.StepColorMethodValue = DefaultStepColorMethodValue;
		// p.ResetLayout = DefaultResetLayout;
		p.BackgroundImageSize = DefaultBackgroundImageSize;
		p.RenderNotes = DefaultRenderNotes;
		p.RenderMarkers = DefaultRenderMarkers;
		p.RenderRegions = DefaultRenderRegions;
		p.RenderMiscEvents = DefaultRenderMiscEvents;
		p.MiscEventAreaWidth = DefaultMiscEventAreaWidth;
		// p.MaxMarkersToDraw = DefaultMaxMarkersToDraw;
		// p.MaxEventsToDraw = DefaultMaxEventsToDraw;
		// p.MaxRateAlteringEventsToProcessPerFrame = DefaultMaxRateAlteringEventsToProcessPerFrame;
		// p.MiniMapMaxNotesToDraw = DefaultMiniMapMaxNotesToDraw;
		p.UndoHistorySize = DefaultUndoHistorySize;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesOptions;
		p.RecentFilesHistorySize = PreviousRecentFilesHistorySize;
		p.DefaultStepsType = PreviousDefaultStepsType;
		p.DefaultDifficultyType = PreviousDefaultDifficultyType;
		p.StartupStepGraphs = [..PreviousStartupStepGraphs];
		p.OpenLastOpenedFileOnLaunch = PreviousOpenLastOpenedFileOnLaunch;
		p.NewSongSyncOffset = PreviousNewSongSyncOffset;
		p.OpenSongSyncOffset = PreviousOpenSongSyncOffset;
		p.UseCustomDpiScale = PreviousUseCustomDpiScale;
		p.DpiScale = PreviousDpiScale;
		p.SuppressExternalSongModificationNotification = PreviousSuppressExternalSongModificationNotification;
		p.SuppressUpdateNotification = PreviousSuppressUpdateNotification;
		p.HideSongBackground = PreviousHideSongBackground;
		p.StepColorMethodValue = PreviousStepColorMethodValue;
		// p.ResetLayout = PreviousResetLayout;
		p.BackgroundImageSize = PreviousBackgroundImageSize;
		p.RenderNotes = PreviousRenderNotes;
		p.RenderMarkers = PreviousRenderMarkers;
		p.RenderRegions = PreviousRenderRegions;
		p.RenderMiscEvents = PreviousRenderMiscEvents;
		p.MiscEventAreaWidth = PreviousMiscEventAreaWidth;
		// p.MaxMarkersToDraw = PreviousMaxMarkersToDraw;
		// p.MaxEventsToDraw = PreviousMaxEventsToDraw;
		// p.MaxRateAlteringEventsToProcessPerFrame = PreviousMaxRateAlteringEventsToProcessPerFrame;
		// p.MiniMapMaxNotesToDraw = PreviousMiniMapMaxNotesToDraw;
		p.UndoHistorySize = PreviousUndoHistorySize;
	}
}
