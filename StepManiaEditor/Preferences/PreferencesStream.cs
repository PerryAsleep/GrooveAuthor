using System;
using System.Collections.Generic;
using System.Linq;
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
	public static readonly Vector4 DefaultDensityGraphLowColor = new(0.306f, 0.788f, 0.506f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphHighColor = new(0.839f, 0.337f, 0.337f, 1.0f);
	public static readonly DensityGraphColorMode DefaultDensityGraphColorModeValue = DensityGraphColorMode.ColorByHeight;
	public static readonly DensityGraphPosition DefaultDensityGraphPositionValue = DensityGraphPosition.RightOfChartArea;

	public static readonly int DefaultDensityGraphHeight = 90;

	public static Dictionary<DensityGraphPosition, int> DefaultDensityGraphPositionOffsets = new()
	{
		{ DensityGraphPosition.RightSideOfWindow, 10 },
		{ DensityGraphPosition.RightOfChartArea, 134 }, // This value takes into account the default position of the mini map.
		{ DensityGraphPosition.MountedToWaveForm, 134 },
		{ DensityGraphPosition.MountedToChart, 134 },
		{ DensityGraphPosition.TopOfWaveForm, 10 },
		{ DensityGraphPosition.BottomOfWaveForm, 81 },
	};

	public static Dictionary<DensityGraphPosition, int> DefaultDensityGraphWidthOffsets = new()
	{
		{ DensityGraphPosition.RightSideOfWindow, -10 },
		{ DensityGraphPosition.RightOfChartArea, -10 },
		{ DensityGraphPosition.MountedToWaveForm, -10 },
		{ DensityGraphPosition.MountedToChart, -10 },
		{ DensityGraphPosition.TopOfWaveForm, 0 },
		{ DensityGraphPosition.BottomOfWaveForm, -112 },
	};

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

	[JsonInclude] public Dictionary<DensityGraphPosition, int> DensityGraphPositionOffsets = new();
	[JsonInclude] public Dictionary<DensityGraphPosition, int> DensityGraphWidthOffsets = new();

	private SubdivisionType NoteTypeInternal = DefaultNoteType;
	private bool ShowDensityGraphInternal = DefaultShowDensityGraph;
	private DensityGraphColorMode DensityGraphColorModeValueInternal = DefaultDensityGraphColorModeValue;
	private Vector4 DensityGraphLowColorInternal = DefaultDensityGraphLowColor;
	private Vector4 DensityGraphHighColorInternal = DefaultDensityGraphHighColor;
	private Vector4 DensityGraphBackgroundColorInternal = DefaultDensityGraphHighColor;

	[JsonIgnore]
	public int DensityGraphPositionOffset
	{
		get => DensityGraphPositionOffsets[DensityGraphPositionValue];
		set => DensityGraphPositionOffsets[DensityGraphPositionValue] = value;
	}

	[JsonIgnore]
	public int DensityGraphWidthOffset
	{
		get => DensityGraphWidthOffsets[DensityGraphPositionValue];
		set => DensityGraphWidthOffsets[DensityGraphPositionValue] = value;
	}

	public void PostLoad()
	{
		foreach (var position in Enum.GetValues(typeof(DensityGraphPosition)).Cast<DensityGraphPosition>())
		{
			DensityGraphPositionOffsets.TryAdd(position, DefaultDensityGraphPositionOffsets[position]);
			DensityGraphWidthOffsets.TryAdd(position, DefaultDensityGraphWidthOffsets[position]);
		}
	}

	public bool IsUsingDefaults()
	{
		if (!(NoteType == DefaultNoteType
		      && ShowBreakLengths == DefaultShowBreakLengths
		      && MinimumLengthToConsiderStream == DefaultMinimumLengthToConsiderStream
		      && ShortBreakCutoff == DefaultShortBreakCutoff
		      && ShortBreakCharacter == DefaultShortBreakCharacter
		      && LongBreakCharacter == DefaultLongBreakCharacter
		      && ShowDensityGraph == DefaultShowDensityGraph
		      && DensityGraphPositionValue == DefaultDensityGraphPositionValue
		      && DensityGraphHeight == DefaultDensityGraphHeight
		      && DensityGraphColorModeValue == DefaultDensityGraphColorModeValue
		      && DensityGraphLowColor == DefaultDensityGraphLowColor
		      && DensityGraphHighColor == DefaultDensityGraphHighColor
		      && DensityGraphBackgroundColor == DefaultDensityGraphBackgroundColor))
			return false;

		foreach (var position in Enum.GetValues(typeof(DensityGraphPosition)).Cast<DensityGraphPosition>())
		{
			if (DensityGraphPositionOffsets[position] != DefaultDensityGraphPositionOffsets[position])
				return false;
			if (DensityGraphWidthOffsets[position] != DefaultDensityGraphWidthOffsets[position])
				return false;
		}

		return true;
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
	private readonly DensityGraphColorMode PreviousDensityGraphColorModeValue;
	private readonly Vector4 PreviousDensityGraphLowColor;
	private readonly Vector4 PreviousDensityGraphHighColor;
	private readonly Vector4 PreviousDensityGraphBackgroundColor;
	private readonly Dictionary<DensityGraphPosition, int> PreviousDensityGraphPositionOffsets;
	private readonly Dictionary<DensityGraphPosition, int> PreviousDensityGraphWidthOffsets;

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
		PreviousDensityGraphColorModeValue = p.DensityGraphColorModeValue;
		PreviousDensityGraphLowColor = p.DensityGraphLowColor;
		PreviousDensityGraphHighColor = p.DensityGraphHighColor;
		PreviousDensityGraphBackgroundColor = p.DensityGraphBackgroundColor;

		PreviousDensityGraphPositionOffsets = new Dictionary<DensityGraphPosition, int>();
		PreviousDensityGraphWidthOffsets = new Dictionary<DensityGraphPosition, int>();
		foreach (var position in Enum.GetValues(typeof(DensityGraphPosition)).Cast<DensityGraphPosition>())
		{
			PreviousDensityGraphPositionOffsets.TryAdd(position, p.DensityGraphPositionOffsets[position]);
			PreviousDensityGraphWidthOffsets.TryAdd(position, p.DensityGraphWidthOffsets[position]);
		}
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
		p.DensityGraphColorModeValue = DefaultDensityGraphColorModeValue;
		p.DensityGraphLowColor = DefaultDensityGraphLowColor;
		p.DensityGraphHighColor = DefaultDensityGraphHighColor;
		p.DensityGraphBackgroundColor = DefaultDensityGraphBackgroundColor;

		foreach (var position in Enum.GetValues(typeof(DensityGraphPosition)).Cast<DensityGraphPosition>())
		{
			p.DensityGraphPositionOffsets[position] = DefaultDensityGraphPositionOffsets[position];
			p.DensityGraphWidthOffsets[position] = DefaultDensityGraphWidthOffsets[position];
		}
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
		p.DensityGraphColorModeValue = PreviousDensityGraphColorModeValue;
		p.DensityGraphLowColor = PreviousDensityGraphLowColor;
		p.DensityGraphHighColor = PreviousDensityGraphHighColor;
		p.DensityGraphBackgroundColor = PreviousDensityGraphBackgroundColor;

		foreach (var position in Enum.GetValues(typeof(DensityGraphPosition)).Cast<DensityGraphPosition>())
		{
			p.DensityGraphPositionOffsets[position] = PreviousDensityGraphPositionOffsets[position];
			p.DensityGraphWidthOffsets[position] = PreviousDensityGraphWidthOffsets[position];
		}
	}
}
