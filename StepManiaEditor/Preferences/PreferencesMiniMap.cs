﻿using System.Text.Json.Serialization;

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
	public const uint DefaultMiniMapXPadding = 32;
	public const uint DefaultMiniMapWidth = 90;
	public const uint DefaultMiniMapNoteWidth = 2;
	public const uint DefaultMiniMapNoteSpacing = 3;
	public const MiniMap.Position DefaultMiniMapPosition = MiniMap.Position.RightOfChartArea;
	public const Editor.SpacingMode DefaultMiniMapSpacingModeForVariable = Editor.SpacingMode.ConstantTime;
	public const uint DefaultMiniMapVisibleTimeRange = 240;
	public const uint DefaultMiniMapVisibleRowRange = 24576;
	public const bool DefaultShowPatterns = true;
	public const uint DefaultPatternsWidth = 8;
	public const bool DefaultShowPreview = true;
	public const uint DefaultPreviewWidth = 8;
	public const bool DefaultShowLabels = true;
	public const bool DefaultQuantizePositions = false;

	// Preferences.
	[JsonInclude] public bool ShowMiniMapPreferencesWindow;
	[JsonInclude] public bool ShowMiniMap = DefaultShowMiniMap;
	[JsonInclude] public MiniMap.SelectMode MiniMapSelectMode = DefaultMiniMapSelectMode;
	[JsonInclude] public uint MiniMapXPadding = DefaultMiniMapXPadding;
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

	public bool IsUsingDefaults()
	{
		return ShowMiniMap == DefaultShowMiniMap
		       && MiniMapSelectMode == DefaultMiniMapSelectMode
		       && MiniMapXPadding == DefaultMiniMapXPadding
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
		       && QuantizePositions == DefaultQuantizePositions;
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
	private readonly uint PreviousMiniMapXPadding;
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

	public ActionRestoreMiniMapPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesMiniMap;
		PreviousShowMiniMap = p.ShowMiniMap;
		PreviousMiniMapSelectMode = p.MiniMapSelectMode;
		PreviousMiniMapXPadding = p.MiniMapXPadding;
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
		p.ShowMiniMap = PreferencesMiniMap.DefaultShowMiniMap;
		p.MiniMapSelectMode = PreferencesMiniMap.DefaultMiniMapSelectMode;
		p.MiniMapXPadding = PreferencesMiniMap.DefaultMiniMapXPadding;
		p.MiniMapWidth = PreferencesMiniMap.DefaultMiniMapWidth;
		p.MiniMapNoteWidth = PreferencesMiniMap.DefaultMiniMapNoteWidth;
		p.MiniMapNoteSpacing = PreferencesMiniMap.DefaultMiniMapNoteSpacing;
		p.MiniMapPosition = PreferencesMiniMap.DefaultMiniMapPosition;
		p.MiniMapSpacingModeForVariable = PreferencesMiniMap.DefaultMiniMapSpacingModeForVariable;
		p.MiniMapVisibleTimeRange = PreferencesMiniMap.DefaultMiniMapVisibleTimeRange;
		p.MiniMapVisibleRowRange = PreferencesMiniMap.DefaultMiniMapVisibleRowRange;
		p.ShowPatterns = PreferencesMiniMap.DefaultShowPatterns;
		p.PatternsWidth = PreferencesMiniMap.DefaultPatternsWidth;
		p.ShowPreview = PreferencesMiniMap.DefaultShowPreview;
		p.PreviewWidth = PreferencesMiniMap.DefaultPreviewWidth;
		p.ShowLabels = PreferencesMiniMap.DefaultShowLabels;
		p.QuantizePositions = PreferencesMiniMap.DefaultQuantizePositions;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesMiniMap;
		p.ShowMiniMap = PreviousShowMiniMap;
		p.MiniMapSelectMode = PreviousMiniMapSelectMode;
		p.MiniMapXPadding = PreviousMiniMapXPadding;
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
	}
}
