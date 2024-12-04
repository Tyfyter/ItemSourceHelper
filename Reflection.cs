using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;
using Tyfyter.Utils;

namespace ItemSourceHelper {
	internal class Reflection : ILoadable {
		internal static FastStaticFieldInfo<Recipe, Dictionary<int, int>> _ownedItems = new("_ownedItems", BindingFlags.Public | BindingFlags.NonPublic);
		public void Load(Mod mod) {}
		public void Unload() {
			_ownedItems = null;
		}
	}
}
