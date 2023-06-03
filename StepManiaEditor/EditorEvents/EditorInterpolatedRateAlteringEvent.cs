using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Fumen;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

internal sealed class EditorInterpolatedRateAlteringEvent : EditorEvent
{
	public static readonly string EventShortDescription =
		"StepMania refers to these events as \"speeds\".\n" +
		"These events change the scroll rate smoothly over a specified period of time from the previous\n" +
		"interpolated scroll rate value to the newly specified value.\n" +
		"If the specified time is 0 then the scroll rate changes instantly.\n" +
		"Unlike non-interpolated scroll rate changes, the player cannot see the effects of interpolated\n" +
		"scroll rate changes before they begin.\n" +
		"Interpolated scroll rate changes and non-interpolated scroll rate changes are independent.";

	public static readonly string WidgetHelp =
		"Interpolated Scroll Rate.\n" +
		"Expected format: \"<rate>x/<length>rows\" or \"<rate>x/<length>s\". e.g. \"2.0x/48rows\".\n" +
		EventShortDescription;

	public double PreviousScrollRate = 1.0;

	private bool WidthDirty;
	public ScrollRateInterpolation ScrollRateInterpolationEvent;

	public string StringValue
	{
		get
		{
			if (ScrollRateInterpolationEvent.PreferPeriodAsTime)
			{
				return $"{ScrollRateInterpolationEvent.Rate}x/{ScrollRateInterpolationEvent.PeriodTimeSeconds:G9}s";
			}
			else
			{
				return $"{ScrollRateInterpolationEvent.Rate}x/{ScrollRateInterpolationEvent.PeriodLengthIntegerPosition}rows";
			}
		}
		set
		{
			var (valid, rate, periodInt, periodTime, preferTime) = IsValidScrollRateInterpolationString(value);
			if (valid)
			{
				ScrollRateInterpolationEvent.Rate = rate;
				ScrollRateInterpolationEvent.PeriodLengthIntegerPosition = periodInt;
				ScrollRateInterpolationEvent.PeriodTimeSeconds = periodTime;
				ScrollRateInterpolationEvent.PreferPeriodAsTime = preferTime;
				WidthDirty = true;
				EditorChart.OnInterpolatedRateAlteringEventModified(this);
			}
		}
	}

	[SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
	public static (bool, double, int, double, bool) IsValidScrollRateInterpolationString(string v)
	{
		var rate = 0.0;
		var periodIntegerPosition = 0;
		var periodTime = 0.0;
		var preferPeriodAsTime = false;

		var match = Regex.Match(v, @"^(\d+\.?\d*|\d*\.?\d+)x/(\d+\.?\d*|\d*\.?\d+)(s|rows)$");
		if (!match.Success)
			return (false, rate, periodIntegerPosition, periodTime, preferPeriodAsTime);
		if (match.Groups.Count != 4)
			return (false, rate, periodIntegerPosition, periodTime, preferPeriodAsTime);
		if (!double.TryParse(match.Groups[1].Captures[0].Value, out rate))
			return (false, rate, periodIntegerPosition, periodTime, preferPeriodAsTime);
		if (match.Groups[3].Captures[0].Value == "s")
			preferPeriodAsTime = true;
		if (preferPeriodAsTime)
		{
			if (!double.TryParse(match.Groups[2].Captures[0].Value, out var periodSeconds))
				return (false, rate, periodIntegerPosition, periodTime, preferPeriodAsTime);
			periodTime = periodSeconds;
		}
		else
		{
			if (!int.TryParse(match.Groups[2].Captures[0].Value, out periodIntegerPosition))
				return (false, rate, periodIntegerPosition, periodTime, preferPeriodAsTime);
		}

		return (true, rate, periodIntegerPosition, periodTime, preferPeriodAsTime);
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
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventStringWidth(StringValue);
				WidthDirty = false;
			}

			return WidthInternal;
		}
		set => WidthInternal = value;
	}

	public EditorInterpolatedRateAlteringEvent(EventConfig config, ScrollRateInterpolation chartEvent) : base(config)
	{
		ScrollRateInterpolationEvent = chartEvent;
		WidthDirty = true;
	}

	public bool InterpolatesByTime()
	{
		return ScrollRateInterpolationEvent.PreferPeriodAsTime;
	}

	public double GetInterpolatedScrollRateFromTime(double chartTime)
	{
		var eventChartTime = GetChartTime();
		return Interpolation.Lerp(
			PreviousScrollRate,
			ScrollRateInterpolationEvent.Rate,
			eventChartTime,
			eventChartTime + ScrollRateInterpolationEvent.PeriodTimeSeconds,
			chartTime);
	}

	public double GetInterpolatedScrollRateFromRow(double row)
	{
		return Interpolation.Lerp(
			PreviousScrollRate,
			ScrollRateInterpolationEvent.Rate,
			GetRow(),
			GetRow() + ScrollRateInterpolationEvent.PeriodLengthIntegerPosition,
			row);
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
		ImGuiLayoutUtils.MiscEditorEventScrollRateInterpolationInputWidget(
			GetImGuiId(),
			this,
			nameof(StringValue),
			(int)X, (int)Y, (int)W,
			Utils.UISpeedsColorRGBA,
			IsSelected(),
			CanBeDeleted(),
			Alpha,
			WidgetHelp);
	}
}
