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
			get => ItemSourceHelper.Instance.BrowserWindow.MainWindow.color;
			set => ItemSourceHelper.Instance.BrowserWindow.MainWindow.color = value;
		}
		[DefaultValue(typeof(Color), "240, 128, 128, 255")]
		public Color FilterListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.FilterList.color;
			set => ItemSourceHelper.Instance.BrowserWindow.FilterList.color = value;
		}
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color IngredientListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.Ingredience.color;
			set => ItemSourceHelper.Instance.BrowserWindow.Ingredience.color = value;
		}
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color SourcesListColor {
			get => ItemSourceHelper.Instance.BrowserWindow.Sources.color;
			set => ItemSourceHelper.Instance.BrowserWindow.Sources.color = value;
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
	}
	public class ItemSourceHelperPositions : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ItemSourceHelperPositions Instance;
		[DefaultValue(200f)]
		public float SourceBrowserLeft {
			get => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Left.Pixels;
			set => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Left.Pixels = value;
		}
		[DefaultValue(200f)]
		public float SourceBrowserTop {
			get => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Top.Pixels;
			set => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Top.Pixels = value;
		}
		[DefaultValue(420f)]
		public float SourceBrowserWidth {
			get => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Width.Pixels;
			set => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Width.Pixels = value;
		}
		[DefaultValue(360f)]
		public float SourceBrowserHeight {
			get => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Height.Pixels;
			set => ItemSourceHelper.Instance.BrowserWindow.MainWindow.Height.Pixels = value;
		}
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
