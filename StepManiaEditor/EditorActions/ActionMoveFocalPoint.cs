
namespace StepManiaEditor
{
	/// <summary>
	/// Action to move the focal point of the receptors to a new location.
	/// </summary>
	internal sealed class ActionMoveFocalPoint : EditorAction
	{
		private int PreviousX;
		private int PreviousY;
		private int NewX;
		private int NewY;

		public ActionMoveFocalPoint(int previousX, int previousY, int newX, int newY) : base(false, false)
		{
			PreviousX = previousX;
			PreviousY = previousY;
			NewX = newX;
			NewY = newY;
		}

		public override string ToString()
		{
			return $"Move receptors from ({PreviousX}, {PreviousY}) to ({NewX}, {NewY}).";
		}

		public override bool AffectsFile()
		{
			return false;
		}

		protected override void DoImplementation()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = NewX;
			Preferences.Instance.PreferencesReceptors.PositionY = NewY;
		}

		protected override void UndoImplementation()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = PreviousX;
			Preferences.Instance.PreferencesReceptors.PositionY = PreviousY;
		}
	}
}
