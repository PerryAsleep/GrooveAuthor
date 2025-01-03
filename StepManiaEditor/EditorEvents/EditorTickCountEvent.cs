﻿using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditor;

internal sealed class EditorTickCountEvent : EditorEvent
{
	public static readonly string EventShortDescription =
		"Ticks represents the number of times per beat that hold notes should contribute towards\n" +
		$"combo. StepMania defines a beat as {SMCommon.MaxValidDenominator} rows.";

	public static readonly string WidgetHelp =
		"Ticks.\n" +
		"Expected format: \"<ticks>ticks\". e.g. \"4ticks\"\n" +
		"Tick value must be non-negative.\n" +
		EventShortDescription;

	private const string Format = "%iticks";
	private const float Speed = 0.1f;

	private readonly TickCount TickCountEvent;
	private bool WidthDirty;

	public int IntValue
	{
		get => TickCountEvent.Ticks;
		set
		{
			if (value != TickCountEvent.Ticks && value >= 0)
			{
				TickCountEvent.Ticks = value;
				WidthDirty = true;
			}
		}
	}

	public EditorTickCountEvent(EventConfig config, TickCount chartEvent) : base(config)
	{
		TickCountEvent = chartEvent;
		WidthDirty = true;
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

	public override string GetShortTypeName()
	{
		return "Ticks";
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
		ImGuiLayoutUtils.MiscEditorEventDragIntWidget(
			GetImGuiId(),
			this,
			nameof(IntValue),
			(int)X, (int)Y, (int)W,
			Utils.UITicksColorRGBA,
			IsSelected(),
			CanBeDeleted(),
			Speed,
			Format,
			Alpha,
			WidgetHelp,
			0);
	}
}
