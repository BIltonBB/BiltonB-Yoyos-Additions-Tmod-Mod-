using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Decorative
{
	// Per-block painting variant storage. Each painting has 3 variants;
	// variant 0 uses the default placed sprite, variants 1-2 share one
	// placeholder placed sprite (so only 2 placed sprites per painting).
	// Right-click cycles through the variants enabled in YoyoConfig.
	public class PaintingSystem : ModSystem
	{
		public static readonly Dictionary<Point, int> Variants = new();

		public static int GetVariant(Point key)
		{
			return Variants.TryGetValue(key, out int v) ? v : 0;
		}

		public static void SetVariant(Point key, int v)
		{
			Variants[key] = v;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static void ForgetTile(Point key)
		{
			if (Variants.Remove(key) && Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static int EnabledCount(bool night)
		{
			try {
				var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
				int n = night ? cfg.NightPaintingVariants : cfg.DayPaintingVariants;
				return System.Math.Clamp(n, 1, 3);
			}
			catch {
				return 3;
			}
		}

		public static string VariantName(bool night, int v) => (night, v) switch {
			(true, 0) => "Night Yoyo (Midnight Spin)",
			(true, 1) => "Night Yoyo (Starless Loop)",
			(true, 2) => "Night Yoyo (Eclipse Sleeper)",
			(false, 0) => "Stringy Day (Morning Throw)",
			(false, 1) => "Stringy Day (Noon Walk)",
			_ => "Stringy Day (Golden Hour)",
		};

		public override void OnWorldLoad() => Variants.Clear();
		public override void OnWorldUnload() => Variants.Clear();

		public override void SaveWorldData(TagCompound tag)
		{
			var xs = new List<int>();
			var ys = new List<int>();
			var vs = new List<int>();
			foreach (var kv in Variants) {
				xs.Add(kv.Key.X);
				ys.Add(kv.Key.Y);
				vs.Add(kv.Value);
			}
			tag["paintX"] = xs;
			tag["paintY"] = ys;
			tag["paintV"] = vs;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Variants.Clear();
			if (!tag.ContainsKey("paintX"))
				return;
			var xs = tag.Get<List<int>>("paintX");
			var ys = tag.Get<List<int>>("paintY");
			var vs = tag.Get<List<int>>("paintV");
			int n = System.Math.Min(xs.Count, System.Math.Min(ys.Count, vs.Count));
			for (int i = 0; i < n; i++)
				Variants[new Point(xs[i], ys[i])] = System.Math.Clamp(vs[i], 0, 2);
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Variants.Count);
			foreach (var kv in Variants) {
				writer.Write(kv.Key.X);
				writer.Write(kv.Key.Y);
				writer.Write(kv.Value);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			Variants.Clear();
			int n = reader.ReadInt32();
			for (int i = 0; i < n; i++)
				Variants[new Point(reader.ReadInt32(), reader.ReadInt32())] = System.Math.Clamp(reader.ReadInt32(), 0, 2);
		}
	}
}
