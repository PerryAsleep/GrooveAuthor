using System.Text.RegularExpressions;
using System.Xml.Linq;

// Get the current version.
Version currentVersion;
try
{
	currentVersion = GetAppVersion();
}
catch (Exception)
{
	Console.WriteLine("Failed to determine current version.");
	return 1;
}

// Read the new version.
var input = "";
Version newVersion = null;
while (string.IsNullOrEmpty(input))
{
	Console.Write($"Enter new version (current version is {currentVersion}): ");
	input = Console.ReadLine();
	try
	{
		newVersion = GetVersionFromString(input);
	}
	catch (Exception e)
	{
		Console.WriteLine($"Could not parse {input} into a semantic version. {e}");
		input = "";
		newVersion = null;
	}

	if (newVersion != null && newVersion < currentVersion)
	{
		Console.WriteLine(
			$"New version ({GetVersionAsString(newVersion)}) must be greater than old version ({GetVersionAsString(currentVersion)}).");
		input = "";
	}
}

// Update csproj files.
if (!WriteVersionIntoCsProj(@"..\..\..\..\StepManiaEditorWindows\StepManiaEditorWindows.csproj", newVersion))
	return 1;
if (!WriteVersionIntoCsProj(@"..\..\..\..\StepManiaEditorWindowsOpenGL\StepManiaEditorWindowsOpenGL.csproj", newVersion))
	return 1;
if (!WriteVersionIntoCsProj(@"..\..\..\..\StepManiaEditorLinux\StepManiaEditorLinux.csproj", newVersion))
	return 1;
if (!WriteVersionIntoCsProj(@"..\..\..\..\StepManiaEditorMacOS\StepManiaEditorMacOS.csproj", newVersion))
	return 1;

// Update MacOS info.plist.
if (!WriteVersionIntoPlist(@"..\..\..\..\StepManiaEditorMacOS\Info.plist", newVersion))
	return 1;

return 0;

Version GetAppVersion()
{
	var xmlDoc = XDocument.Load(@"..\..\..\..\StepManiaEditorWindows\StepManiaEditorWindows.csproj");
	var versionString = xmlDoc.Descendants("Version").First().Value;
	return GetVersionFromString(versionString);
}

Version GetVersionFromString(string versionString)
{
	var parts = versionString.Split('.');
	if (parts.Length != 3)
		throw new Exception($"Expected three parts to version string. Found {parts.Length}");
	return new Version(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
}

string GetVersionAsString(Version version)
{
	return $"{version.Major}.{version.Minor}.{version.Build}";
}

bool WriteVersionIntoCsProj(string file, Version inVersion)
{
	var fileName = file.Substring(file.LastIndexOf('\\') + 1);
	var newVersionString = GetVersionAsString(inVersion);
	Console.WriteLine($"Updating {fileName} version to {newVersionString}.");
	try
	{
		var text = File.ReadAllText(file);
		text = Regex.Replace(
			text,
			"(<Version>)([^<]+)(</Version>)",
			match => $"{match.Groups[1].Value}{inVersion}{match.Groups[3].Value}"
		);
		File.WriteAllText(file, text);
		Console.WriteLine($"Updated {fileName} version to {newVersionString}.");
		return true;
	}
	catch (Exception e)
	{
		Console.WriteLine($"Failed updating {fileName} version to {newVersionString}. {e}");
	}

	return false;
}

bool WriteVersionIntoPlist(string file, Version inVersion)
{
	var fileName = file.Substring(file.LastIndexOf('\\') + 1);
	var newVersionString = GetVersionAsString(inVersion);
	Console.WriteLine($"Updating {fileName} version to {newVersionString}.");
	try
	{
		var text = File.ReadAllText(file);
		text = Regex.Replace(
			text,
			@"(<key>CFBundleShortVersionString</key>\s*<string>)([^<]+)(</string>)",
			match => $"{match.Groups[1].Value}{inVersion}{match.Groups[3].Value}"
		);
		text = Regex.Replace(
			text,
			@"(<key>CFBundleVersion</key>\s*<string>)([^<]+)(</string>)",
			match => $"{match.Groups[1].Value}{inVersion}{match.Groups[3].Value}"
		);
		File.WriteAllText(file, text);
		Console.WriteLine($"Updated {fileName} version to {newVersionString}.");
		return true;
	}
	catch (Exception e)
	{
		Console.WriteLine($"Failed updating {fileName} version to {inVersion}. {e}");
	}

	return false;
}
