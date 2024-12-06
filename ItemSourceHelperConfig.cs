#pragma warning disable CA1822
using Humanizer;
using ItemSourceHelper.Core;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.GameContent;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameInput;
using Terraria.Graphics.Effects;
using ReLogic.Content;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

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

		[CustomModConfigItem(typeof(InvertedItemSourceTypesConfigElement)), DefaultValue(typeof(ItemSourceTypesList), "")]
		public ItemSourceTypesList HideCraftableFor { get; set; } = new();

		/*[DefaultValue(true)]
		public bool ShowCraftableIgnoreConditions { get; set; }*/

		[DefaultValue(IndicatorTypes.All)]
		public IndicatorTypes ItemListIndicators { get; set; }
		[DefaultValue(IndicatorTypes.Material), IndicatorTypeMask(~IndicatorTypes.Craftable)]
		public IndicatorTypes SourceListIndicators { get; set; }
		[DefaultValue(~IndicatorTypes.Material), IndicatorTypeMask(~IndicatorTypes.Material)]
		public IndicatorTypes IngredientListIndicators { get; set; }
		[DefaultValue(IndicatorTypes.All)]
		public IndicatorTypes LootListItemIndicators { get; set; }
		[DefaultValue(IndicatorTypes.Material), IndicatorTypeMask(~(IndicatorTypes.NPCDrop | IndicatorTypes.ItemDrop))]
		public IndicatorTypes DropListIndicators { get; set; }

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
		[DefaultValue(typeof(Color), "230, 230, 40, 255")]
		public Color HoveredCraftableColor { get; set; }
	}
	[TypeConverter(typeof(ToFromStringConverter<ItemSourceTypesList>)), JsonConverter(typeof(ItemSourceTypesList.JsonConverter))]
	public class ItemSourceTypesList {
		HashSet<ItemSourceType> Values { get; init; } = [];
		List<string> UnloadedValues { get; init; } = [];
		public void Add(ItemSourceType value) => Values.Add(value);
		public bool Remove(ItemSourceType value) => Values.Remove(value);
		public void Toggle(ItemSourceType value) {
			if (!Remove(value)) Add(value);
		}
		public bool Contains(ItemSourceType value) => Values.Contains(value);
		public ItemSourceTypesList Clone() {
			return new ItemSourceTypesList() {
				Values = Values.ToHashSet(),
				UnloadedValues = UnloadedValues.ToList()
			};
		}
		public override string ToString() => string.Join(",", Values.Select(t => t.FullName).Concat(UnloadedValues));
		public static ItemSourceTypesList FromString(string s) {
			ItemSourceTypesList result = new();
			foreach (string value in s.Split(',')) {
				if (ModContent.TryFind(value, out ItemSourceType type)) {
					result.Values.Add(type);
				} else {
					result.UnloadedValues.Add(value);
				}
			}
			return result;
		}
		public class JsonConverter : Newtonsoft.Json.JsonConverter {
			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
				ItemSourceTypesList result = new();
				reader.Read();
				while (reader.TokenType != JsonToken.EndArray) {
					string value = reader.Value.ToString();
					if (ModContent.TryFind(value, out ItemSourceType type)) {
						result.Values.Add(type);
					} else {
						result.UnloadedValues.Add(value);
					}
					reader.Read();
				}
				return result;
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
				ItemSourceTypesList list = (ItemSourceTypesList)value;
				writer.WriteStartArray();
				foreach (ItemSourceType item in list.Values) {
					writer.WriteValue(item.FullName);
				}
				foreach (string item in list.UnloadedValues) {
					writer.WriteValue(item);
				}
				writer.WriteEndArray();
			}
			public override bool CanConvert(Type objectType) => objectType == typeof(ItemSourceType);
		}
	}
	public class ItemSourceTypesConfigElement : ConfigElement<ItemSourceTypesList> {
		protected bool inverted = false;
		public override void OnBind() {
			base.OnBind();
			base.TextDisplayFunction = TextDisplayOverride ?? base.TextDisplayFunction;
			SetupList();
			Value ??= new();
		}
		public Func<string> TextDisplayOverride { get; set; }
		protected void SetupList() {
			RemoveAllChildren();
			Main.UIScaleMatrix.Decompose(out Vector3 scale, out _, out _);
			float left = MathF.Max(FontAssets.ItemStack.Value.MeasureString(TextDisplayFunction()).X * scale.X - 8, 4);
			float top = 4;
			Recalculate();
			float width = GetDimensions().Width;
			for (int i = 0; i < ItemSourceHelper.Instance.SourceTypes.Count; i++) {
				if (left + 26 + 4 > width) {
					left = 0;
					top += 30;
					Height.Pixels += 30;
				}
				Append(new ItemSourceTypeElement(this, ItemSourceHelper.Instance.SourceTypes[i], inverted) {
					Left = new StyleDimension(left, 0),
					Top = new StyleDimension(top, 0)
				});
				left += 26 + 4;
			}
			Recalculate();
		}
		public void Toggle(ItemSourceType itemSource) {
			ItemSourceTypesList value = Value.Clone();
			value.Toggle(itemSource);
			Value = value;
		}
		public bool Contains(ItemSourceType value) => Value.Contains(value);
	}
	public class InvertedItemSourceTypesConfigElement : ItemSourceTypesConfigElement {
		public InvertedItemSourceTypesConfigElement() {
			inverted = true;
		}
	}
	public class ItemSourceTypeElement(ItemSourceTypesConfigElement value, ItemSourceType sourceType, bool inverted) : UIElement {
		readonly Asset<Texture2D> texture = ModContent.Request<Texture2D>(sourceType.Texture);
		readonly Texture2D actuator = TextureAssets.Actuator.Value;
		public override void OnInitialize() {
			Width.Set(24, 0);
			Height.Set(24, 0);
		}
		public override void Draw(SpriteBatch spriteBatch) {
			Rectangle button = this.GetDimensions().ToRectangle();
			Color color = new(0, 0, 0, 50);
			Color hiColor = new(50, 50, 50, 0);
			if (this.IsMouseHovering) {
				UIMethods.DrawRoundedRetangle(spriteBatch, button, hiColor);
			} else {
				UIMethods.DrawRoundedRetangle(spriteBatch, button, color);
			}
			Rectangle iconPos = texture.Size().RectWithinCentered(button, 8);
			spriteBatch.Draw(texture.Value, iconPos, null, Color.White);
			if (value.Contains(sourceType)) {
				Rectangle corner = button;
				int halfWidth = corner.Width / 2;
				corner.X += halfWidth;
				corner.Width -= halfWidth;
				corner.Height /= 2;
				spriteBatch.Draw(actuator, corner, inverted ? Color.Red : Color.Green);
			}
		}
		public override void LeftClick(UIMouseEvent evt) {
			value.Toggle(sourceType);
		}
	}
	public class ItemSourceHelperPositions : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ItemSourceHelperPositions Instance;

		public readonly bool ChangeIngameNotice = true;

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