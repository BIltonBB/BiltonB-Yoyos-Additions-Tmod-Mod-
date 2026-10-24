using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace BiltonsYoyoMod.Traps
{
	// Natural generation for offensive traps. New worlds only via config gates.
	// Safety: never overwrite tiles, preserve Dungeon walls/doors/chests/traps,
	// avoid protected tiles, enforce spacing + density caps, independent toggles.
	public class TrapWorldGen : ModSystem
	{
		public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
		{
			int idx = tasks.FindIndex(t => t.Name.Equals("Final Cleanup"));
			if (idx < 0)
				idx = tasks.Count;
			tasks.Insert(idx, new PassLegacy("Bilton Traps", (progress, config) => GenTraps(progress)));
		}

		private void GenTraps(GenerationProgress progress)
		{
			progress.Message = "Placing Bilton traps";
			try {
				Configuration.YoyoServerConfig cfg = ModContent.GetInstance<Configuration.YoyoServerConfig>();
				bool snareD = cfg?.SpawnWildstringSnareInDungeon ?? true;
				bool snareC = cfg?.SpawnWildstringSnareInCaverns ?? false;
				bool bearD = cfg?.SpawnBearingTrapInDungeon ?? true;
				bool bearC = cfg?.SpawnBearingTrapInCaverns ?? false;
				if (!snareD && !snareC && !bearD && !bearC)
					return;
				int dungeonSnare = 0, cavernSnare = 0, dungeonBear = 0, cavernBear = 0;
				const int minSpacing = 12;

				var placed = new List<Point>();

				bool TooClose(int x, int y)
				{
					foreach (var p in placed) {
						int dx = p.X - x, dy = p.Y - y;
						if (dx * dx + dy * dy < minSpacing * minSpacing)
							return true;
					}
					return false;
				}

				bool SafeDungeonSpot(int x, int y)
				{
					if (!WorldGen.InWorld(x, y, 4))
						return false;
					// Must be inside dungeon (dungeon walls).
					Tile t = Framing.GetTileSafely(x, y);
					Tile below = Framing.GetTileSafely(x, y + 1);
					if (!below.HasTile || !Main.tileSolid[below.TileType])
						return false;
					if (t.HasTile)
						return false; // never overwrite
					// Dungeon wall check on surrounding tiles.
					bool dungeonWall = false;
					for (int dx = -2; dx <= 2; dx++) {
						for (int dy = -2; dy <= 2; dy++) {
							Tile wt = Framing.GetTileSafely(x + dx, y + dy);
							if (wt.WallType == WallID.BlueDungeonSlabUnsafe || wt.WallType == WallID.BlueDungeonTileUnsafe
								|| wt.WallType == WallID.GreenDungeonSlabUnsafe || wt.WallType == WallID.GreenDungeonTileUnsafe
								|| wt.WallType == WallID.PinkDungeonSlabUnsafe || wt.WallType == WallID.PinkDungeonTileUnsafe
								|| wt.WallType == WallID.BlueDungeonSlab || wt.WallType == WallID.BlueDungeonTile
								|| wt.WallType == WallID.GreenDungeonSlab || wt.WallType == WallID.GreenDungeonTile
								|| wt.WallType == WallID.PinkDungeonSlab || wt.WallType == WallID.PinkDungeonTile) {
								dungeonWall = true;
								break;
							}
						}
						if (dungeonWall) break;
					}
					if (!dungeonWall)
						return false;
					// Avoid chests, doors, protected tiles nearby.
					for (int dx = -2; dx <= 2; dx++) {
						for (int dy = -2; dy <= 2; dy++) {
							Tile nt = Framing.GetTileSafely(x + dx, y + dy);
							if (!nt.HasTile) continue;
							if (nt.TileType == TileID.Containers || nt.TileType == TileID.Containers2
								|| nt.TileType == TileID.ClosedDoor || nt.TileType == TileID.OpenDoor
								|| nt.TileType == TileID.Spikes || nt.TileType == TileID.WoodenSpikes
								|| nt.TileType == TileID.GeyserTrap
								|| TileID.Sets.BasicChest[nt.TileType] )
								return false;
						}
					}
					// Need headroom (2 free tiles above for fairness).
					Tile head1 = Framing.GetTileSafely(x, y - 1);
					Tile head2 = Framing.GetTileSafely(x, y - 2);
					if (head1.HasTile && Main.tileSolid[head1.TileType])
						return false;
					if (head2.HasTile && Main.tileSolid[head2.TileType])
						return false;
					return true;
				}

				bool SafeCavernSpot(int x, int y)
				{
					if (!WorldGen.InWorld(x, y, 4))
						return false;
					if (y < (int)Main.rockLayer)
						return false;
					if (y > Main.maxTilesY - 200)
						return false;
					Tile t = Framing.GetTileSafely(x, y);
					Tile below = Framing.GetTileSafely(x, y + 1);
					if (!below.HasTile || !Main.tileSolid[below.TileType])
						return false;
					if (t.HasTile)
						return false;
					// Avoid dungeon / temple / important structures.
					for (int dx = -2; dx <= 2; dx++) {
						for (int dy = -2; dy <= 2; dy++) {
							Tile nt = Framing.GetTileSafely(x + dx, y + dy);
							if (!nt.HasTile) continue;
							if (nt.TileType == TileID.Containers || nt.TileType == TileID.Containers2
								|| nt.TileType == TileID.Spikes || nt.TileType == TileID.WoodenSpikes
								|| nt.TileType == TileID.LihzahrdBrick
								|| nt.TileType == TileID.LihzahrdAltar || TileID.Sets.BasicChest[nt.TileType])
								return false;
						}
					}
					return true;
				}

				void TryPlace(int x, int y, int tileType, TrapKind kind, ref int counter)
				{
					WorldGen.PlaceTile(x, y, tileType, true, true);
					Tile placed_tile = Framing.GetTileSafely(x, y);
					// Count ONLY on success (blind counters used to burn the
					// quota on failed placements, spawning fewer traps).
					if (placed_tile.HasTile && placed_tile.TileType == tileType) {
						Point key = new Point(x, y);
						if (!TrapSystem.Traps.ContainsKey(key))
							TrapSystem.Traps[key] = TrapState.DefaultFor(kind);
						placed.Add(key);
						counter++;
					}
				}

				// Spaced pick from candidates: shuffle, then take spots that
				// keep their distance (all trap kinds share one spacing pool).
				void PickSpots(List<Point> candidates, int want, int tileType, TrapKind kind, ref int counter)
				{
					// Fisher-Yates shuffle with the worldgen rand.
					for (int k = candidates.Count - 1; k > 0; k--) {
						int r = WorldGen.genRand.Next(k + 1);
						Point tmp = candidates[k];
						candidates[k] = candidates[r];
						candidates[r] = tmp;
					}
					foreach (var c in candidates) {
						if (counter >= want)
							break;
						if (TooClose(c.X, c.Y))
							continue;
						TryPlace(c.X, c.Y, tileType, kind, ref counter);
					}
				}

				// Dungeon pass: SCAN for real spots (blind random sampling
				// almost never lands inside the dungeon, yielding empty worlds).
				if (snareD || bearD) {
					var snareSpots = new List<Point>();
					var bearSpots = new List<Point>();
					for (int x = 40; x < Main.maxTilesX - 40; x += 6) {
						for (int y = 80; y < Main.maxTilesY - 60; y += 6) {
							if (snareD && snareSpots.Count < 60 && SafeDungeonSpot(x, y))
								snareSpots.Add(new Point(x, y));
							else if (bearD && bearSpots.Count < 60 && SafeDungeonSpot(x, y))
								bearSpots.Add(new Point(x, y));
							if (snareSpots.Count >= 60 && bearSpots.Count >= 60)
								break;
						}
						if (snareSpots.Count >= 60 && bearSpots.Count >= 60)
							break;
					}
					PickSpots(snareSpots, 6, ModContent.TileType<WildstringSnareTile>(), TrapKind.WildSnare, ref dungeonSnare);
					PickSpots(bearSpots, 6, ModContent.TileType<BearingTrapTile>(), TrapKind.Bearing, ref dungeonBear);
				}

				// Caverns pass: same scan approach over the rock layer.
				if (snareC || bearC) {
					var snareSpots = new List<Point>();
					var bearSpots = new List<Point>();
					for (int x = 40; x < Main.maxTilesX - 40; x += 9) {
						for (int y = (int)Main.rockLayer; y < Main.maxTilesY - 200; y += 9) {
							if (snareC && snareSpots.Count < 60 && SafeCavernSpot(x, y))
								snareSpots.Add(new Point(x, y));
							else if (bearC && bearSpots.Count < 60 && SafeCavernSpot(x, y))
								bearSpots.Add(new Point(x, y));
							if (snareSpots.Count >= 60 && bearSpots.Count >= 60)
								break;
						}
						if (snareSpots.Count >= 60 && bearSpots.Count >= 60)
							break;
					}
					PickSpots(snareSpots, 6, ModContent.TileType<WildstringSnareTile>(), TrapKind.WildSnare, ref cavernSnare);
					PickSpots(bearSpots, 6, ModContent.TileType<BearingTrapTile>(), TrapKind.Bearing, ref cavernBear);
				}
			}
			catch {
				// Worldgen must never crash world creation.
			}
		}
	}
}
