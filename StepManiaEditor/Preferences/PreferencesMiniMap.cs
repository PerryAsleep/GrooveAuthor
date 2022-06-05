
using System.Text.Json.Serialization;

namespace StepManiaEditor
{
	/// <summary>
	/// Preferences for the MiniMap.
	/// </summary>
	public class PreferencesMiniMap
	{
		public static readonly Editor.SpacingMode[] MiniMapVariableSpacingModes = { Editor.SpacingMode.ConstantTime, Editor.SpacingMode.ConstantRow };

		// Default values.
		public const bool DefaultShowMiniMap = true;
		public const MiniMap.SelectMode DefaultMiniMapSelectMode = MiniMap.SelectMode.MoveEditorToCursor;
		public const bool DefaultMiniMapStopPlaybackWhenScrolling = false;
		public const uint DefaultMiniMapWidth = 90;
		public const uint DefaultMiniMapNoteWidth = 2;
		public const uint DefaultMiniMapNoteSpacing = 3;
		public const MiniMap.Position DefaultMiniMapPosition = MiniMap.Position.RightOfChartArea;
		public const Editor.SpacingMode DefaultMiniMapSpacingModeForVariable = Editor.SpacingMode.ConstantTime;
		public const uint DefaultMiniMapVisibleTimeRange = 240;
		public const uint DefaultMiniMapVisibleRowRange = 24576;

		// Preferences.
		[JsonInclude] public bool ShowMiniMapPreferencesWindow = false;
		[JsonInclude] public bool ShowMiniMap = DefaultShowMiniMap;
		[JsonInclude] public MiniMap.SelectMode MiniMapSelectMode = DefaultMiniMapSelectMode;
		[JsonInclude] public bool MiniMapStopPlaybackWhenScrolling = DefaultMiniMapStopPlaybackWhenScrolling;
		[JsonInclude] public uint MiniMapWidth = DefaultMiniMapWidth;
		[JsonInclude] public uint MiniMapNoteWidth = DefaultMiniMapNoteWidth;
		[JsonInclude] public uint MiniMapNoteSpacing = DefaultMiniMapNoteSpacing;
		[JsonInclude] public MiniMap.Position MiniMapPosition = DefaultMiniMapPosition;
		[JsonInclude] public Editor.SpacingMode MiniMapSpacingModeForVariable = DefaultMiniMapSpacingModeForVariable;
		[JsonInclude] public uint MiniMapVisibleTimeRange = DefaultMiniMapVisibleTimeRange;
		[JsonInclude] public uint MiniMapVisibleRowRange = DefaultMiniMapVisibleRowRange;

		public bool IsUsingDefaults()
		{
			return ShowMiniMap == DefaultShowMiniMap
			       && MiniMapSelectMode == DefaultMiniMapSelectMode
			       && MiniMapStopPlaybackWhenScrolling == DefaultMiniMapStopPlaybackWhenScrolling
			       && MiniMapWidth == DefaultMiniMapWidth
			       && MiniMapNoteWidth == DefaultMiniMapNoteWidth
			       && MiniMapNoteSpacing == DefaultMiniMapNoteSpacing
			       && MiniMapPosition == DefaultMiniMapPosition
			       && MiniMapSpacingModeForVariable == DefaultMiniMapSpacingModeForVariable
			       && MiniMapVisibleTimeRange == DefaultMiniMapVisibleTimeRange
			       && MiniMapVisibleRowRange == DefaultMiniMapVisibleRowRange;
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
	public class ActionRestoreMiniMapPreferenceDefaults : EditorAction
	{
		private readonly bool PreviousShowMiniMap;
		private readonly MiniMap.SelectMode PreviousMiniMapSelectMode;
		private readonly bool PreviousMiniMapStopPlaybackWhenScrolling;
		private readonly uint PreviousMiniMapWidth;
		private readonly uint PreviousMiniMapNoteWidth;
		private readonly uint PreviousMiniMapNoteSpacing;
		private readonly MiniMap.Position PreviousMiniMapPosition;
		private readonly Editor.SpacingMode PreviousMiniMapSpacingModeForVariable;
		private readonly uint PreviousMiniMapVisibleTimeRange;
		private readonly uint PreviousMiniMapVisibleRowRange;

		public ActionRestoreMiniMapPreferenceDefaults()
		{
			var p = Preferences.Instance.PreferencesMiniMap;
			PreviousShowMiniMap = p.ShowMiniMap;
			PreviousMiniMapSelectMode = p.MiniMapSelectMode;
			PreviousMiniMapStopPlaybackWhenScrolling = p.MiniMapStopPlaybackWhenScrolling;
			PreviousMiniMapWidth = p.MiniMapWidth;
			PreviousMiniMapNoteWidth = p.MiniMapNoteWidth;
			PreviousMiniMapNoteSpacing = p.MiniMapNoteSpacing;
			PreviousMiniMapPosition = p.MiniMapPosition;
			PreviousMiniMapSpacingModeForVariable = p.MiniMapSpacingModeForVariable;
			PreviousMiniMapVisibleTimeRange = p.MiniMapVisibleTimeRange;
			PreviousMiniMapVisibleRowRange = p.MiniMapVisibleRowRange;
		}

		public override string ToString()
		{
			return "Restore Mini Map default preferences.";
		}

		public override void Do()
		{
			var p = Preferences.Instance.PreferencesMiniMap;
			p.ShowMiniMap = PreferencesMiniMap.DefaultShowMiniMap;
			p.MiniMapSelectMode = PreferencesMiniMap.DefaultMiniMapSelectMode;
			p.MiniMapStopPlaybackWhenScrolling = PreferencesMiniMap.DefaultMiniMapStopPlaybackWhenScrolling;
			p.MiniMapWidth = PreferencesMiniMap.DefaultMiniMapWidth;
			p.MiniMapNoteWidth = PreferencesMiniMap.DefaultMiniMapNoteWidth;
			p.MiniMapNoteSpacing = PreferencesMiniMap.DefaultMiniMapNoteSpacing;
			p.MiniMapPosition = PreferencesMiniMap.DefaultMiniMapPosition;
			p.MiniMapSpacingModeForVariable = PreferencesMiniMap.DefaultMiniMapSpacingModeForVariable;
			p.MiniMapVisibleTimeRange = PreferencesMiniMap.DefaultMiniMapVisibleTimeRange;
			p.MiniMapVisibleRowRange = PreferencesMiniMap.DefaultMiniMapVisibleRowRange;
		}

		public override void Undo()
		{
			var p = Preferences.Instance.PreferencesMiniMap;
			p.ShowMiniMap = PreviousShowMiniMap;
			p.MiniMapSelectMode = PreviousMiniMapSelectMode;
			p.MiniMapStopPlaybackWhenScrolling = PreviousMiniMapStopPlaybackWhenScrolling;
			p.MiniMapWidth = PreviousMiniMapWidth;
			p.MiniMapNoteWidth = PreviousMiniMapNoteWidth;
			p.MiniMapNoteSpacing = PreviousMiniMapNoteSpacing;
			p.MiniMapPosition = PreviousMiniMapPosition;
			p.MiniMapSpacingModeForVariable = PreviousMiniMapSpacingModeForVariable;
			p.MiniMapVisibleTimeRange = PreviousMiniMapVisibleTimeRange;
			p.MiniMapVisibleRowRange = PreviousMiniMapVisibleRowRange;
		}
	}
}
