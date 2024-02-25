using StepManiaEditor.AutogenConfig;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Action which sets an arrow weight on a Performed Chart Config.
/// </summary>
internal sealed class ActionSetPerformedChartConfigArrowWeight : EditorAction
{
	private readonly EditorPerformedChartConfig Config;
	private readonly ChartType ChartType;
	private readonly int LaneIndex;
	private readonly int Weight;
	private readonly int PreviousWeight;

	public ActionSetPerformedChartConfigArrowWeight(EditorPerformedChartConfig config, ChartType chartType, int laneIndex,
		int weight,
		int previousWeight) : base(false, false)
	{
		Config = config;
		ChartType = chartType;
		LaneIndex = laneIndex;
		Weight = weight;
		PreviousWeight = previousWeight;
	}

	protected override void DoImplementation()
	{
		Config.Config.SetArrowWeight(ChartType, LaneIndex, Weight);
	}

	protected override void UndoImplementation()
	{
		Config.Config.SetArrowWeight(ChartType, LaneIndex, PreviousWeight);
	}

	public override bool AffectsFile()
	{
		return false;
	}
}
