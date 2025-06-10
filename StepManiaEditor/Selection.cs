using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Readonly interface for Selection.
/// </summary>
internal interface IReadOnlySelection
{
	public bool HasSelectedEvents();
	public bool HasSelectedPatterns();
	public IEnumerable<EditorEvent> GetSelectedEvents();

	public IEnumerable<EditorPatternEvent> GetSelectedPatterns();
	public EditorEvent GetLastSelectedEvent();
}

/// <summary>
/// Class for selecting EditorEvents and providing easy access to which EditorEvents are selected.
/// </summary>
internal sealed class Selection : IReadOnlySelection
{
	/// <summary>
	/// All currently selected EditorEvents.
	/// </summary>
	private readonly HashSet<EditorEvent> SelectedEvents = [];

	/// <summary>
	/// All currently selected EditorPatternEvents.
	/// </summary>
	private readonly HashSet<EditorPatternEvent> SelectedPatterns = [];

	/// <summary>
	/// The last selected EditorEvent.
	/// </summary>
	private EditorEvent LastSelectedEvent;

	public bool HasSelectedEvents()
	{
		return SelectedEvents.Count > 0;
	}

	public bool HasSelectedPatterns()
	{
		return SelectedPatterns.Count > 0;
	}

	public IEnumerable<EditorEvent> GetSelectedEvents()
	{
		return SelectedEvents;
	}

	public IEnumerable<EditorPatternEvent> GetSelectedPatterns()
	{
		return SelectedPatterns;
	}

	public EditorEvent GetLastSelectedEvent()
	{
		return LastSelectedEvent;
	}

	public void SelectEvent(EditorEvent e, bool setLastSelected)
	{
		if (setLastSelected)
			LastSelectedEvent = e;
		if (e.IsSelected())
			return;
		e.Select();
		SelectedEvents.Add(e);
		if (e is EditorPatternEvent p)
			SelectedPatterns.Add(p);
	}

	public void DeselectEvent(EditorEvent e)
	{
		if (!e.IsSelected())
			return;
		if (LastSelectedEvent == e)
			LastSelectedEvent = null;
		e.Deselect();
		SelectedEvents.Remove(e);
		if (e is EditorPatternEvent p)
			SelectedPatterns.Remove(p);
	}

	public void ClearSelectedEvents()
	{
		foreach (var selectedEvent in SelectedEvents)
			selectedEvent.Deselect();
		SelectedEvents.Clear();
		SelectedPatterns.Clear();
		LastSelectedEvent = null;
	}

	public void SetSelectEvents(List<EditorEvent> selectedEvents)
	{
		ClearSelectedEvents();
		foreach (var selectedEvent in selectedEvents)
			SelectEvent(selectedEvent, true);
	}
}
