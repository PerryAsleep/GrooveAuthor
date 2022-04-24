using System;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
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
	/// Configure with SetNumLanes, SetLaneSpacing, and UpdateBounds.
	/// Each frame call UpdateBegin, followed by AddHold/AddNote/AddMine as needed. Finally call UpdateEnd.
	/// Call Draw to render.
	/// For input processing call MouseDown, MouseMove, and MouseUp.
	/// To get the position from the MiniMap call GetEditorPosition.
	/// </summary>
	public class MiniMap
	{
		/// <summary>
		/// Behavior when selecting the MiniMap with the mouse.
		/// </summary>
		public enum SelectMode
		{
			/// <summary>
			/// Move the editor to the cursor, not the area under cursor.
			/// This is the natural option if you consider the MiniMap like a scrollbar.
			/// </summary>
			MoveEditorToCursor,
			/// <summary>
			/// Move the editor to the selected area, not to the cursor.
			/// This is the natural option if you consider the MiniMap like a map.
			/// </summary>
			MoveEditorToSelectedArea,
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
			/// To the right of the Chart area. Will not move with scaling.
			/// </summary>
			RightOfChartArea,
			/// <summary>
			/// Mounted to the right of the WaveForm. Will move with scaling.
			/// </summary>
			MountedToWaveForm,
			/// <summary>
			/// Mounted to the right of the Chart. Will move with scaling.
			/// </summary>
			MountedToChart
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
			BelowBottom
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
		/// ABGR color of the chart background.
		/// </summary>
		private static uint BackgroundColor = 0xFF1E1E1E;
		/// <summary>
		/// ABGR color of the rim border.
		/// </summary>
		private static uint RimColor = 0xFFFFFFFF;
		/// <summary>
		/// ABGR color of the line separating the areas outside of the Chart content from
		/// the area representing the Chart content.
		/// </summary>
		private static uint ContentMarkerColor = 0xFFFFFFFF;
		/// <summary>
		/// ABGR color of the area outside of the Chart content.
		/// </summary>
		private static uint OutsideContentRangeColor = 0xFF020202;
		/// <summary>
		/// ABGR color of the normal editor area.
		/// </summary>
		private static uint EditorAreaColor = 0xFF303030;
		/// <summary>
		/// ABGR color of the editor area when the mouse is over it but it is not being held.
		/// </summary>
		private static uint EditorAreaMouseOverColor = 0xFF373737;
		/// <summary>
		/// ABGR color of the editor area when it is being held.
		/// </summary>
		private static uint EditorAreaSelectedColor = 0xFF3E3E3E;

		/// <summary>
		/// Textures to render to. Array for double buffering.
		/// </summary>
		private Texture2D[] Textures;
		/// <summary>
		/// Index into Textures array to control which texture we write to while the other is being rendered.
		/// </summary>
		private int TextureIndex;
		/// <summary>
		/// ABGR color data to set on the texture after updating each frame.
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
		private uint NoteSpacing = 0;
		/// <summary>
		/// Cached x positions of each lane in pixels relative to Bounds.
		/// </summary>
		private uint[] LaneXPositions;

		/// <summary>
		/// SelectMode for selecting the Editor area.
		/// </summary>
		private SelectMode EditorSelectMode = SelectMode.MoveEditorToCursor;
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
		/// MiniMap area range (height) value. Units are in Chart space (e.g. time or position) and not pixel space.
		/// </summary>
		private double MiniMapAreaRange;
		/// <summary>
		/// Editor area start y value. Units are in Chart space (e.g. time or position) and not pixel space.
		/// </summary>
		private double EditorAreaStart;
		/// <summary>
		/// Editor area end y value. Units are in Chart space (e.g. time or position) and not pixel space.
		/// </summary>
		private double EditorAreaEnd;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="graphicsDevice">GraphicsDevice for rendering.</param>
		/// <param name="bounds">Bounds of the MiniMap in screen space.</param>
		public MiniMap(GraphicsDevice graphicsDevice, Rectangle bounds)
		{
			UpdateBounds(graphicsDevice, bounds);
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
		/// Updates the cached LaneXPositions based on the Bounds, NoteWidth, and NoteSpacing.
		/// All LaneXPositions will always be within the Bounds.
		/// </summary>
		private void UpdateLaneXPositions()
		{
			LaneXPositions = new uint[NumLanes];
			if (NumLanes < 1)
				return;

			var totalWidth = Bounds.Width - (RimWidth << 1);

			var maxNoteSpacing = NumLanes < 2 ? 0 : (int)(totalWidth - (NumLanes * NoteWidth)) / (int)(NumLanes - 1);
			var noteSpacing = Math.Min(maxNoteSpacing, (int)NoteSpacing);

			var totalNoteSpacing = (int)(NoteWidth * NumLanes + noteSpacing * (NumLanes - 1));
			if (totalNoteSpacing > totalWidth)
				totalNoteSpacing = totalWidth;

			var startingLanePosition = RimWidth + ((totalWidth - totalNoteSpacing) >> 1);
			for (var lane = 0; lane < NumLanes; lane++)
			{
				LaneXPositions[lane] = (uint)Math.Max(RimWidth, Math.Min(
					(startingLanePosition + lane * (noteSpacing + NoteWidth)),
					(Bounds.Width - RimWidth - NoteWidth)));
			}
		}

		/// <summary>
		/// Updates the Bounds to the given bounds Rectangle.
		/// </summary>
		/// <param name="graphicsDevice">Graphics device for recreating textures.</param>
		/// <param name="bounds">New bounds in screen space.</param>
		public void UpdateBounds(GraphicsDevice graphicsDevice, Rectangle bounds)
		{
			var dimensionsDirty = bounds.Width != Bounds.Width || bounds.Height != Bounds.Height;

			Bounds = bounds;

			if (!dimensionsDirty || Bounds.Height <= 0 || Bounds.Width <= 0)
				return;

			// Set up the textures.
			Textures = new Texture2D[NumTextures];
			for (var i = 0; i < NumTextures; i++)
			{
				// The documentation for SurfaceFormat.Color claims it is ARGB but it is actually ABGR.
				Textures[i] = new Texture2D(graphicsDevice, bounds.Width, bounds.Height, false, SurfaceFormat.Color);
			}

			// Set up the pixel data.
			ColorData = new uint[bounds.Width * bounds.Height];
			ClearData = new uint[bounds.Width * bounds.Height];
			ClearDataEditorArea = new uint[bounds.Width * bounds.Height];
			ClearDataEditorMouseOverArea = new uint[bounds.Width * bounds.Height];
			ClearDataEditorSelectedArea = new uint[bounds.Width * bounds.Height];
			ClearDataOutsideContentArea = new uint[bounds.Width * bounds.Height];
			for (var x = 0; x < Bounds.Width; x++)
			{
				for (var y = 0; y < Bounds.Height; y++)
				{
					var i = Bounds.Width * y + x;
					if (y < RimWidth || y >= Bounds.Height - RimWidth || x < RimWidth || x >= Bounds.Width - RimWidth)
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

			var editorStartYPixel = GetYPixelRelativeToBounds(EditorAreaStart);
			var editorEndYPixel = GetYPixelRelativeToBounds(EditorAreaEnd);

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
				GrabbedPositionAsPercentageOfEditorArea = 0.5;

				switch (EditorSelectMode)
				{
					// Move the editor to the cursor, not the area under cursor.
					// This is the natural option if you consider the MiniMap like a scrollbar.
					case SelectMode.MoveEditorToCursor:
					{
						Grabbed = true;
						break;
					}

					// Move the editor to the selected area, not to the cursor.
					// This is the natural option if you consider the MiniMap like a map.
					case SelectMode.MoveEditorToSelectedArea:
					{
						if (!Grabbed)
						{
							var selectedPosition = GetPositionRelativeToPixel(screenY);

							// Center the editor area over the selected region.
							var editorAreaRange = EditorAreaEnd - EditorAreaStart;
							EditorAreaStart = selectedPosition - (editorAreaRange * 0.5);
							EditorAreaEnd = EditorAreaStart + editorAreaRange;

							// Update the MiniMap area based on the editor area.
							SetMiniMapAreaFromEditorArea();

							// When moving the editor to the selected area only grab it if it is now under the cursor.
							if (IsScreenPositionInEditorBounds(screenX, screenY))
							{
								Grabbed = true;
								// When grabbing the editor region we need to record where on that region it was grabbed so when
								// the mouse moves the editor region moves naturally.
								editorStartYPixel = GetYPixelRelativeToBounds(EditorAreaStart);
								editorEndYPixel = GetYPixelRelativeToBounds(EditorAreaEnd);
								GrabbedPositionAsPercentageOfEditorArea = (double)(relativePosY - editorStartYPixel) / (editorEndYPixel - editorStartYPixel);
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
			var editorAreaPixelRange = GetYPixelRelativeToBounds(EditorAreaEnd) - GetYPixelRelativeToBounds(EditorAreaStart);
			var editorStartYPixel = selectedPixel - editorAreaPixelRange * GrabbedPositionAsPercentageOfEditorArea;
			var editorStartPositionRange = Bounds.Height - editorAreaPixelRange;
			var percentage = editorStartYPixel / editorStartPositionRange;

			// Put the MiniMap at the appropriate range based on that percentage.
			if (MiniMapAreaRange < FullAreaEnd - FullAreaStart)
			{
				var miniMapStartPositionRange = (FullAreaEnd - FullAreaStart) - MiniMapAreaRange;
				var percentageForMiniMap = Math.Min(1.0, Math.Max(0.0, percentage));
				MiniMapAreaStart = FullAreaStart + (percentageForMiniMap * miniMapStartPositionRange);
			}

			// Put the editor area at the appropriate location based on the MiniMap area.
			var editorRange = EditorAreaEnd - EditorAreaStart;
			var editorStartRange = MiniMapAreaRange - editorRange;
			EditorAreaStart = MiniMapAreaStart + (percentage * editorStartRange);
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
				MiniMapAreaStart = FullAreaStart;
				return;
			}

			// If the MiniMap area is lesser than the full area, then it needs to show only a portion
			// of that full area. The portion shown is based off of the scroll position, which is the
			// editor area's position relative to the full area.
			var percentage = (EditorAreaStart - FullAreaStart) / ((FullAreaEnd - FullAreaStart) - (EditorAreaEnd - EditorAreaStart));
			var miniMapStartPositionRange = (FullAreaEnd - FullAreaStart) - MiniMapAreaRange;
			var percentageForMiniMap = Math.Min(1.0, Math.Max(0.0, percentage));
			MiniMapAreaStart = FullAreaStart + (percentageForMiniMap * miniMapStartPositionRange);
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
		/// Add a note represented by the given LaneNote to the MiniMap.
		/// </summary>
		/// <param name="chartEvent">LaneNote to add.</param>
		/// <param name="position">Position in Chart space. Can be time, row, or variable.</param>
		/// <returns>AddResult describing if the note was added.</returns>
		public AddResult AddNote(LaneNote chartEvent, double position)
		{
			return AddShortNote(
				GetYPixelRelativeToBounds(position),
				LaneXPositions[chartEvent.Lane],
				Utils.GetArrowColorABGR(chartEvent.IntegerPosition));
		}

		/// <summary>
		/// Add a mine represented by the given LaneNote to the MiniMap.
		/// </summary>
		/// <param name="chartEvent">LaneNote to add.</param>
		/// <param name="position">Position in Chart space. Can be time, row, or variable.</param>
		/// <returns>AddResult describing if the note was added.</returns>
		public AddResult AddMine(LaneNote chartEvent, double position)
		{
			return AddShortNote(
				GetYPixelRelativeToBounds(position),
				LaneXPositions[chartEvent.Lane],
				Utils.GetMineColorABGR());
		}

		/// <summary>
		/// Helper method for adding a short, non-hold note.
		/// </summary>
		/// <param name="y">Y position in pixels relative to Bounds.</param>
		/// <param name="x">X position in pixels relative to Bounds.</param>
		/// <param name="color">AGBR color of note to add.</param>
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
		/// <param name="color">AGBR color of line.</param>
		/// <returns>AddResult describing if the line was added.</returns>
		private AddResult AddHorizontalLine(double y, uint x, uint w, uint color)
		{
			var yInt = (int)y;

			if (yInt < RimWidth)
				return AddResult.AboveTop;
			if (yInt >= Bounds.Height - RimWidth)
				return AddResult.BelowBottom;
			if (w == 0)
				return AddResult.InRange;

			var percent = 1.0 - (y - yInt);

			var c1 = Utils.ColorABGRInterpolateBGR(ColorData[yInt * Bounds.Width + x], color, (float)percent);
			for (var i = x; i < x + w; i++)
				ColorData[yInt * Bounds.Width + i] = c1;

			yInt++;
			if (yInt < Bounds.Height - RimWidth)
			{
				var c2 = Utils.ColorABGRInterpolateBGR(ColorData[yInt * Bounds.Width + x], color, (float)(1.0 - percent));
				for (var i = x; i < x + w; i++)
					ColorData[yInt * Bounds.Width + i] = c2;
			}

			return AddResult.InRange;
		}

		/// <summary>
		/// Add a hold or roll note to the MiniMap.
		/// </summary>
		/// <param name="start">LaneHoldStartNote representing the start of the hold.</param>
		/// <param name="startPosition">Start position of the hold in Chart space. Can be time, row, or variable.</param>
		/// <param name="endPosition">End position of the hold in Chart space. Can be time, row, or variable.</param>
		/// <param name="roll">Whether or not the hold is a roll.</param>
		/// <returns>AddResult describing if the hold was added.</returns>
		public AddResult AddHold(LaneHoldStartNote start, double startPosition, double endPosition, bool roll)
		{
			var yStart = GetYPixelRelativeToBounds(startPosition);
			var yEnd = GetYPixelRelativeToBounds(endPosition) + 1.0;

			var x = LaneXPositions[start.Lane];
			var bodyColor = roll ? Utils.GetRollColorABGR() : Utils.GetHoldColorABGR();
			var headColor = Utils.GetArrowColorABGR(start.IntegerPosition);

			var w = (uint)Math.Min(Bounds.Width - (RimWidth << 1), NoteWidth);

			int y = (int)yStart;
			var i = 0;
			while (y < yEnd)
			{
				if (y < RimWidth)
				{
					if (y + 1 >= yEnd)
						return AddResult.AboveTop;

					y++;
					i++;
					continue;
				}

				if (y >= Bounds.Height - RimWidth)
				{
					if (i == 0)
						return AddResult.BelowBottom;
					break;
				}
				
				if (w > 0)
				{
					// Determine the note color by blending the head and body.
					var noteColor = i == 0 ? headColor : bodyColor;
					if (i == 1)
					{
						var spaceToWorkWith = 1.0;
						if (yEnd < y + 1.0)
							spaceToWorkWith = yEnd - y;
						noteColor = Utils.ColorABGRInterpolateBGR(bodyColor, headColor, (float)((yStart - (int)yStart) / spaceToWorkWith));
					}

					// Blend the note color with the background color.
					var color = noteColor;
					if (i == 0)
						color = Utils.ColorABGRInterpolateBGR(noteColor, ColorData[y * Bounds.Width + x], (float)(yStart - (int)yStart));
					else if (y + 1 >= yEnd)
						color = Utils.ColorABGRInterpolateBGR(noteColor, ColorData[y * Bounds.Width + x], (float)(1.0 - (yEnd - y)));

					// Set the color.
					for (var j = x; j < x + w; j++)
						ColorData[y * Bounds.Width + j] = color;
				}

				i++;
				y++;
			}

			return AddResult.InRange;
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
		public void UpdateBegin(
			double fullAreaStart,
			double fullAreaEnd,
			double contentAreaStart,
			double contentAreaEnd,
			double miniMapAreaRange,
			double editorAreaStart,
			double editorAreaEnd)
		{
			FullAreaStart = fullAreaStart;
			FullAreaEnd = fullAreaEnd;
			EditorAreaStart = editorAreaStart;
			EditorAreaEnd = editorAreaEnd;
			MiniMapAreaRange = miniMapAreaRange;

			if (Bounds.Height <= 0 || Bounds.Width <= 0)
				return;

			SetMiniMapAreaFromEditorArea();

			Array.Copy(ClearData, ColorData, Bounds.Width * Bounds.Height);

			// TODO: There is a lot of copy/paste logic around blending colors below. Could be cleaned up.

			// Draw area outside of content region on top.
			var contentStartYPixel = GetYPixelRelativeToBounds(contentAreaStart);
			var yStartInt = RimWidth;
			var yEnd = Math.Min(Bounds.Height - RimWidth, contentStartYPixel);
			var yEndInt = (int)yEnd;
			if (yStartInt < yEndInt)
			{
				Array.Copy(ClearDataOutsideContentArea, yStartInt * Bounds.Width,
					ColorData, yStartInt * Bounds.Width, (yEndInt - yStartInt) * Bounds.Width);
			}
			if (yEndInt >= RimWidth && yEndInt < Bounds.Height - RimWidth)
			{
				var blendColor = Utils.ColorABGRInterpolateBGR(OutsideContentRangeColor, BackgroundColor, (float)(1.0 - (yEnd - yEndInt)));
				for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
				{
					ColorData[yEndInt * Bounds.Width + x] = blendColor;
				}
			}

			// Draw area outside of content region on bottom.
			var contentEndYPixel = GetYPixelRelativeToBounds(contentAreaEnd);
			var yStart = Math.Max(RimWidth, contentEndYPixel + 1);
			yStartInt = (int)yStart;
			yEndInt = Bounds.Height - RimWidth;
			if (yStartInt < yEndInt)
			{
				var blendColor = Utils.ColorABGRInterpolateBGR(OutsideContentRangeColor, BackgroundColor, (float)(yStart - yStartInt));
				for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
				{
					ColorData[yStartInt * Bounds.Width + x] = blendColor;
				}
			}
			if ((yStartInt + 1) < yEndInt)
			{
				Array.Copy(ClearDataOutsideContentArea, (yStartInt + 1) * Bounds.Width,
					ColorData, (yStartInt + 1) * Bounds.Width, (yEndInt - (yStartInt + 1)) * Bounds.Width);
			}

			// Draw the editor area.
			var editorStartYPixel = GetYPixelRelativeToBounds(EditorAreaStart);
			var editorEndYPixel = GetYPixelRelativeToBounds(EditorAreaEnd);
			if (editorEndYPixel < editorStartYPixel + 1.0)
				editorEndYPixel = editorStartYPixel + 1.0;
			var editorClearData = Grabbed ? ClearDataEditorSelectedArea : (MouseOverEditor ? ClearDataEditorMouseOverArea : ClearDataEditorArea);
			var editorColor = Grabbed ? EditorAreaSelectedColor : (MouseOverEditor ? EditorAreaMouseOverColor : EditorAreaColor);
			yStartInt = (int)editorStartYPixel;
			yEndInt = (int)editorEndYPixel;
			var yStartForCopyInclusive = yStartInt + 1;
			var yEndForCopyInclusive = yEndInt;
			if ((yStartForCopyInclusive < Bounds.Height - RimWidth && yEndForCopyInclusive >= RimWidth)
			    && yEndForCopyInclusive >= yStartForCopyInclusive)
			{
				var yStartForCopy = Math.Max(RimWidth, yStartForCopyInclusive);
				var yEndForCopy = Math.Min(yEndForCopyInclusive, Bounds.Height - RimWidth);
				Array.Copy(editorClearData, yStartForCopy * Bounds.Width,
					ColorData, yStartForCopy * Bounds.Width, (yEndForCopy - yStartForCopy) * Bounds.Width);
			}
			if (yStartInt >= RimWidth && yStartInt < Bounds.Height - RimWidth)
			{
				var blendColor = Utils.ColorABGRInterpolateBGR(editorColor, ColorData[yStartInt * Bounds.Width + RimWidth], (float)(editorStartYPixel - yStartInt));
				for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
				{
					ColorData[yStartInt * Bounds.Width + x] = blendColor;
				}
			}
			if (yEndInt >= RimWidth && yEndInt < Bounds.Height - RimWidth)
			{
				var blendColor = Utils.ColorABGRInterpolateBGR(editorColor, ColorData[yEndInt * Bounds.Width + RimWidth], (float)(1.0 - (editorEndYPixel - yEndInt)));
				for (var x = RimWidth; x < Bounds.Width - RimWidth; x++)
				{
					ColorData[yEndInt * Bounds.Width + x] = blendColor;
				}
			}

			// Draw content area start and end markers.
			AddHorizontalLine(contentStartYPixel, RimWidth, (uint)(Bounds.Width - (RimWidth << 1)), ContentMarkerColor);
			AddHorizontalLine(contentEndYPixel, RimWidth, (uint)(Bounds.Width - (RimWidth << 1)), ContentMarkerColor);
		}

		/// <summary>
		/// Called at the end of an update loop after all notes are added.
		/// Commits the color data to the texture to render.
		/// </summary>
		public void UpdateEnd()
		{
			if (Bounds.Height <= 0 || Bounds.Width <= 0)
				return;
			Textures[TextureIndex].SetData(ColorData);
		}

		/// <summary>
		/// Renders the MiniMap texture.
		/// </summary>
		/// <param name="spriteBatch">SpriteBatch to use for rendering the texture.</param>
		public void Draw(SpriteBatch spriteBatch)
		{
			if (Bounds.Height <= 0 || Bounds.Width <= 0)
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
		/// <returns>Y position in screen space relative to the bounds of the MiniMap.</returns>
		private double GetYPixelRelativeToBounds(double position)
		{
			return (position - MiniMapAreaStart) * Bounds.Height / MiniMapAreaRange;
		}

		/// <summary>
		/// Given a Y position in Chart space, return the Y position in screen space.
		/// </summary>
		/// <param name="position">Y position in Chart space.</param>
		/// <returns>Y position in screen space.</returns>
		private double GetYPixelRelativeToScreen(double position)
		{
			return GetYPixelRelativeToBounds(position) + Bounds.Y;
		}

		/// <summary>
		/// Given a Y position in screen space, return the Y position in Chart space.
		/// </summary>
		/// <param name="screenY">Y position in screen space.</param>
		/// <returns>Y position in Chart space.</returns>
		private double GetPositionRelativeToPixel(double screenY)
		{
			return MiniMapAreaStart + ((screenY - Bounds.Y) / Bounds.Height) * MiniMapAreaRange;
		}

		/// <summary>
		/// Returns whether or not the screen position represented by the given x and y values
		/// falls within the MiniMap bounds.
		/// </summary>
		/// <param name="screenX">X coordinate in screen space.</param>
		/// <param name="screenY">Y coordinate in screen space.</param>
		/// <returns>Whether or not the screen position is within the MiniMap bounds.</returns>
		private bool IsScreenPositionInMiniMapBounds(int screenX, int screenY)
		{
			return (screenX >= Bounds.X && screenX <= Bounds.X + Bounds.Width && screenY >= Bounds.Y && screenY <= Bounds.Y + Bounds.Height);
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
			return IsScreenPositionInMiniMapBounds(screenX, screenY)
				&& screenX >= Bounds.X
				&& screenX <= Bounds.X + Bounds.Width
				&& screenY >= GetYPixelRelativeToScreen(EditorAreaStart)
				&& screenY <= GetYPixelRelativeToScreen(EditorAreaEnd);
		}
	}
}
