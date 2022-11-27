using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorLabelEvent : EditorEvent
	{
		public static readonly string WidgetHelp =
			"Label.\n" +
			"Arbitrary text used to label sections of the chart.\n" +
			"Labels are not visible during gameplay.";

		public Label LabelEvent;
		private bool WidthDirty;

		public string StringValue
		{
			get => LabelEvent.Text;
			set
			{
				var (valid, newText) = IsValidLabelString(value);
				if (valid)
				{
					if (!LabelEvent.Text.Equals(newText))
					{
						LabelEvent.Text = newText;
						WidthDirty = true;
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

		public static (bool, string) IsValidLabelString(string v)
		{
			// Accept all input but sanitize the text to change characters which would interfere with MSD file parsing.
			// StepMania replaces these characters with the underscore character.
			v = v.Replace('\r', '_');
			v = v.Replace('\n', '_');
			v = v.Replace('\t', '_');
			v = v.Replace(MSDFile.ValueStartMarker, '_');
			v = v.Replace(MSDFile.ValueEndMarker, '_');
			v = v.Replace(MSDFile.ParamMarker, '_');
			v = v.Replace(MSDFile.EscapeMarker, '_');
			v = v.Replace(MSDFile.CommentChar, '_');
			v = v.Replace(',', '_');
			return (true, v);
		}

		public EditorLabelEvent(EditorChart editorChart, Label chartEvent) : base(editorChart, chartEvent)
		{
			LabelEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (GetAlpha() <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventLabelWidget(
				GetImGuiId(),
				this,
				nameof(StringValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UILabelColorRGBA,
				false,
				true,
				GetAlpha(),
				WidgetHelp);
		}
	}
}
