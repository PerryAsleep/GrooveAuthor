using System.Text.Json.Serialization;

namespace StepManiaEditor
{
	/// <summary>
	/// Preferences for scrolling.
	/// </summary>
	internal sealed class PreferencesSelection
	{
		public enum SelectionMode
		{
			OverlapAny,
			OverlapCenter,
			OverlapAll,
		}

		public enum SelectionRegionMode
		{
			TimeOrPosition,
			TimeOrPositionAndLane,
		}

		// Default values.
		public const SelectionMode DefaultSpacingMode = SelectionMode.OverlapCenter;

	//	public const float DefaultTimeBasedPixelsPerSecond = 300.0f;
	//	public const float DefaultRowBasedPixelsPerRow = 6.0f;
	//	public const float DefaultVariablePixelsPerSecondAtDefaultBPM = 300.0f;
	//	public const Editor.WaveFormScrollMode DefaultRowBasedWaveFormScrollMode = Editor.WaveFormScrollMode.MostCommonTempo;
	//	public const bool DefaultStopPlaybackWhenScrolling = false;

	//	// Preferences.
	//	[JsonInclude] public bool ShowScrollControlPreferencesWindow = true;
	//	[JsonInclude] public Editor.SpacingMode SpacingMode = DefaultSpacingMode;
	//	[JsonInclude] public float TimeBasedPixelsPerSecond = DefaultTimeBasedPixelsPerSecond;
	//	[JsonInclude] public float RowBasedPixelsPerRow = DefaultRowBasedPixelsPerRow;
	//	[JsonInclude] public float VariablePixelsPerSecondAtDefaultBPM = DefaultVariablePixelsPerSecondAtDefaultBPM;
	//	[JsonInclude] public Editor.WaveFormScrollMode RowBasedWaveFormScrollMode = DefaultRowBasedWaveFormScrollMode;
	//	[JsonInclude] public bool StopPlaybackWhenScrolling = DefaultStopPlaybackWhenScrolling;

	//	public bool IsUsingDefaults()
	//	{
	//		return SpacingMode == DefaultSpacingMode
	//			   && TimeBasedPixelsPerSecond.FloatEquals(DefaultTimeBasedPixelsPerSecond)
	//			   && RowBasedPixelsPerRow.FloatEquals(DefaultRowBasedPixelsPerRow)
	//			   && VariablePixelsPerSecondAtDefaultBPM.FloatEquals(DefaultVariablePixelsPerSecondAtDefaultBPM)
	//			   && RowBasedWaveFormScrollMode == DefaultRowBasedWaveFormScrollMode
	//			   && StopPlaybackWhenScrolling == DefaultStopPlaybackWhenScrolling;
	//	}

	//	public void RestoreDefaults()
	//	{
	//		// Don't enqueue an action if it would not have any effect.
	//		if (IsUsingDefaults())
	//			return;
	//		ActionQueue.Instance.Do(new ActionRestoreScrollPreferenceDefaults());
	//	}
	}

	///// <summary>
	///// Action to restore WaveForm preferences to their default values.
	///// </summary>
	//internal sealed class ActionRestoreScrollPreferenceDefaults : EditorAction
	//{
	//	private readonly Editor.SpacingMode PreviousSpacingMode;
	//	private readonly float PreviousTimeBasedPixelsPerSecond;
	//	private readonly float PreviousRowBasedPixelsPerRow;
	//	private readonly float PreviousVariablePixelsPerSecondAtDefaultBPM;
	//	private readonly Editor.WaveFormScrollMode PreviousRowBasedWaveFormScrollMode;
	//	private readonly bool PreviousStopPlaybackWhenScrolling;

	//	public ActionRestoreScrollPreferenceDefaults()
	//	{
	//		var p = Preferences.Instance.PreferencesScroll;
	//		PreviousSpacingMode = p.SpacingMode;
	//		PreviousTimeBasedPixelsPerSecond = p.TimeBasedPixelsPerSecond;
	//		PreviousRowBasedPixelsPerRow = p.RowBasedPixelsPerRow;
	//		PreviousVariablePixelsPerSecondAtDefaultBPM = p.VariablePixelsPerSecondAtDefaultBPM;
	//		PreviousRowBasedWaveFormScrollMode = p.RowBasedWaveFormScrollMode;
	//		PreviousStopPlaybackWhenScrolling = p.StopPlaybackWhenScrolling;
	//	}

	//	public override bool AffectsFile()
	//	{
	//		return false;
	//	}

	//	public override string ToString()
	//	{
	//		return "Restore scroll default preferences.";
	//	}

	//	public override void Do()
	//	{
	//		var p = Preferences.Instance.PreferencesScroll;
	//		p.SpacingMode = PreferencesScroll.DefaultSpacingMode;
	//		p.TimeBasedPixelsPerSecond = PreferencesScroll.DefaultTimeBasedPixelsPerSecond;
	//		p.RowBasedPixelsPerRow = PreferencesScroll.DefaultRowBasedPixelsPerRow;
	//		p.VariablePixelsPerSecondAtDefaultBPM = PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM;
	//		p.RowBasedWaveFormScrollMode = PreferencesScroll.DefaultRowBasedWaveFormScrollMode;
	//		p.StopPlaybackWhenScrolling = PreferencesScroll.DefaultStopPlaybackWhenScrolling;
	//	}

	//	public override void Undo()
	//	{
	//		var p = Preferences.Instance.PreferencesScroll;
	//		p.SpacingMode = PreviousSpacingMode;
	//		p.TimeBasedPixelsPerSecond = PreviousTimeBasedPixelsPerSecond;
	//		p.RowBasedPixelsPerRow = PreviousRowBasedPixelsPerRow;
	//		p.VariablePixelsPerSecondAtDefaultBPM = PreviousVariablePixelsPerSecondAtDefaultBPM;
	//		p.RowBasedWaveFormScrollMode = PreviousRowBasedWaveFormScrollMode;
	//		p.StopPlaybackWhenScrolling = PreviousStopPlaybackWhenScrolling;
	//	}
	//}
}
