using Fumen.Converters;
using StepManiaEditor;
using static StepManiaEditorTests.Utils;

namespace StepManiaEditorTests;

/// <summary>
/// Tests for fakes.
/// </summary>
[TestClass]
public class TestFakes
{
	private static EditorChart CreateTestChart()
	{
		var c = CreateEmptyTestChart();

		// Set a tempo that makes math easy.
		var tempo = c.GetRateAlteringEvents().FindEventAtRow<EditorTempoEvent>(0);
		tempo.DoubleValue = 60.0;
		return c;
	}

	private static void AssertFake(EditorEvent editorEvent, bool fake)
	{
		Assert.AreEqual(fake, editorEvent.GetEditorChart().IsEventInFake(editorEvent));
		Assert.AreEqual(fake, editorEvent.IsFakeDueToRow());
		Assert.AreEqual(fake, editorEvent.IsFake());
	}

	[TestMethod]
	public void Test_NoFakes()
	{
		var c = CreateTestChart();
		Assert.AreEqual(0, c.GetFakes().GetCount());
	}

	[TestMethod]
	public void Test_FakeSegment()
	{
		var c = CreateTestChart();
		const int row = SMCommon.RowsPerMeasure;
		const double time = 1.0;
		const int len = SMCommon.MaxValidDenominator;
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateFakeConfig(c, row, time)));
		Assert.AreEqual(1, c.GetFakes().GetCount());

		var eventBeforeFake = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row - 1, 0));
		c.AddEvent(eventBeforeFake);
		AssertFake(eventBeforeFake, false);

		var eventAtSameRowAsFake = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, 0));
		c.AddEvent(eventAtSameRowAsFake);
		AssertFake(eventAtSameRowAsFake, true);

		var eventWithinFake = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + 1, 0));
		c.AddEvent(eventWithinFake);
		AssertFake(eventWithinFake, true);

		var eventAtSameRowAsFakeEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + len, 0));
		c.AddEvent(eventAtSameRowAsFakeEnd);
		AssertFake(eventAtSameRowAsFakeEnd, false);

		var eventAfterFakeEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + len + 1, 0));
		c.AddEvent(eventAfterFakeEnd);
		AssertFake(eventAfterFakeEnd, false);
	}

	[TestMethod]
	public void Test_Warp()
	{
		var c = CreateTestChart();
		const int row = SMCommon.RowsPerMeasure;
		const int len = SMCommon.MaxValidDenominator;
		// ReSharper disable once RedundantArgumentDefaultValue
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(c, row, len)));
		Assert.AreEqual(1, c.GetWarps().GetCount());

		var eventBeforeWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row - 1, 0));
		c.AddEvent(eventBeforeWarp);
		AssertFake(eventBeforeWarp, false);

		var eventAtSameRowAsWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, 0));
		c.AddEvent(eventAtSameRowAsWarp);
		AssertFake(eventAtSameRowAsWarp, true);

		var eventWithinWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + 1, 0));
		c.AddEvent(eventWithinWarp);
		AssertFake(eventWithinWarp, true);

		var eventAtSameRowAsWarpEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + len, 0));
		c.AddEvent(eventAtSameRowAsWarpEnd);
		AssertFake(eventAtSameRowAsWarpEnd, false);

		var eventAfterWarpEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + len + 1, 0));
		c.AddEvent(eventAfterWarpEnd);
		AssertFake(eventAfterWarpEnd, false);
	}

	[TestMethod]
	public void Test_NegativeStop()
	{
		var c = CreateTestChart();
		const int row = SMCommon.RowsPerMeasure;
		const double time = -1.0;
		const int len = SMCommon.MaxValidDenominator;
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, time)));
		Assert.AreEqual(1, c.GetStops().GetCount());

		var eventBeforeNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row - 1, 0));
		c.AddEvent(eventBeforeNegativeStop);
		AssertFake(eventBeforeNegativeStop, false);

		var eventAtSameRowAsNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, 0));
		c.AddEvent(eventAtSameRowAsNegativeStop);
		AssertFake(eventAtSameRowAsNegativeStop, true);

		var eventWithinNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + 1, 0));
		c.AddEvent(eventWithinNegativeStop);
		AssertFake(eventWithinNegativeStop, true);

		var eventAtSameRowAsNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + len, 0));
		c.AddEvent(eventAtSameRowAsNegativeStopEnd);
		AssertFake(eventAtSameRowAsNegativeStopEnd, false);

		var eventAfterNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + len + 1, 0));
		c.AddEvent(eventAfterNegativeStopEnd);
		AssertFake(eventAfterNegativeStopEnd, false);
	}

	[TestMethod]
	public void Test_WarpWithSimultaneousStop()
	{
		var c = CreateTestChart();
		const int row = SMCommon.RowsPerMeasure;
		const int warpLen = SMCommon.MaxValidDenominator;
		const double stopTime = 1.0;
		// ReSharper disable once RedundantArgumentDefaultValue
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(c, row, warpLen)));
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, stopTime)));
		Assert.AreEqual(1, c.GetWarps().GetCount());
		Assert.AreEqual(1, c.GetStops().GetCount());

		var eventBeforeWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row - 1, 0));
		c.AddEvent(eventBeforeWarp);
		AssertFake(eventBeforeWarp, false);

		// Events at the same time as warps that are coincident with stops are not fake.
		var eventAtSameRowAsWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, 0));
		c.AddEvent(eventAtSameRowAsWarp);
		AssertFake(eventAtSameRowAsWarp, false);

		var eventWithinWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + 1, 0));
		c.AddEvent(eventWithinWarp);
		AssertFake(eventWithinWarp, true);

		var eventAtSameRowAsWarpEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + warpLen, 0));
		c.AddEvent(eventAtSameRowAsWarpEnd);
		AssertFake(eventAtSameRowAsWarpEnd, false);

		var eventAfterWarpEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + warpLen + 1, 0));
		c.AddEvent(eventAfterWarpEnd);
		AssertFake(eventAfterWarpEnd, false);
	}

	[TestMethod]
	public void Test_WarpWithSimultaneousDelay()
	{
		var c = CreateTestChart();
		const int row = SMCommon.RowsPerMeasure;
		const int warpLen = SMCommon.MaxValidDenominator;
		const double stopTime = 1.0;
		// ReSharper disable once RedundantArgumentDefaultValue
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(c, row, warpLen)));
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateDelayConfig(c, row, stopTime)));
		Assert.AreEqual(1, c.GetWarps().GetCount());
		Assert.AreEqual(1, c.GetDelays().GetCount());

		var eventBeforeWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row - 1, 0));
		c.AddEvent(eventBeforeWarp);
		AssertFake(eventBeforeWarp, false);

		// Events at the same time as warps that are coincident with delays are not fake.
		var eventAtSameRowAsWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, 0));
		c.AddEvent(eventAtSameRowAsWarp);
		AssertFake(eventAtSameRowAsWarp, false);

		var eventWithinWarp = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + 1, 0));
		c.AddEvent(eventWithinWarp);
		AssertFake(eventWithinWarp, true);

		var eventAtSameRowAsWarpEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + warpLen, 0));
		c.AddEvent(eventAtSameRowAsWarpEnd);
		AssertFake(eventAtSameRowAsWarpEnd, false);

		var eventAfterWarpEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + warpLen + 1, 0));
		c.AddEvent(eventAfterWarpEnd);
		AssertFake(eventAfterWarpEnd, false);
	}

	[TestMethod]
	public void Test_NegativeStopWithSimultaneousDelay()
	{
		var c = CreateTestChart();
		const int row = SMCommon.RowsPerMeasure;
		const int stopLen = SMCommon.MaxValidDenominator;
		const double stopTime = 1.0;
		const double delayTime = 1.0;
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, row, -stopTime)));
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateDelayConfig(c, row, delayTime)));
		Assert.AreEqual(1, c.GetStops().GetCount());
		Assert.AreEqual(1, c.GetDelays().GetCount());

		var eventBeforeNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row - 1, 0));
		c.AddEvent(eventBeforeNegativeStop);
		AssertFake(eventBeforeNegativeStop, false);

		// Events at the same time as negative stops that are coincident with delays are not fake.
		var eventAtSameRowAsNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, 0));
		c.AddEvent(eventAtSameRowAsNegativeStop);
		AssertFake(eventAtSameRowAsNegativeStop, false);

		var eventWithinNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + 1, 0));
		c.AddEvent(eventWithinNegativeStop);
		AssertFake(eventWithinNegativeStop, true);

		var eventAtSameRowAsNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + stopLen, 0));
		c.AddEvent(eventAtSameRowAsNegativeStopEnd);
		AssertFake(eventAtSameRowAsNegativeStopEnd, false);

		var eventAfterNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + stopLen + 1, 0));
		c.AddEvent(eventAfterNegativeStopEnd);
		AssertFake(eventAfterNegativeStopEnd, false);
	}

	[TestMethod]
	public void Test_NegativeStopWithFollowingPositiveStopThatEndsAtSameTime()
	{
		var c = CreateTestChart();
		const int negativeStopRow = 0;
		const double negativeStopTime = 8.0;
		const int positiveStopRow = 4 * SMCommon.MaxValidDenominator;
		const double positiveStopTime = 2.0;
		const int bothStopEndRow = 6 * SMCommon.MaxValidDenominator;
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, negativeStopRow, -negativeStopTime)));
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, positiveStopRow, positiveStopTime)));
		Assert.AreEqual(2, c.GetStops().GetCount());

		var eventBeforeNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow - 1, 0));
		c.AddEvent(eventBeforeNegativeStop);
		AssertFake(eventBeforeNegativeStop, false);

		var eventAtSameRowAsNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow, 0));
		c.AddEvent(eventAtSameRowAsNegativeStop);
		AssertFake(eventAtSameRowAsNegativeStop, true);

		var eventWithinNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow + 1, 0));
		c.AddEvent(eventWithinNegativeStop);
		AssertFake(eventWithinNegativeStop, true);

		var eventAtPositionOfPositiveStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, positiveStopRow, 0));
		c.AddEvent(eventAtPositionOfPositiveStop);
		AssertFake(eventAtPositionOfPositiveStop, true);

		var eventAtSameRowAsStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, bothStopEndRow, 0));
		c.AddEvent(eventAtSameRowAsStopEnd);
		AssertFake(eventAtSameRowAsStopEnd, false);

		var eventAfterNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, bothStopEndRow + 1, 0));
		c.AddEvent(eventAfterNegativeStopEnd);
		AssertFake(eventAfterNegativeStopEnd, false);
	}

	[TestMethod]
	public void Test_NegativeStopWithFollowingPositiveStopThatEndsEarlier()
	{
		var c = CreateTestChart();
		const int negativeStopRow = 0;
		const double negativeStopTime = 8.0;
		const int positiveStopRow = 3 * SMCommon.MaxValidDenominator;
		const double positiveStopTime = 2.0;
		const int positiveStopEndRow = 5 * SMCommon.MaxValidDenominator;
		const int negativeStopEndRow = 6 * SMCommon.MaxValidDenominator;
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, negativeStopRow, -negativeStopTime)));
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, positiveStopRow, positiveStopTime)));
		Assert.AreEqual(2, c.GetStops().GetCount());

		var eventBeforeNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow - 1, 0));
		c.AddEvent(eventBeforeNegativeStop);
		AssertFake(eventBeforeNegativeStop, false);

		var eventAtSameRowAsNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow, 0));
		c.AddEvent(eventAtSameRowAsNegativeStop);
		AssertFake(eventAtSameRowAsNegativeStop, true);

		var eventWithinNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow + 1, 0));
		c.AddEvent(eventWithinNegativeStop);
		AssertFake(eventWithinNegativeStop, true);

		var eventAtPositionOfPositiveStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, positiveStopRow, 0));
		c.AddEvent(eventAtPositionOfPositiveStop);
		AssertFake(eventAtPositionOfPositiveStop, true);

		var eventAtSameRowAsPositiveStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, positiveStopEndRow, 0));
		c.AddEvent(eventAtSameRowAsPositiveStopEnd);
		AssertFake(eventAtSameRowAsPositiveStopEnd, true);

		var eventAtSameRowAsNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopEndRow, 0));
		c.AddEvent(eventAtSameRowAsNegativeStopEnd);
		AssertFake(eventAtSameRowAsNegativeStopEnd, false);

		var eventAfterNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopEndRow + 1, 0));
		c.AddEvent(eventAfterNegativeStopEnd);
		AssertFake(eventAfterNegativeStopEnd, false);
	}

	[TestMethod]
	public void Test_NegativeStopWithFollowingPositiveStopThatEndsLater()
	{
		var c = CreateTestChart();
		const int negativeStopRow = 0;
		const double negativeStopTime = 8.0;
		const int positiveStopRow = 7 * SMCommon.MaxValidDenominator;
		const double positiveStopTime = 2.0;
		const int stopEndRow = 7 * SMCommon.MaxValidDenominator;
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, negativeStopRow, -negativeStopTime)));
		c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateStopConfig(c, positiveStopRow, positiveStopTime)));
		Assert.AreEqual(2, c.GetStops().GetCount());

		var eventBeforeNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow - 1, 0));
		c.AddEvent(eventBeforeNegativeStop);
		AssertFake(eventBeforeNegativeStop, false);

		var eventAtSameRowAsNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow, 0));
		c.AddEvent(eventAtSameRowAsNegativeStop);
		AssertFake(eventAtSameRowAsNegativeStop, true);

		var eventWithinNegativeStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, negativeStopRow + 1, 0));
		c.AddEvent(eventWithinNegativeStop);
		AssertFake(eventWithinNegativeStop, true);

		// The positive stop brings in the end of the negative stop causing it to be coincident with the positive row
		// start, and events which are coincident with negative stop ends are not warped over.
		var eventAtPositionOfPositiveStop = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, positiveStopRow, 0));
		c.AddEvent(eventAtPositionOfPositiveStop);
		AssertFake(eventAtPositionOfPositiveStop, false);

		var eventAtSameRowAsPositiveStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, stopEndRow, 0));
		c.AddEvent(eventAtSameRowAsPositiveStopEnd);
		AssertFake(eventAtSameRowAsPositiveStopEnd, false);

		var eventAfterNegativeStopEnd = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, stopEndRow + 1, 0));
		c.AddEvent(eventAfterNegativeStopEnd);
		AssertFake(eventAfterNegativeStopEnd, false);
	}
}
