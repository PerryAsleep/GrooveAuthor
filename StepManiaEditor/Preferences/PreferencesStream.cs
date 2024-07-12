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
		get => NoteTypeInternal;
		set
		{
			if (NoteTypeInternal != value)
			{
				NoteTypeInternal = value;
				Notify(NotificationNoteTypeChanged, this);
			}
		}
	}

	[JsonInclude]
	public StepAccumulationType AccumulationType
	{
		get => AccumulationTypeInternal;
		set
		{
			if (AccumulationTypeInternal != value)
			{
				AccumulationTypeInternal = value;
				Notify(NotificationAccumulationTypeChanged, this);
			}
		}
	}

	[JsonInclude]
	public bool ShowBreakLengths
	{
		get => ShowBreakLengthsInternal;
		set
		{
			if (ShowBreakLengthsInternal != value)
			{
				ShowBreakLengthsInternal = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	}

	[JsonInclude]
	public int MinimumLengthToConsiderStream
	{
		get => MinimumLengthToConsiderStreamInternal;
		set
		{
			if (MinimumLengthToConsiderStreamInternal != value)
			{
				MinimumLengthToConsiderStreamInternal = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	}

	[JsonInclude]
	public int ShortBreakCutoff
	{
		get => ShortBreakCutoffInternal;
		set
		{
			if (ShortBreakCutoffInternal != value)
			{
				ShortBreakCutoffInternal = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	}

	[JsonInclude]
	public char ShortBreakCharacter
	{
		get => ShortBreakCharacterInternal;
		set
		{
			if (ShortBreakCharacterInternal != value)
			{
				ShortBreakCharacterInternal = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	}

	[JsonInclude]
	public char LongBreakCharacter
	{
		get => LongBreakCharacterInternal;
		set
		{
			if (LongBreakCharacterInternal != value)
			{
				LongBreakCharacterInternal = value;
				Notify(NotificationStreamTextParametersChanged, this);
			}
		}
	}

	private SubdivisionType NoteTypeInternal = DefaultNoteType;
	private StepAccumulationType AccumulationTypeInternal = DefaultAccumulationType;
	private bool ShowBreakLengthsInternal = DefaultShowBreakLengths;
	private int MinimumLengthToConsiderStreamInternal = DefaultMinimumLengthToConsiderStream;
	private int ShortBreakCutoffInternal = DefaultShortBreakCutoff;
	private char ShortBreakCharacterInternal = DefaultShortBreakCharacter;
	private char LongBreakCharacterInternal = DefaultLongBreakCharacter;

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
internal sealed class ActionRestoreStreamPreferenceDefaults : EditorAction
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
