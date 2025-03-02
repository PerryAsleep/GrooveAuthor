using System.Diagnostics;
using System.Text;

namespace PackageBuild;

/// <summary>
/// Build for Linux.
/// </summary>
internal sealed class BuildLinux : Build
{
	private string SudoPassword;

	public BuildLinux() : base(
		"linux-x64",
		"StepManiaEditorLinux",
		"net8.0",
		"GrooveAuthor")
	{
	}

	protected override bool Package()
	{
		// Delete any unneeded build assets. Ideally these shouldn't be built
		// but some libraries generate binaries for multiple platforms.
		DeleteFiles(GetRelativeBinaryDirectory(), @"\.(dylib|dSYM)$");

		// Create a temp directory for packaging. Use a directory outside
		// the Windows filesystem so that we can set unix permissions.
		var tempDirectory = $"/tmp/{AppName}";
		Console.WriteLine($"Creating temporary directory: {tempDirectory}");
		if (!ExecuteWslCommand($"rm -rf {tempDirectory}", true)
		    || !ExecuteWslCommand($"mkdir {tempDirectory}", true))
		{
			Console.WriteLine($"Failed creating temporary directory {tempDirectory}.");
			return false;
		}

		// Copy product to temp directory.
		Console.WriteLine("Copying product to temporary directory.");
		if (!ExecuteWslCommand($"cp -r {ConvertPathToUnix(GetRelativeBinaryDirectory())}/* {tempDirectory}/", true))
		{
			Console.WriteLine($"Failed copying product to temporary directory {tempDirectory}.");
			return false;
		}

		// Update unix permissions.
		Console.WriteLine("Updating unix permissions");
		if (!ExecuteWslCommand($"find {tempDirectory} -type f -exec chmod 644 {{}} +", true)
		    || !ExecuteWslCommand($"find {tempDirectory} -type d -exec chmod 755 {{}} +", true)
		    || !ExecuteWslCommand($"chmod 755 {tempDirectory}/{AppName}", true))
		{
			Console.WriteLine("Failed updating unix permissions.");
			return false;
		}

		// Fix symlinks.
		if (!ExecuteWslCommand($"rm {tempDirectory}/libfmod.so", true)
		    || !ExecuteWslCommand($"rm {tempDirectory}/libfmod.so.13", true)
		    || !ExecuteWslCommand($"rm {tempDirectory}/libSkiaSharp.so", true)
		    || !ExecuteWslCommand($"ln -sr {tempDirectory}/libfmod.so.13.3 {tempDirectory}/libfmod.so", true)
		    || !ExecuteWslCommand($"ln -sr {tempDirectory}/libfmod.so.13.3 {tempDirectory}/libfmod.so.13", true)
		    || !ExecuteWslCommand($"ln -sr {tempDirectory}/libSkiaSharp.so.116.0.0 {tempDirectory}/libSkiaSharp.so", true))
		{
			Console.WriteLine("Failed updating library symlinks.");
			return false;
		}

		// Remove existing archive.
		var packageFileNoExtension =
			$"{RelativeReleasesFolderPath}\\{AppName}-v{GetAppVersion()}-{Platform}";
		var archiveFile = $"{packageFileNoExtension}.tar";
		if (File.Exists(archiveFile))
		{
			Console.WriteLine($"Deleting existing archive file: {archiveFile}");
			File.Delete(archiveFile);
		}

		// Archive.
		Console.WriteLine($"Archiving to: {archiveFile}");
		if (!ExecuteWslCommand($"tar -cf {ConvertPathToUnix(archiveFile)} -C \"{tempDirectory}/..\" {AppName}", false))
		{
			Console.WriteLine($"Packaging {archiveFile} failed.");
			return false;
		}

		// Clean up temp directory.
		ExecuteWslCommand($"rm -rf {tempDirectory}", true);

		// Remove existing compressed artifact.
		var compressedFile = $"{archiveFile}.gz";
		if (File.Exists(compressedFile))
		{
			Console.WriteLine($"Deleting compressed archive file: {compressedFile}");
			File.Delete(compressedFile);
		}

		// Compress.
		var process = new Process();
		process.StartInfo.FileName = GetSevenZipPath();
		process.StartInfo.Arguments = $"a {compressedFile} {archiveFile}";
		process.Start();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			Console.WriteLine($"Compressing {compressedFile} failed with error code {process.ExitCode}.");
			return false;
		}

		// Remove archive file.
		if (File.Exists(archiveFile))
		{
			Console.WriteLine($"Deleting archive file: {archiveFile}");
			File.Delete(archiveFile);
		}

		return true;
	}

	private static string ConvertPathToUnix(string path)
	{
		path = path.Replace("\\", "/");
		if (path.Length > 1 && path[1] == ':')
		{
			var remainder = "";
			if (path.Length > 2)
				remainder = path.Substring(3);
			path = $"/mnt/{path[0].ToString().ToLower()}/{remainder}";
		}

		return path;
	}

	private bool ExecuteWslCommand(string command, bool sudo)
	{
		var password = sudo ? GetSudoPassword() : "";
		var process = new Process();
		process.StartInfo.FileName = "wsl";
		process.StartInfo.RedirectStandardInput = true;
		if (sudo)
			process.StartInfo.Arguments = $"sudo -S {command}";
		else
			process.StartInfo.Arguments = command;
		process.Start();
		if (sudo)
			process.StandardInput.WriteLine(password);
		process.WaitForExit();
		return process.ExitCode == 0;
	}

	private string GetSudoPassword()
	{
		if (!string.IsNullOrEmpty(SudoPassword))
			return SudoPassword;
		Console.Write("Enter WSL sudo password: ");
		SudoPassword = GetPassword();
		return SudoPassword;
	}

	private string GetPassword()
	{
		var password = new StringBuilder();
		while (true)
		{
			var key = Console.ReadKey(true);
			if (key.Key == ConsoleKey.Enter)
				break;
			password.Append(key.KeyChar);
		}

		return password.ToString();
	}
}
