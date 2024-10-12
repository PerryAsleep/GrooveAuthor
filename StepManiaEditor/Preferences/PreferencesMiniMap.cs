using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Fumen;
using static StepManiaEditor.PreferencesMiniMap;

namespace StepManiaEditor;

/// <summary>
/// Preferences for the MiniMap.
/// </summary>
internal sealed class PreferencesMiniMap
{
	public static readonly Editor.SpacingMode[] MiniMapVariableSpacingModes =
		{ Editor.SpacingMode.ConstantTime, Editor.SpacingMode.ConstantRow };

	// Default values.
	public const bool DefaultShowMiniMap = true;
	public const MiniMap.SelectMode DefaultMiniMapSelectMode = MiniMap.SelectMode.MoveToCursor;
	public const uint DefaultMiniMapWidth = 90;
	public const uint DefaultMiniMapNoteWidth = 2;
	public const uint DefaultMiniMapNoteSpacing = 3;
	public const MiniMap.Position DefaultMiniMapPosition = MiniMap.Position.LeftSideOfWindow;
	public const Editor.SpacingMode DefaultMiniMapSpacingModeForVariable = Editor.SpacingMode.ConstantTime;
	public const uint DefaultMiniMapVisibleTimeRange = 240;
	public const uint DefaultMiniMapVisibleRowRange = 24576;
	public const bool DefaultShowPatterns = true;
	public const uint DefaultPatternsWidth = 8;
	public const bool DefaultShowPreview = true;
	public const uint DefaultPreviewWidth = 8;
	public const bool DefaultShowLabels = true;
	public const bool DefaultQuantizePositions = false;

	public static Dictionary<MiniMap.Position, int> DefaultPositionOffsets = new()
	{
		// When mounted to the window the density graph should be on the outside and the mini map should be on the inside.
		// These value takes into account the default position for the density graph.
		{ MiniMap.Position.RightSideOfWindow, 110 },
		{ MiniMap.Position.LeftSideOfWindow, 110 },
		// When mounted to the chart the mini map should be on the inside and the density graph should be on the outside.
		// A little more of a margin is applied when mounting to the chart.
		{ MiniMap.Position.FocusedChartWithoutScaling, 32 },
		{ MiniMap.Position.FocusedChartWithScaling, 32 },
	};

	// Preferences.
	[JsonInclude] public bool ShowMiniMapPreferencesWindow;
	[JsonInclude] public bool ShowMiniMap = DefaultShowMiniMap;
	[JsonInclude] public MiniMap.SelectMode MiniMapSelectMode = DefaultMiniMapSelectMode;
	[JsonInclude] public uint MiniMapWidth = DefaultMiniMapWidth;
	[JsonInclude] public uint MiniMapNoteWidth = DefaultMiniMapNoteWidth;
	[JsonInclude] public uint MiniMapNoteSpacing = DefaultMiniMapNoteSpacing;
	[JsonInclude] public MiniMap.Position MiniMapPosition = DefaultMiniMapPosition;
	[JsonInclude] public Editor.SpacingMode MiniMapSpacingModeForVariable = DefaultMiniMapSpacingModeForVariable;
	[JsonInclude] public uint MiniMapVisibleTimeRange = DefaultMiniMapVisibleTimeRange;
	[JsonInclude] public uint MiniMapVisibleRowRange = DefaultMiniMapVisibleRowRange;
	[JsonInclude] public bool ShowPatterns = DefaultShowPatterns;
	[JsonInclude] public uint PatternsWidth = DefaultPatternsWidth;
	[JsonInclude] public bool ShowPreview = DefaultShowPreview;
	[JsonInclude] public uint PreviewWidth = DefaultPreviewWidth;
	[JsonInclude] public bool ShowLabels = DefaultShowLabels;
	[JsonInclude] public bool QuantizePositions = DefaultQuantizePositions;
	[JsonInclude] public Dictionary<MiniMap.Position, int> PositionOffsets = new();

	[JsonIgnore]
	public int PositionOffset
	{
		get => PositionOffsets[MiniMapPosition];
		set => PositionOffsets[MiniMapPosition] = value;
	}

	public static void RegisterDefaultsForInvalidEnumValues(PermissiveEnumJsonConverterFactory factory)
	{
		factory.RegisterDefault(DefaultMiniMapSelectMode);
		factory.RegisterDefault(DefaultMiniMapPosition);
	}

	public void PostLoad()
	{
		foreach (var position in Enum.GetValues(typeof(MiniMap.Position)).Cast<MiniMap.Position>())
		{
			PositionOffsets.TryAdd(position, DefaultPositionOffsets[position]);
		}
	}

	public bool IsUsingDefaults()
	{
		if (!(ShowMiniMap == DefaultShowMiniMap
		      && MiniMapSelectMode == DefaultMiniMapSelectMode
		      && MiniMapWidth == DefaultMiniMapWidth
		      && MiniMapNoteWidth == DefaultMiniMapNoteWidth
		      && MiniMapNoteSpacing == DefaultMiniMapNoteSpacing
		      && MiniMapPosition == DefaultMiniMapPosition
		      && MiniMapSpacingModeForVariable == DefaultMiniMapSpacingModeForVariable
		      && MiniMapVisibleTimeRange == DefaultMiniMapVisibleTimeRange
		      && MiniMapVisibleRowRange == DefaultMiniMapVisibleRowRange
		      && ShowPatterns == DefaultShowPatterns
		      && PatternsWidth == DefaultPatternsWidth
		      && ShowPreview == DefaultShowPreview
		      && PreviewWidth == DefaultPreviewWidth
		      && ShowLabels == DefaultShowLabels
		      && QuantizePositions == DefaultQuantizePositions))
			return false;

		foreach (var position in Enum.GetValues(typeof(MiniMap.Position)).Cast<MiniMap.Position>())
		{
			if (PositionOffsets[position] != DefaultPositionOffsets[position])
				return false;
		}

		return true;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreMiniMapPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore Mini Map preferences to their default values.
/// </summary>
internal sealed class ActionRestoreMiniMapPreferenceDefaults : EditorAction
{
	private readonly bool PreviousShowMiniMap;
	private readonly MiniMap.SelectMode PreviousMiniMapSelectMode;
	private readonly uint PreviousMiniMapWidth;
	private readonly uint PreviousMiniMapNoteWidth;
	private readonly uint PreviousMiniMapNoteSpacing;
	private readonly MiniMap.Position PreviousMiniMapPosition;
	private readonly Editor.SpacingMode PreviousMiniMapSpacingModeForVariable;
	private readonly uint PreviousMiniMapVisibleTimeRange;
	private readonly uint PreviousMiniMapVisibleRowRange;
	private readonly bool PreviousShowPatterns;
	private readonly uint PreviousPatternsWidth;
	private readonly bool PreviousShowPreview;
	private readonly uint PreviousPreviewWidth;
	private readonly bool PreviousShowLabels;
	private readonly bool PreviousQuantizePositions;
	private readonly Dictionary<MiniMap.Position, int> PreviousPositionOffsets;

	public ActionRestoreMiniMapPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesMiniMap;
		PreviousShowMiniMap = p.ShowMiniMap;
		PreviousMiniMapSelectMode = p.MiniMapSelectMode;
		PreviousMiniMapWidth = p.MiniMapWidth;
		PreviousMiniMapNoteWidth = p.MiniMapNoteWidth;
		PreviousMiniMapNoteSpacing = p.MiniMapNoteSpacing;
		PreviousMiniMapPosition = p.MiniMapPosition;
		PreviousMiniMapSpacingModeForVariable = p.MiniMapSpacingModeForVariable;
		PreviousMiniMapVisibleTimeRange = p.MiniMapVisibleTimeRange;
		PreviousMiniMapVisibleRowRange = p.MiniMapVisibleRowRange;
		PreviousShowPatterns = p.ShowPatterns;
		PreviousPatternsWidth = p.PatternsWidth;
		PreviousShowPreview = p.ShowPreview;
		PreviousPreviewWidth = p.PreviewWidth;
		PreviousShowLabels = p.ShowLabels;
		PreviousQuantizePositions = p.QuantizePositions;

		PreviousPositionOffsets = new Dictionary<MiniMap.Position, int>();
		foreach (var position in Enum.GetValues(typeof(MiniMap.Position)).Cast<MiniMap.Position>())
		{
			PreviousPositionOffsets.TryAdd(position, p.PositionOffsets[position]);
		}
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Mini Map Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesMiniMap;
		p.ShowMiniMap = DefaultShowMiniMap;
		p.MiniMapSelectMode = DefaultMiniMapSelectMode;
		p.MiniMapWidth = DefaultMiniMapWidth;
		p.MiniMapNoteWidth = DefaultMiniMapNoteWidth;
		p.MiniMapNoteSpacing = DefaultMiniMapNoteSpacing;
		p.MiniMapPosition = DefaultMiniMapPosition;
		p.MiniMapSpacingModeForVariable = DefaultMiniMapSpacingModeForVariable;
		p.MiniMapVisibleTimeRange = DefaultMiniMapVisibleTimeRange;
		p.MiniMapVisibleRowRange = DefaultMiniMapVisibleRowRange;
		p.ShowPatterns = DefaultShowPatterns;
		p.PatternsWidth = DefaultPatternsWidth;
		p.ShowPreview = DefaultShowPreview;
		p.PreviewWidth = DefaultPreviewWidth;
		p.ShowLabels = DefaultShowLabels;
		p.QuantizePositions = DefaultQuantizePositions;

		foreach (var position in Enum.GetValues(typeof(MiniMap.Position)).Cast<MiniMap.Position>())
		{
			p.PositionOffsets[position] = DefaultPositionOffsets[position];
		}
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesMiniMap;
		p.ShowMiniMap = PreviousShowMiniMap;
		p.MiniMapSelectMode = PreviousMiniMapSelectMode;
		p.MiniMapWidth = PreviousMiniMapWidth;
		p.MiniMapNoteWidth = PreviousMiniMapNoteWidth;
		p.MiniMapNoteSpacing = PreviousMiniMapNoteSpacing;
		p.MiniMapPosition = PreviousMiniMapPosition;
		p.MiniMapSpacingModeForVariable = PreviousMiniMapSpacingModeForVariable;
		p.MiniMapVisibleTimeRange = PreviousMiniMapVisibleTimeRange;
		p.MiniMapVisibleRowRange = PreviousMiniMapVisibleRowRange;
		p.ShowPatterns = PreviousShowPatterns;
		p.PatternsWidth = PreviousPatternsWidth;
		p.ShowPreview = PreviousShowPreview;
		p.PreviewWidth = PreviousPreviewWidth;
		p.ShowLabels = PreviousShowLabels;
		p.QuantizePositions = PreviousQuantizePositions;

		foreach (var position in Enum.GetValues(typeof(MiniMap.Position)).Cast<MiniMap.Position>())
		{
			p.PositionOffsets[position] = PreviousPositionOffsets[position];
		}
	}
}
