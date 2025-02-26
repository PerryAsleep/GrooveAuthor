﻿using System.Text.RegularExpressions;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditor;

internal sealed class EditorMultipliersEvent : EditorEvent
{
	public static readonly string EventShortDescription =
		"Multipliers represent hit and miss multiplier values to apply to combo.";

	public static readonly string WidgetHelp =
		"Combo Multipliers.\n" +
		"Expected format: \"<hit multiplier>x/<miss multiplier>x\". e.g. \"1x/1x\"\n" +
		"Multipliers must be non-negative integer values.\n"
		+ EventShortDescription;

	private readonly Multipliers MultipliersEvent;
	private bool WidthDirty;

	public string StringValue
	{
		get => GetMultipliersString();
		set
		{
			var (valid, hit, miss) = IsValidMultipliersString(value);
			if (valid)
			{
				if (MultipliersEvent.HitMultiplier != hit || MultipliersEvent.MissMultiplier != miss)
				{
					MultipliersEvent.HitMultiplier = hit;
					MultipliersEvent.MissMultiplier = miss;
					WidthDirty = true;
				}
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
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventStringWidth(StringValue);
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

	public static (bool, int, int) IsValidMultipliersString(string v)
	{
		var hitMultiplier = 1;
		var missMultiplier = 1;

		var match = Regex.Match(v, @"^(\d+)x/(\d+)x$");
		if (!match.Success)
			return (false, hitMultiplier, missMultiplier);
		if (match.Groups.Count != 3)
			return (false, hitMultiplier, missMultiplier);
		if (!int.TryParse(match.Groups[1].Captures[0].Value, out hitMultiplier))
			return (false, hitMultiplier, missMultiplier);
		if (!int.TryParse(match.Groups[2].Captures[0].Value, out missMultiplier))
			return (false, hitMultiplier, missMultiplier);
		return (true, hitMultiplier, missMultiplier);
	}

	public override string GetShortTypeName()
	{
		return "Multipliers";
	}

	public string GetMultipliersString()
	{
		return $"{MultipliersEvent.HitMultiplier}x/{MultipliersEvent.MissMultiplier}x";
	}

	public EditorMultipliersEvent(EventConfig config, Multipliers chartEvent) : base(config)
	{
		MultipliersEvent = chartEvent;
		WidthDirty = true;
	}

	public int GetHitMultiplier()
	{
		return MultipliersEvent.HitMultiplier;
	}

	public int GetMissMultiplier()
	{
		return MultipliersEvent.MissMultiplier;
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
		ImGuiLayoutUtils.MiscEditorEventMultipliersWidget(
			GetImGuiId(),
			this,
			nameof(StringValue),
			(int)X, (int)Y, (int)W,
			Utils.UIMultipliersColorRGBA,
			IsSelected(),
			CanBeDeleted(),
			Alpha,
			WidgetHelp);
	}
}
