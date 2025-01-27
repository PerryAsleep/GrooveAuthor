using System;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
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
	private static readonly int TitleColumnWidth = UiScaled(40);
	private static readonly float DefaultWidth = UiScaled(508);
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

			if (ImGuiLayoutUtils.BeginTable("Pack Properties", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTitleAndTextWithButton("Name", packName, () => { _ = Pack.Refresh(); }, "Refresh",
					RefreshButtonWidth,
					"A pack's name is defined by the name of the folder which contains the pack's song folders.");

				ImGuiLayoutUtils.DrawRowTexture("Banner", Pack.GetBanner()?.GetTexture(), EmptyTextureBanner,
					"Stepmania infers a pack's banner from image assets in the pack's folder." +
					" It uses the first image asset it finds regardless of its size or dimensions, preferring the following extensions in order: "
					+ "png, jpg, jpeg, gif, bmp. Depending on the Stepmania theme banners have different recommended sizes."
					+ "\nITG banners are 418x164."
					+ "\nDDR banners are 512x160 or 256x80.");

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
						ImGui.Text(song.Title ?? "");

						// Artist.
						ImGui.TableNextColumn();
						ImGui.Text(song.Artist ?? "");

						// Credit.
						ImGui.TableNextColumn();
						song.Extras.TryGetExtra(SMCommon.TagCredit, out string credit, true);
						ImGui.Text(credit ?? "");

						// Ratings.
						ImGui.TableNextColumn();
						ImGui.Text(packSong.GetRatingsString() ?? "");

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
}
