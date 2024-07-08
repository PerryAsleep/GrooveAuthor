using System;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;
using static Fumen.FumenExtensions;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorFakeSegmentEvent : EditorEvent, IEquatable<EditorFakeSegmentEvent>, IChartRegion
{
	public static readonly string EventShortDescription =
		"Notes that occur during a fake region are not counted.";

	public static readonly string WidgetHelp =
		"Fake Region.\n" +
		EventShortDescription + "\n" +
		"Expected format: \"<time>s\". e.g. \"1.0s\"\n" +
		"Fake region lengths are in seconds and must be non-negative.";

	public const double MinFakeSegmentLength = 0.000001;

	private const string Format = "%.9gs";
	private const float Speed = 0.01f;

	private readonly FakeSegment FakeSegmentEvent;
	private bool WidthDirty;
	private double EndChartPosition;

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
		return GetChartPosition() + FakeRegionZOffset;
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
		return GetChartTimeDuration();
	}

	public Color GetRegionColor()
	{
		return IRegion.GetColor(FakeRegionColor, Alpha);
	}

	#endregion IChartRegion Implementation

	public double DoubleValue
	{
		get => FakeSegmentEvent.LengthSeconds;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			if (value >= MinFakeSegmentLength && !FakeSegmentEvent.LengthSeconds.DoubleEquals(value))
			{
				var oldEndTime = GetEndChartTime();
				FakeSegmentEvent.LengthSeconds = value;
				WidthDirty = true;
				EditorChart.OnFakeSegmentTimeModified(this, oldEndTime, GetEndChartTime());
			}
		}
	}

	public double GetFakeTimeSeconds()
	{
		return FakeSegmentEvent.LengthSeconds;
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

	public EditorFakeSegmentEvent(EventConfig config, FakeSegment chartEvent) : base(config)
	{
		FakeSegmentEvent = chartEvent;
		WidthDirty = true;

		Assert(FakeSegmentEvent.LengthSeconds >= MinFakeSegmentLength);
		if (FakeSegmentEvent.LengthSeconds < MinFakeSegmentLength)
			FakeSegmentEvent.LengthSeconds = MinFakeSegmentLength;
	}

	public override string GetShortTypeName()
	{
		return "Fake Region";
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

	public void RefreshEndChartPosition()
	{
		EditorChart.TryGetChartPositionFromTime(GetEndChartTime(), ref EndChartPosition);
	}

	public override int GetEndRow()
	{
		return (int)EndChartPosition;
	}

	public override double GetEndChartPosition()
	{
		return EndChartPosition;
	}

	public override double GetEndChartTime()
	{
		return GetChartTime() + FakeSegmentEvent.LengthSeconds;
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
			UIFakesColorRGBA,
			IsSelected(),
			true,
			Speed,
			Format,
			Alpha,
			WidgetHelp,
			MinFakeSegmentLength);
	}

	#region IEquatable

	public bool Equals(EditorFakeSegmentEvent other)
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
