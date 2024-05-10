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
	public override void Unload() {
		EntryPrices = null;
	}
	public override IEnumerable<ItemSource> FillSourceList() {
		return NPCShopDatabase.AllShops.SelectMany<AbstractNPCShop, ItemSource>(shop => {
			if (!ShopTypes.TryGetValue(shop.GetType(), out var func)) {
				ItemSourceHelper.Instance.Logger.Warn($"Shop type {shop.GetType()} is not handled, items from it will not show up");
				return [];
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
#endregion shop
#region crafting
public class CraftingItemSourceType : ItemSourceType {
	public override string Texture => "Terraria/Images/Item_" + ItemID.WorkBench;
	public static HashSet<Condition> FakeRecipeConditions { get; private set; } = new();
	public override void Load() {

	}
	public override void Unload() {
		FakeRecipeConditions = null;
	}
	public override IEnumerable<ItemSource> FillSourceList() {
        for (int i = 0; i < Main.recipe.Length; i++) {
			Recipe recipe = Main.recipe[i];
			if (!recipe.Disabled && !recipe.Conditions.Any(FakeRecipeConditions.Contains)) yield return new CraftingItemSource(this, recipe);
        }
	}
}
public class CraftingItemSource(ItemSourceType sourceType, Recipe recipe) : ItemSource(sourceType, recipe.createItem.type) {
	public IEnumerable<Condition> Conditions => recipe.Conditions;
	public override IEnumerable<TooltipLine> GetExtraTooltipLines() {
		int i = -1;
		foreach (var condition in Conditions) {
			yield return new TooltipLine(ItemSourceHelper.Instance, $"Condition{++i}", condition.Description.Value);
		}
	}
	public override IEnumerable<Item> GetSourceItems() {
		return recipe.requiredItem;
	}
}
#endregion crafting
#region filters
public class WeaponSourceFilter : ItemSourceFilter {
	List<ItemSourceFilter> children;
	public override void Load() {
		children = new(DamageClassLoader.DamageClassCount);
		ItemSourceFilter child;
		for (int i = 0; i < DamageClassLoader.DamageClassCount; i++) {
			if (!ItemSourceHelper.Instance.IconicWeapons.ContainsKey(i)) continue;
			child = new WeaponTypeSourceFilter(DamageClassLoader.GetDamageClass(i));
			children.Add(child);
			Mod.AddContent(child);
		}
		child = new OtherWeaponTypeSourceFilter();
		children.Add(child);
		Mod.AddContent(child);
	}
	protected override string FilterChannelName => "ItemType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.IronShortsword;
	public override bool Matches(ItemSource source) => source.Item.damage > 0 && source.Item.useStyle != ItemUseStyleID.None;
	public override IEnumerable<ItemSourceFilter> ChildFilters() => children;
}
[Autoload(false)]
public class WeaponTypeSourceFilter(DamageClass damageClass) : ItemSourceFilter {
	public override void SetStaticDefaults() {
		int item = ItemSourceHelper.Instance.IconicWeapons[damageClass.Type];
		Main.instance.LoadItem(item);
		texture = TextureAssets.Item[item];
	}
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "WeaponType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.IronShortsword;
	public override string Name => $"{base.Name}_{damageClass.FullName}";
	public override string DisplayNameText => damageClass.DisplayName.Value;
	public override bool Matches(ItemSource source) => source.item.CountsAsClass(damageClass);
}
[Autoload(false)]
public class OtherWeaponTypeSourceFilter : ItemSourceFilter {
	protected override bool IsChildFilter => true;
	protected override string FilterChannelName => "WeaponType";
	public override string Texture => "Terraria/Images/Item_" + ItemID.UnholyWater;
	public override string Name => $"{base.Name}_Other";
	public override bool Matches(ItemSource source) => !ItemSourceHelper.Instance.IconicWeapons.ContainsKey(source.Item.DamageType.Type);
}
#endregion filters