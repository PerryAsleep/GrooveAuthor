using System;
using AppKit;
using StepManiaEditor;
using StepManiaEditorMacOs;

public static class Program
{
	[STAThread]
	private static void Main()
	{
		// For attaching debugger.
		//Thread.Sleep(10 * 1000);

		NSApplication.Init();
		using var editor = new Editor(new EditorMacOsInterface());
		editor.Run();
	}
}
