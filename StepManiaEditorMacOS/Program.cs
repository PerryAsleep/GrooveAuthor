using System;
using StepManiaEditor;
using StepManiaEditorMacOs;

public static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		// For attaching debugger.
		//Thread.Sleep(10 * 1000);
		using var editor = new Editor(args, new EditorMacOsInterface());
		editor.Run();
	}
}
