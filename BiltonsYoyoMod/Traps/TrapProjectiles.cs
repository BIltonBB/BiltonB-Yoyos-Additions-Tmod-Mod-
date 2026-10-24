using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace BiltonsYoyoMod.Traps
{
	// Wildstring Snare deployment: hostile string anchored to the trap.
	// Damages players who touch the line. Uses progression variant color.
	public class SnareStringProj : ModProjectile
	{
		public override string Texture => "Terraria/Images/MagicPixel";

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 300;
		}

		public override void OnSpawn(IEntitySource source)
		{
			Projectile.localAI[0] = Projectile.Center.X;
			Projectile.localAI[1] = Projectile.Center.Y;
			SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);
		}

		public override void AI()
		{
			SnareRope.Cleanup();
			Vector2 anchor = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
			if (anchor == Vector2.Zero) {
				anchor = Projectile.Center;
				Projectile.localAI[0] = anchor.X;
				Projectile.localAI[1] = anchor.Y;
			}
			Projectile.Center = anchor;
			int variant = (int)Projectile.ai[0];
			float dir = Projectile.ai[1] < 0f ? -1f : 1f;
			var st = SnareRope.GetOrCreate(Projectile.whoAmI, anchor, variant, dir);
			SnareRope.Step(st, anchor);
			Projectile.rotation += 0.05f;
			if (!Main.dedServ && Main.rand.NextBool(12)) {
				Color c = TrapSystem.StringColor(variant);
				Vector2 tip = st.Pos[SnareRope.Points - 1];
				Dust d = Dust.NewDustDirect(tip - new Vector2(4, 4), 8, 8, DustID.Grass, 0f, 0f, 100, c, 1f);
				d.noGravity = true;
			}
		}

		// Anchor lives in localAI (not auto-synced): replicate it + spin explicitly.
		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(Projectile.ai[0]);
			writer.Write(Projectile.ai[1]);
			writer.Write(Projectile.ai[2]);
			writer.Write(Projectile.localAI[0]);
			writer.Write(Projectile.localAI[1]);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			Projectile.ai[0] = reader.ReadSingle();
			Projectile.ai[1] = reader.ReadSingle();
			Projectile.ai[2] = reader.ReadSingle();
			Projectile.localAI[0] = reader.ReadSingle();
			Projectile.localAI[1] = reader.ReadSingle();
		}


		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Vector2 anchor = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
			if (anchor == Vector2.Zero)
				return false;
			int variant = (int)Projectile.ai[0];
			float dir = Projectile.ai[1] < 0f ? -1f : 1f;
			var st = SnareRope.GetOrCreate(Projectile.whoAmI, anchor, variant, dir);
			float point = 0f;
			if (TrapSystem.RopeHits(st.Pos, targetHitbox.TopLeft(), targetHitbox.Size(), 12f, ref point))
				return true;
				try {
					if (TrapSystem.RopeHits(st.Prev, targetHitbox.TopLeft(), targetHitbox.Size(), 12f, ref point))
						return true;
				}
				catch { }
				return false;
		}

		public override void OnHitPlayer(Player target, Player.HurtInfo info)
		{
			target.AddBuff(BuffID.Slow, 120, false);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 anchor = new Vector2(Projectile.localAI[0], Projectile.localAI[1]);
			if (anchor == Vector2.Zero)
				return false;
			int variant = (int)Projectile.ai[0];
			float dir = Projectile.ai[1] < 0f ? -1f : 1f;
			var st = SnareRope.GetOrCreate(Projectile.whoAmI, anchor, variant, dir);
			Texture2D px = TextureAssets.MagicPixel.Value;
			Color col = TrapSystem.StringColor(variant);
			Vector2[] pts = st.Pos;
			for (int k = 0; k < pts.Length - 1; k++) {
				Color seg = SnareLight((pts[k] + pts[k + 1]) * 0.5f, col);
				Vector2 a = pts[k] - Main.screenPosition;
				Vector2 b = pts[k + 1] - Main.screenPosition;
				Vector2 diff = b - a;
				float len = diff.Length();
				if (len < 1f)
					continue;
				Main.spriteBatch.Draw(px, a, new Rectangle(0, 0, 1, 1), seg, diff.ToRotation(), new Vector2(0, 0.5f), new Vector2(len, 2.5f), SpriteEffects.None, 0f);
			}
			Vector2 tipScreen = pts[pts.Length - 1] - Main.screenPosition;
			Main.spriteBatch.Draw(px, tipScreen - new Vector2(2, 2), new Rectangle(0, 0, 1, 1), col, 0f, Vector2.Zero, new Vector2(4, 4), SpriteEffects.None, 0f);
			return false;
		}

		private static Color SnareLight(Vector2 worldMid, Color baseCol)
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

	// Bearing Trap shot: hostile, gravity, consumed on impact, never returns.
	public class BearingTrapShot : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/LooseBearingsAW";

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 300;
			Projectile.aiStyle = 0;
		}

		public override void OnSpawn(IEntitySource source)
		{
			SoundEngine.PlaySound(SoundID.Item11, Projectile.Center);
			if (!Main.dedServ) { for (int d = 0; d < 5; d++) { Dust dd = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, -1f, 100, default, 0.9f); dd.noGravity = true; } }
		}

		public override void AI()
		{
			Projectile.velocity.Y += 0.30f;
			if (Projectile.velocity.Y > 14f)
				Projectile.velocity.Y = 14f;
			Projectile.rotation += Projectile.velocity.X * 0.10f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			Projectile.Kill();
			return false;
		}

		public override void OnHitPlayer(Player target, Player.HurtInfo info)
		{
			Projectile.Kill();
		}
	}

	// Counterweight Sentry attack: friendly, outward then return to trap anchor.
	public class SentryCounterweightProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Content/Projectiles/GoldenCounterweightProj";

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.hostile = true; // PvP mode: also touches players (gated in CanHitPlayer)
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.timeLeft = 600;
			Projectile.extraUpdates = 1; // halve per-step motion (anti-tunnel)
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
			Projectile.DamageType = DamageClass.Melee;

		}

		public override void OnSpawn(IEntitySource source)
		{
			SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
		}
		public override void AI()
		{
            Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
            if (!TrapSystem.IsAnyTrapTile(tile)) { Projectile.Kill(); return; }
			TrapSystem.Traps.TryGetValue(tile, out var ats);
			Vector2 anchor = TrapSystem.SentryMountWorld(tile, ats);
			Projectile.localAI[1]++;
			// phase in localAI[0]: 0 outward, 1 returning
			if (Projectile.localAI[0] == 0) {
				// Gentle homing toward the server-picked target (ai[2]: NPC
				// whoAmI, 1000+player whoAmI, or -1): flyers cannot sidestep it.
				int tgt = (int)Projectile.ai[2];
				Vector2 want = Vector2.Zero;
				bool hasT = false;
				if (tgt >= 0 && tgt < Main.maxNPCs) {
					NPC tn = Main.npc[tgt];
					if (tn != null && tn.active && tn.life > 0) { want = tn.Center; hasT = true; }
				}
				else if (tgt >= 1000 && tgt < 1000 + Main.maxPlayers) {
					Player tp = Main.player[tgt - 1000];
					if (tp != null && tp.active && !tp.dead) { want = tp.Center; hasT = true; }
				}
				float spd = Projectile.velocity.Length();
				if (spd < 1f)
					spd = 11f;
				if (hasT && spd > 0.1f) {
					Vector2 cur = Projectile.velocity.SafeNormalize(Vector2.UnitX);
					Vector2 des = (want - Projectile.Center).SafeNormalize(Vector2.UnitX);
					Projectile.velocity = (cur + des * 0.18f).SafeNormalize(Vector2.UnitX) * spd;
				}
				else {
					Projectile.velocity.Y += 0.08f;
				}
				float rangePx = 720f;
				if (TrapSystem.Traps.TryGetValue(tile, out var hs))
					rangePx = (TrapSystem.EffectiveRangeTiles(tile, hs) + 6) * 16f;
				if (Projectile.localAI[1] > 110 || Vector2.DistanceSquared(Projectile.Center, anchor) > rangePx * rangePx)
					Projectile.localAI[0] = 1;
			}
			else {
				Projectile.tileCollide = false;
				Vector2 to = anchor - Projectile.Center;
				if (to.Length() < 24f) {
					Projectile.Kill();
					return;
				}
				Projectile.velocity = to.SafeNormalize(Vector2.Zero) * 14f;
			}
			// Gyroscope track (+12%/level) and Return behavior (+25%) pay out on
			// the RETURN leg only. One-shot flag in localAI[2], never stacks.
			if (Projectile.localAI[0] == 1 && Projectile.localAI[2] == 0) {
				Projectile.localAI[2] = 1;
				if (TrapSystem.Traps.TryGetValue(tile, out var gs)) {
					float mult = 1f;
					if (gs.Behavior == 1)
						mult *= 1.25f;
					mult *= 1f + 0.12f * System.Math.Clamp(gs.SentryGyro, 0, 3);
					if (mult != 1f) {
						Projectile.damage = (int)(Projectile.damage * mult);
						Projectile.netUpdate = true;
					}
				}
			}
			Projectile.rotation += 0.4f;
			Lighting.AddLight(Projectile.Center, 0.25f, 0.20f, 0.05f);
		}

		// Phase/timer live in localAI (not auto-synced): replicate so MP hit
		// windows (outward vs return) agree on which legs can damage.
		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(Projectile.localAI[0]);
			writer.Write(Projectile.localAI[1]);
			writer.Write(Projectile.localAI[2]);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			Projectile.localAI[0] = reader.ReadSingle();
			Projectile.localAI[1] = reader.ReadSingle();
			try { Projectile.localAI[2] = reader.ReadSingle(); } catch { }
		}

		public override bool? CanDamage()
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
			int behavior = 2;
			if (TrapSystem.Traps.TryGetValue(tile, out var s))
				behavior = s.Behavior;
			bool returning = Projectile.localAI[0] == 1;
			if (behavior == 0) return !returning ? true : false;
			if (behavior == 1) return returning ? true : false;
			return true;
		}

		// PvP mode: hits players only when the trap's Players filter (or ALL)
		// is ON — and never the trap owner (your own sentry spares you).
		// Hit gate (mirrors the TrapUI target filters): OFF categories are
		// never struck, even when they wander into the flight path.
		public override bool? CanHitNPC(NPC target)
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
			if (TrapSystem.Traps.TryGetValue(tile, out var s))
return TrapSystem.SentryEligible(s, target);
			if (target == null || !target.active || target.life <= 0)
return false;
			if (target.townNPC || target.friendly || target.dontTakeDamage)
return false;
			if (target.damage <= 0 || target.lifeMax <= 5)
return false;
			if (Terraria.ID.NPCID.Sets.CountsAsCritter[target.type])
return false;
			return true;
		}

		public override bool CanHitPlayer(Player target)
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return false;
			if (!s.HitPlayers && !s.HitAll)
				return false;
			if (target.whoAmI == Projectile.owner)
				return false;
			return true;
		}

		// Counterweight variety hit effect + Ichor Tracers (unique option).
		// Kills feed the trap's lifetime tally (server-side only).
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
			if (TrapSystem.Traps.TryGetValue(tile, out var s)) {
				var def = TrapSystem.CounterOf(s.CounterType);
				if (def.Debuff >= 0) {
					target.AddBuff(def.Debuff, def.DebuffTicks, false);
					target.netUpdate = true;
				}
				if (s.TracerFire) {
					target.AddBuff(BuffID.Ichor, 300, false);
					target.netUpdate = true;
				}
				if (Main.netMode != NetmodeID.MultiplayerClient && (target.life <= 0 || target.life <= damageDone)) {
					int prev = s.Kills;
					s.Kills++;
					TrapSystem.CheckVeteranMilestone(tile, s, prev);
					TrapSystem.SyncWorld();
				}
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.7f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
			if (oldVelocity.Length() > 10f)
				Projectile.localAI[0] = 1;
			return false;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
			TrapState ts = null;
			TrapSystem.Traps.TryGetValue(tile, out ts);
			Vector2 anchor = TrapSystem.SentryMountWorld(tile, ts);
			Texture2D px = TextureAssets.MagicPixel.Value;
			Vector2 a = anchor - Main.screenPosition;
			Vector2 b = Projectile.Center - Main.screenPosition;
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			float tscale = 1f;
			Color ttint = Color.White;
			if (ts != null) {
				// Per-variety art: mod sprites, or the vanilla dice (Iron).
				var def = TrapSystem.CounterOf(ts.CounterType);
				if (def.VanillaProj >= 0) {
					try {
						tex = TextureAssets.Projectile[def.VanillaProj].Value;
						tscale = def.Scale;
						ttint = def.Tint;
					}
					catch { }
				}
				else {
					try {
						tex = ModContent.Request<Texture2D>(def.Tex).Value;
						tscale = def.Scale;
						ttint = def.Tint;
					}
					catch { }
				}
			}
			Vector2 diff = b - a;
			float len = diff.Length();
			Color tether = new Color(255, 255, 255, 200);
			if (ts != null)
				tether = TrapSystem.SentryStringColor(ts.SentryString);
			tether.A = 200;
			// Tether lives only BEHIND the head: stop at its rim so the line
			// never crosses (or pokes past) the head sprite.
			float headR = tex.Width * tscale * 0.5f;
			Vector2 tip = len > headR + 1f ? b - diff.SafeNormalize(Vector2.Zero) * headR : a;
			Vector2 span = tip - a;
			if (span.Length() > 1f)
				Main.spriteBatch.Draw(px, a, new Rectangle(0, 0, 1, 1), tether, span.ToRotation(), new Vector2(0, 0.5f), new Vector2(span.Length(), 1.6f), SpriteEffects.None, 0f);
			Main.spriteBatch.Draw(tex, b, null, ttint, Projectile.rotation, new Vector2(tex.Width / 2f, tex.Height / 2f), tscale, SpriteEffects.None, 0f);
			return false;
		}
	}

	// Yoyo Tripwire attack: attached returning yoyo anchored to the trap.
	public class TripwireYoyoProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/HeavyYoyoProj";

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.timeLeft = 600;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
			Projectile.DamageType = DamageClass.Melee;
		}
		public override void OnSpawn(IEntitySource source)
		{
			SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
		}

		public override void AI()
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
            if (!TrapSystem.IsAnyTrapTile(tile)) { Projectile.Kill(); return; }
			Vector2 anchor = TrapSystem.TileCenter(tile);
			prevHead[Projectile.whoAmI] = Projectile.Center;
			int maxLen = 170;
			float outSpd = 2.8f;
			float retSpd = 5.5f;
			if (TrapSystem.Traps.TryGetValue(tile, out var s)) {
				maxLen = s.MaxLen;
				outSpd = s.OutSpeed / 10f;
				retSpd = s.ReturnSpeed / 10f;
			}
			float phase = Projectile.localAI[0]; // 0 outward, 1 return
			float traveled = Projectile.localAI[1];
			Vector2 dir = Projectile.ai[2].ToRotationVector2();
			if (phase == 0) {
				Projectile.velocity = dir * outSpd;
				traveled += outSpd;
				Projectile.localAI[1] = traveled;
				if (traveled >= maxLen)
					Projectile.localAI[0] = 1;
			}
			else {
				Vector2 to = anchor - Projectile.Center;
				float d = to.Length();
				if (d < 20f) {
					Projectile.Kill();
					return;
				}
				Projectile.velocity = to.SafeNormalize(Vector2.Zero) * retSpd;
			}
			Projectile.rotation += 0.35f;
			// Ambient string motes (visual only): barbed sheds red, tangled
			// sheds pale wisps along the tether.
			if (!Main.dedServ && Main.rand.NextBool(18) && TrapSystem.Traps.TryGetValue(tile, out var ts2) && ts2.TripString != 0) {
				Vector2 mid = (anchor + Projectile.Center) * 0.5f + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-30f, 30f));
				Dust dd = Dust.NewDustDirect(mid - new Vector2(2, 2), 4, 4,
					ts2.TripString == 1 ? DustID.Blood : DustID.WoodFurniture,
					0f, -0.5f, 120, default, 0.8f);
				dd.noGravity = true;
			}
		}


		// Phase/traveled live in localAI (not auto-synced): replicate so MP
		// clients agree on outward vs returning (UI state + return timing).
		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write(Projectile.localAI[0]);
			writer.Write(Projectile.localAI[1]);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			Projectile.localAI[0] = reader.ReadSingle();
			Projectile.localAI[1] = reader.ReadSingle();
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// Hit a wall: begin return instead of dying.
			Projectile.localAI[0] = 1;
			return false;
		}


		// Damage rule (separate from trigger rules): only hostile NPCs.
		public override bool? CanHitNPC(NPC target)
		{
			if (target == null || !target.active || target.life <= 0)
				return false;
			if (target.townNPC || target.friendly || target.dontTakeDamage)
				return false;
			if (target.damage <= 0 || target.lifeMax <= 5)
				return false;
			if (Terraria.ID.NPCID.Sets.CountsAsCritter[target.type])
				return false;
			return true;
		}

		// Snare Tip (unique option): yoyo hits also restrain with Slow.
		// Glacier heads chill like frostburn (3s). Barbed/Tangled effects live
		// on the STRING (tether touch), mirroring the string accessories.
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
			if (TrapSystem.Traps.TryGetValue(tile, out var s)) {
				int style = System.Math.Clamp(s.TripStyle, 0, 7);
				if (style == 6) {
					target.AddBuff(BuffID.Frostburn, 180, false);
					target.netUpdate = true;
				}
				if (s.SnareTip) {
					target.AddBuff(BuffID.Slow, 120, false);
					target.netUpdate = true;
				}
				if (Main.netMode != NetmodeID.MultiplayerClient && (target.life <= 0 || target.life <= damageDone)) {
					int prev = s.Kills;
					s.Kills++;
					TrapSystem.CheckVeteranMilestone(tile, s, prev);
					TrapSystem.SyncWorld();
				}
			}
		}
		private static readonly Dictionary<int, Vector2> prevHead = new();

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			// Generous head hitbox (inflated) swept along last tick path,
			// so small fast flyers cannot slip between ticks.
			Rectangle fat = projHitbox;
			fat.Inflate(6, 6);
			if (fat.Intersects(targetHitbox))
				return true;
			if (prevHead.TryGetValue(Projectile.whoAmI, out Vector2 ph)) {
				float point = 0f;
				if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), ph, Projectile.Center, projHitbox.Width + 8, ref point))
					return true;
			}
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			prevHead.Remove(Projectile.whoAmI);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Point tile = new Point((int)Projectile.ai[0], (int)Projectile.ai[1]);
			Vector2 anchor = TrapSystem.TileCenter(tile) - Main.screenPosition;
			Vector2 head = Projectile.Center - Main.screenPosition;
			Texture2D px = TextureAssets.MagicPixel.Value;
			Vector2 diff = head - anchor;
			float len = diff.Length();
			var tstyle = TrapSystem.TripStyleOf(0);
			int tstring = 0;
			if (TrapSystem.Traps.TryGetValue(tile, out var tss)) {
				tstyle = TrapSystem.TripStyleOf(tss.TripStyle);
				tstring = System.Math.Clamp(tss.TripString, 0, 24);
			}
			Color scol = TrapSystem.TripStringColor(tstring, tstyle.String);
			if (tstring == 24) scol = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB); // rainbow disco
			scol.A = 200;
			if (len > 1f) {
				if (tstring == 1) {
					// Barbed string: red-black line with barb ticks, like the
					// Barbed Strings accessory look.
					Main.spriteBatch.Draw(px, anchor, new Rectangle(0, 0, 1, 1), scol, diff.ToRotation(), new Vector2(0, 0.5f), new Vector2(len, 2.4f), SpriteEffects.None, 0f);
					Vector2 dir = diff.SafeNormalize(Vector2.UnitX);
					Vector2 norm = new Vector2(-dir.Y, dir.X);
					for (float d = 8f; d < len - 4f; d += 12f) {
						Vector2 bp = anchor + dir * d;
						Main.spriteBatch.Draw(px, bp, new Rectangle(0, 0, 1, 1), new Color(200, 40, 50, 220), (dir + norm * 0.6f).ToRotation(), new Vector2(0, 0.5f), new Vector2(7f, 1.6f), SpriteEffects.None, 0f);
						Main.spriteBatch.Draw(px, bp, new Rectangle(0, 0, 1, 1), new Color(200, 40, 50, 220), (dir - norm * 0.6f).ToRotation(), new Vector2(0, 0.5f), new Vector2(7f, 1.6f), SpriteEffects.None, 0f);
					}
				}
				else if (tstring == 2) {
					// Tangled string: silver double-twist (two sine-offset lines).
					for (int strand = -1; strand <= 1; strand += 2) {
						int segs = System.Math.Max(2, (int)(len / 14f));
						Vector2 dir = diff.SafeNormalize(Vector2.UnitX);
						Vector2 norm = new Vector2(-dir.Y, dir.X);
						for (int k = 1; k <= segs; k++) {
							float t0 = (k - 1) / (float)segs;
							float t1 = k / (float)segs;
							Vector2 p0 = anchor + diff * t0 + norm * (float)System.Math.Sin(t0 * len * 0.25f + Main.GameUpdateCount * 0.1f) * 3f * strand;
							Vector2 p1 = anchor + diff * t1 + norm * (float)System.Math.Sin(t1 * len * 0.25f + Main.GameUpdateCount * 0.1f) * 3f * strand;
							Vector2 sd = p1 - p0;
							if (sd.Length() > 1f)
								Main.spriteBatch.Draw(px, p0, new Rectangle(0, 0, 1, 1), scol, sd.ToRotation(), new Vector2(0, 0.5f), new Vector2(sd.Length(), 1.4f), SpriteEffects.None, 0f);
						}
					}
				}
				else {
					Main.spriteBatch.Draw(px, anchor, new Rectangle(0, 0, 1, 1), scol, diff.ToRotation(), new Vector2(0, 0.5f), new Vector2(len, 1.8f), SpriteEffects.None, 0f);
				}
			}
			// Head: real in-mod yoyo sprite per style (falls back to default).
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			try { tex = ModContent.Request<Texture2D>(tstyle.Tex).Value; } catch { }
			Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
			Main.spriteBatch.Draw(tex, head, null, Color.White, Projectile.rotation, origin, 1f, SpriteEffects.None, 0f);
			return false;
		}
	}
}
