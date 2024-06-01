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
	public const string NotificationShowDensityGraphChanged = "ShowDensityGraphChanged";
	public const string NotificationDensityGraphColorModeChanged = "DensityGraphColorModeChanged";
	public const string NotificationDensityGraphColorsChanged = "DensityGraphColorsChanged";

	public enum DensityGraphColorMode
	{
		ColorByDensity,
		ColorByHeight,
	}

	public enum DensityGraphPosition
	{
		RightSideOfWindow,
		RightOfChartArea,
		MountedToWaveForm,
		MountedToChart,

		TopOfWaveForm,
		BottomOfWaveForm,
	}

	// Default values.
	public const SubdivisionType DefaultNoteType = SubdivisionType.SixteenthNotes;
	public const bool DefaultShowBreakLengths = false;
	public const int DefaultMinimumLengthToConsiderStream = 1;
	public const int DefaultShortBreakCutoff = 4;
	public const char DefaultShortBreakCharacter = '-';
	public const char DefaultLongBreakCharacter = '|';

	public static readonly bool DefaultShowDensityGraph = true;
	public static readonly Vector4 DefaultDensityGraphBackgroundColor = new(0.078f, 0.078f, 0.078f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphLowColor = new(0.337f, 0.612f, 0.839f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphHighColor = new(0.306f, 0.788f, 0.690f, 1.0f);
	public static readonly DensityGraphColorMode DefaultDensityGraphColorModeValue = DensityGraphColorMode.ColorByDensity;
	public static readonly DensityGraphPosition DefaultDensityGraphPositionValue = DensityGraphPosition.RightOfChartArea;

	public static readonly int DefaultDensityGraphHeight = 100;

	// This value takes into account the default position of the mini map.
	public static readonly int DefaultDensityGraphPositionOffset = 154;
	public static readonly int DefaultDensityGraphWidthOffset = 0;

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

	[JsonInclude]
	public bool ShowDensityGraph
	{
		get => ShowDensityGraphInternal;
		set
		{
			if (ShowDensityGraphInternal != value)
			{
				ShowDensityGraphInternal = value;
				Notify(NotificationShowDensityGraphChanged, this);
			}
		}
	}

	[JsonInclude] public DensityGraphPosition DensityGraphPositionValue = DefaultDensityGraphPositionValue;
	[JsonInclude] public int DensityGraphHeight = DefaultDensityGraphHeight;
	[JsonInclude] public int DensityGraphPositionOffset = DefaultDensityGraphPositionOffset;
	[JsonInclude] public int DensityGraphWidthOffset = DefaultDensityGraphWidthOffset;

	[JsonInclude]
	public DensityGraphColorMode DensityGraphColorModeValue
	{
		get => DensityGraphColorModeValueInternal;
		set
		{
			if (DensityGraphColorModeValueInternal != value)
			{
				DensityGraphColorModeValueInternal = value;
				Notify(NotificationDensityGraphColorModeChanged, this);
			}
		}
	}

	[JsonInclude]
	public Vector4 DensityGraphLowColor
	{
		get => DensityGraphLowColorInternal;
		set
		{
			if (DensityGraphLowColorInternal != value)
			{
				DensityGraphLowColorInternal = value;
				Notify(NotificationDensityGraphColorsChanged, this);
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
				Notify(NotificationDensityGraphColorsChanged, this);
			}
		}
	}

	[JsonInclude]
	public Vector4 DensityGraphBackgroundColor
	{
		get => DensityGraphBackgroundColorInternal;
		set
		{
			if (DensityGraphBackgroundColorInternal != value)
			{
				DensityGraphBackgroundColorInternal = value;
				Notify(NotificationDensityGraphColorsChanged, this);
			}
		}
	}

	private SubdivisionType NoteTypeInternal = DefaultNoteType;
	private bool ShowDensityGraphInternal = DefaultShowDensityGraph;
	private DensityGraphColorMode DensityGraphColorModeValueInternal = DefaultDensityGraphColorModeValue;
	private Vector4 DensityGraphLowColorInternal = DefaultDensityGraphLowColor;
	private Vector4 DensityGraphHighColorInternal = DefaultDensityGraphHighColor;
	private Vector4 DensityGraphBackgroundColorInternal = DefaultDensityGraphHighColor;

	public bool IsUsingDefaults()
	{
		return NoteType == DefaultNoteType
		       && ShowBreakLengths == DefaultShowBreakLengths
		       && MinimumLengthToConsiderStream == DefaultMinimumLengthToConsiderStream
		       && ShortBreakCutoff == DefaultShortBreakCutoff
		       && ShortBreakCharacter == DefaultShortBreakCharacter
		       && LongBreakCharacter == DefaultLongBreakCharacter
		       && ShowDensityGraph == DefaultShowDensityGraph
		       && DensityGraphPositionValue == DefaultDensityGraphPositionValue
		       && DensityGraphHeight == DefaultDensityGraphHeight
		       && DensityGraphPositionOffset == DefaultDensityGraphPositionOffset
		       && DensityGraphWidthOffset == DefaultDensityGraphWidthOffset
		       && DensityGraphColorModeValue == DefaultDensityGraphColorModeValue
		       && DensityGraphLowColor == DefaultDensityGraphLowColor
		       && DensityGraphHighColor == DefaultDensityGraphHighColor
		       && DensityGraphBackgroundColor == DefaultDensityGraphBackgroundColor;
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
	private readonly DensityGraphPosition PreviousDensityGraphPositionValue;
	private readonly int PreviousDensityGraphHeight;
	private readonly int PreviousDensityGraphPositionOffset;
	private readonly int PreviousDensityGraphWidthOffset;
	private readonly DensityGraphColorMode PreviousDensityGraphColorModeValue;
	private readonly Vector4 PreviousDensityGraphLowColor;
	private readonly Vector4 PreviousDensityGraphHighColor;
	private readonly Vector4 PreviousDensityGraphBackgroundColor;

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
		PreviousDensityGraphPositionValue = p.DensityGraphPositionValue;
		PreviousDensityGraphHeight = p.DensityGraphHeight;
		PreviousDensityGraphPositionOffset = p.DensityGraphPositionOffset;
		PreviousDensityGraphWidthOffset = p.DensityGraphWidthOffset;
		PreviousDensityGraphColorModeValue = p.DensityGraphColorModeValue;
		PreviousDensityGraphLowColor = p.DensityGraphLowColor;
		PreviousDensityGraphHighColor = p.DensityGraphHighColor;
		PreviousDensityGraphBackgroundColor = p.DensityGraphBackgroundColor;
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
		p.DensityGraphPositionValue = DefaultDensityGraphPositionValue;
		p.DensityGraphHeight = DefaultDensityGraphHeight;
		p.DensityGraphPositionOffset = DefaultDensityGraphPositionOffset;
		p.DensityGraphWidthOffset = DefaultDensityGraphWidthOffset;
		p.DensityGraphColorModeValue = DefaultDensityGraphColorModeValue;
		p.DensityGraphLowColor = DefaultDensityGraphLowColor;
		p.DensityGraphHighColor = DefaultDensityGraphHighColor;
		p.DensityGraphBackgroundColor = DefaultDensityGraphBackgroundColor;
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
		p.DensityGraphPositionValue = PreviousDensityGraphPositionValue;
		p.DensityGraphHeight = PreviousDensityGraphHeight;
		p.DensityGraphPositionOffset = PreviousDensityGraphPositionOffset;
		p.DensityGraphWidthOffset = PreviousDensityGraphWidthOffset;
		p.DensityGraphColorModeValue = PreviousDensityGraphColorModeValue;
		p.DensityGraphLowColor = PreviousDensityGraphLowColor;
		p.DensityGraphHighColor = PreviousDensityGraphHighColor;
		p.DensityGraphBackgroundColor = PreviousDensityGraphBackgroundColor;
	}
}
