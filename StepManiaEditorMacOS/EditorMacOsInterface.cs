using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using AppKit;
using Foundation;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
	private readonly object PendingFileLock = new();

	/// <summary>
	/// Pending file to open on the main thread.
	/// </summary>
	private string PendingFileToOpen;

	public EditorMacOsInterface()
	{
		// Initialize NSApplication but do not run it.
		// Running it blocks the thread. The macOS app uses SDL for window management
		// and Monogame for the update and render loop. We need to initialize the
		// NSApplication in order to respond to OS level events.
		NSApplication.Init();
		NSApplication.SharedApplication.Delegate = new EditorAppDelegate(this);
		NSApplication.SharedApplication.FinishLaunching();

		InitializeKeyboardEvents();
	}

	/// <summary>
	/// On Mac SDL often does not issue key up events, resulting in input getting stuck.
	/// However, Mac's NSEvents for key events do seem to always fire even when the SDL
	/// events do not. Because of this we respond to the NSEvents and route them to the
	/// Monogame Keyboard. This could be moved to inside Monogame but I am currently
	/// trying to keep all the AppKit/Foundation logic in one place.
	/// </summary>
	private void InitializeKeyboardEvents()
	{
		NSEvent.AddLocalMonitorForEventsMatchingMask(NSEventMask.KeyUp, evt =>
		{
			if (KeyboardUtil.TryGetKeyFromNSEventKey(evt.KeyCode, out var key))
				Keyboard.KeyUp(key);
			return evt;
		});
		NSEvent.AddLocalMonitorForEventsMatchingMask(NSEventMask.KeyDown, evt =>
		{
			if (KeyboardUtil.TryGetKeyFromNSEventKey(evt.KeyCode, out var key))
				Keyboard.KeyDown(key);
			return evt;
		});

		// Modifier keys work differently. This event will fire when any modifier key changes
		// but we do not know if the given key was pressed or released as there are multiple
		// keys per modifier (e.g. left shift and right shift both affect shift) and all we can
		// check is the key-agnostic modifier state (e.g. is shift active or not). Because of
		// this limitation (and because our app treats all right modifiers as left) just use the
		// the left modifier keys. This could result in a situation where pressing left, then
		// right then releasing right then left would result in left down, left down again 2
		// more times incorrectly, then finally left up. But this doesn't matter in practice
		// because it is rare to hold both keys for a single modifier.
		NSEvent.AddLocalMonitorForEventsMatchingMask(NSEventMask.FlagsChanged, evt =>
		{
			switch (evt.KeyCode)
			{
				case 0x38: // Left Shift
				case 0x3C: // Right Shift
					if (evt.ModifierFlags.HasFlag(NSEventModifierMask.ShiftKeyMask))
						Keyboard.KeyDown(Keys.LeftShift);
					else
						Keyboard.KeyUp(Keys.LeftShift);
					break;
				case 0x3B: // Left Control
				case 0x3E: // Right Control
					if (evt.ModifierFlags.HasFlag(NSEventModifierMask.ControlKeyMask))
						Keyboard.KeyDown(Keys.LeftControl);
					else
						Keyboard.KeyUp(Keys.LeftControl);
					break;
				case 0x3A: // Left Option
				case 0x3D: // Right Option
					if (evt.ModifierFlags.HasFlag(NSEventModifierMask.AlternateKeyMask))
						Keyboard.KeyDown(Keys.LeftAlt);
					else
						Keyboard.KeyUp(Keys.LeftAlt);
					break;
				case 0x37: // Left Command
				case 0x36: // Right Command
					if (evt.ModifierFlags.HasFlag(NSEventModifierMask.CommandKeyMask))
						Keyboard.KeyDown(Keys.LeftWindows);
					else
						Keyboard.KeyUp(Keys.LeftWindows);
					break;
			}
			return evt;
		});

		// Clear all held keys when gaining or losing focus.
		NSNotificationCenter.DefaultCenter.AddObserver(NSApplication.DidBecomeActiveNotification,
			notification => Keyboard.ClearHeldKeys());
		NSNotificationCenter.DefaultCenter.AddObserver(NSApplication.DidResignActiveNotification,
			notification => Keyboard.ClearHeldKeys());
		NSNotificationCenter.DefaultCenter.AddObserver(NSWindow.DidBecomeKeyNotification,
			notification => Keyboard.ClearHeldKeys());
		NSNotificationCenter.DefaultCenter.AddObserver(NSWindow.DidResignKeyNotification,
			notification => Keyboard.ClearHeldKeys());
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

	// ReSharper disable InconsistentNaming
	[DllImport("/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox")]
	private static extern void AudioServicesPlaySystemSound(uint inSystemSoundID);
	// ReSharper restore InconsistentNaming

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
		return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../Resources");
	}

	private UTType[] GetAllowSimFileTypes()
	{
		var allowedTypes = new List<UTType>();
		var sscType = UTType.CreateFromExtension("ssc");
		if (sscType != null)
			allowedTypes.Add(sscType);
		var smType = UTType.CreateFromExtension("sm");
		if (smType != null)
			allowedTypes.Add(smType);
		return allowedTypes.ToArray();
	}

	public (bool, string) ShowSaveSimFileDialog(string initialDirectory, string fileName, FileFormatType? fileFormatType)
	{
		var confirmed = false;
		var path = "";
		try
		{
			var savePanel = NSSavePanel.SavePanel;
			savePanel.Title = "Save As...";
			savePanel.AllowedContentTypes = GetAllowSimFileTypes();
			savePanel.AllowsOtherFileTypes = false;
			if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
				savePanel.DirectoryUrl = NSUrl.FromFilename(initialDirectory);
			savePanel.NameFieldStringValue = fileName;
			savePanel.CanCreateDirectories = true;
			savePanel.ExtensionHidden = false;
			var result = (NSModalResponse)savePanel.RunModal();
			confirmed = result == NSModalResponse.OK;
			path = savePanel.Url?.Path;
			RestoreMainWindowFocus();
		}
		catch (Exception e)
		{
			Logger.Error($"Failed showing file save simfile dialog: {e}");
		}
		return (confirmed, path);
	}

	public (bool, string) ShowOpenSimFileDialog(string initialDirectory)
	{
		var confirmed = false;
		var path = "";
		try
		{
			var openPanel = NSOpenPanel.OpenPanel;
			openPanel.AllowedContentTypes = GetAllowSimFileTypes();

			if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
				openPanel.DirectoryUrl = NSUrl.FromFilename(initialDirectory);

			openPanel.AllowsMultipleSelection = false;
			openPanel.CanChooseDirectories = false;
			openPanel.CanChooseFiles = true;
			var result = (NSModalResponse)openPanel.RunModal();
			confirmed = result == NSModalResponse.OK;
			path = openPanel.Url?.Path;
			RestoreMainWindowFocus();
		}
		catch (Exception e)
		{
			Logger.Error($"Failed showing file open simfile dialog: {e}");
		}
		return (confirmed, path);
	}

	public string BrowseFile(string name, string initialDirectory, string currentFileRelativePath, List<string[]> extensionTypes,
		bool includeAllFiles)
	{
		string relativePath = null;
		try
		{
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
						var fileType = UTType.CreateFromExtension(extension);
						if (fileType != null)
						{
							allowedContentTypes.Add(UTType.CreateFromExtension(extension));
						}
					}
				}

				openPanel.AllowedContentTypes = allowedContentTypes.ToArray();
			}

			openPanel.DirectoryUrl = NSUrl.FromFilename(initialDirectory ?? "");
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
			RestoreMainWindowFocus();
		}
		catch (Exception e)
		{
			Logger.Error($"Failed showing file open dialog: {e}");
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

	private void RestoreMainWindowFocus()
	{
		try
		{
			var nsApp = NSApplication.SharedApplication;
			nsApp.ActivateIgnoringOtherApps(true);
			var windowsArray = nsApp.ValueForKey((NSString)"windows") as NSArray;
			if (windowsArray != null)
			{
				for (nuint i = 0; i < windowsArray.Count; i++)
				{
					var windowPtr = windowsArray.GetItem<NSObject>(i);
					if (windowPtr is NSWindow window && window.IsVisible && !window.IsMiniaturized)
					{
						window.MakeKeyAndOrderFront(null);
						break;
					}
				}
			}
		}
		catch (Exception)
		{
			// Ignored.
		}
	}

	#endregion File I/O

	public void Update(GameTime gameTime)
	{
		// Process macOS events since we do not use the normal NSApplication lifecycle.
		NSEvent evt;
		do
		{
			evt = NSApplication.SharedApplication.NextEvent(NSEventMask.AnyEvent, NSDate.DistantPast, NSRunLoopMode.Default,
				true);
			if (evt != null)
			{
				NSApplication.SharedApplication.SendEvent(evt);
				NSApplication.SharedApplication.UpdateWindows();
			}
		} while (evt != null);

		// If the OS has requested we open a file, handle it now on the main thread.
		string pendingFile = null;
		lock (PendingFileLock)
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
		// Record the pending file to open for processing later on
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
	[SuppressMessage("ReSharper", "CommentTypo")]
	public override void OpenFiles(NSApplication sender, string[] filenames)
	{
		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		if (filenames != null && filenames.Length > 0)
		{
			// We can only open one file at a time. Just take the first.
			MacOsInterface.OsRequestOpenFile(filenames[0]);
		}
	}
}
