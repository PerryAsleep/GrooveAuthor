
namespace StepManiaEditor
{
	/// <summary>
	/// EditorAction for changing the ShouldAllowEditsOfMax field of an EditorChart's display tempo.
	/// When disabling ShouldAllowEditsOfMax, the max tempo is forced to be the min.
	/// If they were different before setting ShouldAllowEditsOfMax to true, then undoing
	/// that change should restore the max tempo back to what it was previously.
	/// </summary>
	internal sealed class ActionSetDisplayTempoAllowEditsOfMax : EditorAction
	{
		private readonly EditorChart Chart;
		private readonly double PreviousMax;
		private readonly bool Allow;

		public ActionSetDisplayTempoAllowEditsOfMax(EditorChart chart, bool allow) : base(false, false)
		{
			Chart = chart;
			PreviousMax = Chart.DisplayTempoSpecifiedTempoMax;
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

		protected override void DoImplementation()
		{
			Chart.DisplayTempoShouldAllowEditsOfMax = Allow;
			if (!Chart.DisplayTempoShouldAllowEditsOfMax)
				Chart.DisplayTempoSpecifiedTempoMax = Chart.DisplayTempoSpecifiedTempoMin;
		}

		protected override void UndoImplementation()
		{
			Chart.DisplayTempoShouldAllowEditsOfMax = !Allow;
			if (Chart.DisplayTempoShouldAllowEditsOfMax)
				Chart.DisplayTempoSpecifiedTempoMax = PreviousMax;
		}
	}
}
