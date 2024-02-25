using System;
using System.Collections.Generic;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Comparer for comparing EditorPerformedChartConfig so they can be sorted by user-selected columns.
/// </summary>
internal sealed class UIPerformedChartComparer : IComparer<EditorPerformedChartConfig>
{
	/// <summary>
	/// Internal data used to sort EditorPerformedChartConfig, based on ImGui ImGuiTableSortSpecsPtr.
	/// </summary>
	private class Spec
	{
		public readonly UIPerformedChartConfigTable.Column Column;
		public readonly ImGuiSortDirection SortDirection;

		public Spec(UIPerformedChartConfigTable.Column column, ImGuiSortDirection sortDirection)
		{
			Column = column;
			SortDirection = sortDirection;
		}
	}

	private readonly List<Spec> SortSpecs = new();

	public void SetSortSpecs(ImGuiTableSortSpecsPtr sortSpecs)
	{
		// Copy needed data from from the ImGui ImGuiTableSortSpecsPtr.
		SortSpecs.Clear();
		unsafe
		{
			var p = (ImGuiUtils.NativeImGuiTableColumnSortSpecs*)sortSpecs.Specs.NativePtr;
			for (var specIndex = 0; specIndex < sortSpecs.SpecsCount; specIndex++)
			{
				var spec = p[specIndex];
				SortSpecs.Add(new Spec((UIPerformedChartConfigTable.Column)spec.ColumnUserID,
					(ImGuiSortDirection)spec.SortDirection));
			}
		}
	}

	int IComparer<EditorPerformedChartConfig>.Compare(EditorPerformedChartConfig ep1, EditorPerformedChartConfig ep2)
	{
		var p1 = ep1!.Config;
		var p2 = ep2!.Config;

		foreach (var spec in SortSpecs)
		{
			var comparison = 0;
			switch (spec.Column)
			{
				case UIPerformedChartConfigTable.Column.Name:
				{
					if (ep1.Name != null && ep2.Name != null)
						comparison = string.Compare(ep1.Name, ep2.Name, StringComparison.Ordinal);
					else if (!string.IsNullOrEmpty(ep1.Name))
						comparison = 1;
					else if (!string.IsNullOrEmpty(ep2.Name))
						comparison = -1;
					break;
				}
				case UIPerformedChartConfigTable.Column.Abbreviation:
				{
					if (ep1.GetAbbreviation() != null && ep2.GetAbbreviation() != null)
						comparison = string.Compare(ep1.GetAbbreviation(), ep2.GetAbbreviation(), StringComparison.Ordinal);
					else if (!string.IsNullOrEmpty(ep1.GetAbbreviation()))
						comparison = 1;
					else if (!string.IsNullOrEmpty(ep2.GetAbbreviation()))
						comparison = -1;
					break;
				}
				case UIPerformedChartConfigTable.Column.StepSpeedMin:
				{
					var bpm1 = p1.StepTightening.IsSpeedTighteningEnabled() ? ep1.TravelSpeedMinBPM : 0;
					var bpm2 = p2.StepTightening.IsSpeedTighteningEnabled() ? ep1.TravelSpeedMinBPM : 0;
					comparison = bpm1.CompareTo(bpm2);
					break;
				}
				case UIPerformedChartConfigTable.Column.StepDistanceMin:
				{
					var d1 = p1.StepTightening.IsDistanceTighteningEnabled() ? p1.StepTightening.DistanceMin : 0.0;
					var d2 = p2.StepTightening.IsDistanceTighteningEnabled() ? p2.StepTightening.DistanceMin : 0.0;
					comparison = d1.CompareTo(d2);
					break;
				}
				case UIPerformedChartConfigTable.Column.StepStretchMin:
				{
					var s1 = p1.StepTightening.IsStretchTighteningEnabled() ? p1.StepTightening.StretchDistanceMin : 0.0;
					var s2 = p2.StepTightening.IsStretchTighteningEnabled() ? p2.StepTightening.StretchDistanceMin : 0.0;
					comparison = s1.CompareTo(s2);
					break;
				}
				case UIPerformedChartConfigTable.Column.LateralSpeed:
				{
					var s1 = p1.LateralTightening.IsEnabled() ? p1.LateralTightening.Speed : 0.0;
					var s2 = p2.LateralTightening.IsEnabled() ? p2.LateralTightening.Speed : 0.0;
					comparison = s1.CompareTo(s2);
					break;
				}
				case UIPerformedChartConfigTable.Column.LateralRelativeNPS:
				{
					var nps1 = p1.LateralTightening.IsEnabled() ? p1.LateralTightening.RelativeNPS : 0.0;
					var nps2 = p2.LateralTightening.IsEnabled() ? p2.LateralTightening.RelativeNPS : 0.0;
					comparison = nps1.CompareTo(nps2);
					break;
				}
				case UIPerformedChartConfigTable.Column.LateralAbsoluteNPS:
				{
					var nps1 = p1.LateralTightening.IsEnabled() ? p1.LateralTightening.AbsoluteNPS : 0.0;
					var nps2 = p2.LateralTightening.IsEnabled() ? p2.LateralTightening.AbsoluteNPS : 0.0;
					comparison = nps1.CompareTo(nps2);
					break;
				}
				case UIPerformedChartConfigTable.Column.TransitionMin:
				{
					var t1 = p1.Transitions.IsEnabled() ? p1.Transitions.StepsPerTransitionMin : 0.0;
					var t2 = p2.Transitions.IsEnabled() ? p2.Transitions.StepsPerTransitionMin : 0.0;
					comparison = t1.CompareTo(t2);
					break;
				}
				case UIPerformedChartConfigTable.Column.TransitionMax:
				{
					var t1 = p1.Transitions.IsEnabled() ? p1.Transitions.StepsPerTransitionMax : 0.0;
					var t2 = p2.Transitions.IsEnabled() ? p2.Transitions.StepsPerTransitionMax : 0.0;
					comparison = t1.CompareTo(t2);
					break;
				}
				case UIPerformedChartConfigTable.Column.FacingInwardLimit:
				{
					comparison = p1.Facing.MaxInwardPercentage.CompareTo(p2.Facing.MaxInwardPercentage);
					break;
				}
				case UIPerformedChartConfigTable.Column.FacingOutwardLimit:
				{
					comparison = p1.Facing.MaxOutwardPercentage.CompareTo(p2.Facing.MaxOutwardPercentage);
					break;
				}
			}

			if (comparison > 0)
				return spec.SortDirection == ImGuiSortDirection.Ascending ? 1 : -1;
			if (comparison < 0)
				return spec.SortDirection == ImGuiSortDirection.Ascending ? -1 : 1;
		}

		return ep1.Guid.CompareTo(ep2.Guid);
	}
}
