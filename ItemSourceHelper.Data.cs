using ItemSourceHelper.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ItemSourceHelper {
	public static class Data {
		public static void ResizeArrays() {
			List<string> preOrderedFilterChannels = ItemSourceHelper.Instance.PreOrderedFilterChannels;
			for (int i = 0; i < preOrderedFilterChannels.Count; i++) {
				FilterChannels.GetChannel(preOrderedFilterChannels[i]);
			}
		}
		public static void Unload() {
		}
	}
}
