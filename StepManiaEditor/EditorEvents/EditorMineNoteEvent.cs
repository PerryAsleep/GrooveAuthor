﻿using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditor;

internal sealed class EditorMineNoteEvent : EditorEvent
{
	private readonly LaneNote LaneNote;

	public EditorMineNoteEvent(EventConfig config, LaneNote chartEvent) : base(config)
	{
		LaneNote = chartEvent;
	}

	public override string GetShortTypeName()
	{
		return "Mine";
	}

	public override int GetLane()
	{
		return LaneNote.Lane;
	}

	public override bool IsLaneNote()
	{
		return true;
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

		var pos = new Vector2((float)X, (float)Y);
		var selected = IsSelected();
		var player = GetPlayer();

		// Draw fill.
		var (textureId, c) = arrowGraphicManager.GetMineFillTexture(selected, player);
		c.A = (byte)(alpha * byte.MaxValue);
		textureAtlas.Draw(textureId, spriteBatch, pos, Scale, 0.0f, c);

		// Draw rim.
		textureId = arrowGraphicManager.GetMineRimTexture(selected);
		textureAtlas.Draw(textureId, spriteBatch, pos, Scale, 0.0f, alpha);

		if (IsFake())
			DrawFakeMarker(textureAtlas, spriteBatch, arrowGraphicManager);
	}
}
