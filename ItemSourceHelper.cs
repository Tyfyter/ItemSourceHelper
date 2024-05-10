using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;
using Terraria.UI;
using ItemSourceHelper.Core;

namespace ItemSourceHelper {
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class ItemSourceHelper : Mod {
		public static ItemSourceHelper Instance { get; private set; }
		public List<ItemSource> Sources { get; private set; }
		public List<ItemSourceType> SourceTypes { get; private set; }
		public List<ItemSourceFilter> Filters { get; private set; }
		public ItemSourceBrowser BrowserWindow { get; private set; }
		public ItemSourceHelper() {
			Instance = this;
			Sources = [];
			SourceTypes = [];
			Filters = [];
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
		}
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
			if (inventoryIndex != -1) {
				layers.Insert(inventoryIndex, ItemSourceHelper.Instance.BrowserWindow);
			}
		}
	}
}
