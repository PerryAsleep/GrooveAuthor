using System;
using System.IO;
using System.Threading.Tasks;
using Fumen;
using Fumen.Converters;
using ImGuiNET;
using static Fumen.Converters.ItgManiaPack;

namespace StepManiaEditor;

/// <summary>
/// EditorItgManiaPack is a wrapper for ItgManiaPack which adds the following functionality:
///  - State tracking for whether unsaved changes are present.
///  - Observation of the underlying pack file for showing external modification notifications.
///  - Notifications for fields changing which Observers may need to respond to, like the Banner asset.
/// </summary>
internal sealed class EditorItgManiaPack : Notifier<EditorItgManiaPack>, IDisposable
{
	public const string NotificationBannerChanged = "BannerChanged";

	/// <summary>
	/// Owning EditorPack;
	/// </summary>
	private readonly EditorPack EditorPack;

	/// <summary>
	/// Underlying ItgManiaPack.
	/// </summary>
	private readonly ItgManiaPack Pack;

	/// <summary>
	/// Path to the ITGmania pack file.
	/// </summary>
	private readonly string FilePath;

	/// <summary>
	/// Last saved state for tracking whether unsaved changes are present.
	/// </summary>
	private ItgManiaPack LastSavedPackState;

	/// <summary>
	/// FileSystemWatcher for monitoring external changes to teh pack file.
	/// </summary>
	private FileSystemWatcher FileWatcher;

	/// <summary>
	/// Whether or not we are saving.
	/// </summary>
	private bool Saving;

	/// <summary>
	/// Whether or not we are showing an external modification notification.
	/// </summary>
	private bool ShowingFileChangedNotification;

	/// <summary>
	/// The last time a save occurred, for controlling external notification behavior.
	/// </summary>
	private DateTime LastSaveCompleteTime;

	#region Pack Properties

	public string Title
	{
		get => Pack.Title;
		set => Pack.Title = value;
	}

	public string TitleTransliteration
	{
		get => Pack.TitleTransliteration;
		set => Pack.TitleTransliteration = value;
	}

	public string TitleSort
	{
		get => Pack.TitleSort;
		set => Pack.TitleSort = value;
	}

	public string Series
	{
		get => Pack.Series;
		set => Pack.Series = value;
	}

	public int Year
	{
		get => Pack.Year;
		set => Pack.Year = value;
	}

	public string Banner
	{
		get => Pack.Banner;
		set
		{
			if (Pack.Banner != value)
			{
				Pack.Banner = value;
				Notify(NotificationBannerChanged, this);
			}
		}
	}

	public SyncOffSetType SyncOffset
	{
		get => Pack.SyncOffset;
		set => Pack.SyncOffset = value;
	}

	#endregion Pack Properties

	/// <summary>
	/// Private constructor.
	/// </summary>
	/// <param name="editorPack">Owning EditorPack.</param>
	/// <param name="filePath">Path to the ITGmania pack file.</param>
	/// <param name="pack">ItgManiaPack.</param>
	private EditorItgManiaPack(EditorPack editorPack, string filePath, ItgManiaPack pack)
	{
		EditorPack = editorPack;
		FilePath = filePath;
		Pack = pack;
		LastSavedPackState = (ItgManiaPack)Pack?.Clone();
		LastSaveCompleteTime = DateTime.Now;
	}

	/// <summary>
	/// Public method for creating an EditorItgManiaPack from an already loaded ItgManiaPack.
	/// </summary>
	/// <param name="editorPack">Owning EditorPack.</param>
	/// <param name="filePath">Path to the ITGmania pack file.</param>
	/// <param name="pack">Already loaded ItgManiaPack.</param>
	/// <returns>Newly created EditorItgManiaPack.</returns>
	public static EditorItgManiaPack CreatePackFromLoadedItgManiaPack(EditorPack editorPack, string filePath, ItgManiaPack pack)
	{
		if (pack == null)
			return null;
		var editorItgManiaPack = new EditorItgManiaPack(editorPack, filePath, pack);
		editorItgManiaPack.StartObservingFile();
		return editorItgManiaPack;
	}

	/// <summary>
	/// Public method for creating a new EditorItgManiaPack and persisting it to disk.
	/// </summary>
	/// <param name="editorPack">Owning EditorPack.</param>
	/// <param name="filePath">Path for saving the ITGmania pack file.</param>
	/// <param name="title">Pack title.</param>
	/// <param name="banner">Path to the pack banner asset.</param>
	/// <param name="offset">The SyncOffSetType of the pack.</param>
	/// <returns>Newly created EditorItgManiaPack.</returns>
	public static async Task<EditorItgManiaPack> CreateNewPack(
		EditorPack editorPack,
		string filePath,
		string title,
		string banner,
		SyncOffSetType offset)
	{
		var editorItgManiaPack = new EditorItgManiaPack(editorPack, filePath, new ItgManiaPack
		{
			Title = title,
			TitleTransliteration = "",
			TitleSort = title,
			Series = "",
			Year = DateTime.UtcNow.Year,
			Banner = banner,
			SyncOffset = offset,
		});

		var saveSuccess = await editorItgManiaPack.SaveAsync();
		if (saveSuccess)
		{
			editorItgManiaPack.StartObservingFile();
		}

		return editorItgManiaPack;
	}

	/// <summary>
	/// Asynchronously save this EditorItgManiaPack to disk.
	/// </summary>
	/// <returns>True is saving was successful and false otherwise.</returns>
	public async Task<bool> SaveAsync()
	{
		Logger.Info($"Saving {FileName}...");
		Saving = true;
		var clonedPack = (ItgManiaPack)Pack.Clone();
		var success = await clonedPack.SaveAsync(FilePath);
		if (success)
		{
			LastSavedPackState = clonedPack;
			LastSaveCompleteTime = DateTime.Now;
			Logger.Info($"Saved {FileName}.");
		}
		else
		{
			Logger.Error($"Failed to saved {FileName}.");
		}

		Saving = false;
		return success;
	}

	/// <summary>
	/// Synchronously save this EditorItgManiaPack to disk.
	/// </summary>
	/// <returns>True is saving was successful and false otherwise.</returns>
	public bool Save()
	{
		Saving = true;
		var clonedPack = (ItgManiaPack)Pack.Clone();
		var success = clonedPack.Save(FilePath);
		if (success)
		{
			LastSavedPackState = clonedPack;
			LastSaveCompleteTime = DateTime.Now;
		}

		Saving = false;
		return success;
	}

	/// <summary>
	/// Returns whether or not this EditorItgManiaPack has unsaved changes.
	/// </summary>
	/// <returns>True if this EditorItgManiaPack has unsaved changes and false otherwise.</returns>
	public bool HasUnsavedChanges()
	{
		return !Pack.Matches(LastSavedPackState);
	}

	/// <summary>
	/// Gets the path to the pack ini file.
	/// </summary>
	/// <returns>Path to the pack ini file.</returns>
	public string GetFilePath()
	{
		return FilePath;
	}

	/// <summary>
	/// Gets the path to the pack directory, containing the pack ini file and song folders.
	/// </summary>
	/// <returns>Path to the pack directory.</returns>
	public string GetPackDirectory()
	{
		try
		{
			return Directory.GetParent(FilePath)?.FullName;
		}
		catch (Exception)
		{
			// Ignored.
		}

		return null;
	}

	/// <summary>
	/// Delete the underlying Pack.ini file.
	/// This EditorItgManiaPack object should not be used after this operation.
	/// </summary>
	public void Delete()
	{
		StopObservingFile();
		try
		{
			File.Delete(FilePath);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to delete {FileName}. {e}");
		}
	}

	#region File Observation

	private void StartObservingFile()
	{
		try
		{
			var fullPath = FilePath;
			// Remove potential relative directory symbols as FileSystemWatcher
			// throws exceptions when they are present.
			fullPath = System.IO.Path.GetFullPath(fullPath);
			var dir = System.IO.Path.GetDirectoryName(fullPath);
			var file = System.IO.Path.GetFileName(fullPath);
			if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
			{
				FileWatcher?.Dispose();
				FileWatcher = new FileSystemWatcher(dir);
				FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
				FileWatcher.Changed += OnFileChangedNotification;
				FileWatcher.Filter = file;
				FileWatcher.EnableRaisingEvents = true;
			}
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to observe {FilePath} for changes: {e}");
		}
	}

	private void OnFileChangedNotification(object sender, FileSystemEventArgs e)
	{
		if (e.ChangeType != WatcherChangeTypes.Changed)
			return;
		if (Preferences.Instance.PreferencesOptions.SuppressExternalPackModificationNotification)
			return;

		// Check for showing a notification on the main thread.
		MainThreadDispatcher.RunOnMainThread(CheckForShowingSongFileChangedNotification);
	}

	private void CheckForShowingSongFileChangedNotification()
	{
		if (Saving)
			return;

		// There is no clean way to identify whether the notification is due to a change originating
		// from this application or an external application. If we haven't saved recently, assume it
		// is an external application.
		var timeSinceLastSave = DateTime.Now - LastSaveCompleteTime;
		if (timeSinceLastSave.TotalSeconds < 3)
		{
			return;
		}

		ShowFileChangedModal();
	}

	private void ShowFileChangedModal()
	{
		// Do not show the notification if one is already showing.
		if (ShowingFileChangedNotification)
			return;

		UIModals.OpenModalTwoButtons(
			"External Pack Modification",
			$"{FileName} was modified externally.",
			"Ignore", () => { ShowingFileChangedNotification = false; },
			"Reload", () =>
			{
				ShowingFileChangedNotification = false;
				EditorPack.Refresh();
			},
			() =>
			{
				if (HasUnsavedChanges())
				{
					ImGui.PushStyleColor(ImGuiCol.Text, UILog.GetColor(LogLevel.Warn));
					ImGui.TextUnformatted("Warning: There are unsaved changes. Reloading will lose these changes.");
					ImGui.PopStyleColor();
					ImGui.Separator();
				}

				ImGui.Checkbox("Don't notify on external pack file changes.",
					ref Preferences.Instance.PreferencesOptions.SuppressExternalPackModificationNotification);
			});
		ShowingFileChangedNotification = true;
	}

	private void StopObservingFile()
	{
		FileWatcher?.Dispose();
		FileWatcher = null;
	}

	#endregion File Observation

	#region IDisposable

	public void Dispose()
	{
		FileWatcher?.Dispose();
		FileWatcher = null;
	}

	#endregion IDisposable
}
