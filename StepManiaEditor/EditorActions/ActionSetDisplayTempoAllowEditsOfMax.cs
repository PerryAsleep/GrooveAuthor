
namespace StepManiaEditor
{
	/// <summary>
	/// EditorAction for changing the ShouldAllowEditsOfMax field of a DisplayTempo.
	/// When disabling ShouldAllowEditsOfMax, the max tempo is forced to be the min.
	/// If they were different before setting ShouldAllowEditsOfMax to true, then undoing
	/// that change should restore the max tempo back to what it was previously.
	/// </summary>
	internal sealed class ActionSetDisplayTempoAllowEditsOfMax : EditorAction
	{
		private readonly DisplayTempo DisplayTempo;
		private readonly double PreviousMax;
		private readonly bool Allow;

		public ActionSetDisplayTempoAllowEditsOfMax(DisplayTempo displayTempo, bool allow)
		{
			DisplayTempo = displayTempo;
			PreviousMax = DisplayTempo.SpecifiedTempoMax;
			Allow = allow;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Set display tempo ShouldAllowEditsOfMax '{!Allow}' > '{Allow}'.";
		}

		public override void Do()
		{
			DisplayTempo.ShouldAllowEditsOfMax = Allow;
			if (!DisplayTempo.ShouldAllowEditsOfMax)
				DisplayTempo.SpecifiedTempoMax = DisplayTempo.SpecifiedTempoMin;
		}

		public override void Undo()
		{
			DisplayTempo.ShouldAllowEditsOfMax = !Allow;
			if (DisplayTempo.ShouldAllowEditsOfMax)
				DisplayTempo.SpecifiedTempoMax = PreviousMax;
		}
	}
}
