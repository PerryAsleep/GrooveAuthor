using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameExtensions;

/// <summary>
/// TextureAtlas wraps a texture containing sub-textures in order to reduce draw calls.
/// Sub-textures have unique string identifiers.
/// Sub-textures may have mip levels.
/// </summary>
public abstract class TextureAtlas
{
	/// <summary>
	/// A node in the TextureAtlas containing the location of the sub-texture and
	/// any that texture's mips.
	/// This class could have better encapsulation. It currently has two use cases:
	///  1) Used to hold a sub-texture, and to determine where new dynamic sub-textures
	///     should live within the texture atlas.
	///  2) Used to just specify the rect of a sub-texture for a previously-constructed
	///     texture atlas.
	/// </summary>
	protected class PackNode
	{
		/// <summary>
		/// Two children used for packing algorithm.
		/// </summary>
		private readonly PackNode[] Children = new PackNode[2];

		/// <summary>
		/// Rectangle used for packing algorithm.
		/// </summary>
		private Rectangle PackingRect;

		/// <summary>
		/// The rect of the sub-texture within the TextureAtlas.
		/// </summary>
		public Rectangle TextureRect;

		/// <summary>
		/// The original sub-texture packed into the TextureAtlas.
		/// This is null after packing is complete.
		/// </summary>
		public Texture2D Texture;

		/// <summary>
		/// Mip levels. The node at index 0 is this node. Subsequent nodes contain
		/// the texture mips where each is half the size of the previous.
		/// </summary>
		public PackNode[] MipLevels;

		public static PackNode CreateRoot(Rectangle textureBounds)
		{
			return new PackNode
			{
				PackingRect = textureBounds,
			};
		}

		public static PackNode CreateSubTextureNode(Rectangle subTextureBounds)
		{
			return new PackNode
			{
				TextureRect = subTextureBounds,
			};
		}

		private PackNode()
		{
		}

		private PackNode(Rectangle packingRect)
		{
			PackingRect = packingRect;
		}

		/// <summary>
		/// Given a scale, returns the best PackNode to use from this PackNode's mip levels.
		/// </summary>
		/// <param name="scale">
		/// Desired scale. Will be updated to reflect the new scale which should be applied
		/// to the returned PackNode in order to achieve the desired scale.</param>
		/// <param name="mipLevel">The mip level of the returned PackNode.</param>
		/// <returns>PackNode to use.</returns>
		public PackNode GetBest(ref double scale, out int mipLevel)
		{
			mipLevel = 0;
			if (MipLevels == null)
				return this;

			while (scale < 0.5 && mipLevel < MipLevels.Length - 1)
			{
				scale *= 2.0;
				mipLevel++;
			}

			return MipLevels[mipLevel];
		}

		/// <summary>
		/// Given a destination rectangle to render the texture in screen space, returns the best
		/// PackNode to use from this PackNode's mip levels.
		/// </summary>
		/// <param name="destinationRectangle">The rectangle to draw this texture to in screen space.</param>
		/// <param name="scale">
		/// The new scale which should be applied in order to achieve the specified size.
		/// </param>
		/// <param name="mipLevel">The mip level of the returned PackNode.</param>
		/// <returns>PackNode to use.</returns>
		public PackNode GetBest(Rectangle destinationRectangle, out double scale, out int mipLevel)
		{
			var xScale = destinationRectangle.Width / (double)TextureRect.Width;
			var yScale = destinationRectangle.Height / (double)TextureRect.Height;
			scale = Math.Max(xScale, yScale);
			return GetBest(ref scale, out mipLevel);
		}

		/// <summary>
		/// Recursive packing algorithm.
		/// Packing a texture on the root PackNode will return a new PackNode containing the
		/// the Texture and it's location with the TextureAtlas.
		/// </summary>
		/// <param name="texture">Texture to pack.</param>
		/// <param name="padding">Padding in pixels to include around the Texture.</param>
		/// <returns>New PackNode or null if the given texture could not be packed.</returns>
		public PackNode Pack(Texture2D texture, int padding)
		{
			// This PackNode is not a leaf.
			if (Children[0] != null)
			{
				// Try packing in each child.
				var newNode = Children[0].Pack(texture, padding);
				return newNode ?? Children[1].Pack(texture, padding);
			}

			// This PackNode is a leaf.

			// This PackNode already has a Texture.
			if (Texture != null)
				return null;

			// Account for padding.
			var twp = texture.Width + (padding << 1);
			var thp = texture.Height + (padding << 1);

			// Texture is too large for this PackNode.
			if (twp > PackingRect.Width || thp > PackingRect.Height)
				return null;

			// Texture fits perfectly in this PackNode's bounds.
			if (twp == PackingRect.Width && thp == PackingRect.Height)
			{
				Texture = texture;
				TextureRect = new Rectangle(PackingRect.X + padding, PackingRect.Y + padding, texture.Width, texture.Height);
				return this;
			}

			// Texture fits with extra space. Create Children to split the area so it fits perfectly in one
			// dimension. We'll recurse to create a child that fits the texture perfectly.

			// Split into two rectangles aligned horizontally.
			if (PackingRect.Width - twp > PackingRect.Height - thp)
			{
				Children[0] = new PackNode(new Rectangle(PackingRect.X, PackingRect.Y, twp, PackingRect.Height));
				Children[1] = new PackNode(new Rectangle(PackingRect.X + twp, PackingRect.Y, PackingRect.Width - twp,
					PackingRect.Height));
			}
			// Split into two rectangles aligned vertically.
			else
			{
				Children[0] = new PackNode(new Rectangle(PackingRect.X, PackingRect.Y, PackingRect.Width, thp));
				Children[1] = new PackNode(new Rectangle(PackingRect.X, PackingRect.Y + thp, PackingRect.Width,
					PackingRect.Height - thp));
			}

			// Pack into the first child, which is now sized perfectly in at least one dimension.
			return Children[0].Pack(texture, padding);
		}

		public void OnPackComplete()
		{
			// Remove the reference to the original texture so it can be garbage collected.
			Texture = null;
		}
	}

	/// <summary>
	/// Texture containing sub-textures.
	/// </summary>
	private readonly Texture2D Texture;

	/// <summary>
	/// Sub-texture identifiers to PackNodes which specify coordinates of the sub-texture and its mips.
	/// </summary>
	private readonly Dictionary<string, PackNode> TextureNodes = new();

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="texture">
	/// Texture2D of the entire texture to use for holding sub-textures.
	/// This may be a Texture2D, or a RenderTarget2D to dynamically render sub-textures to.
	/// </param>
	protected TextureAtlas(Texture2D texture)
	{
		Texture = texture;
	}

	/// <summary>
	/// Gets the texture containing all sub-textures.
	/// </summary>
	/// <returns>The texture containing all sub-textures.</returns>
	public Texture2D GetTexture()
	{
		return Texture;
	}

	/// <summary>
	/// Returns a Dictionary of all sub-texture identifiers to their bounds within the texture atlas.
	/// </summary>
	/// <param name="includeMips">Whether or not to include sub-texture mip levels.</param>
	/// <returns>Dictionary of all sub-texture identifiers to their bounds within the texture atlas</returns>
	public Dictionary<string, Rectangle> GetAllSubTextureLocations(bool includeMips)
	{
		var allLocations = new Dictionary<string, Rectangle>();
		foreach (var (textureId, node) in TextureNodes)
		{
			allLocations.Add(textureId, node.TextureRect);
			if (includeMips && node.MipLevels?.Length > 1)
			{
				for (var mipLevelIndex = 1; mipLevelIndex < node.MipLevels.Length; mipLevelIndex++)
				{
					allLocations.Add(GetMipSubTextureId(textureId, mipLevelIndex), node.MipLevels[mipLevelIndex].TextureRect);
				}
			}
		}

		return allLocations;
	}

	/// <summary>
	/// Gets the sub-texture identifier of an automatically generated mip level sub-texture.
	/// </summary>
	/// <param name="baseSubTextureId">Base sub-texture id.</param>
	/// <param name="mipLevelIndex">Mip level index.</param>
	/// <returns>Identifier of sub-texture at the given mip level.</returns>
	protected static string GetMipSubTextureId(string baseSubTextureId, int mipLevelIndex)
	{
		return $"{baseSubTextureId}-mip-{mipLevelIndex}";
	}

	/// <summary>
	/// Given a sub-texture identifier that may represent a specific mip level, return the base
	/// sub-texture identifier
	/// </summary>
	/// <param name="subTextureId">Sub-texture identifier.</param>
	/// <returns>
	/// Tuple where the first value is the base sub-texture identifier with no mip level specifier and
	/// the second value is the mip level index.
	/// If the given sub-texture id does not specify a mip level then the base sub-texture identifier
	/// returned will be the given id, and the mip level returned will be 1.
	/// </returns>
	protected static (string, int) GetMipCountFromSubTextureId(string subTextureId)
	{
		var mipMarkerIndex = subTextureId.LastIndexOf("-mip-", StringComparison.Ordinal);
		if (mipMarkerIndex < 0)
			return (subTextureId, 1);
		var indexSubString = subTextureId.Substring(mipMarkerIndex + 5);
		if (!int.TryParse(indexSubString, out var mipIndex))
			return (subTextureId, 1);
		return (subTextureId.Substring(0, mipMarkerIndex), mipIndex + 1);
	}

	protected bool ContainsSubTexture(string subTextureId)
	{
		return TextureNodes.ContainsKey(subTextureId);
	}

	protected void AddSubTextureNode(string subTextureId, PackNode node)
	{
		TextureNodes.Add(subTextureId, node);
	}

	private bool GetNode(string subTextureId, out PackNode node)
	{
		return TextureNodes.TryGetValue(subTextureId, out node);
	}

	private bool GetNode(string subTextureId, out PackNode node, ref double scale, out int mipLevel)
	{
		mipLevel = 0;
		if (!GetNode(subTextureId, out node))
			return false;
		node = node.GetBest(ref scale, out mipLevel);
		return true;
	}

	private bool GetNode(string subTextureId, Rectangle destinationRectangle, out PackNode node, out int mipLevel)
	{
		mipLevel = 0;
		if (!GetNode(subTextureId, out node))
			return false;
		node = node.GetBest(destinationRectangle, out _, out mipLevel);
		return true;
	}

	public (int, int) GetDimensions(string subTextureId)
	{
		if (!GetNode(subTextureId, out var node))
		{
			Logger.Warn($"Failed to get dimensions for texture identified by \"{subTextureId}\". No texture with that id found.");
			return (0, 0);
		}

		return (node.TextureRect.Width, node.TextureRect.Height);
	}

	public (int, int) GetDimensions()
	{
		return (Texture.Width, Texture.Height);
	}

	public void DebugDraw(SpriteBatch spriteBatch)
	{
		spriteBatch.Draw(Texture, new Vector2(0, 0), Color.White);
	}

	public (int, int, int, int) GetSubTextureBounds(string subTextureId)
	{
		if (!GetNode(subTextureId, out var node))
		{
			Logger.Warn($"Failed to get texture identified by \"{subTextureId}\". No texture with that id found.");
			return (0, 0, 0, 0);
		}
		return (node.TextureRect.X, node.TextureRect.Y, node.TextureRect.Width, node.TextureRect.Height);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float alpha)
	{
		if (!GetNode(subTextureId, destinationRectangle, out var node, out _))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		spriteBatch.Draw(Texture, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha));
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, Color color)
	{
		if (!GetNode(subTextureId, destinationRectangle, out var node, out _))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		spriteBatch.Draw(Texture, destinationRectangle, node.TextureRect, color);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, float alpha)
	{
		Draw(subTextureId, spriteBatch, destinationRectangle, rotation, alpha, SpriteEffects.None);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation,
		Vector2 rotationOffset, float alpha, SpriteEffects spriteEffects)
	{
		if (!GetNode(subTextureId, destinationRectangle, out var node, out _))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		spriteBatch.Draw(Texture, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation,
			rotationOffset, spriteEffects, 1.0f);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, float alpha,
		SpriteEffects spriteEffects)
	{
		if (!GetNode(subTextureId, destinationRectangle, out var node, out _))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		// When rotating, we need to provide a center offset to rotate by. For textures with odd lengths, this results
		// in rounding errors. However we need to go through the SpriteBatch Draw method taking rotations in order to
		// use SpriteEffects. When using 0.0f rotation, do not apply the offset to mitigate the rounding issues.
		var rotationOffset = Vector2.Zero;
		if (rotation != 0.0f)
		{
			rotationOffset = new Vector2(node.TextureRect.Width >> 1, node.TextureRect.Height >> 1);
			destinationRectangle.X += destinationRectangle.Width >> 1;
			destinationRectangle.Y += destinationRectangle.Height >> 1;
		}

		Draw(subTextureId, spriteBatch, destinationRectangle, rotation, rotationOffset, alpha, spriteEffects);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Vector2 position, Vector2 origin, double scale, float rotation,
		float alpha, SpriteEffects spriteEffects)
	{
		if (!GetNode(subTextureId, out var node, ref scale, out var mipLevel))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		// If we are using a smaller texture, we need to offset the origin position accordingly.
		if (mipLevel != 0)
		{
			var originScale = Math.Pow(0.5, mipLevel);
			origin.X = (float)(origin.X * originScale);
			origin.Y = (float)(origin.Y * originScale);
		}

		spriteBatch.Draw(Texture, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, origin, (float)scale,
			spriteEffects, 1.0f);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Vector2 position, Vector2 origin, Color color, double scale,
		float rotation, SpriteEffects spriteEffects)
	{
		if (!GetNode(subTextureId, out var node, ref scale, out var mipLevel))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		// If we are using a smaller texture, we need to offset the origin position accordingly.
		if (mipLevel != 0)
		{
			var originScale = Math.Pow(0.5, mipLevel);
			origin.X = (float)(origin.X * originScale);
			origin.Y = (float)(origin.Y * originScale);
		}

		spriteBatch.Draw(Texture, position, node.TextureRect, color, rotation, origin, (float)scale, spriteEffects, 1.0f);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Rectangle sourceRectangle, Rectangle destinationRectangle,
		float rotation, float alpha, SpriteEffects spriteEffects)
	{
		if (!GetNode(subTextureId, destinationRectangle, out var node, out var mipLevel))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		// When rotating, we need to provide a center offset to rotate by. For textures with odd lengths, this results
		// in rounding errors. However we need to go through the SpriteBatch Draw method taking rotations in order to
		// use SpriteEffects. When using 0.0f rotation, do not apply the offset to mitigate the rounding issues.
		var rotationOffset = Vector2.Zero;
		if (rotation != 0.0f)
		{
			rotationOffset = new Vector2(node.TextureRect.Width >> 1, node.TextureRect.Height >> 1);
			destinationRectangle.X += destinationRectangle.Width >> 1;
			destinationRectangle.Y += destinationRectangle.Height >> 1;
		}

		// If we are using a smaller texture, we need to offset the source rectangle accordingly.
		if (mipLevel != 0)
		{
			var originScale = Math.Pow(0.5, mipLevel);
			sourceRectangle.X = (int)(sourceRectangle.X * originScale);
			sourceRectangle.Y = (int)(sourceRectangle.Y * originScale);
			sourceRectangle.Width = (int)(sourceRectangle.Width * originScale);
			sourceRectangle.Height = (int)(sourceRectangle.Height * originScale);
		}

		sourceRectangle.X += node.TextureRect.X;
		sourceRectangle.Y += node.TextureRect.Y;

		spriteBatch.Draw(Texture, destinationRectangle, sourceRectangle, new Color(1.0f, 1.0f, 1.0f, alpha), rotation,
			rotationOffset, spriteEffects, 1.0f);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation,
		Vector2 rotationOffset, float alpha)
	{
		if (!GetNode(subTextureId, destinationRectangle, out var node, out _))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		spriteBatch.Draw(Texture, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation,
			rotationOffset, SpriteEffects.None, 1.0f);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Vector2 position, float alpha)
	{
		if (!GetNode(subTextureId, out var node))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		spriteBatch.Draw(Texture, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha));
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Vector2 position, double scale, float rotation, float alpha)
	{
		Draw(subTextureId, spriteBatch, position, scale, rotation, alpha, SpriteEffects.None);
	}

	public void Draw(string subTextureId, SpriteBatch spriteBatch, Vector2 position, double scale, float rotation, float alpha,
		SpriteEffects spriteEffects)
	{
		if (!GetNode(subTextureId, out var node, ref scale, out _))
		{
			Logger.Warn($"Failed to draw packed texture identified by \"{subTextureId}\". No texture with that id found.");
			return;
		}

		// When rotating, we need to provide a center offset to rotate by. For textures with odd lengths, this results
		// in rounding errors. However we need to go through the SpriteBatch Draw method taking rotations in order to
		// use SpriteEffects. When using 0.0f rotation, do not apply the offset to mitigate the rounding issues.
		var rotationOffset = Vector2.Zero;
		if (rotation != 0.0f)
		{
			rotationOffset = new Vector2(node.TextureRect.Width >> 1, node.TextureRect.Height >> 1);
			position.X += (float)(node.TextureRect.Width * scale * 0.5);
			position.Y += (float)(node.TextureRect.Height * scale * 0.5);
		}

		spriteBatch.Draw(Texture, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset,
			(float)scale, spriteEffects, 1.0f);
	}
}
