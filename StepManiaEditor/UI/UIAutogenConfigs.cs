using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing lists of EditorConfigs.
/// </summary>
internal sealed class UIAutogenConfigs : UIWindow
{
	private static readonly int DefaultWidth = UiScaled(780);

	private UIExpressedChartConfigTable ExpressedChartConfigTable;
	private UIPerformedChartConfigTable PerformedChartConfigTable;
	private UIPatternConfigTable PatternConfigTable;

	public static UIAutogenConfigs Instance { get; } = new();

	private UIAutogenConfigs() : base("Autogen Configs")
	{
	}

	public void Init(Editor editor)
	{
		ExpressedChartConfigTable = new UIExpressedChartConfigTable(editor);
		PerformedChartConfigTable = new UIPerformedChartConfigTable(editor);
		PatternConfigTable = new UIPatternConfigTable(editor);
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowAutogenConfigsWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowAutogenConfigsWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowAutogenConfigsWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowAutogenConfigsWindow, DefaultWidth))
		{
			PerformedChartConfigTable.Draw();
			ImGui.Separator();
			PatternConfigTable.Draw();
			ImGui.Separator();
			ExpressedChartConfigTable.Draw();
		}

		ImGui.End();
	}
}
