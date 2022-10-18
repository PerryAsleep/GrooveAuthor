using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Script to split the arrows.png file into individual pngs.
/// Assumes Image Magick is installed and in the user's path.
/// </summary>
public static void Main()
{
	var fileNames = new string[]
	{
		"itg-down-1-4",
		"itg-solo-1-4",
		"itg-center-1-4",
		"itg-down-1-8",
		"itg-solo-1-8",
		"itg-center-1-8",
		"itg-down-1-32",
		"itg-solo-1-32",
		"itg-center-1-32",
		"itg-down-receptor",
		"itg-solo-receptor",
		"itg-center-receptor",
		"itg-down-1-16",
		"itg-solo-1-16",
		"itg-center-1-16",
		"itg-down-1-48",
		"itg-solo-1-48",
		"itg-center-1-48",
		"itg-down-receptor-held",
		"itg-solo-receptor-held",
		"itg-center-receptor-held",
		"itg-down-1-12",
		"itg-solo-1-12",
		"itg-center-1-12",
		"itg-down-1-64",
		"itg-solo-1-64",
		"itg-center-1-64",
		"itg-down-receptor-glow",
		"itg-solo-receptor-glow",
		"itg-center-receptor-glow",
		"itg-down-1-24",
		"itg-solo-1-24",
		"itg-center-1-24",
		"piu-diagonal-red",
		"piu-center",
		"piu-diagonal-blue",
		"itg-hold-body-inactive",
		"itg-hold-end-inactive",
		"itg-hold-center-body-inactive",
		"itg-hold-center-end-inactive",
		"itg-hold-solo-body-inactive",
		"itg-hold-solo-end-inactive",
		"piu-diagonal-receptor",
		"piu-center-receptor",
		"piu-hold-blue",
		"itg-roll-body-inactive",
		"itg-roll-end-inactive",
		"itg-roll-center-body-inactive",
		"itg-roll-center-end-inactive",
		"itg-roll-solo-body-inactive",
		"itg-roll-solo-end-inactive",
		"piu-diagonal-receptor-held",
		"piu-center-receptor-held",
		"piu-roll-blue",
		"itg-hold-body-active",
		"itg-hold-end-active",
		"itg-hold-center-body-active",
		"itg-hold-center-end-active",
		"itg-hold-solo-body-active",
		"itg-hold-solo-end-active",
		"piu-diagonal-receptor-glow",
		"piu-center-receptor-glow",
		"piu-hold-red",
		"itg-roll-body-active",
		"itg-roll-end-active",
		"itg-roll-center-body-active",
		"itg-roll-center-end-active",
		"itg-roll-solo-body-active",
		"itg-roll-solo-end-active",
		"piu-roll-center",
		"piu-hold-center",
		"piu-roll-red",
		"mine",
		"itg-hold-solo-start-active",
		"itg-hold-solo-start-inactive",
		"itg-roll-solo-start-active",
		"itg-roll-solo-start-inactive",
	};
	ProcessFiles(fileNames, 9, 0, 0, 128, 128, 0);

	var snapFileNames = new string[]
	{
		"snap-1-4",
		"snap-1-8",
		"snap-1-24",
		"snap-1-32",
		"snap-1-16",
		"snap-1-12",
		"snap-1-48",
		"snap-1-64",
	};
	ProcessFiles(snapFileNames, 4, 648, 1032, 40, 40, 8);
}

static void ProcessFiles(string[] fileNames, int numCols, int startX, int startY, int w, int h, int padding)
{
	const string pngAsset = "arrows.png";

	var i = 0;
	foreach(var fileName in fileNames)
	{
		var x = startX + (w + padding) * (i % numCols);
		var y = startY + (h + padding) * (i / numCols);

		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo();
			startInfo.FileName = "magick.exe";
			startInfo.CreateNoWindow = true;
			startInfo.WindowStyle = ProcessWindowStyle.Hidden;
			startInfo.Arguments = $"{pngAsset} -crop {w}x{h}+{x}+{y} +repage PNG32:{fileNames[i]}.png";

			using (Process exeProcess = Process.Start(startInfo))
			{
				exeProcess.WaitForExit();
			}
		}
		catch
		{
			// Nop
		}

		i++;
	}
}

Main();
