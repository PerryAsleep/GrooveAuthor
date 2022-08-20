using System.Collections.Generic;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class TextureAtlas
	{
		public class PackNode
		{
			private PackNode[] Children = new PackNode[2];
			private Rectangle Rect;

			public Rectangle TextureRect;
			public Texture2D Texture;

			public PackNode(Rectangle rect)
			{
				Rect = rect;
			}

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

		private GraphicsDevice GraphicsDevice;
		private RenderTarget2D Atlas;
		private PackNode Root;
		private int Padding;
		private Dictionary<string, PackNode> TextureNodes = new Dictionary<string, PackNode>();
		private Dictionary<string, PackNode> DirtyNodes = new Dictionary<string, PackNode>();

		public TextureAtlas(GraphicsDevice graphicsDevice, int width, int height, int padding)
		{
			GraphicsDevice = graphicsDevice;
			Atlas = new RenderTarget2D(GraphicsDevice, width, height);
			Padding = padding;
			Root = new PackNode(new Rectangle(0, 0, width, height));
		}

		public void AddTexture(string textureId, Texture2D texture)
		{
			// TODO: Mip maps?

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

			var node = Root.Pack(texture, Padding);
			if (node == null)
			{
				Logger.Warn($"Failed to pack texture \"{textureId}\" into TextureAtlas. Not enough space.");
				return;
			}

			TextureNodes.Add(textureId, node);
			DirtyNodes.Add(textureId, node);
		}

		public void Update()
		{
			if (DirtyNodes.Count == 0)
				return;

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

					// Right padding.
					sb.Draw(node.Texture,
						new Rectangle(node.TextureRect.X + node.TextureRect.Width, node.TextureRect.Y, 1, node.TextureRect.Height),
						new Rectangle(node.TextureRect.Width - 1, 0, 1, node.TextureRect.Height),
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

				node.Texture = null;
			}
			sb.End();
			GraphicsDevice.SetRenderTarget(null);
			DirtyNodes.Clear();
		}

		public (int, int) GetDimensions(string textureId)
		{
			if (!TextureNodes.TryGetValue(textureId, out var node))
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
			if (!TextureNodes.TryGetValue(textureId, out var node))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha));
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, float alpha)
		{
			Draw(textureId, spriteBatch, destinationRectangle, rotation, alpha, SpriteEffects.None);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, Vector2 rotationOffset, float alpha, SpriteEffects spriteEffects)
		{
			Draw(textureId, spriteBatch, destinationRectangle, rotation, rotationOffset, alpha, SpriteEffects.None);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, float alpha, SpriteEffects spriteEffects)
		{
			if (!TextureNodes.TryGetValue(textureId, out var node))
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

			spriteBatch.Draw(Atlas, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, spriteEffects, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle sourceRectangle, Rectangle destinationRectangle, float rotation, float alpha, SpriteEffects spriteEffects)
		{
			if (!TextureNodes.TryGetValue(textureId, out var node))
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

			sourceRectangle.X += node.TextureRect.X;
			sourceRectangle.Y += node.TextureRect.Y;

			spriteBatch.Draw(Atlas, destinationRectangle, sourceRectangle, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, spriteEffects, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Rectangle destinationRectangle, float rotation, Vector2 rotationOffset, float alpha)
		{
			if (!TextureNodes.TryGetValue(textureId, out var node))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, destinationRectangle, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset,
				SpriteEffects.None, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, float alpha)
		{
			if (!TextureNodes.TryGetValue(textureId, out var node))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha));
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, float scale, float rotation, float alpha)
		{
			Draw(textureId, spriteBatch, position, scale, rotation, alpha, SpriteEffects.None);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, float scale, float rotation, float alpha, SpriteEffects spriteEffects)
		{
			if (!TextureNodes.TryGetValue(textureId, out var node))
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
				position.X += node.TextureRect.Width * scale * 0.5f;
				position.Y += node.TextureRect.Height * scale * 0.5f;
			}

			spriteBatch.Draw(Atlas, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, scale,
				spriteEffects, 1.0f);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, float scale, float rotation, Vector2 rotationOffset, float alpha)
		{
			Draw(textureId, spriteBatch, position, scale, rotation, rotationOffset, alpha, SpriteEffects.None);
		}

		public void Draw(string textureId, SpriteBatch spriteBatch, Vector2 position, float scale, float rotation, Vector2 rotationOffset, float alpha, SpriteEffects spriteEffects)
		{
			if (!TextureNodes.TryGetValue(textureId, out var node))
			{
				Logger.Warn($"Failed to draw packed texture identified by \"{textureId}\". No texture with that id found.");
				return;
			}

			spriteBatch.Draw(Atlas, position, node.TextureRect, new Color(1.0f, 1.0f, 1.0f, alpha), rotation, rotationOffset, scale,
				spriteEffects, 1.0f);
		}
	}
}
