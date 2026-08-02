using System;
using StepManiaEditor;
using StepManiaEditorLinux;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]

public static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		using var editor = new Editor(args, new EditorLinuxInterface());
		editor.Run();
	}
}
