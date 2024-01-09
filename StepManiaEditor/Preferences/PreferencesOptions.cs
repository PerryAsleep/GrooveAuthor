using System.Linq;
using System.Text.Json.Serialization;
using Fumen;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

internal sealed class PreferencesOptions : Notifier<PreferencesOptions>
{
	public const string NotificationAudioOffsetChanged = "AudioOffsetChanged";
	public const string NotificationMainVolumeChanged = "MainVolumeChanged";
	public const string NotificationMusicVolumeChanged = "MusicVolumeChanged";
	public const string NotificationAssistTickVolumeChanged = "AssistTickVolumeChanged";
	public const string NotificationAssistTickAttackTimeChanged = "AssistTickAttackTimeChanged";
	public const string NotificationUseAssistTickChanged = "UseAssistTickChanged";
	public const string NotificationUndoHistorySizeChanged = "UndoHistorySizeChanged";

	// Default values.
	public const int DefaultRecentFilesHistorySize = 10;
	public const ChartType DefaultDefaultStepsType = ChartType.dance_single;
	public const ChartDifficultyType DefaultDefaultDifficultyType = ChartDifficultyType.Challenge;
	public const double DefaultPreviewFadeInTime = 0.0;
	public const double DefaultPreviewFadeOutTime = 1.5;

	public static readonly ChartType[] DefaultStartupChartTypes =
	{
		ChartType.dance_single,
		ChartType.dance_double,
	};

	public static bool[] DefaultStartupChartTypesBools;
	public const bool DefaultOpenLastOpenedFileOnLaunch = true;
	public const double DefaultNewSongSyncOffset = 0.009;
	public const double DefaultOpenSongSyncOffset = 0.009;
	public const double DefaultAudioOffset = 0.0;
	public const float DefaultMainVolume = 1.0f;
	public const float DefaultMusicVolume = 0.5f;
	public const float DefaultAssistTickVolume = 1.0f;
	public const float DefaultAssistTickAttackTime = 0.0f;
	public const bool DefaultUseAssistTick = false;
	public const int DefaultDspBufferSize = 512;
	public const int DefaultDspNumBuffers = 4;
	public const int DefaultUndoHistorySize = 1024;
	public const bool DefaultUseCustomDpiScale = false;
	public const double DefaultDpiScale = 1.0;
	public const bool DefaultSuppressExternalSongModificationNotification = false;

	// Preferences.
	[JsonInclude] public bool ShowOptionsWindow;
	[JsonInclude] public int RecentFilesHistorySize = DefaultRecentFilesHistorySize;
	[JsonInclude] public ChartType DefaultStepsType = DefaultDefaultStepsType;
	[JsonInclude] public ChartDifficultyType DefaultDifficultyType = DefaultDefaultDifficultyType;
	[JsonInclude] public double PreviewFadeInTime = DefaultPreviewFadeInTime;
	[JsonInclude] public double PreviewFadeOutTime = DefaultPreviewFadeOutTime;
	[JsonInclude] public ChartType[] StartupChartTypes = (ChartType[])DefaultStartupChartTypes.Clone();
	[JsonInclude] public bool OpenLastOpenedFileOnLaunch = DefaultOpenLastOpenedFileOnLaunch;
	[JsonInclude] public double NewSongSyncOffset = DefaultNewSongSyncOffset;
	[JsonInclude] public double OpenSongSyncOffset = DefaultOpenSongSyncOffset;
	[JsonInclude] public bool UseCustomDpiScale = DefaultUseCustomDpiScale;
	[JsonInclude] public double DpiScale = DefaultDpiScale;
	[JsonInclude] public bool SuppressExternalSongModificationNotification;
	[JsonInclude] public int DspBufferSize = DefaultDspBufferSize;
	[JsonInclude] public int DspNumBuffers = DefaultDspNumBuffers;

	[JsonInclude]
	public double AudioOffset
	{
		get => AudioOffsetInternal;
		set
		{
			if (!AudioOffsetInternal.DoubleEquals(value))
			{
				AudioOffsetInternal = value;
				Notify(NotificationAudioOffsetChanged, this);
			}
		}
	}

	[JsonInclude]
	public float MainVolume
	{
		get => MainVolumeInternal;
		set
		{
			if (!MainVolumeInternal.FloatEquals(value))
			{
				MainVolumeInternal = value;
				Notify(NotificationMainVolumeChanged, this);
			}
		}
	}

	[JsonInclude]
	public float MusicVolume
	{
		get => MusicVolumeInternal;
		set
		{
			if (!MusicVolumeInternal.FloatEquals(value))
			{
				MusicVolumeInternal = value;
				Notify(NotificationMusicVolumeChanged, this);
			}
		}
	}

	[JsonInclude]
	public float AssistTickVolume
	{
		get => AssistTickVolumeInternal;
		set
		{
			if (!AssistTickVolumeInternal.FloatEquals(value))
			{
				AssistTickVolumeInternal = value;
				Notify(NotificationAssistTickVolumeChanged, this);
			}
		}
	}

	[JsonInclude]
	public float AssistTickAttackTime
	{
		get => AssistTickAttackTimeInternal;
		set
		{
			if (!AssistTickAttackTimeInternal.FloatEquals(value))
			{
				AssistTickAttackTimeInternal = value;
				Notify(NotificationAssistTickAttackTimeChanged, this);
			}
		}
	}

	[JsonInclude]
	public bool UseAssistTick
	{
		get => UseAssistTickInternal;
		set
		{
			if (UseAssistTickInternal != value)
			{
				UseAssistTickInternal = value;
				Notify(NotificationUseAssistTickChanged, this);
			}
		}
	}

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

	private float MainVolumeInternal = DefaultMainVolume;
	private float MusicVolumeInternal = DefaultMusicVolume;
	private float AssistTickVolumeInternal = DefaultAssistTickVolume;
	private double AudioOffsetInternal = DefaultAudioOffset;
	private float AssistTickAttackTimeInternal = DefaultAssistTickAttackTime;
	private bool UseAssistTickInternal = DefaultUseAssistTick;
	private int UndoHistorySizeInternal = DefaultUndoHistorySize;

	// Strings are serialized, but converted to an array of booleans for UI.
	[JsonIgnore] public bool[] StartupChartTypesBools;

	public bool IsUsingDefaults()
	{
		return RecentFilesHistorySize == DefaultRecentFilesHistorySize
		       && DefaultStepsType == DefaultDefaultStepsType
		       && DefaultDifficultyType == DefaultDefaultDifficultyType
		       && PreviewFadeInTime.DoubleEquals(DefaultPreviewFadeInTime)
		       && PreviewFadeOutTime.DoubleEquals(DefaultPreviewFadeOutTime)
		       && StartupChartTypesBools.SequenceEqual(DefaultStartupChartTypesBools)
		       && OpenLastOpenedFileOnLaunch == DefaultOpenLastOpenedFileOnLaunch
		       && NewSongSyncOffset.DoubleEquals(DefaultNewSongSyncOffset)
		       && OpenSongSyncOffset.DoubleEquals(DefaultOpenSongSyncOffset)
		       && AudioOffset.DoubleEquals(DefaultAudioOffset)
		       && MainVolume.FloatEquals(DefaultMainVolume)
		       && MusicVolume.FloatEquals(DefaultMusicVolume)
		       && AssistTickVolume.FloatEquals(DefaultAssistTickVolume)
		       && AssistTickAttackTime.FloatEquals(DefaultAssistTickAttackTime)
		       && DspBufferSize == DefaultDspBufferSize
		       && DspNumBuffers == DefaultDspNumBuffers
		       && UseAssistTick == DefaultUseAssistTick
		       && UndoHistorySize == DefaultUndoHistorySize
		       && UseCustomDpiScale == DefaultUseCustomDpiScale
		       && DpiScale.DoubleEquals(DefaultDpiScale)
		       && SuppressExternalSongModificationNotification == DefaultSuppressExternalSongModificationNotification;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreOptionPreferenceDefaults());
	}

	public void PostLoad()
	{
		// Set up StartupChartTypesBools from StartupChartTypes.
		StartupChartTypesBools = new bool[Editor.SupportedChartTypes.Length];
		DefaultStartupChartTypesBools = new bool[Editor.SupportedChartTypes.Length];
		foreach (var chartType in StartupChartTypes)
		{
			StartupChartTypesBools[FindSupportedChartTypeIndex(chartType)] = true;
		}

		foreach (var chartType in DefaultStartupChartTypes)
		{
			DefaultStartupChartTypesBools[FindSupportedChartTypeIndex(chartType)] = true;
		}
	}

	public void PreSave()
	{
		// Set up StartupChartTypes from StartupChartTypesBools.
		var count = 0;
		for (var i = 0; i < StartupChartTypesBools.Length; i++)
		{
			if (StartupChartTypesBools[i])
				count++;
		}

		StartupChartTypes = new ChartType[count];
		count = 0;
		for (var i = 0; i < StartupChartTypesBools.Length; i++)
		{
			if (StartupChartTypesBools[i])
			{
				StartupChartTypes[count++] = Editor.SupportedChartTypes[i];
			}
		}
	}

	private int FindSupportedChartTypeIndex(ChartType chartType)
	{
		for (var i = 0; i < Editor.SupportedChartTypes.Length; i++)
		{
			if (Editor.SupportedChartTypes[i] == chartType)
			{
				return i;
			}
		}

		return 0;
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
	private readonly double PreviousPreviewFadeInTime;
	private readonly double PreviousPreviewFadeOutTime;
	private readonly bool[] PreviousStartupChartTypesBools;
	private readonly bool PreviousOpenLastOpenedFileOnLaunch;
	private readonly double PreviousNewSongSyncOffset;
	private readonly double PreviousOpenSongSyncOffset;
	private readonly double PreviousAudioOffset;
	private readonly float PreviousMainVolume;
	private readonly float PreviousMusicVolume;
	private readonly float PreviousAssistTickVolume;
	private readonly float PreviousAssistTickAttackTime;
	private readonly bool PreviousUseAssistTick;
	private readonly int PreviousDspBufferSize;
	private readonly int PreviousDspNumBuffers;
	private readonly int PreviousUndoHistorySize;
	private readonly bool PreviousUseCustomDpiScale;
	private readonly double PreviousDpiScale;
	private readonly bool PreviousSuppressExternalSongModificationNotification;

	public ActionRestoreOptionPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesOptions;

		PreviousRecentFilesHistorySize = p.RecentFilesHistorySize;
		PreviousDefaultStepsType = p.DefaultStepsType;
		PreviousDefaultDifficultyType = p.DefaultDifficultyType;
		PreviousPreviewFadeInTime = p.PreviewFadeInTime;
		PreviousPreviewFadeOutTime = p.PreviewFadeOutTime;
		PreviousStartupChartTypesBools = (bool[])p.StartupChartTypesBools.Clone();
		PreviousOpenLastOpenedFileOnLaunch = p.OpenLastOpenedFileOnLaunch;
		PreviousNewSongSyncOffset = p.NewSongSyncOffset;
		PreviousOpenSongSyncOffset = p.OpenSongSyncOffset;
		PreviousAudioOffset = p.AudioOffset;
		PreviousMainVolume = p.MainVolume;
		PreviousMusicVolume = p.MusicVolume;
		PreviousAssistTickVolume = p.AssistTickVolume;
		PreviousAssistTickAttackTime = p.AssistTickAttackTime;
		PreviousUseAssistTick = p.UseAssistTick;
		PreviousDspBufferSize = p.DspBufferSize;
		PreviousDspNumBuffers = p.DspNumBuffers;
		PreviousUndoHistorySize = p.UndoHistorySize;
		PreviousUseCustomDpiScale = p.UseCustomDpiScale;
		PreviousDpiScale = p.DpiScale;
		PreviousSuppressExternalSongModificationNotification = p.SuppressExternalSongModificationNotification;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore option default preferences.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesOptions;
		p.RecentFilesHistorySize = PreferencesOptions.DefaultRecentFilesHistorySize;
		p.DefaultStepsType = PreferencesOptions.DefaultDefaultStepsType;
		p.DefaultDifficultyType = PreferencesOptions.DefaultDefaultDifficultyType;
		p.PreviewFadeInTime = PreferencesOptions.DefaultPreviewFadeInTime;
		p.PreviewFadeOutTime = PreferencesOptions.DefaultPreviewFadeOutTime;
		p.StartupChartTypesBools = (bool[])PreferencesOptions.DefaultStartupChartTypesBools.Clone();
		p.OpenLastOpenedFileOnLaunch = PreferencesOptions.DefaultOpenLastOpenedFileOnLaunch;
		p.NewSongSyncOffset = PreferencesOptions.DefaultNewSongSyncOffset;
		p.OpenSongSyncOffset = PreferencesOptions.DefaultOpenSongSyncOffset;
		p.AudioOffset = PreferencesOptions.DefaultAudioOffset;
		p.MainVolume = PreferencesOptions.DefaultMainVolume;
		p.MusicVolume = PreferencesOptions.DefaultMusicVolume;
		p.AssistTickVolume = PreferencesOptions.DefaultAssistTickVolume;
		p.AssistTickAttackTime = PreferencesOptions.DefaultAssistTickAttackTime;
		p.UseAssistTick = PreferencesOptions.DefaultUseAssistTick;
		p.DspBufferSize = PreferencesOptions.DefaultDspBufferSize;
		p.DspNumBuffers = PreferencesOptions.DefaultDspNumBuffers;
		p.UndoHistorySize = PreferencesOptions.DefaultUndoHistorySize;
		p.UseCustomDpiScale = PreferencesOptions.DefaultUseCustomDpiScale;
		p.DpiScale = PreferencesOptions.DefaultDpiScale;
		p.SuppressExternalSongModificationNotification = PreferencesOptions.DefaultSuppressExternalSongModificationNotification;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesOptions;
		p.RecentFilesHistorySize = PreviousRecentFilesHistorySize;
		p.DefaultStepsType = PreviousDefaultStepsType;
		p.DefaultDifficultyType = PreviousDefaultDifficultyType;
		p.PreviewFadeInTime = PreviousPreviewFadeInTime;
		p.PreviewFadeOutTime = PreviousPreviewFadeOutTime;
		p.StartupChartTypesBools = (bool[])PreviousStartupChartTypesBools.Clone();
		p.OpenLastOpenedFileOnLaunch = PreviousOpenLastOpenedFileOnLaunch;
		p.NewSongSyncOffset = PreviousNewSongSyncOffset;
		p.OpenSongSyncOffset = PreviousOpenSongSyncOffset;
		p.AudioOffset = PreviousAudioOffset;
		p.MainVolume = PreviousMainVolume;
		p.MusicVolume = PreviousMusicVolume;
		p.AssistTickVolume = PreviousAssistTickVolume;
		p.AssistTickAttackTime = PreviousAssistTickAttackTime;
		p.UseAssistTick = PreviousUseAssistTick;
		p.DspBufferSize = PreviousDspBufferSize;
		p.DspNumBuffers = PreviousDspNumBuffers;
		p.UndoHistorySize = PreviousUndoHistorySize;
		p.UseCustomDpiScale = PreviousUseCustomDpiScale;
		p.DpiScale = PreviousDpiScale;
		p.SuppressExternalSongModificationNotification = PreviousSuppressExternalSongModificationNotification;
	}
}
