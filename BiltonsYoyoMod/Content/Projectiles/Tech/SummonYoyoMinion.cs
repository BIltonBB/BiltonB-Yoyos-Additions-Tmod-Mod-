using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Summon Tech minion: the yoyo becomes a hovering summon that chases enemies.
	/// Damage 0.75x (at spawn), Summon damage class, 1 minion slot each.
	/// Behaviour: idle orbit near player -&gt; chase nearest within 800 -&gt; contact damage -&gt;
	/// bolt every 2s at 50% -&gt; slow return when no target -&gt; teleport if &gt;2000 away.
	/// Cap: 1 + maxMinions + glove/bag + counterweight + string (2..8).
	/// Despawn: tech OFF, item missing, player dead, or minion slots revoked.
	/// </summary>
	public class SummonYoyoMinion : TechProjBase
	{
		private bool hadTarget;

		public override void SetStaticDefaults()
		{
			Main.projPet[Type] = false;
			ProjectileID.Sets.MinionTargettingFeature[Type] = true;
			ProjectileID.Sets.MinionSacrificable[Type] = true;
			ProjectileID.Sets.CultistIsResistantTo[Type] = true;
		}

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.minion = true;
			Projectile.minionSlots = 1f;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 18000;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
			// ai0 = source item, ai1 = source proj.
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			SourceProjType = (int)Projectile.ai[1];
			global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.ApplyEnhancementToSpawn(Projectile, null, ModId);
		}

		public override void AI()
		{
			Player owner = OwnerPlayer();
			if (owner == null || !owner.active || owner.dead) {
				Projectile.Kill();
				return;
			}
			// Despawn when the player is no longer wielding a yoyo as main weapon.
			if (!YoyoTownShared.IsYoyo(owner.HeldItem)) {
				Projectile.Kill();
				return;
			}
			SourceProjType = SourceProjType == 0 ? (int)Projectile.ai[1] : SourceProjType;

			// Minions linger up to 5 min after tech off/switch: expiry is
			// natural (timeLeft), refreshed only while the tech item is carried.
			var tp = owner.GetModPlayer<global::BiltonsYoyoMod.Content.Tech.YoyoTechPlayer>();
			if (tp != null && tp.ActiveTech == global::BiltonsYoyoMod.Content.Tech.YoyoTechType.Summon
				&& global::BiltonsYoyoMod.Content.Tech.YoyoTechPlayer.HasTechItem(owner, global::BiltonsYoyoMod.Content.Tech.YoyoTechType.Summon)) {
				Projectile.timeLeft = 18000; // 5-minute buffer while attuned.
			}
			owner.GetModPlayer<SummonBuffPlayer>().HasSummon = true;

			deployTicks++;
			Projectile.rotation += 0.35f;

			// Spawn poof so fresh summons pop in instead of blinking.
			if (deployTicks == 1 && Projectile.owner == Main.myPlayer) {
				for (int i = 0; i < 10; i++) {
					Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
						DustID.MagicMirror, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 120, default, 0.9f);
					d.noGravity = true;
				}
				Terraria.Audio.SoundEngine.PlaySound(SoundID.Item44, Projectile.Center);
			}

			// Minion target: vanilla targeting feature first, else nearest.
			NPC target = null;
			if (owner.HasMinionAttackTargetNPC) {
				NPC forced = Main.npc[owner.MinionAttackTargetNPC];
				if (forced.CanBeChasedBy())
					target = forced;
			}
			target ??= global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.FindTarget(Projectile.Center, 800f);

			// Acquire ping: one ring when a new target is picked up.
			if (target != null && !hadTarget && Projectile.owner == Main.myPlayer) {
				for (int i = 0; i < 8; i++) {
					Vector2 ring = new Vector2(30f, 0f).RotatedBy(i / 8f * MathHelper.TwoPi);
					Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8,
						DustID.GoldCoin, ring.X * 0.08f, ring.Y * 0.08f, 120, default, 0.9f);
					d.noGravity = true;
				}
			}
			hadTarget = target != null;

			Vector2 idleSpot = owner.Center + new Vector2(
				System.MathF.Cos(deployTicks * 0.03f + Projectile.whoAmI) * 110f, -70f);

			if (target != null) {
				// Chase with soft arrival; contact is the main damage.
				Vector2 toTarget = target.Center - Projectile.Center;
				float dist = toTarget.Length();
				float speed = dist > 300f ? 13f : 9f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity,
					toTarget.SafeNormalize(Vector2.Zero) * speed, 0.12f);
				Lighting.AddLight(Projectile.Center, 0.5f, 0.6f, 0.4f);
			}
			else {
				// Drift home, teleport if lost.
				float dist = Vector2.Distance(Projectile.Center, owner.Center);
				if (dist > 2000f) {
					Projectile.Center = idleSpot;
					Projectile.velocity = Vector2.Zero;
					Projectile.netUpdate = true;
				}
				else {
					Projectile.velocity = Vector2.Lerp(Projectile.velocity,
						(idleSpot - Projectile.Center).SafeNormalize(Vector2.Zero)
						* System.Math.Min(10f, dist * 0.06f), 0.1f);
				}
				Lighting.AddLight(Projectile.Center, 0.3f, 0.35f, 0.25f);
			}

			if (Main.rand.NextBool(8)) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Grass, 0f, -0.5f, 120, default, 0.7f);
				d.noGravity = true;
			}
			// Drifting star motes so the swarm sparkles on the move.
			if (deployTicks % 6 == 0) {
				Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8,
					DustID.GoldCoin, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.5f, -0.5f), 120, default, 0.8f);
				d.noGravity = true;
			}
			// Dive streaks at attack speed.
			if (Projectile.velocity.LengthSquared() > 64f && Main.rand.NextBool(2)) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.WhiteTorch, -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f, 120, default, 0.7f);
				d.noGravity = true;
			}

			GenericEchoShot(40, 180);
			EchoPassive(deployTicks);
			TechStringFx(owner);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ApplySpellDebuff(target);
			EchoHit(target);
		}

		public override void OnKill(int timeLeft)
		{
			// Natural expiry puff (dismissals puff at the call site instead).
			for (int i = 0; i < 8; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Smoke, 0f, -1f, 120, default, 0.8f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			DrawSourceSprite(lightColor);
			return false;
		}
	}

	/// <summary>Marker so minion presence can be queried without scanning projectiles.</summary>
	public class SummonBuffPlayer : ModPlayer
	{
		public bool HasSummon;
		public override void ResetEffects() => HasSummon = false;
	}
}
