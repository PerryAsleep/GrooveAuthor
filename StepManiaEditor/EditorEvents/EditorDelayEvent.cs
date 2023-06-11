using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static Fumen.FumenExtensions;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorDelayEvent : EditorRateAlteringEvent, IChartRegion
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

	public Stop StopEvent;
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

	public double GetRegionPosition()
	{
		return ChartEvent.TimeSeconds;
	}

	public double GetRegionDuration()
	{
		return DoubleValue;
	}

	public bool AreRegionUnitsTime()
	{
		return true;
	}

	public bool IsVisible(SpacingMode mode)
	{
		// Do not draw negative stop regions. It looks incorrect to have the region begin
		// before the negative stop starts.
		return mode == SpacingMode.ConstantTime
		       && GetRegionDuration() > 0.0;
	}

	public Color GetRegionColor()
	{
		return IRegion.GetColor(DelayRegionColor, Alpha);
	}

	#endregion IChartRegion Implementation

	public double DoubleValue
	{
		get => StopEvent.LengthSeconds;
		set
		{
			if (!StopEvent.LengthSeconds.DoubleEquals(value))
			{
				StopEvent.LengthSeconds = value;
				WidthDirty = true;
				EditorChart.OnRateAlteringEventModified(this);
			}
		}
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

	public EditorDelayEvent(EventConfig config, Stop chartEvent) : base(config)
	{
		StopEvent = chartEvent;
		WidthDirty = true;
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
			WidgetHelp);
	}
}
