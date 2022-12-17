using Microsoft.Xna.Framework;
using static StepManiaEditor.Utils;

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
		// Pixel space values.
		private double StartY;
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

		public double GetStartChartTime() { return StartChartTime; }
		public double GetCurrentChartTime() { return CurrentChartTime; }
		public double GetStartChartPosition() { return StartChartPosition; }
		public double GetCurrentChartPosition() { return CurrentChartPosition; }
		public double GetCurrentY() { return CurrentY; }

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
		public void Start(double xInChartSpace, double y, double chartTime, double chartPosition, double xScale, double focalPointX)
		{
			StartXInChartSpace = xInChartSpace;
			StartY = y;
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
			CurrentY = currentY;
			StartX = XPosToScreenSpace(StartXInChartSpace, focalPointX, xScale);
			CurrentX = XPosToScreenSpace(currentXInChartSpace, focalPointX, xScale);
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
			StartY = 0.0;
			StartX = 0.0;
			StartChartTime = 0.0;
			StartChartPosition = 0.0;
			CurrentY = 0.0;
			CurrentX = 0.0;
			CurrentChartTime = 0.0;
			CurrentChartPosition = 0.0;
			StartXInChartSpace = 0.0;
		}

		private double XPosToScreenSpace(double xInChartSpace, double focalPointX, double xScale)
		{
			return focalPointX + xInChartSpace * xScale;
		}
	}
}
