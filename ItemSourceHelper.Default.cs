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
using ItemSourceHelper.Core;
using Terraria.GameContent;
using Terraria.Map;
using System.Collections.Immutable;

namespace ItemSourceHelper.Default;
#region shop
public class ShopItemSourceType : ItemSourceType {
	public override string Texture => "Terraria/Images/Item_" + ItemID.GoldCoin;
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
	public override void PostSetupRecipes() {
		childFilters = [];
		foreach (AbstractNPCShop shop in NPCShopDatabase.AllShops) {
			if (!ShopTypes.ContainsKey(shop.GetType())) continue;
			ItemSourceFilter child = new ShopTypeSourceFilter(shop);
			childFilters.Add(child);
			child.LateRegister();
		}
	}
	public override void Unload() {
		EntryPrices = null;
	}
	public override IEnumerable<ItemSource> FillSourceList() {
		return NPCShopDatabase.AllShops.SelectMany<AbstractNPCShop, ItemSource>(shop => {
			if (!ShopTypes.TryGetValue(shop.GetType(), out var func)) {
				ItemSourceHelper.Instance.Logger.Warn($"Shop type {shop.GetType()} is not handled, items from it will not show up");
				return [];
			}
			return func(shop).Select(entry => new ShopItemSource(this, entry, shop));
		});
	}
	List<ItemSourceFilter> childFilters = [];
	public override IEnumerable<ItemSourceFilter> ChildFilters() => childFilters;
}
public class ShopItemSource(ItemSourceType sourceType, AbstractNPCShop.Entry entry, AbstractNPCShop shop) : ItemSource(sourceType, entry.Item.type) {
	public AbstractNPCShop Shop => shop;
	public override IEnumerable<Condition> GetConditions() => entry.Conditions;
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
	public override LocalizedText GetExtraConditionText() => Language.GetText(ShopTypeSourceFilter.GetNameKey(Shop));
}
[Autoload(false)]
public class ShopTypeSourceFilter(AbstractNPCShop shop) : ItemSourceFilter {
	AbstractNPCShop Shop => shop;
	public override void SetStaticDefaults() {
		Main.instance.LoadNPC(Shop.NpcType);
		int index = NPC.TypeToDefaultHeadIndex(Shop.NpcType);
		if (index != -1) texture = TextureAssets.NpcHead[index];
	}
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "ShopType";
	public override string Name => $"{base.Name}_{shop.FullName}";
	public static string GetNameKey(AbstractNPCShop shop) => $"{Lang.GetNPCName(shop.NpcType).Key.Replace(".DisplayName", "")}.ShopName.{shop.Name}";
	public override LocalizedText DisplayName => Language.GetOrRegister(GetNameKey(Shop), () => {
		if (Shop.Name == "Shop") {
			return Lang.GetNPCNameValue(Shop.NpcType);
		}
		return $"{Lang.GetNPCNameValue(Shop.NpcType)}: {Shop.Name}";
	});
	public override bool Matches(ItemSource source) => source is ShopItemSource shopSource && shopSource.Shop.NpcType == Shop.NpcType && shopSource.Shop.Name == Shop.Name;
}
#endregion shop
#region crafting
public class CraftingItemSourceType : ItemSourceType {
	public override string Texture => "Terraria/Images/Item_" + ItemID.WorkBench;
	public static HashSet<Condition> FakeRecipeConditions { get; private set; } = [];
	public override void Unload() {
		FakeRecipeConditions = null;
	}
	public override IEnumerable<ItemSource> FillSourceList() {
		Dictionary<int, ItemSourceFilter> stations = [];
		for (int i = 0; i < Main.recipe.Length; i++) {
			Recipe recipe = Main.recipe[i];
			if (!recipe.Disabled && !recipe.createItem.IsAir && !recipe.Conditions.Any(FakeRecipeConditions.Contains)) {
				for (int j = 0; j < recipe.requiredTile.Count; j++) {
					int tileType = recipe.requiredTile[j];
					if (!stations.ContainsKey(tileType)) {
						ItemSourceFilter child = new CraftingStationSourceFilter(tileType);
						stations.Add(tileType, child);
						child.LateRegister();
					}
				}
				yield return new CraftingItemSource(this, recipe);
			}
		}
		childFilters = stations.ToImmutableSortedDictionary().Values.ToList();
	}
	List<ItemSourceFilter> childFilters = [];
	public override IEnumerable<ItemSourceFilter> ChildFilters() => childFilters;
}
public class CraftingItemSource(ItemSourceType sourceType, Recipe recipe) : ItemSource(sourceType, recipe.createItem.type) {
	public Recipe Recipe => recipe;
	public override IEnumerable<Condition> GetConditions() => Recipe.Conditions;
	public override IEnumerable<Item> GetSourceItems() => Recipe.requiredItem;
}
[Autoload(false)]
public class CraftingStationSourceFilter(int tileType) : ItemSourceFilter {
	string nameKey;
	string internalName;
	string InternalName => internalName ??= TileID.Search.GetName(TileType);
	public int TileType => tileType;
	public override void SetStaticDefaults() {
		int itemType = TileLoader.GetItemDropFromTypeAndStyle(TileType, 0);
		if (Lang._mapLegendCache.FromType(TileType) is not LocalizedText text) {
			if (itemType != 0) {
				text = Lang.GetItemName(itemType);
			} else {
				nameKey = InternalName;
				goto noLocalization;
			}
		}
		nameKey = text.Key;
		noLocalization:
		if (itemType != 0) {
			Main.instance.LoadItem(itemType);
			texture = TextureAssets.Item[itemType];
		}
	}
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "CraftingStaion";
	public override string Name => $"{base.Name}_{InternalName}";
	public override LocalizedText DisplayName => Language.GetOrRegister(nameKey);
	public override bool Matches(ItemSource source) => source is CraftingItemSource craftingSource && craftingSource.Recipe.requiredTile.Contains(TileType);
}
#endregion crafting
#region shimmer
public class ShimmerItemSourceType : ItemSourceType {
	public override string Texture => "Terraria/Images/Item_" + ItemID.ShimmerBlock;
	public override IEnumerable<ItemSource> FillSourceList() {
		for (int i = 0; i < ItemID.Sets.ShimmerTransformToItem.Length; i++) {
			int result = ItemID.Sets.ShimmerTransformToItem[i];
			if (result != -1) yield return new ShimmerItemSource(this, result, i);
		}
	}
}
public class ShimmerItemSource(ItemSourceType sourceType, int resultType, int ingredientType) : ItemSource(sourceType, resultType) {
	public override IEnumerable<Item> GetSourceItems() {
		yield return ContentSamples.ItemsByType[ingredientType];
	}
}
#endregion shimmer
#region filters
public class WeaponFilter : ItemFilter {
	List<ItemFilter> children;
	public override void Load() {
		if (ModLoader.HasMod("ThoriumMod")) {
			ItemSourceHelper.Instance.IconicWeapons[ModContent.Find<DamageClass>("ThoriumMod", "BardDamage").Type] = ModContent.Find<ModItem>("ThoriumMod", "ChronoOcarina").Type;
		}
		children = new(DamageClassLoader.DamageClassCount + 1);
		ItemFilter child;
		for (int i = 0; i < DamageClassLoader.DamageClassCount; i++) {
			if (!ItemSourceHelper.Instance.IconicWeapons.ContainsKey(i)) continue;
			child = new WeaponTypeFilter(DamageClassLoader.GetDamageClass(i));
			children.Add(child);
			Mod.AddContent(child);
		}
		child = new OtherWeaponTypeFilter();
		children.Add(child);
		Mod.AddContent(child);
		children.TrimExcess();
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.IronShortsword;
	public override bool Matches(Item item) => item.damage > 0 && item.useStyle != ItemUseStyleID.None && item.createTile == -1;
	public override IEnumerable<ItemFilter> ChildItemFilters() => children;
}
[Autoload(false)]
public class WeaponTypeFilter(DamageClass damageClass) : ItemFilter {
	public override void SetStaticDefaults() {
		int item = ItemSourceHelper.Instance.IconicWeapons[damageClass.Type];
		Main.instance.LoadItem(item);
		texture = TextureAssets.Item[item];
	}
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "WeaponType";
	public override string Name => $"{base.Name}_{damageClass.FullName}";
	public override string DisplayNameText => damageClass.DisplayName.Value;
	public override bool Matches(Item item) => item.CountsAsClass(damageClass);
}
[Autoload(false)]
public class OtherWeaponTypeFilter : ItemFilter {
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "WeaponType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.UnholyWater;
	public override bool Matches(Item item) {
		foreach (int type in ItemSourceHelper.Instance.IconicWeapons.Keys) {
			if (item.CountsAsClass(DamageClassLoader.GetDamageClass(type))) return false;
		}
		return true;
	}
}
public class AccessoryFilter : ItemFilter {
	protected override string FilterChannelName => "ItemType";
	public override float SortPriority => 2f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.BandofRegeneration;
	public override bool Matches(Item item) => item.accessory;
}
public class ModdedFilter : ItemFilter {
	List<ItemFilter> children;
	public override void SetStaticDefaults() {
		children = new(ModLoader.Mods.Length + 1);
		ItemFilter child;
		foreach (Mod mod in ModLoader.Mods) {
			if (!mod.GetContent<ModItem>().Any()) continue;
			child = new ModFilter(mod);
			children.Add(child);
			child.LateRegister();
		}
		child = new VanillaFilter();
		children.Add(child);
		child.LateRegister();
		children.TrimExcess();
	}
	public override float SortPriority => 99f;
	public override bool Matches(Item item) => item.ModItem?.Mod != null || item.StatsModifiedBy.Count != 0;
	public override IEnumerable<ItemFilter> ChildItemFilters() => children;
}
[Autoload(false)]
public class ModFilter(Mod mod) : ItemFilter {
	public Mod FilterMod => mod;
	public override string Name => $"{base.Name}_{mod.Name}";
	protected override bool IsChildFilter => true;
	public override string DisplayNameText => FilterMod.DisplayName;
	protected override string FilterChannelName => "ModName";
	public override void SetStaticDefaults() {
		if (!ModContent.RequestIfExists($"{FilterMod.Name}/icon_small", out texture)) {
			texture = Asset<Texture2D>.Empty;
		}
	}
	public override bool Matches(Item item) => item.ModItem?.Mod == mod;
}
[Autoload(false)]
public class VanillaFilter : ItemFilter {
	public override float SortPriority => 0f;
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "ModName";
	public override void SetStaticDefaults() {
		texture = ModContent.Request<Texture2D>("Terraria/Images/UI/WorldCreation/IconDifficultyNormal");
	}
	public override bool Matches(Item item) => item.ModItem == null && item.StatsModifiedBy.Count != 0;
}
#endregion filters
#region search types
public class LiteralSearchFilter(string text) : SearchFilter {
	public override bool Matches(Item item) {
		if (item.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase)) return true;
		for (int i = 0; i < item.ToolTip.Lines; i++) {
			if (item.ToolTip.GetLine(i).Contains(text, StringComparison.InvariantCultureIgnoreCase)) return true;
		}
		return false;
	}
}
public class ModNameSearchProvider : SearchProvider {
	public override string Opener => "@";
	public override SearchFilter GetSearchFilter(string filterText) => new ModNameSearchFilter(filterText);
}
public class ModNameSearchFilter(string text) : SearchFilter {
	public override bool Matches(Item item) {
		if (item.ModItem?.Mod is null) return false;
		return item.ModItem.Mod.DisplayNameClean.Contains(text, StringComparison.InvariantCultureIgnoreCase) || item.ModItem.Mod.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
	}
}
public class ItemNameSearchProvider : SearchProvider {
	public override string Opener => "^";
	public override SearchFilter GetSearchFilter(string filterText) => new ItemNameSearchFilter(filterText);
}
public class ItemNameSearchFilter(string text) : SearchFilter {
	public override bool Matches(Item item) {
		return item.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
	}
}
public class ItemTooltipSearchProvider : SearchProvider {
	public override string Opener => "#";
	public override SearchFilter GetSearchFilter(string filterText) => new ItemTooltipSearchFilter(filterText);
}
public class ItemTooltipSearchFilter(string text) : SearchFilter {
	public override bool Matches(Item item) {
		for (int i = 0; i < item.ToolTip.Lines; i++) {
			if (item.ToolTip.GetLine(i).Contains(text, StringComparison.InvariantCultureIgnoreCase)) return true;
		}
		return false;
	}
}
#endregion search types