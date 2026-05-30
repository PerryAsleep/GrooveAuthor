using System.Text.Json.Serialization;
using Fumen;

namespace StepManiaEditor;

internal sealed class PreferencesAudio : Notifier<PreferencesAudio>
{
	public const string NotificationMusicRateChanged = "MusicRateChanged";
	public const string NotificationAudioOffsetChanged = "AudioOffsetChanged";
	public const string NotificationMainVolumeChanged = "MainVolumeChanged";
	public const string NotificationMusicVolumeChanged = "MusicVolumeChanged";
	public const string NotificationAssistTickVolumeChanged = "AssistTickVolumeChanged";
	public const string NotificationAssistTickAttackTimeChanged = "AssistTickAttackTimeChanged";
	public const string NotificationUseAssistTickChanged = "UseAssistTickChanged";
	public const string NotificationSkipAssistTickOnBeatTickChanged = "SkipAssistTickOnBeatTickChanged";
	public const string NotificationBeatTickVolumeChanged = "BeatTickVolumeChanged";
	public const string NotificationBeatTickAttackTimeChanged = "BeatTickAttackTimeChanged";
	public const string NotificationUseBeatTickChanged = "UseBeatTickChanged";
	public const string NotificationSkipBeatTickOnAssistTickChanged = "SkipBeatTickOnAssistTickChanged";

	// Default values.
	public const double DefaultAudioOffset = 0.0;
	public const double DefaultMusicRate = 1.0;
	public const float DefaultMainVolume = 1.0f;
	public const float DefaultMusicVolume = 0.5f;
	public const float DefaultAssistTickVolume = 0.7f;
	public const float DefaultAssistTickAttackTime = 0.0f;
	public const bool DefaultUseAssistTick = false;
	public const bool DefaultSkipAssistTickOnBeatTick = false;
	public const float DefaultBeatTickVolume = 0.75f;
	public const float DefaultBeatTickAttackTime = 0.0f;
	public const bool DefaultUseBeatTick = false;
	public const bool DefaultSkipBeatTickOnAssistTick = false;
	public const int DefaultDspBufferSize = 512;
	public const int DefaultDspNumBuffers = 4;
	public const double DefaultPreviewFadeInTime = 0.0;
	public const double DefaultPreviewFadeOutTime = 1.5;

	// Preferences.
	[JsonInclude]
	public double MusicRate
	{
		get;
		set
		{
			if (!field.DoubleEquals(value))
			{
				field = value;
				Notify(NotificationMusicRateChanged, this);
			}
		}
	} = DefaultMusicRate;

	[JsonInclude]
	public double AudioOffset
	{
		get;
		set
		{
			if (!field.DoubleEquals(value))
			{
				field = value;
				Notify(NotificationAudioOffsetChanged, this);
			}
		}
	} = DefaultAudioOffset;

	[JsonInclude]
	public float MainVolume
	{
		get;
		set
		{
			if (!field.FloatEquals(value))
			{
				field = value;
				Notify(NotificationMainVolumeChanged, this);
			}
		}
	} = DefaultMainVolume;

	[JsonInclude]
	public float MusicVolume
	{
		get;
		set
		{
			if (!field.FloatEquals(value))
			{
				field = value;
				Notify(NotificationMusicVolumeChanged, this);
			}
		}
	} = DefaultMusicVolume;

	[JsonInclude]
	public float AssistTickVolume
	{
		get;
		set
		{
			if (!field.FloatEquals(value))
			{
				field = value;
				Notify(NotificationAssistTickVolumeChanged, this);
			}
		}
	} = DefaultAssistTickVolume;

	[JsonInclude]
	public float AssistTickAttackTime
	{
		get;
		set
		{
			if (!field.FloatEquals(value))
			{
				field = value;
				Notify(NotificationAssistTickAttackTimeChanged, this);
			}
		}
	} = DefaultAssistTickAttackTime;

	[JsonInclude]
	public bool UseAssistTick
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationUseAssistTickChanged, this);
			}
		}
	} = DefaultUseAssistTick;

	[JsonInclude]
	public bool SkipAssistTickOnBeatTick
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationSkipAssistTickOnBeatTickChanged, this);
			}
		}
	} = DefaultSkipAssistTickOnBeatTick;

	[JsonInclude]
	public float BeatTickVolume
	{
		get;
		set
		{
			if (!field.FloatEquals(value))
			{
				field = value;
				Notify(NotificationBeatTickVolumeChanged, this);
			}
		}
	} = DefaultBeatTickVolume;

	[JsonInclude]
	public float BeatTickAttackTime
	{
		get;
		set
		{
			if (!field.FloatEquals(value))
			{
				field = value;
				Notify(NotificationBeatTickAttackTimeChanged, this);
			}
		}
	} = DefaultBeatTickAttackTime;

	[JsonInclude]
	public bool UseBeatTick
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationUseBeatTickChanged, this);
			}
		}
	} = DefaultUseBeatTick;

	[JsonInclude]
	public bool SkipBeatTickOnAssistTick
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationSkipBeatTickOnAssistTickChanged, this);
			}
		}
	} = DefaultSkipBeatTickOnAssistTick;

	[JsonInclude] public bool ShowAudioPreferencesWindow;
	[JsonInclude] public int DspBufferSize = DefaultDspBufferSize;
	[JsonInclude] public int DspNumBuffers = DefaultDspNumBuffers;
	[JsonInclude] public double PreviewFadeInTime = DefaultPreviewFadeInTime;
	[JsonInclude] public double PreviewFadeOutTime = DefaultPreviewFadeOutTime;

	public bool IsUsingDefaults()
	{
		return
			AudioOffset.DoubleEquals(DefaultAudioOffset)
			&& MusicRate.DoubleEquals(DefaultMusicRate)
			&& MainVolume.FloatEquals(DefaultMainVolume)
			&& MusicVolume.FloatEquals(DefaultMusicVolume)
			&& AssistTickVolume.FloatEquals(DefaultAssistTickVolume)
			&& AssistTickAttackTime.FloatEquals(DefaultAssistTickAttackTime)
			&& UseAssistTick == DefaultUseAssistTick
			&& SkipAssistTickOnBeatTick == DefaultSkipAssistTickOnBeatTick
			&& BeatTickVolume.FloatEquals(DefaultBeatTickVolume)
			&& BeatTickAttackTime.FloatEquals(DefaultBeatTickAttackTime)
			&& UseBeatTick == DefaultUseBeatTick
			&& SkipBeatTickOnAssistTick == DefaultSkipBeatTickOnAssistTick
			&& DspBufferSize == DefaultDspBufferSize
			&& DspNumBuffers == DefaultDspNumBuffers
			&& PreviewFadeInTime.DoubleEquals(DefaultPreviewFadeInTime)
			&& PreviewFadeOutTime.DoubleEquals(DefaultPreviewFadeOutTime);
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreAudioPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore audio preferences to their default values.
/// </summary>
internal sealed class ActionRestoreAudioPreferenceDefaults : UndoableAction
{
	private readonly double PreviousAudioOffset;
	private readonly double PreviousMusicRate;
	private readonly float PreviousMainVolume;
	private readonly float PreviousMusicVolume;
	private readonly float PreviousAssistTickVolume;
	private readonly float PreviousAssistTickAttackTime;
	private readonly bool PreviousUseAssistTick;
	private readonly bool PreviousSkipAssistTickOnBeatTick;
	private readonly float PreviousBeatTickVolume;
	private readonly float PreviousBeatTickAttackTime;
	private readonly bool PreviousUseBeatTick;
	private readonly bool PreviousSkipBeatTickOnAssistTick;
	private readonly int PreviousDspBufferSize;
	private readonly int PreviousDspNumBuffers;
	private readonly double PreviousPreviewFadeInTime;
	private readonly double PreviousPreviewFadeOutTime;

	public ActionRestoreAudioPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesAudio;
		PreviousAudioOffset = p.AudioOffset;
		PreviousMusicRate = p.MusicRate;
		PreviousMainVolume = p.MainVolume;
		PreviousMusicVolume = p.MusicVolume;
		PreviousAssistTickVolume = p.AssistTickVolume;
		PreviousAssistTickAttackTime = p.AssistTickAttackTime;
		PreviousUseAssistTick = p.UseAssistTick;
		PreviousSkipAssistTickOnBeatTick = p.SkipAssistTickOnBeatTick;
		PreviousBeatTickVolume = p.BeatTickVolume;
		PreviousBeatTickAttackTime = p.BeatTickAttackTime;
		PreviousUseBeatTick = p.UseBeatTick;
		PreviousSkipBeatTickOnAssistTick = p.SkipBeatTickOnAssistTick;
		PreviousDspBufferSize = p.DspBufferSize;
		PreviousDspNumBuffers = p.DspNumBuffers;
		PreviousPreviewFadeInTime = p.PreviewFadeInTime;
		PreviousPreviewFadeOutTime = p.PreviewFadeOutTime;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Audio Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesAudio;
		p.AudioOffset = PreferencesAudio.DefaultAudioOffset;
		p.MusicRate = PreferencesAudio.DefaultMusicRate;
		p.MainVolume = PreferencesAudio.DefaultMainVolume;
		p.MusicVolume = PreferencesAudio.DefaultMusicVolume;
		p.AssistTickVolume = PreferencesAudio.DefaultAssistTickVolume;
		p.AssistTickAttackTime = PreferencesAudio.DefaultAssistTickAttackTime;
		p.UseAssistTick = PreferencesAudio.DefaultUseAssistTick;
		p.SkipAssistTickOnBeatTick = PreferencesAudio.DefaultSkipAssistTickOnBeatTick;
		p.BeatTickVolume = PreferencesAudio.DefaultBeatTickVolume;
		p.BeatTickAttackTime = PreferencesAudio.DefaultBeatTickAttackTime;
		p.UseBeatTick = PreferencesAudio.DefaultUseBeatTick;
		p.SkipBeatTickOnAssistTick = PreferencesAudio.DefaultSkipBeatTickOnAssistTick;
		p.DspBufferSize = PreferencesAudio.DefaultDspBufferSize;
		p.DspNumBuffers = PreferencesAudio.DefaultDspNumBuffers;
		p.PreviewFadeInTime = PreferencesAudio.DefaultPreviewFadeInTime;
		p.PreviewFadeOutTime = PreferencesAudio.DefaultPreviewFadeOutTime;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesAudio;
		p.AudioOffset = PreviousAudioOffset;
		p.MusicRate = PreviousMusicRate;
		p.MainVolume = PreviousMainVolume;
		p.MusicVolume = PreviousMusicVolume;
		p.AssistTickVolume = PreviousAssistTickVolume;
		p.AssistTickAttackTime = PreviousAssistTickAttackTime;
		p.UseAssistTick = PreviousUseAssistTick;
		p.SkipAssistTickOnBeatTick = PreviousSkipAssistTickOnBeatTick;
		p.BeatTickVolume = PreviousBeatTickVolume;
		p.BeatTickAttackTime = PreviousBeatTickAttackTime;
		p.UseBeatTick = PreviousUseBeatTick;
		p.SkipBeatTickOnAssistTick = PreviousSkipBeatTickOnAssistTick;
		p.DspBufferSize = PreviousDspBufferSize;
		p.DspNumBuffers = PreviousDspNumBuffers;
		p.PreviewFadeInTime = PreviousPreviewFadeInTime;
		p.PreviewFadeOutTime = PreviousPreviewFadeOutTime;
	}
}
