using System;
using System.Collections.Generic;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary.PerformedChart;

namespace StepManiaEditor;

/// <summary>
/// Comparer for comparing EditorPatternConfig so they can be sorted by user-selected columns.
/// </summary>
internal sealed class UIPatternComparer : IComparer<EditorPatternConfig>
{
	/// <summary>
	/// Internal data used to sort EditorPatternConfig, based on ImGui ImGuiTableSortSpecsPtr.
	/// </summary>
	private class Spec
	{
		public readonly UIPatternConfigTable.Column Column;
		public readonly ImGuiSortDirection SortDirection;

		public Spec(UIPatternConfigTable.Column column, ImGuiSortDirection sortDirection)
		{
			Column = column;
			SortDirection = sortDirection;
		}
	}

	private readonly List<Spec> SortSpecs = [];

	public void SetSortSpecs(ImGuiTableSortSpecsPtr sortSpecs)
	{
		// Copy needed data from the ImGui ImGuiTableSortSpecsPtr.
		SortSpecs.Clear();
		unsafe
		{
			var p = (ImGuiUtils.NativeImGuiTableColumnSortSpecs*)sortSpecs.Specs.NativePtr;
			for (var specIndex = 0; specIndex < sortSpecs.SpecsCount; specIndex++)
			{
				var spec = p[specIndex];
				SortSpecs.Add(new Spec((UIPatternConfigTable.Column)spec.ColumnUserID, (ImGuiSortDirection)spec.SortDirection));
			}
		}
	}

	int IComparer<EditorPatternConfig>.Compare(EditorPatternConfig ep1, EditorPatternConfig ep2)
	{
		var p1 = ep1!.Config;
		var p2 = ep2!.Config;

		foreach (var spec in SortSpecs)
		{
			var comparison = 0;
			switch (spec.Column)
			{
				case UIPatternConfigTable.Column.NoteType:
					comparison = p1.BeatSubDivision.CompareTo(p2.BeatSubDivision);
					break;
				case UIPatternConfigTable.Column.RepetitionLimit:
					var p1Limit = p1.LimitSameArrowsInARowPerFoot ? p1.MaxSameArrowsInARowPerFoot : 0;
					var p2Limit = p2.LimitSameArrowsInARowPerFoot ? p2.MaxSameArrowsInARowPerFoot : 0;
					comparison = p1Limit.CompareTo(p2Limit);
					break;
				case UIPatternConfigTable.Column.StepType:
					comparison = p1.SameArrowStepWeightNormalized.CompareTo(p2.SameArrowStepWeightNormalized);
					break;
				case UIPatternConfigTable.Column.StepTypeCheckPeriod:
					comparison = p1.StepTypeCheckPeriod.CompareTo(p2.StepTypeCheckPeriod);
					break;
				case UIPatternConfigTable.Column.StartingFoot:
					comparison = p1.StartingFootChoice.CompareTo(p2.StartingFootChoice);
					if (comparison == 0 && p1.StartingFootChoice == PatternConfigStartingFootChoice.Specified)
						comparison = p1.StartingFootSpecified.CompareTo(p2.StartingFootSpecified);
					break;
				case UIPatternConfigTable.Column.StartingFooting:
					comparison = string.Compare(ep1.GetStartFootingString(), ep2.GetStartFootingString(),
						StringComparison.Ordinal);
					break;
				case UIPatternConfigTable.Column.EndingFooting:
					comparison = string.Compare(ep1.GetEndFootingString(), ep2.GetEndFootingString(), StringComparison.Ordinal);
					break;
				case UIPatternConfigTable.Column.Abbreviation:
					if (ep1.GetAbbreviation() != null && ep2.GetAbbreviation() != null)
						comparison = string.Compare(ep1.GetAbbreviation(), ep2.GetAbbreviation(), StringComparison.Ordinal);
					else if (!string.IsNullOrEmpty(ep1.GetAbbreviation()))
						comparison = 1;
					else if (!string.IsNullOrEmpty(ep2.GetAbbreviation()))
						comparison = -1;
					break;
				case UIPatternConfigTable.Column.Name:
					if (ep1.Name != null && ep2.Name != null)
						comparison = string.Compare(ep1.Name, ep2.Name, StringComparison.Ordinal);
					else if (!string.IsNullOrEmpty(ep1.Name))
						comparison = 1;
					else if (!string.IsNullOrEmpty(ep2.Name))
						comparison = -1;
					break;
			}

			if (comparison > 0)
				return spec.SortDirection == ImGuiSortDirection.Ascending ? 1 : -1;
			if (comparison < 0)
				return spec.SortDirection == ImGuiSortDirection.Ascending ? -1 : 1;
		}

		return ep1.Guid.CompareTo(ep2.Guid);
	}
}
