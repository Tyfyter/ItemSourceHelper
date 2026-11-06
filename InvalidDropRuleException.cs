using System;
using System.Collections;
using Terraria.GameContent.ItemDropRules;

namespace ItemSourceHelper;
[Serializable]
public class InvalidDropRuleException(Exception inner, IItemDropRule rule, string inContent) : Exception(null, inner) {
	public override string Message => $"Exception thrown while reporting drops for rule {rule} from {inContent}: {InnerException.Message}";
	public override IDictionary Data { get; } = new BlamingDictionary(inner.Data);
	public override string HelpLink { get => InnerException.HelpLink; set => InnerException.HelpLink = value; }
	public override string Source { get => InnerException.Source; set => InnerException.Source = value; }
	readonly struct BlamingDictionary(IDictionary dictionary) : IDictionary {
		public readonly object this[object key] {
			get => dictionary[key];
			set {
				if (key is string _key && _key == "mod" && dictionary.Contains(key)) return;
				dictionary[key] = value;
			}
		}
		public bool IsFixedSize => dictionary.IsFixedSize;
		public bool IsReadOnly => dictionary.IsReadOnly;
		public ICollection Keys => dictionary.Keys;
		public ICollection Values => dictionary.Values;
		public int Count => dictionary.Count;
		public bool IsSynchronized => dictionary.IsSynchronized;
		public object SyncRoot => dictionary.SyncRoot;
		public void Add(object key, object value) => dictionary.Add(key, value);
		public void Clear() => dictionary.Clear();
		public bool Contains(object key) => dictionary.Contains(key);
		public void CopyTo(Array array, int index) => dictionary.CopyTo(array, index);
		public IDictionaryEnumerator GetEnumerator() => dictionary.GetEnumerator();
		public void Remove(object key) => dictionary.Remove(key);
		IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)dictionary).GetEnumerator();
	}
}
