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
using System.Text;
using Terraria.WorldBuilding;

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
	public List<BlockedSource> BlockedSources { get; private set; } = [];
	public List<string> PreOrderedFilterChannels { get; private set; }
	public List<SourceSorter> SourceSorters { get; private set; }
	public HashSet<int> CraftableItems { get; private set; }
	public HashSet<int> NPCLootItems { get; private set; }
	public HashSet<int> ItemLootItems { get; private set; }
	public static ModKeybind OpenToItemHotkey { get; private set; }
	public static ModKeybind OpenDropsToItemHotkey { get; private set; }
	public static ModKeybind OpenMenuHotkey { get; private set; }
	public static ModKeybind OpenBestiaryHotkey { get; private set; }
	internal static UISearchBar BestiarySearchBar;
	public static Asset<Texture2D> InventoryBackOutline { get; private set; }
	public static Asset<Texture2D> SortArrows { get; private set; }
	public static Asset<Texture2D> ItemIndicators { get; private set; }
	public static Asset<Texture2D> NPCDropBack { get; private set; }
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
		SearchLoader.RegisterSearchable<ItemSource>(source => {
			Dictionary<string, string> data = SearchLoader.GetSearchData(source.Item);
			data["Conditions"] = string.Join('\n', source.GetAllConditions().Select(t => t.Value));
			return data;
		});
		SearchLoader.RegisterSearchable<Item>(item => {
			Dictionary<string, string> data = new() {
				["Name"] = item.Name,
				["ModName"] = item?.ModItem?.Mod?.DisplayNameClean ?? "Terraria",
				["ModInternalName"] = item?.ModItem?.Mod?.Name ?? "Terraria",
			};
			int yoyoLogo = -1; int researchLine = -1; float oldKB = item.knockBack; int numLines = 1; string[] toolTipLine = new string[30]; bool[] preFixLine = new bool[30]; bool[] badPreFixLine = new bool[30]; string[] toolTipNames = new string[30];
			Main.MouseText_DrawItemTooltip_GetLinesInfo(item, ref yoyoLogo, ref researchLine, oldKB, ref numLines, toolTipLine, preFixLine, badPreFixLine, toolTipNames, out int prefixlineIndex);

			List<TooltipLine> lines = ItemLoader.ModifyTooltips(item, ref numLines, toolTipNames, ref toolTipLine, ref preFixLine, ref badPreFixLine, ref yoyoLogo, out _, prefixlineIndex);
			StringBuilder builder = new();
			for (int j = 1; j < lines.Count; j++) {
				builder.AppendLine(lines[j].Text);
			}
			data["Description"] = builder.ToString();
			return data;
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
		OpenToItemHotkey = KeybindLoader.RegisterKeybind(this, "Open Source Browser To Hovered Item", nameof(Keys.OemOpenBrackets));
		OpenDropsToItemHotkey = KeybindLoader.RegisterKeybind(this, "Open Drops Browser To Hovered Item", nameof(Keys.OemPipe));
		OpenMenuHotkey = KeybindLoader.RegisterKeybind(this, "Open Browser", nameof(Keys.OemCloseBrackets));
		if (ModLoader.HasMod("GlobalLootViewer")) OpenBestiaryHotkey = KeybindLoader.RegisterKeybind(this, "Open Bestiary To Hovered Item", nameof(Keys.Insert));
		InventoryBackOutline = Assets.Request<Texture2D>("Inventory_Back_Outline");
		SortArrows = Assets.Request<Texture2D>("Sort_Arrows");
		ItemIndicators = Assets.Request<Texture2D>("Item_Indicators");
		NPCDropBack = Assets.Request<Texture2D>("NPC_Drop_Back");
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
			case "ADDBLOCKEDSOURCE":
			BlockedSource blockedSource = BlockedSource.FromDictionary((Dictionary<string, object>)args[1]);
			if (blockedSource.Valid) BlockedSources.Add(blockedSource);
			break;
		}
		return null;
	}
	public override void Unload() {
		Data.Unload();
		Instance = null;
		OpenToItemHotkey = null;
		OpenDropsToItemHotkey = null;
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
public class LootFilterOrderComparer : IComparer<LootSourceFilter> {
	public int Compare(LootSourceFilter x, LootSourceFilter y) {
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
		foreach (LootSourceType sourceType in ItemSourceHelper.Instance.LootSourceTypes) sourceType.PostSetupRecipes();

		ItemSourceHelper.Instance.Sources.AddRange(ItemSourceHelper.Instance.SourceTypes.SelectMany(s => s.FillSourceList().Where(ItemSourceHelper.Instance.BlockedSources.Passes)));
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
				SourceBrowserWindow window = ItemSourceHelper.Instance.BrowserWindow.SetTab<SourceBrowserWindow>(true);
				window.ResetItems();
				window.FilterItem.SetItem(Main.HoverItem);
				return;
			}
		}
		if (ItemSourceHelper.OpenDropsToItemHotkey.JustPressed) {
			if (Main.HoverItem?.IsAir == false) {
				ItemSourceHelper.Instance.BrowserWindow.Open();
				LootBrowserWindow window = ItemSourceHelper.Instance.BrowserWindow.SetTab<LootBrowserWindow>(true);
				window.ResetItems();
				window.FilterItem.SetItem(Main.HoverItem);
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
public record class BlockedSource(int CreateItem, (int type, int? count)[] SourceItems, LocalizedText[] Conditions) {
	public bool Valid { get; private set; } = true;
	public static BlockedSource FromDictionary(Dictionary<string, object> dictionary) {
		bool hasCreateItem = dictionary.TryGetValue("CreateItem", out object _createItem);
		bool hasSourceItems = dictionary.TryGetValue("SourceItems", out object _sourceItems);
		bool hasConditions = dictionary.TryGetValue("Conditions", out object _conditions);
		if (!hasCreateItem) return new(0, null, null) { Valid = false };
		if (_createItem.GetType() == typeof(short)) _createItem = (int)(short)_createItem;
		int createItem = (int)(_createItem ?? 0);
		(int, int?)[] sourceItems = _sourceItems as (int, int?)[];
		LocalizedText[] conditions = _conditions as LocalizedText[];
		if (!hasSourceItems) sourceItems = [];
		if (!hasConditions) conditions = [];
		return new(createItem, sourceItems, conditions);
	}
	public bool Matches(ItemSource itemSource) {
		if (CreateItem != itemSource.Item.type) return false;
		if (SourceItems is not null) {
			Item[] sourceSourceItems = itemSource.GetSourceItems().ToArray();
			if (SourceItems.Length != sourceSourceItems.Length) return false;
			for (int i = 0; i < SourceItems.Length; i++) {
				if (SourceItems[i].type != sourceSourceItems[i].type) return false;
				if (SourceItems[i].count is int count && count != sourceSourceItems[i].stack) return false;
			}
		}
		if (Conditions is not null) {
			LocalizedText[] sourceConditions = itemSource.GetAllConditions().ToArray();
			if (Conditions.Length != sourceConditions.Length) return false;
			for (int i = 0; i < Conditions.Length; i++) {
				if (Conditions[i].Key != sourceConditions[i].Key) return false;
			}
		}
		return true;
	}
}