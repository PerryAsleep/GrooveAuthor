﻿using System;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;
using static Fumen.FumenExtensions;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorDelayEvent : EditorRateAlteringEvent, IEquatable<EditorDelayEvent>, IChartRegion
{
	public static readonly string EventShortDescription =
		"Delays pause the chart playback and occur before notes at the same position.\n" +
		"Stop and delay lengths are in seconds.\n" +
		"Negative stop values result in the chart immediately advancing forward in time during gameplay.\n" +
		"The recommended method for accomplishing this effect is to use a warp.";

	public static readonly string WidgetHelp =
		"Delay.\n" +
		"Expected format: \"<time>s\". e.g. \"1.0s\"\n" +
		EventShortDescription;

	private const string Format = "%.9gs";
	private const float Speed = 0.01f;

	private readonly Stop StopEvent;
	private bool WidthDirty;

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
		return GetChartPosition() + DelayRegionZOffset;
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
		RegionH = h;
	}

	public double GetChartPositionDurationForRegion()
	{
		return GetChartPositionDuration();
	}

	public double GetChartTimeDurationForRegion()
	{
		// When drawing regions do not use the stop's length, but the total stop
		// time remaining. This allows for earlier negative stops to properly reduce
		// the length of following stops if they overlap.
		if (StopEvent.LengthSeconds > 0)
			return Math.Max(0.0, GetStopTimeRemaining());
		return 0.0;
	}

	public Color GetRegionColor()
	{
		return DelayRegionColor;
	}

	public float GetRegionAlpha()
	{
		return Alpha;
	}

	public bool IsRegionSelection()
	{
		return false;
	}

	#endregion IChartRegion Implementation

	public double DoubleValue
	{
		get => StopEvent.LengthSeconds;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;
			if (value < 0.0)
				return;
			if (!StopEvent.LengthSeconds.DoubleEquals(value))
			{
				EditorChart.UpdateDelayTime(this, value, ref StopEvent.LengthSeconds);
				WidthDirty = true;
			}
		}
	}

	public double GetDelayLengthSeconds()
	{
		return StopEvent.LengthSeconds;
	}

	/// <remarks>
	/// This lazily updates the width if it is dirty.
	/// This is a bit of hack because in order to determine the width we need to call into
	/// ImGui but that is not a thread-safe operation. If we were to set the width when
	/// loading the chart for example, this could crash. By lazily setting it we avoid this
	/// problem as long as we assume the caller of GetW() happens on the main thread.
	/// </remarks>
	private double WidthInternal;

	public override double W
	{
		get
		{
			if (WidthDirty)
			{
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventDragDoubleWidgetWidth(DoubleValue, Format);
				WidthDirty = false;
			}

			return WidthInternal;
		}
		set => WidthInternal = value;
	}

	public override double H
	{
		get => ImGuiLayoutUtils.GetMiscEditorEventHeight();
		set { }
	}

	public EditorDelayEvent(EventConfig config, Stop chartEvent) : base(config)
	{
		StopEvent = chartEvent;
		WidthDirty = true;
		Assert(StopEvent.LengthSeconds > 0.0);
		if (StopEvent.LengthSeconds < 0.0)
			StopEvent.LengthSeconds = 0.0;
	}

	public override string GetShortTypeName()
	{
		return "Delay";
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
		return true;
	}

	public override double GetEndChartTime()
	{
		return GetChartTime() + StopEvent.LengthSeconds;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		if (Alpha <= 0.0f)
			return;
		ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
			GetImGuiId(),
			this,
			nameof(DoubleValue),
			(int)X, (int)Y, (int)W,
			UIDelayColorRGBA,
			IsSelected(),
			CanBeDeleted(),
			Speed,
			Format,
			Alpha,
			WidgetHelp,
			0.0);
	}

	#region IEquatable

	public bool Equals(EditorDelayEvent other)
	{
		// Only implementing IEquatable for IntervalTree.
		return ReferenceEquals(this, other);
	}

	public override bool Equals(object obj)
	{
		// Only implementing IEquatable for IntervalTree.
		return ReferenceEquals(this, obj);
	}

	public override int GetHashCode()
	{
		// Only implementing IEquatable for IntervalTree.
		// ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
		return base.GetHashCode();
	}

	#endregion IEquatable
}
