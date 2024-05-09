using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.UI;
using Tyfyter.Utils;

namespace ItemSourceHelper {
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class ItemSourceHelper : Mod {
		public static ItemSourceHelper Instance { get; private set; }
		public List<ItemSource> Sources { get; private set; }
		public List<ItemSourceType> SourceTypes { get; private set; }
		public ItemSourceBrowser BrowserWindow { get; private set; }
		public ItemSourceHelper() {
			Instance = this;
			Sources = [];
			SourceTypes = [];
			BrowserWindow = new();
		}
		public override void Unload() {
			Instance = null;
		}
	}
	public class ItemSourceHelperSystem : ModSystem {
		public override void PostSetupRecipes() {
			ItemSourceHelper.Instance.Sources.AddRange(ItemSourceHelper.Instance.SourceTypes.SelectMany(s => s.FillSourceList()));
			ItemSourceHelper.Instance.BrowserWindow.Ingredience.items = ItemSourceHelper.Instance.Sources.First().GetSourceItems().ToArray();
			ItemSourceHelper.Instance.BrowserWindow.Sources.items = ItemSourceHelper.Instance.Sources;
		}
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
			if (inventoryIndex != -1) {
				layers.Insert(inventoryIndex, ItemSourceHelper.Instance.BrowserWindow);
			}
		}
	}
	public abstract class ItemSource(ItemSourceType sourceType, int itemType) {
		public ItemSourceType SourceType => sourceType;
		public int ItemType => itemType;
		public Item item;
		public Item Item => item ??= ContentSamples.ItemsByType[ItemType];
		public virtual IEnumerable<TooltipLine> GetExtraTooltipLines() {
			yield break;
		}
		public virtual IEnumerable<Item> GetSourceItems() {
			yield break;
		}
	}
	public abstract class ItemSourceType : ModTexturedType, ILocalizedModType {
		public string LocalizationCategory => "ItemSourceType";
		public virtual LocalizedText DisplayName => this.GetLocalization("DisplayName");
		public abstract IEnumerable<ItemSource> FillSourceList();
		public int Type { get; private set; }
		protected override void Register() {
			Type = ItemSourceHelper.Instance.SourceTypes.Count;
			ItemSourceHelper.Instance.SourceTypes.Add(this);
		}
	}
	public class ShopItemSourceType : ItemSourceType {
		public Dictionary<Type, Func<AbstractNPCShop, IEnumerable<AbstractNPCShop.Entry>>> ShopTypes { get; private set; } = new();
		public static Dictionary<Type, Func<CustomCurrencySystem, Item, IEnumerable<Item>>> EntryPrices { get; private set; } = new();
		public override void Load() {
			ShopTypes.Add(typeof(TravellingMerchantShop), shop => ((TravellingMerchantShop)shop).ActiveEntries);
			ShopTypes.Add(typeof(NPCShop), shop => ((NPCShop)shop).Entries);

			FastFieldInfo<CustomCurrencySingleCoin, Dictionary<int, int>> _valuePerUnit = new("_valuePerUnit", System.Reflection.BindingFlags.NonPublic);
			EntryPrices.Add(typeof(CustomCurrencySingleCoin), (c, i) => {
				KeyValuePair<int, int> value = _valuePerUnit.GetValue((CustomCurrencySingleCoin)c).First();
				return [new Item(value.Key, i.shopCustomPrice.Value / value.Value)];
			});
		}
		public override void Unload() {
			EntryPrices = null;
		}
		public override IEnumerable<ItemSource> FillSourceList() {
			return NPCShopDatabase.AllShops.SelectMany<AbstractNPCShop, ItemSource>(shop => {
				if (!ShopTypes.TryGetValue(shop.GetType(), out var func)) {
					ItemSourceHelper.Instance.Logger.Warn($"Shop type {shop.GetType()} is not handled, items from it will not show up");
					return Array.Empty<ItemSource>();
				}
				return func(shop).Select(entry => new ShopItemSource(this, entry));
			});
		}
	}
	public class ShopItemSource(ItemSourceType sourceType, AbstractNPCShop.Entry entry) : ItemSource(sourceType, entry.Item.type) {
		public IEnumerable<Condition> Conditions => entry.Conditions;
		public override IEnumerable<TooltipLine> GetExtraTooltipLines() {
			int i = -1;
			foreach (var condition in Conditions) {
				yield return new TooltipLine(ItemSourceHelper.Instance, $"Condition{++i}", condition.Description.Value);
			}
		}
		public override IEnumerable<Item> GetSourceItems() {
			if (entry.Item.shopSpecialCurrency != -1 && CustomCurrencyManager.TryGetCurrencySystem(entry.Item.shopSpecialCurrency, out CustomCurrencySystem customCurrency)) {
				if (ShopItemSourceType.EntryPrices.TryGetValue(customCurrency.GetType(), out var func)) {
                    foreach (Item item in func(customCurrency, entry.Item)) {
						yield return item;
					}
					yield break;
				}

				Item noIdea = new(ModContent.ItemType<UnloadedItem>(), entry.Item.GetStoreValue());
				noIdea.SetNameOverride(Language.GetOrRegister($"Mods.{nameof(ItemSourceHelper)}.UnknownCurrency").Value);
				yield return noIdea;
				yield break;
			}
			int price = entry.Item.GetStoreValue();
			if ((price / 1000000) % 100 > 0) yield return new Item(ItemID.PlatinumCoin, (price / 1000000) % 100);
			if ((price / 10000) % 100 > 0) yield return new Item(ItemID.GoldCoin, (price / 10000) % 100);
			if ((price / 100) % 100 > 0) yield return new Item(ItemID.SilverCoin, (price / 100) % 100);
			if (price % 100 > 0) yield return new Item(ItemID.CopperCoin, price % 100);
		}
	}
}
