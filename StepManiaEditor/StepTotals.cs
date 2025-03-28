using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Readonly interface for StepTotals.
/// </summary>
internal interface IReadOnlyStepTotals
{
	public int GetStepCount();
	public int GetNumRowsWithSteps();
	public int GetNumStepsAtRow(int row);
	public int[] GetStepCountByLane();
	public int GetHoldCount();

	public int GetRollCount();
	public int GetMineCount();

	public int GetFakeCount();
	public int GetLiftCount();

	public int GetMultipliersCount();
	public int GetNumPlayersWithNotes();
}

/// <summary>
/// Cached totals for various types of steps in an EditorChart.
/// StepTotals owns and exposes StepDensity.
/// Expected Usage:
///  Call InitializeStepDensity once after the EditorChart is initialized and all events are available.
///  Call OnEventAdded when adding an event.
///  Call OnEventDeleted when deleting an event.
///  After adding or deleting a number of events, call CommitAddsAndDeletesToStepDensity.
///  Call OnHoldTypeChanged / OnFakeTypeChanged when appropriate.
/// </summary>
internal sealed class StepTotals : IReadOnlyStepTotals
{
	/// <summary>
	/// Struct for accumulating added and deleted steps for committing to StepDensity.
	/// </summary>
	private struct PendingDensityStep(bool add, EditorEvent editorEvent, int numStepsAtRow)
	{
		public readonly bool Add = add;
		public readonly EditorEvent EditorEvent = editorEvent;
		public readonly int NumStepsAtRow = numStepsAtRow;
	}

	/// <summary>
	/// Total step counts by lane for the EditorChart.
	/// </summary>
	private int[] StepCountsByLane;

	/// <summary>
	/// Total note counts per player for the EditorChart;
	/// </summary>
	private readonly Dictionary<int, int> NoteCountsPerPlayer = new();

	/// <summary>
	/// Total step count for the EditorChart.
	/// This count considers two steps on the same row to be two steps.
	/// This does not include fakes.
	/// </summary>
	private int StepCount;

	/// <summary>
	/// Total hold count for the EditorChart.
	/// </summary>
	private int HoldCount;

	/// <summary>
	/// Total roll count for the EditorChart.
	/// </summary>
	private int RollCount;

	/// <summary>
	/// Total mine count for the EditorChart.
	/// </summary>
	private int MineCount;

	/// <summary>
	/// Total fake note count for the EditorChart.
	/// </summary>
	private int FakeCount;

	/// <summary>
	/// Total lift note count for the EditorChart.
	/// </summary>
	private int LiftCount;

	/// <summary>
	/// Total multipliers count for the EditorChart.
	/// </summary>
	private int MultipliersCount;

	/// <summary>
	/// The EditorChart whose totals are cached.
	/// </summary>
	private readonly EditorChart EditorChart;

	/// <summary>
	/// Step counts per row.
	/// </summary>
	private readonly Dictionary<int, short> StepCountPerRow = new();

	/// <summary>
	/// StepDensity.
	/// </summary>
	private StepDensity StepDensity;

	/// <summary>
	/// List of added and deleted steps which need to be committed to StepDensity.
	/// </summary>
	private readonly List<PendingDensityStep> PendingStepsForDensity = [];

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="editorChart">EditorChart to cache totals for.</param>
	public StepTotals(EditorChart editorChart)
	{
		EditorChart = editorChart;
		Clear();
	}

	public void InitializeStepDensity()
	{
		StepDensity = new StepDensity(EditorChart);
	}

	public StepDensity GetStepDensity()
	{
		return StepDensity;
	}

	private void Clear()
	{
		StepCount = 0;
		HoldCount = 0;
		RollCount = 0;
		MineCount = 0;
		FakeCount = 0;
		LiftCount = 0;
		MultipliersCount = 0;
		StepCountPerRow.Clear();
		NoteCountsPerPlayer.Clear();
		StepCountsByLane = new int[EditorChart.NumInputs];
		for (var a = 0; a < EditorChart.NumInputs; a++)
			StepCountsByLane[a] = 0;
	}

	/// <summary>
	/// Update StepTotals with a newly added event.
	/// This will update step total information but will not update StepDensity information
	/// until CommitAddsAndDeletesToStepDensity is called later.
	/// </summary>
	/// <param name="editorEvent">EditorEvent which was added.</param>
	public void OnEventAdded(EditorEvent editorEvent)
	{
		OnEventAdded(editorEvent, editorEvent.IsFake());
	}

	private static bool CanEventBeConsideredFake(EditorEvent editorEvent)
	{
		return editorEvent.IsLaneNote();
	}

	private void OnEventAdded(EditorEvent editorEvent, bool isFake)
	{
		var isStep = false;
		switch (editorEvent)
		{
			case EditorTapNoteEvent:
				if (isFake)
					FakeCount++;
				else
					isStep = true;
				break;
			case EditorHoldNoteEvent hold:
				if (isFake)
				{
					FakeCount++;
				}
				else
				{
					isStep = true;
					if (hold.IsRoll())
						RollCount++;
					else
						HoldCount++;
				}

				break;
			case EditorMineNoteEvent:
				MineCount++;
				break;
			case EditorFakeNoteEvent:
				FakeCount++;
				break;
			case EditorLiftNoteEvent:
				if (isFake)
				{
					FakeCount++;
				}
				else
				{
					isStep = true;
					LiftCount++;
				}

				break;
			case EditorMultipliersEvent:
				MultipliersCount++;
				break;
		}

		if (editorEvent.IsLaneNote())
		{
			var player = editorEvent.GetPlayer();
			NoteCountsPerPlayer.TryAdd(player, 0);
			NoteCountsPerPlayer[player]++;
		}

		if (isStep)
		{
			StepCount++;
			StepCountsByLane[editorEvent.GetLane()]++;
			var row = editorEvent.GetRow();
			StepCountPerRow.TryAdd(row, 0);
			StepCountPerRow[row]++;

			PendingStepsForDensity.Add(new PendingDensityStep(true, editorEvent, StepCountPerRow[row]));
		}
		else if (editorEvent is EditorLastSecondHintEvent)
		{
			PendingStepsForDensity.Add(new PendingDensityStep(true, editorEvent, 0));
		}
	}

	/// <summary>
	/// Update StepTotals with a newly deleted event.
	/// This will update step total information but will not update StepDensity information
	/// until CommitAddsAndDeletesToStepDensity is called later.
	/// </summary>
	/// <param name="editorEvent">EditorEvent which was deleted.</param>
	public void OnEventDeleted(EditorEvent editorEvent)
	{
		OnEventDeleted(editorEvent, editorEvent.IsFake());
	}

	private void OnEventDeleted(EditorEvent editorEvent, bool isFake)
	{
		var isStep = false;
		switch (editorEvent)
		{
			case EditorTapNoteEvent:
				if (isFake)
					FakeCount--;
				else
					isStep = true;
				break;
			case EditorHoldNoteEvent hold:
				if (isFake)
				{
					FakeCount--;
				}
				else
				{
					isStep = true;
					if (hold.IsRoll())
						RollCount--;
					else
						HoldCount--;
				}

				break;
			case EditorMineNoteEvent:
				MineCount--;
				break;
			case EditorFakeNoteEvent:
				FakeCount--;
				break;
			case EditorLiftNoteEvent:
				if (isFake)
				{
					FakeCount--;
				}
				else
				{
					isStep = true;
					LiftCount--;
				}

				break;
			case EditorMultipliersEvent:
				MultipliersCount--;
				break;
		}

		if (editorEvent.IsLaneNote())
		{
			var player = editorEvent.GetPlayer();
			NoteCountsPerPlayer[player]--;
			if (NoteCountsPerPlayer[player] == 0)
				NoteCountsPerPlayer.Remove(player);
		}

		if (isStep)
		{
			StepCount--;
			StepCountsByLane[editorEvent.GetLane()]--;
			var row = editorEvent.GetRow();
			StepCountPerRow[row]--;
			var newStepCount = StepCountPerRow[row];
			if (StepCountPerRow[row] == 0)
				StepCountPerRow.Remove(row);

			PendingStepsForDensity.Add(new PendingDensityStep(false, editorEvent, newStepCount));
		}
		else if (editorEvent is EditorLastSecondHintEvent)
		{
			PendingStepsForDensity.Add(new PendingDensityStep(false, editorEvent, 0));
		}
	}

	/// <summary>
	/// Commits previously added and deleted events to StepDensity.
	/// </summary>
	public void CommitAddsAndDeletesToStepDensity()
	{
		if (StepDensity == null)
		{
			PendingStepsForDensity.Clear();
			return;
		}

		bool? adding = null;
		foreach (var pendingStep in PendingStepsForDensity)
		{
			if (adding == null || pendingStep.Add != adding.Value)
			{
				if (adding.HasValue)
				{
					if (adding.Value)
					{
						StepDensity.EndAddEvents();
					}
					else
					{
						StepDensity.EndDeleteEvents();
					}
				}

				if (pendingStep.Add)
				{
					StepDensity.BeginAddEvents();
				}
				else
				{
					StepDensity.BeginDeleteEvents();
				}

				adding = pendingStep.Add;
			}

			if (pendingStep.Add)
			{
				StepDensity.AddEvent(pendingStep.EditorEvent, pendingStep.NumStepsAtRow);
			}
			else
			{
				StepDensity.DeleteEvent(pendingStep.EditorEvent, pendingStep.NumStepsAtRow);
			}
		}

		if (adding != null)
		{
			if (adding.Value)
			{
				StepDensity.EndAddEvents();
			}
			else
			{
				StepDensity.EndDeleteEvents();
			}
		}

		PendingStepsForDensity.Clear();
	}

	public void OnHoldTypeChanged(EditorHoldNoteEvent hold)
	{
		if (hold.IsFake())
			return;
		if (hold.IsRoll())
		{
			RollCount++;
			HoldCount--;
		}
		else
		{
			RollCount--;
			HoldCount++;
		}
	}

	public void OnFakeTypeChanged(EditorEvent editorEvent)
	{
		if (!CanEventBeConsideredFake(editorEvent))
			return;
		if (!editorEvent.IsFake())
		{
			FakeCount--;
			OnEventAdded(editorEvent, false);
		}
		else
		{
			FakeCount++;
			OnEventDeleted(editorEvent, false);
		}
	}

	public int GetStepCount()
	{
		return StepCount;
	}

	public int GetNumRowsWithSteps()
	{
		return StepCountPerRow.Keys.Count;
	}

	public int GetNumStepsAtRow(int row)
	{
		if (StepCountPerRow.TryGetValue(row, out var steps))
			return steps;
		return 0;
	}

	public int[] GetStepCountByLane()
	{
		return StepCountsByLane;
	}

	public int GetHoldCount()
	{
		return HoldCount;
	}

	public int GetRollCount()
	{
		return RollCount;
	}

	public int GetMineCount()
	{
		return MineCount;
	}

	public int GetFakeCount()
	{
		return FakeCount;
	}

	public int GetLiftCount()
	{
		return LiftCount;
	}

	public int GetMultipliersCount()
	{
		return MultipliersCount;
	}

	public int GetNumPlayersWithNotes()
	{
		return NoteCountsPerPlayer.Keys.Count;
	}
}
