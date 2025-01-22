using ItemSourceHelper.Core;
using ItemSourceHelper.Default;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using ReLogic.OS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameInput;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace ItemSourceHelper {
	public class ItemSourceBrowser : GameInterfaceLayer, IScrollableUIItem {
		public SourceBrowserWindow SourceBrowser => ModContent.GetInstance<SourceBrowserWindow>();
		public IngredientListGridItem Ingredience => SourceBrowser.Ingredience;
		public FilteredEnumerable<ItemSource> ActiveSourceFilters => SourceBrowser.ActiveSourceFilters;
		public ConditionsGridItem ConditionsItem => SourceBrowser.ConditionsItem;
		public FilteredEnumerable<Item> ActiveItemFilters => ItemBrowser.ActiveItemFilters;
		public ItemBrowserWindow ItemBrowser => ModContent.GetInstance<ItemBrowserWindow>();
		public static bool isItemBrowser => ItemSourceHelper.Instance.BrowserWindow.selectedTab == ItemSourceHelper.Instance.BrowserWindow.ItemBrowser.Index;
		public ItemSourceBrowser() : base($"{nameof(ItemSourceHelper)}: Browser", InterfaceScaleType.UI) { }
		bool tabOverflow = false;
		protected override bool DrawSelf() {
			if (ItemSourceHelperPositions.Instance is null || !isActive) return true;
			if (selectedTab == -1) {
				Windows.Sort();
				selectedTab = 0;
				for (int i = 0; i < Windows.Count; i++) Windows[i].Index = i;
			}
			WindowElement browser = Windows[selectedTab];
			browser.Update(new GameTime());
			float inventoryScale = Main.inventoryScale;
			Main.inventoryScale = 0.75f;
			browser.Recalculate();

			tabOverflow = false;
			bool mouseInterface = Main.LocalPlayer.mouseInterface;
			Main.LocalPlayer.mouseInterface = false;
			for (int i = 0; i < Windows.Count; i++) {
				if (i != selectedTab) tabOverflow |= DrawTab(i);
			}
			browser.blockUseHandles |= Main.LocalPlayer.mouseInterface;
			Main.LocalPlayer.mouseInterface |= mouseInterface;
			{
				float height = ItemSourceHelperPositions.Instance.SourceBrowserHeight;
				int tabCount = (int)Math.Round(height / ItemSourceHelperConfig.Instance.TabSize);
				float tabHeight = height / tabCount;
				browser.blockUseHandles |= new Rectangle(
					(int)(ItemSourceHelperPositions.Instance.SourceBrowserLeft - ItemSourceHelperConfig.Instance.TabWidth),
					(int)(ItemSourceHelperPositions.Instance.SourceBrowserTop + tabHeight * (selectedTab - tabScroll)),
					ItemSourceHelperConfig.Instance.TabWidth,
					(int)tabHeight
				).Contains(Main.mouseX, Main.mouseY);
			}
			browser.Draw(Main.spriteBatch);
			tabOverflow |= DrawTab(selectedTab);

			Main.inventoryScale = inventoryScale;
			return true;
		}
		public bool DrawTab(int tab) {
			int scrolledTab = tab - tabScroll;
			if (scrolledTab >= 0) {
				float height = ItemSourceHelperPositions.Instance.SourceBrowserHeight;
				int tabCount = (int)Math.Round(height / ItemSourceHelperConfig.Instance.TabSize);
				if (scrolledTab < tabCount) {
					float tabHeight = height / tabCount;
					Rectangle area = new(
						(int)(ItemSourceHelperPositions.Instance.SourceBrowserLeft - ItemSourceHelperConfig.Instance.TabWidth),
						(int)(ItemSourceHelperPositions.Instance.SourceBrowserTop + tabHeight * scrolledTab),
						ItemSourceHelperConfig.Instance.TabWidth + 12,
						(int)tabHeight
					);
					Color highlighted = Color.White;
					if (area.Contains(Main.mouseX, Main.mouseY) && Main.mouseX < ItemSourceHelperPositions.Instance.SourceBrowserLeft) {
						this.CaptureScroll();
						Main.LocalPlayer.mouseInterface = true;
						UIMethods.TryMouseText(Windows[tab].DisplayNameText, Windows[tab].DisplayNameRarity);
						if (tab != selectedTab && Main.mouseLeft && Main.mouseLeftRelease) SetTab(tab);
					} else if (tab != selectedTab) {
						highlighted = Color.Gray;
					}
					Color color = Windows[tab].color.MultiplyRGB(highlighted);
					Main.spriteBatch.DrawPartialRoundedRetangle(
						new Rectangle(area.X, area.Y - 8, area.Width + 2, area.Height + 16),
						color, null, 0
					);
					Main.spriteBatch.DrawPartialRoundedRetangle(
						area,
						color, null,
						RectangleHandles.Top | RectangleHandles.Left,
						RectangleHandles.Left,
						RectangleHandles.Bottom | RectangleHandles.Left,
						RectangleHandles.Top,
						0,
						RectangleHandles.Bottom
					);
					if (scrolledTab == 0 || scrolledTab == tabCount - 1) Main.spriteBatch.DrawPartialRoundedRetangle(
						new Rectangle(area.X, area.Y, area.Width + 7, area.Height),
						color, null,
						(scrolledTab == 0 ? RectangleHandles.Top : 0) | (scrolledTab == tabCount - 1 ? RectangleHandles.Bottom : 0)
					);
					Texture2D texture = Windows[tab].TextureValue;
					Rectangle texRect = texture.Size().RectWithinCentered(area, 19);
					texRect.X -= 3;
					Main.spriteBatch.Draw(
						texture,
						texRect,
						highlighted
					);
				} else return true;
			}
			return false;
		}
		public void Reset() {
			for (int i = 0; i < Windows.Count; i++) {
				Windows[i].ResetItems();
			}
			selectedTab = 0;
		}
		int selectedTab = -1;
		public int tabScroll = 0;
		public void Scroll(int direction) {
			//Main.UIScale += direction * 0.05f;
			if (tabOverflow && direction > 0 || tabScroll > 0 && direction < 0) tabScroll += direction;
		}
		public void SetTab(int index, bool clearFilters = false) {
			if (selectedTab != -1) Windows[selectedTab].OnLostFocus();
			selectedTab = index;
			if (clearFilters) Windows[selectedTab].ResetItems();
			Windows[selectedTab].CheckSizes();
		}
		public T SetTab<T>(bool clearFilters = false) where T : WindowElement {
			T tab = ModContent.GetInstance<T>();
			SetTab(tab.Index, clearFilters);
			return tab;
		}
		public bool IsTabActive<T>() where T : WindowElement => selectedTab == ModContent.GetInstance<T>().Index;
		bool isActive = false;
		public void Toggle() {
			if (isActive) Close();
			else Open();
		}
		public void Open() {
			if (selectedTab == -1) {
				Windows.Sort();
				selectedTab = 0;
				for (int i = 0; i < Windows.Count; i++) Windows[i].Index = i;
			}
			isActive = true;
		}
		public void Close() {
			Windows[selectedTab].OnLostFocus();
			isActive = false;
		}
		public static List<WindowElement> Windows { get; internal set; } = [];
	}
	public class FilterListGridItem<T> : GridItem, IScrollableUIItem {
		public FilteredEnumerable<T> activeFilters;
		public IEnumerable<IFilter<T>> filters;
		public IEnumerable<ISorter<T>> sorters;
		public Action ResetScroll { get; init; }
		public IFilter<T> lastFilter;
		int scrollTop;
		bool cutOffTop;
		int scrollBottom;
		bool cutOffBottom;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Height -= 1;
			Texture2D actuator = TextureAssets.Actuator.Value;
			Main.instance.LoadItem(ItemID.TeamBlockWhitePlatform);
			Texture2D platform = TextureAssets.Item[ItemID.UnicornHorn].Value;
			bool shouldResetScroll = false;
			bounds.X += 3;
			bounds.Width -= 3;
			bool drewTop = false;
			bool drewBottom = false;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				bool canHover = bounds.Contains(Main.mouseX, Main.mouseY);
				if (canHover) {
					this.CaptureScroll();
					Main.LocalPlayer.mouseInterface = true;
				}
				int size = (int)(32 * Main.inventoryScale);
				const int padding = 2;
				int sizeWithPadding = size + padding;

				int minX = bounds.X + 5;
				int baseX = minX;
				int x = baseX - scrollTop * sizeWithPadding;
				int maxX = bounds.X + bounds.Width - size / 2;
				int y = bounds.Y + 6;
				int maxY = bounds.Y + bounds.Height - size / 2;
				int topMaxX = maxX;

				cutOffTop = false;
				cutOffBottom = false;
				Point mousePos = Main.MouseScreen.ToPoint();
				Color color = new(0, 0, 0, 50);
				Color hiColor = new(50, 50, 50, 0);
				Rectangle button = new(x, y, size, size);
				if (activeFilters.FilterCount != 0) {
					Rectangle clearButton = new(maxX - (size - 6), y, size, size);
					if (canHover && clearButton.Contains(mousePos)) {
						spriteBatch.Draw(actuator, clearButton, Color.Pink);
						UIMethods.TryMouseText(Language.GetOrRegister($"Mods.{nameof(ItemSourceHelper)}.FilterList.ClearButton").Value);
						if (Main.mouseLeft && Main.mouseLeftRelease) {
							activeFilters.ClearSelectedFilters();
							lastFilter = null;
						}
					} else {
						spriteBatch.Draw(actuator, clearButton, Color.Red);
					}
					topMaxX -= sizeWithPadding;
				}

				int lastFilterChannel = -1;
				foreach (IFilter<T> filter in filters) {
					if (filter.ShouldHide()) continue;
					if (x >= minX - size) {
						if (lastFilterChannel != filter.FilterChannel) {
							if (lastFilterChannel != -1) {
								spriteBatch.Draw(
									TextureAssets.MagicPixel.Value,
									new Rectangle(x, y, 2, size),
									new Color(0, 0, 0, 100)
								);
								x += 2 + padding;
							}
							lastFilterChannel = filter.FilterChannel;
						}
						button.X = x;
						button.Y = y;
						bool changed = false;//OwO
						bool filterIsActive = activeFilters.IsFilterActive(filter);
						if (button.Contains(mousePos)) {
							UIMethods.DrawRoundedRetangle(spriteBatch, button, hiColor);
							UIMethods.TryMouseText(filter.DisplayNameText, filter.DisplayNameRarity);
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								SetFilter(filter, null, Main.keyState.PressingShift(), Main.keyState.PressingControl());
								changed = true;
							} else if (Main.mouseRight && Main.mouseRightRelease) {
								if (filter == lastFilter) {
									lastFilter = null;
									shouldResetScroll = true;
								} else if (activeFilters.IsFilterActive(filter)) {
									lastFilter = filter;
									shouldResetScroll = true;
								}
							}
						} else {
							UIMethods.DrawRoundedRetangle(spriteBatch, button, color);
						}
						Texture2D texture = filter.TextureValue;
						Rectangle iconPos;
						Rectangle? iconFrame = null;
						if (filter.TextureAnimation is null) {
							iconPos = texture.Size().RectWithinCentered(button, 8);
						} else {
							iconFrame = filter.TextureAnimation.GetFrame(texture);
							iconPos = iconFrame.Value.FitWithinCentered(button, 8);
							//filter.TextureAnimation.Update();
						}
						spriteBatch.Draw(texture, iconPos, iconFrame, Color.White);
						if (filterIsActive) {//index != -1 && filter.Type == activeFilters.GetFilter(index).Type
							Rectangle corner = button;
							int halfWidth = corner.Width / 2;
							corner.X += halfWidth;
							corner.Width -= halfWidth;
							corner.Height /= 2;
							spriteBatch.Draw(actuator, corner, Color.Green);
							if (!changed) {
								IFilter<T> index = activeFilters.GetFilter(activeFilters.FindFilterIndex(filter.ShouldReplace));
								bool isNot = false;
								if (index is NotFilter<T> notFilter0 && notFilter0.Filter == filter) {
									isNot = true;
								} else if (index is OrFilter<T> orFilter0) {
									for (int i = 0; i < orFilter0.Filters.Count; i++) {
										if (orFilter0.Filters[i] is NotFilter<T> notFilter1 && notFilter1.Filter == filter) {
											isNot = true;
											break;
										}
									}
								}
								if (isNot) {
									corner.X -= halfWidth;
									spriteBatch.Draw(actuator, corner, Color.Red);
								}
							}
						}
						drewTop = true;
					}
					x += sizeWithPadding;
					if (x >= topMaxX) {
						cutOffTop = true;
						break;
					}
				}
				x = baseX - scrollBottom * sizeWithPadding;
				y += sizeWithPadding;
				if (sorters is not null) {
					foreach (ISorter<T> sorter in sorters) {
						if (!activeFilters.FiltersSupportSortMethod(sorter)) continue;
						if (x >= baseX - size) {
							button.X = x;
							button.Y = y;
							bool selected = activeFilters.IsSelectedSortMethod(sorter);
							if (button.Contains(mousePos)) {
								UIMethods.DrawRoundedRetangle(spriteBatch, button, hiColor);
								UIMethods.TryMouseText(sorter.DisplayName.Value);
								if (Main.mouseLeft && Main.mouseLeftRelease) {
									if (selected) {
										activeFilters.Inverted = !activeFilters.Inverted;
									} else {
										activeFilters.Inverted = false;
										activeFilters.SetSortMethod(sorter);
									}
									ResetScroll();
								}
							} else {
								UIMethods.DrawRoundedRetangle(spriteBatch, button, selected ? hiColor : color);
							}
							Texture2D texture = sorter.TextureAsset.Value;
							spriteBatch.Draw(texture, texture.Size().RectWithinCentered(button, 8), Color.White);
							if (selected) {
								Rectangle corner = button;
								int halfWidth = corner.Width / 2;
								corner.X += halfWidth;
								corner.Width -= halfWidth;
								corner.Height /= 2;
								Texture2D arrows = ItemSourceHelper.SortArrows.Value;
								spriteBatch.Draw(
									arrows,
									corner.Center(),
									arrows.Frame(2, frameX: activeFilters.Inverted ? 1 : 0),
									Color.White,
									0,
									new Vector2(5, 9),
									0.85f,
									0,
								0);
							}
							drewBottom = true;
						}
						x += sizeWithPadding;
						if (x >= maxX) {
							cutOffBottom = true;
							break;
						}
					}
				}
				if (lastFilter is not null) {
					if (sorters is not null && sorters.Any()) {
						spriteBatch.Draw(
							TextureAssets.MagicPixel.Value,
							new Rectangle(x, y, 2, size),
							new Color(0, 0, 0, 100)
						);
						x += 2 + padding;
					}
					foreach (IFilter<T> filter in lastFilter.ChildFilters()) {
						if (filter.ShouldHide()) continue;
						if (x >= baseX - size) {
							button.X = x;
							button.Y = y;
							bool changed = false;
							bool filterIsActive = activeFilters.IsChildFilterActive(lastFilter, filter);
							if (button.Contains(mousePos)) {
								UIMethods.DrawRoundedRetangle(spriteBatch, button, hiColor);
								UIMethods.TryMouseText(filter.DisplayNameText, filter.DisplayNameRarity);
								if (Main.mouseLeft && Main.mouseLeftRelease) {
									if (filterIsActive) {
										activeFilters.TryRemoveChildFilter(lastFilter, filter);
									} else {
										if (Main.keyState.PressingControl()) {
											activeFilters.TryAddChildFilter(lastFilter, new NotFilter<T>(filter), Main.keyState.PressingShift());
										} else {
											activeFilters.TryAddChildFilter(lastFilter, filter, Main.keyState.PressingShift());
										}
									}
									changed = true;
								}
							} else {
								UIMethods.DrawRoundedRetangle(spriteBatch, button, color);
							}
							Texture2D texture = filter.TextureValue;
							Rectangle iconPos;
							Rectangle? iconFrame = null;
							if (filter.TextureAnimation is null) {
								iconPos = texture.Size().RectWithinCentered(button, 8);
							} else {
								iconFrame = filter.TextureAnimation.GetFrame(texture);
								iconPos = iconFrame.Value.FitWithinCentered(button, 8);
								//filter.TextureAnimation.Update();
							}
							spriteBatch.Draw(texture, iconPos, iconFrame, Color.White);
							if (filterIsActive) {//index != -1 && filter.Type == activeFilters.GetFilter(index).Type
								Rectangle corner = button;
								int halfWidth = corner.Width / 2;
								corner.X += halfWidth;
								corner.Width -= halfWidth;
								corner.Height /= 2;
								spriteBatch.Draw(actuator, corner, Color.Green);
								if (!changed) {
									foreach (IFilter<T> child in lastFilter.ActiveChildren) {
										bool isNot = false;
										if (child == filter) {
											goto foundNotnt;
										} else if (child is NotFilter<T> notFilter0 && notFilter0.Filter == filter) {
											isNot = true;
										} else if (child is OrFilter<T> orFilter0) {
											for (int i = 0; i < orFilter0.Filters.Count; i++) {
												if (child == filter) {
													goto foundNotnt;
												} else if(orFilter0.Filters[i] is NotFilter<T> notFilter1 && notFilter1.Filter == filter) {
													isNot = true;
													break;
												}
											}
										}
										if (isNot) {
											corner.X -= halfWidth;
											spriteBatch.Draw(actuator, corner, Color.Red);
											break;
										}
									}
									foundNotnt:;
								}
							}
							drewBottom = true;
						}
						x += sizeWithPadding;
						if (x >= maxX) {
							cutOffBottom = true;
							break;
						}
					}
				}
			}
			if (!Main.mouseText && Main.keyState.PressingShift() && bounds.Contains(Main.MouseScreen.ToPoint())) {
				UIMethods.TryMouseText(Language.GetOrRegister($"Mods.{nameof(ItemSourceHelper)}.FilterList.HelpText").Value);
			}
			if (shouldResetScroll && ResetScroll is not null) {
				ResetScroll();
			} else {
				if (!drewTop && scrollTop > 0) scrollTop--;
				if (!drewBottom && scrollBottom > 0) scrollBottom--;
			}
		}
		public void SetFilter(IFilter<T> filter, bool? state = null, bool orMerge = false, bool invert = false) {
			if (invert) filter = new NotFilter<T>(filter);
			bool recalculate = false;
			if (activeFilters.IsFilterActive(filter)) {
				if (state != true) {
					activeFilters.TryRemoveFilter(filter);
					if (lastFilter == filter) lastFilter = null;
					recalculate = true;
				}
			} else if (state != false) {
				recalculate = activeFilters.TryAddFilter(filter, orMerge);
				if (filter.ChildFilters().Any()) lastFilter = filter;
			}
			if (recalculate) {
				activeFilters.ClearCache();
				if (lastFilter is not null && !activeFilters.IsFilterActive(lastFilter)) lastFilter = null;
			}
		}
		public void Scroll(int direction) {
			if (cutOffTop && direction > 0 || scrollTop > 0 && direction < 0) scrollTop += direction;
			if (cutOffBottom && direction > 0 || scrollBottom > 0 && direction < 0) scrollBottom += direction;
			if (scrollTop < 0) scrollTop = 0;
			if (scrollBottom < 0) scrollBottom = 0;
		}
		public override void Reset() {
			scrollTop = 0;
			scrollBottom = 0;
			lastFilter = null;
			activeFilters.ClearSelectedFilters();
		}
	}
	public class IngredientListGridItem : GridItem, IScrollableUIItem {
		Item[] items = [];
		int scroll;
		bool cutOff;
		int doubleClickTime = 0;
		int doubleClickItem = 0;
		public void SetItems(Item[] items) {
			this.items = items;
			scroll = 0;
		}
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Height -= 1;
			if (doubleClickTime > 0 && --doubleClickTime <= 0) doubleClickItem = 0;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				bool canHover = bounds.Contains(Main.mouseX, Main.mouseY);
				if (canHover) {
					this.CaptureScroll();
					Main.LocalPlayer.mouseInterface = true;
				}
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
				Texture2D texture = TextureAssets.InventoryBack13.Value;
				Color normalColor = ItemSourceHelperConfig.Instance.ItemSlotColor;
				Color hoverColor = ItemSourceHelperConfig.Instance.HoveredItemSlotColor;
				for (int i = 0; i < items.Length; i++) {
					if (x >= minX - size) {
						position.X = x;
						position.Y = y;
						//ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.CraftingMaterial, i, position);
						if (canHover && Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size) {
							UIMethods.DrawColoredItemSlot(spriteBatch, items, i, position, texture, hoverColor);
							ItemSlot.MouseHover(items, ItemSlot.Context.CraftingMaterial, i);
							if ((Main.mouseLeft && Main.mouseLeftRelease) || (Main.mouseRight && Main.mouseRightRelease)) {
								if (items[i].type != doubleClickItem) doubleClickTime = 0;
								if (doubleClickTime > 0) {
									if (Main.mouseLeft) {
										SourceBrowserWindow window = ModContent.GetInstance<SourceBrowserWindow>();
										Item currentItem = items[i];
										window.ResetItems();
										window.FilterItem.SetItem(currentItem);
									} else if (items[i].TryGetGlobalItem(out AnimatedRecipeGroupGlobalItem global) && global.recipeGroup != -1) {
										MaterialFilter materialFilter = ModContent.GetInstance<MaterialFilter>();
										foreach (IFilter<Item> filter in materialFilter.ChildItemFilters()) {
											if (filter is RecipeGroupFilter recipeGroupFilter && recipeGroupFilter.RecipeGroup.RegisteredId == global.recipeGroup) {
												ItemBrowserWindow window = ItemSourceHelper.Instance.BrowserWindow.SetTab<ItemBrowserWindow>(true);
												window.ItemFilterList.SetFilter(materialFilter, true);
												window.ItemFilterList.SetFilter(filter, true);
												break;
											}
										}
									} else {
										ItemSourceHelper.Instance.BrowserWindow.SetTab<ItemBrowserWindow>(true).ScrollToItem(items[i].type);
									}
								} else {
									doubleClickTime = 15;
									doubleClickItem = items[i].type;
								}
							}
						} else {
							UIMethods.DrawColoredItemSlot(spriteBatch, items, i, position, texture, normalColor);
						}
						if (i < items.Length) UIMethods.DrawIndicators(spriteBatch, items[i].type, ItemSourceHelperConfig.Instance.IngredientListIndicators, position, size);
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
		public override void Reset() {
			items = [];
			scroll = 0;
		}
	}
	public class DropListGridItem : GridItem, IScrollableUIItem {
		List<DropRateInfo> drops = [];
		int scroll;
		bool cutOff;
		int doubleClickTime = 0;
		int doubleClickItem = 0;
		public void SetDrops(List<DropRateInfo> drops) {
			this.drops = drops;
			scroll = 0;
		}
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Height -= 1;
			if (doubleClickTime > 0 && --doubleClickTime <= 0) doubleClickItem = 0;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				bool canHover = bounds.Contains(Main.mouseX, Main.mouseY);
				if (canHover) {
					this.CaptureScroll();
					Main.LocalPlayer.mouseInterface = true;
				}
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
				Texture2D texture = TextureAssets.InventoryBack13.Value;
				Color normalColor = ItemSourceHelperConfig.Instance.ItemSlotColor;
				Color hoverColor = ItemSourceHelperConfig.Instance.HoveredItemSlotColor;
				int drew = 0;
				for (int i = 0; i < drops.Count; i++) {
					if (x >= minX - size) {
						drew++;
						position.X = x;
						position.Y = y;
						//ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.CraftingMaterial, i, position);
						DropRateInfo info = drops[i];
						bool hide = false;
						if ((info.conditions?.Count ?? 0) > 0) {
							for (int j = 0; j < info.conditions.Count && !hide; j++) {
								if (!info.conditions[j].CanShowItemDropInUI()) hide = true;
							}
						}
						if (hide) continue;
						Item item = ContentSamples.ItemsByType[info.itemId];
						if (canHover && Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size) {
							UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, texture, hoverColor);

							if (item.ModItem is UnloadedItem unloadedItem && string.IsNullOrEmpty(unloadedItem.ItemName)) {
								UICommon.TooltipMouseText(info.conditions[0].GetConditionDescription());
							} else {
								tooltipModifier.info = info;
								TooltipAdderGlobal.TooltipModifiers.Add(tooltipModifier);
								ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
							}
							if ((Main.mouseLeft && Main.mouseLeftRelease) || (Main.mouseRight && Main.mouseRightRelease)) {
								if (info.itemId != doubleClickItem) doubleClickTime = 0;
								if (doubleClickTime > 0) {
									if (Main.mouseLeft) {
										LootBrowserWindow window = ModContent.GetInstance<LootBrowserWindow>();
										window.ResetItems();
										window.FilterItem.SetItem(info.itemId);
									} else {
										ItemSourceHelper.Instance.BrowserWindow.SetTab<ItemBrowserWindow>(true).ScrollToItem(info.itemId);
									}
								} else {
									doubleClickTime = 15;
									doubleClickItem = item.type;
								}
							}
						} else {
							UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, texture, normalColor);
						}
						UIMethods.DrawIndicators(spriteBatch, item.type, ItemSourceHelperConfig.Instance.DropListIndicators, position, (int)(52 * Main.inventoryScale));
						string chanceText;
						if (info.dropRate >= 0.1f) {
							chanceText = $"{info.dropRate:P0}";
						} else {
							chanceText = $"{(info.dropRate * 100):0.##}%";
						}
						ChatManager.DrawColorCodedStringWithShadow(
							spriteBatch,
							FontAssets.ItemStack.Value,
							chanceText,
							position + new Vector2(6f, 30f) * Main.inventoryScale,
							Color.White,
							0f,
							Vector2.Zero,
							new Vector2(Main.inventoryScale),
							-1f,
							Main.inventoryScale
						);
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
				if (scroll > 0 && drew < (bounds.Width / sizeWithPadding) * 0.5f) scroll -= 1;
			}
		}
		public void Scroll(int direction) {
			if (!cutOff && direction > 0) return;
			if (scroll <= 0 && direction < 0) return;
			scroll += direction;
		}
		public override void Reset() {
			drops = [];
			scroll = 0;
		}
		readonly TooltipModifier tooltipModifier = new();
		class TooltipModifier : ITooltipModifier {
			public DropRateInfo info;
			public void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
				string amountText = null;
				if (info.stackMin != info.stackMax) {
					amountText = $"{info.stackMin}-{info.stackMax}";
				} else if (info.stackMin > 1) {
					amountText = info.stackMin.ToString();
				}
				if (amountText is not null) tooltips[0].Text += $" ({amountText})";
				if (info.conditions is not null) {
					DropAttemptInfo dropInfo = default(DropAttemptInfo);
					dropInfo.player = Main.LocalPlayer;
					dropInfo.npc = ContentSamples.NpcsByNetId[NPCID.Guide];
					dropInfo.IsExpertMode = Main.expertMode;
					dropInfo.IsMasterMode = Main.masterMode;
					dropInfo.IsInSimulation = false;
					dropInfo.rng = Main.rand;
					for (int i = 0; i < info.conditions.Count; i++) {
						string description = info.conditions[i].GetConditionDescription();
						if (description is not null) tooltips.Add(new(ItemSourceHelper.Instance, "Condition" + i, description) {
							IsModifier = true,
							IsModifierBad = !info.conditions[i].CanDrop(dropInfo)
						});
					}
				}
				if ($"{info.dropRate * 100:0.##}" == "0") {
					tooltips.Add(new(ItemSourceHelper.Instance, "DropRateExact", Language.GetOrRegister("Mods.ItemSourceHelper.DropChanceExact").Format(info.dropRate)));
				}
			}
		}
	}
	public interface ISearchFilterReceiver {
		public void SetSearchFilters(IEnumerable<SearchFilter> filters);
	}
	public interface IFilterSlotReceiver {
		public void SetFilterItem(Item item);
	}
	public class FilteredEnumerable<T> : IEnumerable<T>, ISearchFilterReceiver, IFilterSlotReceiver {
		ISorter<T> defaultSortMethod;
		ISorter<T> sourceSourceSource;
		List<T> sourceSource;
		List<IFilter<T>> filters = [];
		List<SearchFilter> searchFilter = [];
		List<(T value, int index)> cache = [];
		Item filterItem;
		bool inverted;
		bool reachedEnd;
		bool altered;
		public bool Inverted {
			get => inverted;
			set {
				if (inverted != value) ClearCache();
				inverted = value;
			}
		}
		public IEnumerator<T> GetEnumerator() {
			altered = false;
			if (Inverted) {
				int i = sourceSource.Count - 1;
				for (int j = 0; j < cache.Count; j++) {
					(T source, int index) = cache[j];
					i = index - 1;
					yield return source;
				}
				if (reachedEnd) yield break;
				for (; i > 0; i--) {
					if (altered) yield break;
					T source = sourceSource[i];
					if (!MatchesSlot(source)) goto cont;
					for (int j = 0; j < filters.Count; j++) if (!filters[j].MatchesAll(source)) goto cont;
					if (searchFilter.Count > 0) {
						Dictionary<string, string> data = SearchLoader.GetSearchData(source);
						for (int j = 0; j < searchFilter.Count; j++) if (!searchFilter[j].Matches(data)) goto cont;
					}
					cache.Add((source, i));
					yield return source;
					cont:;
				}
				reachedEnd = true;
			} else {
				int i = 0;
				for (int j = 0; j < cache.Count; j++) {
					(T source, int index) = cache[j];
					i = index + 1;
					yield return source;
				}
				if (reachedEnd) yield break;
				for (; i < sourceSource.Count; i++) {
					if (altered) yield break;
					T source = sourceSource[i];
					if (!MatchesSlot(source)) goto cont;
					for (int j = 0; j < filters.Count; j++) if (!filters[j].MatchesAll(source)) goto cont;
					if (searchFilter.Count > 0) {
						Dictionary<string, string> data = SearchLoader.GetSearchData(source);
						for (int j = 0; j < searchFilter.Count; j++) if (!searchFilter[j].Matches(data)) goto cont;
					}
					cache.Add((source, i));
					yield return source;
					cont:;
				}
				reachedEnd = true;
			}
		}
		public void SetSearchFilters(IEnumerable<SearchFilter> filters) {
			if (searchFilter.Count != 0) ClearCache();
			searchFilter = filters.ToList();
			if (cache.Count != 0) cache.RemoveAll(i => filters.Any(f => f.DoesntMatch(SearchLoader.GetSearchData(i.value))));
		}
		public void SetDefaultSortMethod(ISorter<T> sortMethod) {
			defaultSortMethod = sortMethod;
			if (sourceSource is null) SetSortMethod(sortMethod);
		}
		public void SetSortMethod(ISorter<T> sortMethod) {
			defaultSortMethod ??= sortMethod;
			SetBackingList(sortMethod.SortedValues);
			sourceSourceSource = sortMethod;
		}
		public void SetBackingList(List<T> sources) {
			sourceSourceSource = null;
			sourceSource = sources;
			ClearCache();
		}
		public bool IsSelectedSortMethod(ISorter<T> sortMethod) => sourceSourceSource == sortMethod;
		public bool FiltersSupportSortMethod(ISorter<T> sortMethod) {
			if (sortMethod == null) return true;
			Predicate<IFilterBase>[] reqs = sortMethod.FilterRequirements;
			bool allReqs = true;
			for (int i = 0; i < reqs.Length && allReqs; i++) {
				foreach (IFilter<T> filter in filters) {
					if (reqs[i](filter)) goto found;
					foreach (IFilter<T> subFilter in filter.ActiveChildren) {
						if (reqs[i](subFilter)) goto found;
					}
				}
				allReqs = false;
				found:;
			}
			return allReqs;
		}
		public void CheckSortMethodSupport() {
			if (sourceSourceSource == null) return;
			if (!FiltersSupportSortMethod(sourceSourceSource)) {
				SetSortMethod(defaultSortMethod);
				inverted = false;
			}
		}
		protected void CheckSortMethodSupport(IFilter<T> lostFilter, IFilter<T> newFilter = null) {
			if (sourceSourceSource == null) return;
			Predicate<IFilterBase>[] reqs = sourceSourceSource.FilterRequirements;
			bool lostReq = false;
			for (int i = 0; i < reqs.Length && !lostReq; i++) {
				if (newFilter is not null && reqs[i](newFilter)) continue;
				if (reqs[i](lostFilter)) {
					lostReq = true;
					break;
				}
				foreach (IFilter<T> subFilter in lostFilter.ActiveChildren) {
					if (reqs[i](subFilter)) {
						lostReq = true;
						break;
					}
				}
			}
			if (lostReq) {
				SetSortMethod(defaultSortMethod);
				inverted = false;
			}
		}
		public void FillTooltipAdders(List<ITooltipModifier> list) {
			if (sourceSourceSource is ITooltipModifier modifier) list.Add(modifier);
			list.AddRange(filters.TryCast<ITooltipModifier>());
		}
		#region selected filters
		public void ClearCache() {
			cache.Clear();
			reachedEnd = false;
			altered = true;
		}
		public IEnumerable<IFilter<T>> SelectedFilters => filters;
		public int FilterCount => filters.Count;
		public int FindFilterIndex(Predicate<IFilter<T>> match) => filters.FindIndex(match);
		public IFilter<T> GetFilter(int index) => filters[index];
		public void ClearSelectedFilters() {
			if (defaultSortMethod is not null) SetSortMethod(defaultSortMethod);
			inverted = false;
			filters.Clear();
			ClearCache();
		}
		/// <returns>true if the cache must be cleared and/or some filters may have lost dependents</returns>
		public bool TryAddFilter(IFilter<T> filter, bool orMerge = false) {
			filter.ActiveChildren.Clear();
			int index = filters.FindIndex(filter.ShouldReplace);
			if (index != -1) {
				if (orMerge) {
					if (filters[index] is OrFilter<T> orFilter) {
						orFilter.Add(filter);
					} else {
						filters[index] = new OrFilter<T>(filters[index], filter);
					}
				} else {
					CheckSortMethodSupport(filters[index], filter);
					filters[index] = filter;
				}
				return true;
			}
			filters.Add(filter);
			cache.RemoveAll(filter.DoesntMatch);
			return false;
		}
		public void TryRemoveFilter(IFilter<T> filter) {
			int index = filters.FindIndex(filter.ShouldReplace);
			if (index != -1) {
				if (filters[index] is OrFilter<T> orFilter && orFilter.Filters.Count > 1) {
					orFilter.Remove(filter);
				} else {
					filters.RemoveAt(index);
				}
				CheckSortMethodSupport(filter);
				ClearCache();
			}
		}
		public bool IsFilterActive(IFilter<T> filter) => IsFilterActive(filter, out _);
		public bool IsFilterActive(IFilter<T> filter, out bool isNot) {
			isNot = false;
			int index = filters.FindIndex(filter.ShouldReplace);
			if (index != -1) {
				if (filters[index] is OrFilter<T> orFilter) {
					return orFilter.Contains(filter);
				} else if (filters[index] is NotFilter<T> notFilter) {
					isNot = true;
					return notFilter.Filter == filter;
				} else {
					return filters[index] == filter;
				}
			}
			return false;
		}
		public void TryAddChildFilter(IFilter<T> parent, IFilter<T> filter, bool orMerge = false, bool invert = false) {
			filter.ActiveChildren.Clear();
			foreach (IFilter<T> child in parent.ActiveChildren) {
				if (filter.ShouldReplace(child)) {
					if (orMerge) {
						if (child is OrFilter<T> orFilter) {
							orFilter.Add(filter);
						} else {
							parent.ActiveChildren.Remove(child);
							parent.ActiveChildren.Add(new OrFilter<T>(child, filter));
						}
						ClearCache();
						return;
					}
					CheckSortMethodSupport(child, filter);
					parent.ActiveChildren.Remove(child);
					ClearCache();
					break;
				}
			}

			parent.ActiveChildren.Add(filter);
			cache.RemoveAll(filter.DoesntMatch);
		}
		public void TryRemoveChildFilter(IFilter<T> parent, IFilter<T> filter) {
			foreach (IFilter<T> child in parent.ActiveChildren) {
				if (filter.ShouldReplace(child)) {
					if (child is OrFilter<T> orFilter && orFilter.Filters.Count > 1) {
						orFilter.Remove(filter);
					} else {
						parent.ActiveChildren.Remove(child);
					}
					CheckSortMethodSupport(child);
					ClearCache();
					break;
				}
			}
		}
		public bool IsChildFilterActive(IFilter<T> parent, IFilter<T> filter) {
			foreach (IFilter<T> child in parent.ActiveChildren) {
				if (filter.ShouldReplace(child)) {
					if (child is OrFilter<T> orFilter) {
						return orFilter.Contains(filter);
					} else if (child is NotFilter<T> notFilter) {
						return notFilter.Filter == filter;
					}
					return child == filter;
				}
			}
			return false;
		}
		#endregion selected filters
		public void SetFilterItem(Item item) {
			if (filterItem?.IsAir == false) {
				ClearCache();
				filterItem = item.Clone();
			} else {
				filterItem = item.Clone();
				cache.RemoveAll(DoesntMatchSlot);
			}
		}
		bool DoesntMatchSlot((T source, int index) data) => !MatchesSlot(data.source);
		public bool MatchesSlot(T value) {
			if (filterItem?.IsAir != false) return true;
			return SlotMatcher(value, filterItem);
		}
		public static Func<T, Item, bool> SlotMatcher = (_, _) => false;
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
	public class SingleSlotGridItem(IFilterSlotReceiver receiver) : GridItem {
		Item item;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			item ??= new();
			Vector2 size = new(52 * Main.inventoryScale);
			Vector2 pos = bounds.Center() - size * 0.5f;
			UIMethods.DrawColoredItemSlot(
				spriteBatch,
				ref item,
				pos,
				TextureAssets.InventoryBack13.Value,
				ItemSourceHelperConfig.Instance.ItemSlotColor
			);
			if (UIMethods.MouseInArea(pos, size)) {
				Main.LocalPlayer.mouseInterface = true;
				ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
				if (Main.mouseLeft && Main.mouseLeftRelease) {
					if (item.type == Main.mouseItem?.type) {
						SetItem(ItemID.None);
					} else {
						SetItem(Main.mouseItem);
					}
				}
			}
		}
		public void SetItem(int type) => SetItem(new Item(type));
		public void SetItem(Item type) {
			item = type?.Clone() ?? new();
			receiver.SetFilterItem(item);
		}
		public override void Reset() {
			item ??= new();
			item.TurnToAir();
		}
	}
	public class SearchGridItem(ISearchFilterReceiver receiver) : GridItem {
		public bool focused = false;
		public int cursorIndex = 0;
		public StringBuilder text = new();
		string lastSearch = "";
		public int typingTimer = 0;
		public void SetSearch(string search) {
			lastSearch = search;
			ItemSourceBrowser browserWindow = ItemSourceHelper.Instance.BrowserWindow;
			List<SearchFilter> filters = search.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(SearchLoader.Parse).ToList();
			receiver.SetSearchFilters(filters);
		}
		void Copy(bool cut = false) {
			Platform.Get<IClipboard>().Value = text.ToString();
			if (cut) Clear();
		}
		void Paste() {
			string clipboard = Platform.Get<IClipboard>().Value;
			text.Insert(cursorIndex, clipboard);
			cursorIndex += clipboard.Length;
		}
		void Clear() {
			text.Clear();
			cursorIndex = 0;
		}
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			int typingTimeoutTime = ItemSourceHelperConfig.Instance.AutoSearchTime + 1;
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			const float scale = 1.25f;
			bounds.Y += 4;
			bounds.Height -= 4;
			string closeText = "X";
			Vector2 helpSize = font.MeasureString(closeText) * scale;
			Vector2 closePos = bounds.TopRight() - new Vector2(helpSize.X + 4, 0);
			Color closeColor = Color.Black;
			if (UIMethods.MouseInArea(closePos, helpSize)) {
				Main.LocalPlayer.mouseInterface = true;
				UIMethods.TryMouseText(Language.GetOrRegister($"Mods.{nameof(ItemSourceHelper)}.Close").Value);
				if (Main.mouseLeft && Main.mouseLeftRelease) ItemSourceHelper.Instance.BrowserWindow.Close();
			} else {
				closeColor *= 0.7f;
			}
			spriteBatch.DrawString(
				font,
				closeText,
				closePos,
				closeColor,
				0,
				new(0, 0),
				scale,
				0,
			0);
			bounds.Width -= (int)helpSize.X + 8;
			Color color = ItemSourceHelperConfig.Instance.SearchBarColor;
			bool hoveringSearch = bounds.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface;
			if (hoveringSearch && Main.mouseRight && Main.mouseRightRelease) Reset();
			Main.LocalPlayer.mouseInterface |= hoveringSearch;
			if (!focused) {
				if (hoveringSearch && Main.mouseLeft && Main.mouseLeftRelease) {
					focused = true;
					cursorIndex = text.Length;
				} else {
					color *= 0.8f;
				}
			} else {
				if (!hoveringSearch && Main.mouseLeft && Main.mouseLeftRelease) {
					focused = false;
				}
			}
			if (Main.keyState.PressingShift() && bounds.Contains(Main.MouseScreen.ToPoint())) {
				UIMethods.TryMouseText(Language.GetOrRegister($"Mods.{nameof(ItemSourceHelper)}.Search.HelpText").Value);
			}
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bool typed = false;
			if (focused) {
				Main.CurrentInputTextTakerOverride = this;
				Main.chatRelease = false;
				PlayerInput.WritingText = true;
				Main.instance.HandleIME();
				string input = Main.GetInputText(" ", allowMultiLine: true);
				if (Main.inputText.PressingControl() || Main.inputText.PressingAlt()) {
					if (UIMethods.JustPressed(Keys.Z)) Clear();
					else if (UIMethods.JustPressed(Keys.X)) Copy(cut: true);
					else if (UIMethods.JustPressed(Keys.C)) Copy();
					else if (UIMethods.JustPressed(Keys.V)) Paste();
					else if (UIMethods.JustPressed(Keys.Left)) {
						if (cursorIndex <= 0) goto specialControls;
						cursorIndex--;
						while (cursorIndex > 0 && text[cursorIndex - 1] != ' ') {
							cursorIndex--;
						}
					} else if (UIMethods.JustPressed(Keys.Right)) {
						if (cursorIndex >= text.Length) goto specialControls;
						cursorIndex++;
						while (cursorIndex < text.Length && text[cursorIndex] != ' ') {
							cursorIndex++;
						}
					} else if (UIMethods.JustPressed(Keys.Back)) {
						if (cursorIndex <= 0) goto specialControls;
						int length = 1;
						cursorIndex--;
						while (cursorIndex > 0 && text[cursorIndex - 1] != ' ') {
							cursorIndex--;
							length++;
						}
						text.Remove(cursorIndex, length);
					}
					typed = true;
					goto specialControls;
				}
				if (Main.inputText.PressingShift()) {
					if (UIMethods.JustPressed(Keys.Delete)) {
						Copy(cut: true);
						typed = true;
						goto specialControls;
					} else if (UIMethods.JustPressed(Keys.Insert)) {
						Paste();
						typed = true;
						goto specialControls;
					}
				}
				if (UIMethods.JustPressed(Keys.Left)) {
					if (cursorIndex > 0) {
						cursorIndex--;
						typed = true;
					}
				} else if (UIMethods.JustPressed(Keys.Right)) {
					if (cursorIndex < text.Length) {
						cursorIndex++;
						typed = true;
					}
				}

				if (input.Length == 0 && cursorIndex > 0) {
					text.Remove(--cursorIndex, 1);
					typed = true;
				} else if (input.Length == 2) {
					text.Insert(cursorIndex++, input[1]);
					typed = true;
				} else if (UIMethods.JustPressed(Keys.Delete)) {
					if (cursorIndex < text.Length) {
						text.Remove(cursorIndex, 1);
						typed = true;
					}
				}
				if (UIMethods.JustPressed(Keys.Enter)) {
					SetSearch(text.ToString());
					focused = false;
					typingTimer = 0;
				} else if (Main.inputTextEscape) {
					Clear();
					text.Append(lastSearch);
					focused = false;
					typingTimer = 0;
				}
			}
			specialControls:
			if (typed) {
				typingTimer = typingTimeoutTime;
			} else if (typingTimer > 0 && --typingTimer == 0) {
				SetSearch(text.ToString());
			}
			Vector2 offset = new(8, 2);
			if (focused && Main.timeForVisualEffects % 40 < 20) {
				spriteBatch.DrawString(
					font,
					"|",
					bounds.TopLeft() + font.MeasureString(text.ToString()[..cursorIndex]) * Vector2.UnitX * scale + offset * new Vector2(0.5f, 1),
					ItemSourceHelperConfig.Instance.SearchBarTextColor,
					0,
					new(0, 0),
					scale,
					0,
				0);
			}
			spriteBatch.DrawString(
				font,
				text,
				bounds.TopLeft() + offset,
				ItemSourceHelperConfig.Instance.SearchBarTextColor,
				0,
				new(0, 0),
				scale,
				0,
			0);
		}
		public override void Reset() {
			Clear();
			receiver.SetSearchFilters([]);
			cursorIndex = 0;
			lastSearch = "";
			focused = false;
		}
	}
	public class ConditionsGridItem : GridItem, IScrollableUIItem {
		public IEnumerable<LocalizedText> conditionts = [];
		public IEnumerable<Condition> conditions = [];
		float scroll = 0;
		bool cutOff = false;
		public void SetConditionsFrom(ItemSource itemSource) {
			SetConditions(itemSource.GetConditions(), itemSource.GetExtraConditionText());
			if (!conditionts.Any()) conditionts = [itemSource.SourceType.DisplayName];
		}
		public void SetConditions(IEnumerable<Condition> conditions, IEnumerable<LocalizedText> conditionts = null) {
			this.conditions = conditions ?? [];
			this.conditionts = conditionts ?? [];
			scroll = 0;
		}
		public int GetHeight() => /*!conditionts.Any() && !conditions.Any() ? 2 : */ 20;
		public override void Update(WindowElement parent, Range x, Range y) {
			int height = GetHeight();
			parent.Height -= parent.MinHeights[y.Start] - height;
			parent.MinHeights[y.Start] = height;
		}
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string commaText = ", ";
			float commaWidth = font.MeasureString(commaText).X * 0.75f;
			bool comma = false;
			cutOff = false;
			if (bounds.Contains(Main.mouseX, Main.mouseY)) this.CaptureScroll();
			int scrollFixTries = 0;
			while (scroll < 0 && ++scrollFixTries < 40) scroll++;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				Vector2 pos = bounds.TopLeft();
				float lastWidth = 0;
				float leftSide = pos.X;
				int rightSide = bounds.Right;
				pos.X -= scroll;
				void DrawText(string text, Color color) {
					if (comma) {
						spriteBatch.DrawString(
							font,
							commaText,
							pos,
							Color.White,
							0,
							new(0, 0f),
							0.75f,
							0,
						0);
						pos.X += commaWidth;
					}
					comma = true;
					spriteBatch.DrawString(
						font,
						text,
						pos,
						color,
						0,
						new(0, 0f),
						0.75f,
						0,
					0);
					lastWidth = font.MeasureString(text).X * 0.75f;
					pos.X += lastWidth;
					if (pos.X > rightSide) {
						cutOff = true;
					}
				}
				foreach (LocalizedText conditiont in conditionts) {
					DrawText(conditiont.Value, Color.White);
				}
				foreach (Condition condition in conditions) {
					DrawText(condition.Description.Value, Color.White);
				}
			}
		}
		public void Scroll(int direction) {
			if (cutOff && direction > 0 || scroll > 0 && direction < 0) scroll += direction * 20;
		}
		public override void Reset() {
			conditionts = [];
			conditions = [];
			scroll = 0;
		}
	}
	public abstract class ThingListGridItem<Thing> : GridItem, IScrollableUIItem {
		public IEnumerable<Thing> things;
		public int scroll;
		bool cutOff = false;
		int lastItemsPerRow = -1;
		public int doubleClickTime = 0;
		Thing doubleClickThing = default;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			Color color = this.color;
			spriteBatch.DrawRoundedRetangle(bounds, color);
			//bounds.X += 12;
			//bounds.Y += 10;
			//bounds.Width -= 8;
			bounds.Height -= 1;
			Point mousePos = Main.MouseScreen.ToPoint();
			if (bounds.Contains(mousePos)) {
				this.CaptureScroll();
				Main.LocalPlayer.mouseInterface = true;
			}
			cutOff = false;
			if (doubleClickTime > 0 && --doubleClickTime <= 0) doubleClickThing = default;
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
				int itemsPerRow = (int)(((maxX - padding) - minX - 1) / (float)sizeWithPadding) + 1;
				if (lastItemsPerRow != -1 && itemsPerRow != lastItemsPerRow) {
					int oldSkips = lastItemsPerRow * scroll;
					int newSkips = itemsPerRow * scroll;
					float ratio = oldSkips / (float)newSkips;
					scroll = (int)Math.Round(ratio * scroll);
				}
				Vector2 position = new();
				bool hadAnyItems = things.Any();
				bool displayedAnyItems = false;
				int overscrollFixerTries = 0;
				retry:
				foreach (Thing thing in things.Skip(itemsPerRow * scroll)) {
					if (x >= maxX - padding) {
						x = baseX;
						y += sizeWithPadding;
						if (y >= maxY - padding) {
							cutOff = true;
							break;
						}
					}
					hadAnyItems = true;
					if (x >= minX - size) {
						displayedAnyItems = true;
						position.X = x;
						position.Y = y;
						bool hovering = canHover && Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size;
						DrawThing(spriteBatch, thing, position, hovering);
						if (hovering && ((Main.mouseLeft && Main.mouseLeftRelease) || (Main.mouseRight && Main.mouseRightRelease))) {
							if (!Equals(thing, doubleClickThing)) doubleClickTime = 0;
							if (doubleClickTime > 0) {
								ClickThing(thing, true);
							} else {
								doubleClickTime = 15;
								doubleClickThing = thing;
								ClickThing(thing, false);
							}
						}
					}
					x += sizeWithPadding;
				}
				if (hadAnyItems && !displayedAnyItems && ++overscrollFixerTries < 100) {
					scroll--;
					goto retry;
				}
				lastItemsPerRow = itemsPerRow;
			}
		}
		public abstract void DrawThing(SpriteBatch spriteBatch, Thing thing, Vector2 position, bool hovering);
		public abstract bool ClickThing(Thing thing, bool doubleClick);
		//public abstract bool IsSameThing(Thing thing1, Thing thing2);
		public void Scroll(int direction) {
			if (!cutOff && direction > 0) return;
			if (scroll <= 0 && direction < 0) return;
			scroll += direction;
		}
		public override void Reset() {
			scroll = 0;
			lastItemsPerRow = -1;
		}
	}
	public class SlidyThingGridItem(SlidyThingGridItem.ValueReference valueReference, LocalizedText text, Action ExtraSwitchAction = null) : GridItem {
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			const int squish = 1;
			bounds.X += squish;
			bounds.Width -= squish * 2;
			ref bool value = ref valueReference();
			if (bounds.Contains(Main.mouseX, Main.mouseY)) {
				Main.LocalPlayer.mouseInterface = true;
				UIMethods.DrawRoundedRetangle(spriteBatch, bounds, new(0.1f, 0.1f, 0.1f, 0f));
				UIMethods.TryMouseText(text.Value);
				if (Main.mouseLeft && Main.mouseLeftRelease) {
					value = !value;
					ExtraSwitchAction?.Invoke();
				}
			} else {
				UIMethods.DrawRoundedRetangle(spriteBatch, bounds, new(0f, 0f, 0f, 0.4f));
			}
			UIMethods.DrawRoundedRetangle(spriteBatch, bounds, Color.White, ItemSourceHelper.InventoryBackOutline.Value);
			Rectangle slider = bounds;
			const int shrink = 1;
			slider.Width = slider.Height - shrink;
			//slider.Y += shrink;
			slider.X += value ? (bounds.Width - (slider.Width + shrink)) : shrink;
			UIMethods.DrawRoundedRetangle(spriteBatch, slider, Color.White);
		}
		public delegate ref bool ValueReference();
	}
	public class CornerSlotGridItem(FilterListGridItem<Item> filterList) : GridItem {
		Item item;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			item ??= new();
			Vector2 size = new Vector2(52 * Main.inventoryScale) * 0.5f;
			Vector2 pos = bounds.TopLeft();
			pos.X += 2;
			Texture2D backTexture = TextureAssets.InventoryBack13.Value;
			Color normalColor = ItemSourceHelperConfig.Instance.ItemSlotColor;
			Color hoverColor = ItemSourceHelperConfig.Instance.HoveredItemSlotColor;

			for (int i = 0; i < 2; i++) {
				for (int j = 0; j < 2; j++) {
					Vector2 offset = size * new Vector2(i, j);
					Rectangle frame = new(26 * i, 26 * j, 26, 26);
					bool hover = UIMethods.MouseInArea(pos + offset, size);
					spriteBatch.Draw(
						backTexture,
						pos + offset,
						frame,
						hover ? hoverColor : normalColor,
						0f,
						Vector2.Zero,
						Main.inventoryScale,
						SpriteEffects.None,
					0f);

					Vector2 tickOffset = new(3, 3);
					frame = new Rectangle(0, 0, 8, 8);
					IFilter<Item> filter = null;
					Color tickColor = Color.White;

					switch ((i, j)) {
						case (0, 0):
						filter = ModContent.GetInstance<CraftableFilter>();
						break;

						case (0, 1):
						tickOffset.Y = size.Y - (8 + 3);
						frame.X = 10;
						filter = ModContent.GetInstance<MaterialFilter>();
						break;

						case (1, 0):
						tickOffset.X = size.X - (8 + 3);
						frame.X = 20;
						filter = ModContent.GetInstance<NPCLootFilter>();
						break;

						case (1, 1):
						tickOffset.X = size.X - (8 + 3);
						tickOffset.Y = size.Y - (8 + 3);
						frame.X = 30;
						filter = ModContent.GetInstance<ItemLootFilter>();
						break;
					}
					if (filter is not null && filterList is not null) {
						if (!filterList.activeFilters.IsFilterActive(filter, out bool isNot)) tickColor *= 0.5f;
						else if (isNot) tickColor = Color.Gray;
					}
					spriteBatch.Draw(
						ItemSourceHelper.ItemIndicators.Value,
						pos + offset + tickOffset,
						frame,
						tickColor
					);
					if (hover && filter is not null) {
						Main.LocalPlayer.mouseInterface = true;
						UIMethods.TryMouseText(filter.DisplayNameText);
						if (filterList is not null && Main.mouseLeft && Main.mouseLeftRelease) {
							filterList.SetFilter(filter, null, Main.keyState.PressingShift(), Main.keyState.PressingControl());
						}
					}
				}
			}
		}
		public void SetItem(int type) => SetItem(new Item(type));
		public void SetItem(Item type) {
			item = type?.Clone() ?? new();
			ItemSourceHelper.Instance.BrowserWindow.ActiveSourceFilters.SetFilterItem(item);
		}
		public override void Reset() {
			item ??= new();
			item.TurnToAir();
		}
	}
	public abstract class WindowElement : UIElement, ILoadable, ILocalizedModType, IComparable<WindowElement> {
		#region other
		public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');
		protected Asset<Texture2D> texture;
		public virtual Texture2D TextureValue => texture.Value;
		protected DrawAnimation animation;
		public virtual DrawAnimation TextureAnimation => animation;
		public string LocalizationCategory => "BrowserMode";
		public virtual LocalizedText DisplayName => Mod is null ? ItemSourceHelper.GetLocalization(this) : this.GetLocalization("DisplayName");
		public virtual int DisplayNameRarity => ItemRarityID.White;
		public virtual string DisplayNameText => DisplayName.Value;
		public void Load(Mod mod) {
			MinWidth = new(180, 0);
			MinHeight = new(242, 0);
			MarginTop = 6; MarginLeft = 4; MarginBottom = 4; MarginRight = 4;
			Mod = mod;
			ItemSourceBrowser.Windows.Add(this);
			if (!ModContent.RequestIfExists(Texture, out texture)) {
				texture = Asset<Texture2D>.Empty;
			}
			SetDefaults();
			Initialize();
		}
		public abstract Color BackgroundColor { get; }
		public void Unload() {}
		public abstract void SetDefaults();
		public abstract void SetDefaultSortMethod();
		public virtual void OnLostFocus() { }
		///<summary>
		/// The mod this belongs to.
		/// </summary>
		public Mod Mod { get; internal set; }
		///<summary>
		/// The index of this mode in.
		/// </summary>
		public int Index { get; internal set; }

		/// <summary>
		/// The internal name of this.
		/// </summary>
		public virtual string Name => GetType().Name;

		/// <summary>
		/// The internal name of this, including the mod it is from.
		/// </summary>
		public string FullName => $"{Mod?.Name ?? "Terraria"}/{Name}";
		public float sortOrder = 0;
		public int CompareTo(WindowElement other) => Comparer<float>.Default.Compare(sortOrder, other.sortOrder);
		#endregion
		#region resize
		public RectangleHandles handles = RectangleHandles.Top | RectangleHandles.Left;
		bool heldHandleStretch;
		RectangleHandles heldHandle;
		Vector2 heldHandleOffset;
		public Color color => BackgroundColor;
		public bool blockUseHandles = false;
		public override void OnInitialize() {
			heights = new float[HeightWeights.Length];
			widths = new float[WidthWeights.Length];
			//this.onu
		}
		public void Resize() {
			float margins = MarginLeft + MarginRight;
			float minSize = Math.Max(calculatedMinWidth, MinWidth.Pixels) + margins;
			if (Width < minSize) Width = minSize;
			float flexWidth = Width - margins - unweightedWidth;

			margins = MarginTop + MarginBottom;
			minSize = Math.Max(calculatedMinHeight, MinHeight.Pixels) + margins;
			if (Height < minSize) Height = minSize;
			float flexHeight = Height - margins - unweightedHeight;
			if (totalWidthWeight > 0) {
				for (int i = 0; i < WidthWeights.Length; i++) {
					float width = WidthWeights[i] * (flexWidth / totalWidthWeight);
					widths[i] = MinWidths[i] + width;
				}
			}
			if (totalHeightWeight > 0) {
				for (int i = 0; i < HeightWeights.Length; i++) {
					float height = HeightWeights[i] * (flexHeight / totalHeightWeight);
					heights[i] = MinHeights[i] + height;
				}
			}
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Texture2D texture = TextureAssets.InventoryBack16.Value;
			Texture2D handleTexture = TextureAssets.InventoryBack13.Value;
			Rectangle textureBounds = texture.Bounds;

			Color handleHoverColor = color * 1.2f;
			Color nonHandleHoverColor = color.MultiplyRGB(new(210, 210, 210));

			Rectangle area = GetOuterDimensions().ToRectangle();
			Point mousePos = Main.MouseScreen.ToPoint();
			if (Left > Main.screenWidth - Width) Left = Main.screenWidth - Width;
			if (Top > Main.screenHeight - Height) Top = Main.screenHeight - Height;
			bool hoveringSomewhere = false;
			foreach (var segment in UIMethods.RectangleSegments) {
				Rectangle bounds = segment.GetBounds(area);
				bool matches = segment.Matches(handles);
				Color partColor = color;
				bool discolor = false;
				if (!blockUseHandles) {
					if (heldHandle == 0) {
						if (bounds.Contains(mousePos)) {
							hoveringSomewhere = true;
							if (segment.Handles != 0) {
								Main.LocalPlayer.mouseInterface = true;
								discolor = true;
								if (Main.mouseLeft && Main.mouseLeftRelease) {
									heldHandleStretch = !matches;
									heldHandle = segment.Handles;
									if (matches) {
										heldHandleOffset = new Vector2(Left, Top) - Main.MouseScreen;
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
			blockUseHandles = Main.LocalPlayer.mouseInterface;
			DrawCells(spriteBatch);
			blockUseHandles ^= Main.LocalPlayer.mouseInterface;
			Main.LocalPlayer.mouseInterface |= hoveringSomewhere;
		}
		public new ref float Left => ref ItemSourceHelperPositions.Instance.SourceBrowserLeft;
		public new ref float Top => ref ItemSourceHelperPositions.Instance.SourceBrowserTop;
		public new ref float Width => ref ItemSourceHelperPositions.Instance.SourceBrowserWidth;
		public new ref float Height => ref ItemSourceHelperPositions.Instance.SourceBrowserHeight;
		public new CalculatedStyle GetInnerDimensions() {
			CalculatedStyle calculatedStyle = new(Left, Top, Width, Height);
			calculatedStyle.X += MarginLeft;
			calculatedStyle.Y += MarginTop;
			calculatedStyle.Width -= MarginLeft + MarginRight;
			calculatedStyle.Height -= MarginTop + MarginBottom;
			calculatedStyle.X += PaddingLeft;
			calculatedStyle.Y += PaddingTop;
			calculatedStyle.Width -= PaddingLeft + PaddingRight;
			calculatedStyle.Height -= PaddingTop + PaddingBottom;
			return calculatedStyle;
		}
		public new CalculatedStyle GetOuterDimensions() {
			return new CalculatedStyle(Left, Top, Width, Height);
		}
		#endregion resize
		#region grid
		float totalWidthWeight;
		float totalHeightWeight;
		float calculatedMinWidth;
		float calculatedMinHeight;
		public Dictionary<int, GridItem> items;
		public int[,] itemIDs;
		protected float[] widths;
		protected float[] heights;
		float unweightedWidth;
		float unweightedHeight;
		public ObservableArray<float> WidthWeights;
		public ObservableArray<float> HeightWeights;
		public ObservableArray<float> MinWidths;
		public ObservableArray<float> MinHeights;
		public void ResetItems() {
			foreach (GridItem item in items.Values) {
				item.Reset();
			}
		}
		public override void Update(GameTime gameTime) {
			if (heldHandle != 0) {
				Main.LocalPlayer.mouseInterface = true;
				bool changed = false;
				if (heldHandleStretch) {
					Rectangle area = GetOuterDimensions().ToRectangle();
					if (heldHandle.HasFlag(RectangleHandles.Left)) {
						Vector2 pos = Main.MouseScreen + heldHandleOffset;
						Width += Left - (int)pos.X;
						Left = (int)pos.X;
						changed = true;
					} else if (heldHandle.HasFlag(RectangleHandles.Right)) {
						Width = (int)Main.MouseScreen.X + heldHandleOffset.X - area.X;
						changed = true;
					}
					if (heldHandle.HasFlag(RectangleHandles.Top)) {
						Vector2 pos = Main.MouseScreen + heldHandleOffset;
						Height += Top - (int)pos.Y;
						Top = (int)pos.Y;
						changed = true;
					} else if (heldHandle.HasFlag(RectangleHandles.Bottom)) {
						Height = (int)Main.MouseScreen.Y + heldHandleOffset.Y - area.Y;
						changed = true;
					}
				} else {
					Vector2 pos = Main.MouseScreen + heldHandleOffset;
					Left = (int)pos.X;
					Top = (int)pos.Y;
					if (Left > Main.screenWidth - Width) Left = Main.screenWidth - Width;
					if (Top > Main.screenHeight - Height) Top = Main.screenHeight - Height;
				}
				if (!Main.mouseLeft) {
					heldHandle = 0;
				}
				if (changed) {
					Resize();
					Recalculate();
				}
			}
			CheckSizes();
			int hCells = itemIDs.GetLength(0);
			int vCells = itemIDs.GetLength(1);
			foreach (KeyValuePair<int, GridItem> item in items) {
				for (int y = 0; y < vCells; y++) {
					for (int x = 0; x < hCells; x++) {
						if (itemIDs[x, y] == item.Key) {
							int currentX = x;
							while (ShouldMerge(currentX, y, currentX + 1, y)) {
								currentX++;
							}
							int currentY = y;
							while (ShouldMerge(x, currentY, x, currentY + 1)) {
								currentY++;
							}
							item.Value.Update(this, x..currentX, y..currentY);
							goto cont;
						}
					}
				}
				cont:;
			}
		}
		public void CheckSizes() {
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
				unweightedWidth = 0;
				for (int i = 0; i < WidthWeights.Length; i++) {
					unweightedWidth += MinWidths[i] + 2;
					float value = MinWidths[i] * (WidthWeights[i] / totalWidthWeight);
					if (calculatedMinWidth < value) calculatedMinWidth = value;
				}
				if (calculatedMinWidth < unweightedWidth) calculatedMinWidth = unweightedWidth;
				anyChanged = true;
			}
			if (HeightWeights.Changed || MinHeights.Changed) {
				HeightWeights.ConsumeChange();
				MinHeights.ConsumeChange();
				calculatedMinHeight = 0;
				unweightedHeight = 0;
				for (int i = 0; i < HeightWeights.Length; i++) {
					unweightedHeight += MinHeights[i] + 2;
					float value = MinHeights[i] * (HeightWeights[i] / totalHeightWeight);
					if (calculatedMinHeight < value) calculatedMinHeight = value;
				}
				if (calculatedMinHeight < unweightedHeight) calculatedMinHeight = unweightedHeight;
				anyChanged = true;
			}
			if (anyChanged) {
				Resize();
			}
		}
		public void DrawCells(SpriteBatch spriteBatch) {
			int hCells = itemIDs.GetLength(0);
			int vCells = itemIDs.GetLength(1);
			CalculatedStyle style = GetInnerDimensions();
			int padding = 2;
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
					int mergeID = itemIDs[x, y];
					float width = widths[x];
					int boxX = (int)cellX;
					cellX += width + padding;
					if (mergeID == -1 || processedIDs.ContainsKey(mergeID)) continue;
					if (width == 0) continue;
					int currentX = x;
					while (ShouldMerge(currentX, y, currentX + 1, y)) {
						currentX++;
						width += widths[currentX] + padding;
					}
					height = heights[y];
					int currentY = y;
					while (ShouldMerge(x, currentY, x, currentY + 1)) {
						currentY++;
						height += heights[currentY] + padding;
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
			int hCells = itemIDs.GetLength(0);
			int vCells = itemIDs.GetLength(1);
			if (aX >= hCells || aY >= vCells || bX >= hCells || bY >= vCells) return false;
			return itemIDs[aX, aY] == itemIDs[bX, bY];
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
		public Color color => colorFunc();
		public Func<Color> colorFunc;
		public virtual void Update(WindowElement parent, Range x, Range y) { }
		public virtual void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			Color color = this.color;
			if (bounds.Contains(Main.MouseScreen.ToPoint())) {
				color *= 1.2f;
			}
			spriteBatch.DrawRoundedRetangle(bounds, color);
		}
		public virtual void Reset() { }
	}
	public static class UIMethods {
		public static bool ShouldReplace<T>(this IFilter<T> self, IFilter<T> other) => self.FilterChannel == -1 ? other.Type == self.Type : other.FilterChannel == self.FilterChannel;
		public static bool DoesntMatch<T>(this IFilter<T> self, T source) => !self.MatchesAll(source);
		internal static bool DoesntMatch<T>(this IFilter<T> self, (T source, int index) data) => !self.MatchesAll(data.source);
		public static void DrawRoundedRetangle(this SpriteBatch spriteBatch, Rectangle rectangle, Color color, Texture2D texture = null) {
			texture ??= TextureAssets.InventoryBack13.Value;
			Rectangle textureBounds = texture.Bounds;
			foreach (var segment in RectangleSegments) {
				spriteBatch.Draw(
					texture,
					segment.GetBounds(rectangle),
					segment.GetBounds(textureBounds),
					color
				);
			}
		}
		public static void DrawPartialRoundedRetangle(this SpriteBatch spriteBatch, Rectangle rectangle, Color color, Texture2D texture = null, params RectangleHandles[] sides) {
			texture ??= TextureAssets.InventoryBack13.Value;
			Rectangle textureBounds = texture.Bounds;
			foreach (var segment in RectangleSegments) {
				if (sides.Contains(segment.Handles)) spriteBatch.Draw(
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
		public static void TryMouseText(string text, int rarity = 0, string tooltip = null) {
			if (!Main.mouseText) {
				Main.instance.MouseTextHackZoom(text, rarity, 0, tooltip);
				Main.mouseText = true;
			}
		}
		public static bool PressingAlt(this KeyboardState state) => state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
		public static bool JustPressed(Keys key) => Main.inputText.IsKeyDown(key) && !Main.oldInputText.IsKeyDown(key);
		public static void DrawColoredItemSlot(SpriteBatch spriteBatch, ref Item item, Vector2 position, Texture2D backTexture, Color slotColor, Color lightColor = default, Color textColor = default, string beforeText = null, string afterText = null) {
			DrawColoredItemSlot(spriteBatch, [item], 0, position, backTexture, slotColor, lightColor, textColor, beforeText, afterText);
		}
		public static IEnumerable<UIElement> Descendants(this UIElement self) {
			return ((IEnumerable<UIElement>)[self]).Concat(self.Children.SelectMany(Descendants));
		}
		public static void DrawColoredItemSlot(SpriteBatch spriteBatch, Item[] items, int slot, Vector2 position, Texture2D backTexture, Color slotColor, Color lightColor = default, Color textColor = default, string beforeText = null, string afterText = null) {
			spriteBatch.Draw(backTexture, position, null, slotColor, 0f, Vector2.Zero, Main.inventoryScale, SpriteEffects.None, 0f);
			if (beforeText is not null) {
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, beforeText, position + new Vector2(8f, 4f) * Main.inventoryScale, textColor, 0f, Vector2.Zero, new Vector2(Main.inventoryScale), -1f, Main.inventoryScale);
			}
			ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.ChatItem, slot, position, lightColor);
			if (afterText is not null) {
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, afterText, position + new Vector2(8f, 4f) * Main.inventoryScale, textColor, 0f, Vector2.Zero, new Vector2(Main.inventoryScale), -1f, Main.inventoryScale);
			}
		}
		public static StretchSegment[] RectangleSegments { get; private set; } = [
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
			RectangleSegments = null;
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
		/*public class ClippingStencil : IDisposable {
			readonly DepthStencilState stencil;
			readonly RasterizerState rasterizerState;
			readonly SpriteBatch spriteBatch;
			public ClippingStencil(SpriteBatch spriteBatch, params DrawData[] stencilData) {
				this.spriteBatch = spriteBatch;
				stencil = spriteBatch.GraphicsDevice.DepthStencilState;
				rasterizerState = spriteBatch.GraphicsDevice.RasterizerState;

				spriteBatch.End();
				Rectangle adjustedClippingRectangle = Rectangle.Intersect(area.Scale(), spriteBatch.GraphicsDevice.ScissorRectangle);
				spriteBatch.GraphicsDevice.ScissorRectangle = adjustedClippingRectangle;
				spriteBatch.GraphicsDevice.RasterizerState = OverflowHiddenRasterizerState;
				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, OverflowHiddenRasterizerState, null, Main.UIScaleMatrix);
			}
			public void Dispose() {
				spriteBatch.End();
				spriteBatch.GraphicsDevice.DepthStencilState = stencil;
				spriteBatch.GraphicsDevice.RasterizerState = rasterizerState;
				spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, rasterizerState, null, Main.UIScaleMatrix);
			}
			private static readonly RasterizerState OverflowHiddenRasterizerState = new() {
				CullMode = CullMode.None,
				ScissorTestEnable = true
			};
		}*/
		public static void CaptureScroll(this IScrollableUIItem scrollable) {
			ScrollingPlayer.scrollable = scrollable;
		}
		public static IEnumerable<TResult> TryCast<TResult>(this IEnumerable source) {
			if (source is IEnumerable<TResult> typedSource) return typedSource;
			ArgumentNullException.ThrowIfNull(source);
			return TryCastIterator<TResult>(source);
		}

		private static IEnumerable<TResult> TryCastIterator<TResult>(IEnumerable source) {
			foreach (object obj in source) {
				if (obj is TResult result) yield return result;
			}
		}
		public static IEnumerable<string> GetLines(this ItemTooltip tooltip) {
			for (int i = 0; i < tooltip.Lines; i++) {
				yield return tooltip.GetLine(i);
			}
		}
		public static void DrawIndicators(SpriteBatch spriteBatch, int itemType, IndicatorTypes mask, Vector2 position, int size) {
			void DoMask(IndicatorTypes type, HashSet<int> set) {
				if ((mask & type) != IndicatorTypes.None && !set.Contains(itemType)) mask ^= type;
			}
			DoMask(IndicatorTypes.Craftable, ItemSourceHelper.Instance.CraftableItems);
			DoMask(IndicatorTypes.Material, ItemSourceHelper.Instance.MaterialItems);
			DoMask(IndicatorTypes.NPCDrop, ItemSourceHelper.Instance.NPCLootItems);
			DoMask(IndicatorTypes.ItemDrop, ItemSourceHelper.Instance.ItemLootItems);
			DrawIndicators(spriteBatch, mask, position, size, Color.White);
		}
		public static void DrawIndicators(SpriteBatch spriteBatch, IndicatorTypes types, Vector2 position, int size, Color color) {
			if (types.HasFlag(IndicatorTypes.Craftable)) {
				spriteBatch.Draw(
					ItemSourceHelper.ItemIndicators.Value,
					position + new Vector2(3, 3),
					new Rectangle(0, 0, 8, 8),
					color
				);
			}
			if (types.HasFlag(IndicatorTypes.Material)) {
				spriteBatch.Draw(
					ItemSourceHelper.ItemIndicators.Value,
					position + new Vector2(3, size - (8 + 3)),
					new Rectangle(10, 0, 8, 8),
					color
				);
			}
			if (types.HasFlag(IndicatorTypes.NPCDrop)) {
				spriteBatch.Draw(
					ItemSourceHelper.ItemIndicators.Value,
					position + new Vector2(size - (8 + 3), 3),
					new Rectangle(20, 0, 8, 8),
					color
				);
			}
			if (types.HasFlag(IndicatorTypes.ItemDrop)) {
				spriteBatch.Draw(
					ItemSourceHelper.ItemIndicators.Value,
				position + new Vector2(size - (8 + 3), size - (8 + 3)),
				new Rectangle(30, 0, 8, 8),
					color
				);
			}
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
	[Flags, CustomModConfigItem(typeof(IndicatorTypesConfigElement))]
	public enum IndicatorTypes {
		None      = 0x0000,
		Craftable = 0x0001,
		Material  = 0x0010,
		NPCDrop   = 0x0100,
		ItemDrop  = 0x1000,
		All       = 0x1111
	}
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
	public sealed class IndicatorTypeMaskAttribute(IndicatorTypes mask) : Attribute {
		public IndicatorTypes Mask { get; } = mask;
	}
	public class IndicatorTypesConfigElement : ConfigElement<IndicatorTypes> {
		protected bool inverted = false;
		public IndicatorTypes allowedMask = IndicatorTypes.All;
		public override void OnBind() {
			base.OnBind();
			base.TextDisplayFunction = TextDisplayOverride ?? base.TextDisplayFunction;
			if (MemberInfo.MemberInfo.GetCustomAttribute<IndicatorTypeMaskAttribute>() is IndicatorTypeMaskAttribute indicatorTypeMask) {
				allowedMask = indicatorTypeMask.Mask;
			}
			Append(new Slot(this) {
				Width = new(52, 0),
				Height = new(52, 0),
				HAlign = 1
			});
			Height.Set(52, 0);
		}
		public Func<string> TextDisplayOverride { get; set; }
		public new IndicatorTypes Value {
			get => base.Value;
			set => base.Value = value;
		}
		public class Slot(IndicatorTypesConfigElement parent) : UIElement {
			IndicatorTypes hovered = IndicatorTypes.None;
			public override void Draw(SpriteBatch spriteBatch) {
				Rectangle bounds = this.GetDimensions().ToRectangle();
				Vector2 size = new Vector2(bounds.Width) * 0.5f * 0.85f;
				Vector2 pos = bounds.Center.ToVector2() - size;
				pos.X += 2;
				Texture2D backTexture = TextureAssets.InventoryBack13.Value;
				Color normalColor = ItemSourceHelperConfig.Instance.ItemSlotColor;
				Color hoverColor = ItemSourceHelperConfig.Instance.HoveredItemSlotColor;
				hovered = IndicatorTypes.None;
				IndicatorTypes value = parent.Value;
				for (int i = 0; i < 2; i++) {
					for (int j = 0; j < 2; j++) {
						Vector2 offset = size * new Vector2(i, j);
						Rectangle frame = new(26 * i, 26 * j, 26, 26);
						bool hover = UIMethods.MouseInArea(pos + offset, size);
						spriteBatch.Draw(
							backTexture,
							pos + offset,
							frame,
							hover ? hoverColor : normalColor,
							0f,
							Vector2.Zero,
							0.85f,
							SpriteEffects.None,
						0f);

						Vector2 tickOffset = new(3, 3);
						frame = new Rectangle(0, 0, 8, 8);
						IFilter<Item> filter = null;
						IndicatorTypes type = IndicatorTypes.None;

						switch ((i, j)) {
							case (0, 0):
							type = IndicatorTypes.Craftable;
							filter = ModContent.GetInstance<CraftableFilter>();
							break;

							case (0, 1):
							tickOffset.Y = size.Y - (8 + 3);
							frame.X = 10;
							type = IndicatorTypes.Material;
							filter = ModContent.GetInstance<MaterialFilter>();
							break;

							case (1, 0):
							tickOffset.X = size.X - (8 + 3);
							frame.X = 20;
							type = IndicatorTypes.NPCDrop;
							filter = ModContent.GetInstance<NPCLootFilter>();
							break;

							case (1, 1):
							tickOffset.X = size.X - (8 + 3);
							tickOffset.Y = size.Y - (8 + 3);
							frame.X = 30;
							type = IndicatorTypes.ItemDrop;
							filter = ModContent.GetInstance<ItemLootFilter>();
							break;
						}
						if ((parent.allowedMask & type) == IndicatorTypes.None) continue;
						Color tickColor = (value & type) != IndicatorTypes.None ? Color.White : Color.Gray;
						spriteBatch.Draw(
							ItemSourceHelper.ItemIndicators.Value,
							pos + offset + tickOffset,
							frame,
							tickColor
						);
						if (hover && filter is not null) {
							Main.LocalPlayer.mouseInterface = true;
							UICommon.TooltipMouseText(filter.DisplayNameText);
							hovered = type;
						}
					}
				}
			}
			public override void LeftClick(UIMouseEvent evt) {
				parent.Value ^= hovered;
			}
		}
	}
}
