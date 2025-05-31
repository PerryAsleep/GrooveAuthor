using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditorTextureGenerator;

/// <summary>
/// Application to generate a StaticTextureAtlas of all images used by StepManiaEditor.
/// Internally implemented as a MonoGame Game in order to leverage Texture functionality.
/// </summary>
internal class Program
{
	private static void Main()
	{
		using var game = new TextureGeneratorGame();
		game.Run();
	}
}

/// <summary>
/// MonoGame Game that handles making the StaticTextureAtlas.
/// </summary>
internal class TextureGeneratorGame : Game
{
	private const int ArrowTextureDimension = 128;

	private const string ContentDir = @"..\..\..\..\StepManiaEditor\Content\";
	private const string EditorDir = @"..\..\..\..\StepManiaEditor\";
	private const string InputArrows = "arrows.png";
	private const string InputIcons = "icons.png";
	private const string OutputImage = "atlas.png";
	private const string OutputAtlas = "atlas.json";
	private const int OutputAtlasWidth = 1280;
	private const int OutputAtlasHeight = 1280;
	private const int MarkerTextureWidth = 128;

	// Selected texture variant parameters.
	private const float SelectionColorMultiplier = 2.0f;
	private const int SelectionRimSize = 8;
	private const int SelectionMaskDimension = SelectionRimSize * 2 + 1; // +1 to ensure odd number so the mask is centered.
	private const uint SelectionHighlightColorBlack = 0xFF000000;
	private const uint SelectionHighlightColorWhite = 0xFFFFFFFF;
	private readonly float[] SelectionDistances = new float[SelectionMaskDimension * SelectionMaskDimension];

	private string RelativeContentDir;
	private string RelativeEditorDir;

	private GraphicsDeviceManager Graphics;
	private DynamicTextureAtlas Atlas;
	private bool Done;

	public TextureGeneratorGame()
	{
		InitializeLogger();

		Logger.Info("Constructing.");

		InitializeDirectories();
		InitializeGraphics();
		InitializeSelectionDistances();
	}

	private void InitializeLogger()
	{
		Logger.StartUp(new Logger.Config
		{
			WriteToConsole = true,
			WriteToFile = false,
		});
	}

	private void InitializeDirectories()
	{
		try
		{
			var assembly = System.Reflection.Assembly.GetEntryAssembly();
			if (assembly == null)
			{
				throw new Exception("Null assembly");
			}

			var programPath = assembly.Location;
			var programDir = System.IO.Path.GetDirectoryName(programPath);
			if (programDir == null)
			{
				throw new Exception("Could not determine application directory.");
			}

			RelativeContentDir = System.IO.Path.Combine(programDir, ContentDir);
			RelativeEditorDir = System.IO.Path.Combine(programDir, EditorDir);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load initialize directories: {e}");
		}
	}

	private void InitializeGraphics()
	{
		Graphics = new GraphicsDeviceManager(this);
		Graphics.GraphicsProfile = GraphicsProfile.HiDef;
	}

	private void InitializeSelectionDistances()
	{
		for (var y = 0; y < SelectionMaskDimension; y++)
		{
			for (var x = 0; x < SelectionMaskDimension; x++)
			{
				var i = y * SelectionMaskDimension + x;
				SelectionDistances[i] = (float)Math.Sqrt((x - SelectionRimSize) * (x - SelectionRimSize) +
				                                         (y - SelectionRimSize) * (y - SelectionRimSize));
			}
		}
	}

	protected override void Initialize()
	{
		Logger.Info("Initializing.");
		Atlas = new DynamicTextureAtlas(GraphicsDevice, OutputAtlasWidth, OutputAtlasHeight, 1);
		base.Initialize();
	}

	protected override void LoadContent()
	{
		Logger.Info("Loading Content.");
		GenerateTextures();
		base.LoadContent();
	}

	protected override void Update(GameTime gameTime)
	{
		if (Done)
		{
			Exit();
		}

		base.Update(gameTime);
	}

	private void GenerateTextures()
	{
		AddArrowTextures();
		AddIconTextures();
		AddMiscProgrammaticTextures();
		Atlas.Update();
		Save();

		Logger.Info("Done.");
		Done = true;
	}

	private void Save()
	{
		// Save all sub-texture locations.
		var locations = Atlas.GetAllSubTextureLocations(true);
		var locationsForSaving = new Dictionary<string, List<int>>();
		foreach (var (subTextureId, subTextureRect) in locations)
		{
			locationsForSaving.Add(subTextureId, [
				subTextureRect.X, subTextureRect.Y, subTextureRect.Width, subTextureRect.Height,
			]);
		}

		JsonSerializerOptions serializerOptions = new()
		{
			Converters =
			{
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
			},
			ReadCommentHandling = JsonCommentHandling.Skip,
			IncludeFields = true,
		};
		Logger.Info($"Saving {OutputAtlas}.");
		try
		{
			var jsonString = JsonSerializer.Serialize(locationsForSaving, serializerOptions);
			File.WriteAllText(GetEditorPath(OutputAtlas), jsonString);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to write {OutputAtlas}: {e}");
		}

		// Save image.
		var atlasTexture = Atlas.GetTexture();
		var w = atlasTexture.Width;
		var h = atlasTexture.Height;
		var atlasPixelData = new uint[w * h];
		atlasTexture.GetData(atlasPixelData);
		using var bitmap = new Bitmap(w, h);
		for (var x = 0; x < w; x++)
		{
			for (var y = 0; y < h; y++)
			{
				var uintColor = atlasPixelData[x + y * w];
				var (r, g, b, a) = Fumen.ColorUtils.ToChannels(uintColor);
				var color = System.Drawing.Color.FromArgb(a, r, g, b);
				bitmap.SetPixel(x, y, color);
			}
		}

		Logger.Info($"Saving {OutputImage}.");
		try
		{
			var outputImageFileName = GetContentPath(OutputImage);
			bitmap.Save(outputImageFileName, ImageFormat.Png);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to write {OutputImage}: {e}");
		}
	}

	private void AddArrowTextures()
	{
		Logger.Info($"Adding images from {InputArrows}.");
		var arrowsTexture = LoadTexture(InputArrows);
		if (arrowsTexture == null)
			return;
		ProcessArrows(arrowsTexture);
		Logger.Info($"Added images from {InputArrows}.");
	}

	private void AddIconTextures()
	{
		Logger.Info($"Adding images from {InputIcons}.");
		var iconsTexture = LoadTexture(InputIcons);
		if (iconsTexture == null)
			return;

		string[] iconSubImageIds =
		[
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
			"icon-dance-center",
			"icon-dance-up-left",
			"icon-dance-up-right",
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
			"icon-dance-left-dim",
			"icon-dance-down-dim",
			"icon-dance-up-dim",
			"icon-dance-right-dim",
			"icon-dance-center-dim",
			"icon-dance-up-left-dim",
			"icon-dance-up-right-dim",
			"icon-pump-down-left-dim",
			"icon-pump-up-left-dim",
			"icon-pump-center-dim",
			"icon-pump-up-right-dim",
			"icon-pump-down-right-dim",
		];
		ProcessGridOfImages(iconsTexture, iconSubImageIds, 12, 0, 0, 16, 16, 0, 0x00FFFFFF);
		Logger.Info($"Added images from {InputIcons}.");
	}

	private void ProcessArrows(Texture2D source)
	{
		var x = 0;
		var sourceColorData = new uint[source.Width * source.Height];
		source.GetData(sourceColorData);

		void AddArrowTextureSet(string name, bool hasHoldStarts, bool hasHoldEnds, int startHeight, int endHeight)
		{
			ProcessArrowTextureSet(sourceColorData, source.Width, name, x, hasHoldStarts, hasHoldEnds, startHeight, endHeight);
			x += ArrowTextureDimension;
		}

		// Process hold end caps trimmed to their exact height.
		// This is done because we use the texture size for determining if a click lands on a hold.
		// Most arrows are big enough in the 128x128 frames, but the hold ends are much shorter.
		// Add padding to account for programmatically generated selection rims.

		AddArrowTextureSet("itg-down", false, true, 0, 57 + SelectionRimSize);
		AddArrowTextureSet("itg-solo", true, true, 28, 45 + SelectionRimSize);
		AddArrowTextureSet("itg-center", false, true, 0, 43 + SelectionRimSize);
		AddArrowTextureSet("piu-diagonal", false, false, 0, 0);
		AddArrowTextureSet("piu-center", false, false, 0, 0);

		// Mines.
		ProcessGridOfImages(source, ["mine-rim", "mine-fill"], 1, x, ArrowTextureDimension, ArrowTextureDimension,
			ArrowTextureDimension, 0, 0x00FFFFFF);

		// Misc Indicators.
		ProcessGridOfImages(source, ["fake-marker", "lift-marker", "player-marker-fill", "player-marker-rim"], 2, 648, 8, 40, 40,
			8, 0x00FFFFFF);
	}

	private void ProcessArrowTextureSet(
		uint[] sourceColorData,
		int sourceWidth,
		string baseIdentifier,
		int x,
		bool hasHoldStarts,
		bool hasHoldEnds,
		int startHeight,
		int endHeight)
	{
		var y = 0;

		void AddArrowTexture(string name, int yOffset = 0, int height = ArrowTextureDimension, uint transparentColor = 0x00000000)
		{
			var subTexture = CopySubTexture(sourceColorData, sourceWidth, x, y + yOffset, ArrowTextureDimension, height,
				transparentColor);
			var paddingMode = TextureAtlas.PaddingMode.Extend;
			if (name.Contains("hold-fill")
			    || name.Contains("roll-fill"))
			{
				paddingMode = TextureAtlas.PaddingMode.Wrap;
			}

			Atlas.AddSubTexture(name, subTexture, true, paddingMode);
			y += ArrowTextureDimension;

			if (!name.Contains("receptor")
			    && !name.Contains("glow")
			    && !name.Contains("fill"))
			{
				var selectedSubTexture = GenerateSelectedTexture(subTexture);
				Atlas.AddSubTexture($"{name}-selected", selectedSubTexture, true);
			}
		}

		AddArrowTexture($"{baseIdentifier}-receptor");
		AddArrowTexture($"{baseIdentifier}-receptor-held", 0, ArrowTextureDimension, 0x00FFFFFF);
		AddArrowTexture($"{baseIdentifier}-receptor-glow", 0, ArrowTextureDimension, 0x00FFFFFF);
		AddArrowTexture($"{baseIdentifier}-fill", 0, ArrowTextureDimension, 0x00FFFFFF);
		AddArrowTexture($"{baseIdentifier}-rim", 0, ArrowTextureDimension, 0x00FFFFFF);
		AddArrowTexture($"{baseIdentifier}-hold-rim", 0, ArrowTextureDimension, 0x00FFFFFF);
		AddArrowTexture($"{baseIdentifier}-hold-fill", 0, ArrowTextureDimension, 0x00FFFFFF);
		AddArrowTexture($"{baseIdentifier}-roll-fill", 0, ArrowTextureDimension, 0x00FFFFFF);
		if (hasHoldEnds)
		{
			AddArrowTexture($"{baseIdentifier}-hold-end-rim", 0, endHeight, 0x00FFFFFF);
			AddArrowTexture($"{baseIdentifier}-hold-end-fill", 0, endHeight, 0x00FFFFFF);
			AddArrowTexture($"{baseIdentifier}-roll-end-fill", 0, endHeight, 0x00FFFFFF);
		}

		if (hasHoldStarts)
		{
			var yOffset = (ArrowTextureDimension >> 1) - startHeight;
			AddArrowTexture($"{baseIdentifier}-hold-start-rim", yOffset, startHeight, 0x00FFFFFF);
			AddArrowTexture($"{baseIdentifier}-hold-start-fill", yOffset, startHeight, 0x00FFFFFF);
			AddArrowTexture($"{baseIdentifier}-roll-start-fill", yOffset, startHeight, 0x00FFFFFF);
		}
	}

	private void ProcessGridOfImages(Texture2D source, string[] identifiers, int numCols, int startX, int startY, int w, int h,
		int padding, uint transparentColor)
	{
		var sourceColorData = new uint[source.Width * source.Height];
		source.GetData(sourceColorData);

		var i = 0;
		var sourceW = source.Width;
		foreach (var identifier in identifiers)
		{
			if (!string.IsNullOrEmpty(identifier))
			{
				var generateMips = !identifier.Contains("icon");
				var generateSelectedTexture =
					!(identifier.Contains("receptor")
					  || identifier.Contains("snap")
					  || identifier.Contains("glow")
					  || identifier.Contains("icon")
					  || identifier.Contains("fake")
					  || identifier.Contains("lift")
					  || identifier.Contains("fill")
					  || identifier.Contains("player-marker"));

				// Copy the sub-texture out of the source texture.
				var subTexture = CopySubTexture(
					sourceColorData,
					source.Width,
					startX + (w + padding) * (i % numCols),
					startY + (h + padding) * (i / numCols),
					w,
					h,
					transparentColor);

				Atlas.AddSubTexture(identifier, subTexture, generateMips);

				if (generateSelectedTexture)
				{
					var selectedSubTexture = GenerateSelectedTexture(subTexture);
					Atlas.AddSubTexture($"{identifier}-selected", selectedSubTexture, generateMips);
				}
			}

			i++;
		}
	}

	private void AddMiscProgrammaticTextures()
	{
		// Generate and add measure marker texture.
		var measureMarkerTexture = new Texture2D(GraphicsDevice, MarkerTextureWidth, 1);
		var textureData = new uint[MarkerTextureWidth];
		for (var i = 0; i < MarkerTextureWidth; i++)
			textureData[i] = 0xFFFFFFFF;
		measureMarkerTexture.SetData(textureData);
		Atlas.AddSubTexture("measure-marker", measureMarkerTexture, true);

		// Generate and add beat marker texture.
		var beatMarkerTexture = new Texture2D(GraphicsDevice, MarkerTextureWidth, 1);
		for (var i = 0; i < MarkerTextureWidth; i++)
			textureData[i] = 0xFF7F7F7F;
		beatMarkerTexture.SetData(textureData);
		Atlas.AddSubTexture("beat-marker", beatMarkerTexture, true);

		// Add focused chart boundary.
		var focusedChartBoundaryTexture = new Texture2D(GraphicsDevice, 1, 1);
		textureData = new uint[1];
		textureData[0] = 0xFFFFFFFF;
		focusedChartBoundaryTexture.SetData(textureData);
		Atlas.AddSubTexture("focused-chart-boundary", focusedChartBoundaryTexture, true);

		// Add unfocused chart boundary.
		var unfocusedChartBoundaryTexture = new Texture2D(GraphicsDevice, 1, 1);
		textureData = new uint[1];
		textureData[0] = 0xFF7F7F7F;
		unfocusedChartBoundaryTexture.SetData(textureData);
		Atlas.AddSubTexture("unfocused-chart-boundary", unfocusedChartBoundaryTexture, true);

		// Generate and add generic region rect texture.
		var regionRectTexture = new Texture2D(GraphicsDevice, 1, 1);
		textureData = new uint[1];
		textureData[0] = 0xFFFFFFFF;
		regionRectTexture.SetData(textureData);
		Atlas.AddSubTexture("region-rect", regionRectTexture, false);

		// Add the dark bg texture.
		var darkBgTexture = new Texture2D(GraphicsDevice, 1, 1);
		textureData = new uint[1];
		textureData[0] = 0xFFFFFFFF;
		darkBgTexture.SetData(textureData);
		Atlas.AddSubTexture("dark-bg", darkBgTexture, false);
	}

	private Texture2D LoadTexture(string fileName)
	{
		var filePath = GetContentPath(fileName);
		if (string.IsNullOrEmpty(filePath))
			return null;
		try
		{
			using var fileStream = File.OpenRead(filePath);
			var texture = Texture2D.FromStream(GraphicsDevice, fileStream);
			return texture;
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to create texture from {fileName}: {e}");
		}

		return null;
	}

	private Texture2D CopySubTexture(uint[] sourceColorData, int sourceW, int x, int y, int w, int h,
		uint transparentColor = 0x00000000)
	{
		var subTexture = new Texture2D(GraphicsDevice, w, h);
		var subTextureData = new uint[w * h];
		var sourceX = x;
		for (var subX = 0; subX < w; subX++, sourceX++)
		{
			var sourceY = y;
			for (var subY = 0; subY < h; subY++, sourceY++)
			{
				subTextureData[subX + subY * w] = sourceColorData[sourceX + sourceY * sourceW];

				// When copying the sub-texture, if we encounter fully transparent pixels then use the
				// specified transparentColor. This allows us to have per-sub-texture transparent colors.
				// This is useful when some images have black rims and some have white rims, and we want
				// Monogame to blend with a color appropriate for that rim when it draws the image scaled.
				if (((subTextureData[subX + subY * w] >> 24) & 0xFF) == 0)
				{
					subTextureData[subX + subY * w] = transparentColor;
				}
			}
		}

		subTexture.SetData(subTextureData);
		return subTexture;
	}

	private string GetContentPath(string fileName)
	{
		return System.IO.Path.Combine(RelativeContentDir, fileName);
	}

	private string GetEditorPath(string fileName)
	{
		return System.IO.Path.Combine(RelativeEditorDir, fileName);
	}

	/// <summary>
	/// Creates a new texture that looks like a selected variation of the given texture.
	/// In practice this brightens the given texture and adds a highlighted rim around it.
	/// The created texture will have the same dimensions as the input texture. It is assumed
	/// that there will be enough padding built into the source texture such that the rim will
	/// fit without being cut off.
	/// </summary>
	/// <param name="input">The texture to generate a selected variant of.</param>
	/// <returns>New texture.</returns>
	private Texture2D GenerateSelectedTexture(Texture2D input)
	{
		var w = input.Width;
		var h = input.Height;

		var newTexture = new Texture2D(GraphicsDevice, w, h);

		var n = w * h;
		var colorData = new uint[n];
		var newColorData = new uint[n];

		input.GetData(colorData);

		// Determine the inner transparent pixels via flood fill.
		// This happens for rims.
		var innerTransparentPixels = new bool[n];
		FloodFill(w, h, colorData, innerTransparentPixels, w >> 1, h >> 1);

		for (var y = 0; y < h; y++)
		{
			for (var x = 0; x < w; x++)
			{
				var i = y * w + x;
				var color = colorData[i];

				// Fully opaque: Copy the brightened source color.
				if (color >> 24 == 0x000000FF)
				{
					newColorData[i] = Fumen.ColorUtils.ColorRGBAMultiply(colorData[i], SelectionColorMultiplier);
				}

				// Partially transparent: Blend the brightened source color over the highlight bg color.
				else if (color >> 24 != 0)
				{
					var alpha = (float)(color >> 24) / byte.MaxValue;
					var sourceColor = Fumen.ColorUtils.ColorRGBAMultiply(colorData[i], SelectionColorMultiplier);
					newColorData[i] = Fumen.ColorUtils.ColorRGBAInterpolateBGR(sourceColor, SelectionHighlightColorWhite, alpha);

					// If this partially transparent pixel is part of the inner transparent region then alpha blend it.
					if (innerTransparentPixels[i])
					{
						newColorData[i] = (newColorData[i] & 0x00FFFFFF) | ((uint)(alpha * byte.MaxValue) << 24);
					}
				}

				// Fully transparent: Generate a highlight bg color rim around the opaque area.
				else
				{
					// If this is an inner transparent area then do not add a rim.
					if (innerTransparentPixels[i])
					{
						// Ensure the inner pixel color is white even though it is transparent.
						// This is to ensure that when monogame draws the texture scaled down and blends
						// the pixels it doesn't blend our desired white rim with black.
						newColorData[i] = 0x00FFFFFF;
						continue;
					}

					// Determine the largest source alpha in the mask centered on this pixel.
					var alpha = 0.0f;
					var distance = (float)SelectionMaskDimension;
					for (int sy = y - SelectionRimSize, my = 0; sy <= y + SelectionRimSize; sy++, my++)
					{
						for (int sx = x - SelectionRimSize, mx = 0; sx <= x + SelectionRimSize; sx++, mx++)
						{
							var mi = my * SelectionMaskDimension + mx;
							if (SelectionDistances[mi] > SelectionRimSize || sx < 0 || sy < 0 || sx >= w || sy >= h)
								continue;
							var sColor = colorData[sy * w + sx];
							var sAlpha = (float)(sColor >> 24) / byte.MaxValue;
							if (sAlpha > alpha)
								alpha = sAlpha;

							if (sAlpha > 0.0f)
							{
								// Adjust the distance based on the alpha value. More transparent values
								// appear further away than more opaque values.
								var adjustedDistance = Math.Max(0.0f, SelectionDistances[mi] - 1.0f + (1.0f - sAlpha));
								if (adjustedDistance < distance)
									distance = adjustedDistance;
							}
						}
					}

					// Use the distance to blend between two colors.
					// The logic below adds a white highlight with a black line through it.
					// This looks decent with the current arrow art that has white outlines as the
					// white from the arrow art blends into the white from the selection. The
					// cutoffs in the blending logic below are taking into account that the arrows
					// have a 2 pixel white rim in order to center the black line.
					var percent = Math.Clamp(distance / SelectionRimSize, 0.0f, 1.0f);
					uint rimColor;
					const float firstCutoff = 0.10f;
					const float secondCutoff = 0.25f;
					const float thirdCutoff = 0.60f;

					// Solid white.
					if (percent < firstCutoff)
					{
						rimColor = SelectionHighlightColorWhite;
					}
					// Blend from white to black.
					else if (percent < secondCutoff)
					{
						percent = (percent - firstCutoff) / (secondCutoff - firstCutoff);
						rimColor = Fumen.ColorUtils.ColorRGBAInterpolateBGR(SelectionHighlightColorWhite,
							SelectionHighlightColorBlack, percent);
					}
					// Blend from black to white.
					else if (percent < thirdCutoff)
					{
						percent = (percent - secondCutoff) / (thirdCutoff - secondCutoff);
						rimColor = Fumen.ColorUtils.ColorRGBAInterpolateBGR(SelectionHighlightColorBlack,
							SelectionHighlightColorWhite, percent);
					}
					// Solid white.
					else
					{
						rimColor = SelectionHighlightColorWhite;
					}

					// Apply that alpha to the selection highlight color and use that.
					newColorData[i] = (rimColor & 0x00FFFFFF) | ((uint)(alpha * byte.MaxValue) << 24);
				}
			}
		}

		newTexture.SetData(newColorData);
		return newTexture;
	}

	private static void FloodFill(int w, int h, uint[] colorData, bool[] filled, int x, int y)
	{
		// Many of the images we deal with are thin rims which aren't actually have fully
		// opaque on some rounded edges. Use a lower cutoff value to capture the flood
		// fill region.
		const uint alphaCutoff = 0x80;

		var toVisit = new HashSet<int> { Hash(x, y) };
		var visited = new HashSet<int>();
		while (toVisit.Count > 0)
		{
			var v = PopAny(toVisit);
			visited.Add(v);
			if (colorData[v] >> 24 >= alphaCutoff)
				continue;
			filled[v] = true;

			(x, y) = UnHash(v);

			if (x - 1 >= 0)
			{
				var hash = Hash(x - 1, y);
				if (!visited.Contains(hash))
					toVisit.Add(hash);
			}

			if (x + 1 < w)
			{
				var hash = Hash(x + 1, y);
				if (!visited.Contains(hash))
					toVisit.Add(hash);
			}

			if (y - 1 >= 0)
			{
				var hash = Hash(x, y - 1);
				if (!visited.Contains(hash))
					toVisit.Add(hash);
			}

			if (y + 1 < h)
			{
				var hash = Hash(x, y + 1);
				if (!visited.Contains(hash))
					toVisit.Add(hash);
			}
		}

		return;

		int PopAny(HashSet<int> set)
		{
			using var enumerator = set.GetEnumerator();
			if (enumerator.MoveNext())
			{
				var val = enumerator.Current;
				set.Remove(val);
				return val;
			}

			return 0;
		}

		(int, int) UnHash(int i)
		{
			var iy = i / w;
			var ix = i - iy * w;
			return (ix, iy);
		}

		int Hash(int ix, int iy)
		{
			return iy * w + ix;
		}
	}
}
