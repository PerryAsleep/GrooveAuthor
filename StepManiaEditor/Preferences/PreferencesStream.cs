using System.Text.Json.Serialization;
using Fumen;
using static StepManiaEditor.PreferencesStream;

namespace StepManiaEditor;

/// <summary>
/// How to count steps.
/// </summary>
public enum StepAccumulationType
{
	/// <summary>
	/// Each individual step is counted once.
	/// </summary>
	Step,

	/// <summary>
	/// Multiple steps on the same row are only counted as one step.
	/// </summary>
	Row,
}

/// <summary>
/// Preferences for stream breakdowns.
/// </summary>
internal sealed class PreferencesStream : Notifier<PreferencesStream>
{
	public const string NotificationNoteTypeChanged = "NoteTypeChanged";
	public const string NotificationAccumulationTypeChanged = "AccumulationTypeChanged";
	public const string NotificationStreamTextParametersChanged = "StreamTextParametersChanged";

	// Default values.
	public const SubdivisionType DefaultNoteType = SubdivisionType.SixteenthNotes;
	public const StepAccumulationType DefaultAccumulationType = StepAccumulationType.Step;
	public const bool DefaultShowBreakLengths = false;
	public const int DefaultMinimumLengthToConsiderStream = 1;
	public const int DefaultShortBreakCutoff = 4;
	public const char DefaultShortBreakCharacter = '-';
	public const char DefaultLongBreakCharacter = '|';

	// Preferences.
	[JsonInclude] public bool ShowStreamPreferencesWindow;

	[JsonInclude]
	public SubdivisionType NoteType
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationNoteTypeChanged, this);
			}
		}
	} = DefaultNoteType;

	[JsonInclude]
	public StepAccumulationType AccumulationType
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationAccumulationTypeChanged, this);
			}
		}
	} = DefaultAccumulationType;

	[JsonInclude]
	public bool ShowBreakLengths
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	} = DefaultShowBreakLengths;

	[JsonInclude]
	public int MinimumLengthToConsiderStream
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	} = DefaultMinimumLengthToConsiderStream;

	[JsonInclude]
	public int ShortBreakCutoff
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	} = DefaultShortBreakCutoff;

	[JsonInclude]
	public char ShortBreakCharacter
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	} = DefaultShortBreakCharacter;

	[JsonInclude]
	public char LongBreakCharacter
	{
		get;
		set
		{
			if (field != value)
			{
				field = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	} = DefaultLongBreakCharacter;

	public static void RegisterDefaultsForInvalidEnumValues(PermissiveEnumJsonConverterFactory factory)
	{
		factory.RegisterDefault(DefaultAccumulationType);
	}

	public bool IsUsingDefaults()
	{
		return NoteType == DefaultNoteType
		       && AccumulationType == DefaultAccumulationType
		       && ShowBreakLengths == DefaultShowBreakLengths
		       && MinimumLengthToConsiderStream == DefaultMinimumLengthToConsiderStream
		       && ShortBreakCutoff == DefaultShortBreakCutoff
		       && ShortBreakCharacter == DefaultShortBreakCharacter
		       && LongBreakCharacter == DefaultLongBreakCharacter;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreStreamPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore stream breakdown preferences to their default values.
/// </summary>
internal sealed class ActionRestoreStreamPreferenceDefaults : UndoableAction
{
	private readonly SubdivisionType PreviousNoteType;
	private readonly StepAccumulationType PreviousAccumulationType;
	private readonly bool PreviousShowBreakLengths;
	private readonly int PreviousMinimumLengthToConsiderStream;
	private readonly int PreviousShortBreakCutoff;
	private readonly char PreviousShortBreakCharacter;
	private readonly char PreviousLongBreakCharacter;

	public ActionRestoreStreamPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesStream;
		PreviousNoteType = p.NoteType;
		PreviousAccumulationType = p.AccumulationType;
		PreviousShowBreakLengths = p.ShowBreakLengths;
		PreviousMinimumLengthToConsiderStream = p.MinimumLengthToConsiderStream;
		PreviousShortBreakCutoff = p.ShortBreakCutoff;
		PreviousShortBreakCharacter = p.ShortBreakCharacter;
		PreviousLongBreakCharacter = p.LongBreakCharacter;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Stream Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesStream;
		p.NoteType = DefaultNoteType;
		p.AccumulationType = DefaultAccumulationType;
		p.ShowBreakLengths = DefaultShowBreakLengths;
		p.MinimumLengthToConsiderStream = DefaultMinimumLengthToConsiderStream;
		p.ShortBreakCutoff = DefaultShortBreakCutoff;
		p.ShortBreakCharacter = DefaultShortBreakCharacter;
		p.LongBreakCharacter = DefaultLongBreakCharacter;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesStream;
		p.NoteType = PreviousNoteType;
		p.AccumulationType = PreviousAccumulationType;
		p.ShowBreakLengths = PreviousShowBreakLengths;
		p.MinimumLengthToConsiderStream = PreviousMinimumLengthToConsiderStream;
		p.ShortBreakCutoff = PreviousShortBreakCutoff;
		p.ShortBreakCharacter = PreviousShortBreakCharacter;
		p.LongBreakCharacter = PreviousLongBreakCharacter;
	}
}
