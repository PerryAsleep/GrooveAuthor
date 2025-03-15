using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Fumen;
using Gtk;
using Microsoft.Xna.Framework;
using StepManiaEditor;
using Path = Fumen.Path;

namespace StepManiaEditorLinux;

/// <summary>
/// Linux platform implementation of IEditorPlatform.
/// </summary>
internal sealed class EditorLinuxInterface : IEditorPlatform
{
	/// <summary>
	/// Directory to use for persistence.
	/// </summary>
	private string PersistenceDirectory;

	public void Initialize()
	{
		// Initialize GTK but prevent it from modifying the SynchronizationContext.
		// It will set it to a GLibSynchronizationContext which will run every async
		// continuation on the main thread which results in nested async operations
		// locking up the main thread.
		var sc = SynchronizationContext.Current;
		Application.Init();
		SynchronizationContext.SetSynchronizationContext(sc);

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

		if (!TryGetDataHomeDirectory(out var dataHomeDirectory))
			return;

		var desiredPersistenceDir = $"{dataHomeDirectory}/grooveauthor";
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

	private bool TryGetDataHomeDirectory(out string directory)
	{
		// Try to use $XDG_DATA_HOME for persistence.
		directory = null;
		try
		{
			directory = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
		}
		catch(Exception)
		{
			// Ignored
		}
		if (!string.IsNullOrEmpty(directory))
		{
			return true;
		}

		// If XDG_DATA_HOME is not set or empty, prefer $HOME/.local/share
		try
		{
			directory = Environment.GetEnvironmentVariable("HOME");
		}
		catch(Exception e)
		{
			Console.WriteLine($"Failed creating creating persistence directory. Could not read HOME directory. {e}");
			return false;
		}
		directory = $"{directory}/.local/share";
		if (!Directory.Exists(directory))
		{
			try
			{
				Directory.CreateDirectory(directory,
					UnixFileMode.UserRead
					| UnixFileMode.UserWrite
					| UnixFileMode.UserExecute);
			}
			catch (Exception e)
			{
				// We have to log to the console here instead of using the Logger because the Logger
				// depends on these directories.
				Console.WriteLine($"Failed creating {directory}. {e}");
				return false;
			}
		}
		return true;
	}

	#region Sounds

	public void PlayExclamationSound()
	{
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
		return AppDomain.CurrentDomain.BaseDirectory;
	}

	public (bool, string) ShowSaveSimFileDialog(string initialDirectory, string fileName, FileFormatType? fileFormatType)
	{
		var confirmed = false;
		string savedFileName = null;
		var dialog = new FileChooserDialog("Save As...",
			null,
			FileChooserAction.Save,
			"Cancel", ResponseType.Cancel,
			"Save", ResponseType.Accept);
		dialog.Modal = true;
		dialog.KeepAbove = true;

		var sscFileFilter = new FileFilter();
		sscFileFilter.Name = "SSC File";
		sscFileFilter.AddPattern("*.ssc");
		dialog.AddFilter(sscFileFilter);

		var smFileFilter = new FileFilter();
		smFileFilter.Name = "SM File";
		smFileFilter.AddPattern("*.sm");
		dialog.AddFilter(smFileFilter);

		dialog.Filter = fileFormatType == FileFormatType.SM ? smFileFilter : sscFileFilter;
		dialog.CurrentName = fileName;
		dialog.SetCurrentFolder(initialDirectory);

		if (dialog.Run() == (int)ResponseType.Accept)
		{
			confirmed = true;
			savedFileName = dialog.Filename;
		}

		dialog.Destroy();
		return (confirmed, savedFileName);
	}

	public (bool, string) ShowOpenSimFileDialog(string initialDirectory)
	{
		var openedFile = false;
		string fileName = null;
		var dialog = new FileChooserDialog("Open File",
			null,
			FileChooserAction.Open,
			"Cancel", ResponseType.Cancel,
			"Open", ResponseType.Accept);
		dialog.Modal = true;
		dialog.KeepAbove = true;

		var simFileFilter = new FileFilter();
		simFileFilter.Name = "StepMania Files (*.sm,*.ssc)";
		simFileFilter.AddPattern("*.sm");
		simFileFilter.AddPattern("*.ssc");
		dialog.AddFilter(simFileFilter);

		var allFileFilter = new FileFilter();
		allFileFilter.Name = "All Files (*.*)";
		allFileFilter.AddPattern("*.*");
		dialog.AddFilter(allFileFilter);

		dialog.Filter = simFileFilter;

		if (!string.IsNullOrEmpty(initialDirectory))
			dialog.SetCurrentFolder(initialDirectory);

		if (dialog.Run() == (int)ResponseType.Accept)
		{
			openedFile = true;
			fileName = dialog.Filename;
		}

		dialog.Destroy();
		return (openedFile, fileName);
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

		var dialog = new FileChooserDialog($"Open {name} File",
			null,
			FileChooserAction.Open,
			"Cancel", ResponseType.Cancel,
			"Open", ResponseType.Accept);
		dialog.Modal = true;
		dialog.KeepAbove = true;

		var filter = new FileFilter();
		var sb = new StringBuilder();
		sb.Append(name);
		sb.Append(" Files (");
		var first = true;
		foreach (var extensions in extensionTypes)
		{
			foreach (var extension in extensions)
			{
				if (!first)
					sb.Append(',');
				var pattern = $"*.{extension}";
				sb.Append(pattern);
				filter.AddPattern(pattern);
				first = false;
			}
		}

		sb.Append(')');
		filter.Name = sb.ToString();
		dialog.AddFilter(filter);

		if (includeAllFiles)
		{
			var allFileFilter = new FileFilter();
			allFileFilter.Name = "All Files (*.*)";
			allFileFilter.AddPattern("*.*");
			dialog.AddFilter(allFileFilter);
		}

		dialog.Filter = filter;

		if (!string.IsNullOrEmpty(initialDirectory))
			dialog.SetCurrentFolder(initialDirectory);

		if (dialog.Run() == (int)ResponseType.Accept)
		{
			var fileName = dialog.Filename;
			relativePath = Path.GetRelativePath(startInitialDirectory, fileName);
		}

		dialog.Destroy();
		return relativePath;
	}

	#endregion File I/O

	public void Update(GameTime gameTime)
	{
		while (Application.EventsPending())
			Application.RunIteration();
	}
}
