#pragma warning disable CA1822
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Config;

namespace ItemSourceHelper {
	public class ItemSourceHelperConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ItemSourceHelperConfig Instance;
		[DefaultValue(typeof(Color), "100, 149, 237, 255")]
		public Color SourceBrowserColor {
			get => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.color;
			set => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.color = value;
		}
		[DefaultValue(typeof(Color), "240, 128, 128, 255")]
		public Color SourceFilterListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.items[1].color;
			set => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.items[1].color = value;
		}
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color IngredientListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.items[3].color;
			set => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.items[3].color = value;
		}
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color SourcesListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.items[2].color;
			set => ItemSourceHelper.Instance.BrowserWindow.SourceBrowser.items[2].color = value;
		}
		[DefaultValue(typeof(Color), "230, 230, 230, 255")]
		public Color SearchBarColor {
			get => ItemSourceHelper.Instance.BrowserWindow.SearchItem.color;
			set => ItemSourceHelper.Instance.BrowserWindow.SearchItem.color = value;
		}
		[DefaultValue(typeof(Color), "72, 67, 159, 255")]
		public Color ItemSlotColor { get; set; }
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color HoveredItemSlotColor { get; set; }
		[DefaultValue(typeof(Color), "80, 207, 129, 255")]
		public Color ItemBrowserColor {
			get => ItemSourceHelper.Instance.BrowserWindow.ItemBrowser.color;
			set => ItemSourceHelper.Instance.BrowserWindow.ItemBrowser.color = value;
		}
		[DefaultValue(typeof(Color), "40, 128, 64, 255")]
		public Color ItemFilterListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.ItemBrowser.items[1].color;
			set => ItemSourceHelper.Instance.BrowserWindow.ItemBrowser.items[1].color = value;
		}
		[DefaultValue(typeof(Color), "40, 160, 100, 255")]
		public Color ItemsListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.ItemBrowser.items[2].color;
			set => ItemSourceHelper.Instance.BrowserWindow.ItemBrowser.items[2].color = value;
		}
	}
	public class ItemSourceHelperPositions : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ItemSourceHelperPositions Instance;
		[DefaultValue(200f)]
		public float SourceBrowserLeft;
		[DefaultValue(200f)]
		public float SourceBrowserTop;
		[DefaultValue(420f)]
		public float SourceBrowserWidth;
		[DefaultValue(360f)]
		public float SourceBrowserHeight;
		public void Save() {
			Directory.CreateDirectory(ConfigManager.ModConfigPath);
			string filename = Mod.Name + "_" + Name + ".json";
			string path = Path.Combine(ConfigManager.ModConfigPath, filename);
			string json = JsonConvert.SerializeObject(this, ConfigManager.serializerSettings);
			WriteFileNoUnneededRewrites(path, json);
		}
		static void WriteFileNoUnneededRewrites(string file, string text) {
			if (File.Exists(file) && File.ReadAllText(file) == text) return;
			File.WriteAllText(file, text);
		}
	}
}