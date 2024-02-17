namespace StepManiaEditor.EditorEvents;

/// <summary>
/// EditorEvent to use when needing to search for EditorEvents in data structures which require comparing
/// to an input EditorEvent.
/// </summary>
internal sealed class EditorSearchEvent : EditorEvent
{
	public EditorSearchEvent(EventConfig config)
		: base(config)
	{
	}

	public override string GetShortTypeName()
	{
		return "Search";
	}

	public override bool IsMiscEvent()
	{
		return false;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return false;
	}

	public override bool IsSelectableWithModifiers()
	{
		return false;
	}

	public override bool IsStandardSearchEvent()
	{
		return true;
	}
}
