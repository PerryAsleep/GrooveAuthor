using System.Collections.Generic;
using Fumen;
using Microsoft.Xna.Framework;

namespace StepManiaEditor;

/// <summary>
/// Interface for platform-specific functionality needed by the Editor.
/// </summary>
public interface IEditorPlatform
{
	public void SetEditor(Editor editor);
	public void InitializeWindowHandleCallbacks();
	public void PlayExclamationSound();

	/// <summary>
	/// Show a save file dialog for a simfile.
	/// </summary>
	/// <param name="initialDirectory">The initial directory of the dialog.</param>
	/// <param name="fileName">File name to default to for saving.</param>
	/// <param name="fileFormatType">The file format type to default to in the dialog.</param>
	/// <returns>Tuple with the following values:
	/// - A boolean representing whether the dialog was confirmed and saving should continue.
	/// - A string representing the full path to the file to be saved.
	/// </returns>
	public (bool, string) ShowSaveSimFileDialog(string initialDirectory, string fileName, FileFormatType? fileFormatType);

	/// <summary>
	/// Show an open file dialog for a simfile.
	/// </summary>
	/// <param name="initialDirectory">The initial directory of the dialog.</param>
	/// <returns>Tuple with the following values:
	/// - A boolean representing whether the dialog was confirmed and opening should continue.
	/// - A string representing the full path to the file to be opened.
	/// </returns>
	public (bool, string) ShowOpenSimFileDialog(string initialDirectory);

	public string BrowseFile(string name, string initialDirectory, string currentFileRelativePath, List<string[]> extensionTypes,
		bool includeAllFiles);

	public void Update(GameTime gameTime);
}
