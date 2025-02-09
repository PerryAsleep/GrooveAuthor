using System;
using StepManiaEditor;
using StepManiaEditorWindows;

public static class Program
{
	[STAThread]
	private static void Main()
	{
		using var editor = new Editor(new EditorWindowsInterface());
		editor.Run();
	}
}
