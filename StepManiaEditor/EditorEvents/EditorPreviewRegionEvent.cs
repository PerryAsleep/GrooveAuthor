using Microsoft.Xna.Framework;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for rendering the song/chart preview.
	/// The preview is rendered as an IRegion and also as a miscellaneous editor event widget.
	/// The preview does not correspond to an EditorEvent in a chart.
	/// </summary>
	internal sealed class EditorPreviewRegionEvent : IChartRegion, IPlaceable
	{
		/// <summary>
		/// The EditorSong.
		/// </summary>
		private EditorSong EditorSong;
		/// <summary>
		/// The active EditorChart. The EditorChart affects the music offset, which affects the preview time.
		/// </summary>
		public EditorChart ActiveChart;

		public static readonly string WidgetHelp =
			"Preview.\n" +
			"Expected format: \"<length>s\". e.g. \"1.0s\"\n" +
			"Time must be non-negative.\n" +
			"The Preview time and length can also be set in the Song Properties window.";
		private const string Format = "%.9gs";
		private const float Speed = 0.01f;

		public bool ShouldDrawMiscEvent;
		public float Alpha = 1.0f;

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
		public double GetRegionPosition() { return EditorSong.SampleStart + (ActiveChart?.GetMusicOffset() ?? 0.0); }
		public double GetRegionDuration() { return EditorSong.SampleLength; }
		public bool AreRegionUnitsTime() { return true; }
		public bool IsVisible(SpacingMode mode) { return true; }
		public Color GetRegionColor() { return PreviewRegionColor; }
		#endregion IChartRegion Implementation

		#region IPlaceable
		public double X { get; set; }
		public double Y { get; set; }
		public double H { get; set; }

		/// <remarks>
		/// This lazily updates the width if it is dirty.
		/// This is a bit of hack because in order to determine the width we need to call into
		/// ImGui but that is not a thread-safe operation. If we were to set the width when
		/// loading the chart for example, this could crash. By lazily setting it we avoid this
		/// problem as long as we assume the caller of GetW() happens on the main thread.
		/// </remarks>
		private double _W;
		public double W
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
		#endregion IPlaceable

		private bool WidthDirty;

		public double DoubleValue
		{
			get => EditorSong.SampleLength;
			set
			{
				if (!EditorSong.SampleLength.DoubleEquals(value) && value >= 0.0)
				{
					EditorSong.SampleLength = value;
					WidthDirty = true;
				}
			}
		}

		public EditorPreviewRegionEvent(EditorSong editorSong)
		{
			EditorSong = editorSong;
			WidthDirty = true;
		}

		public void Draw()
		{
			if (!ShouldDrawMiscEvent)
				return;
			ImGuiLayoutUtils.MiscEditorEventPreviewDragDoubleWidget(
				"PreviewWidget",
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UIPreviewColorRGBA,
				false,
				Speed,
				Format,
				Alpha,
				WidgetHelp,
				0.0);
		}
	}
}
