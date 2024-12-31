using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using Fumen;
using static StepManiaEditor.PreferencesDensityGraph;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Preferences for density graph.
/// </summary>
internal sealed class PreferencesDensityGraph : Notifier<PreferencesDensityGraph>
{
	public const string NotificationShowDensityGraphChanged = "ShowDensityGraphChanged";
	public const string NotificationAccumulationTypeChanged = "AccumulationTypeChanged";
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
		LeftSideOfWindow,
		FocusedChartWithoutScaling,
		FocusedChartWithScaling,
		TopOfFocusedChart,
		BottomOfFocusedChart,
	}

	public const bool DefaultShowDensityGraph = true;
	public const bool DefaultShowStream = true;
	public const StepAccumulationType DefaultAccumulationType = StepAccumulationType.Step;
	public static readonly Vector4 DefaultDensityGraphBackgroundColor = new(0.118f, 0.118f, 0.118f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphLowColor = new(0.251f, 0.647f, 0.419f, 1.0f);
	public static readonly Vector4 DefaultDensityGraphHighColor = new(0.705f, 0.282f, 0.282f, 1.0f);
	public const DensityGraphColorMode DefaultDensityGraphColorModeValue = DensityGraphColorMode.ColorByHeight;
	public const DensityGraphPosition DefaultDensityGraphPositionValue = DensityGraphPosition.LeftSideOfWindow;

	public const int DefaultDensityGraphHeight = 90;

	public static Dictionary<DensityGraphPosition, int> DefaultDensityGraphPositionOffsets = new()
	{
		{ DensityGraphPosition.RightSideOfWindow, SceneWidgetPaddingDefaultDPI },
		{ DensityGraphPosition.LeftSideOfWindow, SceneWidgetPaddingDefaultDPI },
		// When mounting to the chart the mini map is closer.
		// These values takes into account the default position of the mini map.
		{
			DensityGraphPosition.FocusedChartWithoutScaling,
			(int)PreferencesMiniMap.DefaultMiniMapWidth + SceneWidgetPaddingDefaultDPI * 2
		},
		{
			DensityGraphPosition.FocusedChartWithScaling,
			(int)PreferencesMiniMap.DefaultMiniMapWidth + SceneWidgetPaddingDefaultDPI * 2
		},
		{ DensityGraphPosition.TopOfFocusedChart, SceneWidgetPaddingDefaultDPI },
		{ DensityGraphPosition.BottomOfFocusedChart, SceneWidgetPaddingDefaultDPI },
	};

	public static Dictionary<DensityGraphPosition, int> DefaultDensityGraphWidthOffsets = new()
	{
		{ DensityGraphPosition.RightSideOfWindow, -SceneWidgetPaddingDefaultDPI },
		{ DensityGraphPosition.LeftSideOfWindow, -SceneWidgetPaddingDefaultDPI },
		{ DensityGraphPosition.FocusedChartWithoutScaling, -SceneWidgetPaddingDefaultDPI },
		{ DensityGraphPosition.FocusedChartWithScaling, -SceneWidgetPaddingDefaultDPI },
		{ DensityGraphPosition.TopOfFocusedChart, 0 },
		{ DensityGraphPosition.BottomOfFocusedChart, 0 },
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
	private StepAccumulationType AccumulationTypeInternal = DefaultAccumulationType;
	private DensityGraphColorMode DensityGraphColorModeValueInternal = DefaultDensityGraphColorModeValue;
	private Vector4 DensityGraphLowColorInternal = DefaultDensityGraphLowColor;
	private Vector4 DensityGraphHighColorInternal = DefaultDensityGraphHighColor;
	private Vector4 DensityGraphBackgroundColorInternal = DefaultDensityGraphBackgroundColor;

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

	public int GetDensityGraphPositionOffsetUiScaled()
	{
		return UiScaled(DensityGraphPositionOffset);
	}

	public int GetDensityGraphWidthOffsetUiScaled()
	{
		return UiScaled(DensityGraphWidthOffset);
	}

	public int GetDensityGraphHeightUiScaled()
	{
		return UiScaled(DensityGraphHeight);
	}

	public static void RegisterDefaultsForInvalidEnumValues(PermissiveEnumJsonConverterFactory factory)
	{
		factory.RegisterDefault(DefaultDensityGraphColorModeValue);
		factory.RegisterDefault(DefaultDensityGraphPositionValue);
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
		      && AccumulationType == DefaultAccumulationType
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
	private readonly StepAccumulationType PreviousAccumulationType;
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
		PreviousAccumulationType = p.AccumulationType;
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
		p.AccumulationType = DefaultAccumulationType;
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
		p.AccumulationType = PreviousAccumulationType;
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
