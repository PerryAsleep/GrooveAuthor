using System;
using System.Collections.Generic;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	/// <summary>
	/// TextureAtlas wraps a texture that sub-textures are rendered to in order to reduce draw calls.
	/// Optionally, mip maps can be automatically generated for the added sub-textures.
	/// 
	/// Expected Usage:
	///  Add a Texture2D to the TextureAtlas with AddTexture() before any Update() calls.
	///  Call Update() once each frame before any Draw() calls.
	///  Call one of the Draw() methods to draw an individual sub-texture from the Atlas.
	/// </summary>
	public class TextureAtlas
	{
		/// <summary>
		/// A node in the TextureAtlas containing the location of the sub-texture and
		/// any that texture's mips.
		/// </summary>
		public class PackNode
		{
			/// <summary>
			/// Two children used for packing algorithm.
			/// </summary>
			private readonly PackNode[] Children = new PackNode[2];
			/// <summary>
			/// Rectangle used for packing algorithm.
			/// </summary>
			private readonly Rectangle Rect;

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

			public PackNode(Rectangle rect)
			{
				Rect = rect;
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
				if (twp > Rect.Width || thp > Rect.Height)
					return null;

				// Texture fits perfectly in this PackNode's bounds.
				if (twp == Rect.Width && thp == Rect.Height)
				{
					Texture = texture;
					TextureRect = new Rectangle(Rect.X + padding, Rect.Y + padding, texture.Width, texture.Height);
					return this;
				}

				// Texture fits with extra space. Create Children to split the area so it fits perfectly in one
				// dimension. We'll recurse to create a child that fits the texture perfectly.

				// Split into two rectangles aligned horizontally.
				if (Rect.Width - twp > Rect.Height - thp)
				{
					Children[0] = new PackNode(new Rectangle(Rect.X, Rect.Y, twp, Rect.Height));
					Children[1] = new PackNode(new Rectangle(Rect.X + twp, Rect.Y, Rect.Width - twp, Rect.Height));
				}
				// Split into two rectangles aligned vertically.
				else
				{
					Children[0] = new PackNode(new Rectangle(Rect.X, Rect.Y, Rect.Width, thp));
					Children[1] = new PackNode(new Rectangle(Rect.X, Rect.Y + thp, Rect.Width, Rect.Height - thp));
				}

				// Pack into the first child, which is now sized perfectly in at least one dimension.
				return Children[0].Pack(texture, padding);
			}
		}

		private readonly GraphicsDevice GraphicsDevice;
		private readonly RenderTarget2D Atlas;
		private readonly PackNode Root;
		private readonly int Padding;
		private readonly Dictionary<string, PackNode> TextureNodes = new Dictionary<string, PackNode>();
		private readonly Dictionary<string, PackNode> DirtyNodes = new Dictionary<string, PackNode>();

		public TextureAtlas(GraphicsDevice graphicsDevice, int width, int height, int padding)
		{
			GraphicsDevice = graphicsDevice;
			Atlas = new RenderTarget2D(GraphicsDevice, width, height);
			Padding = padding;
			Root = new PackNode(new Rectangle(0, 0, width, height));
		}

		/// <summary>
		/// Adds a Texture2D to the Atlas.
		/// Update must be called at least once after adding a texture in order for it to be packed and available
		/// for Draw calls.
		/// </summary>
		/// <param name="textureId">String identifier of the given texture.</param>
		/// <param name="texture">The texture to pack.</param>
		/// <param name="generateMipLevels">Whether or not to generate mip levels for the given texture.</param>
		public void AddTexture(string textureId, Texture2D texture, bool generateMipLevels)
		{
			if (textureId == null)
			{
				Logger.Warn("Failed to pack texture with null identifier into TextureAtlas. A texture identifier must be non null.");
				return;
			}

			if (TextureNodes.ContainsKey(textureId))
			{
				Logger.Warn($"Failed to pack texture \"{textureId}\" into TextureAtlas. A texture identified by \"{textureId}\" already exists.");
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
					var id = textureId;
					if (i > 0)
					{
						id = textureId + i;
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
						TextureNodes.Add(id, node);
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
				node.MipLevels = new [] { node };

				TextureNodes.Add(textureId, node);
				DirtyNodes.Add(textureId, node);
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
			GraphicsDevice.SetRenderTarget(Atlas);
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
						new Rectangle(node.TextureRect.X + node.TextureRect.Width, node.TextureRect.Y + node.TextureRect.Height, 1, 1),
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

				// Remove the reference to the original texture so it can be garbage collected.
				node.Texture = null;
			}
			sb.End();
			GraphicsDevice.SetRenderTarget(null);
			DirtyNodes.Clear();
		}

		private bool GetNode(string textureId, out PackNode node)
		{
			return TextureNodes.TryGetValue(textureId, out node);
		}

		private bool GetNode(string textureId, out PackNode node, ref double scale, out int mipLevel)
		{
			mipLevel = 0;
			if (!GetNode(textureId, out node))
				return false;
			node = node.GetBest(ref scale, out mipLevel);
			return true;
		}

		private bool GetNode(string textureId, Rectangle destinationRectangle, out PackNode node, out int mipLevel)
		{
			mipLevel = 0;
			if (!GetNode(textureId, out node))
				return false;
			node = node.GetBest(destinationRectangle, out _, out mipLevel);
			return true;
		}

		public (int, int) GetDimensions(string textureId)
		{
			if (!GetNode(textureId, out var node))
			{
				Logger.Warn($"Failed to get dimensions for texture identified by \"{textureId}\". No texture with that id found.");
				return (0, 0);
			}

			return (node.TextureRect.Width, node.TextureRect.Height);
		}

		public void DebugDraw(SpriteBatch spriteBatch)
		{
			spriteBatch.Draw(Atlas, new Vector2(0, 0), Color.White);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float alpha)
		{
			if (!GetNode(textureId, destinationRectangle, out var node, out _))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha));
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, Color color)
		{
			if (!GetNode(textureId, destinationRectangle, out var node, out _))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, destinationRectangle, node.TextureRect, color);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, float alpha)
		{
			Draw(textureId, spriteBatch, destinationRectangle, rotation, alpha, SpriteEffects.None);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, Vector2 rotationOffset, float alpha, SpriteEffects spriteEffects)
		{
			if (!GetNode(textureId, destinationRectangle, out var node, out _))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, spriteEffects, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, float alpha, SpriteEffects spriteEffects)
		{
			if (!GetNode(textureId, destinationRectangle, out var node, out _))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			// When rotating, we need to provide a center offset to rotate by. For textures with odd lengths, this results
			// in rounding errors. However we need to go through the SpriteBatch Draw method taking rotations in order to
			// use SpriteEffects. When using 0.0f rotation, do not apply the offset to mitigate the rounding issues.
			var rotationOffset = Vector2.Zero;
			if (rotation != 0.0f)
			{
				rotationOffset = new Vector2(node.TextureRect.Width >> 1, node.TextureRect.Height >> 1);
				destinationRectangle.X += (destinationRectangle.Width >> 1);
				destinationRectangle.Y += (destinationRectangle.Height >> 1);
			}

			Draw(textureId, spriteBatch, destinationRectangle, rotation, rotationOffset, alpha, spriteEffects);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, Vector2 origin, double scale, float rotation, float alpha, SpriteEffects spriteEffects)
		{
			if (!GetNode(textureId, out var node, ref scale, out var mipLevel))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			// If we are using a smaller texture, we need to offset the origin position accordingly.
			if (mipLevel != 0)
			{
				var originScale = Math.Pow(0.5, mipLevel);
				origin.X = (float)(origin.X * originScale);
				origin.Y = (float)(origin.Y * originScale);
			}

			spriteBatch.Draw(Atlas, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, origin, (float)scale, spriteEffects, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, Vector2 origin, Color color, double scale, float rotation, SpriteEffects spriteEffects)
		{
			if (!GetNode(textureId, out var node, ref scale, out var mipLevel))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			// If we are using a smaller texture, we need to offset the origin position accordingly.
			if (mipLevel != 0)
			{
				var originScale = Math.Pow(0.5, mipLevel);
				origin.X = (float)(origin.X * originScale);
				origin.Y = (float)(origin.Y * originScale);
			}

			spriteBatch.Draw(Atlas, position, node.TextureRect, color, rotation, origin, (float)scale, spriteEffects, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle sourceRectangle, Rectangle destinationRectangle, float rotation, float alpha, SpriteEffects spriteEffects)
		{
			if (!GetNode(textureId, destinationRectangle, out var node, out var mipLevel))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			// When rotating, we need to provide a center offset to rotate by. For textures with odd lengths, this results
			// in rounding errors. However we need to go through the SpriteBatch Draw method taking rotations in order to
			// use SpriteEffects. When using 0.0f rotation, do not apply the offset to mitigate the rounding issues.
			var rotationOffset = Vector2.Zero;
			if (rotation != 0.0f)
			{
				rotationOffset = new Vector2(node.TextureRect.Width >> 1, node.TextureRect.Height >> 1);
				destinationRectangle.X += (destinationRectangle.Width >> 1);
				destinationRectangle.Y += (destinationRectangle.Height >> 1);
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

			spriteBatch.Draw(Atlas, destinationRectangle, sourceRectangle, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, spriteEffects, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, Vector2 rotationOffset, float alpha)
		{
			if (!GetNode(textureId, destinationRectangle, out var node, out _))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, SpriteEffects.None, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, float alpha)
		{
			if (!GetNode(textureId, out var node))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha));
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, double scale, float rotation, float alpha)
		{
			Draw(textureId, spriteBatch, position, scale, rotation, alpha, SpriteEffects.None);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, double scale, float rotation, float alpha, SpriteEffects spriteEffects)
		{
			if (!GetNode(textureId, out var node, ref scale, out _))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
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

			spriteBatch.Draw(Atlas, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, (float)scale, spriteEffects, 1.0f);
		}
	}
}
