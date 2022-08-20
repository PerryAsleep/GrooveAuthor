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
		"itg-hold-solo-body-inactive",
		"itg-hold-solo-end-inactive",
		"itg-hold-center-body-inactive",
		"itg-hold-center-end-inactive",
		"piu-diagonal-receptor",
		"piu-center-receptor",
		"piu-hold-blue",
		"itg-roll-body-inactive",
		"itg-roll-end-inactive",
		"itg-roll-solo-body-inactive",
		"itg-roll-solo-end-inactive",
		"itg-roll-center-body-inactive",
		"itg-roll-center-end-inactive",
		"piu-diagonal-receptor-held",
		"piu-center-receptor-held",
		"piu-roll-blue",
		"itg-hold-body-active",
		"itg-hold-end-active",
		"itg-hold-solo-body-active",
		"itg-hold-solo-end-active",
		"itg-hold-center-body-active",
		"itg-hold-center-end-active",
		"piu-diagonal-receptor-glow",
		"piu-center-receptor-glow",
		"piu-hold-red",
		"itg-roll-body-active",
		"itg-roll-end-active",
		"itg-roll-solo-body-active",
		"itg-roll-solo-end-active",
		"itg-roll-center-body-active",
		"itg-roll-center-end-active",
		"piu-roll-center",
		"piu-hold-center",
		"piu-roll-red",
		"mine",
		"itg-hold-solo-start-active",
		"itg-hold-solo-start-inactive",
		"itg-roll-solo-start-active",
		"itg-roll-solo-start-inactive",
	};

	const int numRows = 9;
	const int numCols = 9;
	const int individualWidth = 128;
	const int individualHeight = 128;
	const string svgAsset = "arrows.svg";
	const string pngAsset = "arrows.png";

	var i = 0;
	foreach(var fileName in fileNames)
	{
		var x = individualWidth * (i % numCols);
		var y = individualWidth * (i / numCols);

		try
		{
			ProcessStartInfo startInfo = new ProcessStartInfo();
			startInfo.FileName = "magick.exe";
			startInfo.CreateNoWindow = true;
			startInfo.WindowStyle = ProcessWindowStyle.Hidden;
			startInfo.Arguments = $"{pngAsset} -crop {individualWidth}x{individualHeight}+{x}+{y} +repage PNG32:{fileNames[i]}.png";

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
