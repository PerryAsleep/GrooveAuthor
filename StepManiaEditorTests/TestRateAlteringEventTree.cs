using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using StepManiaEditor;
using static StepManiaEditorTests.Utils;

namespace StepManiaEditorTests;

/// <summary>
/// Tests for RateAlteringEventTree.
/// </summary>
[TestClass]
public class TestRateAlteringEventTree
{
	/// <summary>
	/// Creates a test chart to use for testing a RateAlteringEventTree.
	/// This chart will exhaust all possible simultaneous combinations of rate altering events.
	/// It will also have tap notes at and between every row of rate altering events.
	/// </summary>
	/// <param name="eventTypeStrings">
	/// List of strings identifying the types of events to produce simultaneous combinations of
	/// for insertion into the chart. See GetEventTypeStrings.
	/// </param>
	/// <returns>New test EditorChart.</returns>
	private static EditorChart CreateTestChart(IReadOnlyList<string> eventTypeStrings)
	{
		// Create a blank chart. It will have some default events at row 0.
		var s = new EditorSong(null, null);
		var c = new EditorChart(s, SMCommon.ChartType.dance_single);

		// At taps at row 0 and halfway to the first combination row.
		for (var l = 0; l < c.NumInputs; l++)
		{
			c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, 0, l)));
			c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, SMCommon.RowsPerMeasure >> 1, l)));
		}

		// Loop over each combination of events. We will add each combination at a distinct row.
		var numEventTypeStrings = eventTypeStrings.Count;
		var lastCombination = (1 << numEventTypeStrings) - 1;
		for (var combination = 0; combination <= lastCombination; combination++)
		{
			// Add the events for this combination at this row.
			var row = (combination + 1) * SMCommon.RowsPerMeasure;
			for (var typeIndex = 0; typeIndex < numEventTypeStrings; typeIndex++)
			{
				var shouldAddType = (combination >> typeIndex) % 2 == 1;
				if (shouldAddType)
				{
					EventConfig config = null;
					switch (eventTypeStrings[typeIndex])
					{
						case nameof(TimeSignature):
							config = EventConfig.CreateTimeSignatureConfig(c, row,
								new Fraction(SMCommon.NumBeatsPerMeasure, SMCommon.NumBeatsPerMeasure));
							break;
						case nameof(Tempo):
							config = EventConfig.CreateTempoConfig(c, row);
							break;
						case nameof(TickCount):
							config = EventConfig.CreateTickCountConfig(c, row);
							break;
						case nameof(FakeSegment):
							config = EventConfig.CreateFakeConfig(c, row, 1.0);
							break;
						case nameof(Multipliers):
							config = EventConfig.CreateMultipliersConfig(c, row);
							break;
						case nameof(Label):
							config = EventConfig.CreateLabelConfig(c, row);
							break;
						case SMCommon.DelayString:
							config = EventConfig.CreateDelayConfig(c, row, 1.0);
							break;
						case nameof(ScrollRate):
							config = EventConfig.CreateScrollRateConfig(c, row);
							break;
						case nameof(ScrollRateInterpolation):
							config = EventConfig.CreateScrollRateInterpolationConfig(c, row);
							break;
						case nameof(Stop):
							config = EventConfig.CreateStopConfig(c, row, 1.0);
							break;
						case nameof(Warp):
							config = EventConfig.CreateWarpConfig(c, row);
							break;
						default:
							Assert.Fail();
							break;
					}

					if (config != null)
						c.AddEvent(EditorEvent.CreateEvent(config));
				}
			}

			// Also add taps at the row and halfway to the next row with events.
			for (var l = 0; l < c.NumInputs; l++)
			{
				c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, l)));
				c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row + (SMCommon.RowsPerMeasure >> 1), l)));
			}
		}

		AssertEventsAreInOrder(c);
		return c;
	}

	/// <summary>
	/// Gets a list of event type strings to use for producing simultaneous combinations of
	/// events in a test chart.
	/// </summary>
	private static List<string> GetEventTypeStrings()
	{
		var eventTypeStrings = new List<string>();
		foreach (var eventTypeString in SMCommon.SMEventComparer.SMEventOrderList)
		{
			switch (eventTypeString)
			{
				// Don't add steps, we'll add them per row.
				case nameof(LaneTapNote):
				case nameof(LaneHoldStartNote):
				case nameof(LaneHoldEndNote):
				case nameof(LaneNote):
					continue;

				// Don't add negative stops. We consider them the same as stops.
				case SMCommon.NegativeStopString:
				case SMCommon.NegativeDelayString:
					continue;

				// In order to cut down on the volume of combination, skip some events which aren't important.
				case nameof(TimeSignature):
				case nameof(TickCount):
				case nameof(FakeSegment):
				case nameof(Multipliers):
				case nameof(Label):
					continue;
			}

			eventTypeStrings.Add(eventTypeString);
		}

		return eventTypeStrings;
	}

	/// <summary>
	/// Given a test chart created with CreateTestChart and the given eventTypeStrings, return the
	/// events which are expected to be present in a RateAlteringEventTree at the given row.
	/// These events will be in order.
	/// </summary>
	/// <param name="row">Row in question.</param>
	/// <param name="eventTypeStrings">
	/// List of strings identifying the types of events used to produce simultaneous combinations
	/// through CreateTestChart. See GetEventTypeStrings.
	/// </param>
	/// <returns>Expected rate altering events for the given row.</returns>
	private static List<string> GetExpectedRateAlteringEventTypesAtRow(int row, IReadOnlyList<string> eventTypeStrings)
	{
		// At row 0 we have the default events that are created with all charts.
		if (row == 0)
		{
			return new List<string>
			{
				nameof(TimeSignature),
				nameof(Tempo),
				nameof(ScrollRate),
			};
		}

		var expectedEventTypes = new List<string>();
		if (row <= 0 || row % SMCommon.RowsPerMeasure != 0)
			return expectedEventTypes;
		var numEventTypeStrings = eventTypeStrings.Count;
		var combination = row / SMCommon.RowsPerMeasure - 1;
		var lastCombination = (1 << numEventTypeStrings) - 1;
		if (combination > lastCombination)
			return expectedEventTypes;
		for (var typeIndex = 0; typeIndex < numEventTypeStrings; typeIndex++)
		{
			if ((combination >> typeIndex) % 2 == 1)
			{
				// Don't include events which aren't in the RateAlteringEventTree.
				switch (eventTypeStrings[typeIndex])
				{
					case nameof(TickCount):
					case nameof(FakeSegment):
					case nameof(Multipliers):
					case nameof(Label):
					case nameof(LaneTapNote):
					case nameof(LaneHoldStartNote):
					case nameof(LaneHoldEndNote):
					case nameof(LaneNote):
					case nameof(ScrollRateInterpolation):
						continue;
				}

				expectedEventTypes.Add(eventTypeStrings[typeIndex]);
			}
		}

		return expectedEventTypes;
	}

	/// <summary>
	/// Given an EditorEvent return the expected type string for validation.
	/// See GetEventTypeStrings.
	/// </summary>
	/// <param name="e">EditorEvent in question.</param>
	/// <returns>Type string to use.</returns>
	private static string GetEventTypeName(EditorEvent e)
	{
		if (e is EditorDelayEvent)
			return SMCommon.DelayString;
		if (e is EditorStopEvent s && s.StopEvent.LengthSeconds < 0.0)
			return SMCommon.NegativeStopString;
		return e.GetEvent().GetType().Name;
	}

	/// <summary>
	/// Test all Find methods in a RateAlteringEventTree.
	/// It would be unwieldy to construct all the possible scenarios by hand so this
	/// method creates a test chart with all combinations of simultaneous rate altering
	/// events and then checks around each one. The scaffolding to create and loop over
	/// the test chart is significant enough that it is simplest to keep this in one
	/// test function.
	/// </summary>
	[TestMethod]
	public void TestFind()
	{
		// Set up a test chart with all possible combinations of simultaneous rate altering events.
		var eventTypeStrings = GetEventTypeStrings();
		var c = CreateTestChart(eventTypeStrings);

		// At the start of the chart we expect these rate altering events.
		var expectedPreviousRateAlteringEventTypes = new List<string>
		{
			nameof(TimeSignature),
			nameof(Tempo),
			nameof(ScrollRate),
		};
		var expectedPreviousRow = 0;

		var numEventTypeStrings = eventTypeStrings.Count;
		var lastCombination = (1 << numEventTypeStrings) - 1;
		var lastRowWithRateAlteringEvents = SMCommon.RowsPerMeasure * lastCombination;
		var firstRowWithTaps = 0;
		var lastRowWithTaps = lastRowWithRateAlteringEvents + (SMCommon.RowsPerMeasure >> 1);
		var firstRowToCheck = -SMCommon.RowsPerMeasure;
		var finalRowToCheck = lastRowWithRateAlteringEvents + SMCommon.RowsPerMeasure;

		// Search rows starting before the chart begins, and extend beyond the end of the chart, and
		// exhaust every combination of simultaneous rate altering events at a row.
		// Also include rows between rate altering events.
		for (var row = firstRowToCheck; row <= finalRowToCheck; row += SMCommon.RowsPerMeasure >> 1)
		{
			var expectedRateAlteringEventTypesAtRow = GetExpectedRateAlteringEventTypesAtRow(row, eventTypeStrings);

			// Test FindBestByPosition
			{
				// FindBestByPosition should return the greatest preceding rate altering event or if there
				// is no previous event, the least event from the current or following rows.
				var foundEnumerator = c.GetRateAlteringEvents().FindBestByPosition(row);
				Assert.IsNotNull(foundEnumerator);
				Assert.IsTrue(foundEnumerator.MoveNext());
				Assert.AreEqual(expectedPreviousRow, foundEnumerator.Current!.GetRow());
				if (row <= 0)
					Assert.AreEqual(expectedPreviousRateAlteringEventTypes[0], GetEventTypeName(foundEnumerator.Current));
				else
					Assert.AreEqual(expectedPreviousRateAlteringEventTypes[^1], GetEventTypeName(foundEnumerator.Current));
			}

			// Test FindActiveRateAlteringEventForPosition and FindActiveRateAlteringEventEnumeratorForPosition
			{
				// FindActiveRateAlteringEventForPosition with allowEqualTo false should return the greatest
				// preceding rate altering event or if there is no previous event, the least event from the
				// current or following rows.
				var foundEvent = c.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(row, false);
				var foundEnumerator = c.GetRateAlteringEvents().FindActiveRateAlteringEventEnumeratorForPosition(row, false);
				Assert.IsNotNull(foundEvent);
				Assert.IsNotNull(foundEnumerator);
				Assert.IsTrue(foundEnumerator.MoveNext());
				Assert.AreEqual(foundEnumerator.Current, foundEvent);
				Assert.AreEqual(expectedPreviousRow, foundEvent.GetRow());
				if (row <= 0)
					Assert.AreEqual(expectedPreviousRateAlteringEventTypes[0], GetEventTypeName(foundEvent));
				else
					Assert.AreEqual(expectedPreviousRateAlteringEventTypes[^1], GetEventTypeName(foundEvent));

				// FindActiveRateAlteringEventForPosition with allowEqualTo true should return the greatest
				// rate altering event that has a row which is equal to or precedes the given row. If there
				// is no such event, it should return the least event from the following rows.
				foundEvent = c.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(row);
				foundEnumerator = c.GetRateAlteringEvents().FindActiveRateAlteringEventEnumeratorForPosition(row);
				Assert.IsNotNull(foundEvent);
				Assert.IsNotNull(foundEnumerator);
				Assert.IsTrue(foundEnumerator.MoveNext());
				Assert.AreEqual(foundEnumerator.Current, foundEvent);
				// This row has rate altering events.
				if (expectedRateAlteringEventTypesAtRow.Count > 0)
				{
					Assert.AreEqual(row, foundEvent.GetRow());
					Assert.AreEqual(expectedRateAlteringEventTypesAtRow[^1], GetEventTypeName(foundEvent));
				}
				// This row has no rate altering events.
				else
				{
					Assert.AreEqual(expectedPreviousRow, foundEvent.GetRow());
					if (row < 0)
						Assert.AreEqual(expectedPreviousRateAlteringEventTypes[0], GetEventTypeName(foundEvent));
					else
						Assert.AreEqual(expectedPreviousRateAlteringEventTypes[^1], GetEventTypeName(foundEvent));
				}
			}

			// Test FindActiveRateAlteringEvent(EditorEvent) and FindActiveRateAlteringEvent(Event)
			{
				// FindActiveRateAlteringEvent for taps should return the greatest preceding rate altering event.
				// Some events occurring on the same row as taps occur after the taps. These events should never
				// be considered the active rate altering event for the tap.
				if (row >= firstRowWithTaps && row <= lastRowWithTaps)
				{
					for (var l = 0; l < c.NumInputs; l++)
					{
						var tap = c.GetEvents().FindNoteAt(row, l, false);
						Assert.IsNotNull(tap);
						Assert.IsTrue(tap is EditorTapNoteEvent);

						// Determine which rate altering event we expect to be active for this event.
						string expectedRateAlteringEventType = null;
						var expectedRateAlteringEventRow = 0;
						if (expectedRateAlteringEventTypesAtRow.Count > 0)
						{
							var index = expectedRateAlteringEventTypesAtRow.Count - 1;
							while (index >= 0)
							{
								// These events occur after taps on the same lane.
								if (expectedRateAlteringEventTypesAtRow[index] == nameof(Warp)
								    || expectedRateAlteringEventTypesAtRow[index] == SMCommon.NegativeStopString
								    || expectedRateAlteringEventTypesAtRow[index] == nameof(Stop)
								    || expectedRateAlteringEventTypesAtRow[index] == nameof(ScrollRate))
								{
									index--;
									if (index < 0)
										break;
									continue;
								}

								expectedRateAlteringEventType = expectedRateAlteringEventTypesAtRow[index];
								expectedRateAlteringEventRow = row;
								break;
							}
						}

						if (expectedRateAlteringEventType == null)
						{
							expectedRateAlteringEventType = expectedPreviousRateAlteringEventTypes[^1];
							expectedRateAlteringEventRow = expectedPreviousRow;
						}

						// Assert that the found active rate altering event matches expectations.

						// Check FindActiveRateAlteringEvent(EditorEvent)
						var foundEvent = c.GetRateAlteringEvents().FindActiveRateAlteringEvent(tap);
						Assert.IsNotNull(foundEvent);
						Assert.AreEqual(expectedRateAlteringEventType, GetEventTypeName(foundEvent));
						Assert.AreEqual(expectedRateAlteringEventRow, foundEvent.GetRow());

						// Check FindActiveRateAlteringEvent(Event)
						foundEvent = c.GetRateAlteringEvents().FindActiveRateAlteringEvent(tap.GetEvent());
						Assert.IsNotNull(foundEvent);
						Assert.AreEqual(expectedRateAlteringEventType, GetEventTypeName(foundEvent));
						Assert.AreEqual(expectedRateAlteringEventRow, foundEvent.GetRow());
					}
				}

				// FindActiveRateAlteringEvent for one of the expected rate altering events should always
				// return the same event.
				if (expectedRateAlteringEventTypesAtRow.Count > 0)
				{
					var foundEvents = 0;
					var currentRowEnumerator = c.GetEvents().FindLeastAtOrAfterChartPosition(row);
					Assert.IsNotNull(currentRowEnumerator);
					while (currentRowEnumerator.MoveNext())
					{
						var currentEvent = currentRowEnumerator.Current;
						Assert.IsNotNull(currentEvent);
						if (currentEvent.GetRow() > row)
							break;

						if (expectedRateAlteringEventTypesAtRow.Contains(GetEventTypeName(currentEvent)))
						{
							// Check FindActiveRateAlteringEvent(EditorEvent)
							var foundEvent = c.GetRateAlteringEvents().FindActiveRateAlteringEvent(currentEvent);
							Assert.AreEqual(currentEvent, foundEvent);

							// Check FindActiveRateAlteringEvent(Event)
							foundEvent = c.GetRateAlteringEvents().FindActiveRateAlteringEvent(currentEvent.GetEvent());
							Assert.AreEqual(currentEvent, foundEvent);

							foundEvents++;
						}
					}

					Assert.AreEqual(expectedRateAlteringEventTypesAtRow.Count, foundEvents);
				}
			}

			// Record the expected rate altering events at this for future comparisons.
			if (expectedRateAlteringEventTypesAtRow.Count > 0)
			{
				expectedPreviousRateAlteringEventTypes = expectedRateAlteringEventTypesAtRow;
				expectedPreviousRow = row;
			}
		}

		// Test all the Find methods which take times given times of the events in the chart.
		foreach (var chartEvent in c.GetEvents())
		{
			ValidateFindByTimeMethods(c, chartEvent.GetChartTime());
		}

		// Test all the Find methods which take times given gradually increasing times.
		Assert.IsTrue(c.GetEvents().LastValue(out var lastEvent));
		var finalTimeToCheck = lastEvent.GetChartTime() + 1.0;
		for (var time = -1.0; time < finalTimeToCheck; time += 0.001)
		{
			ValidateFindByTimeMethods(c, time);
		}
	}

	/// <summary>
	/// Validate all find by time methods for the given time.
	/// </summary>
	/// <param name="chart">Test EditorChart.</param>
	/// <param name="time">Time to check.</param>
	private static void ValidateFindByTimeMethods(EditorChart chart, double time)
	{
		ValidateBestFindByTime(chart, time);
		ValidateFindActiveRateAlteringEventForTime(chart, time);
		ValidateFindActiveRateAlteringEventForTimeAllowEqualTo(chart, time);
	}

	/// <summary>
	/// Validate FindBestByTime.
	/// </summary>
	/// <param name="chart">Test EditorChart.</param>
	/// <param name="time">Time to check.</param>
	private static void ValidateBestFindByTime(EditorChart chart, double time)
	{
		var foundEnumerator = chart.GetRateAlteringEvents().FindBestByTime(time);
		Assert.IsNotNull(foundEnumerator);
		Assert.IsTrue(foundEnumerator.MoveNext());
		var foundEvent = foundEnumerator.Current!;
		if (foundEvent.GetChartTime() >= time)
		{
			// Ensure there is no event which precedes the found event.
			var temp = foundEnumerator.Clone();
			Assert.IsFalse(temp.MovePrev());
		}
		else
		{
			// Ensure there is no event which follows the found event and is also less than the target time.
			var temp = foundEnumerator.Clone();
			while (temp.MoveNext())
			{
				Assert.IsFalse(temp.Current!.GetChartTime() < time);
				if (temp.Current!.GetChartTime() >= time)
					break;
			}
		}
	}

	/// <summary>
	/// Validate FindActiveRateAlteringEventForTime and FindActiveRateAlteringEventEnumeratorForTime
	/// with allowEqualTo false.
	/// </summary>
	/// <param name="chart">Test EditorChart.</param>
	/// <param name="time">Time to check.</param>
	private static void ValidateFindActiveRateAlteringEventForTime(EditorChart chart, double time)
	{
		var foundEvent = chart.GetRateAlteringEvents().FindActiveRateAlteringEventForTime(time, false);
		Assert.IsNotNull(foundEvent);

		// Get an enumerator so we can check the surrounding events
		var foundEnumerator = chart.GetRateAlteringEvents().FindActiveRateAlteringEventEnumeratorForTime(time, false);
		Assert.IsNotNull(foundEnumerator);
		Assert.IsTrue(foundEnumerator.MoveNext());
		Assert.AreEqual(foundEnumerator.Current, foundEvent);
		if (foundEvent.GetChartTime() >= time)
		{
			// Ensure there is no event which precedes the found event.
			var temp = foundEnumerator.Clone();
			Assert.IsFalse(temp.MovePrev());
		}
		else
		{
			// Ensure there is no event which follows the found event and is also less than the target time.
			var temp = foundEnumerator.Clone();
			while (temp.MoveNext())
			{
				Assert.IsFalse(temp.Current!.GetChartTime() < time);
				if (temp.Current!.GetChartTime() >= time)
					break;
			}
		}
	}

	/// <summary>
	/// Validate FindActiveRateAlteringEventForTime and FindActiveRateAlteringEventEnumeratorForTime
	/// with allowEqualTo true.
	/// </summary>
	/// <param name="chart">Test EditorChart.</param>
	/// <param name="time">Time to check.</param>
	private static void ValidateFindActiveRateAlteringEventForTimeAllowEqualTo(EditorChart chart, double time)
	{
		var foundEvent = chart.GetRateAlteringEvents().FindActiveRateAlteringEventForTime(time);
		Assert.IsNotNull(foundEvent);

		// Get an enumerator so we can check the surrounding events
		var foundEnumerator = chart.GetRateAlteringEvents().FindActiveRateAlteringEventEnumeratorForTime(time);
		Assert.IsNotNull(foundEnumerator);
		Assert.IsTrue(foundEnumerator.MoveNext());
		Assert.AreEqual(foundEnumerator.Current, foundEvent);

		if (foundEvent.GetChartTime() > time)
		{
			// Ensure there is no event which precedes the found event.
			var temp = foundEnumerator.Clone();
			Assert.IsFalse(temp.MovePrev());
		}
		else
		{
			// Ensure there is no event which follows the found event and is also less than or equal to the target time.
			var temp = foundEnumerator.Clone();
			while (temp.MoveNext())
			{
				Assert.IsFalse(temp.Current!.GetChartTime() <= time);
				if (temp.Current!.GetChartTime() >= time)
					break;
			}
		}
	}
}
