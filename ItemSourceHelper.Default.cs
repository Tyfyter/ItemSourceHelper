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
using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;
using Stubble.Core.Imported;
using Terraria.GameContent.Creative;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameContent.Bestiary;
using System.Reflection;
using Terraria.ModLoader.UI;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Elements;
using Terraria.Audio;

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
	public override bool OwnsAllItems(ItemSource itemSource) {
		Main.LocalPlayer.GetItemExpectedPrice(itemSource.Item, out _, out long calcForBuying);
		return Main.LocalPlayer.CanAfford(calcForBuying, itemSource.Item.shopSpecialCurrency);
	}
	public override bool OwnsItem(Item item) => false;
}
public class ShopItemSource : ItemSource {
	public ShopItemSource(ItemSourceType sourceType, AbstractNPCShop.Entry entry, AbstractNPCShop shop) : base(sourceType, entry.Item.type) {
		item = entry.Item;
		Shop = shop;
		this.entry = entry;
	}
	readonly AbstractNPCShop.Entry entry;
	public AbstractNPCShop Shop { get; init; }
	public override IEnumerable<Condition> GetConditions() => entry.Conditions;
	public override IEnumerable<Item> GetSourceItems() {
		if (Item.shopSpecialCurrency != -1 && CustomCurrencyManager.TryGetCurrencySystem(Item.shopSpecialCurrency, out CustomCurrencySystem customCurrency)) {
			if (ShopItemSourceType.EntryPrices.TryGetValue(customCurrency.GetType(), out Func<CustomCurrencySystem, Item, IEnumerable<Item>> func)) {
				foreach (Item item in func(customCurrency, Item)) {
					yield return item;
				}
				yield break;
			}

			Item noIdea = new(ModContent.ItemType<UnloadedItem>(), Item.GetStoreValue());
			noIdea.SetNameOverride(Language.GetOrRegister($"Mods.{nameof(ItemSourceHelper)}.UnknownCurrency").Value);
			yield return noIdea;
			yield break;
		}
		int price = Item.GetStoreValue();
		int levelAmount;
		if (HasCoin(price, 3, out levelAmount)) yield return new Item(ItemID.PlatinumCoin, levelAmount);
		if (HasCoin(price, 2, out levelAmount)) yield return new Item(ItemID.GoldCoin, levelAmount);
		if (HasCoin(price, 1, out levelAmount)) yield return new Item(ItemID.SilverCoin, levelAmount);
		if (HasCoin(price, 0, out levelAmount)) yield return new Item(ItemID.CopperCoin, levelAmount);
	}
	public static bool HasCoin(int price, int level, out int levelAmount, int amountPerLevel = 100) => (levelAmount = (price / (int)Math.Pow(amountPerLevel, level)) % 100) > 0;
	public override IEnumerable<LocalizedText> GetExtraConditionText() => [GetShopName(Shop)];
	static string GetNameKey(AbstractNPCShop shop) => Lang.GetNPCName(shop.NpcType).Key;
	public static LocalizedText GetShopName(AbstractNPCShop shop) => Lang.GetNPCName(shop.NpcType);
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
	public override LocalizedText DisplayName => ShopItemSource.GetShopName(Shop);
	public override bool Matches(ItemSource source) => source is ShopItemSource shopSource && shopSource.Shop.NpcType == Shop.NpcType && shopSource.Shop.Name == Shop.Name;
}
#endregion shop
#region crafting
public class CraftingItemSourceType : ItemSourceType {
	public override string Texture => "Terraria/Images/Item_" + ItemID.WorkBench;
	public static HashSet<Condition> FakeRecipeConditions { get; private set; } = [];
	public static HashSet<int> FakeRecipeTiles { get; private set; } = [];
	public static Dictionary<int, Item> CachedRecipeGroupItems { get; private set; } = [];
	public override void Unload() {
		FakeRecipeConditions = null;
		FakeRecipeTiles = null;
		CachedRecipeGroupItems = null;
	}
	public override IEnumerable<ItemSource> FillSourceList() {
		Dictionary<int, ItemSourceFilter> stations = [];
		for (int i = 0; i < Main.recipe.Length; i++) {
			Recipe recipe = Main.recipe[i];
			if (!recipe.Disabled && !recipe.createItem.IsAir && !recipe.Conditions.Any(FakeRecipeConditions.Contains) && !recipe.Conditions.Any(ShimmerItemSourceType.ShimmerRecipeConditions.Contains) && !recipe.requiredTile.Any(FakeRecipeTiles.Contains)) {
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
				if (item.TryGetGlobalItem(out AnimatedRecipeGroupGlobalItem cloneItem) && requiredItem.TryGetGlobalItem(out AnimatedRecipeGroupGlobalItem cloneReq)) cloneItem.recipeGroup = cloneReq.recipeGroup;
				return true;
			}
		}
		item = null;
		return false;
	}
	static void ApplyRecipeGroupName(string text, Item item, int stack) {
		item.stack = stack;
		item.SetNameOverride(text);
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
			UseItemTexture(itemType);
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
	public static HashSet<Condition> ShimmerRecipeConditions { get; private set; } = [];
	public override void Unload() {
		ShimmerRecipeConditions = null;
	}
	public override IEnumerable<ItemSource> FillSourceList() {
		for (int i = 0; i < ItemID.Sets.ShimmerTransformToItem.Length; i++) {
			int result = ItemID.Sets.ShimmerTransformToItem[i];
			if (result != -1) yield return new ShimmerItemSource(this, result, i);
		}
		for (int i = 0; i < Main.recipe.Length; i++) {
			Recipe recipe = Main.recipe[i];
			if (!recipe.Disabled && !recipe.createItem.IsAir && recipe.Conditions.Any(ShimmerRecipeConditions.Contains)) {
				yield return new CraftingItemSource(this, recipe);
			}
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
#region usable
public class WeaponFilter : ItemFilter {
	List<ItemFilter> children;
	public override float SortPriority => 0f;
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
	public DamageClass DamageClass => damageClass;
	public override void SetStaticDefaults() {
		UseItemTexture(ItemSourceHelper.Instance.IconicWeapons[damageClass.Type]);
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
public class ToolFilter : ItemFilter {
	List<ItemFilter> Children { get; set; } = [];
	public override float SortPriority => 1f;
	public void AddChildFilter(ItemFilter child) {
		Children.Add(child);
		Mod.AddContent(child);
	}
	public override void Load() {
		AddChildFilter(new ToolTypeFilter((i) => i.pick > 0, "Pickaxe", ItemID.CopperPickaxe));
		AddChildFilter(new ToolTypeFilter((i) => i.axe > 0, "Axe", ItemID.CopperAxe));
		AddChildFilter(new ToolTypeFilter((i) => i.hammer > 0, "Hammer", ItemID.CopperHammer));
		AddChildFilter(new ToolTypeFilter((i) => ItemID.Sets.SortingPriorityWiring[i.type] != -1, "Wire", ItemID.Wire));
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.IronPickaxe;
	public override bool Matches(Item item) {
		for (int i = 0; i < Children.Count; i++) {
			if (Children[i].Matches(item)) return true;
		}
		return false;
	}
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
[Autoload(false)]
public class ToolTypeFilter(Predicate<Item> condition, string name, int iconicItem) : ItemFilter {
	public override void SetStaticDefaults() {
		UseItemTexture(iconicItem);
	}
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "ToolType";
	public override string Name => $"{base.Name}_{name}";
	public override bool Matches(Item item) => condition(item);
}
public class FishingFilter : ItemFilter {
	List<ItemFilter> Children { get; set; } = [];
	public override float SortPriority => 5f;
	public void AddChildFilter(ItemFilter child) {
		Children.Add(child);
		Mod.AddContent(child);
	}
	public override void Load() {
		AddChildFilter(new ToolTypeFilter(item => item.fishingPole > 0, "FishingRods", ItemID.GoldenFishingRod));
		AddChildFilter(new ToolTypeFilter(item => item.bait > 0, "FishingBait", ItemID.MasterBait));
		AddChildFilter(new ToolTypeFilter(item => Main.anglerQuestItemNetIDs.Contains(item.type), "QuestFish", ItemID.Slimefish));
		AddChildFilter(new ToolTypeFilter(item => ItemID.Sets.IsFishingCrate[item.type], "FishingCrates", ItemID.WoodenCrate));
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.MechanicsRod;
	public override bool Matches(Item item) {
		for (int i = 0; i < Children.Count; i++) {
			if (Children[i].Matches(item)) return true;
		}
		return false;
	}
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public class PotionFilter : ItemFilter {
	List<ItemFilter> Children { get; set; } = [];
	public override float SortPriority => 5f;
	public void AddChildFilter(ItemFilter child) {
		Children.Add(child);
		Mod.AddContent(child);
	}
	public override void Load() {
		AddChildFilter(new ToolTypeFilter(item => item.healLife > 0, "HealingPotion", ItemID.HealingPotion));
		AddChildFilter(new ToolTypeFilter(item => item.healMana > 0, "ManaPotion", ItemID.ManaPotion));
		AddChildFilter(new ToolTypeFilter(item => item.buffType != 0 && item.buffTime > 0 && !BuffID.Sets.IsAFlaskBuff[item.buffType] && !BuffID.Sets.IsFedState[item.buffType], "BuffPotion", ItemID.IronskinPotion));
		AddChildFilter(new ToolTypeFilter(item => item.buffType != 0 && BuffID.Sets.IsFedState[item.buffType], "FoodPotion", ItemID.MonsterLasagna));
		AddChildFilter(new ToolTypeFilter(item => item.buffType != 0 && BuffID.Sets.IsAFlaskBuff[item.buffType], "FlaskPotion", ItemID.FlaskofFire));
		AddChildFilter(new ToolTypeFilter(item => {
			return item.healLife <= 0 && item.healMana <= 0 && (item.buffType == 0 || item.buffTime <= 0) && ItemID.Search.GetName(item.type).Contains("POTION", StringComparison.InvariantCultureIgnoreCase);
		}, "MiscPotion", ItemID.TeleportationPotion));
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.StrangeBrew;
	public override bool Matches(Item item) {
		for (int i = 0; i < Children.Count; i++) {
			if (Children[i].Matches(item)) return true;
		}
		return false;
	}
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public class LootBagFilter : ItemFilter {
	List<ItemFilter> Children { get; set; } = [];
	public override float SortPriority => 5.1f;
	public void AddChildFilter(ItemFilter child) {
		Children.Add(child);
		Mod.AddContent(child);
	}
	public override void Load() {
		AddChildFilter(new ToolTypeFilter(item => ItemID.Sets.BossBag[item.type], "BossBag", ItemID.MoonLordBossBag));
		AddChildFilter(new ToolTypeFilter(item => ItemID.Sets.IsFishingCrate[item.type] && !ItemID.Sets.IsFishingCrateHardmode[item.type], "PrehardmodeCrate", ItemID.IronCrate));
		AddChildFilter(new ToolTypeFilter(item => ItemID.Sets.IsFishingCrateHardmode[item.type], "HardmodeCrate", ItemID.HallowedFishingCrateHard));
		AddChildFilter(new ToolTypeFilter(item => !ItemID.Sets.BossBag[item.type] && !ItemID.Sets.IsFishingCrate[item.type], "LootBag_Misc", ItemID.GoodieBag));
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.Present;
	public override bool Matches(Item item) => Main.ItemDropsDB.GetRulesForItemID(item.type).Count != 0;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public class BossSpawnFilter : ItemFilter {
	public override float SortPriority => 5.2f;
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.SuspiciousLookingEye;
	public override bool Matches(Item item) {
		if (item.type == ItemID.PirateMap) return true;
		if (item.type is ItemID.TreasureMap or ItemID.LifeCrystal or ItemID.ManaCrystal or ItemID.LifeFruit) return false;
		if (ItemID.Sets.DuplicationMenuToolsFilter[item.type]) return false;
		return ItemID.Sets.SortingPriorityBossSpawns[item.type] != -1;
	}
	public override IEnumerable<ItemFilter> ChildItemFilters() => [];
}
#endregion usable
#region equippable
public class ArmorFilter : ItemFilter {
	public List<ItemFilter> Children { get; } = [];
	protected override string FilterChannelName => "ItemType";
	public override float SortPriority => 2f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.IronChainmail;
	public override bool Matches(Item item) => item.headSlot != -1 || item.bodySlot != -1 || item.legSlot != -1;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public class HelmetFilter : ItemFilter {
	public override void SetStaticDefaults() {
		ModContent.GetInstance<ArmorFilter>().Children.InsertOrdered(this, new FilterOrderComparer());
	}
	protected override string FilterChannelName => "ArmorType";
	protected override bool IsChildFilter => true;
	public override float SortPriority => 0f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.HallowedMask;
	public override bool Matches(Item item) => item.headSlot != -1;
}
public class ChestplateFilter : ItemFilter {
	public override void SetStaticDefaults() {
		ModContent.GetInstance<ArmorFilter>().Children.InsertOrdered(this, new FilterOrderComparer());
	}
	protected override string FilterChannelName => "ArmorType";
	protected override bool IsChildFilter => true;
	public override float SortPriority => 1f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.HallowedPlateMail;
	public override bool Matches(Item item) => item.bodySlot != -1;
}
public class LeggingFilter : ItemFilter {
	public override void SetStaticDefaults() {
		ModContent.GetInstance<ArmorFilter>().Children.InsertOrdered(this, new FilterOrderComparer());
	}
	protected override string FilterChannelName => "ArmorType";
	protected override bool IsChildFilter => true;
	public override float SortPriority => 2f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.HallowedGreaves;
	public override bool Matches(Item item) => item.legSlot != -1;
}
public class AccessoryFilter : ItemFilter {
	public List<ItemFilter> Children { get; } = [];
	protected override string FilterChannelName => "ItemType";
	public override float SortPriority => 2.1f;
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
}
public class PetFilter : ItemFilter {
	List<ItemFilter> Children { get; set; } = [];
	public override float SortPriority => 3.2f;
	public void AddChildFilter(ItemFilter child) {
		Children.Add(child);
		Mod.AddContent(child);
	}
	public override void Load() {
		AddChildFilter(new ToolTypeFilter(item => Main.lightPet[item.buffType], "LightPets", ItemID.WispinaBottle));
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.ExoticEasternChewToy;
	public override bool Matches(Item item) => item.buffType > 0 && (Main.vanityPet[item.buffType] || Main.lightPet[item.buffType]);
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public class MountFilter : ItemFilter {
	List<ItemFilter> Children { get; set; } = [];
	public override float SortPriority => 3.1f;
	public void AddChildFilter(ItemFilter child) {
		Children.Add(child);
		Mod.AddContent(child);
	}
	public override void Load() {
		AddChildFilter(new ToolTypeFilter(item => MountID.Sets.Cart[item.mountType], "Minecarts", ItemID.Minecart));
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.MajesticHorseSaddle;
	public override bool Matches(Item item) => item.mountType != -1;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public class HookFilter : ItemFilter {
	public override float SortPriority => 3f;
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.GrapplingHook;
	public override bool Matches(Item item) => Main.projHook[item.shoot];
	public override IEnumerable<ItemFilter> ChildItemFilters() => [];
}
public class DyeFilter : ItemFilter {
	public List<ItemFilter> Children { get; } = [];
	public override void Load() {
		AddChild(new RealDyeFilter());
		AddChild(new HairDyeFilter());
	}
	protected override string FilterChannelName => "ItemType";
	public override float SortPriority => 2.5f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.GreenDye;
	public override bool Matches(Item item) => item.dye > 0 || item.hairDye > -1;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
	public void AddChild(ItemFilter child) {
		Children.Add(child);
		Mod.AddContent(child);
	}
}
[Autoload(false)]
public class RealDyeFilter : ItemFilter {
	protected override string FilterChannelName => "DyeType";
	protected override bool IsChildFilter => true;
	public override float SortPriority => 0f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.FogboundDye;
	public override bool Matches(Item item) => item.dye > 0;
}
[Autoload(false)]
public class HairDyeFilter : ItemFilter {
	protected override string FilterChannelName => "DyeType";
	protected override bool IsChildFilter => true;
	public override float SortPriority => 1f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.RainbowHairDye;
	public override bool Matches(Item item) => item.hairDye > -1;
}
#endregion equippable
public class TileFilter : ItemFilter {
	public List<ItemFilter> Children { get; } = [];
	protected override string FilterChannelName => "ItemType";
	public override float SortPriority => 4f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.StoneBlock;
	public override void SetStaticDefaults() {
		foreach (TileType type in Enum.GetValues<TileType>()) {
			AddChild(new TileTypeFilter(type));
		}
	}
	void AddChild(ItemFilter child) {
		Children.Add(child);
		child.LateRegister();
	}
	public override bool Matches(Item item) => item.createTile != -1;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
public enum TileType {
	Workbench,
	Anvil,
	Furnace,
	Chest,
	Table,
	Chair,
	Door,
	Platform,
	Light,
	Bed,
	Bookcase,
	Pylon,
}
[Autoload(false)]
public class TileTypeFilter(TileType type) : ItemFilter {
	public override string Name => "TileTypeFilter_" + type;
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "TileType";
	public override void SetStaticDefaults() {
		int itemType = type switch {
			TileType.Workbench => ItemID.WorkBench,
			TileType.Anvil => ItemID.IronAnvil,
			TileType.Furnace => ItemID.Furnace,
			TileType.Chest => ItemID.Chest,
			TileType.Table => ItemID.WoodenTable,
			TileType.Chair => ItemID.WoodenChair,
			TileType.Door => ItemID.WoodenDoor,
			TileType.Platform => ItemID.WoodPlatform,
			TileType.Light => ItemID.Torch,
			TileType.Bed => ItemID.Bed,
			TileType.Bookcase => ItemID.Bookcase,
			TileType.Pylon => ItemID.TeleportationPylonPurity,
			_ => throw new NotImplementedException()
		};
		Main.instance.LoadItem(itemType);
		texture = TextureAssets.Item[itemType];
	}
	public override bool Matches(Item item) {
		bool CheckAdjTiles(int baseTile, params int[] extras) {
			if (item.createTile == baseTile) return true;
			for (int i = 0; i < extras.Length; i++) if (item.createTile == extras[i]) return true;
			ModTile modTile = TileLoader.GetTile(item.createTile);
			if (modTile is null) return false;
			return modTile.AdjTiles.Contains(baseTile);
		}
		return type switch {
			TileType.Workbench => CheckAdjTiles(TileID.WorkBenches),
			TileType.Anvil => CheckAdjTiles(TileID.Anvils),
			TileType.Furnace => CheckAdjTiles(TileID.Furnaces),
			TileType.Chest => Main.tileContainer[item.createTile],
			TileType.Table => Main.tileTable[item.createTile],
			TileType.Chair => TileID.Sets.RoomNeeds.CountsAsChair.Contains(item.createTile),
			TileType.Door => TileID.Sets.OpenDoorID[item.createTile] != -1,
			TileType.Platform => TileID.Sets.Platforms[item.createTile],
			TileType.Light => TileID.Sets.RoomNeeds.CountsAsTorch.Contains(item.createTile),
			TileType.Bed => TileID.Sets.IsValidSpawnPoint[item.createTile],
			TileType.Bookcase => CheckAdjTiles(TileID.Bookcases),
			TileType.Pylon => TileID.Sets.CountsAsPylon.Contains(item.createTile),
			_ => throw new NotImplementedException()
		};
	}
}
public class WallFilter : ItemFilter {
	public List<ItemFilter> Children { get; } = [];
	protected override string FilterChannelName => "ItemType";
	public override float SortPriority => 4.1f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.PlankedWall;
	public override void SetStaticDefaults() {
		AddChild(new WallSafetyFilter(true));
		AddChild(new WallSafetyFilter(false));
	}
	void AddChild(ItemFilter child) {
		Children.Add(child);
		child.LateRegister();
	}
	public override bool Matches(Item item) => item.createWall != -1;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
[Autoload(false)]
public class WallSafetyFilter(bool safe) : ItemFilter {
	public override string Name => "WallFilter_" + (safe ? "Safe" : "Dangerous");
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "WallType";
	public override void SetStaticDefaults() {
		if (safe) {
			Main.instance.LoadItem(ItemID.WoodenFence);
			texture = TextureAssets.Item[ItemID.WoodenFence];
		} else {
			texture = TextureAssets.Extra[258];
		}
	}
	public override bool Matches(Item item) => Main.wallHouse[item.createWall] == safe;
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
		/*child = new ModifiedVanillaFilter();
		children.Add(child);
		child.LateRegister();*/
		children.TrimExcess();
	}
	protected override string FilterChannelName => "Modded";
	protected override int? FilterChannelTargetPlacement => 1101;
	public override float SortPriority => 0f;
	public override bool Matches(Item item) => item.ModItem?.Mod != null;// || item.StatsModifiedBy.Count != 0
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
public class ModifiedVanillaFilter : ItemFilter {
	public override float SortPriority => 1f;
	protected override string FilterChannelName => "Modded";
	protected override int? FilterChannelTargetPlacement => 1101;
	public override void SetStaticDefaults() {
		UseItemTexture(ItemID.Cog);
	}
	public override bool Matches(Item item) => item.ModItem == null && item.StatsModifiedBy.Count != 0;
}
public class MaterialFilter : ItemFilter {
	List<RecipeGroupFilter> children;
	public override void PostSetupRecipes() {
		children = new(RecipeGroup.recipeGroups.Count);
		RecipeGroupFilter child;
		HashSet<HashSet<int>> addedGroups = new(new HashSetComparer<int>());
		foreach (RecipeGroup group in RecipeGroup.recipeGroups.Values) {
			if (group.ValidItems.Count == 1 || !addedGroups.Add(group.ValidItems)) continue;
			child = new RecipeGroupFilter(group);
			children.Add(child);
			child.LateRegister();
		}
		children.Sort((x, y) => Comparer<int>.Default.Compare(x.RecipeGroup.IconicItemId, y.RecipeGroup.IconicItemId));
	}
	public override float SortPriority => 98f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.Topaz;
	public override bool Matches(Item item) => item.material;
	public override IEnumerable<ItemFilter> ChildItemFilters() => children;
	public override bool ShouldHide() => ItemSourceBrowser.isItemBrowser;
}
public class CraftableFilter : ItemFilter {
	public override float SortPriority => 98.1f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.TerraBlade;
	public override bool Matches(Item item) => ItemSourceHelper.Instance.CraftableItems.Contains(item.type);
	public override IEnumerable<ItemFilter> ChildItemFilters() => [];
	public override bool ShouldHide() => ItemSourceBrowser.isItemBrowser;
}
public class NPCLootFilter : ItemFilter {
	public override float SortPriority => 98.2f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.Gel;
	public override bool Matches(Item item) => ItemSourceHelper.Instance.NPCLootItems.Contains(item.type);
	public override IEnumerable<ItemFilter> ChildItemFilters() => [];
	public override bool ShouldHide() => ItemSourceBrowser.isItemBrowser;
}
public class ItemLootFilter : ItemFilter {
	public override float SortPriority => 98.3f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.DogWhistle;
	public override bool Matches(Item item) => ItemSourceHelper.Instance.ItemLootItems.Contains(item.type);
	public override IEnumerable<ItemFilter> ChildItemFilters() => [];
	public override bool ShouldHide() => ItemSourceBrowser.isItemBrowser;
}
[Autoload(false)]
public class RecipeGroupFilter(RecipeGroup recipeGroup) : ItemFilter {
	public RecipeGroup RecipeGroup => recipeGroup;
	public override string Name => $"{base.Name}_{recipeGroup.RegisteredId}";
	protected override bool IsChildFilter => true;
	public override string DisplayNameText => recipeGroup.GetText();
	protected override string FilterChannelName => "RecipeGroup";
	public override Texture2D TextureValue {
		get {
			int itemType;
			if (ItemSourceHelperConfig.Instance.AnimatedRecipeGroups) {
				itemType = recipeGroup.ValidItems.Skip((int)(Main.timeForVisualEffects / 60) % recipeGroup.ValidItems.Count).First();
			} else {
				itemType = recipeGroup.IconicItemId;
			}
			Main.instance.LoadItem(itemType);
			animation = Main.itemAnimations[itemType];
			return TextureAssets.Item[itemType].Value;
		}
	}
	public override bool Matches(Item item) => recipeGroup.ValidItems.Contains(item.type);
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
		UseItemTexture(AmmoType);
	}
	public override bool Matches(Item item) => item.ammo == AmmoType;
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
}
public class RarityParentFilter : ItemFilter {
	List<RarityFilter> Children { get; set; } = [];
	public void AddChildFilter(RarityFilter child) {
		Children.Add(child);
		child.LateRegister();
	}
	public override void PostSetupRecipes() {
		Dictionary<int, string> rarities = new(RarityLoader.RarityCount);
		foreach (string rare in ItemRarityID.Search.Names) {
			rarities.Add(ItemRarityID.Search.GetId(rare), rare);
		}
		for (int i = ItemRarityID.Count; i < RarityLoader.RarityCount; i++) {
			rarities.Add(i, RarityLoader.GetRarity(i).FullName);
		}
		Dictionary<int, RarityFilter> registeredRarities = new(RarityLoader.RarityCount);
		for (int i = 0; i < ItemLoader.ItemCount; i++) {
			if (rarities.Count <= 0 && registeredRarities.Count > 0) break;
			if (i == ItemID.None) continue;
			int rare = ContentSamples.ItemsByType[i].rare;
			if (rarities.TryGetValue(rare, out string name)) {
				RarityFilter newFilter = new(rare, name, i);
				AddChildFilter(newFilter);
				registeredRarities.Add(rare, newFilter);
				rarities.Remove(rare);
			}
			if (ItemSourceHelper.Instance.CraftableItems.Contains(i) && registeredRarities.TryGetValue(rare, out RarityFilter filterForChecking)) {
				filterForChecking.Craftable = true;
				registeredRarities.Remove(rare);
			}
		}
		Children.Sort((x, y) => Comparer<int>.Default.Compare(
			x.Rarity >= 0 ? x.Rarity : int.MaxValue + x.Rarity,
			y.Rarity >= 0 ? y.Rarity : int.MaxValue + y.Rarity
		));
	}
	public override float SortPriority => 97f;
	public override string Texture => "Terraria/Images/Item_" + ItemID.Diamond;
	public override bool Matches(Item item) => true;
	public override IEnumerable<ItemFilter> ChildItemFilters() => Children;
}
[Autoload(false)]
public partial class RarityFilter(int rare, string name, int iconicItem) : ItemFilter {
	public bool Craftable { get; internal set; }
	public int Rarity => rare;
	public override void SetStaticDefaults() {
		UseItemTexture(iconicItem);
	}
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "Rarity";
	public override string Name => $"{base.Name}_{name}";
	public override int DisplayNameRarity => rare;
	public override LocalizedText DisplayName => Mod is null ? ItemSourceHelper.GetLocalization(this, makeDefaultValue: makeDefaultValue) : this.GetLocalization("DisplayName", makeDefaultValue: makeDefaultValue);
	string makeDefaultValue() => Regex.Replace(name.Split("/")[^1].Replace("Rarity", "").Replace("_", ""), "([A-Z])", " $1").Trim();
	public override bool Matches(Item item) => item.rare == rare;
	public override bool ShouldHide() => !Craftable && ItemSourceHelper.Instance.BrowserWindow.IsTabActive<SourceBrowserWindow>();
}
public class ResearchedFilter : ItemFilter {
	public override void SetStaticDefaults() {
		texture = ModContent.Request<Texture2D>("Terraria/Images/UI/WorldCreation/IconDifficultyCreative");
	}
	protected override string FilterChannelName => "Research";
	protected override int? FilterChannelTargetPlacement => 1010;
	public override float SortPriority => 97f;
	public override bool ShouldHide() => Main.LocalPlayer.difficulty != PlayerDifficultyID.Creative;
	public override bool Matches(Item item) => CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.TryGetValue(item.type, out int needed) && Main.LocalPlayerCreativeTracker.ItemSacrifices.GetSacrificeCount(item.type) >= needed;
}
public class ExpertFilter : ItemFilter {
	public override void SetStaticDefaults() {
		texture = ModContent.Request<Texture2D>("Terraria/Images/UI/WorldCreation/IconDifficultyExpert");
	}
	protected override string FilterChannelName => "Difficulty";
	protected override int? FilterChannelTargetPlacement => 1011;
	public override float SortPriority => 98f;
	public override int DisplayNameRarity => ItemRarityID.Expert;
	public override bool Matches(Item item) => item.expert;
}
public class MasterFilter : ItemFilter {
	public override void SetStaticDefaults() {
		texture = ModContent.Request<Texture2D>("Terraria/Images/UI/WorldCreation/IconDifficultyMaster");
	}
	protected override string FilterChannelName => "Difficulty";
	protected override int? FilterChannelTargetPlacement => 1011;
	public override float SortPriority => 99f;
	public override int DisplayNameRarity => ItemRarityID.Master;
	public override bool Matches(Item item) => item.master;
}
#endregion filters
#region search types
public class LiteralSearchFilter(string text) : SearchFilter {
	public override bool Matches(Dictionary<string, string> data) {
		foreach (string item in data.Values) {
			if (item.Contains(text, StringComparison.InvariantCultureIgnoreCase)) return true;
		}
		return false;
	}
}
public class ModNameSearchProvider : SearchProvider {
	public override string Opener => "@";
	public override SearchFilter GetSearchFilter(string filterText) => new ModNameSearchFilter(filterText);
}
public class ModNameSearchFilter(string text) : SearchFilter {
	public override bool Matches(Dictionary<string, string> data) {
		if (data.TryGetValue("ModName", out string name) && name.Contains(text, StringComparison.InvariantCultureIgnoreCase)) return true;
		return data.TryGetValue("ModInternalName", out name) && name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
	}
}
public class ItemNameSearchProvider : SearchProvider {
	public override string Opener => "^";
	public override SearchFilter GetSearchFilter(string filterText) => new ItemNameSearchFilter(filterText);
}
public class ItemNameSearchFilter(string text) : SearchFilter {
	public override bool Matches(Dictionary<string, string> data) {
		return data.TryGetValue("Name", out string name) && name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
	}
}
public class ItemTooltipSearchProvider : SearchProvider {
	public override string Opener => "#";
	public override SearchFilter GetSearchFilter(string filterText) => new ItemTooltipSearchFilter(filterText);
}
public class ItemTooltipSearchFilter(string text) : SearchFilter {
	public override bool Matches(Dictionary<string, string> data) {
		return data.TryGetValue("Description", out string name) && name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
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
public class NameItemSorter : ItemSorter {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.Sign];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.Sign);
	public override float SortPriority => 1;
	public override int Compare(Item x, Item y) {
		int manaComp = string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
		if (manaComp != 0) return manaComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
}
public class ValueItemSorter : ItemSorter, ITooltipModifier {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.GoldCoin];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.GoldCoin);
	public override float SortPriority => 2;
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
			int levelAmount;
			if (ShopItemSource.HasCoin((int)price, 3, out levelAmount)) value += $"[i/s{levelAmount}:{ItemID.PlatinumCoin}]";
			if (ShopItemSource.HasCoin((int)price, 2, out levelAmount)) value += $"[i/s{levelAmount}:{ItemID.GoldCoin}]";
			if (ShopItemSource.HasCoin((int)price, 1, out levelAmount)) value += $"[i/s{levelAmount}:{ItemID.SilverCoin}]";
			if (ShopItemSource.HasCoin((int)price, 0, out levelAmount)) value += $"[i/s{levelAmount}:{ItemID.CopperCoin}]";
			tooltips.Add(new(ItemSourceHelper.Instance, "Price", value));
		}
	}
}
public class RarityItemSorter : ItemSorter, ITooltipModifier {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.MetalDetector];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.MetalDetector);
	public override float SortPriority => 2;
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
public class DamageItemSorter : ItemSorter {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.DPSMeter];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.DPSMeter);
	public override float SortPriority => 9;
	public override int Compare(Item x, Item y) {
		int damageComp = Comparer<float>.Default.Compare(x.damage, y.damage);
		if (damageComp != 0) return damageComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
	public override void SetupRequirements() {
		FilterRequirements = [
			(filter) => filter is WeaponFilter
		];
	}
	public override bool ItemFilter(Item item) => item.damage > 0 && item.useStyle != ItemUseStyleID.None && item.createTile == -1;
}
public class ManaItemSorter : ItemSorter {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.ManaCrystal];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.ManaCrystal);
	public override float SortPriority => 9;
	public override int Compare(Item x, Item y) {
		int manaComp = Comparer<float>.Default.Compare(x.mana, y.mana);
		if (manaComp != 0) return manaComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
	public override void SetupRequirements() {
		FilterRequirements = [
			(filter) => filter is WeaponTypeFilter weaponType && (weaponType.DamageClass == DamageClass.Magic || weaponType.DamageClass == DamageClass.Summon)
		];
	}
}
public class DefenseItemSorter : ItemSorter {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.Shackle];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.Shackle);
	public override float SortPriority => 9;
	public override int Compare(Item x, Item y) {
		int defenseComp = Comparer<float>.Default.Compare(x.defense, y.defense);
		if (defenseComp != 0) return defenseComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
	public override void SetupRequirements() {
		FilterRequirements = [
			(filter) => filter is ArmorFilter
		];
	}
	public override bool ItemFilter(Item item) => item.headSlot != -1 || item.bodySlot != -1 || item.legSlot != -1;
}
public class FlightTimeItemSorter : ItemSorter {
	public override Asset<Texture2D> TextureAsset => TextureAssets.Item[ItemID.WingsStardust];
	public override void SetStaticDefaults() => Main.instance.LoadItem(ItemID.WingsStardust);
	public override float SortPriority => 9;
	public override int Compare(Item x, Item y) {
		int flightTimeComp = Comparer<int>.Default.Compare(ArmorIDs.Wing.Sets.Stats[x.wingSlot].FlyTime, ArmorIDs.Wing.Sets.Stats[y.wingSlot].FlyTime);
		if (flightTimeComp != 0) return flightTimeComp;
		return DefaultItemSorter.BasicComparison(x, y);
	}
	public override bool ItemFilter(Item item) => item.wingSlot != -1;
	public override void SetupRequirements() {
		FilterRequirements = [
			(filter) => filter is WingFilter
		];
	}
}
#endregion sorting methods
#region browsers
#region source
public class SourceBrowserWindow : WindowElement {
	public FilterListGridItem<ItemSource> FilterList { get; private set; }
	public ItemSourceListGridItem SourceList { get; private set; }
	public IngredientListGridItem Ingredience { get; private set; }
	public FilteredEnumerable<ItemSource> ActiveSourceFilters { get; private set; }
	public SingleSlotGridItem FilterItem { get; private set; }
	public ConditionsGridItem ConditionsItem { get; private set; }
	public SearchGridItem SearchItem { get; private set; }
	public override Color BackgroundColor => ItemSourceHelperConfig.Instance.SourceBrowserColor;
	public override void SetDefaults() {
		sortOrder = -1f;
		MarginBottom = 0;
		items = new() {
			[1] = FilterList = new() {
				filters = ItemSourceHelper.Instance.Filters,
				activeFilters = ActiveSourceFilters = new(),
				sorters = ItemSourceHelper.Instance.SourceSorters,
				ResetScroll = () => SourceList.scroll = 0,
				colorFunc = () => ItemSourceHelperConfig.Instance.SourceFilterListColor
			},
			[2] = SourceList = new() {
				things = ActiveSourceFilters,
				colorFunc = () => ItemSourceHelperConfig.Instance.SourcesListColor
			},
			[3] = Ingredience = new() {
				colorFunc = () => ItemSourceHelperConfig.Instance.IngredientListColor
			},
			[4] = FilterItem = new(ActiveSourceFilters),
			[5] = ConditionsItem = new(),
			[6] = SearchItem = new(ActiveSourceFilters)
		};
		itemIDs = new int[3, 7] {
			{ 6, 4, 4,-1, 2, 3, 5 },
			{ 6, 1, 1, 1, 2, 3, 5 },
			{ 6, 1, 1, 1, 2, 3, 5 }
		};
		WidthWeights = new([0f, 3, 3]);
		HeightWeights = new([0f, 0f, 0f, 0f, 3f, 0f, 0f]);
		MinWidths = new([43, 180, 180]);
		MinHeights = new([31, 21, 16, 21, 132, 53, 20]);
		Main.instance.LoadNPC(NPCID.Guide);
		int index = NPC.TypeToDefaultHeadIndex(NPCID.Guide);
		if (index != -1) texture = TextureAssets.NpcHead[index];
	}
	public override void SetDefaultSortMethod() {
		ActiveSourceFilters.SetDefaultSortMethod(ItemSourceHelper.Instance.SourceSorters[0]);
	}
}
public class ItemSourceListGridItem : ThingListGridItem<ItemSource> {
	public override bool ClickThing(ItemSource itemSource, bool doubleClick) {
		if (Main.cursorOverride == CursorOverrideID.FavoriteStar) {
			if (!FavoriteUI.Favorites.Remove(itemSource)) {
				FavoriteUI.Favorites.Add(itemSource);
			}
			SoundEngine.PlaySound(SoundID.MenuTick);
		} else if (!doubleClick) {
			ItemSourceHelper.Instance.BrowserWindow.Ingredience.SetItems(itemSource.GetSourceItems().ToArray());
			ItemSourceHelper.Instance.BrowserWindow.ConditionsItem.SetConditionsFrom(itemSource);
		} else if (Main.mouseRight) {
			ItemSourceHelper.Instance.BrowserWindow.SetTab<ItemBrowserWindow>().ScrollToItem(itemSource.Item.type);
		} else {
			ModContent.GetInstance<SourceBrowserWindow>().FilterItem.SetItem(itemSource.Item);
		}
		return false;
	}
	public override void DrawThing(SpriteBatch spriteBatch, ItemSource itemSource, Vector2 position, bool hovering) {
		Item item = itemSource.Item;
		UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, TextureAssets.InventoryBack13.Value, hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor);
		if (hovering) {
			if (things is FilteredEnumerable<ItemSource> filteredEnum) filteredEnum.FillTooltipAdders(TooltipAdderGlobal.TooltipModifiers);
			ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
		}
		if (hovering && Main.keyState.IsKeyDown(Main.FavoriteKey)) {
			Main.cursorOverride = CursorOverrideID.FavoriteStar;//Main.drawingPlayerChat ? CursorOverrideID.Magnifiers : CursorOverrideID.FavoriteStar;
		}
	}
}
#endregion source
#region item
public class ItemBrowserWindow : WindowElement {
	public SearchGridItem SearchItem { get; private set; }
	public ItemListGridItem ItemList { get; private set; }
	public FilterListGridItem<Item> ItemFilterList { get; private set; }
	public FilteredEnumerable<Item> ActiveItemFilters { get; private set; }
	public override Color BackgroundColor => ItemSourceHelperConfig.Instance.ItemBrowserColor;
	public override void SetDefaults() {
		sortOrder = -0.5f;
		items = new() {
			[1] = ItemFilterList = new() {
				filters = ItemSourceHelper.Instance.Filters.TryCast<ItemFilter>(),
				activeFilters = ActiveItemFilters = new(),
				sorters = ItemSourceHelper.Instance.SourceSorters.TryCast<ItemSorter>(),
				ResetScroll = () => ItemList.scroll = 0,
				colorFunc = () => ItemSourceHelperConfig.Instance.ItemFilterListColor
			},
			[2] = ItemList = new() {
				things = ActiveItemFilters,
				colorFunc = () => ItemSourceHelperConfig.Instance.ItemsListColor
			},
			[4] = new CornerSlotGridItem(ItemFilterList),
			[6] = SearchItem = new(ActiveItemFilters)
		};
		itemIDs = new int[3, 7] {
			{ 6, 4, 4,-1, 2, 2, 2 },
			{ 6, 1, 1, 1, 2, 2, 2 },
			{ 6, 1, 1, 1, 2, 2, 2 }
		};
		WidthWeights = new([0f, 3, 3]);
		HeightWeights = new([0f, 0f, 0f, 0f, 3f, 0f, 0f]);
		MinWidths = new([43, 180, 180]);
		MinHeights = new([31, 21, 16, 21, 132, 53, 2]);
		Main.instance.LoadItem(ItemID.Chest);
		texture = TextureAssets.Item[ItemID.Chest];
	}
	public override void SetDefaultSortMethod() {
		ActiveItemFilters.SetDefaultSortMethod(ItemSourceHelper.Instance.SourceSorters.TryCast<ItemSorter>().First());
	}
	public void ScrollToItem(int type) {
		int size = (int)(52 * Main.inventoryScale);
		const int padding = 2;
		int sizeWithPadding = size + padding;

		int minX = 8;
		int maxX = (int)(widths[0] + widths[1] + widths[2] + padding * 2 - size);
		int itemsPerRow = (int)(((maxX - padding) - minX - 1) / (float)sizeWithPadding) + 1;
		int targetItem = 0;
		bool found = false;
		foreach (Item item in ItemList.things) {
			if (item.type == type) {
				found = true;
				break;
			}
			targetItem++;
		}
		if (found) {
			ItemList.scroll = targetItem / itemsPerRow;
		}
	}
}
public class ItemListGridItem : ThingListGridItem<Item> {
	public override bool ClickThing(Item item, bool doubleClick) {
		if (!doubleClick) return false;
		if (Main.mouseRight) {
			ItemSourceHelper.Instance.BrowserWindow.SetTab<LootBrowserWindow>(true).FilterItem.SetItem(item);
		} else {
			ItemSourceHelper.Instance.BrowserWindow.SetTab<SourceBrowserWindow>(true).FilterItem.SetItem(item);
		}
		return true;
	}
	public override void DrawThing(SpriteBatch spriteBatch, Item item, Vector2 position, bool hovering) {
		int size = (int)(52 * Main.inventoryScale);
		UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, TextureAssets.InventoryBack13.Value, hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor);
		if (hovering) {
			if (things is FilteredEnumerable<Item> filteredEnum) filteredEnum.FillTooltipAdders(TooltipAdderGlobal.TooltipModifiers);
			ItemSlot.MouseHover(ref item, ItemSlot.Context.CraftingMaterial);
		}
		if (ItemSourceHelper.Instance.CraftableItems.Contains(item.type)) {
			spriteBatch.Draw(
				ItemSourceHelper.ItemIndicators.Value,
				position + new Vector2(3, 3),
				new Rectangle(0, 0, 8, 8),
				Color.White
			);
		}
		if (item.material) {
			spriteBatch.Draw(
				ItemSourceHelper.ItemIndicators.Value,
				position + new Vector2(3, size - (8 + 3)),
				new Rectangle(10, 0, 8, 8),
				Color.White
			);
		}
		if (ItemSourceHelper.Instance.NPCLootItems.Contains(item.type)) {
			spriteBatch.Draw(
				ItemSourceHelper.ItemIndicators.Value,
				position + new Vector2(size - (8 + 3), 3),
				new Rectangle(20, 0, 8, 8),
				Color.White
			);
		}
		if (ItemSourceHelper.Instance.ItemLootItems.Contains(item.type)) {
			spriteBatch.Draw(
				ItemSourceHelper.ItemIndicators.Value,
				position + new Vector2(size - (8 + 3), size - (8 + 3)),
				new Rectangle(30, 0, 8, 8),
				Color.White
			);
		}
	}
}
#endregion item
#region loot
public class LootBrowserWindow : WindowElement {
	public SearchGridItem SearchItem { get; private set; }
	public LootListGridItem LootList { get; private set; }
	public DropListGridItem Drops { get; private set; }
	public FilterListGridItem<LootSource> LootFilterList { get; private set; }
	public FilteredEnumerable<LootSource> ActiveLootFilters { get; private set; }
	public SingleSlotGridItem FilterItem { get; private set; }
	public override Color BackgroundColor => ItemSourceHelperConfig.Instance.LootBrowserColor;
	public override void SetDefaults() {
		sortOrder = -0.25f;
		items = new() {
			[1] = LootFilterList = new() {
				filters = ItemSourceHelper.Instance.LootFilters,
				activeFilters = ActiveLootFilters = new(),
				//sorters = ItemSourceHelper.Instance.SourceSorters.TryCast<LootSourceFilter>(),
				ResetScroll = () => LootList.scroll = 0,
				colorFunc = () => ItemSourceHelperConfig.Instance.LootFilterListColor
			},
			[2] = LootList = new() {
				things = ActiveLootFilters,
				colorFunc = () => ItemSourceHelperConfig.Instance.LootListColor
			},
			[3] = Drops = new() {
				colorFunc = () => ItemSourceHelperConfig.Instance.DropListColor
			},
			[4] = FilterItem = new(ActiveLootFilters),
			[6] = SearchItem = new(ActiveLootFilters)
		};
		itemIDs = new int[3, 7] {
			{ 6, 4, 4,-1, 2, 2, 3 },
			{ 6, 1, 1, 1, 2, 2, 3 },
			{ 6, 1, 1, 1, 2, 2, 3 }
		};
		WidthWeights = new([0f, 3, 3]);
		HeightWeights = new([0f, 0f, 0f, 0f, 3f, 0f, 0f]);
		MinWidths = new([43, 180, 180]);
		MinHeights = new([31, 21, 16, 21, 132, 2, 53]);
		Main.instance.LoadItem(ItemID.GoodieBag);
		texture = TextureAssets.Item[ItemID.GoodieBag];
	}
	public override void SetDefaultSortMethod() {
		ActiveLootFilters.SetBackingList(ItemSourceHelper.Instance.LootSources);
	}
}
public class LootListGridItem : ThingListGridItem<LootSource> {
	public override bool ClickThing(LootSource lootSource, bool doubleClick) {
		if (doubleClick && lootSource.SourceType.DoubleClick(lootSource.Type)) return false;
		ModContent.GetInstance<LootBrowserWindow>().Drops.SetDrops(lootSource.SourceType.GetDrops(lootSource.Type));
		return false;
	}
	public override void DrawThing(SpriteBatch spriteBatch, LootSource lootSource, Vector2 position, bool hovering) {
		lootSource.SourceType.DrawSource(spriteBatch, lootSource.Type, position, hovering);
	}
}
public class ItemLootSourceType : LootSourceType {
	public static List<LootSourceFilter> Children { get; private set; } = [];
	public override string Texture => "Terraria/Images/Item_" + ItemID.Present;
	public override void DrawSource(SpriteBatch spriteBatch, int type, Vector2 position, bool hovering) {
		Item item = ContentSamples.ItemsByType[type];
		UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, TextureAssets.InventoryBack13.Value, hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor);
		if (hovering) ItemSlot.MouseHover(ref item, ItemSlot.Context.ChatItem);
	}
	public override IEnumerable<LootSource> FillSourceList() {
		for (int i = 0; i < ItemLoader.ItemCount; i++) {
			if (Main.ItemDropsDB.GetRulesForItemID(i)?.Count > 0) yield return new(this, i);
		}
	}
	public override List<DropRateInfo> GetDrops(int type) {
		List<DropRateInfo> drops = [];
		DropRateInfoChainFeed ratesInfo = new(1f);
		foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForItemID(type)) {
			rule.ReportDroprates(drops, ratesInfo);
		}
		return drops;
	}
	public override Dictionary<string, string> GetSearchData(int type) => SearchLoader.GetSearchData(ContentSamples.ItemsByType[type]);
	public override bool DoubleClick(int type) {
		if (Main.mouseRight) {
			ItemSourceHelper.Instance.BrowserWindow.SetTab<ItemBrowserWindow>(true).ScrollToItem(type);
		} else {
			ItemSourceHelper.Instance.BrowserWindow.SetTab<LootBrowserWindow>(true).FilterItem.SetItem(type);
		}
		return false;
	}
	public override void Unload() {
		Children = null;
	}
	public override IEnumerable<LootSourceFilter> ChildFilters() => Children;
}
public abstract class LootItemTypeFilter(Predicate<LootSource> condition, int iconicItem, float priority) : LootSourceFilter {
	public override void SetStaticDefaults() {
		UseItemTexture(iconicItem);
		ItemLootSourceType.Children.InsertOrdered(this, new LootFilterOrderComparer());
	}
	protected override string FilterChannelName => "LootItemType";
	protected override bool IsChildFilter => true;
	public override float SortPriority => priority;
	public override bool Matches(LootSource lootSource) => condition(lootSource);
}
public class BossBagLootItemTypeFilter() : LootItemTypeFilter(lootSource => ItemID.Sets.BossBag[lootSource.Type], ItemID.MoonLordBossBag, 1f) { }
public class PrehardmodeCrateLootItemTypeFilter() : LootItemTypeFilter(lootSource => ItemID.Sets.IsFishingCrate[lootSource.Type] && !ItemID.Sets.IsFishingCrateHardmode[lootSource.Type], ItemID.IronCrate, 2) { }
public class HardmodeCrateLootItemTypeFilter() : LootItemTypeFilter(lootSource => ItemID.Sets.IsFishingCrateHardmode[lootSource.Type], ItemID.HallowedFishingCrateHard, 3f) { }
public class OtherLootItemTypeFilter() : LootItemTypeFilter(lootSource => !ItemLootSourceType.Children.Any(f => f is not OtherLootItemTypeFilter && f.Matches(lootSource)), ItemID.GoodieBag, 99f) { }
public class NPCLootSourceType : LootSourceType {
	public static List<LootSourceFilter> Children { get; private set; } = [];
	public override string Texture => "Terraria/Images/Item_" + ItemID.Gel;
	RenderTarget2D renderTarget;
	public override IEnumerable<LootSource> FillSourceList() {
		for (int i = NPCID.NegativeIDCount; i < NPCLoader.NPCCount; i++) {
			if (Main.ItemDropsDB.GetRulesForNPCID(i, false)?.Count > 0 && HasValidBestiaryEntry(i)) yield return new(this, i);
		}
	}
	public static bool HasValidBestiaryEntry(int type) {
		BestiaryEntry entry = BestiaryDatabaseNPCsPopulator.FindEntryByNPCID(type);
		if (entry?.Info is null) return false;
		if (NPCID.Sets.NPCBestiaryDrawOffset.TryGetValue(type, out NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers)) {
			if (drawModifiers.Hide) return false;
		}
		return true;
	}
	public override List<DropRateInfo> GetDrops(int type) {
		List<DropRateInfo> drops = [];
		DropRateInfoChainFeed ratesInfo = new(1f);
		foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(type, false)) {
			rule.ReportDroprates(drops, ratesInfo);
		}
		return drops;
	}
	public override void DrawSource(SpriteBatch spriteBatch, int type, Vector2 position, bool hovering) {
		if (renderTarget is not null && (renderTarget.Width != Main.screenWidth || renderTarget.Height != Main.screenHeight)) {
			renderTarget.Dispose();
			renderTarget = null;
		}
		renderTarget ??= new(Main.instance.GraphicsDevice, Main.screenWidth, Main.screenHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

		Item item = ContentSamples.ItemsByType[ItemID.None];
		UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, ItemSourceHelper.NPCDropBack.Value, hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor);
		BestiaryEntry bestiaryEntry = BestiaryDatabaseNPCsPopulator.FindEntryByNPCID(type);
		if (bestiaryEntry?.Icon is not null) {
			int size = (int)(52 * Main.inventoryScale);
			Rectangle rectangle = new((int)position.X, (int)position.Y, size, size);
			Rectangle screenPos = new((Main.screenWidth - size / 2) / 2, (Main.screenHeight - size / 2) / 2, size, size);
			BestiaryUICollectionInfo info = new() {
				OwnerEntry = bestiaryEntry,
				UnlockState = BestiaryEntryUnlockState.CanShowDropsWithDropRates_4
			};
			EntryIconDrawSettings settings = new() {
				iconbox = screenPos,
				IsHovered = hovering,
				IsPortrait = false
			};
			bestiaryEntry.Icon.Update(info, screenPos, settings);
			spriteBatch.End();
			spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
			Main.graphics.GraphicsDevice.SetRenderTarget(renderTarget);
			Main.graphics.GraphicsDevice.Clear(Color.Transparent);//rectangle.Contains(Main.mouseX, Main.mouseY) ? Color.Blue : Color.Red
			bestiaryEntry.Icon.Draw(info, spriteBatch, settings);
			/*spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, Vector2.Zero, Color.White);
			spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, new Vector2(Main.screenWidth - 16, 0), Color.White);
			spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, new Vector2(0, Main.screenHeight - 16), Color.White);
			spriteBatch.Draw(TextureAssets.Item[ItemID.DirtBlock].Value, new Vector2(Main.screenWidth - 16, Main.screenHeight - 16), Color.White);*/
			spriteBatch.End();
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
			RenderTargetUsage renderTargetUsage = Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage;
			Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
			Main.graphics.GraphicsDevice.SetRenderTarget(null);
			Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage = renderTargetUsage;
			//origin.Y -= 8;
			const int shrinkage = 2;
			const int padding = shrinkage + 1;
			rectangle.X += padding;
			rectangle.Y += padding;
			rectangle.Width -= padding * 2;
			rectangle.Height -= padding * 2;
			float alignment = 1;
			float npcScale = 1;
			if (type >= 0 && NPCID.Sets.ShouldBeCountedAsBoss[type] || ContentSamples.NpcsByNetId[type].boss || type == NPCID.DungeonGuardian) alignment = 0.5f;
			screenPos = new((int)((screenPos.X - screenPos.Width * 0.5f)), (int)((screenPos.Y - screenPos.Height * alignment)), (int)(screenPos.Width * 2 * npcScale), (int)(screenPos.Height * 2 * npcScale));
			{
				float pixelScale = (screenPos.Height / (float)rectangle.Height);
				rectangle.X -= shrinkage;
				screenPos.X -= (int)(shrinkage * pixelScale);
				rectangle.Y -= shrinkage;
				screenPos.Y -= (int)(shrinkage * pixelScale);
				rectangle.Width += shrinkage * 2;
				screenPos.Width += (int)(shrinkage * pixelScale * 2);
				rectangle.Height += shrinkage * 2;
				screenPos.Height += (int)(shrinkage * pixelScale * 2);
				spriteBatch.Draw(renderTarget, rectangle, screenPos, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
			}
		}
		if (hovering) UIMethods.TryMouseText(Lang.GetNPCNameValue(type), (ContentSamples.NpcBestiaryRarityStars[type] - 1) * 2);
	}
	public FastFieldInfo<FlavorTextBestiaryInfoElement, string> flavorTextKey = new("_key", BindingFlags.NonPublic);
	public override Dictionary<string, string> GetSearchData(int type) {
		if (!ContentSamples.NpcsByNetId.TryGetValue(type, out NPC npc)) return [];
		Dictionary<string, string> data = new() {
			["Name"] = Lang.GetNPCNameValue(type),
			["ModName"] = npc?.ModNPC?.Mod?.DisplayNameClean ?? "Terraria",
			["ModInternalName"] = npc?.ModNPC?.Mod?.Name ?? "Terraria",
		};

		return data;
	}
	public override void Unload() {
		Children = null;
	}
	public override void PostSetupRecipes() {
		LootSource[] sources = FillSourceList().ToArray();
		for (int i = 0; i < Main.BestiaryDB.Filters.Count; i++) {
			for (int j = 0; j < sources.Length; j++) {
				if (Main.BestiaryDB.Filters[i].FitsFilter(BestiaryDatabaseNPCsPopulator.FindEntryByNPCID(sources[j].Type))) {
					AddChild(new BestiaryFilter(Main.BestiaryDB.Filters[i], i / (Main.BestiaryDB.Filters.Count + 1f)));
					break;
				}
			}
		}
	}
	public static void AddChild(LootSourceFilter child) {
		Children.Add(child);
		child.LateRegister();
	}
	public override IEnumerable<LootSourceFilter> ChildFilters() => Children;
}
public class BestiaryFilter(IEntryFilter<BestiaryEntry> entry, float progress) : LootSourceFilter {
	static FastFieldInfo<UIImageFramed, Asset<Texture2D>> Framed_texture = new("_texture", BindingFlags.NonPublic);
	static FastFieldInfo<UIImageFramed, Rectangle> _frame = new("_frame", BindingFlags.NonPublic);
	static FastFieldInfo<UIImage, Asset<Texture2D>> _texture = new("_texture", BindingFlags.NonPublic);
	public override void SetStaticDefaults() {
		UIElement _image = entry.GetImage();
		if (_image is UIImageFramed framedImage) {
			texture = Framed_texture.GetValue(framedImage);
			if (texture.State == AssetState.Loading) {
				texture.Wait();
				framedImage = (UIImageFramed)entry.GetImage();
			}
			animation = new SingleFrameAnimation(_frame.GetValue(framedImage));
		} else if (_image is UIImage image) {
			texture = _texture.GetValue(image);
			if (texture.State == AssetState.NotLoaded) {
				texture = ModContent.Request<Texture2D>(texture.Name);
			}
		}
	}
	protected override string FilterChannelName => "NPCBestiaryFilter";
	public override string Name => $"{base.Name}_{entry.GetDisplayNameKey()}";
	public override LocalizedText DisplayName => Language.GetOrRegister(entry.GetDisplayNameKey());
	protected override bool IsChildFilter => true;
	public override float SortPriority => progress;
	protected override int? FilterChannelTargetPlacement => 10;
	public override bool Matches(LootSource lootSource) => entry.FitsFilter(Main.BestiaryDB.FindEntryByNPCID(lootSource.Type));
}
/*
public class ChestLootSourceType : LootSourceType {
	public override string Texture => "Terraria/Images/Item_" + ItemID.Chest;
	public List<(string name, List<IItemDropRule> drops)> Entries { get; } = [];
	public override void DrawSource(SpriteBatch spriteBatch, int type, Vector2 position, bool hovering) {
		Item item = ContentSamples.ItemsByType[ItemID.Chest];
		UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, TextureAssets.InventoryBack13.Value, hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor);
		if (hovering) UICommon.TooltipMouseText(Entries[type].name);
	}
	public override IEnumerable<LootSource> FillSourceList() {
		FieldInfo field = typeof(ItemLoader).Assembly.GetType("Terraria.ModLoader.ChestLootLoader")?.GetField("lootPools", BindingFlags.NonPublic | BindingFlags.Static);
		if (field is null) yield break;
		int i = 0;
		foreach (KeyValuePair<string, List<IItemDropRule>> item in (Dictionary<string, List<IItemDropRule>>)field.GetValue(null)) {
			Entries.Add((item.Key, item.Value));
			yield return new(this, i);
			i++;
		}
	}
	public override List<DropRateInfo> GetDrops(int type) {
		List<DropRateInfo> drops = [];
		DropRateInfoChainFeed ratesInfo = new(1f);
		foreach (IItemDropRule rule in Entries[type].drops) {
			rule.ReportDroprates(drops, ratesInfo);
		}
		return drops;
	}
	public override Dictionary<string, string> GetSearchData(int type) => [];
	public override bool DoubleClick(int type) {
		if (Main.mouseRight) {
			//ItemSourceHelper.Instance.BrowserWindow.SetTab<ItemBrowserWindow>(true).ScrollToItem(type);
			return true;
		}
		return false;
	}
}
public class ItemPoolLootSourceType : LootSourceType {
	public override string Texture => "Terraria/Images/Item_" + ItemID.Aglet;
	public List<(string name, List<ItemPoolEntry> drops)> Entries { get; } = [];
	public override void DrawSource(SpriteBatch spriteBatch, int type, Vector2 position, bool hovering) {
		Item item = ContentSamples.ItemsByType[Entries[type].drops[0].Type];
		UIMethods.DrawColoredItemSlot(spriteBatch, ref item, position, TextureAssets.InventoryBack13.Value, hovering ? ItemSourceHelperConfig.Instance.HoveredItemSlotColor : ItemSourceHelperConfig.Instance.ItemSlotColor);
		if (hovering) UICommon.TooltipMouseText(Entries[type].name);
	}
	public override IEnumerable<LootSource> FillSourceList() {
		FieldInfo field = typeof(ItemLoader).Assembly.GetType("Terraria.ModLoader.ChestLootLoader")?.GetField("lootPools", BindingFlags.NonPublic | BindingFlags.Static);
		if (field is null) yield break;
		int i = 0;
		foreach (string name in ChestLootLoader.GetItemPools()) {
			Entries.Add((name, ChestLootLoader.GetItemPool(name)));
			yield return new(this, i);
			i++;
		}
	}
	public override List<DropRateInfo> GetDrops(int type) {
		List<DropRateInfo> drops = [];
		DropRateInfoChainFeed ratesInfo = new(1f);
		new DropFromItemPoolRule(Entries[type].name).ReportDroprates(drops, ratesInfo);
		return drops;
	}
	public override Dictionary<string, string> GetSearchData(int type) => [];
	public override bool DoubleClick(int type) {
		if (Main.mouseRight) {
			//ItemSourceHelper.Instance.BrowserWindow.SetTab<ItemBrowserWindow>(true).ScrollToItem(type);
			return true;
		}
		return false;
	}
}
//*/
public record struct LootSource(LootSourceType SourceType, int Type) {
	public static Dictionary<string, string> GetSearchData(LootSource lootSource) => lootSource.SourceType.GetSearchData(lootSource.Type);
}
#endregion loot
#endregion browsers