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
	public virtual IEnumerable<Condition> GetConditions() {
		yield break;
	}
	public virtual LocalizedText GetExtraConditionText() => null;
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
	public sealed override void SetupContent() {
		SetStaticDefaults();
	}
	public virtual void PostSetupRecipes() { }
	public virtual IEnumerable<ItemSourceFilter> ChildFilters() => [];
}
[Autoload(false)]
public class SourceTypeFilter(ItemSourceType sourceType) : ItemSourceFilter {
	public ItemSourceType SourceType => sourceType;
	public override string Name => "SourceTypeFilter_" + SourceType.FullName;
	public override string Texture => SourceType.Texture;
	protected override string FilterChannelName => "SourceType";
	public override IEnumerable<ItemSourceFilter> ChildFilters() => SourceType.ChildFilters();
	public override bool Matches(ItemSource source) => source.SourceType == SourceType;
}
public abstract class ItemSourceFilter : ModTexturedType, ILocalizedModType {
	public string LocalizationCategory => "ItemSourceFilter";
	public virtual LocalizedText DisplayName => this.GetLocalization("DisplayName");
	public virtual string DisplayNameText => DisplayName.Value;
	public int Type { get; private set; }
	public int FilterChannel { get; private set; }
	protected virtual string FilterChannelName => null;
	protected virtual bool IsChildFilter => false;
	protected Asset<Texture2D> texture;
	public virtual Asset<Texture2D> TextureAsset => texture;
	public sealed override void SetupContent() {
		SetStaticDefaults();
	}
	public ItemSourceFilter() {
		if (ModContent.RequestIfExists<Texture2D>(Texture, out var asset)) {
			texture = asset;
		} else {
			texture = Asset<Texture2D>.Empty;
		}
	}
	public void LateRegister() {
		Register();
		SetupContent();
	}
	protected override void Register() {
		ModTypeLookup<ItemSourceFilter>.Register(this);
		if (IsChildFilter) {
			Type = -(++ItemSourceHelper.Instance.ChildFilterCount);
		} else {
			Type = ItemSourceHelper.Instance.Filters.Count;
			ItemSourceHelper.Instance.Filters.Add(this);
		}
		if (FilterChannelName != null) {
			FilterChannel = FilterChannels.GetChannel(FilterChannelName);
		} else {
			FilterChannel = -1;
		}
	}
	public abstract bool Matches(ItemSource source);
	public virtual IEnumerable<ItemSourceFilter> ChildFilters() => [];
	public bool ShouldReplace(ItemSourceFilter other) => FilterChannel == 0 ? other.Type == Type : other.FilterChannel == FilterChannel;
}
public class FilterChannels : ILoadable {
	static List<string> channels = [];
	public static int GetChannel(string name) {
		int index = channels.IndexOf(name);
		if (index != -1) return index + 1;
		channels.Add(name);
		return channels.Count;
	}
	public FilterChannels() {
		channels = [];
	}
	public void Load(Mod mod) { }
	public void Unload() {
		channels = null;
	}

}
