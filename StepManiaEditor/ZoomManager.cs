﻿using System;
using System.Collections.Generic;
using Fumen;
using static StepManiaEditor.Editor;

namespace StepManiaEditor;

internal interface IReadOnlyZoomManager
{
	public double GetSizeZoom();
	public double GetSpacingZoom();
	public double GetSizeCap();
}

/// <summary>
/// Class for managing zoom values controlled by the mouse scroll wheel.
/// Expected Usage:
///  Call ProcessInput once per frame.
///  Call Update once per frame after ProcessInput.
/// </summary>
internal class ZoomManager : Fumen.IObserver<PreferencesScroll>, IReadOnlyZoomManager
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
		private double Min;
		private double Max;
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

		public void UpdateBounds(double min, double max)
		{
			Min = min;
			Max = max;
			if (Value < min || Value > max)
				SetValue(Value, true);
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
	public const double MinSizeCap = MinZoom;
	public const double MaxSizeCap = 1.0;
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
	private double SizeCap;

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
		SizeCap = pScroll.SizeCap;

		AllValues =
		[
			ZoomData,
			ConstantTimeSpacingData,
			ConstantRowSpacingData,
			VariableSpacingData,
		];

		Preferences.Instance.PreferencesScroll.AddObserver(this);
		RefreshZoomLimit();
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
		var pKeyBinds = Preferences.Instance.PreferencesKeyBinds;

		var scrollShouldZoom = keyCommandManager.IsAnyInputDown(pKeyBinds.ScrollZoom);
		var scrollShouldScaleDefaultSpacing = !scrollShouldZoom && keyCommandManager.IsAnyInputDown(pKeyBinds.ScrollSpacing);

		// If the scroll wheel hasn't moved we don't need the input.
		if (scrollDelta.FloatEquals(0.0f))
			return false;

		var scrollMultiplier = Interpolation.Lerp(1.0, pScroll.ZoomMultiplier, 0.0, 1.0, Math.Abs(scrollDelta));
		if (scrollDelta < 0.0)
			scrollMultiplier *= -1;

		// Adjust zoom.
		if (scrollShouldZoom)
		{
			ZoomData.StartInterpolation(currentTime, scrollMultiplier);
			return true;
		}

		// Adjust the default spacing value for the current SpacingMode
		if (scrollShouldScaleDefaultSpacing)
		{
			switch (pScroll.SpacingMode)
			{
				case SpacingMode.ConstantTime:
					ConstantTimeSpacingData.StartInterpolation(currentTime, scrollMultiplier);
					break;
				case SpacingMode.ConstantRow:
					ConstantRowSpacingData.StartInterpolation(currentTime, scrollMultiplier);
					break;
				case SpacingMode.Variable:
					VariableSpacingData.StartInterpolation(currentTime, scrollMultiplier);
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
	/// Sets the size cap. Will be clamped.
	/// </summary>
	/// <param name="sizeCap">New size cap value.</param>
	public void SetSizeCap(double sizeCap)
	{
		SizeCap = Math.Clamp(sizeCap, MinSizeCap, MaxSizeCap);
		RefreshZoomLimit();
	}

	private void RefreshZoomLimit()
	{
		ZoomData.UpdateBounds(MinZoom, Preferences.Instance.PreferencesScroll.LimitZoomToSize ? SizeCap : MaxZoom);
	}

	/// <summary>
	/// Gets the zoom to use for sizing objects.
	/// When zooming in we only zoom the spacing, not the scale of objects.
	/// </summary>
	/// <returns>Zoom level to be used as a multiplier.</returns>
	public double GetSizeZoom()
	{
		return Math.Min(SizeCap, ZoomData.GetValue() > MaxSizeCap ? MaxSizeCap : ZoomData.GetValue());
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
	/// Gets the size cap value.
	/// </summary>
	/// <returns>Size cap value.</returns>
	public double GetSizeCap()
	{
		return SizeCap;
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
			case PreferencesScroll.NotificationSizeCapChanged:
				SetSizeCap(Preferences.Instance.PreferencesScroll.SizeCap);
				break;
			case PreferencesScroll.NotificationLimitZoomToSizeChanged:
				RefreshZoomLimit();
				break;
		}
	}
}
