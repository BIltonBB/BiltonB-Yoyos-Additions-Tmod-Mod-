using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Tracks per-player Resonator coverage. Recalculated each tick;
	// never permanent. Effect itself is applied in ResonatorGlobalNPC
	// (weaken on yoyo hit) and read here for UI/fx.
	public class ResonatorPlayer : ModPlayer
	{
		public bool InResonator;
		public float WeakenPct;
		public int WeakenTicks;
		public float ResonatorRadius;
		public float FeedbackPct;
		public Point ResoTile;
		public bool ShowRing = true;
		private bool wasIn;

		public override void ResetEffects()
		{
			InResonator = false;
			WeakenPct = 0f;
			WeakenTicks = 0;
			ResonatorRadius = 0f;
			FeedbackPct = 0f;
		}

		public override void PostUpdateEquips()
		{
			if (ResonatorSystem.TryGetCoveringResonator(Player, out var tile, out var data)) {
				InResonator = true;
				WeakenPct = ResonatorSystem.EffWeakenPct(data, StationNetwork.ResonatorBonus(tile));
				WeakenTicks = ResonatorSystem.WeakenTicks(System.Math.Min(data.Duration + StationNetwork.ResonatorBonus(tile), ResonatorSystem.MaxDuration));
				ResonatorRadius = ResonatorSystem.EffRadiusPx(data);
				FeedbackPct = ResonatorSystem.FeedbackPct(data.Feedback);
				ResoTile = tile;
				// Harmonic Hum combo (Resonator + Ward share one mesh).
				if (StationNetwork.MeshHasWard(tile))
					Player.AddBuff(ModContent.BuffType<global::BiltonsYoyoMod.Content.Buffs.HarmonicHumBuff>(), 300);
			}
		}

		public override void PostUpdate()
		{
			if (Main.myPlayer != Player.whoAmI || Main.dedServ) {
				wasIn = InResonator;
				return;
			}
			// Crossing the edge announces itself — no more guessing whether
			// the field is live.
			if (InResonator && !wasIn)
				CombatText.NewText(Player.getRect(), new Color(255, 150, 220), "Dissonant Hum live!");
			else if (!InResonator && wasIn)
				CombatText.NewText(Player.getRect(), Color.Gray, "Hum faded.");
			wasIn = InResonator;
			// Block rings stay up whenever a block is near, covered or
			// not: the field is a place, not a player buff.
			if (ShowRing && Main.GameUpdateCount % 4 == 0) {
				foreach (var kv in ResonatorSystem.Tiles) {
					if (!ResonatorSystem.IsTileStillResonator(kv.Key))
						continue;
					Vector2 center = ResonatorSystem.ResonatorCenter(kv.Key);
					if (Vector2.DistanceSquared(Player.Center, center) > 2500f * 2500f)
						continue;
					float rr = ResonatorSystem.EffRadiusPx(kv.Value);
					PlaceableUI.DustRing(center, rr, DustID.PinkTorch, PlaceableUI.RingCount(rr), 1.3f);
				}
			}
			if (!InResonator)
				return;
			// Covered block flares so the source of the field is obvious.
			Lighting.AddLight(ResonatorSystem.ResonatorCenter(ResoTile), 0.5f, 0.2f, 0.4f);
			// Attunement shimmer ON the covered player: hard to miss.
			if (Main.GameUpdateCount % 3 == 0) {
				for (int i = 0; i < 3; i++) {
					Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.PinkTorch, 0f, -1.4f, 120, default, 1.1f);
					d.noGravity = true;
				}
				Lighting.AddLight(Player.Center, 0.45f, 0.15f, 0.35f);
			}
		}

		public override void SaveData(TagCompound tag)
		{
			tag["resoRing"] = ShowRing;
		}

		public override void LoadData(TagCompound tag)
		{
			ShowRing = !tag.ContainsKey("resoRing") || tag.GetBool("resoRing");
		}
	}

	// Yoyo-only weaken application + weakened-enemy damage reduction.
	public class ResonatorGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
		{
			return projectile.aiStyle == ProjAIStyleID.Yoyo;
		}

		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			string ns = projectile.ModProjectile?.GetType().Namespace ?? string.Empty;
			if (ns.Contains("TownNPC") || projectile.hostile || !projectile.friendly)
				return;
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active)
				return;
			var reso = owner.GetModPlayer<ResonatorPlayer>();
			if (reso == null || !reso.InResonator || reso.FeedbackPct <= 0f)
				return;
			// Feedback: bonus yoyo damage, but ONLY against foes already
			// humming with weaken. No weaken, no bonus.
			if (target.GetGlobalNPC<ResonatorGlobalNPC>().IsWeakened)
				modifiers.SourceDamage *= 1f + reso.FeedbackPct;
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			// NPC-cast yoyos must not apply the player-side field.
			string ns = projectile.ModProjectile?.GetType().Namespace ?? string.Empty;
			if (ns.Contains("TownNPC") || projectile.hostile || !projectile.friendly)
				return;
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active)
				return;
			// Attacker must be within a Resonator radius; other weapon
			// classes never reach this hook (yoyo-only via AppliesToEntity).
			var reso = owner.GetModPlayer<ResonatorPlayer>();
			if (reso == null || !reso.InResonator)
				return;
			if (Main.myPlayer != projectile.owner)
				return;
			var g = target.GetGlobalNPC<ResonatorGlobalNPC>();
			g.ApplyWeaken(reso.WeakenPct, reso.WeakenTicks);
			// Superposition capstone: full-mesh resonators slow too.
			if (StationNetwork.IsR30(reso.ResoTile))
				target.AddBuff(BuffID.Slow, reso.WeakenTicks);
			target.netUpdate = true;
			// Violet contact flash so applications read mid-fight.
			for (int i = 0; i < 4; i++) {
				Dust d = Dust.NewDustDirect(projectile.Center - new Microsoft.Xna.Framework.Vector2(4, 4), 8, 8, DustID.PurpleTorch, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 120, default, 0.9f);
				d.noGravity = true;
			}
		}
	}

	public class ResonatorGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		private float weakenPct;
		private int weakenTimer;

		public bool IsWeakened => weakenTimer > 0;

		public void ApplyWeaken(float pct, int ticks)
		{
			// Strongest wins; refresh duration. No stacking.
			if (pct >= weakenPct) {
				weakenPct = pct;
				weakenTimer = ticks;
			}
			else if (weakenTimer < ticks) {
				weakenTimer = ticks;
			}
		}

		public override void PostAI(NPC npc)
		{
			if (weakenTimer > 0 && --weakenTimer <= 0)
				weakenPct = 0f;
			// Violet wobble on weakened foes so the debuff reads in combat.
			if (weakenTimer > 0 && !Main.dedServ && Main.rand.NextBool(30)) {
				Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.MagicMirror, 0f, -1f, 120, default, 0.9f);
				d.noGravity = true;
				d.velocity *= 0.4f;
			}
		}

		public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
		{
			if (weakenTimer > 0 && weakenPct > 0f)
				modifiers.FinalDamage *= 1f - weakenPct;
		}
	}
}
