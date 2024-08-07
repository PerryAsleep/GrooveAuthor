﻿using System;
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
	public ActionChangeNoteType(
		Editor editor,
		EditorChart chart,
		IEnumerable<EditorEvent> events,
		Func<EditorEvent, bool> filter,
		Func<EditorEvent, EditorEvent> converter) : base(false, false)
	{
		Editor = editor;
		Chart = chart;
		OriginalEvents = new List<EditorEvent>();
		NewEvents = new List<EditorEvent>();
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
		return $"Convert {OriginalEvents.Count} Notes.";
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
