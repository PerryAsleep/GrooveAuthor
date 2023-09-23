namespace Fumen.ChartDefinition;

/// <summary>
/// Event that is used for comparisons when searching trees of Events.
/// </summary>
internal sealed class SearchEvent : Event
{
	public SearchEvent()
	{
	}

	public SearchEvent(SearchEvent other)
		: base(other)
	{
	}

	public override SearchEvent Clone()
	{
		return new SearchEvent(this);
	}
}
