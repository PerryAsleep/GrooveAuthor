using System;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorWarpEvent : EditorRateAlteringEvent, IEquatable<EditorWarpEvent>, IChartRegion
{
	public static readonly string EventShortDescription =
		"A warp will instantly advance the chart forward by the specified number of rows.\n" +
		"This is the preferred method of achieving this effect rather than using negative\n" +
		"stops or tempos. Warp durations are specified in rows where one beat in StepMania is\n" +
		$"{SMCommon.MaxValidDenominator} rows.";

	public static readonly string WidgetHelp =
		"Warp.\n" +
		"Expected format: \"<length>rows\". e.g. \"48rows\"\n" +
		"Length must be non-negative.\n" +
		EventShortDescription;

	public const int MinLength = 1;

	private const string Format = "%irows";
	private const float Speed = 1.0f;

	private readonly Warp WarpEvent;
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
		return GetChartPosition() + WarpRegionZOffset;
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
		return WarpRegionColor;
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

	public int IntValue
	{
		get => WarpEvent.LengthIntegerPosition;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			if (WarpEvent.LengthIntegerPosition != value && value >= MinLength)
			{
				var oldPosition = GetEndChartPosition();
				WarpEvent.LengthIntegerPosition = value;
				WidthDirty = true;
				EditorChart.OnWarpLengthModified(this, oldPosition, GetEndChartPosition());
			}
		}
	}

	public int GetWarpLengthRows()
	{
		return WarpEvent.LengthIntegerPosition;
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

	public EditorWarpEvent(EventConfig config, Warp chartEvent) : base(config)
	{
		WarpEvent = chartEvent;
		WidthDirty = true;

		Assert(WarpEvent.LengthIntegerPosition >= MinLength);
		if (WarpEvent.LengthIntegerPosition < MinLength)
			WarpEvent.LengthIntegerPosition = MinLength;
	}

	public override string GetShortTypeName()
	{
		return "Warp";
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
		return GetChartPosition() + WarpEvent.LengthIntegerPosition;
	}

	public override int GetEndRow()
	{
		return GetRow() + WarpEvent.LengthIntegerPosition;
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
			UIWarpColorRGBA,
			IsSelected(),
			CanBeDeleted(),
			Speed,
			Format,
			Alpha,
			WidgetHelp,
			MinLength);
	}

	#region IEquatable

	public bool Equals(EditorWarpEvent other)
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
