using System;
using StepManiaEditor;
using StepManiaEditorLinux;

public static class Program
{
	[STAThread]
	private static void Main()
	{
		using var editor = new Editor(new EditorLinuxInterface());
		editor.Run();
	}
}
