using ItemSourceHelper.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ItemSourceHelper {
	public class ItemSourceBrowser : GameInterfaceLayer {
		public WindowElement MainWindow { get; private set; }
		public ItemSourceListGridItem Sources { get; private set; }
		public IngredientListGridItem Ingredience { get; private set; }
		public ItemSourceEnumerable ActiveFilters { get; private set; }
		public SingleSlotGridItem FilterItem { get; private set; }
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
				items = new() {
					[1] = new FilterListGridItem() {
						color = Color.LightCoral,
						filters = ItemSourceHelper.Instance.Filters
					},
					[2] = Sources = new ItemSourceListGridItem() {
						color = Color.DodgerBlue,
						items = ActiveFilters = new ItemSourceEnumerable()
					},
					[3] = Ingredience = new IngredientListGridItem() {
						color = Color.Tan,
						items = []
					},
					[4] = FilterItem = new SingleSlotGridItem() {
						color = new(72, 67, 159)
					},
				},
				mergeIDs = new int[3, 3] {
					{ 4, -1, -1 },
					{ 1, 2, 3 },
					{ 1, 2, 3 }
				},
				WidthWeights = new([1, 3, 3]),
				HeightWeights = new([1, 3, 1]),
				MinWidths = new([51, 124, 124]),
				MinHeights = new([51, 132, 51]),
			};
			MainWindow.Initialize();
		}
		protected override bool DrawSelf() {
			float inventoryScale = Main.inventoryScale;
			Main.inventoryScale = 0.75f;
			MainWindow.Recalculate();
			MainWindow.Draw(Main.spriteBatch);
			Main.inventoryScale = inventoryScale;
			return true;
		}
	}
	public class FilterListGridItem : GridItem, IScrollableUIItem {
		public IEnumerable<ItemSourceFilter> filters;
		int scroll;
		bool cutOff;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Height -= 1;
			Texture2D actuator = TextureAssets.Actuator.Value;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				bool canHover = bounds.Contains(Main.mouseX, Main.mouseY);
				int size = (int)(32 * Main.inventoryScale);
				const int padding = 2;
				int sizeWithPadding = size + padding;

				int minX = bounds.X + 8;
				int x = minX - scroll * sizeWithPadding;
				int maxX = bounds.X + bounds.Width - size / 2;
				int y = bounds.Y + 6;
				int maxY = bounds.Y + bounds.Height - size / 2;
				cutOff = false;
				Point mousePos = Main.MouseScreen.ToPoint();
				Color color = new(0, 0, 0, 50);
				Color hiColor = new(50, 50, 50, 0);
				ItemSourceEnumerable activeFilters = ItemSourceHelper.Instance.BrowserWindow.ActiveFilters;
				Rectangle button = new(x, y, size, size);
				if (activeFilters.filters.Count != 0) {
					if (canHover && button.Contains(mousePos)) {
						spriteBatch.Draw(actuator, button, Color.Pink);
						if (Main.mouseLeft && Main.mouseLeftRelease) {
							activeFilters.filters.Clear();
						}
					} else {
						spriteBatch.Draw(actuator, button, Color.Red);
					}
					x += sizeWithPadding;
					minX += sizeWithPadding;
				}

				foreach (ItemSourceFilter filter in filters) {
					if (x >= minX - size) {
						button.X = x;
						button.Y = y;
						int index = activeFilters.filters.IndexOf(filter);
						if (button.Contains(mousePos)) {
							UIMethods.DrawRoundedRetangle(spriteBatch, button, hiColor);
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								if (index == -1) {
									activeFilters.filters.Add(filter);
								} else {
									activeFilters.filters.RemoveAt(index);
								}
							}
						} else {
							UIMethods.DrawRoundedRetangle(spriteBatch, button, color);
						}
						Texture2D texture = filter.TextureAsset.Value;
						spriteBatch.Draw(texture, texture.Size().RectWithinCentered(button, 8), Color.White);
						if (index != -1) {
							Rectangle corner = button;
							int halfWidth = corner.Width / 2;
							corner.X += halfWidth;
							corner.Width -= halfWidth;
							corner.Height /= 2;
							spriteBatch.Draw(actuator, corner, Color.Green);
						}
					}
					x += sizeWithPadding;
					if (x >= maxX) {
						cutOff = true;
					}
				}
			}
		}
		public void Scroll(int direction) {
			if (!cutOff && direction > 0) return;
			if (scroll <= 0 && direction < 0) return;
			scroll += direction;
		}
	}
	public class ItemSourceListGridItem : GridItem, IScrollableUIItem {
		public IEnumerable<ItemSource> items;
		int scroll;
		bool cutOff = false;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			Color color = this.color;
			spriteBatch.DrawRoundedRetangle(bounds, color);
			//bounds.X += 12;
			//bounds.Y += 10;
			//bounds.Width -= 8;
			bounds.Height -= 1;
			Point mousePos = Main.MouseScreen.ToPoint();
			if (bounds.Contains(mousePos)) this.CaptureScroll();
			cutOff = false;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				bool canHover = bounds.Contains(Main.mouseX, Main.mouseY);
				Texture2D texture = TextureAssets.InventoryBack13.Value;
				int size = (int)(52 * Main.inventoryScale);
				const int padding = 2;
				int sizeWithPadding = size + padding;

				int minX = bounds.X + 8;
				int baseX = minX;
				int x = baseX;
				int maxX = bounds.X + bounds.Width - size;
				int y = bounds.Y + 6;
				int maxY = bounds.Y + bounds.Height - size / 2;
				int itemsPerRow = bounds.Width / sizeWithPadding;
				Vector2 position = new();
				Color normalColor = new(72, 67, 159);
				Color hoverColor = Color.CornflowerBlue;
				foreach (ItemSource itemSource in items.Skip(itemsPerRow * scroll)) {
					if (x >= minX - size) {
						Item item = itemSource.Item;
						position.X = x;
						position.Y = y;
						bool hover = canHover && Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size;
						UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, texture, hover ? hoverColor : normalColor);
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

		public void Scroll(int direction) {
			if (!cutOff && direction > 0) return;
			if (scroll <= 0 && direction < 0) return;
			scroll += direction;
		}
	}
	public class IngredientListGridItem : GridItem, IScrollableUIItem {
		public Item[] items;
		int scroll;
		bool cutOff;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Height -= 1;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				bool canHover = bounds.Contains(Main.mouseX, Main.mouseY);
				int size = (int)(52 * Main.inventoryScale);
				const int padding = 3;
				int sizeWithPadding = size + padding;

				int minX = bounds.X + 8;
				int baseX = minX - scroll * sizeWithPadding;
				int x = baseX;
				int maxX = bounds.X +  bounds.Width - size / 2;
				int y = bounds.Y + 6;
				int maxY = bounds.Y + bounds.Height - size / 2;
				cutOff = false;
				Vector2 position = new();
				for (int i = 0; i < items.Length; i++) {
					if (x >= minX - size) {
						position.X = x;
						position.Y = y;
						ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.CraftingMaterial, i, position);
						if (canHover && Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size) {
							ItemSlot.MouseHover(items, ItemSlot.Context.CraftingMaterial, i);
						}
					}
					x += sizeWithPadding;
					if (x >= maxX) {
						x = baseX;
						y += sizeWithPadding;
						if (y >= maxY) {
							cutOff = true;
							break;
						}
					}
				}
			}
		}
		public void Scroll(int direction) {
			if (!cutOff && direction > 0) return;
			if (scroll <= 0 && direction < 0) return;
			scroll += direction;
		}
	}
	public class ItemSourceEnumerable : IEnumerable<ItemSource> {
		public List<ItemSourceFilter> filters = [];
		public IEnumerator<ItemSource> GetEnumerator() {
			for (int i = 0; i < ItemSourceHelper.Instance.Sources.Count; i++) {
				ItemSource source = ItemSourceHelper.Instance.Sources[i];
				if (!MatchesSlot(source)) goto cont;
				for (int j = 0; j < filters.Count; j++) if (!filters[j].Matches(source)) goto cont;
				yield return source;
				cont:;
			}
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}
		public static bool MatchesSlot(ItemSource source) {
			Item item = ItemSourceHelper.Instance.BrowserWindow.FilterItem.item;
			if (item.IsAir) return true;
			if (source.ItemType == item.type) return true;
			foreach (Item ingredient in source.GetSourceItems()) {
				if (ingredient.type == item.type) return true;
			}
			return false;
		}
	}
	public class SingleSlotGridItem : GridItem {
		public Item item;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			item ??= new();
			Vector2 size = new(52 * Main.inventoryScale);
			Vector2 pos = bounds.Center() - size * 0.5f;
			UIMethods.DrawColoredItemSlot(
				spriteBatch,
				ref item,
				pos,
				TextureAssets.InventoryBack13.Value,
				color
			);
			if (UIMethods.MouseInArea(pos, size)) {
				ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
				if (Main.mouseLeft && Main.mouseLeftRelease) {
					if (item.type == Main.mouseItem ?.type) {
						item.TurnToAir();
					} else {
						item = Main.mouseItem?.Clone() ?? new();
					}
				}
			}
		}
	}
	public class WindowElement : UIElement {
		#region resize
		public Color color;
		public RectangleHandles handles;
		bool heldHandleStretch;
		RectangleHandles heldHandle;
		Vector2 heldHandleOffset;
		public override void OnInitialize() {
			heights = new float[HeightWeights.Length];
			widths = new float[WidthWeights.Length];
			CheckSizes();
			Resize();
		}
		void Resize() {
			float margins = MarginLeft + MarginRight;
			float minSize = Math.Max(calculatedMinWidth, MinWidth.Pixels) + margins;
			if (Width.Pixels < minSize) Width.Pixels = minSize;
			float innerWidth = Width.Pixels - margins;

			margins = MarginTop + MarginBottom;
			minSize = Math.Max(calculatedMinHeight, MinHeight.Pixels) + margins;
			if (Height.Pixels < minSize) Height.Pixels = minSize;
			float innerHeight = Height.Pixels - margins;

			for (int i = 0; i < WidthWeights.Length; i++) {
				float width = WidthWeights[i] * (innerWidth / totalWidthWeight);
				widths[i] = width;
			}
			for (int i = 0; i < HeightWeights.Length; i++) {
				float height = HeightWeights[i] * (innerHeight / totalHeightWeight);
				heights[i] = height;
			}
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Texture2D texture = TextureAssets.InventoryBack16.Value;
			Texture2D handleTexture = TextureAssets.InventoryBack13.Value;
			Rectangle textureBounds = texture.Bounds;

			Color handleHoverColor = color * 1.2f;
			Color nonHandleHoverColor = color.MultiplyRGB(new(210, 210, 210));

			Rectangle area = GetOuterDimensions().ToRectangle();
			if (heldHandle != 0) {
				Main.LocalPlayer.mouseInterface = true;
				bool changed = false;
				if (heldHandleStretch) {
					if (heldHandle.HasFlag(RectangleHandles.Left)) {
						int diff = (int)(Left.Pixels - Main.MouseScreen.X + heldHandleOffset.X);
						Left.Pixels -= diff;
						Width.Pixels += diff;
						changed = true;
					} else if (heldHandle.HasFlag(RectangleHandles.Right)) {
						Width.Pixels = (int)Main.MouseScreen.X + heldHandleOffset.X - area.X;
						changed = true;
					}
					if (heldHandle.HasFlag(RectangleHandles.Top)) {
						int diff = (int)(Top.Pixels - Main.MouseScreen.X + heldHandleOffset.Y);
						Top.Pixels -= diff;
						Height.Pixels += diff;
						changed = true;
					} else if (heldHandle.HasFlag(RectangleHandles.Bottom)) {
						Height.Pixels = (int)Main.MouseScreen.Y + heldHandleOffset.Y - area.Y;
						changed = true;
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
					Resize();
					Recalculate();
					area = GetOuterDimensions().ToRectangle();
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
				} else if (segment.Handles == heldHandle) {
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
			DrawCells(spriteBatch);
		}
		#endregion resize
		#region grid
		float totalWidthWeight;
		float totalHeightWeight;
		float calculatedMinWidth;
		float calculatedMinHeight;
		public Dictionary<int, GridItem> items;
		public int[,] mergeIDs;
		float[] widths;
		float[] heights;
		public ObservableArray<float> WidthWeights;
		public ObservableArray<float> HeightWeights;
		public ObservableArray<float> MinWidths;
		public ObservableArray<float> MinHeights;
		public override void Update(GameTime gameTime) {
			CheckSizes();
		}
		void CheckSizes() {
			bool anyChanged = false;
			if (WidthWeights.Changed) {
				totalWidthWeight = 0;
				for (int i = 0; i < WidthWeights.Length; i++) totalWidthWeight += WidthWeights[i];
				anyChanged = true;
			}
			if (HeightWeights.Changed) {
				totalHeightWeight = 0;
				for (int i = 0; i < HeightWeights.Length; i++) totalHeightWeight += HeightWeights[i];
				anyChanged = true;
			}
			if (WidthWeights.Changed || MinWidths.Changed) {
				WidthWeights.ConsumeChange();
				MinWidths.ConsumeChange();
				calculatedMinWidth = 0;
				for (int i = 0; i < WidthWeights.Length; i++) {
					float value = MinWidths[i] * (totalWidthWeight / WidthWeights[i]);
					if (calculatedMinWidth < value) calculatedMinWidth = value;
				}
				anyChanged = true;
			}
			if (HeightWeights.Changed || MinHeights.Changed) {
				HeightWeights.ConsumeChange();
				MinHeights.ConsumeChange();
				calculatedMinHeight = 0;
				for (int i = 0; i < HeightWeights.Length; i++) {
					float value = MinHeights[i] * (totalHeightWeight / HeightWeights[i]);
					if (calculatedMinHeight < value) calculatedMinHeight = value;
				}
				anyChanged = true;
			}
			if (anyChanged) Resize();
		}
		public void DrawCells(SpriteBatch spriteBatch) {
			int hCells = mergeIDs.GetLength(0);
			int vCells = mergeIDs.GetLength(1);
			CalculatedStyle style = GetInnerDimensions();
			int padding = 4;
			int baseX = (int)style.X;
			int baseY = (int)style.Y;
			Dictionary<int, (Rectangle bounds, GridItem item)> processedIDs = [];
			float cellY = baseY;
			for (int y = 0; y < vCells; y++) {
				float cellX = baseX;
				float height = heights[y];
				int boxY = (int)cellY;
				cellY += height + padding;
				if (height == 0) continue;
				for (int x = 0; x < hCells; x++) {
					int mergeID = mergeIDs[x, y];
					float width = widths[x];
					int boxX = (int)cellX;
					cellX += width + padding;
					if (mergeID == -1 || processedIDs.ContainsKey(mergeID)) continue;
					if (width == 0) continue;
					int currentX = x;
					while (ShouldMerge(currentX, y, currentX + 1, y)) {
						width += widths[currentX] + padding;
						currentX++;
					}
					int currentY = y;
					while (ShouldMerge(x, currentY, x, currentY + 1)) {
						height += heights[currentY] + padding;
						currentY++;
					}
					Rectangle bounds = new(boxX, boxY, (int)width, (int)height);
					processedIDs.Add(mergeID, (bounds, items[mergeID]));
				}
			}
			foreach (var item in processedIDs.Values) {
				item.item.DrawSelf(item.bounds, spriteBatch);
			}
		}
		public bool ShouldMerge(int aX, int aY, int bX, int bY) {
			if (aX < 0 || aY < 0 || bX < 0 || bY < 0) return false;
			int hCells = mergeIDs.GetLength(0);
			int vCells = mergeIDs.GetLength(1);
			if (aX >= hCells || aY >= vCells || bX >= hCells || bY >= vCells) return false;
			return mergeIDs[aX, aY] == mergeIDs[bX, bY];
		}
		#endregion grid
	}
	public class ObservableArray<T>(T[] values) {
		public bool Changed { get; private set; } = true;
		readonly EqualityComparer<T> comparer = EqualityComparer<T>.Default;
		public int Length => values.Length;
		public T this[int index] {
			get => values[index];
			set {
				if (!comparer.Equals(values[index], value)) {
					values[index] = value;
					Changed = true;
				}
			}
		}
		public void ConsumeChange() => Changed = false;
		public static implicit operator ObservableArray<T>(T[] values) => new(values);
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
		}
		public static Rectangle RectWithinCentered(this Vector2 a, Rectangle bounds, float margin = 4) {
			float factor = Math.Min((bounds.Width - margin) / a.X, (bounds.Width - margin) / a.Y);
			if (factor != 1) {
				a.X *= factor;
				a.Y *= factor;
			}
			return new(
				bounds.X + (int)(bounds.Width - a.X) / 2,
				bounds.Y + (int)(bounds.Height - a.Y) / 2,
				(int)a.X,
				(int)a.Y
			);
		}
		public static Rectangle FitWithinCentered(this Rectangle a, Rectangle bounds, float margin = 4) {
			float factor = Math.Min((bounds.Width - margin) / (float)a.Height, (bounds.Width - margin) / (float)a.Height);
			if (factor != 1) {
				a.Width = (int)(a.Width * factor);
				a.Height = (int)(a.Height * factor);
			}
			a.X = bounds.X + (bounds.Width - a.Width) / 2;
			a.Y = bounds.Y + (bounds.Height - a.Height) / 2;
			return a;
		}
		public static bool MouseInArea(Vector2 pos, Vector2 size) {
			return Main.mouseX >= pos.X && Main.mouseX <= pos.X + size.X && Main.mouseY >= pos.Y && Main.mouseY <= pos.Y + size.Y;
		}
		public static Rectangle Scale(this Rectangle rectangle) {
			Main.UIScaleMatrix.Decompose(out Vector3 scale, out _, out _);
			rectangle.X = (int)(rectangle.X * scale.X);
			rectangle.Y = (int)(rectangle.Y * scale.Y);
			rectangle.Width = (int)(rectangle.Width * scale.X);
			rectangle.Height = (int)(rectangle.Height * scale.Y);
			return rectangle;
		}
		public static void DrawColoredItemSlot(SpriteBatch spriteBatch, ref Item item, Vector2 position, Texture2D backTexture, Color slotColor, Color lightColor = default, Color textColor = default, string beforeText = null, string afterText = null) {
			DrawColoredItemSlot(spriteBatch, [item], 0, position, backTexture, slotColor, lightColor, textColor, beforeText, afterText);
		}
		public static void DrawColoredItemSlot(SpriteBatch spriteBatch, Item[] items, int slot, Vector2 position, Texture2D backTexture, Color slotColor, Color lightColor = default, Color textColor = default, string beforeText = null, string afterText = null) {
			
			spriteBatch.Draw(backTexture, position, null, slotColor, 0f, Vector2.Zero, Main.inventoryScale, SpriteEffects.None, 0f);
			if (beforeText is not null) {
				Terraria.UI.Chat.ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, beforeText, position + new Vector2(8f, 4f) * Main.inventoryScale, textColor, 0f, Vector2.Zero, new Vector2(Main.inventoryScale), -1f, Main.inventoryScale);
			}
			ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.ChatItem, slot, position, lightColor);
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
				Rectangle adjustedClippingRectangle = Rectangle.Intersect(area.Scale(), spriteBatch.GraphicsDevice.ScissorRectangle);
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
		public static void CaptureScroll(this IScrollableUIItem scrollable) {
			ScrollingPlayer.scrollable = scrollable;
		}
	}
	public interface IScrollableUIItem {
		public void Scroll(int direction);
	}
	[Flags]
	public enum RectangleHandles : byte {
		Top = 1 << 0,
		Left = 1 << 1,
		Bottom = 1 << 2,
		Right = 1 << 3,
	}
}
