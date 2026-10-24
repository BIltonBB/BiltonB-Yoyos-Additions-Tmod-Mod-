using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.WorldBuilding;

namespace BiltonsYoyoMod.Content.Decorative
{
	// Rarely replaces a vanilla painting with a yoyo painting during worldgen.
	// Same-size only (3x2 -> Night, 2x3 -> Day) so no holes or overlaps.
	// Painting types resolved via TileID reflection (names starting with
	// "Painting") so missing sizes on any version simply never match.
	public class PaintingWorldGen : ModSystem
	{
		public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
		{
			int idx = tasks.FindIndex(t => t.Name.Equals("Final Cleanup"));
			if (idx < 0)
				idx = tasks.Count;
			tasks.Insert(idx, new PassLegacy("Bilton Yoyo Paintings", (progress, config) => GenPaintings(progress)));
		}

		private static HashSet<int> PaintingTypes()
		{
			var set = new HashSet<int>();
			foreach (var f in typeof(TileID).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
				if (f.FieldType != typeof(ushort) && f.FieldType != typeof(int))
					continue;
				if (!f.Name.StartsWith("Painting"))
					continue;
				try {
					int v = System.Convert.ToInt32(f.GetValue(null));
					if (v > 0)
						set.Add(v);
				}
				catch { }
			}
			return set;
		}

		private void GenPaintings(GenerationProgress progress)
		{
			progress.Message = "Hanging yoyo paintings";
			try {
				var cfg = ModContent.GetInstance<Configuration.YoyoServerConfig>();
				if (cfg != null && !cfg.ReplaceVanillaPaintings)
					return;
				var paintings = PaintingTypes();
				if (paintings.Count == 0)
					return;
				int nightType = ModContent.TileType<Tiles.Decorative.NightYoyoPaintingTile>();
				int dayType = ModContent.TileType<Tiles.Decorative.StringyDayPaintingTile>();
				var candidates = new List<Point>();
				for (int x = 20; x < Main.maxTilesX - 20; x++) {
					for (int y = 20; y < Main.maxTilesY - 20; y++) {
						Tile t = Framing.GetTileSafely(x, y);
						if (!t.HasTile || !paintings.Contains(t.TileType))
							continue;
						if (t.TileFrameX != 0 || t.TileFrameY != 0)
							continue; // origin only
						candidates.Add(new Point(x, y));
					}
				}
				// Shuffle with worldgen rand, replace rarely (5%).
				for (int k = candidates.Count - 1; k > 0; k--) {
					int r = WorldGen.genRand.Next(k + 1);
					Point tmp = candidates[k];
					candidates[k] = candidates[r];
					candidates[r] = tmp;
				}
				foreach (Point p in candidates) {
					if (!WorldGen.genRand.NextBool(20))
						continue;
					Tile t = Framing.GetTileSafely(p.X, p.Y);
					if (!t.HasTile || !paintings.Contains(t.TileType))
						continue;
					TileObjectData data = TileObjectData.GetTileData(t);
					if (data == null)
						continue;
					int w = data.Width;
					int h = data.Height;
					int want = -1;
					if (w == 3 && h == 2)
						want = nightType;
					else if (w == 2 && h == 3)
						want = dayType;
					else
						continue;
					// Clear old multitile.
					for (int dx = 0; dx < w; dx++) {
						for (int dy = 0; dy < h; dy++) {
							Tile c = Framing.GetTileSafely(p.X + dx, p.Y + dy);
							c.ClearTile();
						}
					}
					WorldGen.PlaceTile(p.X, p.Y, want, true, true);
					Tile placed = Framing.GetTileSafely(p.X, p.Y);
					if (!placed.HasTile || placed.TileType != want) {
						// Rollback safety: never leave a hole; worldgen must
						// never crash world creation.
						continue;
					}
				}
			}
			catch {
				// Worldgen must never crash world creation.
			}
		}
	}
}
