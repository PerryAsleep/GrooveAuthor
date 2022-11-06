
using System.Text.Json.Serialization;
using System.Numerics;

namespace StepManiaEditor
{
	/// <summary>
	/// Preferences for receptors.
	/// </summary>
	public class PreferencesReceptors
	{
		private Editor Editor;

		// Default values.
		public const bool DefaultAutoPlayHideArrows = true;
		public const bool DefaultAutoPlayLightHolds = true;
		public const bool DefaultAutoPlayRimEffect = true;
		public const bool DefaultAutoPlayGlowEffect = true;
		public const bool DefaultAutoPlayShrinkEffect = true;
		public const bool DefaultTapRimEffect = true;
		public const bool DefaultTapShrinkEffect = true;
		public const bool DefaultPulseReceptorsWithTempo = true;
		public const bool DefaultCenterHorizontally = true;
		public const int DefaultPositionX = 960;
		public const int DefaultPositionY = 100;

		// Preferences.
		[JsonInclude] public bool ShowReceptorPreferencesWindow = false;
		[JsonInclude] public bool AutoPlayHideArrows = DefaultAutoPlayHideArrows;
		[JsonInclude] public bool AutoPlayLightHolds = DefaultAutoPlayLightHolds;
		[JsonInclude] public bool AutoPlayRimEffect = DefaultAutoPlayRimEffect;
		[JsonInclude] public bool AutoPlayGlowEffect = DefaultAutoPlayGlowEffect;
		[JsonInclude] public bool AutoPlayShrinkEffect = DefaultAutoPlayShrinkEffect;
		[JsonInclude] public bool TapRimEffect = DefaultTapRimEffect;
		[JsonInclude] public bool TapShrinkEffect = DefaultTapShrinkEffect;
		[JsonInclude] public bool PulseReceptorsWithTempo = DefaultPulseReceptorsWithTempo;
		[JsonInclude] public bool CenterHorizontally = DefaultCenterHorizontally;
		
		[JsonInclude] public int PositionX
		{
			get
			{
				return PositionXInternal;
			}
			set
			{
				PositionXInternal = value;
				if (PositionXInternal < 0)
					PositionXInternal = 0;
				if (Editor != null)
				{
					if (PositionXInternal >= Editor.GetViewportWidth())
						PositionXInternal = Editor.GetViewportWidth() - 1;
				}
			}
		}
		[JsonInclude] public int PositionY
		{
			get
			{
				return PositionYInternal;
			}
			set
			{
				PositionYInternal = value;
				if (PositionYInternal < 0)
					PositionYInternal = 0;
				if (Editor != null)
				{
					if (PositionYInternal >= Editor.GetViewportHeight())
						PositionYInternal = Editor.GetViewportHeight() - 1;
				}
			}
		}

		private int PositionXInternal = DefaultPositionX;
		private int PositionYInternal = DefaultPositionY;

		public void SetEditor(Editor editor)
		{
			Editor = editor;
		}

		public void ClampViewportPositions()
		{
			PositionX = PositionX;
			PositionY = PositionY;
		}

		public bool IsUsingDefaults()
		{
			return AutoPlayHideArrows == DefaultAutoPlayHideArrows
				   && AutoPlayLightHolds == DefaultAutoPlayLightHolds
				   && AutoPlayRimEffect == DefaultAutoPlayRimEffect
				   && AutoPlayGlowEffect == DefaultAutoPlayGlowEffect
				   && AutoPlayShrinkEffect == DefaultAutoPlayShrinkEffect
				   && TapRimEffect == DefaultTapRimEffect
				   && TapShrinkEffect == DefaultTapShrinkEffect
				   && PulseReceptorsWithTempo == DefaultPulseReceptorsWithTempo
				   && CenterHorizontally == DefaultCenterHorizontally
				   && PositionX == DefaultPositionX
				   && PositionY == DefaultPositionY;
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
		private readonly bool PreviousCenterHorizontally;
		private readonly int PreviousPositionX;
		private readonly int PreviousPositionY;

		public ActionRestoreAnimationsPreferenceDefaults()
		{
			var p = Preferences.Instance.PreferencesReceptors;
			PreviousAutoPlayHideArrows = p.AutoPlayHideArrows;
			PreviousAutoPlayLightHolds = p.AutoPlayLightHolds;
			PreviousAutoPlayRimEffect = p.AutoPlayRimEffect;
			PreviousAutoPlayGlowEffect = p.AutoPlayGlowEffect;
			PreviousAutoPlayShrinkEffect = p.AutoPlayShrinkEffect;
			PreviousTapRimEffect = p.TapRimEffect;
			PreviousTapShrinkEffect = p.TapShrinkEffect;
			PreviousPulseReceptorsWithTempo = p.PulseReceptorsWithTempo;
			PreviousCenterHorizontally = p.CenterHorizontally;
			PreviousPositionX = p.PositionX;
			PreviousPositionY = p.PositionY;
		}

		public override string ToString()
		{
			return "Restore animation default preferences.";
		}

		public override void Do()
		{
			var p = Preferences.Instance.PreferencesReceptors;

			p.AutoPlayHideArrows = PreferencesReceptors.DefaultAutoPlayHideArrows;
			p.AutoPlayLightHolds = PreferencesReceptors.DefaultAutoPlayLightHolds;
			p.AutoPlayRimEffect = PreferencesReceptors.DefaultAutoPlayRimEffect;
			p.AutoPlayGlowEffect = PreferencesReceptors.DefaultAutoPlayGlowEffect;
			p.AutoPlayShrinkEffect = PreferencesReceptors.DefaultAutoPlayShrinkEffect;
			p.TapRimEffect = PreferencesReceptors.DefaultTapRimEffect;
			p.TapShrinkEffect = PreferencesReceptors.DefaultTapShrinkEffect;
			p.PulseReceptorsWithTempo = PreferencesReceptors.DefaultPulseReceptorsWithTempo;
			p.CenterHorizontally = PreferencesReceptors.DefaultCenterHorizontally;
			p.PositionX = PreferencesReceptors.DefaultPositionX;
			p.PositionY = PreferencesReceptors.DefaultPositionY;
		}

		public override void Undo()
		{
			var p = Preferences.Instance.PreferencesReceptors;
			p.AutoPlayHideArrows = PreviousAutoPlayHideArrows;
			p.AutoPlayLightHolds = PreviousAutoPlayLightHolds;
			p.AutoPlayRimEffect = PreviousAutoPlayRimEffect;
			p.AutoPlayGlowEffect = PreviousAutoPlayGlowEffect;
			p.AutoPlayShrinkEffect = PreviousAutoPlayShrinkEffect;
			p.TapRimEffect = PreviousTapRimEffect;
			p.TapShrinkEffect = PreviousTapShrinkEffect;
			p.PulseReceptorsWithTempo = PreviousPulseReceptorsWithTempo;
			p.CenterHorizontally = PreviousCenterHorizontally;
			p.PositionX = PreviousPositionX;
			p.PositionY = PreviousPositionY;
		}
	}
}
