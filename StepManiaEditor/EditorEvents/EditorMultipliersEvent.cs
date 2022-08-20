using System.Text.RegularExpressions;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	class EditorMultipliersEvent : EditorEvent
	{
		public static readonly string WidgetHelp =
			"Combo Multipliers.\n" +
			"Expected format: \"<hit multiplier>x/<miss multiplier>x\". e.g. \"1x/1x\"\n" +
			"Multipliers must be non-negative integer values.";

		public Multipliers MultipliersEvent;
		private bool WidthDirty;
		public bool CanBeDeleted;

		public string StringValue
		{
			get => GetMultipliersString();
			set
			{
				var (valid, hit, miss) = IsValidMultipliersString(value);
				if (valid)
				{
					if (MultipliersEvent.HitMultiplier != hit || MultipliersEvent.MissMultiplier != miss)
					{
						MultipliersEvent.HitMultiplier = hit;
						MultipliersEvent.MissMultiplier = miss;
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

		public static (bool, int, int) IsValidMultipliersString(string v)
		{
			int hitMultiplier = 1;
			int missMultiplier = 1;

			var match = Regex.Match(v, @"^(\d+)x/(\d+)x$");
			if (!match.Success)
				return (false, hitMultiplier, missMultiplier);
			if (match.Groups.Count != 3)
				return (false, hitMultiplier, missMultiplier);
			if (!int.TryParse(match.Groups[1].Captures[0].Value, out hitMultiplier))
				return (false, hitMultiplier, missMultiplier);
			if (!int.TryParse(match.Groups[2].Captures[0].Value, out missMultiplier))
				return (false, hitMultiplier, missMultiplier);
			return (true, hitMultiplier, missMultiplier);
		}

		public string GetMultipliersString()
		{
			return $"{MultipliersEvent.HitMultiplier}x/{MultipliersEvent.MissMultiplier}x";
		}

		public EditorMultipliersEvent(EditorChart editorChart, Multipliers chartEvent) : base(editorChart, chartEvent)
		{
			MultipliersEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (GetAlpha() <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventMultipliersWidget(
				GetImGuiId(),
				this,
				nameof(StringValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UIMultipliersColorABGR,
				false,
				CanBeDeleted,
				GetAlpha(),
				WidgetHelp);
		}
	}
}
