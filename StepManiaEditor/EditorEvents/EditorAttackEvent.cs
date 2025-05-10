using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditor;

internal sealed class EditorAttackEvent : EditorEvent
{
	public static readonly string EventShortDescription =
		"Modifiers to apply during gameplay.";

	public static readonly string WidgetHelp =
		"Attack.\n" +
		EventShortDescription;

	private readonly Attack AttackEvent;
	private bool WidthDirty;

	/// <remarks>
	/// This lazily updates the width if it is dirty.
	/// This is a bit of hack because in order to determine the width we need to call into
	/// ImGui but that is not a thread-safe operation. If we were to set the width when
	/// loading the chart for example, this could crash. By lazily setting it we avoid this
	/// problem as long as we assume the caller of GetW() happens on the main thread.
	/// </remarks>
	private double WidthInternal;

	public override double W
	{
		get
		{
			if (WidthDirty)
			{
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventStringWidth(GetMiscEventText());
				WidthDirty = false;
			}

			return WidthInternal;
		}
		set => WidthInternal = value;
	}

	public override double H
	{
		get => ImGuiLayoutUtils.GetMiscEditorEventHeight();
		set { }
	}

	public static (bool, string) IsValidModString(string v)
	{
		// Accept all input but sanitize the text to change characters which would interfere with MSD file parsing.
		// Match Label logic of replacing these characters with the underscore character.
		v = v.Replace(' ', '_');
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

	public EditorAttackEvent(EventConfig config, Attack chartEvent) : base(config)
	{
		AttackEvent = chartEvent;
		WidthDirty = true;
	}

	public string GetMiscEventText()
	{
		if (AttackEvent.Modifiers.Count == 0)
			return "No Mods";
		if (AttackEvent.Modifiers.Count == 1)
			return SMCommon.GetModString(AttackEvent.Modifiers[0], false, true);
		return "Multiple Mods";
	}

	public override string GetShortTypeName()
	{
		return "Attack";
	}

	public override bool IsMiscEvent()
	{
		return true;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return false;
	}

	public override bool IsSelectableWithModifiers()
	{
		return true;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		if (Alpha <= 0.0f)
			return;
		ImGuiLayoutUtils.MiscEditorEventAttackWidget(
			GetImGuiId(),
			this,
			(int)X, (int)Y, (int)W,
			Utils.UIAttackColorRGBA,
			IsSelected(),
			Alpha,
			WidgetHelp,
			() => { EditorChart.OnAttackEventRequestEdit(this); });
	}
}
