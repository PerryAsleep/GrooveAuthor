using System;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

internal sealed class EditorHoldNoteEvent : EditorEvent
{
	/// <summary>
	/// The first underlying Event of the hold: the LaneHoldStartNote.
	/// </summary>
	private readonly LaneHoldStartNote LaneHoldStartNote;

	/// <summary>
	/// The second underlying Event of the hold: the LaneHoldEndNote.
	/// </summary>
	private readonly LaneHoldEndNote LaneHoldEndNote;

	/// <summary>
	/// Whether or not this hold is a roll.
	/// </summary>
	private bool Roll;

	/// <summary>
	/// Whether or not this hold should be considered active (lit by input/autoplay) for rendering.
	/// </summary>
	private bool NextDrawActive;

	/// <summary>
	/// When the hold is active the start needs to be brought down to match the receptor.
	/// This cutoff value is used to cut off the top of the hold and bring the start down.
	/// </summary>
	private double NextDrawActiveYCutoffPoint;

	public EditorHoldNoteEvent(EventConfig config, LaneHoldStartNote startEvent, LaneHoldEndNote endEvent) : base(config)
	{
		LaneHoldStartNote = startEvent;
		LaneHoldEndNote = endEvent;
		Roll = LaneHoldStartNote.SourceType == SMCommon.NoteStrings[(int)SMCommon.NoteType.RollStart];
	}

	public override string GetShortTypeName()
	{
		return IsRoll() ? "Roll" : "Hold";
	}

	public override int GetLane()
	{
		return LaneHoldStartNote.Lane;
	}

	public override void SetLane(int lane)
	{
		Assert(lane >= 0 && lane < EditorChart.NumInputs);
		LaneHoldStartNote.Lane = lane;
		LaneHoldEndNote.Lane = lane;
	}

	protected override void RefreshTimeBasedOnRowImplementation(EditorRateAlteringEvent activeRateAlteringEvent)
	{
		base.RefreshTimeBasedOnRowImplementation(activeRateAlteringEvent);
		RefreshHoldEndTime();
	}

	private void SetNewHoldEndPosition(int row)
	{
		LaneHoldEndNote.IntegerPosition = row;
		var chartTime = 0.0;
		EditorChart.TryGetTimeOfEvent(LaneHoldEndNote, ref chartTime);
		LaneHoldEndNote.TimeSeconds = chartTime;
	}

	public override void SetRow(int row)
	{
		var len = GetRowDuration();
		base.SetRow(row);
		SetNewHoldEndPosition(row + len);
	}

	public void RefreshHoldEndTime()
	{
		SetNewHoldEndPosition(GetRow() + GetRowDuration());
	}

	public void SetRowDuration(int length)
	{
		SetNewHoldEndPosition(GetRow() + length);
	}

	public override double GetEndChartPosition()
	{
		return LaneHoldEndNote.IntegerPosition;
	}

	public override int GetEndRow()
	{
		return LaneHoldEndNote.IntegerPosition;
	}

	public override double GetEndChartTime()
	{
		return LaneHoldEndNote.TimeSeconds;
	}

	public override Event GetAdditionalEvent()
	{
		return GetHoldEndEvent();
	}

	public LaneHoldEndNote GetHoldEndEvent()
	{
		return LaneHoldEndNote;
	}

	public bool IsRoll()
	{
		return Roll;
	}

	public void SetIsRoll(bool roll)
	{
		if (roll == Roll)
			return;
		Roll = roll;
		LaneHoldStartNote.SourceType = Roll ? SMCommon.NoteStrings[(int)SMCommon.NoteType.RollStart] : string.Empty;
		LaneHoldStartNote.DestType = LaneHoldStartNote.SourceType;
		EditorChart.OnHoldTypeChanged(this);
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

	public void SetNextDrawActive(bool active, double y)
	{
		NextDrawActive = active;
		NextDrawActiveYCutoffPoint = y;
	}

	private readonly struct HoldRenderState
	{
		private readonly TextureAtlas TextureAtlas;
		private readonly SpriteBatch SpriteBatch;

		private readonly bool Multiplayer;
		private readonly float Alpha;
		private readonly double Scale;

		public readonly string StartArrowTextureId;

		private readonly string BodyTextureId;
		private readonly bool BodyMirrored;
		public readonly string EndTextureId;
		private readonly float EndRotation;
		private readonly string StartTextureId;
		private readonly bool StartMirrored;

		private readonly string BodyFillTextureId;
		private readonly bool BodyFillMirrored;
		private readonly Color BodyColor;
		private readonly string BodyRimTextureId;
		private readonly bool BodyRimMirrored;

		private readonly string StartFillTextureId;
		private readonly bool StartFillMirrored;
		private readonly Color StartColor;
		private readonly string StartRimTextureId;
		private readonly bool StartRimMirrored;

		private readonly string EndFillTextureId;
		private readonly float EndFillRotation;
		private readonly Color EndColor;
		private readonly string EndRimTextureId;
		private readonly float EndRimRotation;

		private readonly int BodyTextureWidth;
		private readonly int BodyTextureHeight;

		public HoldRenderState(
			TextureAtlas textureAtlas,
			SpriteBatch spriteBatch,
			ArrowGraphicManager arrowGraphicManager,
			EditorHoldNoteEvent holdNoteEvent,
			bool active,
			bool startActive,
			float alpha,
			double scale)
		{
			TextureAtlas = textureAtlas;
			SpriteBatch = spriteBatch;
			Multiplayer = holdNoteEvent.EditorChart.IsMultiPlayer() &&
			              arrowGraphicManager.ShouldColorHoldsAndRollsInMultiplayerCharts();
			Alpha = alpha;
			Scale = scale;

			var selected = holdNoteEvent.IsSelected();
			var row = holdNoteEvent.GetRow();
			var lane = holdNoteEvent.GetLane();
			var player = holdNoteEvent.GetPlayer();
			var roll = holdNoteEvent.IsRoll();
			var startRowForColoring = holdNoteEvent.GetStepColorRow();

			(StartArrowTextureId, _) =
				arrowGraphicManager.GetArrowTexture(startRowForColoring, lane, selected);

			if (roll)
			{
				(BodyTextureId, BodyMirrored) =
					arrowGraphicManager.GetRollBodyTexture(row, lane, active, selected);
				(EndTextureId, EndRotation) =
					arrowGraphicManager.GetRollEndTexture(row, lane, active, selected);
				(StartTextureId, StartMirrored) =
					arrowGraphicManager.GetRollStartTexture(startRowForColoring, lane, startActive, selected);
				if (Multiplayer)
				{
					(BodyFillTextureId, BodyFillMirrored, BodyColor) =
						arrowGraphicManager.GetPlayerRollBodyTextureFill(row, lane, active, selected, player);
					(BodyRimTextureId, BodyRimMirrored) =
						arrowGraphicManager.GetPlayerRollBodyTextureRim(lane, selected);
					(StartFillTextureId, StartFillMirrored, StartColor) =
						arrowGraphicManager.GetPlayerRollStartTextureFill(row, lane, startActive, selected, player);
					(StartRimTextureId, StartRimMirrored) =
						arrowGraphicManager.GetPlayerRollStartTextureRim(lane, selected);
					(EndFillTextureId, EndFillRotation, EndColor) =
						arrowGraphicManager.GetPlayerRollEndTextureFill(row, lane, active, selected, player);
					(EndRimTextureId, EndRimRotation) =
						arrowGraphicManager.GetPlayerRollEndTextureRim(lane, selected);
				}
			}
			else
			{
				(BodyTextureId, BodyMirrored) =
					arrowGraphicManager.GetHoldBodyTexture(row, lane, active, selected);
				(EndTextureId, EndRotation) =
					arrowGraphicManager.GetHoldEndTexture(row, lane, active, selected);
				(StartTextureId, StartMirrored) =
					arrowGraphicManager.GetHoldStartTexture(startRowForColoring, lane, startActive, selected);
				if (Multiplayer)
				{
					(BodyFillTextureId, BodyFillMirrored, BodyColor) =
						arrowGraphicManager.GetPlayerHoldBodyTextureFill(row, lane, active, selected, player);
					(BodyRimTextureId, BodyRimMirrored) =
						arrowGraphicManager.GetPlayerHoldBodyTextureRim(lane, selected);
					(StartFillTextureId, StartFillMirrored, StartColor) =
						arrowGraphicManager.GetPlayerHoldStartTextureFill(row, lane, startActive, selected, player);
					(StartRimTextureId, StartRimMirrored) =
						arrowGraphicManager.GetPlayerHoldStartTextureRim(lane, selected);
					(EndFillTextureId, EndFillRotation, EndColor) =
						arrowGraphicManager.GetPlayerHoldEndTextureFill(row, lane, active, selected, player);
					(EndRimTextureId, EndRimRotation) =
						arrowGraphicManager.GetPlayerHoldEndTextureRim(lane, selected);
				}
			}

			(BodyTextureWidth, BodyTextureHeight) = textureAtlas.GetDimensions(BodyTextureId);
		}

		public void DrawStart(int x, int y, int w)
		{
			if (StartTextureId == null)
				return;

			// It is assumed there is no height padding baked into this texture.
			var (_, holdBodyStartHeight) = TextureAtlas.GetDimensions(StartTextureId);
			var holdBodyStartH = (int)(holdBodyStartHeight * Scale);

			// Draw the multiplayer start graphics.
			if (Multiplayer && !string.IsNullOrEmpty(StartFillTextureId) && !string.IsNullOrEmpty(StartRimTextureId))
			{
				// If the multiplayer overlay has alpha draw the normal graphic below it.
				var p = Preferences.Instance.PreferencesMultiplayer;
				if (p.RoutineNoteColorAlpha < 1.0f)
				{
					TextureAtlas.Draw(
						StartTextureId,
						SpriteBatch,
						new Rectangle(x, y - holdBodyStartH, w, holdBodyStartH),
						0.0f,
						Alpha,
						StartMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
				}

				// Draw fill.
				TextureAtlas.Draw(
					StartFillTextureId,
					SpriteBatch,
					new Rectangle(x, y - holdBodyStartH, w, holdBodyStartH),
					0.0f,
					new Color(StartColor.R, StartColor.G, StartColor.B, (byte)(StartColor.A * Alpha)),
					StartFillMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);

				// Draw rim.
				TextureAtlas.Draw(
					StartRimTextureId,
					SpriteBatch,
					new Rectangle(x, y - holdBodyStartH, w, holdBodyStartH),
					0.0f,
					Alpha,
					StartRimMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			}

			// Draw the normal start graphic.
			else
			{
				TextureAtlas.Draw(
					StartTextureId,
					SpriteBatch,
					new Rectangle(x, y - holdBodyStartH, w, holdBodyStartH),
					0.0f,
					Alpha,
					StartMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			}
		}

		public void DrawBody(int x, int y, int w, int minY)
		{
			var bodyTileH = (int)(BodyTextureHeight * Scale + 0.5);
			var spriteEffects = BodyMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			var fillSpriteEffects = BodyFillMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			var rimSpriteEffects = BodyRimMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			var multiplayer = Multiplayer && !string.IsNullOrEmpty(BodyFillTextureId) && !string.IsNullOrEmpty(BodyRimTextureId);
			var multiplayerWithAlpha = Preferences.Instance.PreferencesMultiplayer.RoutineNoteColorAlpha < 1.0f;
			var fillColor = new Color(BodyColor.R, BodyColor.G, BodyColor.B, (byte)(BodyColor.A * Alpha));

			// Draw the body by looping up from the bottom, ensuring that each tiled body texture aligns
			// perfectly with the previous one. We cannot use texture wrapping here because the image
			// is a sub-texture and wrapping only works on entire textures.
			while (y >= minY)
			{
				var h = Math.Min(bodyTileH, y - minY);
				if (h == 0)
					break;
				y -= h;
				if (y < -bodyTileH)
					break;
				if (h < bodyTileH)
				{
					var sourceH = (int)(BodyTextureHeight * ((double)h / bodyTileH));
					var sourceRect = new Rectangle(0, BodyTextureHeight - sourceH, BodyTextureWidth, sourceH);
					var destRect = new Rectangle(x, y, w, h);

					// Draw the multiplayer hold graphics.
					if (multiplayer)
					{
						// If the multiplayer overlay has alpha draw the normal graphic below it.
						if (multiplayerWithAlpha)
						{
							TextureAtlas.Draw(
								BodyTextureId,
								SpriteBatch,
								sourceRect,
								destRect,
								0.0f,
								Alpha,
								spriteEffects);
						}

						// Draw fill.
						TextureAtlas.Draw(
							BodyFillTextureId,
							SpriteBatch,
							sourceRect,
							destRect,
							0.0f,
							fillColor,
							fillSpriteEffects);

						// Draw rim.
						TextureAtlas.Draw(
							BodyRimTextureId,
							SpriteBatch,
							sourceRect,
							destRect,
							0.0f,
							Alpha,
							rimSpriteEffects);
					}

					// Draw the normal hold.
					else
					{
						TextureAtlas.Draw(
							BodyTextureId,
							SpriteBatch,
							sourceRect,
							destRect,
							0.0f,
							Alpha,
							spriteEffects);
					}
				}
				else
				{
					var destRect = new Rectangle(x, y, w, h);

					// Draw the multiplayer hold graphics.
					if (multiplayer)
					{
						// If the multiplayer overlay has alpha draw the normal graphic below it.
						if (multiplayerWithAlpha)
						{
							TextureAtlas.Draw(
								BodyTextureId,
								SpriteBatch,
								destRect,
								0.0f,
								Alpha,
								spriteEffects);
						}

						// Draw fill.
						TextureAtlas.Draw(
							BodyFillTextureId,
							SpriteBatch,
							destRect,
							0.0f,
							fillColor,
							fillSpriteEffects);

						// Draw rim.
						TextureAtlas.Draw(
							BodyRimTextureId,
							SpriteBatch,
							destRect,
							0.0f,
							Alpha,
							rimSpriteEffects);
					}
					// Draw the normal hold.
					else
					{
						TextureAtlas.Draw(
							BodyTextureId,
							SpriteBatch,
							destRect,
							0.0f,
							Alpha,
							spriteEffects);
					}
				}
			}
		}

		public void DrawEnd(int x, int y, int w, int h)
		{
			var destination = new Rectangle(x, y, w, h);

			// Draw the multiplayer hold end graphics.
			if (Multiplayer && !string.IsNullOrEmpty(EndFillTextureId) && !string.IsNullOrEmpty(EndRimTextureId))
			{
				// If the multiplayer overlay has alpha draw the normal graphic below it.
				var p = Preferences.Instance.PreferencesMultiplayer;
				if (p.RoutineNoteColorAlpha < 1.0f)
				{
					TextureAtlas.Draw(
						EndTextureId,
						SpriteBatch,
						destination,
						EndRotation,
						Alpha,
						SpriteEffects.None);
				}

				// Draw fill.
				TextureAtlas.Draw(
					EndFillTextureId,
					SpriteBatch,
					destination,
					EndFillRotation,
					new Color(EndColor.R, EndColor.G, EndColor.B, (byte)(EndColor.A * Alpha)),
					SpriteEffects.None);

				// Draw rim.
				TextureAtlas.Draw(
					EndRimTextureId,
					SpriteBatch,
					destination,
					EndRimRotation,
					Alpha,
					SpriteEffects.None);
			}

			// Draw the normal hold end.
			else
			{
				TextureAtlas.Draw(
					EndTextureId,
					SpriteBatch,
					destination,
					EndRotation,
					Alpha,
					SpriteEffects.None);
			}
		}
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		var alpha = GetRenderAlpha();
		if (alpha <= 0.0f)
		{
			NextDrawActive = false;
			NextDrawActiveYCutoffPoint = 0.0;
			return;
		}

		var active = NextDrawActive && Preferences.Instance.PreferencesReceptors.AutoPlayLightHolds;
		var activeAndCutoff = NextDrawActive && Preferences.Instance.PreferencesReceptors.AutoPlayHideArrows;

		var state = new HoldRenderState(textureAtlas, spriteBatch, arrowGraphicManager, this, active, NextDrawActive, alpha,
			Scale);
		var (_, startArrowHeight) = textureAtlas.GetDimensions(state.StartArrowTextureId);
		var halfArrowHeight = startArrowHeight * 0.5 * Scale;
		var (_, capH) = textureAtlas.GetDimensions(state.EndTextureId);

		// Determine the Y value and height to use.
		// If the note is active, we should bring down the top to the cutoff point.
		var bodyY = Y + halfArrowHeight;
		var noteH = H - halfArrowHeight;
		if (activeAndCutoff)
		{
			noteH -= NextDrawActiveYCutoffPoint - bodyY;
			bodyY = NextDrawActiveYCutoffPoint;
		}

		capH = (int)(capH * Scale + 0.5);
		var y = (int)(bodyY + noteH + 0.5) - capH;
		var minY = (int)(bodyY + 0.5);
		var x = (int)(X + 0.5);
		var w = (int)(W + 0.5);

		// Record the cap position for drawing later.
		// Round down on the minimumCapY to avoid rounding errors for 0 length
		// holds, which are more common than negative holds.
		var capY = y;
		var minimumCapY = (int)bodyY;
		if (arrowGraphicManager.AreHoldCapsCentered())
		{
			y += (int)(capH * 0.5f);
			minimumCapY = (int)Y;
		}

		// Adjust the starting y value so we don't needlessly loop when zoomed in and a large
		// area of the hold is off the screen.
		if (y > ScreenHeight + capH)
		{
			y -= (y - (int)ScreenHeight) / capH * capH;
		}

		// Draw the body.
		state.DrawBody(x, y, w, minY);

		// Some arrows, like solo diagonals need a hold start graphic to fill the gap at the top of the hold
		// between the arrow midpoint and the widest part of the arrow.
		state.DrawStart(x, minY, w);

		// Draw the cap, if it is visible.
		// Also ensure that the cap is below the start. In negative scroll rate regions it may be
		// above the start, in which case we do not want to render it.
		// The cap should be drawn after the body as some caps render on top of the body.
		if (capY > -capH && capY < ScreenHeight && capY >= minimumCapY)
		{
			state.DrawEnd(x, capY, w, capH);
		}

		// Draw the arrow at the start of the hold.
		var holdStartY = bodyY - halfArrowHeight;
		DrawTap(textureAtlas, spriteBatch, arrowGraphicManager, X, holdStartY);

		// Draw the fake marker if this note is a fake.
		if (IsFake())
			DrawFakeMarker(textureAtlas, spriteBatch, state.StartArrowTextureId, X, holdStartY);

		// Reset active flags.
		NextDrawActive = false;
		NextDrawActiveYCutoffPoint = 0.0;
	}

	public bool Matches(EditorHoldNoteEvent other)
	{
		return base.Matches(other)
		       && LaneHoldStartNote.Matches(other.LaneHoldStartNote)
		       && LaneHoldEndNote.Matches(other.LaneHoldEndNote)
		       && Roll == other.Roll;
	}

	public override bool Matches(EditorEvent other)
	{
		if (other.GetType() != GetType())
			return false;
		return Matches((EditorHoldNoteEvent)other);
	}
}
