using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Traps
{
	public static class TrapTiles
	{
		public static int TileTypeFor(TrapKind kind)
		{
			return kind switch {
				TrapKind.WildSnare => ModContent.TileType<WildstringSnareTile>(),
				TrapKind.Bearing => ModContent.TileType<BearingTrapTile>(),
				TrapKind.Sentry => ModContent.TileType<CounterweightSentryTile>(),
				TrapKind.Link => ModContent.TileType<StringLinkTile>(),
				_ => ModContent.TileType<YoyoTripwireTile>(),
			};
		}

		public static TrapKind? KindForTile(int tileType)
		{
			if (tileType == ModContent.TileType<WildstringSnareTile>()) return TrapKind.WildSnare;
			if (tileType == ModContent.TileType<BearingTrapTile>()) return TrapKind.Bearing;
			if (tileType == ModContent.TileType<CounterweightSentryTile>()) return TrapKind.Sentry;
			if (tileType == ModContent.TileType<StringLinkTile>()) return TrapKind.Link;
			if (tileType == ModContent.TileType<YoyoTripwireTile>()) return TrapKind.Tripwire;
			return null;
		}
	}

	public abstract class TrapTileBase : ModTile
	{
		protected abstract TrapKind Kind { get; }
		protected abstract Color MapColor { get; }
		protected abstract int ItemType { get; }

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			// Attachable so other blocks (torches, ropes, platforms) can sit on
			// these 1x1 trap tiles; the traps themselves place freely anywhere.
			Main.tileNoAttach[Type] = false;
			Main.tileLavaDeath[Type] = false; // traps survive lava: roof/wall/floor/underworld alike
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
			// Style1x1 anchors to the tile below: clear it so traps place on floor,
			// wall, ceiling, or mid-air and never pop when support changes.
			TileObjectData.newTile.AnchorBottom = default;
			TileObjectData.newTile.Width = 1;
			TileObjectData.newTile.Height = 1;
			TileObjectData.newTile.Origin = new Point16(0, 0);
			TileObjectData.newTile.CoordinateHeights = new[] { 16 };
			TileObjectData.addTile(Type);
			AddMapEntry(MapColor);
		}

		// Single-tile traps anchor to nothing: they place on floor, wall, ceiling
		// or mid-air, and never pop when neighbors change. Only mining removes them.
		public override bool CanExplode(int i, int j)
		{
			return false;
		}

		// Working glow: endpoints breathe warm light with live load.
		// ModifyLight runs inside the lighting pass (unlike draw-phase
		// AddLight, this actually illuminates). Unwired pairs smoulder dim.
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			try {
				if (!ModContent.GetInstance<Configuration.YoyoConfig>().TrapVisuals.StressGlow)
					return;
			}
			catch { }
			Point key = new Point(i, j);
			if (!TrapSystem.Traps.TryGetValue(key, out var s) || s.Kind != TrapKind.Link)
				return;
			float f = System.Math.Clamp(s.Stress / (float)System.Math.Max(1, s.MaxStress()), 0f, 1f);
			if (!s.WiredActive)
				f *= 0.25f;
			r += 0.10f + 0.40f * f;
			g += 0.08f + 0.28f * f;
			b += 0.04f + 0.08f * f;
		}

		// Tile-draw coordinate fix (per official ExampleMod guidance): custom
		// tile PreDraw/PostDraw drawing must add this shift. In Retro/Trippy
		// lighting the tile pass renders off-screen and every manual
		// world-to-screen conversion is off by offScreenRange without it.
		// In Color mode (draw-to-screen) it is exactly zero: no behavior change.
		protected static Vector2 TileDrawShift()
		{
			return Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
		}

		protected static Vector2 ToScreen(Vector2 world)
		{
			return world - Main.screenPosition + TileDrawShift();
		}

		public override void PlaceInWorld(int i, int j, Item item)
		{
			Point key = new Point(i, j);
			if (!TrapSystem.Traps.ContainsKey(key))
				TrapSystem.Traps[key] = TrapState.DefaultFor(Kind);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// Same-tick double-break guard: MP resends and replacement breaks can
		// invoke KillTile twice for one tile — second call drops nothing.
		private static readonly Dictionary<Point, int> lastDropTick = new();

		public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
		{
			if (fail || effectOnly || noItem)
				return;
			Point key = new Point(i, j);
			int now = (int)Main.GameUpdateCount;
			if (lastDropTick.TryGetValue(key, out int prev) && now - prev < 60) {
				TrapSystem.ForgetTile(key);
				return;
			}
			lastDropTick[key] = now;
			if (lastDropTick.Count > 200) {
				var stale = new System.Collections.Generic.List<Point>();
				foreach (var kv in lastDropTick) {
					if (now - kv.Value > 3600)
						stale.Add(kv.Key);
				}
				foreach (var k in stale)
					lastDropTick.Remove(k);
			}
			// Drop happens via the automatic createTile system (one placing
			// item per trap); manual spawn here doubled it. This hook only
			// forgets trap state (deduped: MP resends can invoke it twice).
			TrapSystem.ForgetTile(key);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ItemType;
			player.noThrow = 2;
		}

		public override bool RightClick(int i, int j)
		{
			Player player = Main.LocalPlayer;
			Point key = new Point(i, j);
			// String Link + linker held: right-click pairs endpoints (never opens UI).
			// Pairing (linker) and activation (wiring) stay separate systems.
			if (Kind == TrapKind.Link && player != null && player.active
				&& player.HeldItem.type == ModContent.ItemType<StringLinkerItem>()) {
				PairWithLinker(player, key);
				return true;
			}
			// Offensive traps have no right-click function at all.
			if (Kind == TrapKind.WildSnare || Kind == TrapKind.Bearing)
				return false;
			// Defensive traps (Sentry/Link/Tripwire) open their config panels.
			if (player == null || !player.active)
				return false;
			if (!TrapSystem.Traps.ContainsKey(key))
				TrapSystem.Traps[key] = TrapState.DefaultFor(Kind);
			TrapUI.OpenFor(Kind, key.X, key.Y);
			return true;
		}

		private static void PairWithLinker(Player player, Point here)
		{
			Point first = StringLinkerItem.FirstEndpoint;
			if (first.X == -9999) {
				StringLinkerItem.FirstEndpoint = here;
				if (Main.netMode != NetmodeID.Server)
					Main.NewText($"Linker: first endpoint set ({here.X},{here.Y}). Right-click the second endpoint.", 150, 220, 255);
				Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick, player.Center);
				return;
			}
			if (first == here) {
				StringLinkerItem.FirstEndpoint = new Point(-9999, -9999);
				if (Main.netMode != NetmodeID.Server)
					Main.NewText("Linker: cannot link an endpoint to itself.", 255, 150, 150);
				return;
			}
			Point a = first;
			StringLinkerItem.FirstEndpoint = new Point(-9999, -9999);
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				if (!TrapActions.CanPair(a, here, out string preErr)) {
					Main.NewText("Linker: " + preErr, 255, 150, 150);
					return;
				}
				TrapNet.SendLinkPair(a, here);
				Main.NewText($"Linker: pairing ({a.X},{a.Y}) <-> ({here.X},{here.Y})...", 150, 220, 255);
			}
			else if (!TrapActions.TryPair(a, here, out string err)) {
				if (Main.netMode != NetmodeID.Server)
					Main.NewText("Linker: " + err, 255, 150, 150);
			}
			else if (Main.netMode != NetmodeID.Server) {
				Main.NewText($"Linked ({a.X},{a.Y}) <-> ({here.X},{here.Y}). Wire it to activate.", 150, 255, 150);
			}
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick, player.Center);
		}
	}

	public class WildstringSnareTile : TrapTileBase
	{
		protected override TrapKind Kind => TrapKind.WildSnare;
		protected override Color MapColor => new Color(120, 220, 120);
		protected override int ItemType => ModContent.ItemType<WildstringSnareItem>();
		public override string Texture => "BiltonsYoyoMod/Traps/WildstringSnarePlaced";
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			r = 0.15f; g = 0.35f; b = 0.15f;
		}
		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			// Draw link-free idle shimmer: small pulsing ring handled by dust in NearbyEffects.
		}
		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (!closer || Main.dedServ) return;
			if (Main.rand.NextBool(40)) {
				Vector2 pos = new Vector2(i * 16f, j * 16f);
				Dust d = Dust.NewDustDirect(pos, 16, 16, DustID.Grass, 0f, -0.5f, 100, default, 0.8f);
				d.noGravity = true;
			}
		}
	}

	public class BearingTrapTile : TrapTileBase
	{
		protected override TrapKind Kind => TrapKind.Bearing;
		protected override Color MapColor => new Color(200, 160, 100);
		protected override int ItemType => ModContent.ItemType<BearingTrapItem>();
		public override string Texture => "BiltonsYoyoMod/Traps/BearingTrapPlaced";
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			r = 0.35f; g = 0.25f; b = 0.12f;
		}
	}

	public class CounterweightSentryTile : TrapTileBase
	{
		protected override TrapKind Kind => TrapKind.Sentry;
		protected override Color MapColor => new Color(220, 190, 90);
		protected override int ItemType => ModContent.ItemType<CounterweightSentryItem>();
		// Base sprite is the tile texture (static, never rotates).
		public override string Texture => "BiltonsYoyoMod/Traps/CounterweightTurretBase";

		private static ReLogic.Content.Asset<Texture2D> BaseTex;
		private static ReLogic.Content.Asset<Texture2D> HeadTex;

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			r = 0.40f; g = 0.33f; b = 0.10f;
		}

		// Head rotates around its mount to face the tracked target; base stays fixed.
		// Vanilla tile drawing is suppressed (50px art on a 1x1 frame would clip).
		// Both parts draw grounded: base bottom-center on the tile bottom-center,
		// head pivots at the mount (base top-center).
		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
		{
			return false;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			if (Main.dedServ)
				return;
			Point key = new Point(i, j);
			float aim = 0f;
			TrapState headState = null;
			if (TrapSystem.Traps.TryGetValue(key, out var s)) { aim = s.AimAngle; headState = s; }
			// Per-frame retarget on the client so the head sweeps smoothly
			// (server AimAngle only refreshes 5x/sec). Uses the same mount
			// as the server sim so aim never looks off-centered.
			Vector2 pivotWorld = TrapSystem.SentryMountWorld(key, headState);
			if (headState != null && !Main.dedServ) {
				int rangePx = (TrapSystem.EffectiveRangeTiles(key, headState) + 6) * 16;
				float best = float.MaxValue;
				for (int k = 0; k < Main.maxNPCs; k++) {
					NPC n = Main.npc[k];
					if (!TrapSystem.SentryEligible(headState, n))
						continue;
					if (!Collision.CanHitLine(pivotWorld, 0, 0, n.Center, 0, 0))
						continue;
					float d = Vector2.DistanceSquared(n.Center, pivotWorld);
					if (d < best && d <= rangePx * rangePx) { best = d; aim = (n.Center - pivotWorld).ToRotation(); }
				}
				// PvP preview: nearest eligible player too (server picks per
				// priority, so Strongest may differ by a frame — cosmetic).
				for (int k = 0; k < Main.maxPlayers; k++) {
					Player pl = Main.player[k];
					if (!TrapSystem.SentryEligiblePlayer(headState, pl))
						continue;
					if (!Collision.CanHitLine(pivotWorld, 0, 0, pl.Center, 0, 0))
						continue;
					float d = Vector2.DistanceSquared(pl.Center, pivotWorld);
					if (d < best && d <= rangePx * rangePx) { best = d; aim = (pl.Center - pivotWorld).ToRotation(); }
				}
			}
			// Normalize so >180 deg wraps instead of spinning the long way.
			while (aim > MathHelper.Pi) aim -= MathHelper.TwoPi;
			while (aim < -MathHelper.Pi) aim += MathHelper.TwoPi;
			Vector2 foot = new Vector2(i * 16f + 8f, j * 16f + 16f);
			Vector2 footScreen = ToScreen(foot);
			try {
				BaseTex ??= ModContent.Request<Texture2D>("BiltonsYoyoMod/Traps/CounterweightTurretBase");
				HeadTex ??= ModContent.Request<Texture2D>("BiltonsYoyoMod/Traps/CounterweightTurretHead");
				Texture2D baseTex = BaseTex.Value;
				Texture2D headTex = HeadTex.Value;
				spriteBatch.Draw(baseTex, footScreen, null, Color.White, 0f, new Vector2(baseTex.Width / 2f, baseTex.Height), 1f, SpriteEffects.None, 0f);
				// Single centered pivot: origin = texture center = measured visual
				// center (25,11), mount = socket from SentryMountWorld (shared
				// with sim so aim never orbits). Spins in place.
				Vector2 mountWorld = TrapSystem.SentryMountWorld(key, headState);
				Vector2 mount = ToScreen(mountWorld);
				if (!Main.dedServ)
					Lighting.AddLight(mountWorld, 0.35f, 0.28f, 0.10f);
				// Head art is drawn upside-down, so it is ALWAYS mirrored
				// vertically (texture-space flip preserves the west-facing
				// barrel, rotation then aims it at the target).
				float rot = aim + MathHelper.Pi;
				SpriteEffects fx = SpriteEffects.FlipVertically;
				Vector2 origin = new Vector2(headTex.Width / 2f, headTex.Height / 2f);
				// Mount-point marker at the exact tile center: proves placement at a glance.
				spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, footScreen - new Vector2(2f, 10f), new Rectangle(0, 0, 1, 1), new Color(255, 255, 255, 160), 0f, Vector2.Zero, new Vector2(4, 4), SpriteEffects.None, 0f);
				spriteBatch.Draw(headTex, mount, null, Color.White, rot, origin, 1f, fx, 0f);
			}
			catch { }
		}
	}
	public class StringLinkTile : TrapTileBase
	{
		protected override TrapKind Kind => TrapKind.Link;
		protected override Color MapColor => new Color(140, 180, 255);
		protected override int ItemType => ModContent.ItemType<StringLinkItem>();
		public override string Texture => "BiltonsYoyoMod/Traps/StringLinkPlaced";

		public override void HitWire(int i, int j)
		{
			Point key = new Point(i, j);
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				TrapNet.SendLinkWire(key);
				return;
			}
			TrapActions.ApplyLinkWire(key);
		}

		// Vanilla endpoint drawing stays on; the rope itself draws in PostDraw.
		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
		{
			return true;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			if (Main.dedServ)
				return;
			Point key = new Point(i, j);
			if (!TrapSystem.Traps.TryGetValue(key, out var s))
				return;
			if (!s.Linked)
				return;
			// Only draw from lexicographically smaller endpoint to avoid double-draw.
			if (key.X > s.LinkX || (key.X == s.LinkX && key.Y > s.LinkY))
				return;
			Point other = s.LinkPoint;
			if (!WorldGen.InWorld(other.X, other.Y))
				return;
			if (!TrapSystem.Traps.TryGetValue(other, out var os))
				return;
			// Wildstring-style verlet rope (both ends pinned). Steps client-side
			// every frame; server steps the same sim for damage in TickLink.
			Vector2[] pts;
			try { pts = LinkRope.StepClient(key, other, s.Bend); }
			catch { return; }
			if (pts == null || pts.Length < 2)
				return;
			Texture2D px = Terraria.GameContent.TextureAssets.MagicPixel.Value;
			Color idle = new Color(180, 190, 200, 160);
			Color active = TrapSystem.StringColor(s.StringVariant);
			// Stress heat readout: rope blushes red toward snap. Gated by
			// client config (presentation only, zero gameplay effect).
			// Shimmer covers the whole iridescent package (fringe, twin,
			// dispersion, film) under one toggle for low-end machines.
			bool redWarn = true;
			bool shimmer = true;
			try {
				var vis = ModContent.GetInstance<Configuration.YoyoConfig>().TrapVisuals;
				redWarn = vis.StressRedWarning;
				shimmer = vis.RopeShimmer;
			}
			catch { }
			float stressFrac = System.Math.Clamp(s.StressPct() / 100f, 0f, 1f);
			Color col = s.WiredActive ? (redWarn ? Color.Lerp(active, Color.Red, stressFrac) : active) : idle;
			// Inactive barriers show only endpoint nodes; the dangerous string
			// exists solely while wired active — except Alarm pairs, which
			// always show a faint dashed trip-line (the sensor must be seen).
			if (!s.WiredActive) {
				Color dim = new Color(120, 130, 150, 200);
				DrawRing(spriteBatch, px, ToScreen(pts[0]), 9f, dim, 1.5f, 0f);
				DrawRing(spriteBatch, px, ToScreen(pts[pts.Length - 1]), 9f, dim, 1.5f, 0f);
				// Fresh snap halves dangle here for 5s (fading), then gone.
				// Cut rope keeps its material: halves draw in the string's
				// own color (never generic white) — only frayed tips pale,
				// like real parted fibers separating. Fresh snaps also run
				// red-hot, cooling ~3x faster than the fade itself.
				if (LinkRope.TryGetSnapHalves(key, other, TrapSystem.TileCenter(key), TrapSystem.TileCenter(other), out var halfA, out var halfB, out float halfFade, out float halfHeat)) {
					Color stringCol = TrapSystem.StringColor(System.Math.Clamp(System.Math.Max(s.StringVariant, os.StringVariant), 0, 7));
					for (int h = 0; h < 2; h++) {
						Vector2[] hp = h == 0 ? halfA : halfB;
						if (hp == null || hp.Length < 2)
							continue;
						for (int k = 0; k < hp.Length - 1; k++) {
							float tipF = h == 0 ? k / (float)(hp.Length - 1) : 1f - k / (float)(hp.Length - 1);
							Color seg = Color.Lerp(stringCol, new Color(235, 235, 230), tipF * tipF * 0.65f);
							if (redWarn && halfHeat > 0f)
								seg = Color.Lerp(seg, Color.Red, halfHeat * 0.55f);
							seg *= halfFade;
							DrawLine(spriteBatch, px, ToScreen(hp[k]), ToScreen(hp[k + 1]), seg, 2f);
						}
					}
				}
				if (s.Behavior == 3 || os.Behavior == 3 || s.Behavior == 4 || os.Behavior == 4) {
					Color dash = new Color(150, 180, 220, 70);
					for (int k = 0; k < pts.Length - 1; k += 3)
						DrawLine(spriteBatch, px, ToScreen(pts[k]), ToScreen(pts[k + 1]), dash, 1.6f);
				}
				return;
			}
			int treat = System.Math.Clamp(s.LinkTreat, 0, 2);
			int tensW = System.Math.Clamp(System.Math.Max(s.TensionWeave, os.TensionWeave), 0, 3);
			// Tension Braid reads on the rope: thicker line per level.
			// Live strain swells it further (visual echo of the stress bar).
			float width = 3f + 0.5f * tensW + 1.2f * stressFrac + 0.6f * (float)System.Math.Sin(Main.GameUpdateCount * 0.15f);
			// Photoelastic fringe: like stressed plastic under polarized
			// light (or thin-film iridescence), the rope flows hue bands as
			// load climbs. Gated by the red-warning toggle with it.
			float fringe = (redWarn && shimmer) ? System.Math.Max(0f, (stressFrac - 0.35f) / 0.65f) : 0f;
			// Birefringence pass: stressed transparent matter splits light
			// in two (ordinary/extraordinary rays) — a faint offset twin of
			// the rope, only when hot, gated with the fringe toggle.
			bool twin = redWarn && shimmer && stressFrac > 0.6f;
			for (int k = 0; k < pts.Length - 1; k++) {
				Vector2 a = ToScreen(pts[k]);
				Vector2 b = ToScreen(pts[k + 1]);
				Color segCol = RopeLight((pts[k] + pts[k + 1]) * 0.5f, col);
				if (fringe > 0f) {
					// Chromatic dispersion: neighboring bands crawl at
					// different speeds, so hues separate along the rope.
					int div = (k % 2 == 0) ? 14 : 9;
					int flow = (int)((Main.GameUpdateCount / (ulong)div + (ulong)(k / 2)) % (ulong)IriFringe.Length);
					// Thin-film phase cancellation: drifting dark nodes dim
					// the shimmer where reflected waves cancel out.
					float ph = k * 1.1f - (float)(Main.GameUpdateCount % 3600) * 0.05f;
					float film = 0.55f + 0.45f * (float)System.Math.Pow(System.Math.Sin(ph), 2.0);
					segCol = Color.Lerp(segCol, IriFringe[flow], fringe * film * 0.6f);
				}
				DrawLine(spriteBatch, px, a, b, segCol, width);
				if (twin) {
					Vector2 dd = b - a;
					float dl = dd.Length();
					if (dl > 0.5f) {
						Vector2 nrm = new Vector2(-dd.Y / dl, dd.X / dl);
						Color twinCol = new Color(255, 250, 235, (int)(120 * fringe));
						DrawLine(spriteBatch, px, a + nrm * 1.5f, b + nrm * 1.5f, twinCol, 1.2f);
					}
				}
			}
			// Snap shockwave: expanding fading ring from the midpoint for
			// ~30 ticks after a snap (the whiplash made visible). Draw-only.
			int snapAge = TrapSystem.SnapFxAge(key, other);
			if (snapAge >= 0) {
				float fade = 1f - snapAge / 30f;
				Vector2 mid = (pts[0] + pts[pts.Length - 1]) * 0.5f;
				float rad = 10f + snapAge * 8f;
				Color ring = new Color(255, 230, 170, (int)(220 * fade));
				const int segs = 20;
				for (int q = 0; q < segs; q++) {
					float a0 = q / (float)segs * MathHelper.TwoPi;
					float a1 = (q + 1) / (float)segs * MathHelper.TwoPi;
					DrawLine(spriteBatch, px, ToScreen(mid + a0.ToRotationVector2() * rad), ToScreen(mid + a1.ToRotationVector2() * rad), ring, 2.5f);
				}
			}
			if (treat == 1) {
				// Barbed Wrap: red barb ticks along the rope (damage you can see).
				Color barb = new Color(200, 40, 50, 220);
				for (int k = 0; k < pts.Length - 1; k++) {
					Vector2 a = ToScreen(pts[k]);
					Vector2 b = ToScreen(pts[k + 1]);
					Vector2 dd = b - a;
					if (dd.Length() < 10f)
						continue;
					Vector2 dir = dd.SafeNormalize(Vector2.UnitX);
					Vector2 norm = new Vector2(-dir.Y, dir.X);
					Vector2 mid = (a + b) * 0.5f;
					spriteBatch.Draw(px, mid, new Rectangle(0, 0, 1, 1), barb, (dir + norm * 0.7f).ToRotation(), new Vector2(0, 0.5f), new Vector2(6f, 1.5f), SpriteEffects.None, 0f);
					spriteBatch.Draw(px, mid, new Rectangle(0, 0, 1, 1), barb, (dir - norm * 0.7f).ToRotation(), new Vector2(0, 0.5f), new Vector2(6f, 1.5f), SpriteEffects.None, 0f);
				}
			}
			else if (treat == 2) {
				// Tangled Wrap: pale second strand winding around the rope.
				Color strand = new Color(225, 230, 235, 180);
				for (int k = 0; k < pts.Length - 1; k++) {
					Vector2 a = ToScreen(pts[k]);
					Vector2 b = ToScreen(pts[k + 1]);
					Vector2 dd = b - a;
					float dlen = dd.Length();
					if (dlen < 8f)
						continue;
					Vector2 dir = dd / dlen;
					Vector2 norm = new Vector2(-dir.Y, dir.X);
					int segs = System.Math.Max(1, (int)(dlen / 8f));
					for (int q = 0; q < segs; q++) {
						float f0 = q / (float)segs;
						float f1 = (q + 1) / (float)segs;
						Vector2 p0 = a + dd * f0 + norm * (float)System.Math.Sin((k + f0) * 1.7f) * 3f;
						Vector2 p1 = a + dd * f1 + norm * (float)System.Math.Sin((k + f1) * 1.7f) * 3f;
						Vector2 sd = p1 - p0;
						if (sd.Length() > 0.5f)
						spriteBatch.Draw(px, p0, new Rectangle(0, 0, 1, 1), strand, sd.ToRotation(), new Vector2(0, 0.5f), new Vector2(sd.Length(), 1.2f), SpriteEffects.None, 0f);
				}
				}
			}
			// Snap warning past 80% stress: smoke + strained creak so the break
			// never feels random (client-side presentation only).
			if (s.StressPct() > 80 && Main.rand.NextBool(20)) {
				Vector2 rp = pts[Main.rand.Next(pts.Length)];
				Dust dd = Dust.NewDustDirect(rp - new Vector2(2, 2), 4, 4, DustID.Smoke, 0f, -0.5f, 100, default, 1f);
				dd.noGravity = true;
			}
			if (s.StressPct() > 80 && Main.GameUpdateCount % 180 == 0)
				Terraria.Audio.SoundEngine.PlaySound(SoundID.Dig, (pts[0] + pts[pts.Length - 1]) * 0.5f);
			// Endpoint rings: hollow circles pinned EXACTLY to tile centers
			// (never drift with rope swing), rotating dashes while live.
			// Both ends read as hardware rings around their blocks, drawn
			// in PostDraw so always in front. Longer spans get slightly
			// bigger hardware; live stress swells them a touch more.
			float spanTiles = pts.Length > 1 ? Vector2.Distance(pts[0], pts[pts.Length - 1]) / 16f : 0f;
			float ringR = 10f + System.Math.Min(3f, spanTiles / 12f) + 2f * stressFrac;
			float ringPhase = Main.GameUpdateCount * 0.05f;
			DrawRing(spriteBatch, px, ToScreen(pts[0]), ringR, col, 2f, ringPhase);
			DrawRing(spriteBatch, px, ToScreen(pts[pts.Length - 1]), ringR, col, 2f, -ringPhase);
			if (Main.rand.NextBool(8)) {
				Vector2 rp = pts[Main.rand.Next(pts.Length)];
				Dust d = Dust.NewDustDirect(rp - new Vector2(2, 2), 4, 4, DustID.GemTopaz, 0f, 0f, 120, default, 1f);
				d.noGravity = true;
			}
		}
		// Iridescent fringe palette for the photoelastic stress readout.
		private static readonly Color[] IriFringe = new[] {
			new Color(120, 230, 255), new Color(255, 130, 220),
			new Color(170, 255, 150), new Color(255, 220, 130),
		};

		// Hollow dashed ring (endpoint hardware, shockwaves share the idiom).
		private static void DrawRing(SpriteBatch sb, Texture2D px, Vector2 center, float radius, Color color, float width, float phase)
		{
			const int segs = 24;
			for (int q = 0; q < segs; q++) {
				if ((q % 6) >= 4)
					continue;
				float a0 = q / (float)segs * MathHelper.TwoPi + phase;
				float a1 = (q + 1) / (float)segs * MathHelper.TwoPi + phase;
				DrawLine(sb, px, center + a0.ToRotationVector2() * radius, center + a1.ToRotationVector2() * radius, color, width);
			}
		}

		private static void DrawLine(SpriteBatch sb, Texture2D px, Vector2 a, Vector2 b, Color col, float width)
		{
			Vector2 diff = b - a;
			float len = diff.Length();
			if (len < 1f) return;
			float rot = diff.ToRotation();
			sb.Draw(px, a, new Rectangle(0, 0, 1, 1), col, rot, new Vector2(0, 0.5f), new Vector2(len, width), SpriteEffects.None, 0f);

		}
		// Same lighting treatment as the real wildstrings: sample tile light at
		// the segment midpoint so ropes sit in the scene instead of glowing flat.
		private static Color RopeLight(Vector2 worldMid, Color baseCol)
		{
			int tx = (int)(worldMid.X / 16f);
			int ty = (int)(worldMid.Y / 16f);
			if (tx < 0 || ty < 0 || tx >= Main.maxTilesX || ty >= Main.maxTilesY)
				return baseCol;
			Color lit = Lighting.GetColor(tx, ty);
			Color shaded = new Color(baseCol.R * lit.R / 255, baseCol.G * lit.G / 255, baseCol.B * lit.B / 255, 255);
			return Color.Lerp(baseCol, shaded, 0.5f);
		}
	}

	public class YoyoTripwireTile : TrapTileBase
	{
		protected override TrapKind Kind => TrapKind.Tripwire;
		protected override Color MapColor => new Color(200, 120, 200);
		protected override int ItemType => ModContent.ItemType<YoyoTripwireItem>();
		public override string Texture => "BiltonsYoyoMod/Traps/YoyoTripwirePlaced";
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			r = 0.35f; g = 0.12f; b = 0.35f;
		}
	}
}
