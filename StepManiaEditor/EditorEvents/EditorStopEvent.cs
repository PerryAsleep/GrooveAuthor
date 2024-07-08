using System;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;
using static Fumen.FumenExtensions;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorStopEvent : EditorRateAlteringEvent, IEquatable<EditorStopEvent>, IChartRegion
{
	public static readonly string EventShortDescription =
		"Stops pause the chart playback and occur after notes at the same position.\n" +
		"Stop and delay lengths are in seconds.\n" +
		"Negative stop values result in the chart immediately advancing forward in time during gameplay.\n" +
		"The recommended method for accomplishing this effect is to use a warp.";

	public static readonly string WidgetHelp =
		"Stop.\n" +
		"Expected format: \"<time>s\". e.g. \"1.0s\"\n" +
		EventShortDescription;

	private const string Format = "%.9gs";
	private const float Speed = 0.01f;

	private readonly Stop StopEvent;
	private bool WidthDirty;
	private double EndChartPosition;

	public static bool IsStopLengthValid(double stopLength)
	{
		return stopLength != 0.0;
	}

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
		return GetChartPosition() + StopRegionZOffset;
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
		return IRegion.GetColor(StopEvent.LengthSeconds > 0.0 ? StopRegionColor : WarpRegionColor, Alpha);
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

			if (IsStopLengthValid(value) && !StopEvent.LengthSeconds.DoubleEquals(value))
			{
				EditorChart.UpdateStopTime(this, value, ref StopEvent.LengthSeconds);
				WidthDirty = true;
			}
		}
	}

	public double GetStopLengthSeconds()
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

	public EditorStopEvent(EventConfig config, Stop chartEvent) : base(config)
	{
		StopEvent = chartEvent;
		WidthDirty = true;
		Assert(IsStopLengthValid(StopEvent.LengthSeconds));
	}

	public override string GetShortTypeName()
	{
		return "Stop";
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
		if (StopEvent.LengthSeconds > 0)
			return GetChartTime() + StopEvent.LengthSeconds;
		return GetChartTime();
	}

	public override int GetEndRow()
	{
		if (StopEvent.LengthSeconds > 0)
			return GetRow();
		return (int)EndChartPosition;
	}

	public override double GetEndChartPosition()
	{
		if (StopEvent.LengthSeconds > 0)
			return GetChartPosition();
		return EndChartPosition;
	}

	public void RefreshEndChartPosition()
	{
		// For negative stops we need to determine what row the negative stop ends at.
		// There may other rate altering events between the negative stop start and end.
		// We need to find the last rate altering event before the stop end, and use its
		// rate to determine the end position.
		if (StopEvent.LengthSeconds < 0.0)
		{
			var endTime = GetChartTime();
			var enumerator = EditorChart.GetRateAlteringEvents().FindActiveRateAlteringEventEnumerator(this);
			while (enumerator.MoveNext())
			{
				// This event is beyond the negative stop.
				if (enumerator.Current!.GetChartTime() > endTime)
					break;

				EndChartPosition = enumerator.Current!.GetChartPositionFromTime(endTime);
			}
		}
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
			UIStopColorRGBA,
			IsSelected(),
			CanBeDeleted(),
			Speed,
			Format,
			Alpha,
			WidgetHelp);
	}

	#region IEquatable

	public bool Equals(EditorStopEvent other)
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
