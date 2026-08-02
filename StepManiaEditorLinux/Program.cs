using System;
using System.Runtime.InteropServices;
using StepManiaEditor;
using StepManiaEditorLinux;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]

public static class Program
{
	[DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr setlocale(int category, string locale);

	[STAThread]
	private static void Main(string[] args)
	{
		setlocale(1, "C");
		using var editor = new Editor(args, new EditorLinuxInterface());
		editor.Run();
	}
}
