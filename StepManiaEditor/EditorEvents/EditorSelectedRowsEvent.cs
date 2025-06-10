using System;
using Microsoft.Xna.Framework;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// EditorEvent for drawing a region of selected rows.
/// Implemented as an EditorEvent to simplify rendering.
/// This EditorEvent is only present within the chart while the selected rows are active.
/// </summary>
internal sealed class EditorSelectedRowsEvent : EditorEvent, IChartRegion
{
	private int EndRow;

	#region IChartRegion Implementation

	private double RegionX, RegionY, RegionW, RegionH;

	public double GetRegionX()
	{
		return RegionX;
	}

	public double GetRegionY()
	{
		return RegionY;
	}

	public double GetRegionW()
	{
		return RegionW;
	}

	public double GetRegionH()
	{
		return RegionH;
	}

	public double GetRegionZ()
	{
		return GetChartPosition() + SelectedRowsRegionZOffset;
	}

	public void SetRegionX(double x)
	{
		RegionX = x;
	}

	public void SetRegionY(double y)
	{
		RegionY = y;
	}

	public void SetRegionW(double w)
	{
		RegionW = w;
	}

	public void SetRegionH(double h)
	{
		// Ensure the selected row region is always rendered with some height.
		RegionH = Math.Max(2.0, h);
	}

	public double GetChartPositionDurationForRegion()
	{
		return GetChartPositionDuration();
	}

	public double GetChartTimeDurationForRegion()
	{
		return GetChartTimeDuration();
	}

	public Color GetRegionColor()
	{
		return SelectionRegionColor;
	}

	public float GetRegionAlpha()
	{
		return Alpha;
	}

	public bool IsRegionSelection()
	{
		return true;
	}

	#endregion IChartRegion Implementation

	public EditorSelectedRowsEvent(EventConfig config) : base(config)
	{
	}

	public void SetSelectionRows(int startRow, int endRow)
	{
		EndRow = endRow;
		SetRow(startRow);
	}

	public override string GetShortTypeName()
	{
		return "Selection";
	}

	public override bool IsMiscEvent()
	{
		return true;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return false;
	}

	public override bool IsSelectableWithModifiers()
	{
		return false;
	}

	public override double GetChartTime()
	{
		var chartTime = 0.0;
		EditorChart.TryGetTimeFromChartPosition(GetChartPosition(), ref chartTime);
		return chartTime;
	}

	public override double GetEndChartTime()
	{
		var endChartTime = 0.0;
		EditorChart.TryGetTimeFromChartPosition(GetEndChartPosition(), ref endChartTime);
		return endChartTime;
	}

	public override int GetEndRow()
	{
		return EndRow;
	}

	public override double GetEndChartPosition()
	{
		return EndRow;
	}
}
