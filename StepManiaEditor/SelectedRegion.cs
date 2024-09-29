using Microsoft.Xna.Framework;
using static StepManiaEditor.Utils;
using static System.Math;

namespace StepManiaEditor;

internal interface IReadOnlySelectedRegion : IRegion
{
	public double GetStartChartTime();
	public double GetCurrentChartTime();
	public double GetStartChartPosition();
	public double GetCurrentChartPosition();
	public double GetStartXInChartSpace();
	public double GetCurrentXInChartSpace();
	public double GetCurrentYInScreenSpace();
	public (double, double) GetSelectedXChartSpaceRange();
	public (double, double) GetSelectedXScreenSpaceRange();
	public (double, double) GetSelectedChartTimeRange();
	public (double, double) GetSelectedChartPositionRange();
	public (double, double) GetCurrentScreenSpacePosition();
	public bool IsClick();
	public bool IsActive();
}

/// <summary>
/// A region of the chart selected with the cursor.
/// Internally the x position of the selected region points is relative to the focal
/// point and scaled based on the zoom level. It represents a value in chart space.
/// Internally the y position of the selected region is stored as both a chart time and
/// row value.
/// If the chart animates, or the zoom or focal point change while a selection is active,
/// the starting position will move with the chart.
/// When drawing the IRegion the SelectedRegions pixel coordinates are used.
/// Expected Usage:
///  Call Start() to start a selection.
///  Per frame call the following functions. They are split to simplify the Editor update loop.
///   UpdateTime()
///   UpdatePerFrameValues()
///   UpdatePerFrameDerivedStartY()
///   UpdatePerFrameDerivedChartTimeAndPosition()
///  Call Stop() to stop the selection.
///  Call DrawRegion() to draw the selected region.
///  To access the selected region call the following functions.
///   GetStartChartTime()/GetCurrentChartTime()
///   GetStartChartPosition()/GetCurrentChartPosition()
/// </summary>
internal sealed class SelectedRegion : IReadOnlySelectedRegion
{
	#region IRegion Implementation

	public double GetRegionX()
	{
		return StartXScreenSpace;
	}

	public double GetRegionY()
	{
		return StartYScreenSpace;
	}

	public double GetRegionW()
	{
		return CurrentXScreenSpace - StartXScreenSpace;
	}

	public double GetRegionH()
	{
		return CurrentYScreenSpace - StartYScreenSpace;
	}

	public double GetRegionZ()
	{
		return 0.0;
	}

	public Color GetRegionColor()
	{
		return SelectionRegionColor;
	}

	#endregion IRegion Implementation

	// State tracking.
	private bool Active;
	private bool StartYHasBeenUpdatedThisFrame;

	private bool CurrentValuesHaveBeenUpdatedThisFrame;

	// Time tracking.
	private double StartTime;
	private double CurrentTime;

	// Pixel space values.
	private double StartYScreenSpace;
	private double StartYScreenSpaceUnmodified;
	private double StartXScreenSpace;
	private double CurrentYScreenSpace;
	private double CurrentXScreenSpace;

	// Starting y values.
	private double StartChartTime;
	private double StartChartPosition;

	// Current y values.
	private double CurrentChartTime;
	private double CurrentChartPosition;

	// Starting x value.
	private double StartXInChartSpace;

	// Current x value.
	private double CurrentXInChartSpace;

	public double GetStartChartTime()
	{
		return StartChartTime;
	}

	public double GetCurrentChartTime()
	{
		return CurrentChartTime;
	}

	public double GetStartChartPosition()
	{
		return StartChartPosition;
	}

	public double GetCurrentChartPosition()
	{
		return CurrentChartPosition;
	}

	public double GetStartXInChartSpace()
	{
		return StartXInChartSpace;
	}

	public double GetCurrentXInChartSpace()
	{
		return CurrentXInChartSpace;
	}

	public double GetCurrentYInScreenSpace()
	{
		return CurrentYScreenSpace;
	}

	public (double, double) GetSelectedXChartSpaceRange()
	{
		if (StartXInChartSpace <= CurrentXInChartSpace)
			return (StartXInChartSpace, CurrentXInChartSpace);
		return (CurrentXInChartSpace, StartXInChartSpace);
	}

	public (double, double) GetSelectedXScreenSpaceRange()
	{
		if (StartXScreenSpace <= CurrentXScreenSpace)
			return (StartXScreenSpace, CurrentXScreenSpace);
		return (CurrentXScreenSpace, StartXScreenSpace);
	}

	public (double, double) GetSelectedChartTimeRange()
	{
		if (StartChartTime <= CurrentChartTime)
			return (StartChartTime, CurrentChartTime);
		return (CurrentChartTime, StartChartTime);
	}

	public (double, double) GetSelectedChartPositionRange()
	{
		if (StartChartPosition <= CurrentChartPosition)
			return (StartChartPosition, CurrentChartPosition);
		return (CurrentChartPosition, StartChartPosition);
	}

	public (double, double) GetCurrentScreenSpacePosition()
	{
		return (CurrentXScreenSpace, CurrentYScreenSpace);
	}

	public bool IsClick()
	{
		// Allow some movement to still count as clicks if it happens quickly.
		var dx = Round(CurrentXScreenSpace) - Round(StartXScreenSpace);
		var dy = Round(CurrentYScreenSpace) - Round(StartYScreenSpaceUnmodified);
		var d = Sqrt(dx * dx + dy * dy);
		if (d <= 2.0)
			return true;
		if (d <= 5.0)
			return CurrentTime - StartTime < 0.25;
		if (d <= 10.0)
			return CurrentTime - StartTime < 0.10;
		return false;
	}

	public void ClearPerFrameData()
	{
		StartYHasBeenUpdatedThisFrame = false;
		CurrentValuesHaveBeenUpdatedThisFrame = false;
	}

	public bool HasStartYBeenUpdatedThisFrame()
	{
		return StartYHasBeenUpdatedThisFrame;
	}

	public bool HaveCurrentValuesBeenUpdatedThisFrame()
	{
		return CurrentValuesHaveBeenUpdatedThisFrame;
	}

	public bool IsActive()
	{
		return Active;
	}

	/// <summary>
	/// Start a selection with the cursor.
	/// Sets the starting x and y values.
	/// </summary>
	public void Start(
		double xInChartSpace,
		double screenSpaceY,
		double chartTime,
		double chartPosition,
		double xScale,
		double focalPointScreenSpaceX,
		double time)
	{
		StartXInChartSpace = xInChartSpace;
		CurrentXInChartSpace = xInChartSpace;
		StartTime = time;
		StartYScreenSpace = screenSpaceY;
		StartYScreenSpaceUnmodified = screenSpaceY;
		StartChartTime = chartTime;
		StartChartPosition = chartPosition;
		CurrentChartTime = chartTime;
		CurrentChartPosition = chartPosition;
		StartXScreenSpace = XPosChartSpaceToScreenSpace(StartXInChartSpace, focalPointScreenSpaceX, xScale);
		CurrentXScreenSpace = StartXScreenSpace;
		Active = true;
	}

	/// <summary>
	/// Update per frame values which do not need to be derived by walking through rate altering events.
	/// From the cursor position alone we know the x value in screen and chart space, and the y value in screen space.
	/// </summary>
	public void UpdatePerFrameValues(double currentXInChartSpace, double currentYInScreenSpace, double xScale,
		double focalPointScreenSpaceX)
	{
		CurrentXInChartSpace = currentXInChartSpace;
		CurrentYScreenSpace = currentYInScreenSpace;
		StartXScreenSpace = XPosChartSpaceToScreenSpace(StartXInChartSpace, focalPointScreenSpaceX, xScale);
		CurrentXScreenSpace = XPosChartSpaceToScreenSpace(CurrentXInChartSpace, focalPointScreenSpaceX, xScale);
	}

	/// <summary>
	/// Update the starting y value, which is derived from the starting chart time and starting chart position.
	/// Deriving this value requires walking through the visible rate altering events.
	/// </summary>
	public void UpdatePerFrameDerivedStartY(double startYScreenSpace)
	{
		StartYScreenSpace = startYScreenSpace;
		StartYHasBeenUpdatedThisFrame = true;
	}

	/// <summary>
	/// Update the current chart time and chart position, which are derived from the starting y position.
	/// Deriving these values requires walking through the visible rate altering events.
	/// </summary>
	public void UpdatePerFrameDerivedChartTimeAndPosition(double chartTime, double chartPosition)
	{
		CurrentChartTime = chartTime;
		CurrentChartPosition = chartPosition;

		CurrentValuesHaveBeenUpdatedThisFrame = true;
	}

	/// <summary>
	/// Updates the current time being tracked by the region for determining whether the region was
	/// a click or a drag.
	/// </summary>
	public void UpdateTime(double time)
	{
		if (Active)
			CurrentTime = time;
	}

	/// <summary>
	/// Stop the selection.
	/// </summary>
	public void Stop()
	{
		Reset();
	}

	private void Reset()
	{
		Active = false;
		StartYHasBeenUpdatedThisFrame = false;
		CurrentValuesHaveBeenUpdatedThisFrame = false;
		StartTime = 0.0;
		CurrentTime = 0.0;
		StartYScreenSpace = 0.0;
		StartYScreenSpaceUnmodified = 0.0;
		StartXScreenSpace = 0.0;
		StartChartTime = 0.0;
		StartChartPosition = 0.0;
		CurrentYScreenSpace = 0.0;
		CurrentXScreenSpace = 0.0;
		CurrentChartTime = 0.0;
		CurrentChartPosition = 0.0;
		StartXInChartSpace = 0.0;
		CurrentXInChartSpace = 0.0;
	}

	private double XPosChartSpaceToScreenSpace(double xInChartSpace, double focalPointScreenSpaceX, double xScale)
	{
		return focalPointScreenSpaceX + xInChartSpace * xScale;
	}
}
