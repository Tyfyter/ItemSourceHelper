using ItemSourceHelper.Default;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using ReLogic.Content;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ItemSourceHelper.Core;
public abstract class ItemSource(ItemSourceType sourceType, int itemType) {
	public ItemSourceType SourceType => sourceType;
	public int ItemType => itemType;
	Item item;
	public Item Item => item ??= ContentSamples.ItemsByType[ItemType];
	public ItemSource(ItemSourceType sourceType, Item item) : this(sourceType, item.type) {
		this.item = item;
	}
	public virtual IEnumerable<Condition> GetConditions() {
		yield break;
	}
	public virtual IEnumerable<LocalizedText> GetExtraConditionText() => null;
	public virtual IEnumerable<Item> GetSourceItems() {
		yield break;
	}
	public virtual IEnumerable<HashSet<int>> GetSourceGroups() {
		yield break;
	}
	public IEnumerable<LocalizedText> GetAllConditions() => (GetConditions() ?? []).Select(c => c.Description).Concat((GetExtraConditionText() ?? []).Select(c => c));
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
public abstract class LootSourceType : ModTexturedType, ILocalizedModType {
	public string LocalizationCategory => "LootSourceType";
	public virtual LocalizedText DisplayName => this.GetLocalization("DisplayName");
	public abstract IEnumerable<LootSource> FillSourceList();
	public int Type { get; private set; }
	protected override void Register() {
		ModTypeLookup<LootSourceType>.Register(this);
		Type = ItemSourceHelper.Instance.LootSourceTypes.Count;
		ItemSourceHelper.Instance.LootSourceTypes.Add(this);
		Mod.AddContent(new LootSourceTypeFilter(this));
		_ = DisplayName.Value;
	}
	public sealed override void SetupContent() {
		SetStaticDefaults();
	}
	public virtual void PostSetupRecipes() { }
	public virtual IEnumerable<LootSourceFilter> ChildFilters() => [];
	public abstract List<DropRateInfo> GetDrops(int type);
	public abstract void DrawSource(SpriteBatch spriteBatch, int type, Vector2 position, bool hovering);
	public abstract Dictionary<string, string> GetSearchData(int type);
	/// <summary>
	/// return true if something was done
	/// </summary>
	public virtual bool DoubleClick(int type) => false;
}
public interface IFilterBase { }
public interface IFilter<T> : IFilterBase {
	public string DisplayNameText { get; }
	public int DisplayNameRarity => ItemRarityID.White;
	public bool Matches(T source);
	public IEnumerable<IFilter<T>> ChildFilters();
	public int FilterChannel { get; }
	public int Type { get; }
	public Texture2D TextureValue { get; }
	public DrawAnimation TextureAnimation => null;
	public IFilter<T> SimplestForm => this;
	public ICollection<IFilter<T>> ActiveChildren { get; }
	public bool ShouldHide() => false;
}
public interface ITooltipModifier {
	public void ModifyTooltips(Item item, List<TooltipLine> tooltips);
}
public class OrFilter<T>(params IFilter<T>[] filters) : IFilter<T> {
	public List<IFilter<T>> Filters { get; private set; } = filters.ToList();
	public string DisplayNameText => string.Join(" | ", Filters.Select(f => f.DisplayNameText));
	public int FilterChannel => Filters[0].FilterChannel;
	public int Type => Filters[0].Type;
	public Texture2D TextureValue => Asset<Texture2D>.DefaultValue;
	public IEnumerable<IFilter<T>> ChildFilters() => [];
	public bool Matches(T source) {
		for (int i = 0; i < Filters.Count; i++) {
			if (Filters[i].MatchesAll(source)) return true;
		}
		return false;
	}
	public void Add(IFilter<T> value) => Filters.Add(value);
	public bool Contains(IFilter<T> value) => Filters.Contains(value, containsComparer);
	public IFilter<T> Remove(IFilter<T> value) {
		Filters.Remove(value);
		return Filters.Count == 1 ? Filters[0] : this;
	}
	public IFilter<T> SimplestForm => Filters.Count == 1 ? Filters[0] : this;
	public ICollection<IFilter<T>> ActiveChildren { get; private set; } = [];
	static readonly ContainsComparer containsComparer = new();
	class ContainsComparer : IEqualityComparer<IFilter<T>> {
		public bool Equals(IFilter<T> x, IFilter<T> y) => (x is NotFilter<T> notFilterX ? notFilterX.Filter : x) == (y is NotFilter<T> notFilterY ? notFilterY.Filter : y);
		public int GetHashCode([DisallowNull] IFilter<T> obj) => obj is NotFilter<T> notFilter ? notFilter.Filter.GetHashCode() : obj.GetHashCode();
	}
}
public class NotFilter<T>(IFilter<T> filter) : IFilter<T> {
	public IFilter<T> Filter => filter;
	public string DisplayNameText => "-" + filter.DisplayNameText;
	public int FilterChannel => filter.FilterChannel;
	public int Type => filter.Type;
	public Texture2D TextureValue => Asset<Texture2D>.DefaultValue;
	public IEnumerable<IFilter<T>> ChildFilters() => [];
	public bool Matches(T source) => !filter.MatchesAll(source);
	public ICollection<IFilter<T>> ActiveChildren { get; private set; } = [];
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
[Autoload(false)]
public class LootSourceTypeFilter(LootSourceType sourceType) : LootSourceFilter {
	public LootSourceType SourceType => sourceType;
	public override string Name => "SourceTypeFilter_" + SourceType.FullName;
	public override string Texture => SourceType.Texture;
	protected override string FilterChannelName => "SourceType";
	public override LocalizedText DisplayName => SourceType.DisplayName;
	public override IEnumerable<LootSourceFilter> ChildFilters() => SourceType.ChildFilters();
	public override bool Matches(LootSource source) => source.SourceType == SourceType;
}
public abstract class SourceFilterBase<T> : ModTexturedType, ILocalizedModType, IFilter<T> {
	public abstract string LocalizationCategory { get; }
	public virtual LocalizedText DisplayName => Mod is null ? ItemSourceHelper.GetLocalization(this) : this.GetLocalization("DisplayName");
	public virtual int DisplayNameRarity => ItemRarityID.White;
	public virtual string DisplayNameText => DisplayName.Value;
	public int Type { get; protected set; }
	public int FilterChannel { get; protected set; }
	protected virtual string FilterChannelName => null;
	protected virtual int? FilterChannelTargetPlacement => null;
	protected virtual bool IsChildFilter => false;
	protected Asset<Texture2D> texture;
	public virtual Texture2D TextureValue => texture.Value;
	protected DrawAnimation animation;
	public virtual DrawAnimation TextureAnimation => animation;
	public virtual float SortPriority => 1f;
	public sealed override void SetupContent() {
		SetupChildren();
		if (FilterChannelName != null) {
			if (!FilterChannels.ChannelExists(FilterChannelName) && FilterChannelTargetPlacement.HasValue) {
				int placement = FilterChannelTargetPlacement.Value;
				while (!FilterChannels.ReserveChannel(FilterChannelName, placement)) placement++;
			}
			FilterChannel = FilterChannels.GetChannel(FilterChannelName);
		} else {
			FilterChannel = -1;
		}
		SetStaticDefaults();
		_ = DisplayNameText;
	}
	public abstract void SetupChildren();
	public virtual void PostSetupRecipes() { }
	public virtual bool ShouldHide() => false;
	public void LateRegister() {
		Register();
		SetupContent();
	}
	public abstract bool Matches(T source);
	public virtual IEnumerable<IFilter<T>> ChildFilters() => [];
	protected void UseItemTexture(int itemType) {
		Main.instance.LoadItem(itemType);
		texture = TextureAssets.Item[itemType];
		animation = Main.itemAnimations[itemType];
	}
	public ICollection<IFilter<T>> ActiveChildren { get; protected set; }
}
public abstract class LootSourceFilter : SourceFilterBase<LootSource> {
	public override string LocalizationCategory => "LootSourceFilter";
	public override void SetupChildren() {
		ActiveChildren = new List<IFilter<LootSource>>();
	}
	protected sealed override void Register() {
		ModTypeLookup<LootSourceFilter>.Register(this);
		if (IsChildFilter) {
			Type = -(++ItemSourceHelper.Instance.ChildFilterCount);
		} else {
			Type = ItemSourceHelper.Instance.LootFilters.Count;
			ItemSourceHelper.Instance.LootFilters.Add(this);
		}
		if (!ModContent.RequestIfExists(Texture, out texture)) {
			texture = Asset<Texture2D>.Empty;
		}
	}
}
public abstract class ItemSourceFilter : SourceFilterBase<ItemSource> {
	public override string LocalizationCategory => "ItemSourceFilter";
	public override void SetupChildren() {
		ActiveChildren = new List<IFilter<ItemSource>>();
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
}
public abstract class ItemFilter : ItemSourceFilter, IFilter<Item> {
	public override void SetupChildren() {
		base.SetupChildren();
		ActiveChildren = new SubCollection<IFilter<Item>, IFilter<ItemSource>>(base.ActiveChildren);
	}
	public override sealed bool Matches(ItemSource source) => Matches(source.Item);
	public abstract bool Matches(Item item);
	public override sealed IEnumerable<ItemSourceFilter> ChildFilters() => ChildItemFilters();
	IEnumerable<IFilter<Item>> IFilter<Item>.ChildFilters() => ChildItemFilters();
	public virtual IEnumerable<ItemFilter> ChildItemFilters() => [];
	public new ICollection<IFilter<Item>> ActiveChildren { get; private set; }
}
public class FilterChannels : ILoadable {
	static Dictionary<int, string> channels = [];
	static Dictionary<string, int> channelsReverse = [];
	static int channelCount = 1;
	public static bool ChannelExists(string name) => channelsReverse.ContainsKey(name);
	public static int GetChannel(string name) {
		if (channelsReverse.TryGetValue(name, out int index)) return index;
		while (channels.ContainsKey(channelCount)) channelCount++;
		channels.Add(channelCount, name);
		channelsReverse.Add(name, channelCount);
		return channelCount;
	}
	public static bool ReserveChannel(string name, int id) {
		if (!channelsReverse.ContainsKey(name) && channels.TryAdd(id, name)) {
			channelsReverse.Add(name, id);
			return true;
		} else return false;
	}
	public void Load(Mod mod) { }
	public void Unload() {
		channels = null;
		channelsReverse = null;
	}
}
public class SearchLoader : ILoadable {
	static Dictionary<string, SearchProvider> providersByOpener = [];
	public void Load(Mod mod) {}
	public void Unload() {
		providersByOpener = null;
	}
	public static void RegisterSearchProvider(SearchProvider searchProvider) {
		providersByOpener.Add(searchProvider.Opener, searchProvider);
	}
	public static Dictionary<string, string> GetSearchData<T>(T value) => SearchDataGetter<T>.function(value);
	public static void RegisterSearchable<T>(Func<T, Dictionary<string, string>> function) {
		SearchDataGetter<T>.function = function;
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
internal static class SearchDataGetter<T> {
	internal static Func<T, Dictionary<string, string>> function = _ => [];
}
public abstract class SearchProvider : ModType {
	public abstract string Opener { get; }
	public abstract SearchFilter GetSearchFilter(string filterText);
	protected sealed override void Register() {
		ModTypeLookup<SearchProvider>.Register(this);
		SearchLoader.RegisterSearchProvider(this);
	}
}
[Autoload(false)]
public abstract class SearchFilter : IFilter<Dictionary<string, string>> {
	public string DisplayNameText => null;
	public int FilterChannel { get; }
	public int Type { get; }
	public Texture2D TextureValue { get; }
	public ICollection<IFilter<Dictionary<string, string>>> ActiveChildren { get; }
	public IEnumerable<IFilter<Dictionary<string, string>>> ChildFilters() => [];
	public abstract bool Matches(Dictionary<string, string> source);
}
public interface ISorter<T> {
	public List<T> SortedValues { get; }
	public LocalizedText DisplayName { get; }
	public Asset<Texture2D> TextureAsset { get; }
	public Predicate<IFilterBase>[] FilterRequirements { get; }
	public void SetupRequirements();
}
public abstract class SourceSorter : ModTexturedType, ILocalizedModType, IComparer<ItemSource>, ISorter<ItemSource> {
	public string LocalizationCategory => "SourceSorter";
	public virtual LocalizedText DisplayName => this.GetLocalization("DisplayName");
	public int Type { get; private set; }
	protected Asset<Texture2D> texture;
	public virtual Asset<Texture2D> TextureAsset => texture;
	public virtual float SortPriority => 1f;
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
	internal void SortSources() => SortedSources = ItemSourceHelper.Instance.Sources.Where(SourceFilter).Order(this).ToList();
	public virtual bool SourceFilter(ItemSource source) => true;
	public List<ItemSource> SortedSources { get; private set; }
	public List<ItemSource> SortedValues => SortedSources;
	public abstract int Compare(ItemSource x, ItemSource y);
	public virtual void SetupRequirements() {

	}
	public Predicate<IFilterBase>[] FilterRequirements { get; protected set;  } = [];
}
public abstract class ItemSorter : SourceSorter, IComparer<Item>, ISorter<Item> {
	internal void SortItems() => SortedItems = ContentSamples.ItemsByType.Values.Where(i => !i.IsAir).Where(ItemFilter).Order(this).ToList();
	public virtual bool ItemFilter(Item item) => true;
	public override bool SourceFilter(ItemSource source) => ItemFilter(source.Item);
	public List<Item> SortedItems { get; private set; }
	public new List<Item> SortedValues => SortedItems;
	public abstract int Compare(Item x, Item y);
	public override int Compare(ItemSource x, ItemSource y) {
		int itemComp = Compare(x.Item, y.Item);
		if (itemComp != 0) return itemComp;
		return Comparer<int>.Default.Compare(x.SourceType.Type, y.SourceType.Type);
	}
}
public class HashSetComparer<T> : IEqualityComparer<ISet<T>> {
	public bool Equals(ISet<T> x, ISet<T> y) => x.SetEquals(y);
	public int GetHashCode([DisallowNull] ISet<T> obj) {
		int hash = 0;
		foreach (T item in obj) hash ^= item.GetHashCode();
		return hash;
	}
}
public class SubCollection<T, B>(ICollection<B> Parent) : ICollection<T> {
	public int Count => Parent.Count;
	public bool IsReadOnly => Parent.IsReadOnly;
	readonly List<T> rejects = [];
	public void Add(T _item) {
		if (_item is B item) Parent.Add(item);
		else rejects.Add(_item);
	}
	public void Clear() {
		Parent.Clear();
		rejects.Clear();
	}
	public bool Contains(T _item) => _item is B item ? Parent.Contains(item) : rejects.Contains(_item);
	public void CopyTo(T[] array, int arrayIndex) {
		foreach (T item in this) {
			array[arrayIndex++] = item;
		}
		foreach (T item in rejects) {
			array[arrayIndex++] = item;
		}
	}
	public IEnumerator<T> GetEnumerator() {
		foreach (B _item in Parent) if (_item is T item) yield return item;
		for (int i = 0; i < rejects.Count; i++) yield return rejects[i];
	}
	public bool Remove(T _item) => _item is B item ? Parent.Remove(item) : rejects.Remove(_item);
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
public class SingleFrameAnimation(Rectangle frame) : DrawAnimation {
	public SingleFrameAnimation(int frameX = 0, int frameY = 0, int width = 0, int height = 0) : this(new(frameX, frameY, width, height)) { }
	public override Rectangle GetFrame(Texture2D texture, int frameCounterOverride = -1) => frame;
}
public static class CoreExtenstions {
	public static bool MatchesAll<T>(this IFilter<T> filter, T item) {
		if (!filter.Matches(item)) return false;
		if (filter.ActiveChildren is not null) foreach (IFilter<T> child in filter.ActiveChildren) if (!child.Matches(item)) return false;
		return true;
	}
	public static void SetChild<T>(this IFilter<T> filter, IFilter<T> child, bool? active) {
		if (filter.ActiveChildren.Contains(child)) {
			if (active != true) filter.ActiveChildren.Remove(child);
		} else {
			if (active != false) filter.ActiveChildren.Add(child);
		}
	}
	public static bool Passes(this List<BlockedSource> self, ItemSource itemSource) {
		for (int i = 0; i < self.Count; i++) {
			if (self[i].Matches(itemSource)) return false;
		}
		return true;
	}
}