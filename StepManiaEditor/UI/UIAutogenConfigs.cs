using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing lists of EditorConfigs.
/// </summary>
internal sealed class UIAutogenConfigs
{
	public const string WindowTitle = "Autogen Configs";

	private static readonly int DefaultWidth = UiScaled(780);

	private readonly UIExpressedChartConfigTable ExpressedChartConfigTable;
	private readonly UIPerformedChartConfigTable PerformedChartConfigTable;
	private readonly UIPatternConfigTable PatternConfigTable;

	public UIAutogenConfigs(Editor editor)
	{
		ExpressedChartConfigTable = new UIExpressedChartConfigTable(editor);
		PerformedChartConfigTable = new UIPerformedChartConfigTable(editor);
		PatternConfigTable = new UIPatternConfigTable(editor);
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
