﻿using Microsoft.Xna.Framework.Graphics;
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
using Microsoft.Xna.Framework;

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
	public override IEnumerable<LocalizedText> GetExtraConditionText() => [Language.GetText(ShopTypeSourceFilter.GetNameKey(Shop))];
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
	public static Dictionary<int, Item> CachedRecipeGroupItems { get; private set; } = [];
	public override void Unload() {
		FakeRecipeConditions = null;
		CachedRecipeGroupItems = null;
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
	public static bool GetRecipeGroup(List<int> groups, Item requiredItem, out Item item) {
		for (int i = 0; i < groups.Count; i++) {
			int group = groups[i];
			if (RecipeGroup.recipeGroups[group].ContainsItem(requiredItem.type)) {
				if (!CachedRecipeGroupItems.TryGetValue(group, out item)) CachedRecipeGroupItems[group] = item = requiredItem.Clone();
				ApplyRecipeGroupName(RecipeGroup.recipeGroups[group].GetText(), item, requiredItem.stack);
				return true;
			}
		}
		item = null;
		return false;
	}
	static void ApplyRecipeGroupName(string text, Item item, int stack) {
		item.stack = stack;
		if (stack > 1) {
			item.SetNameOverride($"{text} ({stack})");
		} else {
			item.SetNameOverride(text);
		}
	}
}
public class CraftingItemSource(ItemSourceType sourceType, Recipe recipe) : ItemSource(sourceType, recipe.createItem) {
	public Recipe Recipe => recipe;
	public override IEnumerable<Condition> GetConditions() => Recipe.Conditions;
	public override IEnumerable<Item> GetSourceItems() {
		for (int i = 0; i < Recipe.requiredItem.Count; i++) {
			if (CraftingItemSourceType.GetRecipeGroup(Recipe.acceptedGroups, Recipe.requiredItem[i], out Item req)) {
				yield return req;
			} else {
				yield return Recipe.requiredItem[i];
			}
		}
	}
	public override IEnumerable<HashSet<int>> GetSourceGroups() => Recipe.acceptedGroups.Select(g => RecipeGroup.recipeGroups[g].ValidItems);
	public override IEnumerable<LocalizedText> GetExtraConditionText() {
		for (int i = 0; i < recipe.requiredTile.Count; i++) {
			int tileType = recipe.requiredTile[i];
			if (tileType == -1) continue;
			yield return Lang._mapLegendCache[MapHelper.TileToLookup(tileType, Recipe.GetRequiredTileStyle(tileType))];
		}
		//if (recipe.needWater) yield return Lang.inter[53];
		//if (recipe.needHoney) yield return Lang.inter[58];
		//if (recipe.needLava) yield return Lang.inter[56];
		//if (recipe.needSnowBiome) yield return Lang.inter[123];
		//if (recipe.needGraveyardBiome) yield return Lang.inter[124];
	}
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
	public override IEnumerable<Type> FilterDependencies => [typeof(WeaponFilter)];
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
	public override IEnumerable<Type> FilterDependencies => [typeof(WeaponFilter)];
}
public class AccessoryFilter : ItemFilter {
	public List<ItemFilter> Children { get; } = [];
	protected override string FilterChannelName => "ItemType";
	public override float SortPriority => 2f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.BandofRegeneration;
	public override bool Matches(Item item) => item.accessory;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public class WingFilter : ItemFilter {
	public override void SetStaticDefaults() {
		ModContent.GetInstance<AccessoryFilter>().Children.Add(this);
	}
	protected override string FilterChannelName => "AccessoryType";
	protected override bool IsChildFilter => true;
	public override float SortPriority => 0f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.SteampunkWings;
	public override bool Matches(Item item) => item.wingSlot != -1;
	public override IEnumerable<Type> FilterDependencies => [typeof(AccessoryFilter)];
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
	public override IEnumerable<Type> FilterDependencies => [typeof(ModdedFilter)];
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
	public override IEnumerable<Type> FilterDependencies => [typeof(ModdedFilter)];
}
public class MaterialFilter : ItemFilter {
	public override float SortPriority => 98f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.Topaz;
	public override bool Matches(Item item) => item.material;
}
public class AmmoFilter : ItemFilter {
	List<ItemFilter> children;
	public override float SortPriority => 2f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.EndlessMusketPouch;
	protected override string FilterChannelName => "Ammo";
	public override bool Matches(Item item) => item.ammo != AmmoID.None;
	public override void PostSetupRecipes() {
		AmmoUseFilter ammoUseFilter = ModContent.GetInstance<AmmoUseFilter>();
		children = [];
		ammoUseFilter.children = [];
		HashSet<int> added = [AmmoID.None];
		Dictionary<int, int> ammoTypes = [];
		Dictionary<int, int> ammoUseTypes = [];
		for (int i = 0; i < ItemLoader.ItemCount; i++) {
			Item item = ContentSamples.ItemsByType[i];
			if (item.ammo != AmmoID.None) {
				ammoTypes.TryGetValue(item.ammo, out int count);
				ammoTypes[item.ammo] = count + 1;
			}
			if (item.useAmmo != AmmoID.None) {
				ammoUseTypes.TryGetValue(item.useAmmo, out int count);
				ammoUseTypes[item.useAmmo] = count + 1;
			}
		}
		foreach (var item in ammoTypes.OrderByDescending(p => p.Value)) {
			if (item.Value > 1) {
				AmmoTypeFilter child = new(item.Key);
				children.Add(child);
				child.LateRegister();
			}
		}
		foreach (var item in ammoUseTypes.OrderByDescending(p => p.Value)) {
			if (item.Value > 1) {
				AmmoUseTypeFilter useChild = new(item.Key);
				ammoUseFilter.children.Add(useChild);
				useChild.LateRegister();
			}
		}
	}
	public override IEnumerable<ItemFilter> ChildItemFilters() => children;
}
[Autoload(false)]
public class AmmoTypeFilter(int type) : ItemFilter {
	public int AmmoType => type;
	public override string Name => $"{base.Name}_{ItemID.Search.GetName(AmmoType)}";
	public override LocalizedText DisplayName {
		get {
			string key = "Mods.ItemSourceHelper.AmmoName." + ItemID.Search.GetName(AmmoType);
			if (Language.Exists(key)) return Language.GetText(key);
			return Lang.GetItemName(AmmoType);
		}
	}
	protected override string FilterChannelName => "AmmoType";
	protected override bool IsChildFilter => true;
	public override void SetStaticDefaults() {
		Main.instance.LoadItem(AmmoType);
		texture = TextureAssets.Item[AmmoType];
	}
	public override bool Matches(Item item) => item.ammo == AmmoType;
	public override IEnumerable<Type> FilterDependencies => [typeof(AmmoFilter)];
}
public class AmmoUseFilter : ItemFilter {
	internal List<ItemFilter> children;
	public override float SortPriority => 1f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.Clentaminator;
	protected override string FilterChannelName => "Ammo";
	public override bool Matches(Item item) => item.useAmmo != AmmoID.None;
	public override IEnumerable<ItemFilter> ChildItemFilters() => children;
}
[Autoload(false)]
public class AmmoUseTypeFilter(int type) : AmmoTypeFilter(type) {
	public override bool Matches(Item item) => item.useAmmo == AmmoType;
	public override IEnumerable<Type> FilterDependencies => [typeof(AmmoUseFilter)];
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
#region sorting methods
public class DefaultItemSorter : ItemSorter, ITooltipModifier {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.TallyCounter];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.TallyCounter);
	public override float SortPriority => 0;
	public override int Compare(Item x, Item y) => BasicComparison(x, y);
	public static int BasicComparison(Item x, Item y) {
		return Comparer<float>.Default.Compare(x.type, y.type);
	}
	public void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
		if (!Main.keyState.PressingShift()) return;
		tooltips.Add(new(ItemSourceHelper.Instance, "ItemID", $"Item ID {item.type}: {ItemID.Search.GetName(item.type)}"));
	}
}
public class ValueItemSorter : ItemSorter, ITooltipModifier {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.GoldCoin];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.GoldCoin);
	public override float SortPriority => 1;
	public override int Compare(Item x, Item y) {
		int valueComp = Comparer<float>.Default.Compare(x.value, y.value);
		if (valueComp != 0) return valueComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
	public void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
		Main.LocalPlayer.GetItemExpectedPrice(item, out long price, out _);
		if (price == 0) {
			byte value = (byte)(120f * (Main.mouseTextColor / 255f));
			tooltips.Add(new(ItemSourceHelper.Instance, "Price", Lang.tip[51].Value) {
				OverrideColor = new Color(value, value, value, Main.mouseTextColor)
			});
		} else {
			string value = "";
			if ((price / 1000000) % 100 > 0) value += $"[i/s{(price / 1000000) % 100}:{ItemID.PlatinumCoin}]";
			if ((price / 10000) % 100 > 0) value += $"[i/s{(price / 10000) % 100}:{ItemID.GoldCoin}]";
			if ((price / 100) % 100 > 0) value += $"[i/s{(price / 100) % 100}:{ItemID.SilverCoin}]";
			if (price % 100 > 0) value += $"[i/s{price % 100}:{ItemID.CopperCoin}]";
			tooltips.Add(new(ItemSourceHelper.Instance, "Price", value));
		}
	}
}
public class RarityItemSorter : ItemSorter {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.MetalDetector];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.MetalDetector);
	public override float SortPriority => 1;
	public override int Compare(Item x, Item y) {
		int rarityComp = Comparer<float>.Default.Compare(x.rare, y.rare);
		if (rarityComp != 0) return rarityComp;
		int valueComp = Comparer<float>.Default.Compare(x.value, y.value);
		if (valueComp != 0) return valueComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
	public void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
		if (ItemRarityID.Search.TryGetName(item.rare, out string rarity)) {
			tooltips.Add(new(ItemSourceHelper.Instance, "Best Pony", rarity));
		}
	}
}
public class DamageSourceSorter : ItemSorter {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.DPSMeter];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.DPSMeter);
	public override float SortPriority => 2;
	public override int Compare(Item x, Item y) {
		int damageComp = Comparer<float>.Default.Compare(x.damage, y.damage);
		if (damageComp != 0) return damageComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
}
#endregion sorting methods