using System.Drawing.Imaging;
using System.Drawing;
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
	private static readonly string[] ArrowSquareSubImageIds = new[]
	{
		"itg-down-1-4",
		"itg-solo-1-4",
		"itg-center-1-4",
		"itg-down-1-8",
		"itg-solo-1-8",
		"itg-center-1-8",
		"itg-down-1-32",
		"itg-solo-1-32",
		"itg-center-1-32",
		"itg-down-receptor",
		"itg-solo-receptor",
		"itg-center-receptor",
		"itg-down-1-16",
		"itg-solo-1-16",
		"itg-center-1-16",
		"itg-down-1-48",
		"itg-solo-1-48",
		"itg-center-1-48",
		"itg-down-receptor-held",
		"itg-solo-receptor-held",
		"itg-center-receptor-held",
		"itg-down-1-12",
		"itg-solo-1-12",
		"itg-center-1-12",
		"itg-down-1-64",
		"itg-solo-1-64",
		"itg-center-1-64",
		"itg-down-receptor-glow",
		"itg-solo-receptor-glow",
		"itg-center-receptor-glow",
		"itg-down-1-24",
		"itg-solo-1-24",
		"itg-center-1-24",
		"piu-diagonal-red",
		"piu-center",
		"piu-diagonal-blue",
		"itg-hold-body-inactive",
		null, //"itg-hold-end-inactive",
		"itg-hold-center-body-inactive",
		null, //"itg-hold-center-end-inactive",
		"itg-hold-solo-body-inactive",
		null, //"itg-hold-solo-end-inactive",
		"piu-diagonal-receptor",
		"piu-center-receptor",
		"piu-hold-blue",
		"itg-roll-body-inactive",
		null, //"itg-roll-end-inactive",
		"itg-roll-center-body-inactive",
		null, //"itg-roll-center-end-inactive",
		"itg-roll-solo-body-inactive",
		null, //"itg-roll-solo-end-inactive",
		"piu-diagonal-receptor-held",
		"piu-center-receptor-held",
		"piu-roll-blue",
		"itg-hold-body-active",
		null, //"itg-hold-end-active",
		"itg-hold-center-body-active",
		null, //"itg-hold-center-end-active",
		"itg-hold-solo-body-active",
		null, //"itg-hold-solo-end-active",
		"piu-diagonal-receptor-glow",
		"piu-center-receptor-glow",
		"piu-hold-red",
		"itg-roll-body-active",
		null, //"itg-roll-end-active",
		"itg-roll-center-body-active",
		null, //"itg-roll-center-end-active",
		"itg-roll-solo-body-active",
		null, //"itg-roll-solo-end-active",
		"piu-roll-center",
		"piu-hold-center",
		"piu-roll-red",
		"mine",
	};

	private static readonly string[] ArrowSnapSubImageIds = new[]
	{
		"snap-1-4",
		"snap-1-8",
		"snap-1-24",
		"snap-1-32",
		"snap-1-16",
		"snap-1-12",
		"snap-1-48",
		"snap-1-64",
	};

	private static readonly string[] ArrowHoldStartSubImageIds = new[]
	{
		"itg-hold-solo-start-active",
		"itg-hold-solo-start-inactive",
		"itg-roll-solo-start-active",
		"itg-roll-solo-start-inactive",
	};

	private static readonly string[] IconSubImageIds = new[]
	{
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
	};

	private const string ContentDir = @"..\..\..\..\StepManiaEditor\Content\";
	private const string EditorDir = @"..\..\..\..\StepManiaEditor\";
	private const string InputArrows = "arrows.png";
	private const string InputIcons = "icons.png";
	private const string OutputImage = "atlas.png";
	private const string OutputAtlas = "atlas.json";
	private const int OutputAtlasWidth = 1800;
	private const int OutputAtlasHeight = 1800;
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
			locationsForSaving.Add(subTextureId, new List<int>
			{
				subTextureRect.X, subTextureRect.Y, subTextureRect.Width, subTextureRect.Height,
			});
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

		ProcessGridOfImages(arrowsTexture, ArrowSquareSubImageIds, 9, 0, 0, 128, 128, 0);
		ProcessGridOfImages(arrowsTexture, ArrowSnapSubImageIds, 4, 648, 1032, 40, 40, 8);
		ProcessGridOfImages(arrowsTexture, ArrowHoldStartSubImageIds, 4, 128, 1060, 128, 28, 0);

		// Process hold end caps trimmed to their exact height.
		// This is done because we use the texture size for determining if a click lands on a hold.
		// Most arrows are big enough in the 128x128 frames, but the hold ends are much shorter.
		// TODO: read color data to trim height.

		// Padding to account for programmatically generated selection rims.
		var capHeightPadding = SelectionRimSize;
		var capHeight = 57 + capHeightPadding;
		var capHeightCenter = 43 + capHeightPadding;
		var capHeightSolo = 45 + capHeightPadding;
		ProcessGridOfImages(arrowsTexture, new[] { "itg-hold-end-inactive" }, 1, 128, 512, 128, capHeight, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-hold-center-end-inactive" }, 1, 384, 512, 128, capHeightCenter, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-hold-solo-end-inactive" }, 1, 640, 512, 128, capHeightSolo, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-roll-end-inactive" }, 1, 128, 640, 128, capHeight, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-roll-center-end-inactive" }, 1, 384, 640, 128, capHeightCenter, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-roll-solo-end-inactive" }, 1, 640, 640, 128, capHeightSolo, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-hold-end-active" }, 1, 128, 768, 128, capHeight, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-hold-center-end-active" }, 1, 384, 768, 128, capHeightCenter, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-hold-solo-end-active" }, 1, 640, 768, 128, capHeightSolo, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-roll-end-active" }, 1, 128, 896, 128, capHeight, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-roll-center-end-active" }, 1, 384, 896, 128, capHeightCenter, 0);
		ProcessGridOfImages(arrowsTexture, new[] { "itg-roll-solo-end-active" }, 1, 640, 896, 128, capHeightSolo, 0);

		Logger.Info($"Added images from {InputArrows}.");
	}

	private void AddIconTextures()
	{
		Logger.Info($"Adding images from {InputIcons}.");
		var iconsTexture = LoadTexture(InputIcons);
		if (iconsTexture == null)
			return;
		ProcessGridOfImages(iconsTexture, IconSubImageIds, 12, 0, 0, 16, 16, 0);
		Logger.Info($"Added images from {InputIcons}.");
	}

	private void ProcessGridOfImages(Texture2D source, string[] identifiers, int numCols, int startX, int startY, int w, int h,
		int padding)
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
					  || identifier.Contains("icon"));

				// Copy the sub-texture out of the source texture.
				var subTexture = new Texture2D(GraphicsDevice, w, h);
				var subTextureData = new uint[w * h];
				var sourceX = startX + (w + padding) * (i % numCols);
				for (var subX = 0; subX < w; subX++, sourceX++)
				{
					var sourceY = startY + (h + padding) * (i / numCols);
					for (var subY = 0; subY < h; subY++, sourceY++)
					{
						subTextureData[subX + subY * w] = sourceColorData[sourceX + sourceY * sourceW];
					}
				}

				subTexture.SetData(subTextureData);
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

		// Generate and add generic region rect texture.
		var regionRectTexture = new Texture2D(GraphicsDevice, 1, 1);
		textureData = new uint[1];
		textureData[0] = 0xFFFFFFFF;
		regionRectTexture.SetData(textureData);
		Atlas.AddSubTexture("region-rect", regionRectTexture, true);
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
				}

				// Fully transparent: Generate a highlight bg color rim around the opaque area.
				else
				{
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
}
