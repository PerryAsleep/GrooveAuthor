#if DEBUG
using ImGuiNET;

namespace StepManiaEditor;

internal sealed class UIDebug : UIWindow
{
	private Editor Editor;

	public static UIDebug Instance { get; } = new();

	private UIDebug() : base("Debug")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowDebugWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowDebugWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowDebugWindow)
			return;

		if (ImGui.Begin(WindowTitle, ref p.ShowDebugWindow))
		{
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
