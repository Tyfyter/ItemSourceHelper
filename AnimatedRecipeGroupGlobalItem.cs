using ItemSourceHelper.Default;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ItemSourceHelper {
	internal class AnimatedRecipeGroupGlobalItem : GlobalItem {
		public override bool InstancePerEntity => true;
		public int recipeGroup = -1;
		public static void PostSetupRecipes() {
			for (int i = 0; i < Main.recipe.Length; i++) {
				Recipe recipe = Main.recipe[i];
				for (int j = 0; j < recipe.acceptedGroups.Count; j++) {
					recipe.requiredItem
					.Find(item => RecipeGroup.recipeGroups[recipe.acceptedGroups[j]]
					.ContainsItem(item.type))
					.GetGlobalItem<AnimatedRecipeGroupGlobalItem>()
					.recipeGroup = recipe.acceptedGroups[j];
				}
			}
		}
		public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
			if (recipeGroup != -1 && ItemSourceHelperConfig.Instance.AnimatedRecipeGroups) {
				RecipeGroup group = RecipeGroup.recipeGroups[recipeGroup];
				int itemType = group.ValidItems.Skip((int)(Main.timeForVisualEffects / 60) % group.ValidItems.Count).First();
				Main.instance.LoadItem(itemType);
				Texture2D texture = TextureAssets.Item[itemType].Value;
				frame = (Main.itemAnimations[itemType] is DrawAnimation animation) ? animation.GetFrame(texture) : texture.Frame();
				origin = frame.Size() * 0.5f;
				Item otherItem = ContentSamples.ItemsByType[itemType];
				spriteBatch.Draw(
					texture,
					position,
					frame,
					otherItem.color == Color.Transparent ? otherItem.GetAlpha(Color.White) : otherItem.GetColor(Color.White),
					0f,
					origin,
					scale,
					SpriteEffects.None,
				0f);
				return false;
			}
			return true;
		}
		public override void OnResearched(Item item, bool fullyResearched) {
			if (fullyResearched) {
				ResearchedFilter filter = ModContent.GetInstance<ResearchedFilter>();
				if (ItemSourceHelper.Instance.BrowserWindow.ActiveItemFilters.IsFilterActive(filter)) {
					ItemSourceHelper.Instance.BrowserWindow.ActiveItemFilters.ClearCache();
				}
				if (ItemSourceHelper.Instance.BrowserWindow.ActiveSourceFilters.IsFilterActive(filter)) {
					ItemSourceHelper.Instance.BrowserWindow.ActiveSourceFilters.ClearCache();
				}
			}
		}
	}
}
