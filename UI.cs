using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace ItemSourceHelper {
	public class ItemSourceBrowser : GameInterfaceLayer {
		public ManipulableRectangleElement MainWindow { get; private set; }
		public ItemSourceListGridItem Sources { get; private set; }
		public ItemListGridItem Ingredience { get; private set; }
		public ItemSourceBrowser() : base($"{nameof(ItemSourceHelper)}: Browser", InterfaceScaleType.UI) {
			MainWindow = new() {
				Left = new(100, 0),
				Top = new(100, 0),
				Width = new(200, 0),
				Height = new(150, 0),
				color = Color.CornflowerBlue,
				handles = RectangleHandles.Top | RectangleHandles.Left,
				MinWidth = new(150, 0),
				MinHeight = new(100, 0),
				MarginTop = 4, MarginLeft = 4, MarginBottom = 4, MarginRight = 4,
			};
			MainWindow.Initialize();
			MainWindow.Append(new RoundedRectangleGridElement() {
				items = new() {
					[1] = new GridItem() { color = Color.Coral },
					[2] = Sources = new ItemSourceListGridItem() {
						color = Color.DodgerBlue
					},
					[3] = Ingredience = new ItemListGridItem() {
						color = Color.Tan,
						items = [new(ItemID.AaronsBreastplate), new(ItemID.WorkBench), new(ItemID.Zenith)]
					},
				},
				mergeIDs = new int[3, 3] {
					{ 1, 1, 1 },
					{ -1, 2, 3 },
					{ -1, 2, 3 }
				},
				widths = [new(-4, 1 / 3f), new(-4, 1 / 3f), new(-4, 1 / 3f)],
				heights = [new(-4, 1 / 4f), new(-4, 1 / 2f), new(-4, 1 / 4f)]
			});
		}
		protected override bool DrawSelf() {
			MainWindow.Recalculate();
			MainWindow.Draw(Main.spriteBatch);
			return true;
		}
	}
	public class ItemSourceListGridItem : GridItem {
		public IEnumerable<ItemSource> items;
		int scroll;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			Color color = this.color;
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Width += 20;
			bounds.Height += 10;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				int size = (int)(52 * Main.inventoryScale);
				const int padding = 4;
				int sizeWithPadding = size + padding;

				int minX = bounds.X + padding;
				int baseX = minX - scroll;
				int x = baseX;
				int maxX = bounds.X + bounds.Width - sizeWithPadding;
				int y = bounds.Y + padding;
				int maxY = bounds.Y + bounds.Height - sizeWithPadding;
				int itemsPerRow = bounds.Width - sizeWithPadding;
				bool cutOff = false;
                foreach (ItemSource itemSource in items.Skip(itemsPerRow * scroll)) {
					if (x >= minX - size) {
						Item item = itemSource.Item;
						bool hover = Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size;
						ItemSlot.Draw(spriteBatch, ref item, ItemSlot.Context.CraftingMaterial, new Vector2(x, y));
						if (hover) {
							ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								ItemSourceHelper.Instance.BrowserWindow.Ingredience.items = itemSource.GetSourceItems().ToArray();
							}
						}
					}
					x += sizeWithPadding;
					if (x >= maxX - padding) {
						x = baseX;
						y += sizeWithPadding;
						if (y >= maxY - padding) {
							cutOff = true;
							break;
						}
					}
				}
            }
		}
	}
	public class ItemListGridItem : GridItem {
		public Item[] items;
		int scroll;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			Color color = this.color;
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Width += 20;
			bounds.Height += 10;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				int minX = bounds.X + 4;
				int baseX = minX - scroll;
				int x = baseX;
				int maxX = bounds.X +  bounds.Width - 26;
				int y = bounds.Y + 4;
				int maxY = bounds.Y + bounds.Height - 26;
				bool cutOff = false;
				for (int i = 0; i < items.Length; i++) {
					if (x >= minX - 52) {
						ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.CraftingMaterial, i, new Vector2(x, y));
						if (Main.mouseX >= x && Main.mouseX <= x + 52 && Main.mouseY >= y && Main.mouseY <= y + 52) {
							ItemSlot.MouseHover(items, ItemSlot.Context.CraftingMaterial, i);
						}
					}
					x += 52 + 2;
					if (x >= maxX) {
						x = baseX;
						y += 52 + 2;
						if (y >= maxY) {
							cutOff = true;
							break;
						}
					}
				}
			}
		}
	}
	public class ManipulableRectangleElement : UIElement {
		//public Rectangle area;
		public Color color;
		public RectangleHandles handles;
		bool heldHandleStretch;
		RectangleHandles heldHandle;
		Vector2 heldHandleOffset;
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Texture2D texture = TextureAssets.InventoryBack16.Value;
			Texture2D handleTexture = TextureAssets.InventoryBack13.Value;
			Rectangle textureBounds = texture.Bounds;

			Color handleHoverColor = color * 1.2f;
			Color nonHandleHoverColor = color.MultiplyRGB(new(210, 210, 210));

			/*float parentWidth = Main.screenWidth;
			float parentHeight = Main.screenHeight;
			if (Parent is not null) {
				parentWidth = Parent.GetInnerDimensions().Width;
				parentHeight = Parent.GetInnerDimensions().Height;
			}
			int minWidth = (int)MinWidth.GetValue(parentWidth);
			int minHeight = (int)MinHeight.GetValue(parentHeight);*/

			Rectangle area = GetDimensions().ToRectangle();
			if (heldHandle != 0) {
				Main.LocalPlayer.mouseInterface = true;
				bool changed = false;
				if (heldHandleStretch) {
					if (heldHandle.HasFlag(RectangleHandles.Left)) {
						int diff = (int)(Left.Pixels - Main.MouseScreen.X + heldHandleOffset.X);
						Left.Pixels -= diff;
						Width.Pixels += diff;
						changed = true;
						//area.X -= diff;
						//area.Width += diff;
						//if (area.Width < minWidth) area.Width = minWidth;
					} else if (heldHandle.HasFlag(RectangleHandles.Right)) {
						Width.Pixels = (int)Main.MouseScreen.X + heldHandleOffset.X - area.X;
						changed = true;
						//area.Width = (int)Main.MouseScreen.X - area.X;
						//if (area.Width < minWidth) area.Width = minWidth;
					}
					if (heldHandle.HasFlag(RectangleHandles.Top)) {
						int diff = (int)(Top.Pixels - Main.MouseScreen.X + heldHandleOffset.Y);
						Top.Pixels -= diff;
						Height.Pixels += diff;
						changed = true;
						//int diff = area.Y - (int)Main.MouseScreen.Y;
						//area.Y -= diff;
						//area.Height += diff;
						//if (area.Height < minHeight) area.Height = minHeight;
					} else if (heldHandle.HasFlag(RectangleHandles.Bottom)) {
						Height.Pixels = (int)Main.MouseScreen.Y + heldHandleOffset.Y - area.Y;
						changed = true;
						//area.Height = (int)Main.MouseScreen.Y - area.Y;
						//if (area.Height < minHeight) area.Height = minHeight;
					}
				} else {
					Vector2 pos = Main.MouseScreen + heldHandleOffset;
					Left.Pixels = (int)pos.X;
					Top.Pixels = (int)pos.Y;
				}
				if (!Main.mouseLeft) {
					heldHandle = 0;
				}
				if (changed) {
					Recalculate();
					area = GetDimensions().ToRectangle();
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
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								heldHandleStretch = !matches;
								heldHandle = segment.Handles;
								if (matches) {
									heldHandleOffset = new Vector2(Left.Pixels, Top.Pixels) - Main.MouseScreen;
								} else {
									heldHandleOffset = bounds.TopLeft() - Main.MouseScreen + bounds.Size();
								}
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
	public class RoundedRectangleGridElement : UIElement {
		public Dictionary<int, GridItem> items;
		public int[,] mergeIDs;
		public StyleDimension[] widths;
		public StyleDimension[] heights;
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Texture2D texture = TextureAssets.InventoryBack13.Value;
			int hCells = mergeIDs.GetLength(0);
			int vCells = mergeIDs.GetLength(1);
			Rectangle textureBounds = texture.Bounds;
			int parentX = 0;
			int parentY = 0;
			int parentWidth = 300;
			int parentHeight = 200;
			int padding = 4;
			if (Parent is not null) {
				CalculatedStyle parent = Parent.GetInnerDimensions();
				parentX = (int)parent.X;
				parentY = (int)parent.Y;
				parentWidth = (int)parent.Width - padding;
				parentHeight = (int)parent.Height - padding;
			}
			Dictionary<int, (Rectangle bounds, GridItem item)> processedIDs = [];
			float cellY = parentY + padding;
			for (int y = 0; y < vCells; y++) {
				float cellX = parentX + padding;
				float height = heights[y].GetValue(parentHeight);
				int boxY = (int)cellY;
				cellY += height + padding;
				if (height == 0) continue;
				for (int x = 0; x < hCells; x++) {
					int mergeID = mergeIDs[x, y];
					float width = widths[x].GetValue(parentWidth);
					int boxX = (int)cellX;
					cellX += width + padding;
					if (mergeID == -1 || processedIDs.ContainsKey(mergeID)) continue;
					if (width == 0) continue;
					int currentX = x;
					while (ShouldMerge(currentX, y, currentX + 1, y)) {
						width += widths[currentX].GetValue(parentWidth) + padding;
						currentX++;
					}
					int currentY = y;
					while (ShouldMerge(x, currentY, x, currentY + 1)) {
						height += heights[currentY].GetValue(parentHeight) + padding;
						currentY++;
					}
					Rectangle bounds = new(boxX, boxY, (int)width, (int)height);
					processedIDs.Add(mergeID, (bounds, items[mergeID]));
				}
			}
			RasterizerState rasterizerState = spriteBatch.GraphicsDevice.RasterizerState;
			Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
			foreach (var item in processedIDs.Values) {
				//using (new UIMethods.ClippingRectangle(item.bounds, spriteBatch)) {
					item.item.DrawSelf(item.bounds, spriteBatch);
				//}
			}
		}
		public bool ShouldMerge(int aX, int aY, int bX, int bY) {
			if (aX < 0 || aY < 0 || bX < 0 || bY < 0) return false;
			int hCells = mergeIDs.GetLength(0);
			int vCells = mergeIDs.GetLength(1);
			if (aX >= hCells || aY >= vCells || bX >= hCells || bY >= vCells) return false;
			return mergeIDs[aX, aY] == mergeIDs[bX, bY];
		}
	}
	public class GridItem {
		public Color color;
		public virtual void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			Color color = this.color;
			if (bounds.Contains(Main.MouseScreen.ToPoint())) {
				color *= 1.2f;
			}
			spriteBatch.DrawRoundedRetangle(bounds, color);
		}
	}
	public static class UIMethods {
		public static void DrawRoundedRetangle(this SpriteBatch spriteBatch, Rectangle rectangle, Color color) {
			Texture2D texture = TextureAssets.InventoryBack13.Value;
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
		public class ClippingRectangle : IDisposable {
			readonly Rectangle scissorRectangle;
			readonly RasterizerState rasterizerState;
			readonly SpriteBatch spriteBatch;
			public ClippingRectangle(Rectangle area, SpriteBatch spriteBatch) {
				this.spriteBatch = spriteBatch;
				scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
				rasterizerState = spriteBatch.GraphicsDevice.RasterizerState;

				spriteBatch.End();
				Rectangle adjustedClippingRectangle = Rectangle.Intersect(area, spriteBatch.GraphicsDevice.ScissorRectangle);
				spriteBatch.GraphicsDevice.ScissorRectangle = adjustedClippingRectangle;
				spriteBatch.GraphicsDevice.RasterizerState = OverflowHiddenRasterizerState;
				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, OverflowHiddenRasterizerState, null, Main.UIScaleMatrix);
			}
			public void Dispose() {
				spriteBatch.End();
				spriteBatch.GraphicsDevice.ScissorRectangle = scissorRectangle;
				spriteBatch.GraphicsDevice.RasterizerState = rasterizerState;
				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
			}
			private static readonly RasterizerState OverflowHiddenRasterizerState = new() {
				CullMode = CullMode.None,
				ScissorTestEnable = true
			};
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
