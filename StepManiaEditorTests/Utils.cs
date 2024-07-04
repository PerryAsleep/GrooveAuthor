using Fumen.Converters;
using StepManiaEditor;
using static StepManiaLibrary.Constants;

namespace StepManiaEditorTests;

internal sealed class Utils
{
	internal static readonly Type[] ExpectedEmptyChartTypes = new[]
	{
		typeof(EditorPreviewRegionEvent),
		typeof(EditorTimeSignatureEvent),
		typeof(EditorTempoEvent),
		typeof(EditorTickCountEvent),
		typeof(EditorMultipliersEvent),
		typeof(EditorScrollRateEvent),
		typeof(EditorInterpolatedRateAlteringEvent),
	};

	internal static EditorChart CreateEmptyTestChart(SMCommon.ChartType chartType = SMCommon.ChartType.dance_single)
	{
		var s = new EditorSong(null, null);
		var c = new EditorChart(s, chartType);
		AssertEventsAreInOrder(c);
		return c;
	}

	internal static void AssertEventsAreInOrder(EditorChart chart)
	{
		var tree = chart.GetEvents();
		var enumerator = tree.First();
		var list = new List<EditorEvent>();
		while (enumerator != null && enumerator.MoveNext())
		{
			list.Add(enumerator.Current!);
		}

		var previousRow = 0;
		var laneNotes = new EditorEvent[chart.NumInputs];
		var eventsByTypeAtCurrentRow = new HashSet<Type>();
		for (var i = 0; i < list.Count; i++)
		{
			// Ensure events are sorted as expected.
			if (i > 0)
			{
				var previousBeforeThis = list[i - 1].CompareTo(list[i]) < 0;
				var thisAfterPrevious = list[i].CompareTo(list[i - 1]) > 0;
				Assert.IsTrue(previousBeforeThis);
				Assert.IsTrue(thisAfterPrevious);
			}

			if (i < list.Count - 1)
			{
				var thisBeforeNext = list[i].CompareTo(list[i + 1]) < 0;
				var nextAfterThis = list[i + 1].CompareTo(list[i]) > 0;
				Assert.IsTrue(thisBeforeNext);
				Assert.IsTrue(nextAfterThis);
			}

			// Ensure rows never decrease.
			var row = list[i].GetRow();
			Assert.IsTrue(row >= previousRow);

			// Update row tracking variables.
			if (row != previousRow)
			{
				for (var l = 0; l < chart.NumInputs; l++)
					laneNotes[l] = null;
				eventsByTypeAtCurrentRow.Clear();
			}

			// Ensure there aren't two events at the same row and lane.
			var lane = list[i].GetLane();
			if (lane != InvalidArrowIndex)
			{
				Assert.IsNull(laneNotes[lane]);
				laneNotes[lane] = list[i];
			}
			// Ensure there aren't two non-lane events at the same row with the same type.
			else
			{
				Assert.IsFalse(eventsByTypeAtCurrentRow.Contains(list[i].GetType()));
				eventsByTypeAtCurrentRow.Add(list[i].GetType());
			}

			previousRow = row;
		}
	}
}
