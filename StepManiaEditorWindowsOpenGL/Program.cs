using System;
using StepManiaEditor;
using StepManiaEditorWindowsOpenGL;

public static class Program
{
	[STAThread]
	private static void Main()
	{
		using var editor = new Editor(new EditorWindowsOpenGLInterface());
		editor.Run();
	}
}
