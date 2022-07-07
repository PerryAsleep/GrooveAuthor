using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorTimeSignatureEvent : EditorRateAlteringEvent
	{
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
						WidthDirty = false;
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
		public override double GetW()
		{
			if (WidthDirty)
			{
				SetW(ImGuiLayoutUtils.GetMiscEditorEventStringWidth(StringValue));
				WidthDirty = false;
			}
			return base.GetW();
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
			return (true, f);
		}

		public EditorTimeSignatureEvent(EditorChart editorChart, TimeSignature chartEvent) : base(editorChart, chartEvent)
		{
			TimeSignatureEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventTimeSignatureWidget(
				GetImGuiId(),
				this,
				nameof(StringValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UITimeSignatureColorABGR,
				false,
				CanBeDeleted);
		}
	}
}
