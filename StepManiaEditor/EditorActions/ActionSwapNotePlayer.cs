using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to swap the players of notes in a selection between two players.
/// </summary>
internal sealed class ActionSwapNotePlayer : EditorAction
{
	private readonly List<EditorEvent> OriginalEvents = [];
	private readonly List<EditorEvent> NewEvents = [];
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private readonly int PlayerA;
	private readonly int PlayerB;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">EditorChart containing the EditorEvents.</param>
	/// <param name="events">
	/// Events to consider for changing player. This may contain more events than will be converted.
	/// </param>
	/// <param name="playerA">First player to swap.</param>
	/// <param name="playerB">Second player to swap.</param>
	public ActionSwapNotePlayer(
		Editor editor,
		EditorChart chart,
		IEnumerable<EditorEvent> events,
		int playerA,
		int playerB) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		PlayerA = playerA;
		PlayerB = playerB;
		foreach (var editorEvent in events)
		{
			if (!editorEvent.IsLaneNote())
				continue;
			if (editorEvent.GetPlayer() == PlayerA)
			{
				OriginalEvents.Add(editorEvent);
				var newEvent = editorEvent.Clone();
				newEvent.SetPlayer(PlayerB);
				NewEvents.Add(newEvent);
			}
			else if (editorEvent.GetPlayer() == PlayerB)
			{
				OriginalEvents.Add(editorEvent);
				var newEvent = editorEvent.Clone();
				newEvent.SetPlayer(PlayerA);
				NewEvents.Add(newEvent);
			}
		}
	}

	public override string ToString()
	{
		return $"Swap {OriginalEvents.Count} Player {PlayerA + 1} and Player {PlayerB + 1} Notes.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		Editor.OnNoteTransformationBegin();
		Chart.DeleteEvents(OriginalEvents);
		Chart.AddEvents(NewEvents);
		Editor.OnNoteTransformationEnd(NewEvents);
	}

	protected override void UndoImplementation()
	{
		Editor.OnNoteTransformationBegin();
		Chart.DeleteEvents(NewEvents);
		Chart.AddEvents(OriginalEvents);
		Editor.OnNoteTransformationEnd(OriginalEvents);
	}
}
