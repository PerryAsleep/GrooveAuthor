using System;
using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
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

	private static readonly ColumnData[] TableColumnData;
	private static readonly float DefaultWidth = UiScaled(513);
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

	public void Init(Editor editor, EditorPack pack)
	{
		Editor = editor;
		Pack = pack;
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

		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowPackPropertiesWindow, DefaultWidth))
		{
			// Pack title and refresh button.
			var titleWidth = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X - RefreshButtonWidth;
			Text(Pack.GetPackName() ?? "", titleWidth);
			ImGui.SameLine();
			if (ImGui.Button("Refresh", new Vector2(RefreshButtonWidth, 0.0f)))
			{
				_ = Pack.Refresh();
			}

			// Song table.
			var packSongs = Pack.GetSongs();
			if (packSongs != null && packSongs.Count > 0)
			{
				ImGui.Separator();
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
		}

		ImGui.End();
	}
}
