using System.Collections.Generic;
using ImGuiNET;

namespace StepManiaEditor;

/// <summary>
/// Abstract base class for UI windows that can open/close, and be docked.
/// </summary>
internal abstract class UIWindow
{
	protected readonly string WindowTitle;

	private static readonly HashSet<UIWindow> Windows = [];

	protected UIWindow(string windowTitle)
	{
		Windows.Add(this);
		WindowTitle = windowTitle;
	}

	public abstract void Open(bool focus);
	public abstract void Close();

	public virtual void Focus()
	{
		ImGui.SetWindowFocus(WindowTitle);
	}

	public virtual void DockIntoNode(uint dockNodeId)
	{
		ImGui.DockBuilderDockWindow(WindowTitle, dockNodeId);
	}

	public static void CloseAllWindows()
	{
		foreach (var window in Windows)
		{
			window.Close();
		}
	}
}
