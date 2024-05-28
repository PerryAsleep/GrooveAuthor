using System.Numerics;
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
	public const string NotificationDensityColorModeChanged = "DensityColorModeChanged";
	public const string NotificationDensityColorsChanged = "DensityColorsChanged";

	public enum DensityColorMode
	{
		ColorByDensity,
		ColorByHeight,
	}

	// Default values.
	public const SubdivisionType DefaultNoteType = SubdivisionType.SixteenthNotes;
	public const bool DefaultShowBreakLengths = false;
	public const int DefaultMinimumLengthToConsiderStream = 1;
	public const int DefaultShortBreakCutoff = 4;
	public const char DefaultShortBreakCharacter = '-';
	public const char DefaultLongBreakCharacter = '|';

	public static readonly bool DefaultShowDensityGraph = true;
	public static readonly Vector4 DefaultDensityGraphLowColor = new(0.337f, 0.612f, 0.839f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphHighColor = new(0.306f, 0.788f, 0.690f, 1.0f);
	public static readonly DensityColorMode DefaultDensityGraphColorMode = DensityColorMode.ColorByDensity;

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
	[JsonInclude] public bool ShowDensityGraph = DefaultShowDensityGraph;

	[JsonInclude]
	public Vector4 DensityGraphLowColor
	{
		get => DensityGraphLowColorInternal;
		set
		{
			if (DensityGraphLowColorInternal != value)
			{
				DensityGraphLowColorInternal = value;
				Notify(NotificationDensityColorsChanged, this);
			}
		}
	}

	[JsonInclude]
	public Vector4 DensityGraphHighColor
	{
		get => DensityGraphHighColorInternal;
		set
		{
			if (DensityGraphHighColorInternal != value)
			{
				DensityGraphHighColorInternal = value;
				Notify(NotificationDensityColorsChanged, this);
			}
		}
	}

	[JsonInclude]
	public DensityColorMode DensityGraphColorMode
	{
		get => DensityGraphColorModeInternal;
		set
		{
			if (DensityGraphColorModeInternal != value)
			{
				DensityGraphColorModeInternal = value;
				Notify(NotificationDensityColorModeChanged, this);
			}
		}
	}

	private SubdivisionType NoteTypeInternal = DefaultNoteType;
	private DensityColorMode DensityGraphColorModeInternal = DefaultDensityGraphColorMode;
	private Vector4 DensityGraphLowColorInternal = DefaultDensityGraphLowColor;
	private Vector4 DensityGraphHighColorInternal = DefaultDensityGraphHighColor;

	public bool IsUsingDefaults()
	{
		return NoteType == DefaultNoteType
		       && ShowBreakLengths == DefaultShowBreakLengths
		       && MinimumLengthToConsiderStream == DefaultMinimumLengthToConsiderStream
		       && ShortBreakCutoff == DefaultShortBreakCutoff
		       && ShortBreakCharacter == DefaultShortBreakCharacter
		       && LongBreakCharacter == DefaultLongBreakCharacter
		       && ShowDensityGraph == DefaultShowDensityGraph
		       && DensityGraphLowColor == DefaultDensityGraphLowColor
		       && DensityGraphHighColor == DefaultDensityGraphHighColor
		       && DensityGraphColorMode == DefaultDensityGraphColorMode;
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
	private readonly bool PreviousShowDensityGraph;
	private readonly Vector4 PreviousDensityGraphLowColor;
	private readonly Vector4 PreviousDensityGraphHighColor;
	private readonly DensityColorMode PreviousDensityGraphColorMode;

	public ActionRestoreStreamPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesStream;
		PreviousNoteType = p.NoteType;
		PreviousShowBreakLengths = p.ShowBreakLengths;
		PreviousMinimumLengthToConsiderStream = p.MinimumLengthToConsiderStream;
		PreviousShortBreakCutoff = p.ShortBreakCutoff;
		PreviousShortBreakCharacter = p.ShortBreakCharacter;
		PreviousLongBreakCharacter = p.LongBreakCharacter;
		PreviousShowDensityGraph = p.ShowDensityGraph;
		PreviousDensityGraphLowColor = p.DensityGraphLowColor;
		PreviousDensityGraphHighColor = p.DensityGraphHighColor;
		PreviousDensityGraphColorMode = p.DensityGraphColorMode;
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
		p.ShowDensityGraph = DefaultShowDensityGraph;
		p.DensityGraphLowColor = DefaultDensityGraphLowColor;
		p.DensityGraphHighColor = DefaultDensityGraphHighColor;
		p.DensityGraphColorMode = DefaultDensityGraphColorMode;
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
		p.ShowDensityGraph = PreviousShowDensityGraph;
		p.DensityGraphLowColor = PreviousDensityGraphLowColor;
		p.DensityGraphHighColor = PreviousDensityGraphHighColor;
		p.DensityGraphColorMode = PreviousDensityGraphColorMode;
	}
}
