using System;
using StepManiaEditor;
using StepManiaEditorMacOs;

public static class Program
{
	[STAThread]
	private static void Main()
	{
		using var editor = new Editor(new EditorMacOsInterface());
		editor.Run();
	}
}
