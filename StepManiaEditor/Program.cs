using System;

namespace StepManiaEditor;

public static class Program
{
	[STAThread]
	private static void Main()
	{
		using var game = new Editor();
		game.Run();
	}
}
