using System;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorFakeSegmentEvent : EditorEvent, IEquatable<EditorFakeSegmentEvent>, IChartRegion
{
	public static readonly string EventShortDescription =
		"Notes that occur during a fake region are not counted.";

	public static readonly string WidgetHelp =
		"Fake Region.\n" +
		EventShortDescription + "\n" +
		"Expected format: \"<length>rows\". e.g. \"48rows\"\n" +
		"Length must be non-negative.";

	public const int MinFakeSegmentLength = 1;

	private const string Format = "%irows";
	private const float Speed = 1.0f;

	private readonly FakeSegment FakeSegmentEvent;
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

	public int IntValue
	{
		get => FakeSegmentEvent.LengthIntegerPosition;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			if (FakeSegmentEvent.LengthIntegerPosition != value && value >= MinFakeSegmentLength)
			{
				var oldPosition = GetEndChartPosition();
				FakeSegmentEvent.LengthIntegerPosition = value;
				WidthDirty = true;
				EditorChart.OnFakeSegmentLengthModified(this, oldPosition, GetEndChartPosition());
			}
		}
	}

	public int GetFakeLengthRows()
	{
		return FakeSegmentEvent.LengthIntegerPosition;
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
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventDragIntWidgetWidth(IntValue, Format);
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

	public EditorFakeSegmentEvent(EventConfig config, FakeSegment chartEvent) : base(config)
	{
		FakeSegmentEvent = chartEvent;
		WidthDirty = true;

		Assert(FakeSegmentEvent.LengthIntegerPosition >= MinFakeSegmentLength);
		if (FakeSegmentEvent.LengthIntegerPosition < MinFakeSegmentLength)
			FakeSegmentEvent.LengthIntegerPosition = MinFakeSegmentLength;
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

	public override double GetEndChartPosition()
	{
		return GetChartPosition() + FakeSegmentEvent.LengthIntegerPosition;
	}

	public override int GetEndRow()
	{
		return GetRow() + FakeSegmentEvent.LengthIntegerPosition;
	}

	public override double GetEndChartTime()
	{
		var endChartTime = 0.0;
		EditorChart.TryGetTimeFromChartPosition(GetEndChartPosition(), ref endChartTime);
		return endChartTime;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		if (Alpha <= 0.0f)
			return;
		ImGuiLayoutUtils.MiscEditorEventDragIntWidget(
			GetImGuiId(),
			this,
			nameof(IntValue),
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
