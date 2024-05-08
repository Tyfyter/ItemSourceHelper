using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace ItemSourceHelper {
	public class ItemSourceBrowser : GameInterfaceLayer {
		public ManipulableRectangleElement MainWindow { get; private set; }
		public ItemSourceBrowser() : base($"{nameof(ItemSourceHelper)}: Browser", InterfaceScaleType.UI) {
			MainWindow = new() {
				area = new Rectangle(100, 100, 200, 150),
				color = Color.CornflowerBlue,
				handles = RectangleHandles.Top | RectangleHandles.Left,
				minSize = new(150, 100)
			};
		}
		protected override bool DrawSelf() {
			MainWindow.Draw(Main.spriteBatch);
			return true;
		}
	}
	public class ManipulableRectangleElement : UIElement {
		public Rectangle area;
		public Color color;
		public RectangleHandles handles;
		public Point minSize;
		bool heldHandleStretch;
		RectangleHandles heldHandle;
		Vector2 heldHandleOffset;
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Texture2D texture = TextureAssets.InventoryBack16.Value;
			Texture2D handleTexture = TextureAssets.InventoryBack13.Value;
			Rectangle textureBounds = texture.Bounds;

			Color handleHoverColor = color * 1.2f;
			Color nonHandleHoverColor = color.MultiplyRGB(new(210, 210, 210));
			if (heldHandle != 0) {
				Main.LocalPlayer.mouseInterface = true;
				if (heldHandleStretch) {
					if (heldHandle.HasFlag(RectangleHandles.Left)) {
						int diff = area.X - (int)Main.MouseScreen.X;
						area.X -= diff;
						area.Width += diff;
						if (area.Width < minSize.X) area.Width = minSize.X;
					} else if (heldHandle.HasFlag(RectangleHandles.Right)) {
						area.Width = (int)Main.MouseScreen.X - area.X;
						if (area.Width < minSize.X) area.Width = minSize.X;
					}
					if (heldHandle.HasFlag(RectangleHandles.Top)) {
						int diff = area.Y - (int)Main.MouseScreen.Y;
						area.Y -= diff;
						area.Height += diff;
						if (area.Height < minSize.Y) area.Height = minSize.Y;
					} else if (heldHandle.HasFlag(RectangleHandles.Bottom)) {
						area.Height = (int)Main.MouseScreen.Y - area.Y;
						if (area.Height < minSize.Y) area.Height = minSize.Y;
					}
				} else {
					Vector2 pos = Main.MouseScreen + heldHandleOffset;
					area.X = (int)pos.X;
					area.Y = (int)pos.Y;
				}
				if (!Main.mouseLeft) {
					heldHandle = 0;
				}
			}
			foreach (var segment in UIMethods.rectangleSegments) {
				Rectangle bounds = segment.GetBounds(area);
				bool matches = segment.Matches(handles);
				Color partColor = color;
				bool discolor = false;
				if (heldHandle == 0) {
					if (bounds.Contains(Main.MouseScreen.ToPoint())) {
						Main.LocalPlayer.mouseInterface = true;
						if (segment.Handles != 0) {
							discolor = true;
							if (Main.mouseLeft) {
								heldHandleStretch = !matches;
								heldHandle = segment.Handles;
								heldHandleOffset = new Vector2(area.X, area.Y) - Main.MouseScreen;
							}
						}
					}
				}else if (segment.Handles == heldHandle) {
					Main.LocalPlayer.mouseInterface = true;
					discolor = true;
				}
				if (discolor) {
					partColor = matches ? handleHoverColor : nonHandleHoverColor;
				}

				Main.spriteBatch.Draw(
					matches ? handleTexture : texture,
					bounds,
					segment.GetBounds(textureBounds),
					partColor
				);
			}
		}
	}
	public static class UIMethods {
		public static void DrawRoundedRetangle(Rectangle rectangle, Color color) {
			Texture2D texture = TextureAssets.InventoryBack16.Value;
			Rectangle textureBounds = texture.Bounds;
			foreach (var segment in rectangleSegments) {
				Main.spriteBatch.Draw(
					texture,
					segment.GetBounds(rectangle),
					segment.GetBounds(textureBounds),
					color
				);
			}

			/*Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Top | RectangleHandles.Left) ? handleTexture : texture, new Rectangle(x, y, 10, 10), cornerFrame, color, 0, default, SpriteEffects.None, 0);
			Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Top | RectangleHandles.Right) ? handleTexture : texture, new Rectangle(x + width - 10, y, 10, 10), cornerFrame, color, 0, default, SpriteEffects.FlipHorizontally, 0);
			Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Bottom | RectangleHandles.Left) ? handleTexture : texture, new Rectangle(x, y + height - 10, 10, 10), cornerFrame, color, 0, default, SpriteEffects.FlipVertically, 0);
			Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Bottom | RectangleHandles.Right) ? handleTexture : texture, new Rectangle(x + width - 10, y + height - 10, 10, 10), cornerFrame, color, 0, default, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0);

			Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Top) ? handleTexture : texture, new Rectangle(x + 10, y, width - 10 * 2, 10), topFrame, color, 0, default, SpriteEffects.None, 0);
			Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Bottom) ? handleTexture : texture, new Rectangle(x + 10, y + height - 10, width - 10 * 2, 10), topFrame, color, 0, default, SpriteEffects.FlipVertically, 0);

			Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Left) ? handleTexture : texture, new Rectangle(x, y + 10, 10, height - 10 * 2), leftFrame, color, 0, default, SpriteEffects.None, 0);
			Main.spriteBatch.Draw(handles.HasFlag(RectangleHandles.Right) ? handleTexture : texture, new Rectangle(x + width - 10, y + 10, 10, height - 10 * 2), leftFrame, color, 0, default, SpriteEffects.FlipHorizontally, 0);

			Main.spriteBatch.Draw(texture, new Rectangle(x + 10, y + 10, width - 10 * 2, height - 10 * 2), centerFrame, color, 0, default, SpriteEffects.FlipHorizontally, 0);*/
		}
		public static void DrawColoredItemSlot(SpriteBatch spriteBatch, ref Item item, Vector2 position, Texture2D backTexture, Color slotColor, Color lightColor = default, Color textColor = default, string beforeText = null, string afterText = null) {
			spriteBatch.Draw(backTexture, position, null, slotColor, 0f, Vector2.Zero, Main.inventoryScale, SpriteEffects.None, 0f);
			if (beforeText is not null) {
				Terraria.UI.Chat.ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, beforeText, position + new Vector2(8f, 4f) * Main.inventoryScale, textColor, 0f, Vector2.Zero, new Vector2(Main.inventoryScale), -1f, Main.inventoryScale);
			}
			ItemSlot.Draw(spriteBatch, ref item, ItemSlot.Context.ChatItem, position, lightColor);
			if (afterText is not null) {
				Terraria.UI.Chat.ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, afterText, position + new Vector2(8f, 4f) * Main.inventoryScale, textColor, 0f, Vector2.Zero, new Vector2(Main.inventoryScale), -1f, Main.inventoryScale);
			}
		}
		public static StretchSegment[] rectangleSegments = [
			new(StretchLength.Position, StretchLength.Position, new(0, 0, 10), new(0, 0, 10), RectangleHandles.Top | RectangleHandles.Left),
			new(StretchLength.Position, new(1, 1, -10), new(0, 0, 10), new(0, 0, 10), RectangleHandles.Bottom | RectangleHandles.Left),
			new(new(1, 1, -10), StretchLength.Position, new(0, 0, 10), new(0, 0, 10), RectangleHandles.Top | RectangleHandles.Right),
			new(new(1, 1, -10), new(1, 1, -10), new(0, 0, 10), new(0, 0, 10), RectangleHandles.Bottom | RectangleHandles.Right),

			new(new(1, 0, 10), StretchLength.Position, new(0, 1, -10 * 2), new(0, 0, 10), RectangleHandles.Top),
			new(new(1, 0, 10), new(1, 1, -10), new(0, 1, -10 * 2), new(0, 0, 10), RectangleHandles.Bottom),

			new(StretchLength.Position, new(1, 0, 10), new(0, 0, 10), new(0, 1, -10 * 2), RectangleHandles.Left),
			new(new(1, 1, -10), new(1, 0, 10), new(0, 0, 10), new(0, 1, -10 * 2), RectangleHandles.Right),

			new(new(1, 0, 10), new(1, 0, 10), new(0, 1, -10 * 2), new(0, 1, -10 * 2), 0)
		];
		internal static void Unload() {
			rectangleSegments = null;
		}
		public record struct StretchSegment(StretchLength Left, StretchLength Top, StretchLength Width, StretchLength Height, RectangleHandles Handles = 0) {
			public readonly Rectangle GetBounds(Rectangle parent) => new(
				(int)Left.GetValue(parent.X, parent.Width),
				(int)Top.GetValue(parent.Y, parent.Height),
				(int)Width.GetValue(parent.X, parent.Width),
				(int)Height.GetValue(parent.Y, parent.Height)
			);
			public readonly bool Matches(RectangleHandles handles) {
				if (Handles == 0) return false;
				return (Handles & handles) == Handles;
			}
		}
		public struct StretchLength(float PositionFactor, float SizeFactor, float Flat) {
			public static StretchLength Position = new(1, 0, 0);
			public readonly float GetValue(float position, float size) => position * PositionFactor + size * SizeFactor + Flat;
		}

	}
	[Flags]
	public enum RectangleHandles : byte {
		Top = 1 << 0,
		Left = 1 << 1,
		Bottom = 1 << 2,
		Right = 1 << 3,
	}
}
