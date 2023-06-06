using System;
using System.Collections.Generic;
using Fumen;
using Microsoft.Xna.Framework.Input;
using static StepManiaEditor.Editor;

namespace StepManiaEditor;

/// <summary>
/// Class for managing zoom values controlled by the mouse scroll wheel.
/// Expected Usage:
///  Call ProcessInput once per frame.
///  Call Update once per frame after ProcessInput.
/// </summary>
internal class ZoomManager : Fumen.IObserver<PreferencesScroll>
{
	/// <summary>
	/// Data for a zoom value that can be changed directly or interpolated to a new value.
	/// </summary>
	internal class InterpolatedValueData
	{
		private double InterpolationTimeStart;
		private double Value;
		private double ValueAtStartOfInterpolation;
		private double DesiredValue;
		private readonly double Min;
		private readonly double Max;
		private readonly Action<double> OnChangeCallback;
		private bool SettingValue;

		public InterpolatedValueData(double min, double max, double current, Action<double> onChangeCallback)
		{
			Min = min;
			Max = max;
			SetValue(current, true);
			ValueAtStartOfInterpolation = Value;
			OnChangeCallback = onChangeCallback;
		}

		public void Update(double currentTime)
		{
			if (!Value.DoubleEquals(DesiredValue))
			{
				SetValue(Interpolation.Lerp(
					ValueAtStartOfInterpolation,
					DesiredValue,
					InterpolationTimeStart,
					InterpolationTimeStart + Preferences.Instance.PreferencesScroll.ScrollInterpolationDuration,
					currentTime), false);
			}
		}

		public void StartInterpolation(double currentTime, double multiplier)
		{
			if (multiplier > 0.0)
				SetDesiredValue(DesiredValue * multiplier);
			else
				SetDesiredValue(DesiredValue / -multiplier);
			InterpolationTimeStart = currentTime;
			ValueAtStartOfInterpolation = Value;
		}

		public void OnValueChanged(double value)
		{
			// It is expected to be notified of the value changing when we are changing it.
			// Ignore these changes.
			if (Value.DoubleEquals(value) && SettingValue)
				return;

			// Set both the value and the desired value to the externally set value.
			Value = Math.Clamp(value, Min, Max);
			SetDesiredValue(Value);
		}

		public void SetValue(double value, bool setDesired)
		{
			SettingValue = true;
			var oldValue = Value;
			Value = Math.Clamp(value, Min, Max);
			if (setDesired)
				SetDesiredValue(Value);
			if (!Value.DoubleEquals(oldValue) && OnChangeCallback != null)
				OnChangeCallback(Value);
			SettingValue = false;
		}

		public void SetDesiredValue(double desiredValue)
		{
			DesiredValue = Math.Clamp(desiredValue, Min, Max);
		}

		public double GetValue()
		{
			return Value;
		}
	}

	public const double SpacingDataScrollFactor = 1.2;
	public const double MinZoom = 0.000001;
	public const double MaxZoom = 1000000.0;
	public const double MinConstantTimeSpeed = 10.0;
	public const double MaxConstantTimeSpeed = 100000.0;
	public const double MinConstantRowSpacing = 0.1;
	public const double MaxConstantRowSpacing = 10000.0;
	public const double MinVariableSpeed = 10.0;
	public const double MaxVariableSpeed = 100000.0;

	private readonly InterpolatedValueData ZoomData;
	private readonly InterpolatedValueData ConstantTimeSpacingData;
	private readonly InterpolatedValueData ConstantRowSpacingData;
	private readonly InterpolatedValueData VariableSpacingData;
	private readonly List<InterpolatedValueData> AllValues;

	/// <summary>
	/// Constructor.
	/// </summary>
	public ZoomManager()
	{
		var pScroll = Preferences.Instance.PreferencesScroll;

		ZoomData = new InterpolatedValueData(MinZoom, MaxZoom, 1.0, null);
		ConstantTimeSpacingData = new InterpolatedValueData(MinConstantTimeSpeed, MaxConstantTimeSpeed,
			pScroll.TimeBasedPixelsPerSecond,
			newValue => { pScroll.TimeBasedPixelsPerSecond = newValue; });
		ConstantRowSpacingData = new InterpolatedValueData(MinConstantRowSpacing, MaxConstantRowSpacing,
			pScroll.RowBasedPixelsPerRow,
			newValue => { pScroll.RowBasedPixelsPerRow = newValue; });
		VariableSpacingData = new InterpolatedValueData(MinVariableSpeed, MaxVariableSpeed,
			pScroll.VariablePixelsPerSecondAtDefaultBPM,
			newValue => { pScroll.VariablePixelsPerSecondAtDefaultBPM = newValue; });

		AllValues = new List<InterpolatedValueData>
		{
			ZoomData,
			ConstantTimeSpacingData,
			ConstantRowSpacingData,
			VariableSpacingData,
		};

		Preferences.Instance.PreferencesScroll.AddObserver(this);
	}

	/// <summary>
	/// Process input, to be called once per frame.
	/// </summary>
	/// <param name="currentTime">Total application time in seconds.</param>
	/// <param name="keyCommandManager">KeyCommandManager to use for checking input.</param>
	/// <param name="scrollDelta">Scroll delta value since last frame.</param>
	/// <returns>True if the ZoomManager has captured the input and false otherwise.</returns>
	public bool ProcessInput(double currentTime, KeyCommandManager keyCommandManager, float scrollDelta)
	{
		var pScroll = Preferences.Instance.PreferencesScroll;
		var scrollShouldZoom = keyCommandManager.IsKeyDown(Keys.LeftControl);
		var scrollShouldScaleDefaultSpacing = !scrollShouldZoom && keyCommandManager.IsKeyDown(Keys.LeftShift);

		// Hack.
		if (keyCommandManager.IsKeyDown(Keys.OemPlus))
		{
			ZoomData.SetValue(ZoomData.GetValue() * 1.0001f, true);
		}

		if (keyCommandManager.IsKeyDown(Keys.OemMinus))
		{
			ZoomData.SetValue(ZoomData.GetValue() / 1.0001f, true);
		}

		// If the scroll wheel hasn't moved we don't need the input.
		if (scrollDelta.FloatEquals(0.0f))
			return false;

		// Adjust zoom.
		if (scrollShouldZoom)
		{
			ZoomData.StartInterpolation(currentTime, pScroll.ZoomMultiplier * scrollDelta);
			return true;
		}

		// Adjust the default spacing value for the current SpacingMode
		if (scrollShouldScaleDefaultSpacing)
		{
			switch (pScroll.SpacingMode)
			{
				case SpacingMode.ConstantTime:
					ConstantTimeSpacingData.StartInterpolation(currentTime, SpacingDataScrollFactor * scrollDelta);
					break;
				case SpacingMode.ConstantRow:
					ConstantRowSpacingData.StartInterpolation(currentTime, SpacingDataScrollFactor * scrollDelta);
					break;
				case SpacingMode.Variable:
					VariableSpacingData.StartInterpolation(currentTime, SpacingDataScrollFactor * scrollDelta);
					break;
			}
			return true;
		}
		
		return false;
	}

	/// <summary>
	/// Update method to be called once per frame.
	/// </summary>
	/// <param name="currentTime">Total application time in seconds.</param>
	public void Update(double currentTime)
	{
		foreach (var value in AllValues)
			value.Update(currentTime);
	}

	/// <summary>
	/// Sets the zoom value. Will be clamped.
	/// </summary>
	/// <param name="zoom">New zoom value.</param>
	public void SetZoom(double zoom)
	{
		ZoomData.SetValue(zoom, true);
	}

	/// <summary>
	/// Gets the zoom to use for sizing objects.
	/// When zooming in we only zoom the spacing, not the scale of objects.
	/// </summary>
	/// <returns>Zoom level to be used as a multiplier.</returns>
	public double GetSizeZoom()
	{
		return ZoomData.GetValue() > 1.0 ? 1.0 : ZoomData.GetValue();
	}

	/// <summary>
	/// Gets the zoom to use for spacing objects.
	/// Objects are spaced one to one with the zoom level.
	/// </summary>
	/// <returns>Zoom level to be used as a multiplier.</returns>
	public double GetSpacingZoom()
	{
		return ZoomData.GetValue();
	}

	/// <summary>
	/// Notification of scroll preferences changing.
	/// </summary>
	public void OnNotify(string eventId, PreferencesScroll notifier, object payload)
	{
		switch (eventId)
		{
			case PreferencesScroll.NotificationTimeBasedPpsChanged:
				ConstantTimeSpacingData.OnValueChanged(notifier.TimeBasedPixelsPerSecond);
				break;
			case PreferencesScroll.NotificationRowBasedPprChanged:
				ConstantRowSpacingData.OnValueChanged(notifier.RowBasedPixelsPerRow);
				break;
			case PreferencesScroll.NotificationVariablePpsChanged:
				VariableSpacingData.OnValueChanged(notifier.VariablePixelsPerSecondAtDefaultBPM);
				break;
		}
	}
}
