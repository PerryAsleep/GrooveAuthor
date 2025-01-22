using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to change the player of notes in a selection to another player.
/// </summary>
internal sealed class ActionChangeNotePlayer : EditorAction
{
	private readonly List<EditorEvent> OriginalEvents = [];
	private readonly List<EditorEvent> NewEvents = [];
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private readonly int Player;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">EditorChart containing the EditorEvents.</param>
	/// <param name="events">
	/// Events to consider for changing player. This may contain more events than will be converted.
	/// </param>
	/// <param name="player">New payer.</param>
	public ActionChangeNotePlayer(
		Editor editor,
		EditorChart chart,
		IEnumerable<EditorEvent> events,
		int player) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		Player = player;
		foreach (var editorEvent in events)
		{
			if (editorEvent.IsLaneNote() && editorEvent.GetPlayer() != Player)
			{
				OriginalEvents.Add(editorEvent);
				var newEvent = editorEvent.Clone();
				newEvent.SetPlayer(Player);
				NewEvents.Add(newEvent);
			}
		}
	}

	public override string ToString()
	{
		return $"Convert {OriginalEvents.Count} Notes to Player {Player + 1} Notes.";
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
