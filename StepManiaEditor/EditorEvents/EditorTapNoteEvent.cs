﻿using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditor;

internal sealed class EditorTapNoteEvent : EditorEvent
{
	private readonly LaneTapNote LaneTapNote;

	public EditorTapNoteEvent(EventConfig config, LaneTapNote chartEvent) : base(config)
	{
		LaneTapNote = chartEvent;
	}

	public override string GetShortTypeName()
	{
		return "Tap";
	}

	public override int GetLane()
	{
		return LaneTapNote.Lane;
	}

	public override bool IsStep()
	{
		return true;
	}

	public override bool IsLaneNote()
	{
		return true;
	}

	public override bool IsConsumedByReceptors()
	{
		return !IsFake();
	}

	public override bool IsMiscEvent()
	{
		return false;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return true;
	}

	public override bool IsSelectableWithModifiers()
	{
		return false;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		var alpha = GetRenderAlpha();
		if (alpha <= 0.0f)
			return;
		DrawTap(textureAtlas, spriteBatch, arrowGraphicManager);
		if (IsFake())
			DrawFakeMarker(textureAtlas, spriteBatch, arrowGraphicManager);
	}
}
