using System.Text.Json.Serialization;
using System.Text.Json;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameExtensions;

/// <summary>
/// A TextureAtlas from a pre-made texture containing all sub-textures and a corresponding
/// atlas definition for locating sub-textures.
/// </summary>
public class StaticTextureAtlas : TextureAtlas
{
	/// <summary>
	/// Constructor for creating an empty StaticTextureAtlas.
	/// </summary>
	/// <param name="texture">Texture containing all sub-textures.</param>
	public StaticTextureAtlas(Texture2D texture)
		: base(texture)
	{
	}

	/// <summary>
	/// Synchronously loads a texture and atlas file into a new StaticTextureAtlas.
	/// </summary>
	/// <param name="contentManager">ContentManager to use for loading the texture.</param>
	/// <param name="atlasTextureId">Identifier of the texture to load.</param>
	/// <param name="atlasFile">File containing the atlas definition associated with the texture.</param>
	/// <returns>New StaticTextureAtlas or null if any errors were encountered.</returns>
	public static StaticTextureAtlas Load(ContentManager contentManager, string atlasTextureId, string atlasFile)
	{
		// Start loading the atlas file asynchronously so we can load the texture in parallel.
		var loadAtlasTask = Task.Run(async () => await LoadAtlasAsync(atlasFile));

		// Load the texture.
		Texture2D texture;
		try
		{
			texture = contentManager.Load<Texture2D>(atlasTextureId);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load Texture {atlasTextureId}. {e}");
			texture = null;
		}

		// Wait for the atlas to finish loading if it hasn't finished yet.
		loadAtlasTask.Wait();
		var locations = loadAtlasTask.Result;

		// If either the texture or the atlas failed to load the StaticTextureAtlas cannot be made.
		if (texture == null || locations == null)
		{
			return null;
		}

		var textureAtlas = new StaticTextureAtlas(texture);

		// Check all the sub-texture ids, which include mip levels, and accumulate the mip level count per base sub-texture id.
		var mipCounts = new Dictionary<string, int>();
		foreach (var (textureId, _) in locations)
		{
			var (baseTextureId, mipLevel) = GetMipCountFromSubTextureId(textureId);
			if (mipCounts.TryGetValue(baseTextureId, out var currentMipLevel))
			{
				mipLevel = Math.Max(mipLevel, currentMipLevel);
			}

			mipCounts[baseTextureId] = mipLevel;
		}

		// Add PackNodes specifying the bounds of all sub-textures.
		foreach (var (baseTextureId, mipCount) in mipCounts)
		{
			if (mipCount < 1)
			{
				Logger.Error($"Invalid mip level count for {baseTextureId}.");
				continue;
			}

			var loc = locations[baseTextureId];
			if (loc.Count != 4)
			{
				Logger.Error($"Invalid sub-texture bounds for {baseTextureId}.");
				continue;
			}

			var packNodeError = false;
			var packNode = PackNode.CreateSubTextureNode(new Rectangle(loc[0], loc[1], loc[2], loc[3]));
			packNode.MipLevels = new PackNode[mipCount];
			packNode.MipLevels[0] = packNode;
			for (var mipIndex = 1; mipIndex < mipCount; mipIndex++)
			{
				if (!locations.TryGetValue(GetMipSubTextureId(baseTextureId, mipIndex), out var mipLocation))
				{
					packNodeError = true;
					Logger.Error($"No mip level bounds defined for {baseTextureId} mip level {mipIndex}");
					break;
				}

				if (mipLocation.Count != 4)
				{
					packNodeError = true;
					Logger.Error($"Invalid sub-texture bounds for {baseTextureId} mip level {mipIndex}.");
					break;
				}

				packNode.MipLevels[mipIndex] =
					PackNode.CreateSubTextureNode(new Rectangle(mipLocation[0], mipLocation[1], mipLocation[2], mipLocation[3]));
			}

			if (!packNodeError)
				textureAtlas.AddSubTextureNode(baseTextureId, packNode);
		}

		return textureAtlas;
	}

	/// <summary>
	/// Loads the atlas file.
	/// </summary>
	/// <param name="atlasFile">File containing the atlas data.</param>
	/// <returns>Dictionary of sub-texture id to sub-texture rect bounds.</returns>
	private static async Task<Dictionary<string, List<int>>> LoadAtlasAsync(string atlasFile)
	{
		var options = new JsonSerializerOptions
		{
			Converters =
			{
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
			},
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			IncludeFields = true,
		};

		Dictionary<string, List<int>> atlas;
		try
		{
			if (!File.Exists(atlasFile))
			{
				Logger.Error($"Could not find {atlasFile}.");
				return null;
			}

			await using var openStream = File.OpenRead(atlasFile);
			atlas = await JsonSerializer.DeserializeAsync<Dictionary<string, List<int>>>(openStream, options);
			if (atlas == null)
				throw new Exception($"Could not deserialize {atlasFile}.");
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to load {atlasFile}. {e}");
			return null;
		}

		return atlas;
	}
}
