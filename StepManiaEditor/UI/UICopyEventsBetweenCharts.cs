using System;
using System.Collections.Generic;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI to copy types of EditorEvents from one EditorChart to one or more other EditorCharts.
/// </summary>
internal class UICopyEventsBetweenCharts : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(100);
	private static readonly int DefaultWidth = UiScaled(460);

	/// <summary>
	/// State associated with each Type of EditorEvent that can be copied.
	/// </summary>
	private class CopyableTypeState
	{
		public readonly Type EventType;
		public readonly string PrettyName;
		public readonly bool IsTimingEvent;
		public readonly bool IsScrollEvent;
		public readonly bool IsStepmaniaEvent;
		public bool Selected;

		public CopyableTypeState(Type eventType, string prettyName, bool isTimingEvent, bool isScrollEvent, bool isStepmaniaEvent)
		{
			EventType = eventType;
			PrettyName = prettyName;
			IsTimingEvent = isTimingEvent;
			IsScrollEvent = isScrollEvent;
			IsStepmaniaEvent = isStepmaniaEvent;
		}
	}

	/// <summary>
	/// Whether the UI is configured to copy to one destination chart, or all destination charts.
	/// </summary>
	private enum CopyToType
	{
		AllCharts,
		SingleChart,
	}

	private static readonly CopyableTypeState[] State =
	{
		new(typeof(EditorTimeSignatureEvent), "Time Signatures", true, false, true),
		new(typeof(EditorTempoEvent), "Tempos", true, false, true),
		new(typeof(EditorStopEvent), "Stops", true, false, true),
		new(typeof(EditorDelayEvent), "Delays", true, false, true),
		new(typeof(EditorWarpEvent), "Warps", true, false, true),
		new(typeof(EditorScrollRateEvent), "Scroll Rates", false, true, true),
		new(typeof(EditorInterpolatedRateAlteringEvent), "Interpolated Scroll Rates", false, true, true),
		new(typeof(EditorFakeSegmentEvent), "Fake Region", false, false, true),
		new(typeof(EditorMultipliersEvent), "Multipliers", false, false, true),
		new(typeof(EditorTickCountEvent), "Tick Counts", false, false, true),
		new(typeof(EditorLabelEvent), "Labels", false, false, true),
		new(typeof(EditorPatternEvent), "Patterns", false, false, false),
	};

	/// <summary>
	/// Whether or not this window is showing.
	/// This state is tracked internally and not persisted.
	/// </summary>
	private bool Showing;

	/// <summary>
	/// The single EditorChart to copy from.
	/// </summary>
	private EditorChart SourceChart;

	/// <summary>
	/// The single EditorChart to copy to when the DestinationType is SingleChart.
	/// </summary>
	private EditorChart DestinationChart;

	/// <summary>
	/// Whether the UI is configured to copy to one destination chart, or all destination charts.
	/// </summary>
	private CopyToType DestinationType;

	/// <summary>
	/// Editor.
	/// </summary>
	private Editor Editor;

	public static UICopyEventsBetweenCharts Instance { get; } = new();

	private UICopyEventsBetweenCharts() : base("Copy Events")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public static IEnumerable<Type> GetStepmaniaTypes()
	{
		var types = new List<Type>();
		foreach (var state in State)
			if (state.IsStepmaniaEvent)
				types.Add(state.EventType);
		return types;
	}

	public static IEnumerable<Type> GetTimingAndScrollTypes()
	{
		var types = new List<Type>();
		foreach (var state in State)
			if (state.IsTimingEvent || state.IsScrollEvent)
				types.Add(state.EventType);
		return types;
	}

	public static IEnumerable<Type> GetTimingTypes()
	{
		var types = new List<Type>();
		foreach (var state in State)
			if (state.IsTimingEvent)
				types.Add(state.EventType);
		return types;
	}

	/// <summary>
	/// Show this UI.
	/// </summary>
	public override void Open(bool focus)
	{
		SourceChart = null;
		DestinationChart = null;

		// Configure with Stepmania events selected
		foreach (var state in State)
			state.Selected = state.IsStepmaniaEvent;

		Showing = true;
		if (focus)
			Focus();
	}

	/// <summary>
	/// Close this UI if it is showing.
	/// </summary>
	public override void Close()
	{
		Showing = false;
		SourceChart = null;
	}

	public void Draw()
	{
		if (!Showing)
			return;

		Utils.EnsureChartReferencesValidChartFromActiveSong(ref SourceChart, Editor);

		var numSelectedTypes = 0;

		if (BeginWindow(WindowTitle, ref Showing, DefaultWidth))
		{
			// Explanation
			ImGui.TextWrapped(
				"Copying events replaces all events of the specified types in one or more charts with the events from another chart.");

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("UICopyEventsBetweenCharts", TitleColumnWidth))
			{
				// Source Chart.
				ImGuiLayoutUtils.DrawTitle("Copy From", "The chart to use for copying events from.");
				ImGui.SameLine();
				if (SourceChart != null)
				{
					var selectedName = SourceChart.GetDescriptiveName();
					if (ImGui.BeginCombo("Copy From", selectedName))
					{
						UIChartList.DrawChartList(
							Editor.GetActiveSong(),
							SourceChart,
							selectedChart => SourceChart = selectedChart);
						ImGui.EndCombo();
					}
				}
				else
				{
					ImGui.Text("No available charts.");
				}

				// Events to copy.
				ImGuiLayoutUtils.DrawTitle("Events to Copy",
					"Which events to copy between charts.");

				ImGui.SameLine();
				if (ImGui.BeginTable("Events to Copy Table", 1))
				{
					ImGui.TableNextRow();

					// Row of buttons for common sets of events.
					ImGui.TableSetColumnIndex(0);
					if (ImGui.Button("Stepmania Events"))
					{
						foreach (var state in State)
							state.Selected = state.IsStepmaniaEvent;
					}

					ImGui.SameLine();
					if (ImGui.Button("Timing"))
					{
						foreach (var state in State)
							state.Selected = state.IsTimingEvent;
					}

					ImGui.SameLine();
					if (ImGui.Button("Timing & Scroll"))
					{
						foreach (var state in State)
							state.Selected = state.IsTimingEvent || state.IsScrollEvent;
					}

					ImGui.SameLine();
					if (ImGui.Button("All"))
					{
						foreach (var state in State)
							state.Selected = true;
					}

					// Next row is a table of event types.
					ImGui.TableNextRow();
					ImGui.TableSetColumnIndex(0);
					if (ImGui.BeginTable("Events to Copy Table Inner", 2))
					{
						// Split after the seventh entry as that nicely splits
						// the timing and scroll events from the remainder.
						var maxNumPerCol = 7;
						var maxI = Math.Max(maxNumPerCol, (int)(State.Length * 0.5) + 1);
						for (var i = 0; i < maxI; i++)
						{
							ImGui.TableNextRow();

							var left = i;
							var right = i + maxNumPerCol;

							if (left >= State.Length)
								break;
							ImGui.TableSetColumnIndex(0);
							ImGui.Checkbox(State[left].PrettyName, ref State[left].Selected);
							if (State[left].Selected)
								numSelectedTypes++;

							if (right >= State.Length)
								continue;
							ImGui.TableSetColumnIndex(1);
							ImGui.Checkbox(State[right].PrettyName, ref State[right].Selected);
							if (State[right].Selected)
								numSelectedTypes++;
						}

						ImGui.EndTable();
					}

					ImGui.EndTable();
				}

				// Whether to copy to one chart or all charts.
				ImGuiLayoutUtils.DrawRowEnum("Copy To", "CopyEventsBetweenChartsDestinationType", ref DestinationType, null,
					"Which charts to copy events to.");

				// If copying to one chart, add UI for choosing a chart.
				if (DestinationType == CopyToType.SingleChart)
				{
					Utils.EnsureChartReferencesValidChartFromActiveSong(ref DestinationChart, Editor);

					// Destination Chart.
					ImGuiLayoutUtils.DrawTitle("Destination Chart", "The chart to copy events to.");
					ImGui.SameLine();
					if (DestinationChart != null)
					{
						var selectedName = DestinationChart.GetDescriptiveName();
						if (ImGui.BeginCombo("Copy To", selectedName))
						{
							UIChartList.DrawChartList(
								Editor.GetActiveSong(),
								DestinationChart,
								selectedChart => DestinationChart = selectedChart);
							ImGui.EndCombo();
						}
					}
					else
					{
						ImGui.Text("No available charts.");
					}
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();

			// Determine whether the events can by copied.
			var canCopy = numSelectedTypes > 0 && SourceChart != null;
			if (canCopy)
			{
				switch (DestinationType)
				{
					case CopyToType.AllCharts:
						if (Editor.GetActiveSong()?.GetNumCharts() < 2)
							canCopy = false;
						break;
					case CopyToType.SingleChart:
						if (DestinationChart == null || DestinationChart == SourceChart)
							canCopy = false;
						break;
				}
			}

			// Confirm button.
			if (!canCopy)
				PushDisabled();
			if (ImGui.Button("Copy Events"))
			{
				// Accumulate a list of destination charts.
				var destCharts = new List<EditorChart>();
				switch (DestinationType)
				{
					case CopyToType.AllCharts:
						foreach (var chart in Editor.GetActiveSong().GetCharts())
						{
							if (chart != SourceChart)
								destCharts.Add(chart);
						}

						break;
					case CopyToType.SingleChart:
						destCharts.Add(DestinationChart);
						break;
				}

				// Accumulate event types to copy.
				var eventTypes = new List<Type>();
				foreach (var state in State)
					if (state.Selected)
						eventTypes.Add(state.EventType);

				// Perform the action to copy the events.
				ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(SourceChart, eventTypes, destCharts));

				Close();
			}

			if (!canCopy)
				PopDisabled();

			// Cancel button.
			ImGui.SameLine();
			if (ImGui.Button("Cancel"))
			{
				Close();
			}
		}

		ImGui.End();
	}
}
