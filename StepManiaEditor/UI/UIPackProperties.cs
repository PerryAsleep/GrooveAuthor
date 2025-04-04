using System;
using Fumen;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Converters.ItgManiaPack;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing pack properties UI.
/// </summary>
internal sealed class UIPackProperties : UIWindow
{
	/// <summary>
	/// The columns of the pattern config table.
	/// </summary>
	private enum Column
	{
		Title,
		Artist,
		Credit,
		Ratings,
		Open,
	}

	private Editor Editor;
	private EditorPack Pack;

	private EmptyTexture EmptyTextureBanner;

	private static readonly ColumnData[] TableColumnData;
	private static readonly int TitleColumnWidth = UiScaled(80);
	private static readonly float DefaultWidth = UiScaled(548);
	private static readonly float DefaultHeight = UiScaled(860);
	private static readonly float RefreshButtonWidth = UiScaled(52);

	public static UIPackProperties Instance { get; } = new();

	static UIPackProperties()
	{
		var count = Enum.GetNames(typeof(Column)).Length;
		TableColumnData = new ColumnData[count];

		TableColumnData[(int)Column.Title] = new ColumnData("Title", null, ImGuiTableColumnFlags.WidthStretch, 2.0f);
		TableColumnData[(int)Column.Artist] = new ColumnData("Artist", null, ImGuiTableColumnFlags.WidthStretch, 2.0f);
		TableColumnData[(int)Column.Credit] = new ColumnData("Credit", null, ImGuiTableColumnFlags.WidthStretch, 1.0f);
		TableColumnData[(int)Column.Ratings] = new ColumnData("Ratings", null, ImGuiTableColumnFlags.WidthStretch, 1.0f);
		TableColumnData[(int)Column.Open] = new ColumnData("Open", null, ImGuiTableColumnFlags.WidthFixed);
	}

	private UIPackProperties() : base("Pack Properties")
	{
	}

	public void Init(Editor editor, EditorPack pack, GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
	{
		Editor = editor;
		Pack = pack;
		EmptyTextureBanner = new EmptyTexture(graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(), (uint)GetBannerHeight());
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowPackPropertiesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowPackPropertiesWindow = false;
	}

	public void Draw()
	{
		if (!Preferences.Instance.ShowPackPropertiesWindow)
			return;

		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowPackPropertiesWindow, DefaultWidth, DefaultHeight))
		{
			// Pack title and refresh button.
			var packName = Pack.GetPackName();
			var hasPack = !string.IsNullOrEmpty(packName);
			if (!hasPack)
				PushDisabled();

			var itgManiaPack = Pack.GetItgManiaPack();
			var hasItgManiaPack = itgManiaPack != null;

			if (ImGuiLayoutUtils.BeginTable("Pack Properties", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTitleAndTextWithButton("Name", packName, () => { Editor.ReloadPack(); }, "Refresh",
					RefreshButtonWidth,
					"A pack's name is defined by the name of the folder which contains the pack's song folders."
					+ (hasItgManiaPack ? "\n\nIn ITGmania the name can be defined explicitly in the Pack file below." : ""));

				ImGuiLayoutUtils.DrawRowTexture("Banner", Pack.GetBanner()?.GetTexture(), EmptyTextureBanner,
					"Stepmania infers a pack's banner from image assets in the pack's folder." +
					" It uses the lexicographically first image asset in the pack folder regardless of its size or dimensions, preferring the following extensions in order: "
					+ "png, jpg, jpeg, gif, bmp. Depending on the Stepmania theme banners have different recommended sizes."
					+ "\nITG banners are 418x164."
					+ "\nDDR banners are 512x160 or 256x80."
					+ (hasItgManiaPack ? "\n\nIn ITGmania the banner can be defined explicitly in the Pack file below." : ""));

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();

			// ItgMania pack data.
			if (ImGuiLayoutUtils.BeginTable("ItgMania Pack Properties", TitleColumnWidth))
			{
				var packCannotBeEdited = !Pack.CanBeEdited();
				if (packCannotBeEdited)
					PushDisabled();

				// No ITGmania pack file exists. Show a button for adding one.
				if (!hasItgManiaPack)
				{
					if (ImGuiLayoutUtils.DrawRowButton("ITGmania Pack", "Add ITGmania Pack File",
						    $"ITGmania is a popular fork of Stepmania which supports {FileName} files that offer more control for how"
						    + " the game displays packs. Adding an ITGmania Pack will have no effect in Stepmania."))
					{
						// Determine an appropriate default sync for the new ItgMania pack.
						SyncOffSetType? packOffset = null;
						var song = Editor.GetActiveSong();
						if (song != null)
						{
							if (song.SyncOffset.DoubleEquals(SMCommon.ItgOffset))
								packOffset = SyncOffSetType.ITG;
							else if (song.SyncOffset.DoubleEquals(SMCommon.NullOffset))
								packOffset = SyncOffSetType.NULL;
						}

						if (packOffset == null)
						{
							var preferredSync = Preferences.Instance.PreferencesOptions.NewSongSyncOffset;
							if (preferredSync.DoubleEquals(SMCommon.ItgOffset))
								packOffset = SyncOffSetType.ITG;
							else if (preferredSync.DoubleEquals(SMCommon.NullOffset))
								packOffset = SyncOffSetType.NULL;
						}

						packOffset ??= SyncOffSetType.ITG;

						// Create the pack.
						Pack.CreateItgManiaPack(packOffset.Value);
					}
				}

				// An ITGmania pack exists. Show controls for editing it.
				else
				{
					ImGuiLayoutUtils.DrawRowTextInputWithTransliteration(true, "Title", itgManiaPack,
						nameof(EditorItgManiaPack.Title), nameof(EditorItgManiaPack.TitleTransliteration), false, false,
						"(ITGmania only) The pack's title in game.");
					ImGuiLayoutUtils.DrawRowTextInput(true, "Sort Title", itgManiaPack, nameof(EditorItgManiaPack.TitleSort),
						false,
						"(ITGmania only) Text to use for sorting this pack against other packs in game.");
					ImGuiLayoutUtils.DrawRowTextInput(true, "Series", itgManiaPack, nameof(EditorItgManiaPack.Series), false,
						"(ITGmania only) What series this pack is a part of.");
					ImGuiLayoutUtils.DrawRowDragInt(true, "Year", itgManiaPack, nameof(EditorItgManiaPack.Year), false,
						"(ITGmania only) The year of this pack's release.", 0.1f, "%i", 0);
					ImGuiLayoutUtils.DrawRowFileBrowse("Banner", itgManiaPack, nameof(EditorItgManiaPack.Banner),
						() => BrowseBanner(Editor.GetPlatformInterface()),
						ClearBanner,
						false,
						"(ITGmania only) An explicit banner to use for the pack."
						+ "\nITGmania banners are 418x164.");
					ImGuiLayoutUtils.DrawRowEnum<SyncOffSetType>(true, "Sync", itgManiaPack,
						nameof(EditorItgManiaPack.SyncOffset), false,
						"(ITGmania only) The pack's sync offset. ITGmania intends to use this in the future as a mechanism for avoiding"
						+ " needing to apply the standard 9ms offset in all charts. This is currently unused but is recommended to specify"
						+ " a value which reflects the sync of the charts within the pack. ITGmania only supports 0ms and 9ms values."
						+ "\nNull: (Less Common) The pack's charts are synced to 0ms."
						+ "\nItg:  (More Common) The pack's charts are synced to 9ms.");

					var hasUnsavedChanges = itgManiaPack.HasUnsavedChanges();
					if (!hasUnsavedChanges)
						PushDisabled();
					var saveKeybind = UIControls.GetCommandString(Preferences.Instance.PreferencesKeyBinds.SavePackFile);
					if (ImGuiLayoutUtils.DrawRowButton("Save", "Save Pack File",
						    $"Save the {FileName} file."
						    + $"\nThe {FileName} file can also be saved with {saveKeybind}."
						    + (hasUnsavedChanges ? "\nThere are currently unsaved changes." : "")))
					{
						Pack.SaveItgManiaPack(false);
					}

					if (!hasUnsavedChanges)
						PopDisabled();

					if (ImGuiLayoutUtils.DrawRowButton("Delete", "Delete Pack File",
						    $"Delete the {FileName} file."))
					{
						UIModals.OpenModalTwoButtons(
							"Confirm Deletion",
							$"Are you sure you want to delete {FileName}? This operation cannot be undone.",
							"Cancel", () => { },
							"Delete", Pack.DeleteItgManiaPack);
					}
				}

				if (packCannotBeEdited)
					PopDisabled();

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();

			// Song table.
			var packSongs = Pack.GetSongs();
			if (packSongs != null && packSongs.Count > 0)
			{
				string fileToOpen = null;
				if (ImGui.BeginTable("Pack Songs", Enum.GetNames(typeof(Column)).Length,
					    ImGuiTableFlags.ScrollY
					    | ImGuiTableFlags.RowBg
					    | ImGuiTableFlags.Borders))
				{
					BeginTable(TableColumnData);

					var i = -1;
					foreach (var packSong in packSongs)
					{
						i++;
						var song = packSong?.GetSong();
						if (song == null)
							continue;

						ImGui.TableNextRow();

						// Title.
						ImGui.TableNextColumn();
						ImGui.TextUnformatted(song.Title ?? "");

						// Artist.
						ImGui.TableNextColumn();
						ImGui.TextUnformatted(song.Artist ?? "");

						// Credit.
						ImGui.TableNextColumn();
						song.Extras.TryGetExtra(SMCommon.TagCredit, out string credit, true);
						ImGui.TextUnformatted(credit ?? "");

						// Ratings.
						ImGui.TableNextColumn();
						ImGui.TextUnformatted(packSong.GetRatingsString() ?? "");

						// Open.
						ImGui.TableNextColumn();
						if (ImGui.Button($"Open##{i}"))
						{
							fileToOpen = packSong.GetFileInfo().FullName;
						}
					}

					ImGui.EndTable();
				}

				if (!string.IsNullOrEmpty(fileToOpen))
				{
					Editor.OpenSongFile(fileToOpen);
				}
			}

			if (!hasPack)
				PopDisabled();
		}

		ImGui.End();
	}

	private void BrowseBanner(IEditorPlatform platformInterface)
	{
		var itgManiaPack = Pack.GetItgManiaPack();
		if (itgManiaPack == null)
			return;
		var relativePath = platformInterface.BrowseFile(
			"Pack Banner",
			Pack.GetPackDirectory(),
			itgManiaPack.Banner,
			Utils.GetExtensionsForImages(), true);
		if (string.IsNullOrEmpty(relativePath))
			return;
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(itgManiaPack,
			nameof(EditorItgManiaPack.Banner), relativePath, false));
	}

	private void ClearBanner()
	{
		var itgManiaPack = Pack.GetItgManiaPack();
		if (itgManiaPack == null)
			return;
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(itgManiaPack,
			nameof(EditorItgManiaPack.Banner), "", false));
	}
}
