using Fumen;
using Microsoft.Xna.Framework;
using static StepManiaEditor.Utils;
using static System.Math;

namespace StepManiaEditor
{
	/// <summary>
	/// A region of the chart selected with the cursor.
	/// Internally the x position of the selected region points is relative to the focal
	/// point and scaled based on the zoom level. It represents a value in chart space.
	/// Interally the y position of the selected region is stored as both a chart time and
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
	internal sealed class SelectedRegion : IRegion
	{
		#region IRegion Implementation
		public double GetRegionX() { return StartX; }
		public double GetRegionY() { return StartY; }
		public double GetRegionW() { return CurrentX - StartX; }
		public double GetRegionH() { return CurrentY - StartY; }
		public Color GetRegionColor() { return SelectionRegionColor; }
		#endregion IRegion Implementation

		// State tracking.
		private bool Active;
		private bool StartYHasBeenUpdatedThisFrame;
		private bool CurrentValuesHaveBeenUpdatedThisFrame;
		// Time tracking.
		private double StartTime;
		private double CurrentTime;
		// Pixel space values.
		private double StartY;
		private double StartYUnmodified;
		private double StartX;
		private double CurrentY;
		private double CurrentX;
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

		public double GetStartChartTime() { return StartChartTime; }
		public double GetCurrentChartTime() { return CurrentChartTime; }
		public double GetStartChartPosition() { return StartChartPosition; }
		public double GetCurrentChartPosition() { return CurrentChartPosition; }
		public double GetStartXInChartSpace() { return StartXInChartSpace; }
		public double GetCurrentXInChartSpace() { return CurrentXInChartSpace;}

		public double GetCurrentY() { return CurrentY; }

		public (double, double) GetSelectedXChartSpaceRange()
		{
			if (StartXInChartSpace <= CurrentXInChartSpace)
				return (StartXInChartSpace, CurrentXInChartSpace);
			return (CurrentXInChartSpace, StartXInChartSpace);
		}

		public (double, double) GetSelectedXRange()
		{
			if (StartX <= CurrentX)
				return (StartX, CurrentX);
			return (CurrentX, StartX);
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

		public (double, double) GetCurrentPosition()
		{
			return (CurrentX, CurrentY);
		}

		public bool IsClick()
		{
			// Allow some movement to still count as clicks if it happens quickly.
			var dx = Round(CurrentX) - Round(StartX);
			var dy = Round(CurrentY) - Round(StartYUnmodified);
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
		public bool HasStartYBeenUpdatedThisFrame() { return StartYHasBeenUpdatedThisFrame; }
		public bool HaveCurrentValuesBeenUpdatedThisFrame() { return CurrentValuesHaveBeenUpdatedThisFrame; }

		public bool IsActive() { return Active; }

		/// <summary>
		/// Start a selction with the cursor.
		/// Sets the starting x and y values.
		/// </summary>
		public void Start(
			double xInChartSpace,
			double y,
			double chartTime,
			double chartPosition,
			double xScale,
			double focalPointX,
			double time)
		{
			StartXInChartSpace = xInChartSpace;
			CurrentXInChartSpace = xInChartSpace;
			StartTime = time;
			StartY = y;
			StartYUnmodified = y;
			StartChartTime = chartTime;
			StartChartPosition = chartPosition;
			CurrentChartTime = chartTime;
			CurrentChartPosition = chartPosition;
			StartX = XPosToScreenSpace(StartXInChartSpace, focalPointX, xScale);
			CurrentX = StartX;
			Active = true;
		}

		/// <summary>
		/// Update per frame values which do not need to be derived by walking through rate altering events.
		/// From the cursor position alone we know the x value in screen and chart space, and the y value in screen space.
		/// </summary>
		public void UpdatePerFrameValues(double currentXInChartSpace, double currentY, double xScale, double focalPointX)
		{
			CurrentXInChartSpace = currentXInChartSpace;
			CurrentY = currentY;
			StartX = XPosToScreenSpace(StartXInChartSpace, focalPointX, xScale);
			CurrentX = XPosToScreenSpace(CurrentXInChartSpace, focalPointX, xScale);
		}

		/// <summary>
		/// Update the starting y value, which is derived from the starting chart time and starting chart position.
		/// Deriving this value requires walking through the visible rate altering events.
		/// </summary>
		public void UpdatePerFrameDerivedStartY(double startY)
		{
			StartY = startY;
			StartYHasBeenUpdatedThisFrame = true;
		}

		/// <summary>
		/// Update the current chart time and chart positon, which are derived from the starting y position.
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
			StartY = 0.0;
			StartYUnmodified = 0.0;
			StartX = 0.0;
			StartChartTime = 0.0;
			StartChartPosition = 0.0;
			CurrentY = 0.0;
			CurrentX = 0.0;
			CurrentChartTime = 0.0;
			CurrentChartPosition = 0.0;
			StartXInChartSpace = 0.0;
			CurrentXInChartSpace = 0.0;
		}

		private double XPosToScreenSpace(double xInChartSpace, double focalPointX, double xScale)
		{
			return focalPointX + xInChartSpace * xScale;
		}
	}
}
