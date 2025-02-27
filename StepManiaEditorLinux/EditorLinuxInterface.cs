using System.Collections.Generic;
using System.Text;
using System.Threading;
using Fumen;
using Gtk;
using Microsoft.Xna.Framework;
using StepManiaEditor;

namespace StepManiaEditorLinux;

/// <summary>
/// Linux platform implementation of IEditorPlatform.
/// </summary>
internal sealed class EditorLinuxInterface : IEditorPlatform
{
	public void Initialize()
	{
		// Initialize GTK but prevent it from modifying the SynchronizationContext.
		// It will set it to a GLibSynchronizationContext which will run every async
		// continuation on the main thread which results in nested async operations
		// locking up the main thread.
		var sc = SynchronizationContext.Current;
		Application.Init();
		SynchronizationContext.SetSynchronizationContext(sc);
	}

	#region Sounds

	public void PlayExclamationSound()
	{
	}

	#endregion Sounds

	#region File I/O

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
