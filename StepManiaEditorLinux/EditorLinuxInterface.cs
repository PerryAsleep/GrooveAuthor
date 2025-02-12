using System.Collections.Generic;
using Fumen;
using StepManiaEditor;
using Gtk;
using Microsoft.Xna.Framework;

namespace StepManiaEditorLinux;

/// <summary>
/// Linux platform implementation of IEditorPlatform.
/// </summary>
internal sealed class EditorLinuxInterface : IEditorPlatform
{
	private Editor Editor;
	private Application App;

	public EditorLinuxInterface()
	{
	}

	public void SetEditor(Editor editor)
	{
		Editor = editor;

		Application.Init();
	}

	public void InitializeWindowHandleCallbacks(bool maximized)
	{
		
	}

	#region Drag and Drop


	#endregion Drag and Drop

	#region Window Size

	public void SetResolution(int x, int y)
	{
		
	}

	public bool IsMaximized()
	{
		return false;
	}

	#endregion Window Size

	#region Sounds

	public void PlayExclamationSound()
	{
		
	}

	#endregion Sounds

	#region File I/O

	public (bool, string) ShowSaveSimFileDialog(string initialDirectory, FileFormatType? fileFormatType)
	{
		return (false, null);
	}

	public (bool, string) ShowOpenSimFileDialog(string initialDirectory)
	{
		var openedFile = false;
		string fileName = null;
		var dialog = new FileChooserDialog("Open simfile",
		null,
		FileChooserAction.Open,
		"Cancel", ResponseType.Cancel,
		"Open", ResponseType.Accept);
		dialog.Modal = true;
		dialog.KeepAbove = true;
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
		return null;
	}

	#endregion File I/O

	#region Clipboard

	public void CopyToClipboard(string text)
	{

	}

	#endregion Clipboard

	#region Application Focus

	public bool IsApplicationFocused()
	{
		return true;
	}

	#endregion Application Focus

	public void Update(GameTime gameTime)
	{
		while(Application.EventsPending())
			Application.RunIteration();
	}
}
