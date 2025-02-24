using System.Collections.Generic;
using System.Media;
using System.Text;
using System.Windows.Forms;
using Fumen;
using Microsoft.Xna.Framework;
using StepManiaEditor;

namespace StepManiaEditorWindowsOpenGL;

/// <summary>
/// Windows platform implementation of IEditorPlatform.
/// </summary>
internal sealed class EditorWindowsOpenGLInterface : IEditorPlatform
{
	private Editor Editor;

	public void SetEditor(Editor editor)
	{
		Editor = editor;
	}

	public void InitializeWindowHandleCallbacks()
	{
	}

	#region Drag and Drop

	/// <summary>
	/// Called when dragging a file into the window.
	/// </summary>
	public void DragEnter(object sender, DragEventArgs e)
	{
	}

	/// <summary>
	/// Called when dropping a file into the window.
	/// </summary>
	public void DragDrop(object sender, DragEventArgs e)
	{
	}

	#endregion Drag and Drop

	#region Sounds

	public void PlayExclamationSound()
	{
		SystemSounds.Exclamation.Play();
	}

	#endregion Sounds

	#region File I/O

	public (bool, string) ShowSaveSimFileDialog(string initialDirectory, string fileName, FileFormatType? fileFormatType)
	{
		var saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "SSC File|*.ssc|SM File|*.sm";
		saveFileDialog.Title = "Save As...";
		saveFileDialog.FilterIndex = 0;
		if (fileFormatType == FileFormatType.SM)
			saveFileDialog.FilterIndex = 2;
		saveFileDialog.InitialDirectory = initialDirectory;
		saveFileDialog.FileName = fileName;
		var confirmed = saveFileDialog.ShowDialog() == DialogResult.OK;
		return (confirmed, saveFileDialog.FileName);
	}

	public (bool, string) ShowOpenSimFileDialog(string initialDirectory)
	{
		using var openFileDialog = new OpenFileDialog();
		openFileDialog.InitialDirectory = initialDirectory;
		openFileDialog.Filter = "StepMania Files (*.sm,*.ssc)|*.sm;*.ssc|All files (*.*)|*.*";
		openFileDialog.FilterIndex = 1;
		var confirmed = openFileDialog.ShowDialog() == DialogResult.OK;
		return (confirmed, openFileDialog.FileName);
	}

	private static string FileOpenFilter(string name, List<string[]> extensionTypes, bool includeAllFiles)
	{
		var sb = new StringBuilder();
		sb.Append(name);
		sb.Append(" Files (");
		var first = true;
		foreach (var extensions in extensionTypes)
		{
			foreach (var extension in extensions)
			{
				if (!first)
					sb.Append(",");
				sb.Append("*.");
				sb.Append(extension);
				first = false;
			}
		}

		sb.Append(")|");
		first = true;
		foreach (var extensions in extensionTypes)
		{
			foreach (var extension in extensions)
			{
				if (!first)
					sb.Append(";");
				sb.Append("*.");
				sb.Append(extension);
				first = false;
			}
		}

		if (includeAllFiles)
		{
			sb.Append("|All files (*.*)|*.*");
		}

		return sb.ToString();
	}

	public string BrowseFile(string name, string initialDirectory, string currentFileRelativePath, List<string[]> extensionTypes,
		bool includeAllFiles)
	{
		var filter = FileOpenFilter(name, extensionTypes, includeAllFiles);

		string relativePath = null;
		using var openFileDialog = new OpenFileDialog();
		var startInitialDirectory = initialDirectory;
		if (!string.IsNullOrEmpty(currentFileRelativePath))
		{
			initialDirectory = Path.Combine(initialDirectory, currentFileRelativePath);
			initialDirectory = System.IO.Path.GetDirectoryName(initialDirectory);
		}

		openFileDialog.InitialDirectory = initialDirectory ?? "";
		openFileDialog.Filter = filter;
		openFileDialog.FilterIndex = 1;
		openFileDialog.Title = $"Open {name} File";

		if (openFileDialog.ShowDialog() == DialogResult.OK)
		{
			var fileName = openFileDialog.FileName;
			relativePath = Path.GetRelativePath(startInitialDirectory, fileName);
		}

		return relativePath;
	}

	#endregion File I/O

	public void Update(GameTime gameTime)
	{
	}
}
