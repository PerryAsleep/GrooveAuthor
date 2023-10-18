using System;
using System.Collections.Generic;
using System.Text;

namespace StepManiaEditor;

/// <summary>
/// Action to copy types of EditorEvents from one EditorChart to one or more other EditorCharts.
/// </summary>
internal sealed class ActionCopyEventsBetweenCharts : EditorAction
{
	/// <summary>
	/// State per destination EditorChart for undoing the action.
	/// </summary>
	private class ChartState
	{
		/// <summary>
		/// All events deleted from the EditorChart in order to copy another EditorChart's
		/// events into this EditorChart.
		/// </summary>
		public List<EditorEvent> AllDeletedEvents;

		/// <summary>
		/// All events added to this EditorChart from another EditorChart.
		/// </summary>
		public List<EditorEvent> AllAddedEvents;

		// The types below cannot have their first occurrences deleted from
		// a chart as they are needed for note positioning. Instead of deleting
		// and adding these events we alter them in place. The variables below
		// let us change and undo the values for these events.
		public EditorTimeSignatureEvent DestinationFirstTimeSignature;
		public string OriginalFirstTimeSignatureValue;
		public EditorTempoEvent DestinationFirstTempo;
		public double OriginalFirstTempoValue;
		public EditorScrollRateEvent DestinationFirstScrollRate;
		public double OriginalFirstScrollRateValue;
		public EditorInterpolatedRateAlteringEvent DestinationFirstInterpolatedRateAlteringEvent;
		public string OriginalFirstInterpolatedRateAlteringEventValue;
		public EditorMultipliersEvent DestinationFirstMultipliers;
		public string OriginalFirstMultipliersValue;
		public EditorTickCountEvent DestinationFirstTickCount;
		public int OriginalFirstTickCountValue;
	}

	/// <summary>
	/// The EditorChart being copied from.
	/// </summary>
	private readonly EditorChart SourceChart;

	/// <summary>
	/// Types of all EditorEvents being copied.
	/// </summary>
	private readonly List<Type> EventTypes;

	/// <summary>
	/// All EditorCharts to copy EditorEvents to.
	/// </summary>
	private readonly List<EditorChart> DestinationCharts;

	/// <summary>
	/// ChartState for Each destination EditorChart.
	/// </summary>
	private readonly Dictionary<EditorChart, ChartState> ChartStates = new();

	public ActionCopyEventsBetweenCharts(EditorChart sourceChart, IEnumerable<Type> eventTypes,
		IEnumerable<EditorChart> destinationCharts) : base(false, false)
	{
		SourceChart = sourceChart;
		EventTypes = new List<Type>(eventTypes);
		DestinationCharts = new List<EditorChart>(destinationCharts);
	}

	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append("Copy ");
		if (EventTypes.Count > 1)
		{
			sb.Append($"{EventTypes.Count} types of events ");
		}
		else
		{
			sb.Append("one type of event ");
		}

		sb.Append($"from {SourceChart.GetDescriptiveName()} to ");

		if (DestinationCharts.Count > 1)
		{
			sb.Append($"{DestinationCharts.Count} charts.");
		}
		else
		{
			sb.Append($"{DestinationCharts[0].GetDescriptiveName()}.");
		}

		return sb.ToString();
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		ChartStates.Clear();

		// Get all the events of the specified types from the source chart.
		var eventsToAdd = new List<EditorEvent>();
		foreach (var chartEvent in SourceChart.GetEvents())
		{
			var t = chartEvent.GetType();
			foreach (var eventType in EventTypes)
			{
				if (t == eventType)
				{
					eventsToAdd.Add(chartEvent);
					break;
				}
			}
		}

		// Process each destination chart.
		foreach (var destChart in DestinationCharts)
		{
			// Set up a new state for this chart so we can undo changes later.
			var chartState = new ChartState();

			// Record special timing events.
			EditorTimeSignatureEvent sourceFirstTimeSignature = null;
			EditorTempoEvent sourceFirstTempo = null;
			EditorScrollRateEvent sourceFirstScrollRate = null;
			EditorInterpolatedRateAlteringEvent sourceFirstInterpolatedRateAlteringEvent = null;
			EditorMultipliersEvent sourceFirstMultipliers = null;
			EditorTickCountEvent sourceFirstTickCount = null;

			// Clone the events to copy into the destination chart.
			chartState.AllAddedEvents = new List<EditorEvent>(eventsToAdd.Count);
			foreach (var eventToClone in eventsToAdd)
			{
				// Do not include special timing events.
				if (eventToClone is EditorTimeSignatureEvent tse && sourceFirstTimeSignature == null)
				{
					sourceFirstTimeSignature = tse;
					continue;
				}

				if (eventToClone is EditorTempoEvent te && sourceFirstTempo == null)
				{
					sourceFirstTempo = te;
					continue;
				}

				if (eventToClone is EditorScrollRateEvent sre && sourceFirstScrollRate == null)
				{
					sourceFirstScrollRate = sre;
					continue;
				}

				if (eventToClone is EditorInterpolatedRateAlteringEvent irae && sourceFirstInterpolatedRateAlteringEvent == null)
				{
					sourceFirstInterpolatedRateAlteringEvent = irae;
					continue;
				}

				if (eventToClone is EditorMultipliersEvent me && sourceFirstMultipliers == null)
				{
					sourceFirstMultipliers = me;
					continue;
				}

				if (eventToClone is EditorTickCountEvent tce && sourceFirstTickCount == null)
				{
					sourceFirstTickCount = tce;
					continue;
				}

				// Include all other events.
				chartState.AllAddedEvents.Add(eventToClone.Clone(destChart));
			}

			// Determine which events to delete from the destination chart.
			var eventsToDelete = new List<EditorEvent>();
			foreach (var chartEvent in destChart.GetEvents())
			{
				var t = chartEvent.GetType();
				foreach (var eventType in EventTypes)
				{
					if (t == eventType)
					{
						// Do not include special timing events.
						if (t == typeof(EditorTimeSignatureEvent) && chartState.DestinationFirstTimeSignature == null)
						{
							chartState.DestinationFirstTimeSignature = (EditorTimeSignatureEvent)chartEvent;
							continue;
						}

						if (t == typeof(EditorTempoEvent) && chartState.DestinationFirstTempo == null)
						{
							chartState.DestinationFirstTempo = (EditorTempoEvent)chartEvent;
							continue;
						}

						if (t == typeof(EditorScrollRateEvent) && chartState.DestinationFirstScrollRate == null)
						{
							chartState.DestinationFirstScrollRate = (EditorScrollRateEvent)chartEvent;
							continue;
						}

						if (t == typeof(EditorInterpolatedRateAlteringEvent) &&
						    chartState.DestinationFirstInterpolatedRateAlteringEvent == null)
						{
							chartState.DestinationFirstInterpolatedRateAlteringEvent =
								(EditorInterpolatedRateAlteringEvent)chartEvent;
							continue;
						}

						if (t == typeof(EditorMultipliersEvent) && chartState.DestinationFirstMultipliers == null)
						{
							chartState.DestinationFirstMultipliers = (EditorMultipliersEvent)chartEvent;
							continue;
						}

						if (t == typeof(EditorTickCountEvent) && chartState.DestinationFirstTickCount == null)
						{
							chartState.DestinationFirstTickCount = (EditorTickCountEvent)chartEvent;
							continue;
						}

						// Include all other events.
						eventsToDelete.Add(chartEvent);
						break;
					}
				}
			}

			// We cannot delete some events at row 0 as they are needed in order
			// compute timing and spacing of other events. For these kinds of events,
			// do not delete them but instead update the existing ones after the
			// others have been updated.

			// Delete all events minus the special timing events.
			chartState.AllDeletedEvents = destChart.DeleteEvents(eventsToDelete);

			// Update the special timing events
			if (sourceFirstTimeSignature != null && chartState.DestinationFirstTimeSignature != null)
			{
				chartState.OriginalFirstTimeSignatureValue = chartState.DestinationFirstTimeSignature.StringValue;
				chartState.DestinationFirstTimeSignature.StringValue = sourceFirstTimeSignature.StringValue;
			}

			if (sourceFirstTempo != null && chartState.DestinationFirstTempo != null)
			{
				chartState.OriginalFirstTempoValue = chartState.DestinationFirstTempo.DoubleValue;
				chartState.DestinationFirstTempo.DoubleValue = sourceFirstTempo.DoubleValue;
			}

			if (sourceFirstScrollRate != null && chartState.DestinationFirstScrollRate != null)
			{
				chartState.OriginalFirstScrollRateValue = chartState.DestinationFirstScrollRate.DoubleValue;
				chartState.DestinationFirstScrollRate.DoubleValue = sourceFirstScrollRate.DoubleValue;
			}

			if (sourceFirstInterpolatedRateAlteringEvent != null &&
			    chartState.DestinationFirstInterpolatedRateAlteringEvent != null)
			{
				chartState.OriginalFirstInterpolatedRateAlteringEventValue =
					chartState.DestinationFirstInterpolatedRateAlteringEvent.StringValue;
				chartState.DestinationFirstInterpolatedRateAlteringEvent.StringValue =
					sourceFirstInterpolatedRateAlteringEvent.StringValue;
			}

			if (sourceFirstMultipliers != null && chartState.DestinationFirstMultipliers != null)
			{
				chartState.OriginalFirstMultipliersValue = chartState.DestinationFirstMultipliers.StringValue;
				chartState.DestinationFirstMultipliers.StringValue = sourceFirstMultipliers.StringValue;
			}

			if (sourceFirstTickCount != null && chartState.DestinationFirstTickCount != null)
			{
				chartState.OriginalFirstTickCountValue = chartState.DestinationFirstTickCount.IntValue;
				chartState.DestinationFirstTickCount.IntValue = sourceFirstTickCount.IntValue;
			}

			// Add all events minus the special timing events.
			destChart.AddEvents(chartState.AllAddedEvents);

			ChartStates[destChart] = chartState;
		}
	}

	protected override void UndoImplementation()
	{
		foreach (var destChart in DestinationCharts)
		{
			var chartState = ChartStates[destChart];

			// Delete all added events minus the special timing events.
			destChart.DeleteEvents(chartState.AllAddedEvents);

			// Undo the changes to the special timing events.
			if (chartState.DestinationFirstTickCount != null)
			{
				chartState.DestinationFirstTickCount.IntValue = chartState.OriginalFirstTickCountValue;
			}

			if (chartState.DestinationFirstMultipliers != null)
			{
				chartState.DestinationFirstMultipliers.StringValue = chartState.OriginalFirstMultipliersValue;
			}

			if (chartState.DestinationFirstInterpolatedRateAlteringEvent != null)
			{
				chartState.DestinationFirstInterpolatedRateAlteringEvent.StringValue =
					chartState.OriginalFirstInterpolatedRateAlteringEventValue;
			}

			if (chartState.DestinationFirstScrollRate != null)
			{
				chartState.DestinationFirstScrollRate.DoubleValue = chartState.OriginalFirstScrollRateValue;
			}

			if (chartState.DestinationFirstTempo != null)
			{
				chartState.DestinationFirstTempo.DoubleValue = chartState.OriginalFirstTempoValue;
			}

			if (chartState.DestinationFirstTimeSignature != null)
			{
				chartState.DestinationFirstTimeSignature.StringValue = chartState.OriginalFirstTimeSignatureValue;
			}

			// Add all deleted events minus the special timing events.
			destChart.AddEvents(chartState.AllDeletedEvents);
		}
	}
}
