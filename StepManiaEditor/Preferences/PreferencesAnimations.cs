
using System.Text.Json.Serialization;

namespace StepManiaEditor
{
	/// <summary>
	/// Preferences for animations.
	/// </summary>
	public class PreferencesAnimations
	{
		// Default values.
		public const bool DefaultAutoPlayHideArrows = true;
		public const bool DefaultAutoPlayLightHolds = true;
		public const bool DefaultAutoPlayRimEffect = true;
		public const bool DefaultAutoPlayGlowEffect = true;
		public const bool DefaultAutoPlayShrinkEffect = true;
		public const bool DefaultTapRimEffect = true;
		public const bool DefaultTapShrinkEffect = true;
		public const bool DefaultPulseReceptorsWithTempo = true;

		// Preferences.
		[JsonInclude] public bool ShowAnimationsPreferencesWindow = false;
		[JsonInclude] public bool AutoPlayHideArrows = DefaultAutoPlayHideArrows;
		[JsonInclude] public bool AutoPlayLightHolds = DefaultAutoPlayLightHolds;
		[JsonInclude] public bool AutoPlayRimEffect = DefaultAutoPlayRimEffect;
		[JsonInclude] public bool AutoPlayGlowEffect = DefaultAutoPlayGlowEffect;
		[JsonInclude] public bool AutoPlayShrinkEffect = DefaultAutoPlayShrinkEffect;
		[JsonInclude] public bool TapRimEffect = DefaultTapRimEffect;
		[JsonInclude] public bool TapShrinkEffect = DefaultTapShrinkEffect;
		[JsonInclude] public bool PulseReceptorsWithTempo = DefaultPulseReceptorsWithTempo;

		public bool IsUsingDefaults()
		{
			return AutoPlayHideArrows == DefaultAutoPlayHideArrows
			       && AutoPlayLightHolds == DefaultAutoPlayLightHolds
			       && AutoPlayRimEffect == DefaultAutoPlayRimEffect
			       && AutoPlayGlowEffect == DefaultAutoPlayGlowEffect
			       && AutoPlayShrinkEffect == DefaultAutoPlayShrinkEffect
			       && TapRimEffect == DefaultTapRimEffect
			       && TapShrinkEffect == DefaultTapShrinkEffect
			       && PulseReceptorsWithTempo == DefaultPulseReceptorsWithTempo;
		}

		public void RestoreDefaults()
		{
			// Don't enqueue an action if it would not have any effect.
			if (IsUsingDefaults())
				return;
			ActionQueue.Instance.Do(new ActionRestoreAnimationsPreferenceDefaults());
		}
	}

	/// <summary>
	/// Action to restore animation preferences to their default values.
	/// </summary>
	public class ActionRestoreAnimationsPreferenceDefaults : EditorAction
	{
		private readonly bool PreviousAutoPlayHideArrows;
		private readonly bool PreviousAutoPlayLightHolds;
		private readonly bool PreviousAutoPlayRimEffect;
		private readonly bool PreviousAutoPlayGlowEffect;
		private readonly bool PreviousAutoPlayShrinkEffect;
		private readonly bool PreviousTapRimEffect;
		private readonly bool PreviousTapShrinkEffect;
		private readonly bool PreviousPulseReceptorsWithTempo;

		public ActionRestoreAnimationsPreferenceDefaults()
		{
			var p = Preferences.Instance.PreferencesAnimations;
			PreviousAutoPlayHideArrows = p.AutoPlayHideArrows;
			PreviousAutoPlayLightHolds = p.AutoPlayLightHolds;
			PreviousAutoPlayRimEffect = p.AutoPlayRimEffect;
			PreviousAutoPlayGlowEffect = p.AutoPlayGlowEffect;
			PreviousAutoPlayShrinkEffect = p.AutoPlayShrinkEffect;
			PreviousTapRimEffect = p.TapRimEffect;
			PreviousTapShrinkEffect = p.TapShrinkEffect;
			PreviousPulseReceptorsWithTempo = p.PulseReceptorsWithTempo;
		}

		public override string ToString()
		{
			return "Restore animation default preferences.";
		}

		public override void Do()
		{
			var p = Preferences.Instance.PreferencesAnimations;

			p.AutoPlayHideArrows = PreferencesAnimations.DefaultAutoPlayHideArrows;
			p.AutoPlayLightHolds = PreferencesAnimations.DefaultAutoPlayLightHolds;
			p.AutoPlayRimEffect = PreferencesAnimations.DefaultAutoPlayRimEffect;
			p.AutoPlayGlowEffect = PreferencesAnimations.DefaultAutoPlayGlowEffect;
			p.AutoPlayShrinkEffect = PreferencesAnimations.DefaultAutoPlayShrinkEffect;
			p.TapRimEffect = PreferencesAnimations.DefaultTapRimEffect;
			p.TapShrinkEffect = PreferencesAnimations.DefaultTapShrinkEffect;
			p.PulseReceptorsWithTempo = PreferencesAnimations.DefaultPulseReceptorsWithTempo;
		}

		public override void Undo()
		{
			var p = Preferences.Instance.PreferencesAnimations;
			p.AutoPlayHideArrows = PreviousAutoPlayHideArrows;
			p.AutoPlayLightHolds = PreviousAutoPlayLightHolds;
			p.AutoPlayRimEffect = PreviousAutoPlayRimEffect;
			p.AutoPlayGlowEffect = PreviousAutoPlayGlowEffect;
			p.AutoPlayShrinkEffect = PreviousAutoPlayShrinkEffect;
			p.TapRimEffect = PreviousTapRimEffect;
			p.TapShrinkEffect = PreviousTapShrinkEffect;
			p.PulseReceptorsWithTempo = PreviousPulseReceptorsWithTempo;
		}
	}
}
