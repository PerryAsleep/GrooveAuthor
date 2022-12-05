using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Utils;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class EditorStopEvent : EditorRateAlteringEvent, IRegion
	{
		public static readonly string WidgetHelp =
			"Stop.\n" +
			"Stops pause the chart playback and occur after notes at the same position.\n" +
			"Expected format: \"<time>s\". e.g. \"1.0s\"\n" +
			"Stop and delay lengths are in seconds.\n" +
			"Negative stop values result in the chart immediately advancing forward in time during gameplay.\n" +
			"The recommended method for accomplishing this effect is to use a warp.";
		private const string Format = "%.9gs";
		private const float Speed = 0.01f;

		public Stop StopEvent;
		private bool WidthDirty;

		#region IRegion Implementation
		public double RegionX { get; set; }
		public double RegionY { get; set; }
		public double RegionW { get; set; }
		public double RegionH { get; set; }
		public double GetRegionPosition() { return ToSeconds(ChartEvent.TimeMicros); }
		public double GetRegionDuration() { return DoubleValue; }
		public bool AreRegionUnitsTime() { return true; }
		public bool IsVisible(SpacingMode mode) { return mode == SpacingMode.ConstantTime; }
		public Color GetRegionColor() { return StopRegionColor; }
		#endregion IRegion Implementation

		public double DoubleValue
		{
			get => ToSeconds(StopEvent.LengthMicros);
			set
			{
				var newMicros = ToMicrosRounded(value);
				if (StopEvent.LengthMicros != newMicros)
				{
					StopEvent.LengthMicros = newMicros;
					WidthDirty = true;
					EditorChart.OnRateAlteringEventModified(this);
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

		public EditorStopEvent(EditorChart editorChart, Stop chartEvent) : base(editorChart, chartEvent)
		{
			StopEvent = chartEvent;
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
				Utils.UIStopColorRGBA,
				false,
				CanBeDeleted,
				Speed,
				Format,
				GetAlpha(),
				WidgetHelp);
		}
	}
}
