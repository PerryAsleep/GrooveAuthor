using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Windows.Forms;
using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Path = Fumen.Path;

namespace StepManiaEditor
{
	public class UISongProperties
	{
		private static Dictionary<string, string> Cache = new Dictionary<string, string>();
		private static Dictionary<string, double> DoubleCache = new Dictionary<string, double>();

		private static EditorSong EditorSong;


		private readonly Editor Editor;
		private static ActionQueue ActionQueue;
		private static EmptyTexture EmptyTextureBanner;
		private static EmptyTexture EmptyTextureCDTitle;

		public UISongProperties(Editor editor, GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer, ActionQueue actionQueue)
		{
			EmptyTextureBanner = new EmptyTexture(graphicsDevice, imGuiRenderer, Utils.BannerWidth, Utils.BannerHeight);
			EmptyTextureCDTitle = new EmptyTexture(graphicsDevice, imGuiRenderer, Utils.CDTitleWidth, Utils.CDTitleHeight);
			ActionQueue = actionQueue;
			Editor = editor;
		}

		public void Draw(EditorSong editorSong)
		{
			EditorSong = editorSong;

			if (!Preferences.Instance.ShowSongPropertiesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Song Properties", ref Preferences.Instance.ShowSongPropertiesWindow, ImGuiWindowFlags.NoScrollbar);

			if (EditorSong == null)
				Utils.PushDisabled();

			if (EditorSong != null)
			{
				var (bound, pressed) = EditorSong.Banner.GetTexture().DrawButton();
				if (pressed || (!bound && EmptyTextureBanner.DrawButton()))
					BrowseBanner();

				ImGui.SameLine();

				(bound, pressed) = EditorSong.CDTitle.GetTexture().DrawButton();
				if (pressed || (!bound && EmptyTextureCDTitle.DrawButton()))
					BrowseCDTitle();
			}
			else
			{
				EmptyTextureBanner.DrawButton();
				ImGui.SameLine();
				EmptyTextureCDTitle.DrawButton();
			}

			if (ImGui.BeginTable("SongInfoTable", 2, ImGuiTableFlags.None))
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);

				DrawTextInputRowWithTransliteration("Title", EditorSong, nameof(EditorSong.Title), nameof(EditorSong.TitleTransliteration),
					"The title of the song.");
				DrawTextInputRowWithTransliteration("Subtitle", EditorSong, nameof(EditorSong.Subtitle), nameof(EditorSong.SubtitleTransliteration),
					"The subtitle of the song.");
				DrawTextInputRowWithTransliteration("Artist", EditorSong, nameof(EditorSong.Artist), nameof(EditorSong.ArtistTransliteration),
					"The artist who composed the song.");
				DrawTextInputRow("Genre", EditorSong, nameof(EditorSong.Genre),
					"The genre of the song.");
				DrawTextInputRow("Credit", EditorSong, nameof(EditorSong.Credit),
					"Who this file should be credited to.");
				DrawTextInputRow("Origin", EditorSong, nameof(EditorSong.Origin),
					"(Uncommon) What game this song originated from.");
			}
			ImGui.EndTable();

			ImGui.Separator();
			if (ImGui.BeginTable("SongAssetsTable", 2, ImGuiTableFlags.None))
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);

				DrawAutoFileBrowseRow("Banner", EditorSong?.Banner, nameof(EditorSong.Banner.Path), TryFindBestBanner, BrowseBanner, ClearBanner,
					"The banner graphic to display for this song when it is selected in the song wheel."
					+ "\nITG banners are 418x164."
					+ "\nDDR banners are 512x160 or 256x80.");

				DrawAutoFileBrowseRow("Background", EditorSong?.Background, nameof(EditorSong.Background.Path), TryFindBestBackground, BrowseBackground, ClearBackground,
					"The background graphic to display for this song while it is being played."
					+ "\nITG backgrounds are 640x480.");

				DrawFileBrowseRow("CD Title", EditorSong?.CDTitle, nameof(EditorSong.CDTitle.Path), BrowseCDTitle, ClearCDTitle,
					"The CD title graphic is most commonly used as a logo for the file author."
					+ "\nDimensions are arbitrary.");

				DrawFileBrowseRow("Jacket", EditorSong?.Jacket, nameof(EditorSong.CDTitle.Path), BrowseJacket, ClearJacket,
					"(Uncommon) Jacket graphic."
					+ "\nMeant for themes which display songs with jacket assets in the song wheel like DDR X2."
					+ "\nTypically square, but dimensions are arbitrary.");

				DrawFileBrowseRow("CD Image", EditorSong?.CDImage, nameof(EditorSong.CDTitle.Path), BrowseCDImage, ClearCDImage,
					"(Uncommon) CD image graphic."
					+ "\nOriginally meant to capture song select graphics which looked like CDs from the original DDR."
					+ "\nTypically square, but dimensions are arbitrary.");

				DrawFileBrowseRow("Disc Image", EditorSong?.DiscImage, nameof(EditorSong.CDTitle.Path), BrowseDiscImage, ClearDiscImage,
					"(Uncommon) Disc Image graphic."
					+ "\nOriginally meant to capture PIU song select graphics, which were discs in very old versions."
					+ "\nMore modern PIU uses rectangular banners, but dimensions are arbitrary.");

				DrawFileBrowseRow("Preview Video", EditorSong, nameof(EditorSong.PreviewVideoPath), BrowsePreviewVideoFile, ClearPreviewVideoFile,
					"(Uncommon) The preview video file." +
					"\nMeant for themes based on PIU where videos play on the song select screen.");
			}
			ImGui.EndTable();

			ImGui.Separator();
			if (ImGui.BeginTable("SongMusicTimingTable", 2, ImGuiTableFlags.None))
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);

				DrawFileBrowseRow("Music", EditorSong, nameof(EditorSong.MusicPath), BrowseMusicFile, ClearMusicFile,
					"The default audio file to use for all Charts for this Song." +
					"\nIn most cases all Charts use the same Music and it is defined here at the Song level.");
				
				DrawDragDoubleRow("Music Offset", EditorSong, nameof(EditorSong.MusicOffset),
					"The music offset from the start of the chart.");

				DrawFileBrowseRow("Preview File", EditorSong, nameof(EditorSong.MusicPreviewPath), BrowseMusicPreviewFile, ClearMusicPreviewFile,
					"(Uncommon) An audio file to use for a preview instead of playing a range from the music file.");

				// TODO: Better Preview Controls
				DrawDragDoubleRow("Preview Start", EditorSong, nameof(EditorSong.SampleStart),
					"Music preview start time.");
				DrawDragDoubleRow("Preview Length", EditorSong, nameof(EditorSong.SampleLength),
					"Music preview length.",
					0.0001f, "%.6f seconds", true, 0.0f);
				if (DrawButtonRow(null, Editor.IsPlayingPreview() ? "Stop Preview" : "Play Preview"))
					Editor.OnTogglePlayPreview();

				// DisplayTempo
				

				// TODO: Better LastSecondHint Controls.
				DrawDragDoubleRow("Last Second Hint", EditorSong, nameof(EditorSong.LastSecondHint),
					"The specified end time of the song." +
					"\nOptional. When not set StepMania will stop a chart shortly after the last note." +
					"\nUseful if you want the chart to continue after the last note." +
					"\nStepMania will ignore this value if it is less than or equal to 0.0.",
					0.0001f, "%.6f seconds", true, 0.0f);

			}
			ImGui.EndTable();

			ImGui.Separator();
			if (ImGui.BeginTable("SongMiscTable", 2, ImGuiTableFlags.None))
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);

				DrawEnumRow<Selectable>("Selectable", EditorSong, nameof(EditorSong.Selectable),
					"(Uncommon) Under what conditions this song should be selectable." +
					"\nMeant to capture stage requirements from DDR like extra stage and one more extra stage." +
					"\nLeave as YES if you are unsure what to use.");
				
				DrawFileBrowseRow("Lyrics", EditorSong, nameof(EditorSong.LyricsPath), BrowseLyricsFile, ClearLyricsFile,
					"(Uncommon) Lyrics file for displaying lyrics while the song plays.");
			}
			ImGui.EndTable();

			if (EditorSong == null)
				Utils.PopDisabled();

			ImGui.End();
		}

		private static void DrawRowTitleAndAdvanceColumn(string title)
		{
			ImGui.TableNextRow();

			ImGui.TableSetColumnIndex(0);
			if (!string.IsNullOrEmpty(title))
				ImGui.Text(title);

			ImGui.TableSetColumnIndex(1);
		}

		private static bool DrawButtonRow(string title, string buttonText)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return ImGui.Button(buttonText);
		}

		private static void DrawTextInputRowWithTransliteration(string title, object o, string fieldName, string transliterationFieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawTextInput(title, o, fieldName, ImGui.GetContentRegionAvail().X * 0.5f, help);
			ImGui.SameLine();
			DrawTextInput("Transliteration", o, transliterationFieldName, ImGui.GetContentRegionAvail().X,
				"Optional text to use when sorting by this value.\nStepMania sorts values lexicographically, preferring transliterations.");
		}

		private static void DrawTextInputRow(string title, object o, string fieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawTextInput(title, o, fieldName, ImGui.GetContentRegionAvail().X, help);
		}

		private static void DrawTextInput(string title, object o, string fieldName, float width, string help = null)
		{
			var cacheKey = $"{title}{fieldName}";
			if (!Cache.TryGetValue(cacheKey, out var cachedValue))
			{
				cachedValue = "";
				Cache[cacheKey] = cachedValue;
			}

			var hasHelp = !string.IsNullOrEmpty(help);
			var textWidth = hasHelp ? Math.Max(1.0f, width - Utils.HelpWidth - ImGui.GetStyle().ItemSpacing.X) : width;

			string value = "";
			var isField = false;
			if (o != null)
			{
				isField = o.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance) != null;
				if (isField)
					value = (string)o.GetType().GetField(fieldName).GetValue(o);
				else
					value = (string)o.GetType().GetProperty(fieldName).GetValue(o);
			}

			ImGui.SetNextItemWidth(textWidth);
			ImGui.InputTextWithHint($"##{title}{fieldName}", title, ref cachedValue, 256);
			Cache[cacheKey] = cachedValue;
			if (ImGui.IsItemDeactivatedAfterEdit() && o != null)
			{
				if (cachedValue != value)
				{
					if (isField)
						ActionQueue.Do(new ActionSetObjectField<string>(o, fieldName, cachedValue));
					else
						ActionQueue.Do(new ActionSetObjectProperty<string>(o, fieldName, cachedValue));
					value = cachedValue;
				}
			}
			if (!ImGui.IsItemActive())
				Cache[cacheKey] = value;

			if (hasHelp)
			{
				ImGui.SameLine();
				Utils.HelpMarker(help);
			}
		}

		private static void DrawDragDoubleRow(
			string title,
			object o,
			string fieldName,
			string help = null,
			float speed = 0.0001f,
			string format = "%.6f seconds",
			bool useMin = false,
			double min = 0.0,
			bool useMax = false,
			double max = 0.0)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawDragDouble(title, o, fieldName, ImGui.GetContentRegionAvail().X, help, speed, format, useMin, min, useMax, max);
		}

		private static void DrawDragDouble(
			string title,
			object o,
			string fieldName,
			float width,
			string help,
			float speed,
			string format,
			bool useMin = false,
			double min = 0.0,
			bool useMax = false,
			double max = 0.0)
		{
			var cacheKey = $"{title}{fieldName}";

			// Get the current double value.
			double value = 0.0;
			var isField = false;
			if (o != null)
			{
				isField = o.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance) != null;
				if (isField)
					value = (double)o.GetType().GetField(fieldName).GetValue(o);
				else
					value = (double)o.GetType().GetProperty(fieldName).GetValue(o);
			}
			var beforeValue = value;

			// Determine width.
			var hasHelp = !string.IsNullOrEmpty(help);
			var itemWidth = hasHelp ? Math.Max(1.0f, width - Utils.HelpWidth - ImGui.GetStyle().ItemSpacing.X) : width;
			ImGui.SetNextItemWidth(itemWidth);

			// Draw a scalar using the actual value, not the cached value.
			// We want to see the effect of changing this value immediately.
			// Do not however enqueue an EditorAction for this change yet.
			if (Utils.DragDouble(ref value, $"##{title}{fieldName}", speed, format, useMin, min, useMax, max))
			{
				if (isField)
					o.GetType().GetField(fieldName).SetValue(o, value);
				else
					o.GetType().GetProperty(fieldName).SetValue(o, value);
			}
			// At the moment of activating the drag control, record the current value so we can undo to it later.
			if (ImGui.IsItemActivated())
			{
				DoubleCache[cacheKey] = beforeValue;
			}
			// At the moment of releasing the drag control, enqueue an event so we can undo to the previous value.
			if (ImGui.IsItemDeactivatedAfterEdit() && o != null && value != DoubleCache[cacheKey])
			{
				if (isField)
					ActionQueue.Do(new ActionSetObjectField<double>(o, fieldName, value, DoubleCache[cacheKey]));
				else
					ActionQueue.Do(new ActionSetObjectProperty<double>(o, fieldName, value, DoubleCache[cacheKey]));
			}

			if (hasHelp)
			{
				var helpText = help +
				               "\nShift+drag for large adjustments." +
				               "\nAlt+drag for small adjustments.";
				ImGui.SameLine();
				Utils.HelpMarker(helpText);
			}
		}

		private static void DrawEnumRow<T>(string title, object o, string fieldName, string help = null) where T : Enum
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawEnum<T>(title, o, fieldName, ImGui.GetContentRegionAvail().X, help);
		}

		private static void DrawEnum<T>(string title, object o, string fieldName, float width, string help = null) where T : Enum
		{
			var hasHelp = !string.IsNullOrEmpty(help);
			var enumWidth = hasHelp ? Math.Max(1.0f, width - Utils.HelpWidth - ImGui.GetStyle().ItemSpacing.X) : width;

			var value = default(T);
			var isField = false;
			if (o != null)
			{
				isField = o.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance) != null;
				if (isField)
					value = (T)o.GetType().GetField(fieldName).GetValue(o);
				else
					value = (T)o.GetType().GetProperty(fieldName).GetValue(o);
			}

			ImGui.SetNextItemWidth(enumWidth);
			T newValue = value;
			if (Utils.ComboFromEnum("", ref newValue))
			{
				if (!newValue.Equals(value))
				{
					if (isField)
						ActionQueue.Do(new ActionSetObjectField<Enum>(o, fieldName, newValue));
					else
						ActionQueue.Do(new ActionSetObjectProperty<Enum>(o, fieldName, newValue));
				}
			};

			if (hasHelp)
			{
				ImGui.SameLine();
				Utils.HelpMarker(help);
			}
		}

		private static void DrawAutoFileBrowseRow(
			string title, object o, string fieldName, Action autoAction, Action browseAction, Action clearAction, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);

			Utils.PushDisabled();
			DrawTextInput(title, o, fieldName, Math.Max(1.0f, ImGui.GetContentRegionAvail().X - 120.0f - ImGui.GetStyle().ItemSpacing.X * 3), help);
			Utils.PopDisabled();

			ImGui.SameLine();
			if (ImGui.Button($"X##{title}{fieldName}", new Vector2(20.0f, 0.0f)))
			{
				clearAction();
			}

			ImGui.SameLine();
			if (ImGui.Button($"Auto##{title}{fieldName}", new Vector2(50.0f, 0.0f)))
			{
				autoAction();
			}

			ImGui.SameLine();
			if (ImGui.Button($"Browse##{title}{fieldName}", new Vector2(50.0f, 0.0f)))
			{
				browseAction();
			}
		}

		private static void DrawFileBrowseRow(
			string title, object o, string fieldName, Action browseAction, Action clearAction, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);

			Utils.PushDisabled();
			DrawTextInput(title, o, fieldName, Math.Max(1.0f, ImGui.GetContentRegionAvail().X - 70.0f - ImGui.GetStyle().ItemSpacing.X * 2), help);
			Utils.PopDisabled();

			ImGui.SameLine();
			if (ImGui.Button($"X##{title}{fieldName}", new Vector2(20.0f, 0.0f)))
			{
				clearAction();
			}

			ImGui.SameLine();
			if (ImGui.Button($"Browse##{title}{fieldName}", new Vector2(50.0f, 0.0f)))
			{
				browseAction();
			}
		}

		private static string BrowseFile(string name, string currentFileRelativePath, string filter)
		{
			string relativePath = null;
			using var openFileDialog = new OpenFileDialog();
			var initialDirectory = EditorSong.FileDirectory;
			if (!string.IsNullOrEmpty(currentFileRelativePath))
			{
				initialDirectory = Path.Combine(initialDirectory, currentFileRelativePath);
				initialDirectory = System.IO.Path.GetDirectoryName(initialDirectory);
			}

			openFileDialog.InitialDirectory = initialDirectory;
			openFileDialog.Filter = filter;
			openFileDialog.FilterIndex = 1;
			openFileDialog.Title = $"Open {name} File";

			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				var fileName = openFileDialog.FileName;
				relativePath = Path.GetRelativePath(EditorSong.FileDirectory, fileName);
			}

			return relativePath;
		}

		private static string TryFindBestAsset(
			int w,
			int h,
			string endsWith,
			string contains,
			bool includeVideoFiles,
			bool preferBiggestFile,
			bool preferAnyVideo)
		{
			try
			{
				var files = Directory.GetFiles(EditorSong.FileDirectory);

				// Check for files which match StepMania's expected names.
				var assetFiles = new List<string>();
				var videoFiles = new List<string>();
				foreach (var file in files)
				{
					try
					{
						var fileNameNoExtension = System.IO.Path.GetFileNameWithoutExtension(file).ToLower();
						var extension = System.IO.Path.GetExtension(file);
						if (extension.StartsWith('.'))
							extension = extension.Substring(1);
						var imageFile = Utils.ExpectedImageFormats.Contains(extension);
						var videoFile = Utils.ExpectedVideoFormats.Contains(extension);

						if (!includeVideoFiles && !imageFile)
							continue;
						if (includeVideoFiles && !imageFile && !videoFile)
							continue;

						assetFiles.Add(file);
						if (videoFile)
							videoFiles.Add(file);
						if (fileNameNoExtension.EndsWith(endsWith) || fileNameNoExtension.Contains(contains))
						{
							return file;
						}
					}
					catch (Exception)
					{
						// Ignored.
					}
				}

				// Try to match expected dimensions.
				var filesWithExpectedAspectRatio = new List<string>();
				var expectedAspectRatio = (float)w / h;
				string biggestFile = null;
				int biggestSize = 0;
				foreach (var file in assetFiles)
				{
					try
					{
						using Stream stream = File.OpenRead(file);
						using var sourceImage = System.Drawing.Image.FromStream(stream, false, false);

						if ((float)sourceImage.Width / sourceImage.Height - expectedAspectRatio < 0.001f)
							filesWithExpectedAspectRatio.Add(file);

						var size = sourceImage.Width * sourceImage.Height;
						if (size > biggestSize && sourceImage.Width >= w && sourceImage.Height >= h)
						{
							biggestSize = size;
							biggestFile = file;
						}

						if (sourceImage.Width == w && sourceImage.Height == h)
						{
							return file;
						}
					}
					catch (Exception)
					{
						// Ignored.
					}
				}

				// Try to match expected aspect ratio.
				if (filesWithExpectedAspectRatio.Count > 0)
					return filesWithExpectedAspectRatio[0];

				// If configured to prefer the biggest file and a file exists that is at
				// least as expected dimensions, use that.
				if (preferBiggestFile && !string.IsNullOrEmpty(biggestFile))
					return biggestFile;

				// If configured to prefer video files and any video file exists, use that.
				if (preferAnyVideo && videoFiles.Count > 0)
					return videoFiles[0];
			}
			catch (Exception)
			{
				// Ignored.
			}

			return null;
		}

		private static void TryFindBestBanner()
		{
			var bestFile = TryFindBestAsset(Utils.BannerWidth, Utils.BannerHeight, " bn", "banner", false, false, false);
			if (!string.IsNullOrEmpty(bestFile))
			{
				var relativePath = Path.GetRelativePath(EditorSong.FileDirectory, bestFile);
				if (relativePath != EditorSong.Banner.Path)
					ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), relativePath));
				else
					Logger.Info($"Song banner is already set to the best automatic choice: '{relativePath}'.");
			}
			else
			{
				Logger.Info("Could not automatically determine the song banner.");
			}
		}

		private static void BrowseBanner()
		{
			var relativePath = BrowseFile(
				"Banner",
				EditorSong.Banner.Path,
				Utils.FileOpenFilterForImages("Banner", true));
			if (relativePath != null && relativePath != EditorSong.Banner.Path)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), relativePath));
		}

		private static void ClearBanner()
		{
			if (!string.IsNullOrEmpty(EditorSong.Banner.Path))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), ""));
		}

		private static void TryFindBestBackground()
		{
			var bestFile = TryFindBestAsset(Utils.BannerWidth, Utils.BannerHeight, "bg", "background", true, true, true);
			if (!string.IsNullOrEmpty(bestFile))
			{
				var relativePath = Path.GetRelativePath(EditorSong.FileDirectory, bestFile);
				if (relativePath != EditorSong.Background.Path)
					ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Background, nameof(EditorSong.Background.Path), relativePath));
				else
					Logger.Info($"Song background is already set to the best automatic choice: '{relativePath}'.");
			}
			else
			{
				Logger.Info("Could not automatically determine the song background.");
			}
		}

		private static void BrowseBackground()
		{
			var relativePath = BrowseFile(
				"Background",
				EditorSong.Banner.Path,
				Utils.FileOpenFilterForImagesAndVideos("Background", true));
			if (relativePath != null && relativePath != EditorSong.Background.Path)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Background, nameof(EditorSong.Background.Path), relativePath));
		}

		private static void ClearBackground()
		{
			if (!string.IsNullOrEmpty(EditorSong.Background.Path))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Background, nameof(EditorSong.Background.Path), ""));
		}

		private static void BrowseCDTitle()
		{
			var relativePath = BrowseFile(
				"CD Title",
				EditorSong.CDTitle.Path,
				Utils.FileOpenFilterForImages("CD Title", true));
			if (relativePath != null && relativePath != EditorSong.CDTitle.Path)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.CDTitle, nameof(EditorSong.CDTitle.Path), relativePath));
		}

		private static void ClearCDTitle()
		{
			if (!string.IsNullOrEmpty(EditorSong.CDTitle.Path))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.CDTitle, nameof(EditorSong.CDTitle.Path), ""));
		}

		private static void BrowseJacket()
		{
			var relativePath = BrowseFile(
				"Jacket",
				EditorSong.Jacket.Path,
				Utils.FileOpenFilterForImages("Jacket", true));
			if (relativePath != null && relativePath != EditorSong.Jacket.Path)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Jacket, nameof(EditorSong.Jacket.Path), relativePath));
		}

		private static void ClearJacket()
		{
			if (!string.IsNullOrEmpty(EditorSong.Jacket.Path))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.Jacket, nameof(EditorSong.Jacket.Path), ""));
		}

		private static void BrowseCDImage()
		{
			var relativePath = BrowseFile(
				"CD Image",
				EditorSong.CDImage.Path,
				Utils.FileOpenFilterForImages("CD Image", true));
			if (relativePath != null && relativePath != EditorSong.CDImage.Path)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.CDImage, nameof(EditorSong.CDImage.Path), relativePath));
		}

		private static void ClearCDImage()
		{
			if (!string.IsNullOrEmpty(EditorSong.CDImage.Path))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.CDImage, nameof(EditorSong.CDImage.Path), ""));
		}

		private static void BrowseDiscImage()
		{
			var relativePath = BrowseFile(
				"Disc Image",
				EditorSong.DiscImage.Path,
				Utils.FileOpenFilterForImages("Disc Image", true));
			if (relativePath != null && relativePath != EditorSong.DiscImage.Path)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.DiscImage, nameof(EditorSong.DiscImage.Path), relativePath));
		}

		private static void ClearDiscImage()
		{
			if (!string.IsNullOrEmpty(EditorSong.DiscImage.Path))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong.DiscImage, nameof(EditorSong.DiscImage.Path), ""));
		}

		private static void BrowseMusicFile()
		{
			var relativePath = BrowseFile(
				"Music",
				EditorSong.MusicPath,
				Utils.FileOpenFilterForAudio("Music", true));
			if (relativePath != null && relativePath != EditorSong.MusicPath)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong, nameof(EditorSong.MusicPath), relativePath));
		}

		private static void ClearMusicFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.MusicPath))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong, nameof(EditorSong.MusicPath), ""));
		}

		private static void BrowseMusicPreviewFile()
		{
			var relativePath = BrowseFile(
				"Music Preview",
				EditorSong.MusicPreviewPath,
				Utils.FileOpenFilterForAudio("Music Preview", true));
			if (relativePath != null && relativePath != EditorSong.MusicPreviewPath)
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong, nameof(EditorSong.MusicPreviewPath), relativePath));
		}

		private static void ClearMusicPreviewFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.MusicPreviewPath))
				ActionQueue.Do(new ActionSetObjectProperty<string>(EditorSong, nameof(EditorSong.MusicPreviewPath), ""));
		}

		private static void BrowseLyricsFile()
		{
			var relativePath = BrowseFile(
				"Lyrics",
				EditorSong.LyricsPath,
				Utils.FileOpenFilterForLyrics("Lyrics", true));
			if (relativePath != null && relativePath != EditorSong.LyricsPath)
				ActionQueue.Do(new ActionSetObjectField<string>(EditorSong, nameof(EditorSong.LyricsPath), relativePath));
		}

		private static void ClearLyricsFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.LyricsPath))
				ActionQueue.Do(new ActionSetObjectField<string>(EditorSong, nameof(EditorSong.LyricsPath), ""));
		}

		private static void BrowsePreviewVideoFile()
		{
			var relativePath = BrowseFile(
				"Preview Video",
				EditorSong.PreviewVideoPath,
				Utils.FileOpenFilterForVideo("Preview Video", true));
			if (relativePath != null && relativePath != EditorSong.PreviewVideoPath)
				ActionQueue.Do(new ActionSetObjectField<string>(EditorSong, nameof(EditorSong.PreviewVideoPath), relativePath));
		}

		private static void ClearPreviewVideoFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.PreviewVideoPath))
				ActionQueue.Do(new ActionSetObjectField<string>(EditorSong, nameof(EditorSong.PreviewVideoPath), ""));
		}
	}
}
