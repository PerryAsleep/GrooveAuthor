using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Fumen;
using StepManiaEditor;

namespace StepManiaEditorWindows;

/// <summary>
/// Windows platform implementation of IEditorPlatform.
/// </summary>
internal sealed class EditorWindowsInterface : IEditorPlatform
{
	private Editor Editor;
	private Form Form;

	public void SetEditor(Editor editor)
	{
		Editor = editor;
		Form = (Form)Control.FromHandle(Editor.Window.Handle);
	}

	public void InitializeWindowHandleCallbacks(bool maximized)
	{
		if (maximized)
			Form.WindowState = FormWindowState.Maximized;

		Form.FormClosing += Editor.ClosingForm;
		Form.AllowDrop = true;
		Form.DragEnter += DragEnter;
		Form.DragDrop += DragDrop;
	}

	#region Drag and Drop

	/// <summary>
	/// Called when dragging a file into the window.
	/// </summary>
	public void DragEnter(object sender, DragEventArgs e)
	{
		if (e.Data == null)
			return;
		// The application only supports opening one file at a time.
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
			return;
		var files = (string[])e.Data.GetData(DataFormats.FileDrop);
		if (files?.Length != 1)
		{
			e.Effect = DragDropEffects.None;
			return;
		}

		var file = files[0];

		// Get the extension to determine if the file type is supported.
		if (!Path.GetExtensionWithoutSeparator(file, out var extension))
		{
			e.Effect = DragDropEffects.None;
			return;
		}

		// Set the effect for the drop based on if the file type is supported.
		if (Editor.IsExtensionSupportedForFileDrop(extension))
			e.Effect = DragDropEffects.Copy;
		else
			e.Effect = DragDropEffects.None;
	}

	/// <summary>
	/// Called when dropping a file into the window.
	/// </summary>
	public void DragDrop(object sender, DragEventArgs e)
	{
		if (e.Data == null)
			return;
		// The application only supports opening one file at a time.
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
			return;
		var files = (string[])e.Data.GetData(DataFormats.FileDrop);
		if (files == null)
			return;
		if (files.Length != 1)
			return;
		var file = files[0];

		Editor.DragDrop(file);
	}

	#endregion Drag and Drop

	#region Window Size

	public void SetResolution(int x, int y)
	{
		Form.WindowState = FormWindowState.Normal;
		Form.ClientSize = new System.Drawing.Size(x, y);
	}

	public bool IsMaximized()
	{
		return Form.WindowState == FormWindowState.Maximized;
	}

	#endregion Window Size

	#region Sounds

	public void PlayExclamationSound()
	{
		SystemSounds.Exclamation.Play();
	}

	#endregion Sounds

	#region File I/O

	public (bool, string) ShowSaveSimFileDialog(string initialDirectory, FileFormatType? fileFormatType)
	{
		var saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "SSC File|*.ssc|SM File|*.sm";
		saveFileDialog.Title = "Save As...";
		saveFileDialog.FilterIndex = 0;
		if (fileFormatType == FileFormatType.SM)
			saveFileDialog.FilterIndex = 2;
		saveFileDialog.InitialDirectory = initialDirectory;
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

	#region Clipboard

	public void CopyToClipboard(string text)
	{
		Clipboard.SetText(text);
	}

	#endregion Clipboard

	#region Application Focus

	public bool IsApplicationFocused()
	{
		var activatedHandle = GetForegroundWindow();
		if (activatedHandle == IntPtr.Zero)
			return false;

		GetWindowThreadProcessId(activatedHandle, out var activeProcId);
		return activeProcId == Process.GetCurrentProcess().Id;
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

	#endregion Application Focus

	public void Update(GameTime gameTime)
	{

	}
}
