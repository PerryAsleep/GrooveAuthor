using System.Diagnostics;

const string fumenDevEnv = "FUMEN_DEVENV";
const string fumen7Z = "FUMEN_7Z";

const string appName = "GrooveAuthor";
const string relativeProjectRoot = "..\\..\\..\\..";
const string relativeReleasesFolderPath = $"{relativeProjectRoot}\\Releases";
const string relativeSlnPath = $"{relativeProjectRoot}\\{appName}.sln";

var builds = new List<Build>
{
	new(
		"Windows",
		"StepManiaEditorWindows",
		$@"{relativeProjectRoot}\StepManiaEditorWindows\bin\Release\net8.0-windows7.0\{appName}.exe",
		$@"{relativeProjectRoot}\StepManiaEditorWindows\bin\Release\net8.0-windows7.0"),
};

static void CopyDirectory(string sourceDir, string destinationDir,
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

static void ReplaceTextInFile(string fileName, IReadOnlyDictionary<string, string> replacements)
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

var devEnvPath = Environment.GetEnvironmentVariable(fumenDevEnv);
if (string.IsNullOrEmpty(devEnvPath))
{
	Console.WriteLine(
		$"{fumenDevEnv} is not defined. Please set {fumenDevEnv} in your environment variables to the path of your devenv.exe executable.");
	return 1;
}

var sevenZipPath = Environment.GetEnvironmentVariable(fumen7Z);
if (string.IsNullOrEmpty(sevenZipPath))
{
	Console.WriteLine(
		$"{fumen7Z} is not defined. Please set {fumen7Z} in your environment variables to the path of your 7z.exe executable.");
	return 1;
}

foreach (var build in builds)
{
	// Rebuild project.
	// This is split into separate Clean and Build steps to work around an issue in Visual Studio
	// where Rebuild commands fail due to deleting and not restoring nuget packages.
	Console.WriteLine($"Cleaning {build.ProjectName} project.");
	var process = new Process();
	process.StartInfo.FileName = devEnvPath;
	process.StartInfo.Arguments = $"{relativeSlnPath} /Clean Release /Project {build.ProjectName}";
	process.Start();
	process.WaitForExit();
	if (process.ExitCode != 0)
	{
		Console.WriteLine($"Cleaning {build.ProjectName} failed with error code {process.ExitCode}.");
		return 1;
	}

	// Delete any state left over in the build directory.
	Console.WriteLine("Deleting build folder.");
	Directory.Delete(build.RelativeBinaryFolderPath, true);

	Console.WriteLine($"Building {build.ProjectName} project.");
	process = new Process();
	process.StartInfo.FileName = devEnvPath;
	process.StartInfo.Arguments = $"{relativeSlnPath} /Build Release /Project {build.ProjectName}";
	process.Start();
	process.WaitForExit();
	if (process.ExitCode != 0)
	{
		Console.WriteLine($"Building {build.ProjectName} failed with error code {process.ExitCode}.");
		return 1;
	}

	// Get version.
	var versionInfo = FileVersionInfo.GetVersionInfo(build.RelativeBinaryPath);
	Console.WriteLine(
		$"{appName} version is {versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}.");

	// Copy Release product to temp directory with desired folder name for packaging.
	var tempDirectory = Path.Combine(Path.GetTempPath(), appName);
	Console.WriteLine($"Creating temporary directory: {tempDirectory}");
	if (Directory.Exists(tempDirectory))
		Directory.Delete(tempDirectory, true);
	Directory.CreateDirectory(tempDirectory);
	Console.WriteLine("Copying Release product to temporary directory.");
	CopyDirectory(build.RelativeBinaryFolderPath, tempDirectory);

	// Remove existing release package.
	var packageFile =
		$"{relativeReleasesFolderPath}\\{appName}-v{versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileBuildPart}-{build.Platform}.zip";
	if (File.Exists(packageFile))
	{
		Console.WriteLine($"Deleting existing package file: {packageFile}");
		File.Delete(packageFile);
	}

	// Package.
	Console.WriteLine($"Archiving to: {packageFile}");
	process = new Process();
	process.StartInfo.FileName = sevenZipPath;
	process.StartInfo.Arguments = $"a {packageFile} \"{tempDirectory}\"";
	process.Start();
	process.WaitForExit();
	if (process.ExitCode != 0)
	{
		Console.WriteLine($"Packaging {appName} failed with error code {process.ExitCode}.");
		return 1;
	}
}

Console.WriteLine("Done.");
return 0;

internal class Build
{
	public readonly string Platform;
	public readonly string ProjectName;
	public readonly string RelativeBinaryPath;
	public readonly string RelativeBinaryFolderPath;

	public Build(string platform, string projectName, string relativeBinaryPath, string relativeBinaryFolderPath)
	{
		Platform = platform;
		ProjectName = projectName;
		RelativeBinaryPath = relativeBinaryPath;
		RelativeBinaryFolderPath = relativeBinaryFolderPath;
	}
}
