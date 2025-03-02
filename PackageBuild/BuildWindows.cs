using System.Diagnostics;

namespace PackageBuild;

/// <summary>
/// Build for Windows.
/// </summary>
internal sealed class BuildWindows : Build
{
	public BuildWindows() : base(
		"win-x64",
		"StepManiaEditorWindows",
		"net8.0-windows7.0",
		"GrooveAuthor.exe")
	{
	}

	protected override bool Package()
	{
		// Copy product to temp directory with desired folder name for packaging.
		var tempDirectory = Path.Combine(Path.GetTempPath(), AppName);
		Console.WriteLine($"Creating temporary directory: {tempDirectory}");
		if (Directory.Exists(tempDirectory))
			Directory.Delete(tempDirectory, true);
		Directory.CreateDirectory(tempDirectory);
		Console.WriteLine("Copying product to temporary directory.");
		CopyDirectory(GetRelativeBinaryDirectory(), tempDirectory);

		// Remove existing archive.
		var packageFileNoExtension =
			$"{RelativeReleasesFolderPath}\\{AppName}-v{GetAppVersion()}-{Platform}";
		var archiveFile = $"{packageFileNoExtension}.zip";
		if (File.Exists(archiveFile))
		{
			Console.WriteLine($"Deleting existing archive file: {archiveFile}");
			File.Delete(archiveFile);
		}

		// Archive.
		Console.WriteLine($"Archiving to: {archiveFile}");
		var process = new Process();
		process.StartInfo.FileName = GetSevenZipPath();
		process.StartInfo.Arguments = $"a {archiveFile} \"{tempDirectory}\"";
		process.Start();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			Console.WriteLine($"Packaging {archiveFile} failed.");
			return false;
		}

		// Clean up temp directory.
		if (Directory.Exists(tempDirectory))
			Directory.Delete(tempDirectory, true);

		return true;
	}
}
