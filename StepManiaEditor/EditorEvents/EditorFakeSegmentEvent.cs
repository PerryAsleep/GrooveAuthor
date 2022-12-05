using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Utils;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class EditorFakeSegmentEvent : EditorEvent, IRegion
	{
		public static readonly string WidgetHelp =
			"Fake region.\n" +
			"Notes that occur during a fake region are not counted.\n" +
			"Expected format: \"<time>s\". e.g. \"1.0s\"\n" +
			"Fake region lengths are in seconds and must be non-negative.";
		private const string Format = "%.9gs";
		private const float Speed = 1.0f;

		public FakeSegment FakeSegmentEvent;
		private bool WidthDirty;

		#region IRegion Implementation
		public double RegionX { get; set; }
		public double RegionY { get; set; }
		public double RegionW { get; set; }
		public double RegionH { get; set; }
		public double GetRegionPosition() { return ToSeconds(ChartEvent.TimeMicros); }
		public double GetRegionDuration() { return DoubleValue; }
		public bool AreRegionUnitsTime() { return true; }
		public bool IsVisible(SpacingMode mode) { return true; }
		public Color GetRegionColor() { return FakeRegionColor; }
		#endregion IRegion Implementation

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
		public override double GetW()
		{
			if (WidthDirty)
			{
				SetW(ImGuiLayoutUtils.GetMiscEditorEventDragDoubleWidgetWidth(DoubleValue, Format));
				WidthDirty = false;
			}
			return base.GetW();
		}

		public EditorFakeSegmentEvent(EditorChart editorChart, FakeSegment chartEvent) : base(editorChart, chartEvent)
		{
			FakeSegmentEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (GetAlpha() <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UIFakesColorRGBA,
				false,
				true,
				Speed,
				Format,
				GetAlpha(),
				WidgetHelp,
				0.0);
		}
	}
}
