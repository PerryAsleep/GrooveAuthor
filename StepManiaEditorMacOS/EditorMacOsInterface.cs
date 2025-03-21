using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

	/// <summary>
	/// Editor instance.
	/// </summary>
	private Editor Editor;

	/// <summary>
	/// Lock for managing the pending file to open.
	/// </summary>
	private readonly object PendingFileLock = new object();

	/// <summary>
	/// Pending file to open on the main thread.
	/// </summary>
	private string PendingFileToOpen;

	public EditorMacOsInterface(string[] args)
	{
		// Initialize NSApplication but do not run it.
		// Running it blocks the thread. The Mac OS app uses SDL for window management
		// and Monogame for the update and render loop. We need to initialize the
		// NSApplication in order to respond to OS level events.
		NSApplication.Init();
		NSApplication.SharedApplication.Delegate = new EditorAppDelegate(this);
		NSApplication.SharedApplication.FinishLaunching();
	}

	public void Initialize(Editor editor)
	{
		Editor = editor;

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
		return (confirmed, savePanel.Url?.Path);
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
		return (confirmed, openPanel.Url?.Path);
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
			var fileName = openPanel.Url?.Path;
			relativePath = Path.GetRelativePath(startInitialDirectory, fileName);
		}

		return relativePath;
	}

	public void OpenUrl(string url)
	{
		try
		{
			Process.Start("open", url);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed opening {url}. {e}");
		}
	}

	public void OpenFileBrowser(string path)
	{
		try
		{
			Process.Start("open", $"\"{path}\"");
		}
		catch (Exception e)
		{
			Logger.Error($"Failed opening {path}. {e}");
		}
	}

	#endregion File I/O

	public void Update(GameTime gameTime)
	{
		// Process Mac OS events since we do not use the normal NSApplication lifecycle.
		NSEvent evt;
		do
		{
			evt = NSApplication.SharedApplication.NextEvent(NSEventMask.AnyEvent, NSDate.DistantPast, NSRunLoopMode.Default, true);
			if (evt != null)
			{
				NSApplication.SharedApplication.SendEvent(evt);
				NSApplication.SharedApplication.UpdateWindows();
			}
		} while(evt != null);

		// If the OS has requested we open a file, handle it now on the main thread.
		string pendingFile = null;
		lock(PendingFileLock)
		{
			if (!string.IsNullOrEmpty(PendingFileToOpen))
			{
				pendingFile = PendingFileToOpen;
				PendingFileToOpen = null;
			}
		}
		if (!string.IsNullOrEmpty(pendingFile))
		{
			Logger.Info($"Trying to open pending file from mac os: {pendingFile}");
			Editor.OpenSongFile(pendingFile, true);
		}
	}

	/// <summary>
	/// Callback from EditorAppDelegate for the OS requesting we open a file.
	/// </summary>
	/// <param name="fileName">The file to open.</param>
	public void OsRequestOpenFile(string fileName)
	{
		// We may be on a different thread than the main thread,
		// and we can't guarantee the Editor is instantiated yet.
		// Record the pending file to open for procesing later on
		// the Update loop.
		lock (PendingFileLock)
		{
			PendingFileToOpen = fileName;
		}
	}
}

/// <summary>
/// NSApplicationDelegate implementation.
/// This allows for responding to various OS events.
/// See also: https://developer.apple.com/documentation/appkit/nsapplicationdelegate
/// </summary>
internal sealed class EditorAppDelegate : NSApplicationDelegate
{
	private readonly EditorMacOsInterface MacOsInterface;

	public EditorAppDelegate(EditorMacOsInterface macOsInterface)
	{
		MacOsInterface = macOsInterface;
	}

	/// <summary>
	/// Handle a message from the OS to open files.
	/// See also: https://developer.apple.com/documentation/appkit/nsapplicationdelegate/application(_:openfiles:)
	/// </summary>
	/// <param name="sender">The application object associated with the delegate.</param>
	/// <param name="filenames">An array of strings containing the names of the files to open.</param>
	public override void OpenFiles(NSApplication sender, string[] filenames)
	{
		if (filenames != null && filenames.Length > 0)
		{
			// We can only open one file at a time. Just take the first.
			MacOsInterface.OsRequestOpenFile(filenames[0]);
		}
	}
}
