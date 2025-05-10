using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor.UI;

/// <summary>
/// Class for drawing information about an EditorAttackEvent in a chart.
/// </summary>
internal sealed class UIAttackEvent : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(180);
	private static readonly int DefaultWidth = UiScaled(460);

	private Editor Editor;

	public static UIAttackEvent Instance { get; } = new();

	private UIAttackEvent() : base("Attack Event Properties")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowAttackEventWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowAttackEventWindow = false;
	}

	public void Draw(EditorAttackEvent attackEvent)
	{
		if (attackEvent == null)
		{
			Preferences.Instance.ShowAttackEventWindow = false;
		}

		if (!Preferences.Instance.ShowAttackEventWindow)
			return;

		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowAttackEventWindow, DefaultWidth))
		{
			var disabled = !Editor.CanEdit();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("AttackEventTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.EndTable();
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}
}
