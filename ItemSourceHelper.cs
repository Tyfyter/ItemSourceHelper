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

namespace ItemSourceHelper {
	public class ItemSourceHelper : Mod {
		public static ItemSourceHelper Instance { get; private set; }
		internal List<ItemSource> Sources { get; private set; }
		public List<ItemSourceType> SourceTypes { get; private set; }
		public List<ItemSourceFilter> Filters { get; private set; }
		public int ChildFilterCount { get; internal set; }
		public ItemSourceBrowser BrowserWindow { get; private set; }
		public Dictionary<int, int> IconicWeapons { get; private set; }
		public List<string> PreOrderedFilterChannels { get; private set; }
		public List<SourceSorter> SourceSorters { get; private set; }
		public static ModKeybind OpenToItemHotkey { get; private set; }
		public static ModKeybind OpenMenuHotkey { get; private set; }
		public static ModKeybind OpenBestiaryHotkey { get; private set; }
		internal static UISearchBar BestiarySearchBar;
		public static Asset<Texture2D> InventoryBackOutline { get; private set; }
		public static Asset<Texture2D> SortArrows { get; private set; }
		public ItemSourceHelper() {
			Instance = this;
			Sources = [];
			SourceSorters = [];
			SourceTypes = [];
			Filters = [];
			IconicWeapons = new() {
				[DamageClass.Melee.Type] = ItemID.NightsEdge,
				[DamageClass.Ranged.Type] = ItemID.Handgun,
				[DamageClass.Magic.Type] = ItemID.WaterBolt,
				[DamageClass.Summon.Type] = ItemID.SlimeStaff,
			};
			PreOrderedFilterChannels = [
				"SourceType",
				"ItemType"
			];
			BrowserWindow = new();
			ChildFilterCount = 1;
		}
		public override void Load() {
			OpenToItemHotkey = KeybindLoader.RegisterKeybind(this, "Open Browser To Hovered Item", nameof(Keys.OemOpenBrackets));
			OpenMenuHotkey = KeybindLoader.RegisterKeybind(this, "Open Browser", nameof(Keys.OemCloseBrackets));
			if (ModLoader.HasMod("GlobalLootViewer")) OpenBestiaryHotkey = KeybindLoader.RegisterKeybind(this, "Open Bestiary To Hovered Item", nameof(Keys.OemPipe));
			InventoryBackOutline = Assets.Request<Texture2D>("Inventory_Back_Outline");
			SortArrows = Assets.Request<Texture2D>("Sort_Arrows");
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
	file class FilterComparer : IComparer<ItemSourceFilter> {
		public int Compare(ItemSourceFilter x, ItemSourceFilter y) {
			int xChannel = x.FilterChannel, yChannel = y.FilterChannel;
			if (xChannel == -1) xChannel = int.MaxValue;
			if (yChannel == -1) yChannel = int.MaxValue;
			int channelComp = Comparer<int>.Default.Compare(xChannel, yChannel);
			if (channelComp != 0) return channelComp;
			return Comparer<float>.Default.Compare(x.SortPriority, y.SortPriority);
		}
	}
	file class SorterComparer : IComparer<SourceSorter> {
		public int Compare(SourceSorter x, SourceSorter y) => Comparer<float>.Default.Compare(x.SortPriority, y.SortPriority);
	}
	public class ItemSourceHelperSystem : ModSystem {
		public static bool isActive = false;
		public override void PostSetupRecipes() {
			foreach (ItemSourceType sourceType in ItemSourceHelper.Instance.SourceTypes) sourceType.PostSetupRecipes();
			int count = ItemSourceHelper.Instance.Filters.Count;
			for (int i = 0; i < count; i++) ItemSourceHelper.Instance.Filters[i].PostSetupRecipes();

			ItemSourceHelper.Instance.Sources.AddRange(ItemSourceHelper.Instance.SourceTypes.SelectMany(s => s.FillSourceList()));
			ItemSourceHelper.Instance.BrowserWindow.Ingredience.items = ItemSourceHelper.Instance.Sources.First().GetSourceItems().ToArray();
			ItemSourceHelper.BestiarySearchBar = (UISearchBar)Main.BestiaryUI.Descendants().First(c => c is UISearchBar);
			ItemSourceHelper.Instance.SourceSorters.Sort(new SorterComparer());
			foreach (SourceSorter sorter in ItemSourceHelper.Instance.SourceSorters) {
				sorter.SortSources();
				if (sorter is ItemSorter itemSorter) itemSorter.SortItems();
			}
			ItemSourceHelper.Instance.BrowserWindow.ActiveSourceFilters.SetSortMethod(ItemSourceHelper.Instance.SourceSorters[0]);
			ItemSourceHelper.Instance.BrowserWindow.ActiveItemFilters.SetSortMethod(ItemSourceHelper.Instance.SourceSorters.TryCast<ItemSorter>().First());
			ItemSourceHelper.Instance.Filters.Sort(new FilterComparer());
		}
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			if (!isActive) {
				ItemSourceHelper.Instance.BrowserWindow.SearchItem.focused = false;
				return;
			}
			int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
			if (inventoryIndex != -1) {
				layers.Insert(inventoryIndex + 1, ItemSourceHelper.Instance.BrowserWindow);
			}
		}
		public override void PreSaveAndQuit() {
			ItemSourceHelperPositions.Instance.Save();
		}
	}
	public class ScrollingPlayer : ModPlayer {
		internal static IScrollableUIItem scrollable;
		public override void Unload() {
			scrollable = null;
		}
		public override void SetControls() {
			if (scrollable is not null) {
				if (Math.Abs(PlayerInput.ScrollWheelDelta) >= 60) {
					scrollable.Scroll(PlayerInput.ScrollWheelDelta / -120);
					PlayerInput.ScrollWheelDelta = 0;
				}
				scrollable = null;
			}
			if (ItemSourceHelper.OpenToItemHotkey.JustPressed) {
				if (Main.HoverItem?.IsAir == false) {
					ItemSourceHelperSystem.isActive = true;
					ItemSourceBrowser.isItemBrowser = false;
					ItemSourceBrowser browserWindow = ItemSourceHelper.Instance.BrowserWindow;
					browserWindow.Reset();
					browserWindow.FilterItem.SetItem(Main.HoverItem);
					return;
				}
			}
			if (ItemSourceHelper.OpenBestiaryHotkey.JustPressed) {
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
				ItemSourceHelperSystem.isActive = !ItemSourceHelperSystem.isActive;
			}
		}
	}
}
