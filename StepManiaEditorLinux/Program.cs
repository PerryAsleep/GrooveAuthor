using System;
using StepManiaEditor;
using StepManiaEditorLinux;

public static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		using var editor = new Editor(args, new EditorLinuxInterface());
		editor.Run();
	}
}
