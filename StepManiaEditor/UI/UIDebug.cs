#if DEBUG

using ImGuiNET;

namespace StepManiaEditor;

internal sealed class UIDebug
{
	public const string WindowTitle = "Debug";

	private readonly Editor Editor;

	public UIDebug(Editor editor)
	{
		Editor = editor;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowDebugWindow)
			return;

		if (ImGui.Begin(WindowTitle, ref p.ShowDebugWindow))
		{
			var renderChart = Editor.DebugGetShouldRenderChart();
			ImGui.Checkbox("Render Chart", ref renderChart);
			Editor.DebugSetShouldRenderChart(renderChart);

			if (ImGui.Button("Splash"))
			{
				Editor.DebugShowSplashSequence();
			}

			if (ImGui.Button("Save Time and Zoom"))
			{
				Editor.DebugSaveTimeAndZoom();
			}

			if (ImGui.Button("Load Time and Zoom"))
			{
				Editor.DebugLoadTimeAndZoom();
			}

			if (Editor.IsVSyncEnabled())
			{
				if (ImGui.Button("Disable VSync"))
				{
					Editor.SetVSyncEnabled(false);
				}
			}
			else
			{
				if (ImGui.Button("Enable VSync"))
				{
					Editor.SetVSyncEnabled(true);
				}
			}
		}

		ImGui.End();
	}
}

#endif
