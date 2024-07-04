using Fumen;
using Fumen.Converters;
using StepManiaEditor;
using static StepManiaEditorTests.Utils;

namespace StepManiaEditorTests;

/// <summary>
/// Tests for TestActionChangeWarpsToNegativeStops.
/// </summary>
[TestClass]
public class TestActionChangeWarpsToNegativeStops
{
	private static EditorChart CreateTestChart()
	{
		var c = CreateEmptyTestChart();

		// Set a tempo that makes math easy.
		var tempo = c.GetRateAlteringEvents().FindEventAtRow<EditorTempoEvent>(0);
		tempo.DoubleValue = 60.0;
		return c;
	}

	[TestMethod]
	public void Test_NoWarpsNoStops()
	{
		var c = CreateTestChart();
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(null, c));
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());
	}

	[TestMethod]
	public void Test_ZeroLengthWarpsAreConverted()
	{
		var c = CreateTestChart();
		var row = SMCommon.RowsPerMeasure;
		var warpLength = 0;
		var warp = (EditorWarpEvent)EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(c, row, warpLength));
		c.AddEvent(warp);
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(1, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(null, c));
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		var stopEnum = c.GetStops().First();
		stopEnum.MoveNext();
		var foundStop = stopEnum.Current;
		Assert.AreEqual(row, foundStop!.GetRow());
		Assert.IsTrue(foundStop.GetStopLengthSeconds().DoubleEquals(0.0));
	}

	[TestMethod]
	public void Test_WarpsWithSimultaneousStopsMaintainOriginalStops()
	{
		var c = CreateTestChart();
		var row = SMCommon.RowsPerMeasure;
		var warpLength = SMCommon.MaxValidDenominator;
		var warp = (EditorWarpEvent)EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(c, row, warpLength));
		c.AddEvent(warp);
		var stopLength = 4.0;
		var stop = (EditorStopEvent)EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, stopLength));
		c.AddEvent(stop);
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(1, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(null, c));
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		var stopEnum = c.GetStops().First();
		stopEnum.MoveNext();
		var foundStop = stopEnum.Current;
		Assert.AreEqual(row, foundStop!.GetRow());
		Assert.IsTrue(foundStop.GetStopLengthSeconds().DoubleEquals(stopLength));
	}

	[TestMethod]
	public void Test_WarpsChangeToNegativeStops()
	{
		var c = CreateTestChart();
		var row = SMCommon.RowsPerMeasure;
		var warpLength = SMCommon.MaxValidDenominator;
		var warp = (EditorWarpEvent)EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(c, row, warpLength));
		c.AddEvent(warp);
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(1, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(null, c));
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		var stopEnum = c.GetStops().First();
		stopEnum.MoveNext();
		var foundStop = stopEnum.Current;
		Assert.AreEqual(row, foundStop!.GetRow());
		Assert.IsTrue(foundStop.GetStopLengthSeconds().DoubleEquals(-1.0));
	}
}