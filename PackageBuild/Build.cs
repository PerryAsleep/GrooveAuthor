using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PackageBuild;

/// <summary>
/// Abstract class for building GrooveAuthor for a specific platform.
/// </summary>
internal abstract class Build
{
	protected const string Fumen7Z = "FUMEN_7Z";
	protected const string AppName = "GrooveAuthor";
	protected const string RelativeProjectRoot = @"..\..\..\..";
	protected const string RelativeReleasesFolderPath = @$"{RelativeProjectRoot}\Releases";

	/// <summary>
	/// Platform, e.g. win64
	/// </summary>
	protected readonly string Platform;

	/// <summary>
	/// Project name, e.g. StepManiaEditorWindows
	/// </summary>
	protected readonly string ProjectName;

	/// <summary>
	/// Framework, e.g. net8.0
	/// </summary>
	protected readonly string Framework;

	/// <summary>
	/// Binary name, e.g. GrooveAuthor.exe
	/// </summary>
	protected readonly string BinaryName;

	/// <summary>
	/// Constructor.
	/// </summary>
	protected Build(string platform, string projectName, string framework, string binaryName)
	{
		Platform = platform;
		ProjectName = projectName;
		Framework = framework;
		BinaryName = binaryName;
	}

	/// <summary>
	/// Publish and package the build for distribution.
	/// </summary>
	/// <returns>True if building was successful and false otherwise.</returns>
	public bool GenerateBuild()
	{
		Console.WriteLine($"Generating {Platform} build.");

		// Clean.
		Console.WriteLine($"Cleaning {ProjectName} project.");
		var process = new Process();
		process.StartInfo.FileName = "dotnet";
		process.StartInfo.Arguments =
			@$"clean .\{GetRelativeProjectPath()} -c Release -f {Framework} -r {Platform}";
		process.Start();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			Console.WriteLine($"Cleaning {ProjectName} failed with error code {process.ExitCode}.");
			return false;
		}

		// Delete any state left over in the build directory.
		Console.WriteLine("Deleting build folder.");
		Directory.Delete(GetRelativeBinRootDirectory(), true);

		// Publish project.
		Console.WriteLine($"Publishing {ProjectName} project.");
		process = new Process();
		process.StartInfo.FileName = "dotnet";
		process.StartInfo.Arguments =
			@$"publish .\{GetRelativeProjectPath()} --force -c Release -f {Framework} -r {Platform} -p:PublishReadyToRun=false -p:TieredCompilation=false";
		process.Start();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			Console.WriteLine($"Publish {ProjectName} failed with error code {process.ExitCode}.");
			return false;
		}

		var result = Package();
		Console.WriteLine(result ? $"Finished generating {Platform} build." : $"Failed generating {Platform} build.");
		return result;
	}

	/// <summary>
	/// Abstract method base classes to take the published build and package it for distribution.
	/// </summary>
	/// <returns>True if packaging was successful and false otherwise.</returns>
	protected abstract bool Package();

	#region Helpers

	protected string GetRelativeProjectDirectory()
	{
		return $@"{RelativeProjectRoot}\{ProjectName}";
	}

	protected string GetRelativeBinRootDirectory()
	{
		return $@"{RelativeProjectRoot}\{ProjectName}\bin\Release";
	}

	protected string GetRelativeBinaryDirectory()
	{
		return $@"{GetRelativeBinRootDirectory()}\{Framework}\{Platform}\publish";
	}

	protected string GetRelativeBinaryPath()
	{
		return GetRelativeBinaryDirectory() + $@"\{BinaryName}";
	}

	protected string GetRelativeProjectPath()
	{
		return @$"{RelativeProjectRoot}\{ProjectName}\{ProjectName}.csproj";
	}

	protected string GetAppVersion()
	{
		var xmlDoc = XDocument.Load(GetRelativeProjectPath());
		return xmlDoc.Descendants("Version").First().Value;
	}

	protected static void CopyDirectory(string sourceDir, string destinationDir,
		IReadOnlyDictionary<string, string>? documentationReplacements = null)
	{
		var dir = new DirectoryInfo(sourceDir);
		if (!dir.Exists)
			return;

		var subDirectories = dir.GetDirectories();
		Directory.CreateDirectory(destinationDir);

		// Copy files.
		foreach (var fileInfo in dir.GetFiles())
		{
			var targetFilePath = Path.Combine(destinationDir, fileInfo.Name);
			fileInfo.CopyTo(targetFilePath);

			// Update documentation if this is a markdown file.
			if (documentationReplacements != null && fileInfo.Extension.Equals(".md"))
			{
				ReplaceTextInFile(targetFilePath, documentationReplacements);
			}
		}

		// Recursively copy directories.
		foreach (var subDirectory in subDirectories)
		{
			var newDestinationDir = Path.Combine(destinationDir, subDirectory.Name);
			CopyDirectory(subDirectory.FullName, newDestinationDir, documentationReplacements);
		}
	}

	private static void ReplaceTextInFile(string fileName, IReadOnlyDictionary<string, string> replacements)
	{
		if (replacements == null || replacements.Count == 0)
			return;

		var text = File.ReadAllText(fileName);
		foreach (var replacement in replacements)
		{
			text = text.Replace(replacement.Key, replacement.Value);
		}

		File.WriteAllText(fileName, text);
	}

	protected static void DeleteFiles(string sourceDir, string regex)
	{
		var dir = new DirectoryInfo(sourceDir);
		if (!dir.Exists)
			return;

		var subDirectories = dir.GetDirectories();

		foreach (var fileInfo in dir.GetFiles())
		{
			if (Regex.Match(fileInfo.Name, regex).Success)
				File.Delete(fileInfo.FullName);
		}

		foreach (var subDirectory in subDirectories)
			DeleteFiles(subDirectory.FullName, regex);
	}

	protected static string? GetSevenZipPath()
	{
		var sevenZipPath = Environment.GetEnvironmentVariable(Fumen7Z);
		if (string.IsNullOrEmpty(sevenZipPath))
		{
			Console.WriteLine(
				$"{Fumen7Z} is not defined. Please set {Fumen7Z} in your environment variables to the path of your 7z.exe executable.");
		}

		return sevenZipPath;
	}

	#endregion Helpers
}
