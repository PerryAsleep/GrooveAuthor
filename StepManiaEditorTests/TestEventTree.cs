using Fumen.Converters;
using StepManiaEditor;
using static StepManiaEditorTests.Utils;

namespace StepManiaEditorTests;

/// <summary>
/// Tests for EventTree.
/// </summary>
[TestClass]
public class TestEventTree
{
	private static EditorChart CreateEmptyTestChart()
	{
		var s = new EditorSong(null, null);
		var c = new EditorChart(s, SMCommon.ChartType.dance_single);
		AssertEventsAreInOrder(c);
		return c;
	}

	private static EditorChart CreateTestChartWithNotesAtRow(int row)
	{
		return CreateTestChartWithNotesAtRows(new List<int> { row });
	}

	private static EditorChart CreateTestChartWithNotesAtRows(IEnumerable<int> rows)
	{
		var s = new EditorSong(null, null);
		var c = new EditorChart(s, SMCommon.ChartType.dance_single);
		foreach (var row in rows)
			for (var l = 0; l < c.NumInputs; l++)
				c.AddEvent(EditorEvent.CreateEvent(EventConfig.CreateTapConfig(c, row, l)));
		AssertEventsAreInOrder(c);
		return c;
	}

	private static EditorChart CreateTestChartWithNotesAtRowsBeingEdited(IEnumerable<int> rows)
	{
		var s = new EditorSong(null, null);
		var c = new EditorChart(s, SMCommon.ChartType.dance_single);
		foreach (var row in rows)
		{
			for (var l = 0; l < c.NumInputs; l++)
			{
				var config = EventConfig.CreateTapConfig(c, row, l);
				config.IsBeingEdited = true;
				c.AddEvent(EditorEvent.CreateEvent(config));
			}
		}

		AssertEventsAreInOrder(c);
		return c;
	}

	[TestMethod]
	public void TestEmptyChart()
	{
		var c = CreateEmptyTestChart();
		var i = 0;
		foreach (var ce in c.GetEvents())
		{
			Assert.AreEqual(ExpectedEmptyChartTypes[i++], ce.GetType());
		}
	}

	[TestMethod]
	public void TestFindBestByPosition_BeforeFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRow(100);
		var e = c.GetEvents().FindBestByPosition(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindBestByPosition_AtFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRow(100);
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindBestByPosition(f.GetChartPosition());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindBestByPosition_AfterFirst_EqualToSecond_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRow(100);
		var e = c.GetEvents().FindBestByPosition(0.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindBestByPosition_AtRowWithMany_ReturnsBeforeRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindBestByPosition(100);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorInterpolatedRateAlteringEvent);
	}

	[TestMethod]
	public void TestFindBestByPosition_AfterRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindBestByPosition(101);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindBestByPosition_AtLast_ReturnsGreatestInPrecedingRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindBestByPosition(200);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindBestByPosition_AfterLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindBestByPosition(300);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartPosition_BeforeFirst_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestBeforeChartPosition(-1.0);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartPosition_AtFirst_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestBeforeChartPosition(f.GetChartPosition());
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartPosition_AfterFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestBeforeChartPosition(f.GetChartPosition() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartPosition_AtRowWithMany_ReturnsBeforeRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestBeforeChartPosition(100);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorInterpolatedRateAlteringEvent);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartPosition_AfterRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestBeforeChartPosition(101);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartPosition_AtLast_ReturnsGreatestInPreviousRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestBeforeChartPosition(200);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartPosition_AfterLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestBeforeChartPosition(300);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartPosition_BeforeFirst_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestAtOrBeforeChartPosition(-1.0);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartPosition_AtFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartPosition(f.GetChartPosition());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartPosition_AfterFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartPosition(f.GetChartPosition() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartPosition_AtRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestAtOrBeforeChartPosition(100);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartPosition_AfterRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestAtOrBeforeChartPosition(101);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartPosition_AtLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestAtOrBeforeChartPosition(200);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartPosition_AfterLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestAtOrBeforeChartPosition(300);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindLeastAfterChartPosition_BeforeFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAfterChartPosition(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindLeastAfterChartPosition_AtFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAfterChartPosition(f.GetChartPosition());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindLeastAfterChartPosition_AfterFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAfterChartPosition(f.GetChartPosition() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindLeastAfterChartPosition_AtRowWithMany_ReturnFirstInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAfterChartPosition(100);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAfterChartPosition_AfterRowWithMany_ReturnFirstInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAfterChartPosition(101);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAfterChartPosition_AtLast_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAfterChartPosition(200);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindLeastAfterChartPosition_AfterLast_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAfterChartPosition(300);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartPosition_BeforeFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAtOrAfterChartPosition(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartPosition_AtFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAtOrAfterChartPosition(f.GetChartPosition());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartPosition_AfterFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAtOrAfterChartPosition(f.GetChartPosition() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartPosition_AtRowWithMany_ReturnFirstInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAtOrAfterChartPosition(100);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartPosition_AfterRowWithMany_ReturnFirstInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAtOrAfterChartPosition(101);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartPosition_AtLast_ReturnsFirstInLastRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAtOrAfterChartPosition(200);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartPosition_AfterLast_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAfterChartPosition(300);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindBestByTime_BeforeFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRow(100);
		var e = c.GetEvents().FindBestByTime(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindBestByTime_AtFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRow(100);
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindBestByTime(f.GetChartTime());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindBestByTime_AfterFirst_EqualToSecond_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRow(100);
		var e = c.GetEvents().FindBestByTime(0.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindBestByTime_AtRowWithMany_ReturnsBeforeRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindBestByTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorInterpolatedRateAlteringEvent);
	}

	[TestMethod]
	public void TestFindBestByTime_AfterRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindBestByTime(time + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindBestByTime_AtLast_ReturnsGreatestInPrecedingRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(200, ref time));
		var e = c.GetEvents().FindBestByTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindBestByTime_AfterLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(300, ref time));
		var e = c.GetEvents().FindBestByTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartTime_BeforeFirst_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestBeforeChartTime(-1.0);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartTime_AtFirst_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestBeforeChartTime(f.GetChartTime());
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartTime_AfterFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestBeforeChartTime(f.GetChartTime() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartTime_AtRowWithMany_ReturnsBeforeRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindGreatestBeforeChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorInterpolatedRateAlteringEvent);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartTime_AfterRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindGreatestBeforeChartTime(time + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartTime_AtLast_ReturnsGreatestInPreviousRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(200, ref time));
		var e = c.GetEvents().FindGreatestBeforeChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestBeforeChartTime_AfterLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(300, ref time));
		var e = c.GetEvents().FindGreatestBeforeChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartTime_BeforeFirst_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindGreatestAtOrBeforeChartTime(-1.0);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartTime_AtFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartTime(f.GetChartTime());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartTime_AfterFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartTime(f.GetChartTime() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartTime_AtRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartTime_AfterRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartTime(time + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartTime_AtLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(200, ref time));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindGreatestAtOrBeforeChartTime_AfterLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(300, ref time));
		var e = c.GetEvents().FindGreatestAtOrBeforeChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindLeastAfterChartTime_BeforeFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAfterChartTime(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindLeastAfterChartTime_AtFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAfterChartTime(f.GetChartTime());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindLeastAfterChartTime_AfterFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAfterChartTime(f.GetChartTime() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindLeastAfterChartTime_AtRowWithMany_ReturnFirstInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindLeastAfterChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAfterChartTime_AfterRowWithMany_ReturnFirstInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindLeastAfterChartTime(time + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAfterChartTime_AtLast_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(200, ref time));
		var e = c.GetEvents().FindLeastAfterChartTime(time);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindLeastAfterChartTime_AfterLast_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(300, ref time));
		var e = c.GetEvents().FindLeastAfterChartTime(time);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartTime_BeforeFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindLeastAtOrAfterChartTime(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartTime_AtFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAtOrAfterChartTime(f.GetChartTime());
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartTime_AfterFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindLeastAtOrAfterChartTime(f.GetChartTime() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartTime_AtRowWithMany_ReturnFirstInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindLeastAtOrAfterChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartTime_AfterRowWithMany_ReturnFirstInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(100, ref time));
		var e = c.GetEvents().FindLeastAtOrAfterChartTime(time + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartTime_AtLast_ReturnsFirstInLastRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(200, ref time));
		var e = c.GetEvents().FindLeastAtOrAfterChartTime(time);
		Assert.IsNotNull(e);
		Assert.IsTrue(e.MoveNext());
		Assert.IsTrue(e.Current is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e.Current).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindLeastAtOrAfterChartTime_AfterLast_ReturnsNull()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var time = 0.0;
		Assert.IsTrue(c.TryGetTimeFromChartPosition(300, ref time));
		var e = c.GetEvents().FindLeastAfterChartTime(time);
		Assert.IsNull(e);
	}

	[TestMethod]
	public void TestFindPreviousEventWithLooping_BeforeFirst_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindPreviousEventWithLooping(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindPreviousEventWithLooping_AtFirst_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindPreviousEventWithLooping(f.GetChartPosition());
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindPreviousEventWithLooping_AfterFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindPreviousEventWithLooping(f.GetChartPosition() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindPreviousEventWithLooping_AtRowWithMany_ReturnsBeforeRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindPreviousEventWithLooping(100);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorInterpolatedRateAlteringEvent);
	}

	[TestMethod]
	public void TestFindPreviousEventWithLooping_AfterRowWithMany_ReturnsGreatestInRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindPreviousEventWithLooping(101);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindPreviousEventWithLooping_AtLast_ReturnsGreatestInPreviousRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindPreviousEventWithLooping(200);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetRow() == 100);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindPreviousEventWithLooping_AfterLast_ReturnsLast()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindPreviousEventWithLooping(300);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetLane() == c.NumInputs - 1);
	}

	[TestMethod]
	public void TestFindNextEventWithLooping_BeforeFirst_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindNextEventWithLooping(-1.0);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindNextEventWithLooping_AtFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindNextEventWithLooping(f.GetChartPosition());
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindNextEventWithLooping_AfterFirst_ReturnsSecond()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		Assert.IsTrue(c.GetEvents().FirstValue(out var f));
		var e = c.GetEvents().FindNextEventWithLooping(f.GetChartPosition() + 0.001);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTimeSignatureEvent);
	}

	[TestMethod]
	public void TestFindNextEventWithLooping_AtRowWithMany_ReturnsLeastInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindNextEventWithLooping(100);
		Assert.IsTrue(e is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindNextEventWithLooping_AfterRowWithMany_ReturnsLeastInNextRow()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindNextEventWithLooping(101);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorTapNoteEvent);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetRow() == 200);
		Assert.IsTrue(((EditorTapNoteEvent)e).GetLane() == 0);
	}

	[TestMethod]
	public void TestFindNextEventWithLooping_AtLast_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindNextEventWithLooping(200);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindNextEventWithLooping_AfterLast_ReturnsFirst()
	{
		var c = CreateTestChartWithNotesAtRows(new List<int> { 100, 200 });
		var e = c.GetEvents().FindNextEventWithLooping(300);
		Assert.IsNotNull(e);
		Assert.IsTrue(e is EditorPreviewRegionEvent);
	}

	[TestMethod]
	public void TestFindNoteAt()
	{
		var expectedRows = new List<int> { 100, 200 };
		var ignoreChoices = new[] { true, false };
		var c = CreateTestChartWithNotesAtRows(expectedRows);
		for (var row = -1; row <= 300; row++)
		{
			for (var lane = 0; lane < c.NumInputs; lane++)
			{
				foreach (var ignore in ignoreChoices)
				{
					var foundNote = c.GetEvents().FindNoteAt(row, lane, ignore);
					if (expectedRows.Contains(row))
					{
						Assert.IsNotNull(foundNote);
						Assert.IsTrue(foundNote is EditorTapNoteEvent);
						Assert.AreEqual(row, ((EditorTapNoteEvent)foundNote).GetRow());
						Assert.AreEqual(lane, ((EditorTapNoteEvent)foundNote).GetLane());
					}
					else
					{
						Assert.IsNull(foundNote);
					}
				}
			}
		}

		c = CreateTestChartWithNotesAtRowsBeingEdited(expectedRows);
		for (var row = -1; row <= 300; row++)
		{
			for (var lane = 0; lane < c.NumInputs; lane++)
			{
				foreach (var ignore in ignoreChoices)
				{
					var foundNote = c.GetEvents().FindNoteAt(row, lane, ignore);
					if (!ignore && expectedRows.Contains(row))
					{
						Assert.IsNotNull(foundNote);
						Assert.IsTrue(foundNote is EditorTapNoteEvent);
						Assert.AreEqual(row, ((EditorTapNoteEvent)foundNote).GetRow());
						Assert.AreEqual(lane, ((EditorTapNoteEvent)foundNote).GetLane());
					}
					else
					{
						Assert.IsNull(foundNote);
					}
				}
			}
		}
	}

	[TestMethod]
	public void TestFindEventsAtRow()
	{
		var expectedRows = new List<int> { 100, 200 };
		var c = CreateTestChartWithNotesAtRows(expectedRows);
		for (var row = -1; row <= 300; row++)
		{
			var foundNotes = c.GetEvents().FindEventsAtRow(row);
			if (row == 0)
			{
				Assert.AreEqual(ExpectedEmptyChartTypes.Length, foundNotes.Count);
				for (var i = 0; i < ExpectedEmptyChartTypes.Length; i++)
				{
					Assert.AreEqual(ExpectedEmptyChartTypes[i], foundNotes[i].GetType());
					Assert.AreEqual(row, foundNotes[i].GetRow());
				}
			}
			else if (expectedRows.Contains(row))
			{
				Assert.AreEqual(c.NumInputs, foundNotes.Count);
				for (var lane = 0; lane < c.NumInputs; lane++)
				{
					Assert.IsTrue(foundNotes[lane] is EditorTapNoteEvent);
					Assert.AreEqual(row, ((EditorTapNoteEvent)foundNotes[lane]).GetRow());
					Assert.AreEqual(lane, foundNotes[lane].GetLane());
				}
			}
			else
			{
				Assert.AreEqual(0, foundNotes.Count);
			}
		}
	}
}
