using System;
using System.Runtime.InteropServices;
using StepManiaEditor;
using StepManiaEditorWindowsOpenGL;

public static class Program
{
	[DllImport("user32.dll")]
	private static extern bool SetProcessDPIAware();

	[STAThread]
	private static void Main()
	{
		// At runtime set this process to be DPI aware. This is needed for
		// us to ignore DPI scaling, which we want to do to avoid Windows
		// performing bitmap scaling on the window and making it blurry. The
		// application will handle scaling fonts and UI values based on the
		// monitor's DPI scaling value.
		SetProcessDPIAware();

		using var editor = new Editor(new EditorWindowsOpenGLInterface());
		editor.Run();
	}
}
