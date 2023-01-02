using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	internal sealed class EditorTimeSignatureEvent : EditorRateAlteringEvent
	{
		public static readonly string EventShortDescription =
			"StepMania ignores time signatures during gameplay. They are a convenience for visualizing measures in the editor.\n" +
			"StepMania does not color notes based on their beat relative to the current time signature. Rather, it colors\n" +
			"notes based on their absolute row.";
		public static readonly string WidgetHelp =
			"Time Signature.\n" +
			"Expected format: \"<beat unit>/<number of beats>\". e.g. \"4/4\"\n" +
			"Both values must be positive.\n" +
			EventShortDescription;

		public TimeSignature TimeSignatureEvent;
		private bool WidthDirty;

		public string StringValue
		{
			get => TimeSignatureEvent.Signature.ToString();
			set
			{
				var (valid, newSignature) = IsValidTimeSignatureString(value);
				if (valid)
				{
					if (!TimeSignatureEvent.Signature.Equals(newSignature))
					{
						TimeSignatureEvent.Signature = newSignature;
						WidthDirty = true;
						EditorChart.OnRateAlteringEventModified(this);
					}
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
					_W = ImGuiLayoutUtils.GetMiscEditorEventStringWidth(StringValue);
					WidthDirty = false;
				}
				return _W;
			}
			set
			{
				_W = value;
			}
		}

		public static (bool, Fraction) IsValidTimeSignatureString(string v)
		{
			var f = Fraction.FromString(v);
			if (f == null)
				return (false, f);
			if (f.Denominator <= 0 || f.Denominator > SMCommon.MaxValidDenominator)
				return (false, f);
			if (f.Numerator <= 0)
				return (false, f);
			if ((f.Denominator & (f.Denominator - 1)) != 0)
				return (false, f);
			return (true, f);
		}

		public EditorTimeSignatureEvent(EventConfig config, TimeSignature chartEvent) : base(config)
		{
			TimeSignatureEvent = chartEvent;
			WidthDirty = true;
		}

		public override bool IsMiscEvent() { return true; }
		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return true; }

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (Alpha <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventTimeSignatureWidget(
				GetImGuiId(),
				this,
				nameof(StringValue),
				(int)X, (int)Y, (int)W,
				Utils.UITimeSignatureColorRGBA,
				IsSelected(),
				CanBeDeleted,
				Alpha,
				WidgetHelp);
		}
	}
}
