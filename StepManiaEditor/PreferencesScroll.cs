using System.Text.Json.Serialization;

namespace StepManiaEditor
{
	/// <summary>
	/// Preferences for scrolling.
	/// </summary>
	public class PreferencesScroll
	{
		public const float DefaultVariableSpeedBPM = 120.0f;
		
		// Default values.
		public const bool DefaultShowWaveForm = true;
		public const Editor.ScrollMode DefaultScrollMode = Editor.ScrollMode.Time;
		public const Editor.SpacingMode DefaultSpacingMode = Editor.SpacingMode.ConstantTime;
		public const float DefaultTimeBasedPixelsPerSecond = 300.0f;
		public const float DefaultRowBasedPixelsPerRow = 6.0f;
		public const float DefaultVariablePixelsPerSecondAtDefaultBPM = 300.0f;
		public const Editor.WaveFormScrollMode DefaultRowBasedWaveFormScrollMode = Editor.WaveFormScrollMode.MostCommonTempo;
		public const bool DefaultStopPlaybackWhenScrolling = false;

		// Preferences.
		[JsonInclude] public bool ShowScrollControlPreferencesWindow = true;
		[JsonInclude] public Editor.ScrollMode ScrollMode = DefaultScrollMode;
		[JsonInclude] public Editor.SpacingMode SpacingMode = DefaultSpacingMode;
		[JsonInclude] public float TimeBasedPixelsPerSecond = DefaultTimeBasedPixelsPerSecond;
		[JsonInclude] public float RowBasedPixelsPerRow = DefaultRowBasedPixelsPerRow;
		[JsonInclude] public float VariablePixelsPerSecondAtDefaultBPM = DefaultVariablePixelsPerSecondAtDefaultBPM;
		[JsonInclude] public Editor.WaveFormScrollMode RowBasedWaveFormScrollMode = DefaultRowBasedWaveFormScrollMode;
		[JsonInclude] public bool StopPlaybackWhenScrolling = DefaultStopPlaybackWhenScrolling;

		public bool IsUsingDefaults()
		{
			return ScrollMode == DefaultScrollMode
			       && SpacingMode == DefaultSpacingMode
			       && TimeBasedPixelsPerSecond.FloatEquals(DefaultTimeBasedPixelsPerSecond)
			       && RowBasedPixelsPerRow.FloatEquals(DefaultRowBasedPixelsPerRow)
			       && VariablePixelsPerSecondAtDefaultBPM.FloatEquals(DefaultVariablePixelsPerSecondAtDefaultBPM)
			       && RowBasedWaveFormScrollMode == DefaultRowBasedWaveFormScrollMode
			       && StopPlaybackWhenScrolling == DefaultStopPlaybackWhenScrolling;
	}

		public void RestoreDefaults()
		{
			// Don't enqueue an action if it would not have any effect.
			if (IsUsingDefaults())
				return;
			ActionQueue.Instance.Do(new ActionRestoreScrollPreferenceDefaults());
		}
	}

	/// <summary>
	/// Action to restore WaveForm preferences to their default values.
	/// </summary>
	public class ActionRestoreScrollPreferenceDefaults : EditorAction
	{
		private readonly Editor.ScrollMode PreviousScrollMode;
		private readonly Editor.SpacingMode PreviousSpacingMode;
		private readonly float PreviousTimeBasedPixelsPerSecond;
		private readonly float PreviousRowBasedPixelsPerRow;
		private readonly float PreviousVariablePixelsPerSecondAtDefaultBPM;
		private readonly Editor.WaveFormScrollMode PreviousRowBasedWaveFormScrollMode;
		private readonly bool PreviousStopPlaybackWhenScrolling;

		public ActionRestoreScrollPreferenceDefaults()
		{
			var p = Preferences.Instance.PreferencesScroll;
			PreviousScrollMode = p.ScrollMode;
			PreviousSpacingMode = p.SpacingMode;
			PreviousTimeBasedPixelsPerSecond = p.TimeBasedPixelsPerSecond;
			PreviousRowBasedPixelsPerRow = p.RowBasedPixelsPerRow;
			PreviousVariablePixelsPerSecondAtDefaultBPM = p.VariablePixelsPerSecondAtDefaultBPM;
			PreviousRowBasedWaveFormScrollMode = p.RowBasedWaveFormScrollMode;
			PreviousStopPlaybackWhenScrolling = p.StopPlaybackWhenScrolling;
		}

		public override string ToString()
		{
			return "Restore scroll default preferences.";
		}

		public override void Do()
		{
			var p = Preferences.Instance.PreferencesScroll;
			p.ScrollMode = PreferencesScroll.DefaultScrollMode;
			p.SpacingMode = PreferencesScroll.DefaultSpacingMode;
			p.TimeBasedPixelsPerSecond = PreferencesScroll.DefaultTimeBasedPixelsPerSecond;
			p.RowBasedPixelsPerRow = PreferencesScroll.DefaultRowBasedPixelsPerRow;
			p.VariablePixelsPerSecondAtDefaultBPM = PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM;
			p.RowBasedWaveFormScrollMode = PreferencesScroll.DefaultRowBasedWaveFormScrollMode;
			p.StopPlaybackWhenScrolling = PreferencesScroll.DefaultStopPlaybackWhenScrolling;
		}

		public override void Undo()
		{
			var p = Preferences.Instance.PreferencesScroll;
			p.ScrollMode = PreviousScrollMode;
			p.SpacingMode = PreviousSpacingMode;
			p.TimeBasedPixelsPerSecond = PreviousTimeBasedPixelsPerSecond;
			p.RowBasedPixelsPerRow = PreviousRowBasedPixelsPerRow;
			p.VariablePixelsPerSecondAtDefaultBPM = PreviousVariablePixelsPerSecondAtDefaultBPM;
			p.RowBasedWaveFormScrollMode = PreviousRowBasedWaveFormScrollMode;
			p.StopPlaybackWhenScrolling = PreviousStopPlaybackWhenScrolling;
		}
	}
}
