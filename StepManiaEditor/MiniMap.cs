using System;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

/// <summary>
/// Mini map of the Editor view that also functions as a scroll bar.
/// This functions similarly to the Mini map in applications like VS Code.
///
/// The MiniMap uses these terms:
/// 1) Editor Area: This is the area that is visible in the editor. This area is selectable
///    with the mouse and is rendered with a highlighted grey color. This area also functions
///    like a scroll bar where it moves from the top to the bottom.
/// 2) Full Area: This is the full area of the content. This can be much bigger than the visible
///    area in the Mini map. This corresponds to the area of the entire Chart. This can include
///    padding so that the Editor area can go above the top or below the bottom of the chart.
/// 3) Content Area: The full area but with no padding.
/// 4) MiniMap Area: This is the area that the MiniMap is showing. When the full area is large,
///    the MiniMap area will be only a portion of the full area. This area is derived from the
///    editor area and the full area.
///
/// Expected Usage:
///  Each frame call UpdateBegin, followed by the various Add functions as needed. Finally call UpdateEnd.
///  Alternatively, call UpdateNoChart if there is no data to display.
///  Call Draw to render.
///  For input processing call MouseDown, MouseMove, and MouseUp.
///  To get the position from the MiniMap call GetEditorPosition.
/// </summary>
internal sealed class MiniMap
{
	/// <summary>
	/// Behavior when selecting the MiniMap with the mouse.
	/// </summary>
	public enum SelectMode
	{
		/// <summary>
		/// Move the editor position to the cursor, not the area under cursor.
		/// This is the natural option if you consider the MiniMap like a scrollbar.
		/// </summary>
		MoveToCursor,

		/// <summary>
		/// Move the editor position to the selected area, not to the cursor.
		/// This is the natural option if you consider the MiniMap like a map.
		/// </summary>
		MoveToSelectedArea,
	}

	/// <summary>
	/// Where the MiniMap can be positioned.
	/// </summary>
	public enum Position
	{
		/// <summary>
		/// Mounted to the right side of the window.
		/// </summary>
		RightSideOfWindow,

		/// <summary>
		/// Mounted to the left side of the window.
		/// </summary>
		LeftSideOfWindow,

		/// <summary>
		/// Mounted to the right of the focused chart. Will not moving with scaling.
		/// </summary>
		FocusedChartWithoutScaling,

		/// <summary>
		/// Mounted to the right of the focused chart. Will moving with scaling.
		/// </summary>
		FocusedChartWithScaling,
	}

	/// <summary>
	/// Result when adding a note to the MiniMap each frame.
	/// </summary>
	public enum AddResult
	{
		/// <summary>
		/// The added note was above the top of the MiniMap area and not rendered.
		/// </summary>
		AboveTop,

		/// <summary>
		/// The added note was in range of the MiniMap and was rendered.
		/// </summary>
		InRange,

		/// <summary>
		/// The added note was below the bottom of the MiniMap area and not rendered.
		/// </summary>
		BelowBottom,
	}

	/// <summary>
	/// Number of textures to use for buffering. Double buffering is fine.
	/// </summary>
	private const int NumTextures = 2;

	/// <summary>
	/// Width in pixels of rim border around the MiniMap.
	/// The rim is inset.
	/// </summary>
	private const int RimWidth = 1;

	/// <summary>
	/// RGBA color of the chart background.
	/// </summary>
	private const uint BackgroundColor = 0xFF1E1E1E;

	/// <summary>
	/// RGBA color of the rim border.
	/// </summary>
	private const uint RimColor = 0xFFFFFFFF;

	/// <summary>
	/// RGBA transparent color.
	/// </summary>
	private const uint TransparentColor = 0x00000000;

	/// <summary>
	/// RGBA color of the line separating the areas outside of the Chart content from
	/// the area representing the Chart content.
	/// </summary>
	private const uint ContentMarkerColor = 0xFFFFFFFF;

	/// <summary>
	/// RGBA color of the area outside of the Chart content.
	/// </summary>
	private const uint OutsideContentRangeColor = 0xFF020202;

	/// <summary>
	/// RGBA color of the normal editor area.
	/// </summary>
	private const uint EditorAreaColor = 0xFF303030;

	/// <summary>
	/// RGBA color of the editor area when the mouse is over it but it is not being held.
	/// </summary>
	private const uint EditorAreaMouseOverColor = 0xFF373737;

	/// <summary>
	/// RGBA color of the editor area when it is being held.
	/// </summary>
	private const uint EditorAreaSelectedColor = 0xFF3E3E3E;

	/// <summary>
	/// RGBA color of the cursor line.
	/// </summary>
	private const uint CursorColor = 0xFFCCCCCC;

	/// <summary>
	/// RGBA color of labels.
	/// </summary>
	private static readonly uint LabelColor;

	/// <summary>
	/// RGBA color of patterns.
	/// </summary>
	private static readonly uint PatternColor;

	/// <summary>
	/// RGBA color of the music preview.
	/// </summary>
	private static readonly uint PreviewColor;

	/// <summary>
	/// Textures to render to. Array for double buffering.
	/// </summary>
	private Texture2D[] Textures;

	/// <summary>
	/// Index into Textures array to control which texture we write to while the other is being rendered.
	/// </summary>
	private int TextureIndex;

	/// <summary>
	/// RGBA color data to set on the texture after updating each frame.
	/// </summary>
	private uint[] ColorData;

	/// <summary>
	/// Buffer holding color data for the content region.
	/// </summary>
	private uint[] ClearData;

	/// <summary>
	/// Buffer holding color data for the editor area.
	/// </summary>
	private uint[] ClearDataEditorArea;

	/// <summary>
	/// Buffer holding color data for the editor area when the mouse is over it but it is not being held.
	/// </summary>
	private uint[] ClearDataEditorMouseOverArea;

	/// <summary>
	/// Buffer holding color data for the editor area when it is being held.
	/// </summary>
	private uint[] ClearDataEditorSelectedArea;

	/// <summary>
	/// Buffer holding color data for areas outside of the content region.
	/// </summary>
	private uint[] ClearDataOutsideContentArea;

	/// <summary>
	/// Bounds of the MiniMap in pixels.
	/// </summary>
	private Rectangle Bounds;

	/// <summary>
	/// Height of the visible area of the MiniMap in pixels.
	/// Less than or equal to the Bounds height.
	/// This is tracked separately as UI resizing can cause the visible area to change
	/// often but we do not want to perform expensive texture resizes that often.
	/// </summary>
	private int VisibleHeight;

	/// <summary>
	/// Number of lanes of the underlying Chart.
	/// </summary>
	private uint NumLanes;

	/// <summary>
	/// Width of a note in pixels.
	/// Height is 1.
	/// </summary>
	private uint NoteWidth = 2;

	/// <summary>
	/// Spacing between each note in pixels.
	/// </summary>
	private uint NoteSpacing;

	/// <summary>
	/// Width of a pattern region in pixels.
	/// </summary>
	private uint PatternWidth = 2;

	/// <summary>
	/// Width of the preview region in pixels.
	/// </summary>
	private uint PreviewWidth = 2;

	/// <summary>
	/// Whether or not to quantize positions.
	/// </summary>
	private bool QuantizePositions = true;

	/// <summary>
	/// Percentage of Editor Area to MiniMap Area over which to start fading out.
	/// </summary>
	private double FadeOutPercentage = 1.0;

	/// <summary>
	/// Cached x positions of each lane in pixels relative to Bounds.
	/// </summary>
	private uint[] LaneXPositions;

	/// <summary>
	/// SelectMode for selecting the Editor area.
	/// </summary>
	private SelectMode EditorSelectMode = SelectMode.MoveToCursor;

	/// <summary>
	/// Whether or not the editor area in the MiniMap is currently being grabbed.
	/// </summary>
	private bool Grabbed;

	/// <summary>
	/// Whether or not the mouse is over the editor area.
	/// </summary>
	private bool MouseOverEditor;

	/// <summary>
	/// When grabbing the editor area for scrolling, this stores where within the editor
	/// area the user clicked so that when they scroll the editor doesn't jump to center
	/// on the selected area.
	/// </summary>
	private double GrabbedPositionAsPercentageOfEditorArea;

	/// <summary>
	/// Full area start y value. Units are in Chart space (e.g. time or position) and not pixel space.
	/// </summary>
	private double FullAreaStart;

	/// <summary>
	/// Full area end y value. Units are in Chart space (e.g. time or position) and not pixel space.
	/// </summary>
	private double FullAreaEnd;

	/// <summary>
	/// MiniMap area start y value. Units are in Chart space (e.g. time or position) and not pixel space.
	/// </summary>
	private double MiniMapAreaStart;

	/// <summary>
	/// MiniMap area start y value. Units are in Chart space (e.g. time or position) and not pixel space.
	/// This value will always be unquantized whereas MiniMapAreaStart may or may not be quantized depending on settings.
	/// </summary>
	private double MiniMapAreaStartUnquantized;

	/// <summary>
	/// MiniMap area range (height) value. Units are in Chart space (e.g. time or position) and not pixel space.
	/// </summary>
	private double MiniMapAreaRange;

	/// <summary>
	/// Cached value of VisibleHeight / MiniMapAreaRange.
	/// </summary>
	private double HeightOverMiniMapAreaRange;

	/// <summary>
	/// Editor area start y value. Units are in Chart space (e.g. time or position) and not pixel space.
	/// </summary>
	private double EditorAreaStart;

	/// <summary>
	/// Editor area end y value. Units are in Chart space (e.g. time or position) and not pixel space.
	/// </summary>
	private double EditorAreaEnd;

	/// <summary>
	/// Editor cursor position. Units are in Chart space (e.g. time or position) and not pixel space.
	/// </summary>
	private double CursorPosition;

	/// <summary>
	/// ArrowGraphicManager for getting note colors.
	/// </summary>
	private ArrowGraphicManager ArrowGraphicManager;

	/// <summary>
	/// Static initializer.
	/// </summary>
	static MiniMap()
	{
		// Configure colors based on their UI colors.
		LabelColor = ColorUtils.ColorRGBAMultiply(Utils.UILabelColorRGBA | 0xFF000000, 1.5f);
		PatternColor = Utils.UIPatternColorRGBA | 0xFF000000;
		PreviewColor = Utils.UIPreviewColorRGBA | 0xFF000000;
	}

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="graphicsDevice">GraphicsDevice for rendering.</param>
	/// <param name="bounds">Bounds of the MiniMap in screen space.</param>
	/// <param name="visibleHeight">Visible height of the MiniMap in screen space.</param>
	public MiniMap(GraphicsDevice graphicsDevice, Rectangle bounds, uint visibleHeight)
	{
		UpdateBounds(graphicsDevice, bounds, visibleHeight);
	}

	/// <summary>
	/// Sets the percentage of the Editor Area compared to the MiniMap area over which to start fading out.
	/// </summary>
	/// <param name="fadeOutPercentage">
	/// Percentage of the Editor Area compared to the MiniMap area over which to start fading out.
	/// </param>
	public void SetFadeOutPercentage(double fadeOutPercentage)
	{
		FadeOutPercentage = Math.Clamp(fadeOutPercentage, 0.0, 1.0);
	}

	/// <summary>
	/// Sets whether or not to quantize positions on the MiniMap.
	/// </summary>
	/// <param name="quantizePositions">Whether or not to quantize positions on the MiniMap.</param>
	public void SetShouldQuantizePositions(bool quantizePositions)
	{
		QuantizePositions = quantizePositions;
	}

	/// <summary>
	/// Set the number of lanes for the Chart being shown.
	/// </summary>
	/// <param name="numLanes">Number of lanes.</param>
	public void SetNumLanes(uint numLanes)
	{
		if (NumLanes == numLanes)
			return;

		NumLanes = numLanes;
		UpdateLaneXPositions();
	}

	/// <summary>
	/// Set the note spacing parameters.
	/// </summary>
	/// <param name="noteWidth">Note width in pixels.</param>
	/// <param name="noteSpacing">Spacing between notes in pixels.</param>
	public void SetLaneSpacing(uint noteWidth, uint noteSpacing)
	{
		var dirty = NoteWidth != noteWidth || NoteSpacing != noteSpacing;

		NoteWidth = noteWidth;
		NoteSpacing = noteSpacing;

		if (dirty)
			UpdateLaneXPositions();
	}

	/// <summary>
	/// Set the width to display patterns in pixels.
	/// </summary>
	/// <param name="patternWidth">Pattern width in pixels.</param>
	public void SetPatternWidth(uint patternWidth)
	{
		PatternWidth = patternWidth;
	}

	/// <summary>
	/// Set the width to display the  music preview in pixels.
	/// </summary>
	/// <param name="previewWidth">Music preview width in pixels.</param>
	public void SetPreviewWidth(uint previewWidth)
	{
		PreviewWidth = previewWidth;
	}

	/// <summary>
	/// Updates the cached LaneXPositions based on the Bounds, NoteWidth, and NoteSpacing.
	/// All LaneXPositions will always be within the Bounds.
	/// </summary>
	private void UpdateLaneXPositions()
	{
		LaneXPositions = new uint[NumLanes];
		if (NumLanes < 1)
			return;

		var totalWidth = Bounds.Width - (RimWidth << 1);

		var maxNoteSpacing = NumLanes < 2 ? 0 : (int)(totalWidth - NumLanes * NoteWidth) / (int)(NumLanes - 1);
		var noteSpacing = Math.Min(maxNoteSpacing, (int)NoteSpacing);

		var totalNoteSpacing = (int)(NoteWidth * NumLanes + noteSpacing * (NumLanes - 1));
		if (totalNoteSpacing > totalWidth)
			totalNoteSpacing = totalWidth;

		var startingLanePosition = RimWidth + ((totalWidth - totalNoteSpacing) >> 1);
		for (var lane = 0; lane < NumLanes; lane++)
		{
			LaneXPositions[lane] = (uint)Math.Max(RimWidth, Math.Min(
				startingLanePosition + lane * (noteSpacing + NoteWidth),
				Bounds.Width - RimWidth - NoteWidth));
		}
	}

	/// <summary>
	/// Updates the Bounds to the given bounds Rectangle.
	/// </summary>
	/// <param name="graphicsDevice">Graphics device for recreating textures.</param>
	/// <param name="bounds">New bounds in screen space.</param>
	/// <param name="visibleHeight"></param>
	public void UpdateBounds(GraphicsDevice graphicsDevice, Rectangle bounds, uint visibleHeight)
	{
		var textureDimensionsDirty = bounds.Width != Bounds.Width || bounds.Height != Bounds.Height;
		var visibleHeightDirty = visibleHeight != VisibleHeight;

		Bounds = bounds;
		var oldVisibleHeight = VisibleHeight;
		VisibleHeight = (int)visibleHeight;

		if (Bounds.Height <= 0 || Bounds.Width <= 0)
			return;
		if (!textureDimensionsDirty && !visibleHeightDirty)
			return;

		// Set up the textures.
		if (textureDimensionsDirty)
		{
			Textures = new Texture2D[NumTextures];
			for (var i = 0; i < NumTextures; i++)
			{
				Textures[i] = new Texture2D(graphicsDevice, bounds.Width, bounds.Height, false, SurfaceFormat.Color);
			}
		}

		// Set up the pixel data.
		if (textureDimensionsDirty)
		{
			ColorData = new uint[bounds.Width * bounds.Height];
			ClearData = new uint[bounds.Width * bounds.Height];
			ClearDataEditorArea = new uint[bounds.Width * bounds.Height];
			ClearDataEditorMouseOverArea = new uint[bounds.Width * bounds.Height];
			ClearDataEditorSelectedArea = new uint[bounds.Width * bounds.Height];
			ClearDataOutsideContentArea = new uint[bounds.Width * bounds.Height];
		}

		var startY = 0;
		var endY = Bounds.Height;
		if (!textureDimensionsDirty)
		{
			startY = Math.Max(0, Math.Min(oldVisibleHeight, VisibleHeight) - 1);
			endY = Math.Min(endY, Math.Max(oldVisibleHeight, VisibleHeight));
		}

		for (var x = 0; x < Bounds.Width; x++)
		{
			for (var y = startY; y < endY; y++)
			{
				var i = Bounds.Width * y + x;

				if (y >= VisibleHeight)
				{
					ClearData[i] = TransparentColor;
					ClearDataEditorArea[i] = TransparentColor;
					ClearDataEditorMouseOverArea[i] = TransparentColor;
					ClearDataEditorSelectedArea[i] = TransparentColor;
					ClearDataOutsideContentArea[i] = TransparentColor;
				}
				else if (y < RimWidth || y >= VisibleHeight - RimWidth || x < RimWidth || x >= Bounds.Width - RimWidth)
				{
					ClearData[i] = RimColor;
					ClearDataEditorArea[i] = RimColor;
					ClearDataEditorMouseOverArea[i] = RimColor;
					ClearDataEditorSelectedArea[i] = RimColor;
					ClearDataOutsideContentArea[i] = RimColor;
				}
				else
				{
					ClearData[i] = BackgroundColor;
					ClearDataEditorArea[i] = EditorAreaColor;
					ClearDataEditorMouseOverArea[i] = EditorAreaMouseOverColor;
					ClearDataEditorSelectedArea[i] = EditorAreaSelectedColor;
					ClearDataOutsideContentArea[i] = OutsideContentRangeColor;
				}
			}
		}

		// Bounds affect lane positions.
		UpdateLaneXPositions();
	}

	/// <summary>
	/// Sets the SelectMode for the editor region.
	/// </summary>
	/// <param name="selectMode">SelectMode to user.</param>
	public void SetSelectMode(SelectMode selectMode)
	{
		EditorSelectMode = selectMode;
	}

	/// <summary>
	/// Called when the mouse button is pressed.
	/// </summary>
	/// <param name="screenX">X mouse position in screen space.</param>
	/// <param name="screenY">Y mouse position in screen space.</param>
	/// <returns>Whether or not the MiniMap has captured this input.</returns>
	public bool MouseDown(int screenX, int screenY)
	{
		// Update tracking of if the mouse is over the editor area for visual feedback.
		MouseOverEditor = IsScreenPositionInEditorBounds(screenX, screenY);

		// If the mouse isn't over the MiniMap, do not do any further processing.
		if (!IsScreenPositionInMiniMapBounds(screenX, screenY))
			return false;

		// Force unquantized positions since the editor area is never quantized.
		var editorStartYPixel = GetYPixelRelativeToBounds(EditorAreaStart, true);
		var editorEndYPixel = GetYPixelRelativeToBounds(EditorAreaEnd, true);

		var relativePosY = screenY - Bounds.Y;

		// Grabbed the editor area.
		if (relativePosY >= editorStartYPixel && relativePosY <= editorEndYPixel)
		{
			Grabbed = true;

			// When grabbing the editor region we need to record where on that region it was grabbed so when
			// the mouse moves the editor region moves naturally.
			GrabbedPositionAsPercentageOfEditorArea = (relativePosY - editorStartYPixel) / (editorEndYPixel - editorStartYPixel);
		}

		// Grabbed outside of the editor area.
		else
		{
			GrabbedPositionAsPercentageOfEditorArea = (CursorPosition - EditorAreaStart) / (EditorAreaEnd - EditorAreaStart);

			switch (EditorSelectMode)
			{
				// Move the editor to the cursor, not the area under cursor.
				// This is the natural option if you consider the MiniMap like a scrollbar.
				case SelectMode.MoveToCursor:
				{
					Grabbed = true;
					break;
				}

				// Move the editor to the selected area, not to the cursor.
				// This is the natural option if you consider the MiniMap like a map.
				case SelectMode.MoveToSelectedArea:
				{
					if (!Grabbed)
					{
						var selectedPosition = GetPositionRelativeToPixel(screenY);

						// Center the cursor at the selected location.
						var editorAreaRange = EditorAreaEnd - EditorAreaStart;
						EditorAreaStart = selectedPosition - (CursorPosition - EditorAreaStart);
						EditorAreaEnd = EditorAreaStart + editorAreaRange;

						// Update the MiniMap area based on the editor area.
						SetMiniMapAreaFromEditorArea();

						// When moving the editor to the selected area only grab it if it is now under the cursor.
						if (IsScreenPositionInEditorBounds(screenX, screenY))
						{
							Grabbed = true;
							// When grabbing the editor region we need to record where on that region it was grabbed so when
							// the mouse moves the editor region moves naturally. Force unquantized positions since the
							// editor area is never quantized.
							editorStartYPixel = GetYPixelRelativeToBounds(EditorAreaStart, true);
							editorEndYPixel = GetYPixelRelativeToBounds(EditorAreaEnd, true);
							GrabbedPositionAsPercentageOfEditorArea =
								(relativePosY - editorStartYPixel) / (editorEndYPixel - editorStartYPixel);
						}
					}

					break;
				}
			}
		}

		// Update the areas.
		MouseMove(screenX, screenY);

		return true;
	}

	/// <summary>
	/// Called when the mouse moves.
	/// </summary>
	/// <param name="screenX">Mouse X position in screen space.</param>
	/// <param name="screenY">Mouse Y position in screen space.</param>
	public void MouseMove(int screenX, int screenY)
	{
		// Update tracking of if the mouse is over the editor area for visual feedback.
		MouseOverEditor = IsScreenPositionInEditorBounds(screenX, screenY);

		if (!Grabbed)
			return;

		// If the editor is grabbed, update the editor area and MiniMap area based on the y position.
		UpdateBasedOnSelectedPixel(screenY);
	}

	/// <summary>
	/// Called when the mouse button is released.
	/// </summary>
	/// <param name="screenX">Mouse X position in screen space.</param>
	/// <param name="screenY">Mouse Y position in screen space.</param>
	public void MouseUp(int screenX, int screenY)
	{
		// Update tracking of if the mouse is over the editor area for visual feedback.
		MouseOverEditor = IsScreenPositionInEditorBounds(screenX, screenY);
		// The user is no longer grabbing the editor if they released the mouse button.
		Grabbed = false;
	}

	/// <summary>
	/// Returns whether or not the MiniMap wants to be processing mouse input, which is
	/// equivalent to if the MiniMap's editor region is being grabbed.
	/// </summary>
	/// <returns>Whether or not the MiniMap wants to be processing mouse input.</returns>
	public bool WantsMouse()
	{
		return Grabbed;
	}

	/// <summary>
	/// Updates the MiniMap area and editor area based on the selected y coordinate in screen space.
	/// </summary>
	/// <param name="screenY">Selected Y coordinate in screen space.</param>
	private void UpdateBasedOnSelectedPixel(int screenY)
	{
		// If the zoom is so wide that the editor window doesn't fit, then dragging it scroll
		// doesn't make sense. I can't find a solution which feels natural, so I am disabling
		// it at least for now.
		if (EditorAreaEnd - EditorAreaStart >= MiniMapAreaRange)
			return;

		// Determine what percentage the editor pixel range is at with respect to the bounds.
		var selectedPixel = screenY - Bounds.Y;
		var editorAreaPixelRange =
			GetYPixelRelativeToBounds(EditorAreaEnd, true) - GetYPixelRelativeToBounds(EditorAreaStart, true);
		var editorStartYPixel = selectedPixel - editorAreaPixelRange * GrabbedPositionAsPercentageOfEditorArea;
		var editorStartPositionRange = VisibleHeight - editorAreaPixelRange;
		var percentage = editorStartYPixel / editorStartPositionRange;

		// Put the MiniMap at the appropriate range based on that percentage.
		if (MiniMapAreaRange < FullAreaEnd - FullAreaStart)
		{
			var miniMapStartPositionRange = FullAreaEnd - FullAreaStart - MiniMapAreaRange;
			var percentageForMiniMap = Math.Min(1.0, Math.Max(0.0, percentage));
			SetMiniMapAreaStart(FullAreaStart + percentageForMiniMap * miniMapStartPositionRange);
		}

		// Put the editor area at the appropriate location based on the MiniMap area.
		var editorRange = EditorAreaEnd - EditorAreaStart;
		var editorStartRange = MiniMapAreaRange - editorRange;
		EditorAreaStart = MiniMapAreaStart + percentage * editorStartRange;
		EditorAreaEnd = EditorAreaStart + editorRange;
	}

	/// <summary>
	/// Sets the MiniMap area by updating MiniMapAreaStart.
	/// The MiniMap area is determined by the full area and the editor area.
	/// </summary>
	private void SetMiniMapAreaFromEditorArea()
	{
		// If the MiniMap area is greater than the full area, then it is always in the same
		// place, at the start of the full area.
		if (MiniMapAreaRange >= FullAreaEnd - FullAreaStart)
		{
			SetMiniMapAreaStart(FullAreaStart);
			return;
		}

		// If the MiniMap area is lesser than the full area, then it needs to show only a portion
		// of that full area. The portion shown is based off of the scroll position, which is the
		// editor area's position relative to the full area.
		var percentage = (EditorAreaStart - FullAreaStart) / (FullAreaEnd - FullAreaStart - (EditorAreaEnd - EditorAreaStart));
		var miniMapStartPositionRange = FullAreaEnd - FullAreaStart - MiniMapAreaRange;
		var percentageForMiniMap = Math.Min(1.0, Math.Max(0.0, percentage));
		SetMiniMapAreaStart(FullAreaStart + percentageForMiniMap * miniMapStartPositionRange);
	}

	/// <summary>
	/// Sets MiniMapAreaStart and MiniMapAreaStartUnquantized to the given value.
	/// MiniMapAreaStart will be quantized depending on the value of QuantizePositions.
	/// MiniMapAreaStartUnquantized will store the unquantized value.
	/// </summary>
	private void SetMiniMapAreaStart(double miniMapAreaStart)
	{
		MiniMapAreaStartUnquantized = miniMapAreaStart;
		MiniMapAreaStart = MiniMapAreaStartUnquantized;
		if (QuantizePositions)
		{
			var chartSpacePerPixel = MiniMapAreaRange / VisibleHeight;
			var numPixels = (int)(MiniMapAreaStart / chartSpacePerPixel);
			var remainder = MiniMapAreaStart / (numPixels * chartSpacePerPixel);
			if (remainder >= 0.5)
				numPixels++;
			MiniMapAreaStart = numPixels * chartSpacePerPixel;
		}
	}

	/// <summary>
	/// Returns the editor area start position in Chart space.
	/// </summary>
	/// <returns>The editor area start position.</returns>
	public double GetEditorPosition()
	{
		return EditorAreaStart;
	}

	/// <summary>
	/// Returns the editor area range in Chart space.
	/// </summary>
	/// <returns>The editor area range.</returns>
	public double GetEditorRange()
	{
		return EditorAreaEnd - EditorAreaStart;
	}

	/// <summary>
	/// Returns the MiniMap area start position in Chart space.
	/// </summary>
	/// <returns>The MiniMap area start position.</returns>
	public double GetMiniMapAreaStart()
	{
		return MiniMapAreaStart;
	}

	/// <summary>
	/// Add a tap note represented by the given EditorEvent to the MiniMap.
	/// </summary>
	/// <param name="chartEvent">EditorEvent to add.</param>
	/// <param name="position">Position in Chart space. Can be time or row.</param>
	/// <param name="selected">Whether or not the note is selected.</param>
	/// <returns>AddResult describing if the note was added.</returns>
	public AddResult AddTapNote(EditorEvent chartEvent, double position, bool selected)
	{
		return AddShortNote(
			GetYPixelRelativeToBounds(position),
			LaneXPositions[chartEvent.GetLane()],
			ArrowGraphicManager.GetArrowColor(chartEvent.GetStepColorRow(), chartEvent.GetLane(), selected));
	}

	/// <summary>
	/// Add a mine represented by the given EditorMineNoteEvent to the MiniMap.
	/// </summary>
	/// <param name="chartEvent">EditorMineNoteEvent to add.</param>
	/// <param name="position">Position in Chart space. Can be time or row.</param>
	/// <param name="selected">Whether or not the mine is selected.</param>
	/// <returns>AddResult describing if the note was added.</returns>
	public AddResult AddMine(EditorMineNoteEvent chartEvent, double position, bool selected)
	{
		return AddShortNote(
			GetYPixelRelativeToBounds(position),
			LaneXPositions[chartEvent.GetLane()],
			ArrowGraphicManager.GetMineColor(selected));
	}

	/// <summary>
	/// Add a cursor marker line to the MiniMap.
	/// </summary>
	/// <param name="position">Position in Chart space. Can be time or row.</param>
	/// <returns></returns>
	public AddResult AddCursor(double position)
	{
		// Force the use of an unquantized position for the cursor.
		// This is because it should look locked in with the editor area and we never
		// quantize the editor area.
		return AddHorizontalLine(
			GetYPixelRelativeToBounds(position, true),
			RimWidth,
			(uint)(Bounds.Width - (RimWidth << 1)),
			CursorColor, true);
	}

	/// <summary>
	/// Add a label marker line to the MiniMap.
	/// </summary>
	/// <param name="position">Position in Chart space. Can be time or row.</param>
	/// <returns>AddResult describing if the label was added.</returns>
	public AddResult AddLabel(double position)
	{
		return AddHorizontalLine(
			GetYPixelRelativeToBounds(position),
			RimWidth,
			(uint)(Bounds.Width - (RimWidth << 1)),
			LabelColor);
	}

	/// <summary>
	/// Add a pattern region to the MiniMap.
	/// </summary>
	/// <param name="startPosition">Start position of the pattern in Chart space. Can be time or row.</param>
	/// <param name="endPosition">End position of the pattern in Chart space. Can be time or row.</param>
	/// <returns>AddResult describing if the pattern was added.</returns>
	public AddResult AddPattern(double startPosition, double endPosition)
	{
		return AddRect(
			(uint)Math.Max(RimWidth, Bounds.Width - RimWidth - PatternWidth),
			(uint)(Bounds.Width - RimWidth - 1),
			GetYPixelRelativeToBounds(startPosition),
			GetYPixelRelativeToBounds(endPosition),
			PatternColor
		);
	}

	/// <summary>
	/// Add a preview region to the MiniMap.
	/// </summary>
	/// <param name="startPosition">Start position of the preview in Chart space. Can be time or row.</param>
	/// <param name="endPosition">End position of the preview in Chart space. Can be time or row.</param>
	/// <returns>AddResult describing if the preview was added.</returns>
	public AddResult AddPreview(double startPosition, double endPosition)
	{
		return AddRect(
			RimWidth,
			(uint)Math.Min(Bounds.Width - RimWidth - 1, RimWidth + PreviewWidth - 1),
			GetYPixelRelativeToBounds(startPosition),
			GetYPixelRelativeToBounds(endPosition),
			PreviewColor
		);
	}

	/// <summary>
	/// Add a hold or roll note to the MiniMap.
	/// </summary>
	/// <param name="start">EditorHoldNoteEvent representing the start of the hold.</param>
	/// <param name="startPosition">Start position of the hold in Chart space. Can be time or row.</param>
	/// <param name="endPosition">End position of the hold in Chart space. Can be time or row.</param>
	/// <param name="roll">Whether or not the hold is a roll.</param>
	/// <param name="selected">Whether or not the hold is selected.</param>
	/// <returns>AddResult describing if the hold was added.</returns>
	public AddResult AddHold(EditorHoldNoteEvent start, double startPosition, double endPosition, bool roll, bool selected)
	{
		var yStart = GetYPixelRelativeToBounds(startPosition);
		var yEnd = GetYPixelRelativeToBounds(endPosition) + 1.0;

		var x = LaneXPositions[start.GetLane()];
		var bodyColor = roll
			? ArrowGraphicManager.GetRollColor(start.GetStepColorRow(), start.GetLane(), selected)
			: ArrowGraphicManager.GetHoldColor(start.GetStepColorRow(), start.GetLane(), selected);
		var headColor = ArrowGraphicManager.GetArrowColor(start.GetStepColorRow(), start.GetLane(), selected);

		var w = (uint)Math.Min(Bounds.Width - (RimWidth << 1), NoteWidth);

		var yStartInt = MathUtils.FloorDouble(yStart);
		var y = yStartInt;
		if (y >= VisibleHeight - RimWidth)
			return AddResult.BelowBottom;
		if (yEnd < RimWidth)
			return AddResult.AboveTop;
		if (w == 0)
			return AddResult.InRange;

		var i = 0;
		if (y < RimWidth)
		{
			i += RimWidth - y;
			y = RimWidth;
		}

		while (y < yEnd)
		{
			if (y >= VisibleHeight - RimWidth)
			{
				break;
			}

			uint color;
			if (QuantizePositions)
			{
				color = i == 0 ? headColor : bodyColor;
			}
			else
			{
				// Determine the note color by blending the head and body.
				var noteColor = i == 0 ? headColor : bodyColor;
				if (i == 1)
				{
					var spaceToWorkWith = 1.0;
					if (yEnd < y + 1.0)
						spaceToWorkWith = yEnd - y;
					noteColor = Utils.ColorRGBAInterpolateBGR(bodyColor, headColor,
						(float)((yStart - yStartInt) / spaceToWorkWith));
				}

				// Blend the note color with the background color.
				color = noteColor;
				if (i == 0)
					color = Utils.ColorRGBAInterpolateBGR(noteColor, ColorData[y * Bounds.Width + x],
						(float)(yStart - yStartInt));
				else if (y + 1 >= yEnd)
					color = Utils.ColorRGBAInterpolateBGR(noteColor, ColorData[y * Bounds.Width + x], (float)(1.0 - (yEnd - y)));
			}

			// Set the color.
			for (var j = x; j < x + w; j++)
				ColorData[y * Bounds.Width + j] = color;

			i++;
			y++;
		}

		return AddResult.InRange;
	}

	/// <summary>
	/// Helper method for adding a short, non-hold note.
	/// </summary>
	/// <param name="y">Y position in pixels relative to Bounds.</param>
	/// <param name="x">X position in pixels relative to Bounds.</param>
	/// <param name="color">Color of note to add.</param>
	/// <returns>AddResult describing if the note was added.</returns>
	private AddResult AddShortNote(double y, uint x, uint color)
	{
		return AddHorizontalLine(y, x, (uint)Math.Min(Bounds.Width - (RimWidth << 1), NoteWidth), color);
	}

	/// <summary>
	/// Helper method for adding a horizontal 1-pixel tall line.
	/// </summary>
	/// <param name="y">Y position in pixels relative to Bounds.</param>
	/// <param name="x">X position in pixels relative to Bounds.</param>
	/// <param name="w">Width in pixels.</param>
	/// <param name="color">Color of line.</param>
	/// <param name="forceUnquantized">
	/// If true, then even when configured to use quantized positions return an unquantized result.
	/// </param>
	/// <returns>AddResult describing if the line was added.</returns>
	private AddResult AddHorizontalLine(double y, uint x, uint w, uint color, bool forceUnquantized = false)
	{
		var yInt = MathUtils.FloorDouble(y);

		if (yInt < RimWidth)
			return AddResult.AboveTop;
		if (yInt >= VisibleHeight - RimWidth)
			return AddResult.BelowBottom;
		if (w == 0)
			return AddResult.InRange;

		var i = yInt * Bounds.Width + x;
		var iEnd = i + w;

		if (QuantizePositions && !forceUnquantized)
		{
			for (; i < iEnd; i++)
				ColorData[i] = color;
			return AddResult.InRange;
		}

		var percent = (float)(1.0 - (y - yInt));
		var previousData = 0u;
		var c = 0u;
		for (; i < iEnd; i++)
		{
			if (ColorData[i] != previousData)
			{
				previousData = ColorData[i];
				c = Utils.ColorRGBAInterpolateBGR(ColorData[i], color, percent);
			}

			ColorData[i] = c;
		}

		yInt++;
		if (yInt >= VisibleHeight - RimWidth)
			return AddResult.InRange;

		percent = 1.0f - percent;
		i = yInt * Bounds.Width + x;
		iEnd = i + w;
		previousData = 0u;
		c = 0u;
		for (; i < iEnd; i++)
		{
			if (ColorData[i] != previousData)
			{
				previousData = ColorData[i];
				c = Utils.ColorRGBAInterpolateBGR(ColorData[i], color, percent);
			}

			ColorData[i] = c;
		}

		return AddResult.InRange;
	}

	/// <summary>
	/// Helper method for adding a colored rectangle.
	/// </summary>
	/// <param name="startX">Inclusive start x position in pixels.</param>
	/// <param name="endX">Inclusive end x position in pixels.</param>
	/// <param name="startY">Start y position in pixels.</param>
	/// <param name="endY">End y position in pixels.</param>
	/// <param name="color">Color of the rectangle.</param>
	/// <returns>AddResult describing if the rectangle was added.</returns>
	private AddResult AddRect(uint startX, uint endX, double startY, double endY, uint color)
	{
		var startYInt = MathUtils.FloorDouble(startY);
		var y = startYInt;
		if (y >= VisibleHeight - RimWidth)
			return AddResult.BelowBottom;
		if (endY < RimWidth)
			return AddResult.AboveTop;
		if (endX < startX)
			return AddResult.InRange;

		var i = 0;
		if (y < RimWidth)
		{
			i += RimWidth - y;
			y = RimWidth;
		}

		while (y < endY)
		{
			if (y >= VisibleHeight - RimWidth)
			{
				break;
			}

			var destColor = color;
			if (!QuantizePositions)
			{
				if (i == 0)
					destColor = Utils.ColorRGBAInterpolateBGR(color, ColorData[y * Bounds.Width + startX],
						(float)(startY - startYInt));
				else if (y + 1 >= endY)
					destColor = Utils.ColorRGBAInterpolateBGR(color, ColorData[y * Bounds.Width + startX],
						(float)(1.0 - (endY - y)));
			}

			// Set the color.
			for (var j = startX; j <= endX; j++)
				ColorData[y * Bounds.Width + j] = destColor;

			i++;
			y++;
		}

		return AddResult.InRange;
	}

	/// <summary>
	/// Called to update the MiniMap when there is no Chart to draw data from.
	/// </summary>
	public void UpdateNoChart()
	{
		if (Bounds.Height <= 0 || Bounds.Width <= 0 || VisibleHeight <= 0)
			return;
		Array.Copy(ClearData, ColorData, Bounds.Width * Bounds.Height);
		Textures[TextureIndex].SetData(ColorData);
	}

	/// <summary>
	/// Called at the beginning of an update loop.
	/// Sets up the color data used on the texture for the MiniMap based on the given regions.
	/// </summary>
	/// <param name="fullAreaStart">
	/// Full area start in Chart space.
	/// May be padded to be larger than the content area.</param>
	/// <param name="fullAreaEnd">
	/// Full area end in Chart space.
	/// May be padded to be larger than the content area.</param>
	/// <param name="contentAreaStart">Content area start in Chart space.</param>
	/// <param name="contentAreaEnd">Content area end in Chart space.</param>
	/// <param name="miniMapAreaRange">MiniMap area range in Chart space.</param>
	/// <param name="editorAreaStart">Editor area start in Chart space.</param>
	/// <param name="editorAreaEnd">Editor area end in Chart space.</param>
	/// <param name="cursorPosition">Position of the cursor in Chart space.</param>
	/// <param name="arrowGraphicManager">ArrowGraphicManager to use for getting event colors.</param>
	public void UpdateBegin(
		double fullAreaStart,
		double fullAreaEnd,
		double contentAreaStart,
		double contentAreaEnd,
		double miniMapAreaRange,
		double editorAreaStart,
		double editorAreaEnd,
		double cursorPosition,
		ArrowGraphicManager arrowGraphicManager)
	{
		FullAreaStart = fullAreaStart;
		FullAreaEnd = fullAreaEnd;
		EditorAreaStart = editorAreaStart;
		EditorAreaEnd = editorAreaEnd;
		MiniMapAreaRange = miniMapAreaRange;
		CursorPosition = cursorPosition;
		ArrowGraphicManager = arrowGraphicManager;

		if (Bounds.Height <= 0 || Bounds.Width <= 0 || VisibleHeight <= 0)
			return;

		HeightOverMiniMapAreaRange = VisibleHeight / MiniMapAreaRange;

		SetMiniMapAreaFromEditorArea();

		// If we are zoomed out so far that the MiniMap Area can't fit the Editor Areas or scroll
		// then we can't render anything meaningful and we should early-ouy.
		if (GetEditorAreaPercentageOfMiniMapArea() >= 1.0)
			return;

		Array.Copy(ClearData, ColorData, Bounds.Width * Bounds.Height);

		// TODO: There is a lot of copy/paste logic around blending colors below. Could be cleaned up.

		// Draw area outside of content region on top.
		var contentStartYPixel = GetYPixelRelativeToBounds(contentAreaStart);
		var yStartInt = RimWidth;
		var yEnd = Math.Min(VisibleHeight - RimWidth, contentStartYPixel);
		var yEndInt = MathUtils.FloorDouble(yEnd);
		if (yStartInt < yEndInt)
		{
			// ReSharper disable UselessBinaryOperation
			Array.Copy(ClearDataOutsideContentArea, yStartInt * Bounds.Width,
				ColorData, yStartInt * Bounds.Width, (yEndInt - yStartInt) * Bounds.Width);
			// ReSharper restore UselessBinaryOperation
		}

		if (yEndInt >= RimWidth && yEndInt < VisibleHeight - RimWidth)
		{
			var blendColor =
				Utils.ColorRGBAInterpolateBGR(OutsideContentRangeColor, BackgroundColor, (float)(1.0 - (yEnd - yEndInt)));
			for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
			{
				ColorData[yEndInt * Bounds.Width + x] = blendColor;
			}
		}

		// Draw area outside of content region on bottom.
		var contentEndYPixel = GetYPixelRelativeToBounds(contentAreaEnd);
		var yStart = Math.Max(RimWidth, contentEndYPixel + 1);
		yStartInt = MathUtils.FloorDouble(yStart);
		yEndInt = VisibleHeight - RimWidth;
		if (yStartInt > RimWidth && yStartInt < yEndInt)
		{
			var blendColor =
				Utils.ColorRGBAInterpolateBGR(OutsideContentRangeColor, BackgroundColor, (float)(yStart - yStartInt));
			for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
			{
				ColorData[yStartInt * Bounds.Width + x] = blendColor;
			}
		}

		if (yStartInt + 1 < yEndInt)
		{
			Array.Copy(ClearDataOutsideContentArea, (yStartInt + 1) * Bounds.Width,
				ColorData, (yStartInt + 1) * Bounds.Width, (yEndInt - (yStartInt + 1)) * Bounds.Width);
		}

		// Draw the editor area.
		// Force this to use unquantized positions. If we don't do this then when using quantized positions the editor
		// area will briefly move up and down as it scrolls downwards. This is because the mini map area is slowly moving up
		// while the editor area is moving down even more slowly. Calculations for position are done relative to the mini map
		// area. So the mini map effectively pulls the editor area up while it is also scrolling down. This causes it to
		// shake up and down while it gradually moves down. Using unquantized positions fixes this, and it looks better to keep
		// the editor area blended as it isn't subject to the same pulsing artifacts that can occur with 1 pixel tall markers.
		var editorStartYPixel = GetYPixelRelativeToBounds(EditorAreaStart, true);
		var editorEndYPixel = GetYPixelRelativeToBounds(EditorAreaEnd, true);
		if (editorEndYPixel < editorStartYPixel + 1.0)
			editorEndYPixel = editorStartYPixel + 1.0;
		var editorClearData = Grabbed ? ClearDataEditorSelectedArea :
			MouseOverEditor ? ClearDataEditorMouseOverArea : ClearDataEditorArea;
		var editorColor = Grabbed ? EditorAreaSelectedColor : MouseOverEditor ? EditorAreaMouseOverColor : EditorAreaColor;
		yStartInt = MathUtils.FloorDouble(editorStartYPixel);
		yEndInt = MathUtils.FloorDouble(editorEndYPixel);
		var yStartForCopyInclusive = yStartInt + 1;
		var yEndForCopyInclusive = yEndInt;
		if (yStartForCopyInclusive < VisibleHeight - RimWidth && yEndForCopyInclusive >= RimWidth
		                                                      && yEndForCopyInclusive >= yStartForCopyInclusive)
		{
			var yStartForCopy = Math.Max(RimWidth, yStartForCopyInclusive);
			var yEndForCopy = Math.Min(yEndForCopyInclusive, VisibleHeight - RimWidth);
			Array.Copy(editorClearData, yStartForCopy * Bounds.Width,
				ColorData, yStartForCopy * Bounds.Width, (yEndForCopy - yStartForCopy) * Bounds.Width);
		}

		if (yStartInt >= RimWidth && yStartInt < VisibleHeight - RimWidth)
		{
			var blendColor = Utils.ColorRGBAInterpolateBGR(editorColor, ColorData[yStartInt * Bounds.Width + RimWidth],
				(float)(editorStartYPixel - yStartInt));
			for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
			{
				ColorData[yStartInt * Bounds.Width + x] = blendColor;
			}
		}

		if (yEndInt >= RimWidth && yEndInt < VisibleHeight - RimWidth)
		{
			var blendColor = Utils.ColorRGBAInterpolateBGR(editorColor, ColorData[yEndInt * Bounds.Width + RimWidth],
				(float)(1.0 - (editorEndYPixel - yEndInt)));
			for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
			{
				ColorData[yEndInt * Bounds.Width + x] = blendColor;
			}
		}

		// Draw content area start and end markers.
		AddHorizontalLine(contentStartYPixel, RimWidth, (uint)(Bounds.Width - (RimWidth << 1)), ContentMarkerColor);
		AddHorizontalLine(contentEndYPixel, RimWidth, (uint)(Bounds.Width - (RimWidth << 1)), ContentMarkerColor);

		AddCursor(CursorPosition);
	}

	/// <summary>
	/// Called at the end of an update loop after all notes are added.
	/// Commits the color data to the texture to render.
	/// </summary>
	public void UpdateEnd()
	{
		if (Bounds.Height <= 0 || Bounds.Width <= 0 || VisibleHeight <= 0)
			return;

		// If we are zoomed out so far that the MiniMap Area can't fit the Editor Areas or scroll
		// then we can't render anything meaningful and we should early-ouy.
		var editorAreaPercentage = GetEditorAreaPercentageOfMiniMapArea();
		if (editorAreaPercentage >= 1.0)
			return;

		// Fade out if configured to do so.
		if (editorAreaPercentage >= FadeOutPercentage)
		{
			var alphaPercentage = Interpolation.Lerp(1.0, 0.0, FadeOutPercentage, 1.0, editorAreaPercentage);
			var alphaByte = (byte)(alphaPercentage * byte.MaxValue);
			var alphaColor = (uint)alphaByte << 24;
			for (var i = 0; i < ColorData.Length; i++)
			{
				ColorData[i] = (ColorData[i] & 0x00FFFFFF) | alphaColor;
			}
		}

		Textures[TextureIndex].SetData(ColorData);
	}

	/// <summary>
	/// Gets the Editor Area as a percentage of the MiniMap Area.
	/// If this is greater than or equal to 1.0 then we can't rendering a meaningful representation
	/// or scroll so we should not render.
	/// </summary>
	/// <returns>Editor Area as a percentage of the MiniMap Area.</returns>
	private double GetEditorAreaPercentageOfMiniMapArea()
	{
		return (EditorAreaEnd - EditorAreaStart) / MiniMapAreaRange;
	}

	/// <summary>
	/// Renders the MiniMap texture.
	/// </summary>
	/// <param name="spriteBatch">SpriteBatch to use for rendering the texture.</param>
	public void Draw(SpriteBatch spriteBatch)
	{
		if (Bounds.Height <= 0 || Bounds.Width <= 0 || VisibleHeight <= 0)
			return;

		// If we are zoomed out so far that the MiniMap Area can't fit the Editor Areas or scroll
		// then we can't render anything meaningful and we should early-ouy.
		if (GetEditorAreaPercentageOfMiniMapArea() >= 1.0)
			return;

		// Draw the current texture.
		spriteBatch.Draw(Textures[TextureIndex], new Vector2(Bounds.X, Bounds.Y), null, Color.White);
		// Advance to the next texture index for the next frame.
		TextureIndex = (TextureIndex + 1) % NumTextures;
	}

	/// <summary>
	/// Given a Y position in Chart space, return the Y position in screen space relative
	/// to the bounds of the MiniMap.
	/// </summary>
	/// <param name="position">Y position in Chart space.</param>
	/// <param name="forceUnquantized">
	/// If true, then even when configured to use quantized positions return an unquantized result.
	/// </param>
	/// <returns>Y position in screen space relative to the bounds of the MiniMap.</returns>
	private double GetYPixelRelativeToBounds(double position, bool forceUnquantized = false)
	{
		return QuantizePositions && !forceUnquantized
			? (long)((position - MiniMapAreaStart) * HeightOverMiniMapAreaRange + 0.5)
			: (position - MiniMapAreaStartUnquantized) * HeightOverMiniMapAreaRange;
	}

	/// <summary>
	/// Given a Y position in Chart space, return the Y position in screen space.
	/// </summary>
	/// <param name="position">Y position in Chart space.</param>
	/// <param name="forceUnquantized">
	/// If true, then even when configured to use quantized positions return an unquantized result.
	/// </param>
	/// <returns>Y position in screen space.</returns>
	private double GetYPixelRelativeToScreen(double position, bool forceUnquantized)
	{
		return GetYPixelRelativeToBounds(position, forceUnquantized) + Bounds.Y;
	}

	/// <summary>
	/// Given a Y position in screen space, return the Y position in Chart space.
	/// </summary>
	/// <param name="screenY">Y position in screen space.</param>
	/// <returns>Y position in Chart space.</returns>
	private double GetPositionRelativeToPixel(double screenY)
	{
		return MiniMapAreaStart + (screenY - Bounds.Y) / VisibleHeight * MiniMapAreaRange;
	}

	/// <summary>
	/// Returns whether or not the screen position represented by the given x and y values
	/// falls within the MiniMap bounds.
	/// </summary>
	/// <param name="screenX">X coordinate in screen space.</param>
	/// <param name="screenY">Y coordinate in screen space.</param>
	/// <returns>Whether or not the screen position is within the MiniMap bounds.</returns>
	public bool IsScreenPositionInMiniMapBounds(int screenX, int screenY)
	{
		return screenX >= Bounds.X && screenX <= Bounds.X + Bounds.Width && screenY >= Bounds.Y &&
		       screenY <= Bounds.Y + VisibleHeight;
	}

	/// <summary>
	/// Returns whether or not the screen position represented by the given x and y values
	/// falls within the editor area bounds of the MiniMap.
	/// </summary>
	/// <param name="screenX">X coordinate in screen space.</param>
	/// <param name="screenY">Y coordinate in screen space.</param>
	/// <returns>Whether or not the screen position is within the editor bounds.</returns>
	private bool IsScreenPositionInEditorBounds(int screenX, int screenY)
	{
		// Force unquantized positions since we are comparing to the editor area which is never quantized.
		return IsScreenPositionInMiniMapBounds(screenX, screenY)
		       && screenX >= Bounds.X
		       && screenX <= Bounds.X + Bounds.Width
		       && screenY >= GetYPixelRelativeToScreen(EditorAreaStart, true)
		       && screenY <= GetYPixelRelativeToScreen(EditorAreaEnd, true);
	}
}
