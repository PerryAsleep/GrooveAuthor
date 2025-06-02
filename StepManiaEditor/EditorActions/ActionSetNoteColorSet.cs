using static StepManiaEditor.PreferencesNoteColor;

namespace StepManiaEditor;

/// <summary>
/// Action for setting note colors to a pre-defined set of colors.
/// </summary>
internal sealed class ActionSetNoteColorSet : EditorAction
{
	private readonly NoteColorSet PreviousColors;
	private readonly ColorSet NewColorSet;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="colorSet">ColorSet to apply.</param>
	public ActionSetNoteColorSet(ColorSet colorSet) : base(false, false)
	{
		var p = Preferences.Instance.PreferencesNoteColor;

		PreviousColors = p.GetCurrentNoteColors();
		NewColorSet = colorSet;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Apply {NewColorSet} note colors.";
	}

	protected override void DoImplementation()
	{
		Preferences.Instance.PreferencesNoteColor.ApplyColorSet(NewColorSet);
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesNoteColor;
		p.QuarterColor = PreviousColors.Quarter;
		p.EighthColor = PreviousColors.Eighth;
		p.TwelfthColor = PreviousColors.Twelfth;
		p.SixteenthColor = PreviousColors.Sixteenth;
		p.TwentyForthColor = PreviousColors.TwentyForth;
		p.ThirtySecondColor = PreviousColors.ThirtySecond;
		p.FortyEighthColor = PreviousColors.FortyEighth;
		p.SixtyForthColor = PreviousColors.SixtyForth;
		p.OneHundredNinetySecondColor = PreviousColors.OneHundredNinetySecond;
		p.HoldColor = PreviousColors.Hold;
		p.RollColor = PreviousColors.Roll;
		p.MineColor = PreviousColors.Mine;
	}
}
