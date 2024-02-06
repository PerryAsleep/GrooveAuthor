using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary;
using StepManiaLibrary.ExpressedChart;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;
using static StepManiaLibrary.ExpressedChart.ExpressedChart;
using Config = StepManiaLibrary.ExpressedChart.Config;

namespace StepManiaEditor;

/// <summary>
/// Action to autogenerate steps for one or more EditorPatternEvents.
/// </summary>
internal sealed class ActionAutoGeneratePatterns : EditorAction
{
	/// <summary>
	/// When searching for surrounding footing for a pattern, extend this many number steps beyond the bounds
	/// of the pattern in both directions.
	/// </summary>
	private const int NumStepsToSearchBeyondPattern = 32;

	/// <summary>
	/// Editor.
	/// </summary>
	private readonly Editor Editor;

	/// <summary>
	/// EditorChart to generate events within.
	/// </summary>
	private readonly EditorChart EditorChart;

	/// <summary>
	/// All the patterns to generate in this action.
	/// </summary>
	private readonly List<EditorPatternEvent> Patterns;

	/// <summary>
	/// All EditorEvents deleted as a result of the last time this action was run.
	/// </summary>
	private ActionDeletePatternNotes.Alterations Deletions;

	/// <summary>
	/// All EditorEvents added as a result of the last time this action was run.
	/// </summary>
	private readonly List<EditorEvent> AddedEvents = new();

	public ActionAutoGeneratePatterns(
		Editor editor,
		EditorChart editorChart,
		IEnumerable<EditorPatternEvent> allPatterns) : base(true, false)
	{
		Editor = editor;
		EditorChart = editorChart;
		Patterns = new List<EditorPatternEvent>();
		Patterns.AddRange(allPatterns);
	}

	public override string ToString()
	{
		if (Patterns.Count == 1)
			return $"Autogenerate \"{Patterns[0].GetPatternConfig()}\" Pattern at row {Patterns[0].ChartRow}.";
		return $"Autogenerate {Patterns.Count} Patterns.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void UndoImplementation()
	{
		// To undo this action synchronously delete the newly added events and re-add the deleted events.
		EditorChart.DeleteEvents(AddedEvents);
		Deletions.Undo(EditorChart);
	}

	protected override void DoImplementation()
	{
		// Check for redo and avoid doing the work again.
		if (AddedEvents.Count > 0 || Deletions != null)
		{
			Deletions.Redo(EditorChart);
			if (AddedEvents.Count > 0)
				EditorChart.AddEvents(AddedEvents);
			OnDone();
			return;
		}

		// Early out. It's easier to do this here than adding similar checks throughout.
		if (Patterns.Count == 0)
		{
			OnDone();
			return;
		}

		var errorString = Patterns.Count == 1 ? "Failed to generate pattern." : "Failed to generate patterns.";

		// Get the StepGraph.
		if (!Editor.GetStepGraph(EditorChart.ChartType, out var stepGraph) || stepGraph == null)
		{
			Logger.Error($"{errorString} No {ImGuiUtils.GetPrettyEnumString(EditorChart.ChartType)} StepGraph is loaded.");
			OnDone();
			return;
		}

		// Get the ExpressedChart Config.
		var expressedChartConfig = ExpressedChartConfigManager.Instance.GetConfig(EditorChart.ExpressedChartConfig);
		if (expressedChartConfig == null)
		{
			Logger.Error($"{errorString} No {EditorChart.ExpressedChartConfig} Expressed Chart Config defined.");
			OnDone();
			return;
		}

		// Delete all events which overlap regions to fill based on the patterns.
		Deletions = ActionDeletePatternNotes.DeleteEventsOverlappingPatterns(EditorChart, Patterns);

		// Asynchronously generate the patterns.
		DoPatternGenerationAsync(stepGraph, expressedChartConfig.Config);
	}

	/// <summary>
	/// Performs the bulk of the event generation logic.
	/// Each pattern is run asynchronously and when it is complete the generated EditorEvents
	/// are added back to the EditorChart synchronously.
	/// </summary>
	/// <param name="stepGraph">The StepGraph for the EditorChart.</param>
	/// <param name="expressedChartConfig">The ExpressedChart Config for the EditorChart.</param>
	private async void DoPatternGenerationAsync(StepGraph stepGraph, Config expressedChartConfig)
	{
		// Get the timing events. These are needed by the PerformedChart to properly time new events to support
		// generation logic which relies on time.
		var timingEvents = EditorChart.GetSmTimingEvents();

		// Get the NPS. This is needed by the PerformedChart to properly account for relative density.
		var nps = GetNps();

		// Set up some trackers
		var lastRowCapturedForLaneCounts = -1;
		var currentLaneCounts = new int[stepGraph.NumArrows];
		var totalStepsBeforePattern = 0;

		// Generate each pattern.
		for (var patternIndex = 0; patternIndex < Patterns.Count; patternIndex++)
		{
			var pattern = Patterns[patternIndex];
			var nextPattern = patternIndex < Patterns.Count - 1 ? Patterns[patternIndex + 1] : null;
			List<EditorEvent> patternEvents = null;

			// Asynchronously generate the events.
			await Task.Run(() =>
			{
				try
				{
					patternEvents = GeneratePatternAsync(
						pattern,
						nextPattern,
						stepGraph,
						expressedChartConfig,
						nps,
						timingEvents,
						currentLaneCounts,
						ref lastRowCapturedForLaneCounts,
						ref totalStepsBeforePattern);
				}
				catch (Exception e)
				{
					Logger.Error($"Failed to generate patterns. {e}");
				}
			});

			// Synchronously add the events.
			// Due to the application being a Windows Forms application we know this continuation after the
			// above async operation will occur on the calling (main) thread. These continuations occur separately
			// from the Application's Idle EventHandler, which control the main tick function. So no extra
			// synchronization work is needed to ensure these are added safely on the main thread.
			if (patternEvents != null)
				EditorChart.AddEvents(patternEvents);
		}

		OnDone();
	}

	/// <summary>
	/// Generates new EditorEvents for a pattern. Does not modify the EditorChart.
	/// Newly generated events are stored in IncrementalEvents.
	/// If IncrementalEvents is not yet null, this method will wait.
	/// </summary>
	/// <param name="pattern">The EditorPatternEvent to generate notes for.</param>
	/// <param name="nextPattern">The next EditorPatternEvent following this one which will be generated. May be null.</param>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="expressedChartConfig">ExpressedChart Config for the chart.</param>
	/// <param name="nps">Notes per second of the chart with all patterns generated.</param>
	/// <param name="timingEvents">All the StepMania Events in the chart which affect timing.</param>
	/// <param name="currentLaneCounts">
	/// The count of steps per lane in the chart going up to lastRowCapturedForLaneCounts.
	/// This will be updated by calling this function.
	/// </param>
	/// <param name="lastRowCapturedForLaneCounts">
	/// The last row that was scanned to for updating currentLaneCounts and totalStepsBeforePattern.
	/// Will be updated.
	/// </param>
	/// <param name="totalStepsBeforePattern">
	/// The total number of steps in the chart going up to lastRowCapturedForLaneCounts.
	/// Will be updated.
	/// </param>
	/// <returns>Newly generated EditorEvents.</returns>
	private List<EditorEvent> GeneratePatternAsync(
		EditorPatternEvent pattern,
		EditorPatternEvent nextPattern,
		StepGraph stepGraph,
		Config expressedChartConfig,
		double nps,
		List<Event> timingEvents,
		int[] currentLaneCounts,
		ref int lastRowCapturedForLaneCounts,
		ref int totalStepsBeforePattern)
	{
		var errorString = $"Failed to generate {pattern.GetMiscEventText()} pattern at row {pattern.GetRow()}.";

		// Ensure this pattern is long enough to generate steps.
		if (pattern.GetNumSteps() <= 0)
		{
			Logger.Warn($"{errorString} Pattern range is too short to generate steps.");
			return null;
		}

		// Update the trackers for steps and steps per row to capture any new steps up to this pattern.
		UpdatePrecedingLaneCounts(ref lastRowCapturedForLaneCounts, ref totalStepsBeforePattern, currentLaneCounts, pattern);

		// Get the range of rows to consider for this pattern.
		var (rangeRowStart, rangeRowEnd) = GetRowRangeToConsiderForPattern(pattern);
		var rangeStartsAfterStartOfChart = rangeRowStart > 0;

		// Now that we have a range, get the following EditorEvents and StepMania Events.
		var (smEvents, editorEvents) = EditorChart.GetEventsInRangeForPattern(pattern.GetLastStepRow() + 1, rangeRowEnd);
		smEvents.AddRange(timingEvents);
		smEvents.Sort(new SMEventComparer());

		// Create an ExpressedChart from the following events to determine the following footing.
		var expressedChart = CreateFromSMEvents(smEvents, stepGraph, expressedChartConfig, EditorChart.Rating);
		if (expressedChart == null)
		{
			Logger.Error($"{errorString} Could not create Expressed Chart.");
			return null;
		}

		// Get the following footing. This is done first as following footing can affect previous footing.
		GetFollowingFooting(pattern, stepGraph, expressedChart, editorEvents, out var followingStepFoot,
			out var followingFooting);

		// Get the preceding EditorEvents and StepMania Events.
		(smEvents, editorEvents) = EditorChart.GetEventsInRangeForPattern(rangeRowStart, pattern.GetFirstStepRow() - 1);
		smEvents.AddRange(timingEvents);
		smEvents.Sort(new SMEventComparer());

		// Create an ExpressedChart encompassing the notes preceding the pattern.
		expressedChart = CreateFromSMEvents(smEvents, stepGraph, expressedChartConfig, EditorChart.Rating);
		if (expressedChart == null)
		{
			Logger.Error($"{errorString} Could not create Expressed Chart.");
			return null;
		}

		// Use the ExpressedChart to determine the previous footing and transition information.
		GetPrecedingFootingAndTransitions(
			pattern,
			stepGraph,
			expressedChart,
			editorEvents,
			totalStepsBeforePattern,
			rangeStartsAfterStartOfChart,
			followingStepFoot,
			out var previousStepFoot,
			out var previousStepTime,
			out var previousFooting,
			out var numStepsAtLastTransition,
			out var lastTransitionLeft);

		// Now that we know all the needed previous and following step information, create a PerformedChart for the pattern.
		var performedChart = PerformedChart.CreateWithPattern(stepGraph,
			pattern.GetPatternConfig().Config,
			pattern.GetPerformedChartConfig().Config,
			pattern.GetFirstStepRow(),
			pattern.GetLastStepRow(),
			pattern.RandomSeed,
			previousStepFoot,
			previousStepTime,
			previousFooting,
			followingFooting,
			pattern.IgnorePrecedingDistribution ? new int[stepGraph.NumArrows] : currentLaneCounts,
			timingEvents,
			totalStepsBeforePattern,
			numStepsAtLastTransition,
			lastTransitionLeft,
			nps,
			pattern.GetMiscEventText());
		if (performedChart == null)
		{
			Logger.Error($"{errorString} Could not create Performed Chart.");
			return null;
		}

		//LogDebugInfo(pattern, previousStepFoot, followingStepFoot, lastTransitionLeft, currentLaneCounts, previousFooting,
		//	followingFooting, totalStepsBeforePattern, numStepsAtLastTransition, nps);

		// Convert this PerformedChart section to Stepmania Events.
		var newSmEvents = performedChart.CreateSMChartEvents();

		// Check for excluding some Events. It is possible that future patterns will
		// overlap this pattern. In that case we do not want to add the notes from
		// this pattern which overlap, and we instead want to let the next pattern
		// generate those notes.
		RemoveEventsWhichOverlapNextPattern(pattern, nextPattern, newSmEvents);

		// Add EditorEvents for the new StepMania events.
		var newEvents = new List<EditorEvent>();
		foreach (var smEvent in newSmEvents)
			newEvents.Add(EditorEvent.CreateEvent(EventConfig.CreateConfig(EditorChart, smEvent)));
		AddedEvents.AddRange(newEvents);

		return newEvents;
	}

	/// <summary>
	/// Updates counters for total steps and steps per lane going up to the given pattern.
	/// </summary>
	/// <param name="lastRowCapturedForLaneCounts">
	/// The last row that was tracked up to previously. This will be updated as we advance to the next pattern.
	/// </param>
	/// <param name="totalStepCount">Total step count to update.</param>
	/// <param name="laneCounts">Lane counts to update.</param>
	/// <param name="pattern">The pattern to advance up to.</param>
	private static void UpdatePrecedingLaneCounts(
		ref int lastRowCapturedForLaneCounts,
		ref int totalStepCount,
		int[] laneCounts,
		EditorPatternEvent pattern)
	{
		var patternFirstStepRow = pattern.GetFirstStepRow();
		if (lastRowCapturedForLaneCounts >= patternFirstStepRow)
			return;

		var editorChart = pattern.GetEditorChart();
		var editorEventEnum = editorChart.GetEvents().FindFirstAtOrAfterChartPosition(lastRowCapturedForLaneCounts);
		if (editorEventEnum != null)
		{
			while (editorEventEnum.MoveNext())
			{
				var editorEvent = editorEventEnum.Current;
				var row = editorEvent!.GetRow();
				if (row >= patternFirstStepRow)
					break;
				lastRowCapturedForLaneCounts = row;
				if (editorEvent.IsStep())
				{
					var lane = editorEvent.GetLane();
					laneCounts[lane]++;
					totalStepCount++;
				}
			}
		}
	}

	/// <summary>
	/// Helper function to get the following footing of a pattern.
	/// If following steps are brackets only the DefaultFootPortion (Heel)'s lane will be used.
	/// </summary>
	/// <param name="pattern">EditorPatternEvent to find the following footing of.</param>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="expressedChart">ExpressedChart associated with the following steps.</param>
	/// <param name="editorEvents">
	/// List of EditorEvents encompassing a range that extends beyond the pattern.
	/// </param>
	/// <param name="followingStepFoot">
	/// Out parameter to record the next foot which steps first in the following steps.
	/// </param>
	/// <param name="followingFooting">
	/// Out parameter to record the lane stepped on per foot of the following steps.
	/// </param>
	private static void GetFollowingFooting(
		EditorPatternEvent pattern,
		StepGraph stepGraph,
		ExpressedChart expressedChart,
		List<EditorEvent> editorEvents,
		out int followingStepFoot,
		out int[] followingFooting)
	{
		// Get the first ExpressedChart node that follows the pattern.
		var node = expressedChart.GetRootSearchNode();
		while (node != null && node.Position < pattern.GetLastStepRow())
			node = node.GetNextNode();

		// Initialize out parameters.
		followingFooting = new int[Constants.NumFeet];
		for (var i = 0; i < Constants.NumFeet; i++)
			followingFooting[i] = Constants.InvalidFoot;
		followingStepFoot = Constants.InvalidFoot;

		// Unused variable, but it simplifies the common footing update logic.
		var followingStepTime = new double[Constants.NumFeet];

		// Scan forwards.
		var editorEventIndex = 0;
		var numFeetFound = 0;
		var positionOfCurrentSteps = -1;
		var currentSteppedLanes = new bool[stepGraph.NumArrows];
		while (node != null)
		{
			// If we have scanned forward into a new row, update the currently stepped on lanes for that row.
			CheckAndUpdateCurrentSteppedLanes(stepGraph, node, editorEvents, ref editorEventIndex, ref positionOfCurrentSteps,
				ref currentSteppedLanes, true);

			// Update the tracked footing based on the currently stepped on lanes.
			CheckAndUpdateFooting(stepGraph, node, followingFooting, currentSteppedLanes, ref numFeetFound, ref followingStepFoot,
				ref followingStepTime);

			if (numFeetFound == Constants.NumFeet)
				break;

			// Advance.
			node = node.GetNextNode();
		}
	}

	/// <summary>
	/// Helper function to get the preceding footing of a pattern, and information about the preceding transition.
	/// </summary>
	/// <param name="pattern">EditorPatternEvent to find the preceding information of.</param>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="expressedChart">ExpressedChart associated with the preceding steps.</param>
	/// <param name="editorEvents">
	/// List of EditorEvents encompassing a range that extends before the pattern.
	/// </param>
	/// <param name="totalStepsBeforePattern">
	/// Total number of steps before the pattern.
	/// </param>
	/// <param name="rangeStartsAfterStartOfChart">
	/// Whether or not the range of the preceding steps starts after the start of the chart.
	/// </param>
	/// <param name="followingStepFoot">
	/// Which foot makes the first step following this pattern.
	/// </param>
	/// <param name="previousStepFoot">
	/// Out parameter to record the foot used to step on the most recent preceding step.
	/// </param>
	/// <param name="previousStepTime">
	/// Out parameter to record the time of the most recent preceding step.
	/// </param>
	/// <param name="previousFooting">
	/// Out parameter to record the lane stepped on per foot of the preceding steps.
	/// </param>
	/// <param name="numStepsAtLastTransition">
	/// Out parameter to hold the number of steps in the chart the last transition occurred at.
	/// </param>
	/// <param name="lastTransitionLeft">
	/// Out parameter to record whether the last transition is in the left direction.
	/// Null if unknown.
	/// </param>
	private static void GetPrecedingFootingAndTransitions(
		EditorPatternEvent pattern,
		StepGraph stepGraph,
		ExpressedChart expressedChart,
		List<EditorEvent> editorEvents,
		int totalStepsBeforePattern,
		bool rangeStartsAfterStartOfChart,
		int followingStepFoot,
		out int previousStepFoot,
		out double[] previousStepTime,
		out int[] previousFooting,
		out int numStepsAtLastTransition,
		out bool? lastTransitionLeft)
	{
		// Initialize out variables.
		previousStepFoot = Constants.InvalidFoot;
		previousStepTime = new double[Constants.NumFeet];
		previousFooting = new int[Constants.NumFeet];
		for (var i = 0; i < Constants.NumFeet; i++)
			previousFooting[i] = Constants.InvalidFoot;

		// Set up transition checking variables.
		var transitionCutoffPercentage = pattern.GetPerformedChartConfig().Config.Transitions.TransitionCutoffPercentage;
		numStepsAtLastTransition = Math.Max(0,
			totalStepsBeforePattern - pattern.GetPerformedChartConfig().Config.Transitions.StepsPerTransitionMin);
		var currentStepsInRegionBeforePattern = 0;
		lastTransitionLeft = null;

		// Loop over all ExpressedChart search nodes.
		// The nodes give us GraphNodes, which let us determine which arrows are associated with which feet.
		var currentExpressedChartSearchNode = expressedChart.GetRootSearchNode();
		ChartSearchNode previousExpressedChartSearchNode = null;
		var editorEventIndex = 0;
		var sumOfLanesBeforePattern = 0;
		var numStepsCheckedBeforePattern = 0;
		var firstStepRow = pattern.GetFirstStepRow();
		var stepsIntoPrecedingRangeOfLastTransition = -1;
		while (currentExpressedChartSearchNode != null)
		{
			if (currentExpressedChartSearchNode.Position >= firstStepRow)
				break;

			// Track transition information.
			var isStep = !(currentExpressedChartSearchNode.PreviousLink?.GraphLink?.IsRelease() ?? true);
			if (isStep)
				currentStepsInRegionBeforePattern++;
			stepGraph.GetSide(currentExpressedChartSearchNode.GraphNode, transitionCutoffPercentage, out var leftSide);
			if (leftSide != null && lastTransitionLeft != leftSide)
			{
				if (lastTransitionLeft != null)
					stepsIntoPrecedingRangeOfLastTransition = currentStepsInRegionBeforePattern;
				lastTransitionLeft = leftSide;
			}

			// Advance within the EditorEvents list.
			while (editorEventIndex < editorEvents.Count &&
			       editorEvents[editorEventIndex].GetRow() <= currentExpressedChartSearchNode.Position)
			{
				if (editorEvents[editorEventIndex].IsStep())
				{
					sumOfLanesBeforePattern += editorEvents[editorEventIndex].GetLane();
					numStepsCheckedBeforePattern++;
				}

				editorEventIndex++;
			}

			previousExpressedChartSearchNode = currentExpressedChartSearchNode;
			currentExpressedChartSearchNode = currentExpressedChartSearchNode.GetNextNode();
		}

		// If we found a transition, convert it to an absolute step count.
		if (stepsIntoPrecedingRangeOfLastTransition != -1)
			numStepsAtLastTransition = totalStepsBeforePattern - currentStepsInRegionBeforePattern +
			                           stepsIntoPrecedingRangeOfLastTransition;

		// There is a situation where we might check all preceding steps and not find a transition but we are
		// deep into the chart and we need to set lastTransitionLeft. Guess the last transition direction based on
		// the distribution of steps within the region we checked.
		if (lastTransitionLeft == null && rangeStartsAfterStartOfChart && numStepsCheckedBeforePattern > 0)
		{
			var averageLane = (double)sumOfLanesBeforePattern / numStepsCheckedBeforePattern;
			lastTransitionLeft = averageLane < stepGraph.NumArrows * 0.5;
		}

		// Scan backwards for the preceding footing.
		if (previousExpressedChartSearchNode != null)
		{
			// Ensure the editorEventIndex is still within the range if the events so we can scan backwards.
			editorEventIndex = Math.Min(editorEventIndex, editorEvents.Count - 1);
			editorEventIndex = Math.Max(editorEventIndex, 0);

			GetPrecedingFooting(
				stepGraph,
				previousExpressedChartSearchNode,
				editorEvents,
				editorEventIndex,
				out previousStepTime,
				out previousStepFoot,
				out previousFooting);
		}

		// If there are no previous notes, use the default position.
		if (previousFooting[Constants.L] == Constants.InvalidArrowIndex)
			previousFooting[Constants.L] = stepGraph.GetRoot().State[Constants.L, Constants.DefaultFootPortion].Arrow;
		if (previousFooting[Constants.R] == Constants.InvalidArrowIndex)
			previousFooting[Constants.R] = stepGraph.GetRoot().State[Constants.R, Constants.DefaultFootPortion].Arrow;

		// Due to the above logic to assign footing to the default state it is possible
		// for both feet to be assigned to the same arrow. Correct that.
		if (previousFooting[Constants.L] == previousFooting[Constants.R])
		{
			previousFooting[Constants.L] = stepGraph.GetRoot().State[Constants.L, Constants.DefaultFootPortion].Arrow;
			previousFooting[Constants.R] = stepGraph.GetRoot().State[Constants.R, Constants.DefaultFootPortion].Arrow;
		}

		// If we don't know what foot to start on, choose a starting foot.
		if (previousStepFoot == Constants.InvalidFoot)
		{
			// If we know the following foot, choose a starting foot that will lead into it
			// through alternating.
			if (followingStepFoot != Constants.InvalidArrowIndex)
			{
				var numStepsInPattern = pattern.GetNumSteps();

				// Even number of steps, start on the same foot.
				if (numStepsInPattern % 2 == 0)
				{
					previousStepFoot = Constants.OtherFoot(followingStepFoot);
				}
				// Otherwise, start on the opposite foot.
				else
				{
					previousStepFoot = followingStepFoot;
				}
			}
			// Otherwise, start on the right foot.
			else
			{
				previousStepFoot = Constants.L;
			}
		}
	}

	/// <summary>
	/// Helper function to get the preceding footing of a pattern.
	/// If preceding steps are brackets only the DefaultFootPortion (Heel)'s lane will be used.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="node">The ChartSearchNode of the last event preceding the pattern.</param>
	/// <param name="editorEvents">
	/// List of EditorEvents encompassing a range that extends beyond the pattern in both directions.
	/// </param>
	/// <param name="editorEventIndex">
	/// The current index into the EditorEvents List.
	/// </param>
	/// <param name="previousStepTime">
	/// Out parameter to record the time of the most recent preceding step.
	/// </param>
	/// <param name="previousStepFoot">
	/// Out parameter to record the foot used to step on the most recent preceding step.
	/// </param>
	/// <param name="previousFooting">
	/// Out parameter to record the lane stepped on per foot of the preceding steps.
	/// </param>
	private static void GetPrecedingFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		IReadOnlyList<EditorEvent> editorEvents,
		int editorEventIndex,
		out double[] previousStepTime,
		out int previousStepFoot,
		out int[] previousFooting)
	{
		// Initialize out parameters.
		previousStepFoot = Constants.InvalidFoot;
		previousStepTime = new double[Constants.NumFeet];
		previousFooting = new int[Constants.NumFeet];
		for (var i = 0; i < Constants.NumFeet; i++)
			previousFooting[i] = Constants.InvalidFoot;

		// Scan backwards.
		var numFeetFound = 0;
		var positionOfCurrentSteps = -1;
		var currentSteppedLanes = new bool[stepGraph.NumArrows];
		while (node != null)
		{
			// If we have scanned backwards into a new row, update the currently stepped on lanes for that row.
			CheckAndUpdateCurrentSteppedLanes(stepGraph, node, editorEvents, ref editorEventIndex, ref positionOfCurrentSteps,
				ref currentSteppedLanes, false);

			// Update the tracked footing based on the currently stepped on lanes.
			CheckAndUpdateFooting(stepGraph, node, previousFooting, currentSteppedLanes, ref numFeetFound, ref previousStepFoot,
				ref previousStepTime);

			if (numFeetFound == Constants.NumFeet)
				break;

			// Advance.
			node = node.PreviousNode;
		}
	}

	/// <summary>
	/// Helper function for updating an array of currently stepped on lanes when scanning and the row changes.
	/// The currently stepped on lanes are used for determining footing when comparing against a GraphNode.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the chart.</param>
	/// <param name="node">
	/// Current ChartSearchNode. If the position of the current steps doesn't equal this node's position
	/// then the currentSteppedLanes will be updated accordingly.
	/// </param>
	/// <param name="editorEvents">
	/// List of EditorEvents encompassing a range that extends beyond the pattern in both directions.
	/// </param>
	/// <param name="editorEventIndex">
	/// The current index into the EditorEvents List.
	/// </param>
	/// <param name="positionOfCurrentSteps">
	/// Last position of the currentSteppedLanes. Will be updated if currentSteppedLanes are updated.
	/// </param>
	/// <param name="currentSteppedLanes">
	/// Array of bools, one per lane. This will be updated to reflect which lanes have steps on them
	/// if the positionOfCurrentSteps is old and needs to be updated based on the given node's position.
	/// </param>
	/// <param name="scanForward">
	/// If true, scan forward for following steps. If false, scan backwards for preceding steps.
	/// </param>
	private static void CheckAndUpdateCurrentSteppedLanes(
		StepGraph stepGraph,
		ChartSearchNode node,
		IReadOnlyList<EditorEvent> editorEvents,
		ref int editorEventIndex,
		ref int positionOfCurrentSteps,
		ref bool[] currentSteppedLanes,
		bool scanForward)
	{
		// Determine the steps which occur at the row of this node, so we can assign feet to them.
		if (positionOfCurrentSteps != node.Position)
		{
			// Clear stepped lanes.
			for (var i = 0; i < stepGraph.NumArrows; i++)
				currentSteppedLanes[i] = false;

			// Scan the current row, recording the lanes being stepped on at this position.
			while (editorEventIndex >= 0 && editorEventIndex < editorEvents.Count
			                             && (scanForward
				                             ? editorEvents[editorEventIndex].GetRow() <= node.Position
				                             : editorEvents[editorEventIndex].GetRow() >= node.Position))
			{
				if (editorEvents[editorEventIndex].GetRow() == node.Position)
				{
					if (editorEvents[editorEventIndex].IsStep())
					{
						currentSteppedLanes[editorEvents[editorEventIndex].GetLane()] = true;
					}
				}

				editorEventIndex += scanForward ? 1 : -1;
			}

			// Update the position we have recorded steps for.
			positionOfCurrentSteps = node.Position;
		}
	}

	/// <summary>
	/// Helper function to update preceding or following footing.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the chart.</param>
	/// <param name="node">Current ChartSearchNode.</param>
	/// <param name="footing">
	/// Array of lanes per foot representing previous or following footing to fill.
	/// Will be updated as footing is found.
	/// </param>
	/// <param name="steppedLanes">
	/// Array of bools per lane representing which lanes are currently stepped on.
	/// </param>
	/// <param name="numFeetFound">
	/// Number of feet whose footing is currently found. Will be updated as footing
	/// is found.
	/// </param>
	/// <param name="stepFoot">
	/// Foot of the first preceding or following step to set.
	/// </param>
	/// <param name="stepFootTime">
	/// Array of time per foot representing the times of the previous or following steps.
	/// Time of the first preceding of following step to set.
	/// </param>
	private static void CheckAndUpdateFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		int[] footing,
		bool[] steppedLanes,
		ref int numFeetFound,
		ref int stepFoot,
		ref double[] stepFootTime)
	{
		// With the stepped on lanes known, use the GraphNodes to determine which foot stepped
		// on each lane.
		if (node.PreviousLink != null && !node.PreviousLink.GraphLink.IsRelease())
		{
			for (var f = 0; f < Constants.NumFeet; f++)
			{
				if (footing[f] != Constants.InvalidFoot)
					continue;
				for (var p = 0; p < Constants.NumFootPortions; p++)
				{
					if (footing[f] != Constants.InvalidFoot)
						continue;

					if (node.GraphNode.State[f, p].State != GraphArrowState.Lifted)
					{
						for (var a = 0; a < stepGraph.NumArrows; a++)
						{
							if (steppedLanes[a] && a == node.GraphNode.State[f, p].Arrow)
							{
								if (stepFoot == Constants.InvalidFoot)
									stepFoot = f;

								footing[f] = node.GraphNode.State[f, p].Arrow;
								stepFootTime[f] = node.TimeSeconds;
								numFeetFound++;
								break;
							}
						}
					}

					if (numFeetFound == Constants.NumFeet)
						break;
				}

				if (numFeetFound == Constants.NumFeet)
					break;
			}
		}
	}

	/// <summary>
	/// Removes Events generated by the given pattern which overlap those which will be generated by
	/// the given next pattern.
	/// </summary>
	/// <param name="pattern">Current pattern which generated events.</param>
	/// <param name="nextPattern">Next pattern which will generate events.</param>
	/// <param name="patternEvents">
	/// The events generated by the current pattern.
	/// This will be modified.
	/// </param>
	private static void RemoveEventsWhichOverlapNextPattern(
		EditorPatternEvent pattern,
		EditorPatternEvent nextPattern,
		List<Event> patternEvents)
	{
		if (nextPattern == null || nextPattern.GetNumSteps() == 0)
			return;

		var nextPatternStartRow = nextPattern.GetFirstStepRow();
		if (nextPatternStartRow > pattern.GetLastStepRow())
			return;

		for (var i = 0; i < patternEvents.Count; i++)
		{
			if (patternEvents[i].IntegerPosition >= nextPatternStartRow)
			{
				patternEvents.RemoveRange(i, patternEvents.Count - i);
				return;
			}
		}
	}

	/// <summary>
	/// Given an EditorPatternEvent, gets a range of rows to consider for determining previous and following
	/// footing, and previous transition information.
	/// </summary>
	/// <param name="pattern">The EditorPatternEvent to consider.</param>
	/// <returns>
	/// Tuple representing an inclusive row range around the pattern to consider.
	/// </returns>
	private static (int start, int end) GetRowRangeToConsiderForPattern(EditorPatternEvent pattern)
	{
		var editorChart = pattern.GetEditorChart();

		// Extend earlier than the pattern start. We do this so that we have enough prior steps
		// to interpret the previous footing, and also so that we can know when the last transition occurred.
		var patternFirstStepRow = pattern.GetFirstStepRow();
		var rangeStart = patternFirstStepRow;
		var transitionConfig = pattern.GetPerformedChartConfig().Config.Transitions;
		var transitionExtension = transitionConfig.IsEnabled() ? Math.Max(0, transitionConfig.StepsPerTransitionMin) : 0;
		var stepsToBackUp = Math.Max(transitionExtension, NumStepsToSearchBeyondPattern);
		var editorEventEnum = editorChart.GetEvents().FindFirstBeforeChartPosition(patternFirstStepRow);
		if (editorEventEnum != null)
		{
			var numStepsBeyondPattern = 0;
			var finalRow = -1;
			while (editorEventEnum.MovePrev())
			{
				var row = editorEventEnum.Current!.GetRow();
				if (finalRow != -1 && row < finalRow)
					break;
				rangeStart = row;
				if (editorEventEnum.Current.GetRow() < patternFirstStepRow && editorEventEnum.Current.IsStep())
				{
					numStepsBeyondPattern++;
					if (numStepsBeyondPattern == stepsToBackUp)
						finalRow = row;
				}
			}
		}

		// Extend beyond the end of the pattern so we can interpret the following footing.
		var patternLastStepRow = pattern.GetLastStepRow();
		var rangeEnd = patternLastStepRow;
		editorEventEnum = editorChart.GetEvents().FindFirstAtOrAfterChartPosition(patternLastStepRow);
		if (editorEventEnum != null)
		{
			var numStepsBeyondPattern = 0;
			var finalRow = -1;
			while (editorEventEnum.MoveNext())
			{
				var row = editorEventEnum.Current!.GetRow();
				if (finalRow != -1 && row > finalRow)
					break;
				rangeEnd = row;
				if (editorEventEnum.Current.GetRow() > patternLastStepRow && editorEventEnum.Current.IsStep())
				{
					numStepsBeyondPattern++;
					if (numStepsBeyondPattern == NumStepsToSearchBeyondPattern)
						finalRow = row;
				}
			}
		}

		return (rangeStart, rangeEnd);
	}

	/// <summary>
	/// Gets the notes per second of the chart including the notes from the patterns for this action.
	/// </summary>
	/// <returns>Notes per second of the chart including the notes from the patterns for this action.</returns>
	private double GetNps()
	{
		var numSteps = EditorChart.GetStepCount() + GetTotalStepsFromAllPatterns();
		var startTime = EditorChart.GetStartChartTime();
		var endTime = Math.Max(GetLastPatternStepTime(), EditorChart.GetEndChartTime());
		var totalTime = endTime - startTime;
		return totalTime > 0.0 ? numSteps / totalTime : 0.0;
	}

	/// <summary>
	/// Gets the total number of steps which will be generated by all patterns for this action.
	/// </summary>
	/// <returns>Total number of steps which will be generated by all patterns for this action.</returns>
	private int GetTotalStepsFromAllPatterns()
	{
		var steps = 0;
		for (var patternIndex = 0; patternIndex < Patterns.Count; patternIndex++)
		{
			// Get the row the next pattern starts at in case it cuts off this pattern.
			var nextPatternStartRow = -1;
			var nextPatternIndex = patternIndex + 1;
			if (nextPatternIndex < Patterns.Count)
			{
				if (Patterns[nextPatternIndex].GetNumSteps() > 0)
				{
					nextPatternStartRow = Patterns[nextPatternIndex].GetFirstStepRow();
				}
			}

			// Add the events from this pattern which won't be cut off by the following pattern.
			if (nextPatternStartRow != -1)
				steps += Patterns[patternIndex].GetNumStepsBeforeRow(nextPatternStartRow);
			else
				steps += Patterns[patternIndex].GetNumSteps();
		}

		return steps;
	}

	/// <summary>
	/// Gets the time of the last step to be generated by the patterns for this action.
	/// </summary>
	/// <returns>Time of the last step to be generated by the patterns for this action.</returns>
	private double GetLastPatternStepTime()
	{
		var lastStepTime = 0.0;

		// For overlapping patterns we intentionally only generate up to the end of the last pattern.
		for (var patternIndex = Patterns.Count - 1; patternIndex >= 0; patternIndex--)
		{
			if (Patterns[patternIndex].GetNumSteps() > 0)
			{
				var lastStepRow = Patterns[patternIndex].GetLastStepRow();
				if (EditorChart.TryGetTimeFromChartPosition(lastStepRow, ref lastStepTime))
					return lastStepTime;
			}
		}

		return lastStepTime;
	}

	/// <summary>
	/// Log debug information about a pattern.
	/// </summary>
	// ReSharper disable once UnusedMember.Local
	private static void LogDebugInfo(
		EditorPatternEvent pattern,
		int previousStepFoot,
		int followingStepFoot,
		bool? lastTransitionLeft,
		int[] currentLaneCounts,
		int[] previousFooting,
		int[] followingFooting,
		int totalStepsBeforePattern,
		int numStepsAtLastTransition,
		double nps)
	{
		string GetLaneString(int lane)
		{
			return lane == Constants.InvalidArrowIndex ? "?" : lane.ToString();
		}

		string GetFootString(int foot)
		{
			return foot == Constants.InvalidFoot ? "Unknown" : foot == Constants.L ? "Left" : "Right";
		}

		var transitionDirectionString = "Unknown";
		if (lastTransitionLeft != null)
			transitionDirectionString = lastTransitionLeft.Value ? "Left" : "Right";

		var stepsPerLaneStringStringBuilder = new StringBuilder();
		var first = true;
		foreach (var count in currentLaneCounts)
		{
			if (!first)
				stepsPerLaneStringStringBuilder.Append(',');
			stepsPerLaneStringStringBuilder.Append(count);
			first = false;
		}

		var stepsPerLaneString = stepsPerLaneStringStringBuilder.ToString();

		Logger.Info($"Pattern [{pattern.GetRow()}-{pattern.GetRow() + pattern.GetLength()}] Generation Info:"
		            + $"\n\tFirst Step Row: {pattern.GetFirstStepRow()}"
		            + $"\n\tLast Step Row: {pattern.GetLastStepRow()}"
		            + $"\n\tPreceding Footing: L:{GetLaneString(previousFooting[Constants.L])}, R:{GetLaneString(previousFooting[Constants.R])}"
		            + $"\n\tPreceding Step: {GetFootString(previousStepFoot)}"
		            + $"\n\tFollowing Footing: L:{GetLaneString(followingFooting[Constants.L])}, R:{GetLaneString(followingFooting[Constants.R])}"
		            + $"\n\tFollowing Step: {GetFootString(followingStepFoot)}"
		            + $"\n\tLast Transition: {totalStepsBeforePattern - numStepsAtLastTransition} steps ago"
		            + $"\n\tLast Transition Direction: {transitionDirectionString}"
		            + $"\n\tTotal Steps before pattern: {totalStepsBeforePattern}"
		            + $"\n\tSteps per lane before pattern: {stepsPerLaneString}"
		            + $"\n\tChart NPS: {nps}");
	}
}
