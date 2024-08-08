using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;
using Terraria.UI;
using ItemSourceHelper.Core;
using System;
using Terraria.GameInput;
using Microsoft.Build.Tasks;
using Terraria.ID;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.States;
using System.Reflection;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using ReLogic.Content;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.ItemDropRules;
using ItemSourceHelper.Default;
using Tyfyter.Utils;
using Terraria.GameContent.Bestiary;

namespace ItemSourceHelper;
public class ItemSourceHelper : Mod {
	public static ItemSourceHelper Instance { get; private set; }
	internal List<ItemSource> Sources { get; private set; }
	internal List<LootSource> LootSources { get; private set; }
	public List<ItemSourceType> SourceTypes { get; private set; }
	public List<LootSourceType> LootSourceTypes { get; private set; }
	public List<ItemSourceFilter> Filters { get; private set; }
	public List<LootSourceFilter> LootFilters { get; private set; }
	public int ChildFilterCount { get; internal set; }
	public ItemSourceBrowser BrowserWindow { get; private set; }
	public Dictionary<int, int> IconicWeapons { get; private set; }
	public List<string> PreOrderedFilterChannels { get; private set; }
	public List<SourceSorter> SourceSorters { get; private set; }
	public HashSet<int> CraftableItems { get; private set; }
	public HashSet<int> NPCLootItems { get; private set; }
	public HashSet<int> ItemLootItems { get; private set; }
	public static ModKeybind OpenToItemHotkey { get; private set; }
	public static ModKeybind OpenMenuHotkey { get; private set; }
	public static ModKeybind OpenBestiaryHotkey { get; private set; }
	internal static UISearchBar BestiarySearchBar;
	public static Asset<Texture2D> InventoryBackOutline { get; private set; }
	public static Asset<Texture2D> SortArrows { get; private set; }
	public static Asset<Texture2D> ItemIndicators { get; private set; }
	public ItemSourceHelper() {
		Instance = this;
		Sources = [];
		SourceSorters = [];
		SourceTypes = [];
		Filters = [];
		LootSources = [];
		LootSourceTypes = [];
		LootFilters = [];
		IconicWeapons = new() {
			[DamageClass.Melee.Type] = ItemID.NightsEdge,
			[DamageClass.Ranged.Type] = ItemID.Handgun,
			[DamageClass.Magic.Type] = ItemID.WaterBolt,
			[DamageClass.Summon.Type] = ItemID.SlimeStaff,
		};
		PreOrderedFilterChannels = [
			"SourceType",
			"ItemType",
			"ConsumableType",
		];
		BrowserWindow = new();
		ChildFilterCount = 1;
		CraftableItems = [];
		NPCLootItems = [];
		ItemLootItems = [];
		SearchLoader.RegisterSearchable<ItemSource>(source => new Dictionary<string, string>() {
			["Name"] = source.Item.Name,
			["Description"] = string.Join('\n', source.Item.ToolTip.GetLines()),
			["ModName"] = source.Item?.ModItem?.Mod?.DisplayNameClean ?? "Terraria",
			["ModInternalName"] = source.Item?.ModItem?.Mod?.Name ?? "Terraria",
			["Conditions"] = string.Join('\n', (source.GetConditions() ?? []).Select(c => c.Description.Value).Concat((source.GetExtraConditionText() ?? []).Select(c => c.Value))),
		});
		SearchLoader.RegisterSearchable<Item>(item => new Dictionary<string, string>(){
			["Name"] = item.Name,
			["Description"] = string.Join('\n', item.ToolTip.GetLines()),
			["ModName"] = item?.ModItem?.Mod?.DisplayNameClean ?? "Terraria",
			["ModInternalName"] = item?.ModItem?.Mod?.Name ?? "Terraria",
		});
		SearchLoader.RegisterSearchable<LootSource>(LootSource.GetSearchData);
		FilteredEnumerable<ItemSource>.SlotMatcher = (source, filterItem) => {
			RecipeGroup recipeGroup = null;
			if (filterItem.TryGetGlobalItem(out AnimatedRecipeGroupGlobalItem groupItem) && groupItem.recipeGroup != -1) {
				RecipeGroup.recipeGroups.TryGetValue(groupItem.recipeGroup, out recipeGroup);
				if (recipeGroup is not null && recipeGroup.ValidItems.Count == 1) recipeGroup = null;
			}
			if (recipeGroup is null) {
				if (source.ItemType == filterItem.type) return true;
				foreach (Item ingredient in source.GetSourceItems()) {
					if (ingredient.type == filterItem.type) return true;
				}
			}
			foreach (HashSet<int> group in source.GetSourceGroups()) {
				if (ReferenceEquals(group, recipeGroup?.ValidItems)) return true;
				if (group.Contains(filterItem.type)) return true;
			}
			return false;
		};
		FilteredEnumerable<Item>.SlotMatcher = (item, filterItem) => item.type == filterItem.type;
		FilteredEnumerable<LootSource>.SlotMatcher = (lootSource, filterItem) => {
			if (lootSource.SourceType == ModContent.GetInstance<ItemLootSourceType>() && lootSource.Type == filterItem.type) return true;
			foreach (DropRateInfo info in lootSource.SourceType.GetDrops(lootSource.Type)) {
				if (info.itemId == filterItem.type) return true;
			}
			return false;
		};
		
	}
	public override void Load() {
		OpenToItemHotkey = KeybindLoader.RegisterKeybind(this, "Open Browser To Hovered Item", nameof(Keys.OemOpenBrackets));
		OpenMenuHotkey = KeybindLoader.RegisterKeybind(this, "Open Browser", nameof(Keys.OemCloseBrackets));
		if (ModLoader.HasMod("GlobalLootViewer")) OpenBestiaryHotkey = KeybindLoader.RegisterKeybind(this, "Open Bestiary To Hovered Item", nameof(Keys.OemPipe));
		InventoryBackOutline = Assets.Request<Texture2D>("Inventory_Back_Outline");
		SortArrows = Assets.Request<Texture2D>("Sort_Arrows");
		ItemIndicators = Assets.Request<Texture2D>("Item_Indicators");
		MonoModHooks.Add(
			typeof(ModContent).GetMethod("ResizeArrays", BindingFlags.NonPublic | BindingFlags.Static),
			(Action<bool> orig, bool unloading) => {
				orig(unloading);
				if (!unloading) {
					Data.ResizeArrays();
				}
			}
		);
	}
	public override object Call(params object[] args) {
		string switchOn;
		try {
			switchOn = ((string)args[0]).ToUpper();
		} catch (Exception e) {
			Logger.Error($"Error while trying to process arg 0 {(args[0] is string ? ", provided value must be a string" : "")}:", e);
			throw;
		}
		switch (switchOn) {
			case "ADDICONICWEAPON":
			IconicWeapons.Add((int)args[1], (int)args[2]);
			break;
		}
		return null;
	}
	public override void Unload() {
		Data.Unload();
		Instance = null;
		OpenToItemHotkey = null;
		OpenMenuHotkey = null;
	}
	public static LocalizedText GetLocalization(ILocalizedModType self, string suffix = "DisplayName", Func<string> makeDefaultValue = null) =>
		Language.GetOrRegister(Instance.GetLocalizationKey($"{self.LocalizationCategory}.{self.Name}.{suffix}"), makeDefaultValue);
}
public class FilterOrderComparer : IComparer<ItemSourceFilter> {
	public int Compare(ItemSourceFilter x, ItemSourceFilter y) {
		int xChannel = x.FilterChannel, yChannel = y.FilterChannel;
		if (xChannel == -1) xChannel = 1000;
		if (yChannel == -1) yChannel = 1000;
		int channelComp = Comparer<int>.Default.Compare(xChannel, yChannel);
		if (channelComp != 0) return channelComp;
		return Comparer<float>.Default.Compare(x.SortPriority, y.SortPriority);
	}
}
file class SorterComparer : IComparer<SourceSorter> {
	public int Compare(SourceSorter x, SourceSorter y) => Comparer<float>.Default.Compare(x.SortPriority, y.SortPriority);
}
public class ItemSourceHelperSystem : ModSystem {
	public override void PostSetupRecipes() {
		foreach (ItemSourceType sourceType in ItemSourceHelper.Instance.SourceTypes) sourceType.PostSetupRecipes();

		ItemSourceHelper.Instance.Sources.AddRange(ItemSourceHelper.Instance.SourceTypes.SelectMany(s => s.FillSourceList()));
		ItemSourceHelper.Instance.LootSources.AddRange(ItemSourceHelper.Instance.LootSourceTypes.SelectMany(s => s.FillSourceList()));
		ItemSourceHelper.BestiarySearchBar = (UISearchBar)Main.BestiaryUI.Descendants().First(c => c is UISearchBar);
		ItemSourceHelper.Instance.SourceSorters.Sort(new SorterComparer());
		foreach (SourceSorter sorter in ItemSourceHelper.Instance.SourceSorters) {
			sorter.SortSources();
			if (sorter is ItemSorter itemSorter) itemSorter.SortItems();
			sorter.SetupRequirements();
		}
		for (int i = 0; i < ItemSourceBrowser.Windows.Count; i++) {
			ItemSourceBrowser.Windows[i].SetDefaultSortMethod();
		}
		ItemSourceHelper.Instance.Filters.Sort(new FilterOrderComparer());
		AnimatedRecipeGroupGlobalItem.PostSetupRecipes();
		foreach (ItemSource item in ItemSourceHelper.Instance.Sources) {
			ItemSourceHelper.Instance.CraftableItems.Add(item.ItemType);
		}

		static void DoAddDropGroup(List<IItemDropRule> rules, HashSet<int> dropSet, DropGroup group) {
			foreach (IItemDropRule rule in rules) {
				DropRateInfoChainFeed ratesInfo = new(1f);
				List<DropRateInfo> dropInfoList = [];
				rule.ReportDroprates(dropInfoList, ratesInfo);
				if (dropInfoList.Count > 0) {
					for (int i = 0; i < dropInfoList.Count; i++) {
						dropSet.Add(dropInfoList[i].itemId);
					}
					group.DropInfoList = dropInfoList;
				}
			}
		}
		static T GetFieldValue<T>(string name) {
			return (T)typeof(ItemDropDatabase).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Main.ItemDropsDB);
		}
		foreach (var rules in GetFieldValue<Dictionary<int, List<IItemDropRule>>>("_entriesByNpcNetId")) {
			DoAddDropGroup(rules.Value, ItemSourceHelper.Instance.NPCLootItems, new DropGroup(npc: rules.Key));
		}
		DoAddDropGroup(GetFieldValue<List<IItemDropRule>>("_globalEntries"), ItemSourceHelper.Instance.NPCLootItems, new DropGroup());

		foreach (var rules in GetFieldValue<Dictionary<int, List<IItemDropRule>>>("_entriesByItemId")) {
			DoAddDropGroup(rules.Value, ItemSourceHelper.Instance.ItemLootItems, new DropGroup(item: rules.Key));
		}
		int count = ItemSourceHelper.Instance.Filters.Count;
		for (int i = 0; i < count; i++) ItemSourceHelper.Instance.Filters[i].PostSetupRecipes();
	}
	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
		int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
		if (inventoryIndex != -1) {
			layers.Insert(inventoryIndex + 1, ItemSourceHelper.Instance.BrowserWindow);
		}
	}
	public override void PreSaveAndQuit() {
		ItemSourceHelperPositions.Instance.Save();
	}
}
public class DropGroup(int? npc = null, int? item = null) {
	public readonly int? npc = npc;
	public readonly int? item = item;
	public List<DropRateInfo> DropInfoList { get; set; }
}
public class ScrollingPlayer : ModPlayer {
	internal static IScrollableUIItem scrollable;
	public override void Unload() {
		scrollable = null;
	}
	public override void SetControls() {
		if (scrollable is not null) {
			if (Math.Abs(PlayerInput.ScrollWheelDelta) >= ItemSourceHelperConfig.Instance.ScrollSensitivity * 5) {
				scrollable.Scroll(PlayerInput.ScrollWheelDelta / (ItemSourceHelperConfig.Instance.ScrollSensitivity * -10));
				PlayerInput.ScrollWheelDelta = 0;
			}
			scrollable = null;
		}
		if (ItemSourceHelper.OpenToItemHotkey.JustPressed) {
			if (Main.HoverItem?.IsAir == false) {
				ItemSourceHelper.Instance.BrowserWindow.Open();
				ItemSourceHelper.Instance.BrowserWindow.SetTab<SourceBrowserWindow>(true);
				ModContent.GetInstance<SourceBrowserWindow>().ResetItems();
				ModContent.GetInstance<SourceBrowserWindow>().FilterItem.SetItem(Main.HoverItem);
				return;
			}
		}
		if (ItemSourceHelper.OpenBestiaryHotkey?.JustPressed == true) {
			if (Main.HoverItem?.IsAir == false) {
				Main.player[Main.myPlayer].SetTalkNPC(-1);
				Main.npcChatCornerItem = 0;
				Main.npcChatText = "";
				SoundEngine.PlaySound(SoundID.MenuTick);
				IngameFancyUI.OpenUIState(Main.BestiaryUI);
				Main.BestiaryUI.OnOpenPage();
				ItemSourceHelper.BestiarySearchBar.SetContents("$" + Main.HoverItem.type);
				return;
			}
		}
		if (ItemSourceHelper.OpenMenuHotkey.JustPressed) {
			ItemSourceHelper.Instance.BrowserWindow.Toggle();
		}
	}
}
public class TooltipAdderGlobal : GlobalItem {
	public static List<ITooltipModifier> TooltipModifiers { get; private set; }
	public override void Load() => TooltipModifiers = [];
	public override void Unload() => TooltipModifiers = null;
	public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
		for (int i = 0; i < TooltipModifiers.Count; i++) TooltipModifiers[i].ModifyTooltips(item, tooltips);
		TooltipModifiers.Clear();
	}
}
