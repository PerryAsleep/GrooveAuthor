using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using Fumen;
using static StepManiaEditor.PreferencesDensityGraph;

namespace StepManiaEditor;

/// <summary>
/// Preferences for density graph.
/// </summary>
internal sealed class PreferencesDensityGraph : Notifier<PreferencesDensityGraph>
{
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

	public static readonly bool DefaultShowDensityGraph = true;
	public static readonly bool DefaultShowStream = true;
	public static readonly Vector4 DefaultDensityGraphBackgroundColor = new(0.118f, 0.118f, 0.118f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphLowColor = new(0.251f, 0.647f, 0.419f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphHighColor = new(0.705f, 0.282f, 0.282f, 1.0f);
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
	[JsonInclude] public bool ShowDensityGraphPreferencesWindow;

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

	[JsonInclude] public bool ShowStream = DefaultShowStream;
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
		if (!(ShowDensityGraph == DefaultShowDensityGraph
		      && ShowStream == DefaultShowStream
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
		ActionQueue.Instance.Do(new ActionRestoreDensityGraphPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore density graph preferences to their default values.
/// </summary>
internal sealed class ActionRestoreDensityGraphPreferenceDefaults : EditorAction
{
	private readonly bool PreviousShowDensityGraph;
	private readonly bool PreviousShowStream;
	private readonly DensityGraphPosition PreviousDensityGraphPositionValue;
	private readonly int PreviousDensityGraphHeight;
	private readonly DensityGraphColorMode PreviousDensityGraphColorModeValue;
	private readonly Vector4 PreviousDensityGraphLowColor;
	private readonly Vector4 PreviousDensityGraphHighColor;
	private readonly Vector4 PreviousDensityGraphBackgroundColor;
	private readonly Dictionary<DensityGraphPosition, int> PreviousDensityGraphPositionOffsets;
	private readonly Dictionary<DensityGraphPosition, int> PreviousDensityGraphWidthOffsets;

	public ActionRestoreDensityGraphPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesDensityGraph;
		PreviousShowDensityGraph = p.ShowDensityGraph;
		PreviousShowStream = p.ShowStream;
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
		return "Restore Density Graph Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesDensityGraph;
		p.ShowDensityGraph = DefaultShowDensityGraph;
		p.ShowStream = DefaultShowStream;
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
		var p = Preferences.Instance.PreferencesDensityGraph;
		p.ShowDensityGraph = PreviousShowDensityGraph;
		p.ShowStream = PreviousShowStream;
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
