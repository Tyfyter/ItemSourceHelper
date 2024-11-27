using ItemSourceHelper.Core;
using ItemSourceHelper.Default;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Design;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.UI.Chat;
using Tyfyter.Utils;

namespace ItemSourceHelper {
	public class FavoriteUI : UIPanel {
		public static List<ItemSource> Favorites { get; internal set; }
		readonly List<FavoriteElement> list = [];
		public FavoriteUI() {
			PaddingLeft = 6;
			PaddingRight = 4;
			PaddingTop = 6;
			PaddingBottom = 4;
			Left.Pixels = ItemSourceHelperPositions.Instance.FavoritesLeft;
			Top.Pixels = ItemSourceHelperPositions.Instance.FavoritesTop;
		}
		public override void Update(GameTime gameTime) {
			Favorites ??= [];
			if (list.Count != Favorites.Count) {
				list.Clear();
				RemoveAllChildren();
				Width.Set(0, 0);
				Height.Set(0, 0);
				for (int i = 0; i < Favorites.Count; i++) {
					FavoriteElement fav = new(Favorites[i]);
					if (fav.Width.Pixels > Width.Pixels) Width.Set(fav.Width.Pixels + PaddingLeft + PaddingRight, 0);
					fav.Top.Set(Height.Pixels, 0);
					Height.Set(Height.Pixels + fav.Height.Pixels + 4, 0);
					list.Add(fav);
					Append(fav);
				}
				Height.Set(Height.Pixels + PaddingTop + PaddingBottom, 0);
				Recalculate();
			}
			base.Update(gameTime);
		}
		internal static bool canDrag = false;
		internal static bool dragging = false;
		internal static Vector2 dragOffset;
		public override void Draw(SpriteBatch spriteBatch) {
			canDrag = !dragging && GetOuterDimensions().ToRectangle().Contains(Main.mouseX, Main.mouseY);
			if (canDrag) Main.LocalPlayer.mouseInterface = true;
			base.Draw(spriteBatch);
			if (canDrag && Main.mouseLeft && Main.mouseLeftRelease) {
				dragging = true;
				dragOffset.X = Left.Pixels - Main.mouseX;
				dragOffset.Y = Top.Pixels - Main.mouseY;
			}
			if (dragging) {
				if (Main.mouseLeft) {
					Main.LocalPlayer.mouseInterface = true;
					float left = Left.Pixels;
					float top = Top.Pixels;
					Left.Pixels = Main.mouseX + dragOffset.X;
					Top.Pixels = Main.mouseY + dragOffset.Y;
					if (left != Left.Pixels || top != Top.Pixels) Recalculate();
				} else {
					dragging = false;
					ItemSourceHelperPositions.Instance.FavoritesLeft = Left.Pixels;
					ItemSourceHelperPositions.Instance.FavoritesTop = Top.Pixels;
				}
			}
		}
	}
	public class FavoriteElement : UIElement {
		const int padding = 3;
		readonly ItemSource source;
		readonly Item[] sourceItems;
		Item createItem;
		public FavoriteElement(ItemSource source) {
			this.source = source;
			this.sourceItems = source.GetSourceItems().ToArray();
			this.createItem = source.Item.Clone();
			int size = (int)(52 * 0.85f);
			Width.Set((sourceItems.Length + 1) * (size + padding) + 24, 0);
			Height.Set(size, 0);
		}
		public override void Draw(SpriteBatch spriteBatch) {
			float inventoryScale = Main.inventoryScale;
			try {
				Texture2D texture = TextureAssets.InventoryBack13.Value;
				Color normalColor = ItemSourceHelperConfig.Instance.ItemSlotColor;
				Color craftableColor = ItemSourceHelperConfig.Instance.FavoriteListCraftableColor;
				Main.inventoryScale = 0.85f;
				int size = (int)(52 * Main.inventoryScale);
				Vector2 position = GetDimensions().ToRectangle().TopLeft();
				bool hasAllIngredients = source.SourceType.OwnsAllItems(source);
				for (int i = 0; i < sourceItems.Length; i++) {
					bool hasIngredient = hasAllIngredients || source.SourceType.OwnsItem(sourceItems[i]);
					//ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.CraftingMaterial, i, position);
					if (Main.mouseX >= position.X && Main.mouseX <= position.X + size && Main.mouseY >= position.Y && Main.mouseY <= position.Y + size) {
						UIMethods.DrawColoredItemSlot(spriteBatch, sourceItems, i, position, texture, hasIngredient ? craftableColor : normalColor);
						ItemSlot.MouseHover(sourceItems, ItemSlot.Context.CraftingMaterial, i);
						FavoriteUI.canDrag = false;
					} else {
						UIMethods.DrawColoredItemSlot(spriteBatch, sourceItems, i, position, texture, hasIngredient ? craftableColor : normalColor);
					}
					position.X += size + padding;
				}
				ChatManager.DrawColorCodedStringWithShadow(
					Main.spriteBatch,
					FontAssets.MouseText.Value,
					"→",
					position + new Vector2(0, size * 0.5f),
					Color.White,
					0,
					FontAssets.MouseText.Value.MeasureString("→") * Vector2.UnitY * 0.35f,
					Vector2.One
				);
				position.X += 24;
				if (Main.mouseX >= position.X && Main.mouseX <= position.X + size && Main.mouseY >= position.Y && Main.mouseY <= position.Y + size) {
					if (Main.keyState.IsKeyDown(Main.FavoriteKey)) {
						Main.cursorOverride = CursorOverrideID.FavoriteStar;//Main.drawingPlayerChat ? CursorOverrideID.Magnifiers : CursorOverrideID.FavoriteStar;
						if (Main.mouseLeft && Main.mouseLeftRelease) {
							FavoriteUI.Favorites.Remove(source);
							SoundEngine.PlaySound(SoundID.MenuTick);
						}
					}
					UIMethods.DrawColoredItemSlot(spriteBatch, ref createItem, position, texture, hasAllIngredients ? craftableColor : normalColor);
					ItemSlot.MouseHover(ref createItem, ItemSlot.Context.CraftingMaterial);

					FavoriteUI.canDrag = false;
				} else {
					UIMethods.DrawColoredItemSlot(spriteBatch, ref createItem, position, texture, hasAllIngredients ? craftableColor : normalColor);
				}
			} finally {
				Main.inventoryScale = inventoryScale;
			}
		}
	}
	public class FavoritesSaverPlayer : ModPlayer {
		List<ItemSource> favorites;
		public override void OnEnterWorld() {
			FavoriteUI.Favorites = favorites;
		}
		public override void SaveData(TagCompound tag) {
			if (FavoriteUI.Favorites is not null) tag.Add("Favorites", FavoriteUI.Favorites.Select(s => s.ToSourceMatcher()).ToList());
		}
		public override void LoadData(TagCompound tag) {
			favorites = (tag.TryGet("Favorites", out List<SourceMatcher> favs) ? favs : [])
				.Select(m => ItemSourceHelper.Instance.Sources.FirstOrDefault(m.Matches))
				.Where(s => s is not null)
				.ToList();
		}
	}
}
