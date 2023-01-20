using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	internal sealed class EditorWarpEvent : EditorRateAlteringEvent, IChartRegion
	{
		public static readonly string EventShortDescription =
			"A warp will instantly advance the chart forward by the specified number of rows.\n" +
			"This is the preferred method of achieving this effect rather than using negative\n" +
			"stops or tempos. Warp durations are specified in rows where one beat in StepMania is\n" +
			$"{SMCommon.MaxValidDenominator} rows.";
		public static readonly string WidgetHelp =
			"Warp.\n" +
			"Expected format: \"<length>rows\". e.g. \"48rows\"\n" +
			"Length must be non-negative.\n" +
			EventShortDescription;
		private const string Format = "%irows";
		private const float Speed = 1.0f;

		public Warp WarpEvent;
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
		public double GetRegionPosition() { return GetRow(); }
		public double GetRegionDuration() { return IntValue; }
		public bool AreRegionUnitsTime() { return false; }
		public bool IsVisible(SpacingMode mode) { return mode != SpacingMode.ConstantTime; }
		public Color GetRegionColor() { return IRegion.GetColor(WarpRegionColor, Alpha); }
		#endregion IChartRegion Implementation

		public int IntValue
		{
			get => WarpEvent.LengthIntegerPosition;
			set
			{
				if (WarpEvent.LengthIntegerPosition != value && value >= 0)
				{
					WarpEvent.LengthIntegerPosition = value;
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
		private double _W;
		public override double W
		{
			get
			{
				if (WidthDirty)
				{
					_W = ImGuiLayoutUtils.GetMiscEditorEventDragIntWidgetWidth(IntValue, Format);
					WidthDirty = false;
				}
				return _W;
			}
			set
			{
				_W = value;
			}
		}

		public EditorWarpEvent(EventConfig config, Warp chartEvent) : base(config)
		{
			WarpEvent = chartEvent;
			WidthDirty = true;
		}

		public override bool IsMiscEvent() { return true; }
		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return true; }

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (Alpha <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventDragIntWidget(
				GetImGuiId(),
				this,
				nameof(IntValue),
				(int)X, (int)Y, (int)W,
				Utils.UIWarpColorRGBA,
				IsSelected(),
				CanBeDeleted(),
				Speed,
				Format,
				Alpha,
				WidgetHelp,
				0);
		}
	}
}
