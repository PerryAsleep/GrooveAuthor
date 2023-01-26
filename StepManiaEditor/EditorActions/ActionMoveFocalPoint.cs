
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

		public ActionMoveFocalPoint(int previousX, int previousY, int newX, int newY)
		{
			PreviousX = previousX;
			PreviousY = previousY;
			NewX = newX;
			NewY = newY;
		}

		public override bool AffectsFile()
		{
			return false;
		}

		public override string ToString()
		{
			return $"Move receptors from ({PreviousX}, {PreviousY}) to ({NewX}, {NewY}).";
		}

		public override void Do()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = NewX;
			Preferences.Instance.PreferencesReceptors.PositionY = NewY;
		}

		public override void Undo()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = PreviousX;
			Preferences.Instance.PreferencesReceptors.PositionY = PreviousY;
		}
	}
}
