using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Utils;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class EditorFakeSegmentEvent : EditorEvent, IChartRegion
	{
		public static readonly string EventShortDescription =
			"Notes that occur during a fake region are not counted.";
		public static readonly string WidgetHelp =
			"Fake Region.\n" +
			EventShortDescription + "\n" +
			"Expected format: \"<time>s\". e.g. \"1.0s\"\n" +
			"Fake region lengths are in seconds and must be non-negative.";
		private const string Format = "%.9gs";
		private const float Speed = 0.01f;

		public FakeSegment FakeSegmentEvent;
		private bool WidthDirty;

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
		public double GetRegionPosition() { return ToSeconds(ChartEvent.TimeMicros); }
		public double GetRegionDuration() { return DoubleValue; }
		public bool AreRegionUnitsTime() { return true; }
		public bool IsVisible(SpacingMode mode) { return true; }
		public Color GetRegionColor() { return FakeRegionColor; }
		#endregion IChartRegion Implementation

		public double DoubleValue
		{
			get => ToSeconds(FakeSegmentEvent.LengthMicros);
			set
			{
				var newMicros = ToMicrosRounded(value);
				if (FakeSegmentEvent.LengthMicros != newMicros)
				{
					FakeSegmentEvent.LengthMicros = newMicros;
					WidthDirty = true;
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

		public EditorFakeSegmentEvent(EditorChart editorChart, FakeSegment chartEvent) : base(editorChart, chartEvent)
		{
			FakeSegmentEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (Alpha <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UIFakesColorRGBA,
				false,
				true,
				Speed,
				Format,
				Alpha,
				WidgetHelp,
				0.0);
		}
	}
}
