#pragma warning disable CA1822
using Humanizer;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace ItemSourceHelper {
	public class ItemSourceHelperConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ItemSourceHelperConfig Instance;

		[DefaultValue(12), Range(1, 18), Slider]
		public int ScrollSensitivity { get; set; }

		[DefaultValue(26), Range(26, 180)]
		public int TabSize { get; set; }

		[DefaultValue(24), Range(18, 52)]
		public int TabWidth { get; set; }

		[DefaultValue(15), Range(-1, 600)]
		public int AutoSearchTime { get; set; }

		[DefaultValue(true)]
		public bool AnimatedRecipeGroups { get; set; }

		[Header("Colors")]
		[DefaultValue(typeof(Color), "100, 149, 237, 255")]
		public Color SourceBrowserColor { get; set; }
		[DefaultValue(typeof(Color), "240, 128, 128, 255")]
		public Color SourceFilterListColor { get; set; }
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color IngredientListColor { get; set; }
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color SourcesListColor { get; set; }
		[DefaultValue(typeof(Color), "230, 230, 230, 255")]
		public Color SearchBarColor { get; set; }
		[DefaultValue(typeof(Color), "0, 0, 0, 255")]
		public Color SearchBarTextColor { get; set; }
		[DefaultValue(typeof(Color), "72, 67, 159, 255")]
		public Color ItemSlotColor { get; set; }
		[DefaultValue(typeof(Color), "80, 144, 255, 255")]
		public Color HoveredItemSlotColor { get; set; }

		[DefaultValue(typeof(Color), "80, 207, 129, 255")]
		public Color ItemBrowserColor { get; set; }
		[DefaultValue(typeof(Color), "40, 128, 64, 255")]
		public Color ItemFilterListColor { get; set; }
		[DefaultValue(typeof(Color), "40, 160, 100, 255")]
		public Color ItemsListColor { get; set; }

		[DefaultValue(typeof(Color), "207, 129, 80, 255")]
		public Color LootBrowserColor { get; set; }
		[DefaultValue(typeof(Color), "128, 64, 40, 255")]
		public Color LootFilterListColor { get; set; }
		[DefaultValue(typeof(Color), "160, 100, 40, 255")]
		public Color LootListColor { get; set; }
		[DefaultValue(typeof(Color), "255, 144, 80, 255")]
		public Color DropListColor { get; set; }
		[DefaultValue(typeof(Color), "230, 230, 40, 255")]
		public Color FavoriteListCraftableColor { get; set; }
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

		[DefaultValue(700f)]
		public float FavoritesLeft;
		[DefaultValue(100f)]
		public float FavoritesTop;
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