using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Readonly interface for StepTotals.
/// </summary>
internal interface IReadOnlyStepTotals
{
	public int GetStepCount();
	public int GetNumRowsWithSteps();
	public int[] GetStepCountByLane();
	public int GetHoldCount();

	public int GetRollCount();
	public int GetMineCount();

	public int GetFakeCount();
	public int GetLiftCount();

	public int GetMultipliersCount();
}

/// <summary>
/// Cached totals for various types of steps in an EditorChart.
/// </summary>
internal sealed class StepTotals : IReadOnlyStepTotals
{
	/// <summary>
	/// Total step counts by lane for the EditorChart.
	/// </summary>
	private int[] StepCountsByLane;

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
	/// Constructor.
	/// </summary>
	/// <param name="editorChart">EditorChart to cache totals for.</param>
	public StepTotals(EditorChart editorChart)
	{
		EditorChart = editorChart;
		Clear();
	}

	public void Clear()
	{
		StepCount = 0;
		HoldCount = 0;
		RollCount = 0;
		MineCount = 0;
		FakeCount = 0;
		LiftCount = 0;
		MultipliersCount = 0;
		StepCountPerRow.Clear();
		StepCountsByLane = new int[EditorChart.NumInputs];
		for (var a = 0; a < EditorChart.NumInputs; a++)
			StepCountsByLane[a] = 0;
	}

	public void OnEventAdded(EditorEvent editorEvent)
	{
		var isStep = false;
		switch (editorEvent)
		{
			case EditorTapNoteEvent:
				if (editorEvent.IsFake())
					FakeCount++;
				else
					isStep = true;
				break;
			case EditorHoldNoteEvent hold:
				if (editorEvent.IsFake())
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
				if (editorEvent.IsFake())
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

		if (isStep)
		{
			StepCount++;
			StepCountsByLane[editorEvent.GetLane()]++;
			IncrementStepCountPerRow(editorEvent.GetRow());
		}
	}

	private void IncrementStepCountPerRow(int row)
	{
		if (!StepCountPerRow.ContainsKey(row))
			StepCountPerRow[row] = 1;
		else
			StepCountPerRow[row]++;
	}

	public void OnEventDeleted(EditorEvent editorEvent)
	{
		var isStep = false;
		switch (editorEvent)
		{
			case EditorTapNoteEvent:
				if (editorEvent.IsFake())
					FakeCount--;
				else
					isStep = true;
				break;
			case EditorHoldNoteEvent hold:
				if (editorEvent.IsFake())
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
				if (editorEvent.IsFake())
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

		if (isStep)
		{
			StepCount--;
			StepCountsByLane[editorEvent.GetLane()]--;
			DecrementStepCountPerRow(editorEvent.GetRow());
		}
	}

	private void DecrementStepCountPerRow(int row)
	{
		StepCountPerRow[row]--;
		if (StepCountPerRow[row] == 0)
			StepCountPerRow.Remove(row);
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
		if (!editorEvent.IsFake())
		{
			FakeCount--;
			OnEventAdded(editorEvent);
		}
		else
		{
			FakeCount++;
			OnEventDeleted(editorEvent);
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
}
