using ItemSourceHelper.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using ReLogic.OS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace ItemSourceHelper {
	public class ItemSourceBrowser : GameInterfaceLayer {
		public WindowElement SourceBrowser { get; private set; }
		public FilterListGridItem<ItemSource> FilterList { get; private set; }
		public ItemSourceListGridItem SourceList { get; private set; }
		public IngredientListGridItem Ingredience { get; private set; }
		public FilteredEnumerable<ItemSource> ActiveSourceFilters { get; private set; }
		public SingleSlotGridItem FilterItem { get; private set; }
		public ConditionsGridItem ConditionsItem { get; private set; }
		public SearchGridItem SearchItem { get; private set; }
		public ItemListGridItem ItemList { get; private set; }
		public FilterListGridItem<Item> ItemFilterList { get; private set; }
		public FilteredEnumerable<Item> ActiveItemFilters { get; private set; }
		public WindowElement ItemBrowser { get; private set; }
		public static bool isItemBrowser = false;
		public ItemSourceBrowser() : base($"{nameof(ItemSourceHelper)}: Browser", InterfaceScaleType.UI) {
			SlidyThingGridItem slidyThing = new(() => ref isItemBrowser, Language.GetOrRegister("Mods.ItemSourceHelper.BrowserToggleTip"), () => {
				ItemSourceBrowser window = ItemSourceHelper.Instance.BrowserWindow;
				if (isItemBrowser) {
					//window.SourceBrowser.items[5].
					int height = window.ConditionsItem.GetHeight();
					window.SourceBrowser.Height -= height - 2;
					window.ItemBrowser.Resize();
				} else {
					int height = window.ConditionsItem.GetHeight();
					window.SourceBrowser.Height += height - 2;
					window.SourceBrowser.Resize();
				}
			});
			SourceBrowser = new() {
				handles = RectangleHandles.Top | RectangleHandles.Left,
				MinWidth = new(180, 0),
				MinHeight = new(242, 0),
				MarginTop = 6, MarginLeft = 4, MarginBottom = 0, MarginRight = 4,
				items = new() {
					[1] = FilterList = new() {
						filters = ItemSourceHelper.Instance.Filters,
						activeFilters = ActiveSourceFilters = new(),
						sorters = ItemSourceHelper.Instance.SourceSorters,
						ResetScroll = () => SourceList.scroll = 0
					},
					[2] = SourceList = new() {
						items = ActiveSourceFilters
					},
					[3] = Ingredience = new() {
						items = []
					},
					[4] = FilterItem = new(),
					[5] = ConditionsItem = new(),
					[6] = SearchItem = new(),
					[7] = slidyThing,
				},
				itemIDs = new int[3, 7] {
					{ 6, 4, 4, 7, 2, 3, 5 },
					{ 6, 1, 1, 1, 2, 3, 5 },
					{ 6, 1, 1, 1, 2, 3, 5 }
				},
				WidthWeights = new([0f, 3, 3]),
				HeightWeights = new([0f, 0f, 0f, 0f, 3f, 0f, 0f]),
				MinWidths = new([43, 180, 180]),
				MinHeights = new([31, 21, 16, 21, 132, 53, 20]),
			};
			SourceBrowser.Initialize();

			ItemBrowser = new() {
				handles = RectangleHandles.Top | RectangleHandles.Left,
				MinWidth = new(180, 0),
				MinHeight = new(242, 0),
				MarginTop = 6, MarginLeft = 4, MarginBottom = 4, MarginRight = 4,
				items = new() {
					[1] = ItemFilterList = new() {
						filters = ItemSourceHelper.Instance.Filters.TryCast<ItemFilter>(),
						activeFilters = ActiveItemFilters = new(),
						sorters = ItemSourceHelper.Instance.SourceSorters.TryCast<ItemSorter>(),
						ResetScroll = () => ItemList.scroll = 0
					},
					[2] = ItemList = new() {
						items = ActiveItemFilters
					},
					[4] = FilterItem,
					[6] = SearchItem,
					[7] = slidyThing,
				},
				itemIDs = new int[3, 7] {
					{ 6, -1, -1, 7, 2, 2, 2 },
					{ 6, 1, 1, 1, 2, 2, 2 },
					{ 6, 1, 1, 1, 2, 2, 2 }
				},
				WidthWeights = new([0f, 3, 3]),
				HeightWeights = new([0f, 0f, 0f, 0f, 3f, 0f, 0f]),
				MinWidths = new([43, 180, 180]),
				MinHeights = new([31, 21, 16, 21, 132, 53, 2]),
			};
			ItemBrowser.Initialize();
		}
		protected override bool DrawSelf() {
			if (ItemSourceHelperPositions.Instance is null) return true;
			WindowElement browser = isItemBrowser ? ItemBrowser : SourceBrowser;
			browser.Update(new GameTime());
			float inventoryScale = Main.inventoryScale;
			Main.inventoryScale = 0.75f;
			browser.Recalculate();
			browser.Draw(Main.spriteBatch);
			Main.inventoryScale = inventoryScale;
			return true;
		}
		public void Reset() {
			SourceBrowser.ResetItems();
			ActiveSourceFilters.ClearSelectedFilters();
			ItemBrowser.ResetItems();
			ActiveItemFilters.ClearSelectedFilters();
			isItemBrowser = false;
		}
	}
	public class FilterListGridItem<T> : GridItem, IScrollableUIItem {
		public FilteredEnumerable<T> activeFilters;
		public IEnumerable<IFilter<T>> filters;
		public IEnumerable<ISorter<T>> sorters;
		public Action ResetScroll { get; init; }
		IFilter<T> lastFilter;
		int scrollTop;
		bool cutOffTop;
		int scrollBottom;
		bool cutOffBottom;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Height -= 1;
			Texture2D actuator = TextureAssets.Actuator.Value;
			bool shouldResetScroll = false;
			bounds.X += 3;
			bounds.Width -= 3;
			using (new UIMethods.ClippingRectangle(bounds, spriteBatch)) {
				bool canHover = bounds.Contains(Main.mouseX, Main.mouseY);
				if (canHover) {
					this.CaptureScroll();
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
				cutOffTop = false;
				cutOffBottom = false;
				Point mousePos = Main.MouseScreen.ToPoint();
				Color color = new(0, 0, 0, 50);
				Color hiColor = new(50, 50, 50, 0);
				Rectangle button = new(x, y, size, size);
				if (activeFilters.FilterCount != 0) {
					if (canHover && button.Contains(mousePos)) {
						spriteBatch.Draw(actuator, button, Color.Pink);
						if (Main.mouseLeft && Main.mouseLeftRelease) {
							activeFilters.ClearSelectedFilters();
							lastFilter = null;
						}
					} else {
						spriteBatch.Draw(actuator, button, Color.Red);
					}
					x += sizeWithPadding;
					minX += sizeWithPadding;
				}

				int lastFilterChannel = -1;
				foreach (IFilter<T> filter in filters) {
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
						int index = activeFilters.FindFilterIndex(filter.ShouldReplace);
						bool filterIsActive = activeFilters.IsFilterActive(filter);
						if (button.Contains(mousePos)) {
							UIMethods.DrawRoundedRetangle(spriteBatch, button, hiColor);
							UIMethods.TryMouseText(filter.DisplayNameText);
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								bool recalculate = false;
								if (filterIsActive) {
									activeFilters.TryRemoveFilter(filter);
									if (lastFilter == filter) lastFilter = null;
									recalculate = true;
								} else {
									recalculate = activeFilters.TryAddFilter(filter);
									if (filter.ChildFilters().Any()) lastFilter = filter;
								}
								if (recalculate) {
									activeFilters.RemoveOrphans();
									activeFilters.ClearCache();
								}
								/*if (index == -1) {
									activeFilters.AddSelectedFilter(filter);
									if (filter.ChildFilters().Any()) lastFilter = filter;
									shouldResetScroll = true;
								} else if (filter.Type == activeFilters.GetFilter(index).Type) {
									activeFilters.RemoveSelectedFilter(index);
									if (lastFilter == filter) lastFilter = null;
									index = -1;
								} else {
									activeFilters.RemoveSelectedFilter(index, filter);
									if (lastFilter == filter) lastFilter = null;
									if (filter.ChildFilters().Any()) lastFilter = filter;
								}*/
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
						Texture2D texture = filter.TextureAsset.Value;
						spriteBatch.Draw(texture, texture.Size().RectWithinCentered(button, 8), Color.White);
						if (filterIsActive) {//index != -1 && filter.Type == activeFilters.GetFilter(index).Type
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
						cutOffTop = true;
					}
				}
				x = baseX - scrollBottom * sizeWithPadding;
				y += sizeWithPadding;
				if (sorters is not null) {
					foreach (ISorter<T> sorter in sorters) {
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
						}
						x += sizeWithPadding;
						if (x >= maxX) {
							cutOffBottom = true;
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
						if (x >= baseX - size) {
							button.X = x;
							button.Y = y;
							int index = activeFilters.FindFilterIndex(filter.ShouldReplace);
							bool filterIsActive = activeFilters.IsFilterActive(filter);
							if (button.Contains(mousePos)) {
								UIMethods.DrawRoundedRetangle(spriteBatch, button, hiColor);
								UIMethods.TryMouseText(filter.DisplayNameText);
								if (Main.mouseLeft && Main.mouseLeftRelease) {
									bool recalculate = false;
									if (filterIsActive) {
										activeFilters.TryRemoveFilter(filter);
										recalculate = true;
									} else {
										recalculate = activeFilters.TryAddFilter(filter, Main.keyState.PressingShift());
									}
									if (recalculate) {
										activeFilters.RemoveOrphans();
										activeFilters.ClearCache();
									}
									/*if (index == -1) {
										activeFilters.AddSelectedFilter(filter);
										shouldResetScroll = true;
									} else if (filter.Type == activeFilters.GetFilter(index).Type) {
										activeFilters.RemoveSelectedFilter(index);
										index = -1;
									} else {
										activeFilters.RemoveSelectedFilter(index, filter);
										shouldResetScroll = true;
									}*/
								}
							} else {
								UIMethods.DrawRoundedRetangle(spriteBatch, button, color);
							}
							Texture2D texture = filter.TextureAsset.Value;
							spriteBatch.Draw(texture, texture.Size().RectWithinCentered(button, 8), Color.White);
							if (filterIsActive) {//index != -1 && filter.Type == activeFilters.GetFilter(index).Type
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
							cutOffBottom = true;
						}
					}
				}
			}
			if (shouldResetScroll && ResetScroll is not null) ResetScroll();
		}
		public void Scroll(int direction) {
			if (cutOffTop && direction > 0 || scrollTop > 0 && direction < 0) scrollTop += direction;
			if (cutOffBottom && direction > 0 || scrollBottom > 0 && direction < 0) scrollBottom += direction;
		}
		public override void Reset() {
			scrollTop = 0;
			scrollBottom = 0;
			lastFilter = null;
		}
	}
	public class ItemSourceListGridItem : GridItem, IScrollableUIItem {
		public IEnumerable<ItemSource> items;
		public int scroll;
		bool cutOff = false;
		int lastItemsPerRow = -1;
		int doubleClickTime = 0;
		int doubleClickItem = 0;
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
			if (doubleClickTime > 0 && --doubleClickTime <= 0) doubleClickItem = 0;
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
				Color normalColor = ItemSourceHelperConfig.Instance.ItemSlotColor;
				Color hoverColor = ItemSourceHelperConfig.Instance.HoveredItemSlotColor;
				bool hadAnyItems = items.Any();
				bool displayedAnyItems = false;
				int overscrollFixerTries = 0;
				retry:
				foreach (ItemSource itemSource in items.Skip(itemsPerRow * scroll)) {
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
						Item item = itemSource.Item;
						position.X = x;
						position.Y = y;
						bool hover = canHover && Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size;
						UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, texture, hover ? hoverColor : normalColor);
						if (hover) {
							if (items is FilteredEnumerable<ItemSource> filteredEnum) filteredEnum.FillTooltipAdders(TooltipAdderGlobal.TooltipModifiers);
							ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								ItemSourceHelper.Instance.BrowserWindow.Ingredience.items = itemSource.GetSourceItems().ToArray();
								ItemSourceHelper.Instance.BrowserWindow.ConditionsItem.SetConditionsFrom(itemSource);
								if (item.type != doubleClickItem) doubleClickTime = 0;
								if (doubleClickTime > 0) {
									ItemSourceHelper.Instance.BrowserWindow.FilterItem.SetItem(item);
									ItemSourceBrowser.isItemBrowser = false;
									break;
								} else {
									doubleClickTime = 15;
									doubleClickItem = item.type;
								}
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
	public class IngredientListGridItem : GridItem, IScrollableUIItem {
		public Item[] items;
		int scroll;
		bool cutOff;
		int doubleClickTime = 0;
		int doubleClickItem = 0;
		public override void DrawSelf(Rectangle bounds, SpriteBatch spriteBatch) {
			spriteBatch.DrawRoundedRetangle(bounds, color);
			bounds.Height -= 1;
			if (doubleClickTime > 0 && --doubleClickTime <= 0) doubleClickItem = 0;
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
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								if (items[i].type != doubleClickItem) doubleClickTime = 0;
								if (doubleClickTime > 0) {
									ItemSourceHelper.Instance.BrowserWindow.FilterItem.SetItem(items[i]);
									ItemSourceBrowser.isItemBrowser = false;
								} else {
									doubleClickTime = 15;
									doubleClickItem = items[i].type;
								}
							}
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
		public override void Reset() {
			items = [];
			scroll = 0;
		}
	}
	public class FilteredEnumerable<T> : IEnumerable<T> {
		ISorter<T> sourceSourceSource;
		List<T> sourceSource;
		List<IFilter<T>> filters = [];
		List<IFilter<T>> searchFilter = [];
		List<(T, int)> cache = [];
		Item filterItem;
		bool inverted;
		bool reachedEnd;
		public bool Inverted {
			get => inverted;
			set {
				if (inverted != value) ClearCache();
				inverted = value;
			}
		}
		public IEnumerator<T> GetEnumerator() {
			if (Inverted) {
				int i = sourceSource.Count - 1;
				for (int j = 0; j < cache.Count; j++) {
					(T source, int index) = cache[j];
					i = index - 1;
					yield return source;
				}
				if (reachedEnd) yield break;
				for (; i > 0; i--) {
					T source = sourceSource[i];
					if (!MatchesSlot(source)) goto cont;
					for (int j = 0; j < searchFilter.Count; j++) if (!searchFilter[j].Matches(source)) goto cont;
					for (int j = 0; j < filters.Count; j++) if (!filters[j].Matches(source)) goto cont;
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
					T source = sourceSource[i];
					if (!MatchesSlot(source)) goto cont;
					for (int j = 0; j < searchFilter.Count; j++) if (!searchFilter[j].Matches(source)) goto cont;
					for (int j = 0; j < filters.Count; j++) if (!filters[j].Matches(source)) goto cont;
					cache.Add((source, i));
					yield return source;
					cont:;
				}
				reachedEnd = true;
			}
		}
		public void SetSearchFilters(IEnumerable<IFilter<T>> filters) {
			if (searchFilter.Count != 0) ClearCache();
			searchFilter = filters.ToList();
			if (cache.Count != 0) cache.RemoveAll(i => filters.Any(f => f.DoesntMatch(i)));
		}
		public void SetSortMethod(ISorter<T> sortMethod) {
			SetBackingList(sortMethod.SortedValues);
			sourceSourceSource = sortMethod;
		}
		public void SetBackingList(List<T> sources) {
			sourceSourceSource = null;
			sourceSource = sources;
			ClearCache();
		}
		public bool IsSelectedSortMethod(ISorter<T> sortMethod) => sourceSourceSource == sortMethod;
		public void FillTooltipAdders(List<ITooltipModifier> list) {
			if (sourceSourceSource is ITooltipModifier modifier) list.Add(modifier);
			list.AddRange(filters.TryCast<ITooltipModifier>());
		}
		#region selected filters
		public void ClearCache() {
			cache.Clear();
			reachedEnd = false;
		}
		public IEnumerable<IFilter<T>> SelectedFilters => filters;
		public int FilterCount => filters.Count;
		public int FindFilterIndex(Predicate<IFilter<T>> match) => filters.FindIndex(match);
		public IFilter<T> GetFilter(int index) => filters[index];
		public void ClearSelectedFilters() {
			filters.Clear();
			ClearCache();
		}
		/// <returns>true if the cache must be cleared and/or some filters may have lost dependents</returns>
		public bool TryAddFilter(IFilter<T> filter, bool orMerge = false) {
			int index = filters.FindIndex(filter.ShouldReplace);
			if (index != -1) {
				if (orMerge) {
					if (filters[index] is OrFilter<T> orFilter) {
						orFilter.Add(filter);
					} else {
						filters[index] = new OrFilter<T>(filters[index], filter);
					}
				} else {
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
				if (filters[index] is OrFilter<T> orFilter) {
					orFilter.Remove(filter);
				} else {
					filters.RemoveAt(index);
				}
			}
			ClearCache();
		}
		public bool IsFilterActive(IFilter<T> filter) {
			int index = filters.FindIndex(filter.ShouldReplace);
			if (index != -1) {
				if (filters[index] is OrFilter<T> orFilter) {
					return orFilter.Contains(filter);
				} else {
					return filters[index] == filter;
				}
			}
			return false;
		}
		public void RemoveOrphans() {
			while (filters.RemoveAll(f => f.ShouldRemove(filters)) > 0) ;
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
			if (value is ItemSource source) {
				if (source.ItemType == filterItem.type) return true;
				foreach (Item ingredient in source.GetSourceItems()) {
					if (ingredient.type == filterItem.type) return true;
				}
				foreach (HashSet<int> group in source.GetSourceGroups()) {
					if (group.Contains(filterItem.type)) return true;
				}
			} else if (value is Item item) {
				if (item.type == filterItem.type) return true;
			}
			return false;
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
	public class SingleSlotGridItem : GridItem {
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
			ItemSourceHelper.Instance.BrowserWindow.ActiveSourceFilters.SetFilterItem(item);
		}
		public override void Reset() {
			item ??= new();
			item.TurnToAir();
		}
	}
	public class SearchGridItem : GridItem {
		public bool focused = false;
		public int cursorIndex = 0;
		public StringBuilder text = new();
		string lastSearch = "";
		public void SetSearch(string search) {
			lastSearch = search;
			ItemSourceBrowser browserWindow = ItemSourceHelper.Instance.BrowserWindow;
			List<SearchFilter> filters = search.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(SearchProviderLoader.Parse).ToList();
			browserWindow.ActiveSourceFilters.SetSearchFilters(filters);
			browserWindow.ActiveItemFilters.SetSearchFilters(filters);
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
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			const float scale = 1.25f;
			bounds.Y += 4;
			bounds.Height -= 4;
			string helpText = "?";
			Vector2 helpSize = font.MeasureString(helpText) * scale;
			Vector2 helpPos = bounds.TopRight() - new Vector2(helpSize.X + 4, 0);
			Color helpColor = Color.Black;
			if (UIMethods.MouseInArea(helpPos, helpSize)) {
				UIMethods.TryMouseText(Language.GetOrRegister($"Mods.{nameof(ItemSourceHelper)}.Search.HelpText").Value);
			} else {
				helpColor *= 0.7f;
			}
			spriteBatch.DrawString(
				font,
				helpText,
				helpPos,
				helpColor,
				0,
				new(0, 0),
				scale,
				0,
			0);
			bounds.Width -= (int)helpSize.X + 8;
			Color color = this.color;
			if (!focused) {
				if (bounds.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface) {
					if (Main.mouseLeft && Main.mouseLeftRelease) {
						focused = true;
						cursorIndex = text.Length;
					}
				} else {
					color *= 0.8f;
				}
			} else {
				if (!bounds.Contains(Main.mouseX, Main.mouseY)) {
					if (Main.mouseLeft && Main.mouseLeftRelease) {
						focused = false;
					}
				}
			}
			spriteBatch.DrawRoundedRetangle(bounds, color);
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
					}
					goto specialControls;
				}
				if (Main.inputText.PressingShift()) {
					if (UIMethods.JustPressed(Keys.Delete)) {
						Copy(cut: true);
						goto specialControls;
					} else if (UIMethods.JustPressed(Keys.Insert)) {
						Paste();
						goto specialControls;
					}
				}
				if (UIMethods.JustPressed(Keys.Left)) {
					if (cursorIndex > 0) cursorIndex--;
				} else if (UIMethods.JustPressed(Keys.Right)) {
					if (cursorIndex < text.Length) cursorIndex++;
				}

				if (input.Length == 0 && cursorIndex > 0) {
					text.Remove(--cursorIndex, 1);
				} else if (input.Length == 2) {
					text.Insert(cursorIndex++, input[1]);
				} else if (UIMethods.JustPressed(Keys.Delete)) {
					if (cursorIndex < text.Length) text.Remove(cursorIndex, 1);
				}
				if (UIMethods.JustPressed(Keys.Enter)) {
					SetSearch(text.ToString());
					focused = false;
				} else if (Main.inputTextEscape) {
					Clear();
					text.Append(lastSearch);
					focused = false;
				}
			}
			specialControls:
			Vector2 offset = new(8, 2);
			if (focused && Main.timeForVisualEffects % 40 < 20) {
				spriteBatch.DrawString(
					font,
					"|",
					bounds.TopLeft() + font.MeasureString(text.ToString()[..cursorIndex]) * Vector2.UnitX * scale + offset * new Vector2(0.5f, 1),
					Color.Black,
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
				Color.Black,
				0,
				new(0, 0),
				scale,
				0,
			0);
		}
		public override void Reset() {
			Clear();
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
		public void SetConditionsFrom(ItemSource itemSource) => SetConditions(itemSource.GetConditions(), itemSource.GetExtraConditionText());
		public void SetConditions(IEnumerable<Condition> conditions, IEnumerable<LocalizedText> conditionts = null) {
			this.conditions = conditions ?? [];
			this.conditionts = conditionts ?? [];
			scroll = 0;
		}
		public int GetHeight() => !conditionts.Any() && !conditions.Any() ? 2 : 20;
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
	public class ItemListGridItem : GridItem, IScrollableUIItem {
		public IEnumerable<Item> items;
		public int scroll;
		bool cutOff = false;
		int lastItemsPerRow = -1;
		int doubleClickTime = 0;
		int doubleClickItem = 0;
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
			if (doubleClickTime > 0 && --doubleClickTime <= 0) doubleClickItem = 0;
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
				Color normalColor = ItemSourceHelperConfig.Instance.ItemSlotColor;
				Color hoverColor = ItemSourceHelperConfig.Instance.HoveredItemSlotColor;
				bool hadAnyItems = items.Any();
				bool displayedAnyItems = false;
				int overscrollFixerTries = 0;
				retry:
				foreach (Item _item in items.Skip(itemsPerRow * scroll)) {
					Item item = _item;
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
						bool hover = canHover && Main.mouseX >= x && Main.mouseX <= x + size && Main.mouseY >= y && Main.mouseY <= y + size;
						UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, texture, hover ? hoverColor : normalColor);
						if (hover) {
							if (items is FilteredEnumerable<Item> filteredEnum) filteredEnum.FillTooltipAdders(TooltipAdderGlobal.TooltipModifiers);
							ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
							if (Main.mouseLeft && Main.mouseLeftRelease) {
								if (item.type != doubleClickItem) doubleClickTime = 0;
								if (doubleClickTime > 0) {
									ItemSourceHelper.Instance.BrowserWindow.FilterItem.SetItem(item);
									ItemSourceBrowser.isItemBrowser = false;
								} else {
									doubleClickTime = 15;
									doubleClickItem = item.type;
								}
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
	public class WindowElement : UIElement {
		#region resize
		public RectangleHandles handles;
		bool heldHandleStretch;
		RectangleHandles heldHandle;
		Vector2 heldHandleOffset;
		public Color color;
		public override void OnInitialize() {
			heights = new float[HeightWeights.Length];
			widths = new float[WidthWeights.Length];
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
			if (heldHandle != 0) {
				Main.LocalPlayer.mouseInterface = true;
				bool changed = false;
				if (heldHandleStretch) {
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
					if (pos.X > Main.screenWidth - Width) pos.X = Main.screenWidth - Width;
					if (pos.Y > Main.screenHeight - Height) pos.X = Main.screenHeight - Height;
					Left = (int)pos.X;
					Top = (int)pos.Y;
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
					if (bounds.Contains(mousePos)) {
						Main.LocalPlayer.mouseInterface = true;
						if (segment.Handles != 0) {
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
		float[] widths;
		float[] heights;
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
		public Color color;
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
		public static bool DoesntMatch<T>(this IFilter<T> self, T source) => !self.Matches(source);
		internal static bool DoesntMatch<T>(this IFilter<T> self, (T source, int index) data) => !self.Matches(data.source);
		public static void DrawRoundedRetangle(this SpriteBatch spriteBatch, Rectangle rectangle, Color color, Texture2D texture = null) {
			texture ??= TextureAssets.InventoryBack13.Value;
			Rectangle textureBounds = texture.Bounds;
			foreach (var segment in rectangleSegments) {
				spriteBatch.Draw(
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
		public static void TryMouseText(string text, string tooltip = null) {
			if (!Main.mouseText) {
				Main.instance.MouseTextHackZoom(text, tooltip);
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
