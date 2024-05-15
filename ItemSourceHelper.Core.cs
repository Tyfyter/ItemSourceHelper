using ItemSourceHelper.Default;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ItemSourceHelper.Core;
public abstract class ItemSource(ItemSourceType sourceType, int itemType) {
	public ItemSourceType SourceType => sourceType;
	public int ItemType => itemType;
	Item item;
	public Item Item => item ??= ContentSamples.ItemsByType[ItemType];
	public virtual IEnumerable<Condition> GetConditions() {
		yield break;
	}
	public virtual IEnumerable<LocalizedText> GetExtraConditionText() => null;
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
		_ = DisplayName.Value;
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
	public override LocalizedText DisplayName => SourceType.DisplayName;
	public override IEnumerable<ItemSourceFilter> ChildFilters() => SourceType.ChildFilters();
	public override bool Matches(ItemSource source) => source.SourceType == SourceType;
}
public abstract class ItemSourceFilter : ModTexturedType, ILocalizedModType {
	public string LocalizationCategory => "ItemSourceFilter";
	public virtual LocalizedText DisplayName => Mod is null ? ItemSourceHelper.GetLocalization(this) : this.GetLocalization("DisplayName");
	public virtual string DisplayNameText => DisplayName.Value;
	public int Type { get; private set; }
	public int FilterChannel { get; private set; }
	protected virtual string FilterChannelName => null;
	protected virtual bool IsChildFilter => false;
	protected Asset<Texture2D> texture;
	public virtual Asset<Texture2D> TextureAsset => texture;
	public virtual float SortPriority => 1f;
	public sealed override void SetupContent() {
		if (FilterChannelName != null) {
			FilterChannel = FilterChannels.GetChannel(FilterChannelName);
		} else {
			FilterChannel = -1;
		}
		SetStaticDefaults();
		_ = DisplayNameText;
	}
	public void LateRegister() {
		Register();
		SetupContent();
	}
	protected sealed override void Register() {
		ModTypeLookup<ItemSourceFilter>.Register(this);
		if (IsChildFilter) {
			Type = -(++ItemSourceHelper.Instance.ChildFilterCount);
		} else {
			Type = ItemSourceHelper.Instance.Filters.Count;
			ItemSourceHelper.Instance.Filters.Add(this);
		}
		if (!ModContent.RequestIfExists(Texture, out texture)) {
			texture = Asset<Texture2D>.Empty;
		}
	}
	public abstract bool Matches(ItemSource source);
	public virtual IEnumerable<ItemSourceFilter> ChildFilters() => [];
	public bool ShouldReplace(ItemSourceFilter other) => FilterChannel == -1 ? other.Type == Type : other.FilterChannel == FilterChannel;
}
public abstract class ItemFilter : ItemSourceFilter {
	public override sealed bool Matches(ItemSource source) => Matches(source.Item);
	public abstract bool Matches(Item item);
	public override sealed IEnumerable<ItemSourceFilter> ChildFilters() => ChildItemFilters();
	public virtual IEnumerable<ItemFilter> ChildItemFilters() => [];
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
public class SearchProviderLoader : ILoadable {
	static Dictionary<string, SearchProvider> providersByOpener = [];
	public void Load(Mod mod) {}
	public void Unload() {
		providersByOpener = null;
	}
	public static void RegisterSearchProvider(SearchProvider searchProvider) {
		providersByOpener.Add(searchProvider.Opener, searchProvider);
	}
	public static SearchFilter Parse(string text) {
		int consumed = 0;
		do {
			consumed++;
			if (providersByOpener.TryGetValue(text[..consumed], out SearchProvider searchProvider)) return searchProvider.GetSearchFilter(text[consumed..]);
		} while (consumed < text.Length);
		return new LiteralSearchFilter(text);
	}
}
public abstract class SearchProvider : ModType {
	public abstract string Opener { get; }
	public abstract SearchFilter GetSearchFilter(string filterText);
	protected sealed override void Register() {
		ModTypeLookup<SearchProvider>.Register(this);
		SearchProviderLoader.RegisterSearchProvider(this);
	}
}
[Autoload(false)]
public abstract class SearchFilter : ItemFilter {
	protected override bool IsChildFilter => true;
	public override string DisplayNameText => null;
}
public abstract class SourceSorter : ModTexturedType, ILocalizedModType, IComparer<ItemSource> {
	public string LocalizationCategory => "SourceSorter";
	public virtual LocalizedText DisplayName => this.GetLocalization("DisplayName");
	public int Type { get; private set; }
	protected Asset<Texture2D> texture;
	public virtual Asset<Texture2D> TextureAsset => texture;
	protected override void Register() {
		ModTypeLookup<SourceSorter>.Register(this);
		Type = ItemSourceHelper.Instance.SourceSorters.Count;
		ItemSourceHelper.Instance.SourceSorters.Add(this);
		if (!ModContent.RequestIfExists(Texture, out texture)) {
			texture = Asset<Texture2D>.Empty;
		}
		_ = DisplayName.Value;
	}
	public sealed override void SetupContent() {
		SetStaticDefaults();
	}
	internal void SortSources() => SortedSources = ItemSourceHelper.Instance.Sources.Order(this).ToList();
	public List<ItemSource> SortedSources { get; private set; }
	public abstract int Compare(ItemSource x, ItemSource y);
}
