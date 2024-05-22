using System.Text.Json.Serialization;
using Fumen;
using static StepManiaEditor.PreferencesStream;

namespace StepManiaEditor;

/// <summary>
/// Preferences for stream breakdowns.
/// </summary>
internal sealed class PreferencesStream : Notifier<PreferencesStream>
{
	public const string NotificationNoteTypeChanged = "NoteTypeChanged";

	// Default values.
	public const SubdivisionType DefaultNoteType = SubdivisionType.SixteenthNotes;
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

	[JsonInclude] public bool ShowBreakLengths = DefaultShowBreakLengths;
	[JsonInclude] public int MinimumLengthToConsiderStream = DefaultMinimumLengthToConsiderStream;
	[JsonInclude] public int ShortBreakCutoff = DefaultShortBreakCutoff;
	[JsonInclude] public char ShortBreakCharacter = DefaultShortBreakCharacter;
	[JsonInclude] public char LongBreakCharacter = DefaultLongBreakCharacter;

	private SubdivisionType NoteTypeInternal = DefaultNoteType;

	public bool IsUsingDefaults()
	{
		return NoteType == DefaultNoteType
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
	private readonly bool PreviousShowBreakLengths;
	private readonly int PreviousMinimumLengthToConsiderStream;
	private readonly int PreviousShortBreakCutoff;
	private readonly char PreviousShortBreakCharacter;
	private readonly char PreviousLongBreakCharacter;

	public ActionRestoreStreamPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesStream;
		PreviousNoteType = p.NoteType;
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
		p.ShowBreakLengths = PreviousShowBreakLengths;
		p.MinimumLengthToConsiderStream = PreviousMinimumLengthToConsiderStream;
		p.ShortBreakCutoff = PreviousShortBreakCutoff;
		p.ShortBreakCharacter = PreviousShortBreakCharacter;
		p.LongBreakCharacter = PreviousLongBreakCharacter;
	}
}
