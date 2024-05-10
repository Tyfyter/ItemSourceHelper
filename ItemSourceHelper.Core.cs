using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ItemSourceHelper.Core;
public abstract class ItemSource(ItemSourceType sourceType, int itemType) {
	public ItemSourceType SourceType => sourceType;
	public int ItemType => itemType;
	public Item item;
	public Item Item => item ??= ContentSamples.ItemsByType[ItemType];
	public virtual IEnumerable<TooltipLine> GetExtraTooltipLines() {
		yield break;
	}
	public virtual IEnumerable<Item> GetSourceItems() {
		yield break;
	}
}
public abstract class ItemSourceType : ModTexturedType, ILocalizedModType {
	public string LocalizationCategory => "ItemSourceType";
	public virtual LocalizedText DisplayName => this.GetLocalization("DisplayName");
	public abstract IEnumerable<ItemSource> FillSourceList();
	public int Type { get; private set; }
	protected override void Register() {
		ModTypeLookup<ItemSourceType>.Register(this);
		Type = ItemSourceHelper.Instance.SourceTypes.Count;
		ItemSourceHelper.Instance.SourceTypes.Add(this);
		Mod.AddContent(new SourceTypeFilter(this));
	}
}
[Autoload(false)]
public class SourceTypeFilter(ItemSourceType sourceType) : ItemSourceFilter {
	public ItemSourceType SourceType => sourceType;
	public override string Name => "SourceTypeFilter_" + SourceType.Name;
	public override string Texture => SourceType.Texture;
	public override bool Matches(ItemSource source) => source.SourceType == SourceType;
}
public abstract class ItemSourceFilter : ModTexturedType, ILocalizedModType {
	public string LocalizationCategory => "ItemSourceFilter";
	public virtual LocalizedText DisplayName => this.GetLocalization("DisplayName");
	public int Type { get; private set; }
	public Asset<Texture2D> TextureAsset { get; protected set; }
	public ItemSourceFilter() {
		if (ModContent.RequestIfExists<Texture2D>(Texture, out var asset)) {
			TextureAsset = asset;
		} else {
			TextureAsset = Asset<Texture2D>.Empty;
		}
	}
	protected override void Register() {
		ModTypeLookup<ItemSourceFilter>.Register(this);
		Type = ItemSourceHelper.Instance.Filters.Count;
		ItemSourceHelper.Instance.Filters.Add(this);
	}
	public abstract bool Matches(ItemSource source);
	public virtual IEnumerable<ItemSourceFilter> ChildFilters() => [];
}
