using Microsoft.Xna.Framework;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;
using static Fumen.FumenExtensions;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for rendering the song/chart preview.
	/// The preview is rendered as an IRegion and also as a miscellaneous editor event widget.
	/// The preview does not correspond to an Event in a Chart.
	/// </summary>
	internal sealed class EditorPreviewRegionEvent : EditorEvent, IChartRegion
	{
		public static readonly string WidgetHelp =
			"Preview.\n" +
			"Expected format: \"<length>s\". e.g. \"1.0s\"\n" +
			"Time must be non-negative.\n" +
			"The Preview time and length can also be set in the Song Properties window.";
		private const string Format = "%.9gs";
		private const float Speed = 0.01f;

		#region IChartRegion Implementation
		private double RegionX, RegionY, RegionW, RegionH;
		public double GetRegionX() { return RegionX; }
		public double GetRegionY() { return RegionY; }
		public double GetRegionW() { return RegionW; }
		public double GetRegionH() { return RegionH; }
		public void SetRegionX(double x) { RegionX = x; }
		public void SetRegionY(double y) { RegionY = y; }
		public void SetRegionW(double w) { RegionW = w; }
		public void SetRegionH(double h) { RegionH = h; }
		public double GetRegionPosition() { return GetChartTime(); }
		public double GetRegionDuration() { return EditorChart.EditorSong.SampleLength; }
		public bool AreRegionUnitsTime() { return true; }
		public bool IsVisible(SpacingMode mode) { return true; }
		public Color GetRegionColor() { return IRegion.GetColor(PreviewRegionColor, Alpha); }
		#endregion IChartRegion Implementation

		/// <remarks>
		/// This lazily updates the width if it is dirty.
		/// This is a bit of hack because in order to determine the width we need to call into
		/// ImGui but that is not a thread-safe operation. If we were to set the width when
		/// loading the chart for example, this could crash. By lazily setting it we avoid this
		/// problem as long as we assume the caller of GetW() happens on the main thread.
		/// </remarks>
		private double _W;
		public override double W
		{
			get
			{
				if (WidthDirty)
				{
					_W = ImGuiLayoutUtils.GetMiscEditorEventDragDoubleWidgetWidth(DoubleValue, Format);
					WidthDirty = false;
				}
				return _W;
			}
			set
			{
				_W = value;
			}
		}

		private bool WidthDirty;

		public double DoubleValue
		{
			get => EditorChart.EditorSong.SampleLength;
			set
			{
				if (!EditorChart.EditorSong.SampleLength.DoubleEquals(value) && value >= 0.0)
				{
					EditorChart.EditorSong.SampleLength = value;
					WidthDirty = true;
				}
			}
		}

		public EditorPreviewRegionEvent(EditorChart editorChart, double chartPosition)
			: base(EventConfig.CreateConfigNoEvent(editorChart, chartPosition))
		{
			WidthDirty = true;
			IsPositionImmutable = true;
		}

		public override double GetChartTime()
		{
			return EditorChart.EditorSong.SampleStart + EditorChart.GetMusicOffset();
		}

		public override bool IsMiscEvent() { return true; }
		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return false; }

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			ImGuiLayoutUtils.MiscEditorEventPreviewDragDoubleWidget(
				"PreviewWidget",
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UIPreviewColorRGBA,
				IsSelected(),
				Speed,
				Format,
				Alpha,
				WidgetHelp,
				0.0);
		}
	}
}
