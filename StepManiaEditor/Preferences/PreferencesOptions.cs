using System.Linq;
using System.Text.Json.Serialization;
using Fumen;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

internal sealed class PreferencesOptions : Notifier<PreferencesOptions>
{
	public const string NotificationAudioOffsetChanged = "AudioOffsetChanged";
	public const string NotificationVolumeChanged = "VolumeChanged";
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
	public const bool DefaultOpenLastOpenedFileOnLaunch = false;
	public const double DefaultNewSongSyncOffset = 0.009;
	public const double DefaultOpenSongSyncOffset = 0.009;
	public const double DefaultAudioOffset = 0.0;
	public const float DefaultVolume = 1.0f;
	public const int DefaultUndoHistorySize = 1024;

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
	public float Volume
	{
		get => VolumeInternal;
		set
		{
			if (!VolumeInternal.FloatEquals(value))
			{
				VolumeInternal = value;
				Notify(NotificationVolumeChanged, this);
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

	private float VolumeInternal = DefaultVolume;
	private double AudioOffsetInternal = DefaultAudioOffset;
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
		       && Volume.FloatEquals(DefaultVolume)
		       && UndoHistorySize == DefaultUndoHistorySize;
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
	private readonly float PreviousVolume;
	private readonly int PreviousUndoHistorySize;

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
		PreviousVolume = p.Volume;
		PreviousUndoHistorySize = p.UndoHistorySize;
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
		p.Volume = PreferencesOptions.DefaultVolume;
		p.UndoHistorySize = PreferencesOptions.DefaultUndoHistorySize;
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
		p.Volume = PreviousVolume;
		p.UndoHistorySize = PreviousUndoHistorySize;
	}
}
