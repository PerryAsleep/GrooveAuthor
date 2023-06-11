using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameExtensions;

/// <summary>
/// A TextureAtlas that is constructed by adding sub-textures at runtime.
/// Expected Usage:
///  Call AddSubTexture as needed to add sub-textures.
///  Call Update after adding textures and before using.
/// </summary>
public class DynamicTextureAtlas : TextureAtlas
{
	private readonly GraphicsDevice GraphicsDevice;
	private readonly PackNode Root;
	private readonly int Padding;
	private readonly Dictionary<string, PackNode> DirtyNodes = new();
	private readonly RenderTarget2D RenderTarget;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="graphicsDevice">GraphicsDevice to use for creating the texture.</param>
	/// <param name="width">Texture atlas width.</param>
	/// <param name="height">Texture atlas height.</param>
	/// <param name="padding">Padding in pixels between sub-textures.</param>
	public DynamicTextureAtlas(GraphicsDevice graphicsDevice, int width, int height, int padding)
		: base(new RenderTarget2D(graphicsDevice, width, height))
	{
		RenderTarget = (RenderTarget2D)GetTexture();
		GraphicsDevice = graphicsDevice;
		Padding = padding;
		Root = PackNode.CreateRoot(new Rectangle(0, 0, width, height));
	}

	/// <summary>
	/// Adds a Texture2D as a sub-texture to the Atlas.
	/// Update must be called at least once after adding a texture in order for it to be packed and available
	/// for Draw calls.
	/// </summary>
	/// <param name="subTextureId">String identifier of the given sub-texture.</param>
	/// <param name="texture">The texture to pack.</param>
	/// <param name="generateMipLevels">Whether or not to generate mip levels for the given texture.</param>
	public void AddSubTexture(string subTextureId, Texture2D texture, bool generateMipLevels)
	{
		if (subTextureId == null)
		{
			Logger.Warn("Failed to pack texture with null identifier into TextureAtlas. A texture identifier must be non null.");
			return;
		}

		if (ContainsSubTexture(subTextureId))
		{
			Logger.Warn(
				$"Failed to pack texture \"{subTextureId}\" into TextureAtlas. A texture identified by \"{subTextureId}\" already exists.");
			return;
		}

		// Mip levels - generate levels and pack each one.
		if (generateMipLevels)
		{
			// Generate mip levels.
			var mipLevels = TextureUtils.GenerateMipLevels(GraphicsDevice, texture);

			// Pack each mip level texture.
			var i = 0;
			PackNode level0MipNode = null;
			var mipNodes = new PackNode[mipLevels.Count];
			foreach (var mipLevel in mipLevels)
			{
				// Create an id for this texture.
				var id = subTextureId;
				if (i > 0)
				{
					id = subTextureId + i;
				}

				// Pack.
				var node = Root.Pack(mipLevel, Padding);
				if (node == null)
				{
					Logger.Warn($"Failed to pack texture \"{id}\" into TextureAtlas. Not enough space.");
					return;
				}

				// Only add the level 0 texture to the TextureNodes that we track for the public API.
				// The other nodes are collected to set on the level 0 PackNode as it's mip level nodes.
				if (i == 0)
				{
					level0MipNode = node;
					AddSubTextureNode(id, node);
				}

				mipNodes[i] = node;

				// Add every nodes to the DirtyNodes set so we render them to the texture atlas.
				DirtyNodes.Add(id, node);
				i++;
			}

			// Set the MipLevels on the first PackNode.
			if (level0MipNode != null)
				level0MipNode.MipLevels = mipNodes;
		}

		// No mip levels - just pack the texture directly.
		else
		{
			var node = Root.Pack(texture, Padding);
			if (node == null)
			{
				Logger.Warn($"Failed to pack texture \"{texture}\" into TextureAtlas. Not enough space.");
				return;
			}

			// Set the mip levels to be just this PackNode.
			node.MipLevels = new[] { node };

			AddSubTextureNode(subTextureId, node);
			DirtyNodes.Add(subTextureId, node);
		}
	}

	/// <summary>
	/// Updates the TextureAtlas by packing any unpacked sub-textures.
	/// </summary>
	public void Update()
	{
		if (DirtyNodes.Count == 0)
			return;

		// Render each dirty texture to the atlas.
		GraphicsDevice.SetRenderTarget(RenderTarget);
		GraphicsDevice.DepthStencilState = new DepthStencilState() { DepthBufferEnable = true };
		GraphicsDevice.Clear(Color.Transparent);
		var sb = new SpriteBatch(GraphicsDevice);
		sb.Begin();
		foreach (var kvp in DirtyNodes)
		{
			var node = kvp.Value;
			sb.Draw(node.Texture, node.TextureRect, Color.White);

			// If we are using padding, extend the rim of the texture by one to mitigate sampling artifacts.
			if (Padding > 0)
			{
				// Left padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X - 1, node.TextureRect.Y, 1, node.TextureRect.Height),
					new Rectangle(0, 0, 1, node.TextureRect.Height),
					Color.White);

				// Top left padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X - 1, node.TextureRect.Y - 1, 1, 1),
					new Rectangle(0, 0, 1, 1),
					Color.White);

				// Bottom left padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X - 1, node.TextureRect.Y + node.TextureRect.Height, 1, 1),
					new Rectangle(0, node.TextureRect.Height - 1, 1, 1),
					Color.White);

				// Right padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X + node.TextureRect.Width, node.TextureRect.Y, 1, node.TextureRect.Height),
					new Rectangle(node.TextureRect.Width - 1, 0, 1, node.TextureRect.Height),
					Color.White);

				// Top right padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X + node.TextureRect.Width, node.TextureRect.Y - 1, 1, 1),
					new Rectangle(node.TextureRect.Width - 1, 0, 1, 1),
					Color.White);

				// Bottom right padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X + node.TextureRect.Width, node.TextureRect.Y + node.TextureRect.Height, 1,
						1),
					new Rectangle(node.TextureRect.Width - 1, node.TextureRect.Height - 1, 1, 1),
					Color.White);

				// Top padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X, node.TextureRect.Y - 1, node.TextureRect.Width, 1),
					new Rectangle(0, 0, node.TextureRect.Width, 1),
					Color.White);

				// Bottom padding.
				sb.Draw(node.Texture,
					new Rectangle(node.TextureRect.X, node.TextureRect.Y + node.TextureRect.Height, node.TextureRect.Width, 1),
					new Rectangle(0, node.TextureRect.Height - 1, node.TextureRect.Width, 1),
					Color.White);
			}

			// Let the node perform cleanup now that it is done rendering to the texture atlas.
			node.OnPackComplete();
		}

		sb.End();
		GraphicsDevice.SetRenderTarget(null);
		DirtyNodes.Clear();
	}
}
