using System;
using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to change the type of notes in a selection to another type.
/// </summary>
internal sealed class ActionChangeNoteType : EditorAction
{
	private readonly List<EditorEvent> OriginalEvents;
	private readonly List<EditorEvent> NewEvents;
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private readonly string OriginalType;
	private readonly string NewType;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	/// <param name="chart">EditorChart containing the EditorEvents.</param>
	/// <param name="events">
	/// Events to consider for changing types. This may contain more events than will be converted.
	/// </param>
	/// <param name="filter">
	/// Function to filter the given events. It should return true if an event should be converted and false otherwise.
	/// </param>
	/// <param name="converter">
	/// Function to convert a given EditorEvent into a new EditorEvent.
	/// </param>
	/// <param name="originalType">Original type to use for logging.</param>
	/// <param name="newType">New type to use for logging.</param>
	public ActionChangeNoteType(
		Editor editor,
		EditorChart chart,
		IEnumerable<EditorEvent> events,
		Func<EditorEvent, bool> filter,
		Func<EditorEvent, EditorEvent> converter,
		string originalType,
		string newType) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		OriginalEvents = new List<EditorEvent>();
		NewEvents = new List<EditorEvent>();
		OriginalType = originalType;
		NewType = newType;
		foreach (var editorEvent in events)
		{
			if (filter(editorEvent))
			{
				OriginalEvents.Add(editorEvent);
				var newEvent = converter(editorEvent);
				if (newEvent != null)
					NewEvents.Add(newEvent);
			}
		}
	}

	public override string ToString()
	{
		return $"Convert {OriginalEvents.Count} {OriginalType} to {NewType}.";
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
