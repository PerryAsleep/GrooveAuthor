using Fumen;
using Fumen.Converters;
using StepManiaEditor;
using static StepManiaEditorTests.Utils;

namespace StepManiaEditorTests;

/// <summary>
/// Tests for TestActionChangeNegativeStopsToWarps.
/// </summary>
[TestClass]
public class TestActionChangeNegativeStopsToWarps
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
	public void Test_NoStopsNoWarps()
	{
		var c = CreateTestChart();
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(null, c));
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());
	}

	[TestMethod]
	public void Test_PositiveStopsAreNotConverted()
	{
		var c = CreateTestChart();
		var row = SMCommon.RowsPerMeasure;
		var stopTime = 1.0;
		var stop = (EditorStopEvent)EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, stopTime));
		c.AddEvent(stop);
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(null, c));
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		var stopEnum = c.GetStops().First();
		stopEnum.MoveNext();
		var foundStop = stopEnum.Current;
		Assert.AreEqual(stop, foundStop);
		Assert.AreEqual(row, foundStop!.GetRow());
		Assert.IsTrue(stop.GetStopLengthSeconds().DoubleEquals(stopTime));
	}

	[TestMethod]
	public void Test_ZeroLengthStopsAreNotConverted()
	{
		var c = CreateTestChart();
		var row = SMCommon.RowsPerMeasure;
		var stopTime = 0.0;
		var stop = (EditorStopEvent)EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, stopTime));
		c.AddEvent(stop);
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(null, c));
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		var stopEnum = c.GetStops().First();
		stopEnum.MoveNext();
		var foundStop = stopEnum.Current;
		Assert.AreEqual(stop, foundStop);
		Assert.AreEqual(row, foundStop!.GetRow());
		Assert.IsTrue(stop.GetStopLengthSeconds().DoubleEquals(stopTime));
	}

	[TestMethod]
	public void Test_NegativeStopsWithSimultaneousWrapsMaintainOriginalWarps()
	{
		var c = CreateTestChart();
		var row = SMCommon.RowsPerMeasure;
		var stop = (EditorStopEvent)EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, -1.0));
		c.AddEvent(stop);
		var warpLength = SMCommon.MaxValidDenominator * 2;
		var warp = (EditorWarpEvent)EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(c, row, warpLength));
		c.AddEvent(warp);
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(1, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(null, c));
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(1, c.GetWarps().GetCount());

		var warpEnum = c.GetWarps().First();
		warpEnum.MoveNext();
		var foundWarp = warpEnum.Current;
		Assert.AreEqual(warp, foundWarp);
		Assert.AreEqual(row, foundWarp!.GetRow());
		Assert.AreEqual(warpLength, foundWarp.GetWarpLengthRows());
	}

	[TestMethod]
	public void Test_NegativeStopsChangeToWarps()
	{
		var c = CreateTestChart();
		var row = SMCommon.RowsPerMeasure;
		var stop = (EditorStopEvent)EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, -1.0));
		c.AddEvent(stop);
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(0, c.GetWarps().GetCount());

		ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(null, c));
		Assert.AreEqual(0, c.GetStops().GetCount());
		Assert.AreEqual(1, c.GetWarps().GetCount());

		var warpEnum = c.GetWarps().First();
		warpEnum.MoveNext();
		var foundWarp = warpEnum.Current;
		Assert.AreEqual(row, foundWarp!.GetRow());
		Assert.AreEqual(SMCommon.MaxValidDenominator, foundWarp.GetWarpLengthRows());
	}
}