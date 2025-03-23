using Fumen;

namespace StepManiaEditor;

/// <summary>
/// EditorAction for an EditorChart's DisplayTempo's DisplayTempoMode.
/// When changing mode we may want to alter other properties of the DisplayTempo, like it's
/// specified values.
/// </summary>
internal sealed class ActionSetDisplayTempoMode : EditorAction
{
	private readonly EditorChart Chart;
	private readonly DisplayTempoMode Mode;
	private readonly DisplayTempoMode PreviousMode;
	private readonly double PreviousSpecifiedMin;
	private readonly double PreviousSpecifiedMax;
	private readonly bool PreviousShouldAllowEditsOfMax;

	public ActionSetDisplayTempoMode(EditorChart chart, DisplayTempoMode mode) : base(false, false)
	{
		Chart = chart;
		Mode = mode;
		PreviousMode = Chart.DisplayTempoMode;
		PreviousSpecifiedMin = Chart.DisplayTempoSpecifiedTempoMin;
		PreviousSpecifiedMax = Chart.DisplayTempoSpecifiedTempoMax;
		PreviousShouldAllowEditsOfMax = Chart.DisplayTempoShouldAllowEditsOfMax;
	}

	public override bool AffectsFile()
	{
		return true;
	}

	public override string ToString()
	{
		return $"Set display tempo mode to {Mode}.";
	}

	protected override void DoImplementation()
	{
		Chart.DisplayTempoMode = Mode;

		// When changing to a specified mode, use the current actual min and max tempo.
		if (Mode == DisplayTempoMode.Specified && PreviousMode != DisplayTempoMode.Specified)
		{
			Chart.DisplayTempoSpecifiedTempoMin = Chart.GetMinTempo();
			Chart.DisplayTempoSpecifiedTempoMax = Chart.GetMaxTempo();
			if (!Chart.DisplayTempoSpecifiedTempoMin.DoubleEquals(Chart.DisplayTempoSpecifiedTempoMax))
				Chart.DisplayTempoShouldAllowEditsOfMax = true;
		}
	}

	protected override void UndoImplementation()
	{
		Chart.DisplayTempoMode = PreviousMode;
		Chart.DisplayTempoSpecifiedTempoMin = PreviousSpecifiedMin;
		Chart.DisplayTempoSpecifiedTempoMax = PreviousSpecifiedMax;
		Chart.DisplayTempoShouldAllowEditsOfMax = PreviousShouldAllowEditsOfMax;
	}
}
