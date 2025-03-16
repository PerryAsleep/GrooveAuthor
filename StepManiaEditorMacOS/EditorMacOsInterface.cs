using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using AppKit;
using Foundation;
using Fumen;
using Microsoft.Xna.Framework;
using StepManiaEditor;
using UniformTypeIdentifiers;
using Path = Fumen.Path;

namespace StepManiaEditorMacOs;

/// <summary>
/// MacOS platform implementation of IEditorPlatform.
/// </summary>
internal sealed class EditorMacOsInterface : IEditorPlatform
{
	/// <summary>
	/// Directory to use for persistence.
	/// </summary>
	private string PersistenceDirectory;

	public void Initialize()
	{
		// Ensure the directory we need to use for persistence is available.
		InitializePersistenceDirectory();
	}

	/// <summary>
	/// Initialize the directory to use for persistence.
	/// </summary>
	private void InitializePersistenceDirectory()
	{
		// Fallback.
		PersistenceDirectory = Editor.GetAssemblyPath();

		string userProfileFolder;
		try
		{
			userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}
		catch (Exception e)
		{
			// We have to log to the console here instead of using the Logger because the Logger
			// depends on these directories.
			Console.WriteLine($"Failed creating creating persistence directory. Could not user profile directory. {e}");
			return;
		}

		var desiredPersistenceDir = Path.Combine(userProfileFolder, "Library/Application Support/GrooveAuthor");
		if (!Directory.Exists(desiredPersistenceDir))
		{
			try
			{
				Directory.CreateDirectory(desiredPersistenceDir,
					UnixFileMode.UserRead
					| UnixFileMode.UserWrite
					| UnixFileMode.UserExecute
					| UnixFileMode.GroupRead
					| UnixFileMode.GroupExecute
					| UnixFileMode.OtherRead
					| UnixFileMode.OtherExecute);
			}
			catch (Exception e)
			{
				// We have to log to the console here instead of using the Logger because the Logger
				// depends on these directories.
				Console.WriteLine($"Failed creating {desiredPersistenceDir}. {e}");
				return;
			}
		}

		PersistenceDirectory = desiredPersistenceDir;
	}

	#region Sounds

	[DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
	private static extern void AudioServicesPlaySystemSound(uint inSystemSoundID);

	public void PlayExclamationSound()
	{
		// 0x00001000 is kSystemSoundID_UserPreferredAlert.
		AudioServicesPlaySystemSound(0x00001000);
	}

	#endregion Sounds

	#region File I/O

	public string GetImGuiSaveFileName()
	{
		return $"{PersistenceDirectory}/imgui.ini";
	}

	public string GetPreferencesSaveFileName()
	{
		return $"{PersistenceDirectory}/Preferences.json";
	}

	public string GetLogsDirectory()
	{
		return $"{PersistenceDirectory}/logs";
	}

	public string GetAutogenConfigsDirectory()
	{
		return $"{PersistenceDirectory}/AutogenConfigs";
	}

	public string GetResourceDirectory()
	{
		return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../Resources");
	}

	public (bool, string) ShowSaveSimFileDialog(string initialDirectory, string fileName, FileFormatType? fileFormatType)
	{
		var savePanel = NSSavePanel.SavePanel;
		savePanel.Title = "Save As...";
		savePanel.AllowedContentTypes = [UTType.CreateFromExtension("ssc"), UTType.CreateFromExtension("sm")];
		savePanel.AllowsOtherFileTypes = false;
		savePanel.DirectoryUrl = NSUrl.FromString(initialDirectory);
		savePanel.NameFieldStringValue = fileName;
		savePanel.CanCreateDirectories = true;
		savePanel.ExtensionHidden = false;
		var result = (NSModalResponse)savePanel.RunModal();
		var confirmed = result == NSModalResponse.OK;
		return (confirmed, savePanel.Url.Path);
	}

	public (bool, string) ShowOpenSimFileDialog(string initialDirectory)
	{
		var openPanel = NSOpenPanel.OpenPanel;
		openPanel.AllowedContentTypes = [UTType.CreateFromExtension("ssc"), UTType.CreateFromExtension("sm")];
		openPanel.DirectoryUrl = NSUrl.FromString(initialDirectory ?? "");
		openPanel.AllowsMultipleSelection = false;
		openPanel.CanChooseDirectories = false;
		openPanel.CanChooseFiles = true;
		var result = (NSModalResponse)openPanel.RunModal();
		var confirmed = result == NSModalResponse.OK;
		return (confirmed, openPanel.Url.Path);
	}

	public string BrowseFile(string name, string initialDirectory, string currentFileRelativePath, List<string[]> extensionTypes,
		bool includeAllFiles)
	{
		string relativePath = null;

		var startInitialDirectory = initialDirectory;
		if (!string.IsNullOrEmpty(currentFileRelativePath))
		{
			initialDirectory = Path.Combine(initialDirectory, currentFileRelativePath);
			initialDirectory = System.IO.Path.GetDirectoryName(initialDirectory);
		}

		var openPanel = NSOpenPanel.OpenPanel;
		openPanel.Title = $"Open {name} File";
		if (!includeAllFiles)
		{
			var allowedContentTypes = new List<UTType>();
			foreach (var extensionType in extensionTypes)
			{
				foreach (var extension in extensionType)
				{
					allowedContentTypes.Add(UTType.CreateFromExtension(extension));
				}
			}

			openPanel.AllowedContentTypes = allowedContentTypes.ToArray();
		}

		openPanel.DirectoryUrl = NSUrl.FromString(initialDirectory ?? "");
		openPanel.AllowsMultipleSelection = false;
		openPanel.CanChooseDirectories = false;
		openPanel.CanChooseFiles = true;
		var result = (NSModalResponse)openPanel.RunModal();
		var confirmed = result == NSModalResponse.OK;
		if (confirmed)
		{
			var fileName = openPanel.Url.Path;
			relativePath = Path.GetRelativePath(startInitialDirectory, fileName);
		}

		return relativePath;
	}

	#endregion File I/O

	public void Update(GameTime gameTime)
	{
	}
}
