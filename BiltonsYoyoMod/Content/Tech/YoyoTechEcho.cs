using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.Tech;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Dedicated echo system: source yoyos lend weakened versions of their
	/// signature effects to tech shells. Additive-only: never touches the real
	/// yoyo projectiles, so existing content cannot break.
	/// Channels: HIT (throwable + swing + summon + magical) and PASSIVE
	/// (summon + magical only, reduced rates). Unknown yoyos get the generic
	/// fallback (nothing extra: base bolts already cover them).
	/// All echoes run at ~1/4 rate and 25-35% damage, hard-capped counts.
	///
	/// AUTO COVERAGE: every other modded yoyo is auto-mapped from its own
	/// sub-projectiles by class-name family (built once, lazily). Vanilla
	/// yoyos keep generic bolts (their code lives in Terraria itself).
	/// All echo spawns are forced player-friendly, never hostile.
	/// </summary>
	public static class YoyoTechEcho
	{
		private static System.Collections.Generic.Dictionary<int, int[]> autoSubs;
		private static bool autoBuilt;

		/// <summary>Auto-map every modded yoyo item to its sub-projectiles.</summary>
		private static void EnsureAuto()
		{
			if (autoBuilt)
				return;
			autoBuilt = true;
			autoSubs = new System.Collections.Generic.Dictionary<int, int[]>();
			try {
				foreach (int itemType in Index.YoyoIndexRegistry.AllYoyoTypes()) {
					Item tmp = new Item();
					try { tmp.SetDefaults(itemType); }
					catch { continue; }
					if (tmp.IsAir || tmp.shoot <= ProjectileID.None)
						continue;
					ModProjectile main = null;
					try { main = ModContent.GetModProjectile(tmp.shoot); }
					catch { continue; }
					if (main == null)
						continue; // Vanilla: generic bolts only.
					string ns = main.GetType().Namespace ?? string.Empty;
					if (ns.Contains(".Tech"))
						continue;
					string name = main.GetType().Name;
					string prefix = name.EndsWith("Proj") && name.Length > 4 ? name.Substring(0, name.Length - 4) : name;
					var scored = new System.Collections.Generic.List<(int type, int score)>();
					foreach (ModProjectile c in ModContent.GetContent<ModProjectile>()) {
						if (c == null)
							continue;
						string cn = c.GetType().Name ?? string.Empty;
						if (cn == name || !cn.StartsWith(prefix))
							continue;
						string cns = c.GetType().Namespace ?? string.Empty;
						if (cns.Contains(".Tech"))
							continue;
						// Helpers/controllers/transitions are rigging, not ammo: never echo them.
						if (cn.Contains("Helper") || cn.Contains("Controller") || cn.Contains("Transition") || cn.Contains("Base"))
							continue;
						int score = ScoreEchoCandidate(cn);
						scored.Add((c.Type, score));
					}
					scored.Sort((a, b) => b.score.CompareTo(a.score));
					var subs = new System.Collections.Generic.List<int>();
					foreach (var entry in scored) {
						subs.Add(entry.type);
						if (subs.Count >= 4)
							break;
					}
					if (subs.Count > 0 && !autoSubs.ContainsKey(tmp.shoot))
						autoSubs[tmp.shoot] = subs.ToArray();
				}
			}
			catch { }
		}

		private static bool IsVanillaYoyoProj(int projType)
		{
			try {
				if (projType <= ProjectileID.None || projType >= ProjectileID.Count)
					return false;
				if (ModContent.GetModProjectile(projType) != null)
					return false;
				float[] life = ProjectileID.Sets.YoyosLifeTimeMultiplier;
				return projType < life.Length && life[projType] != 0f;
			}
			catch { return false; }
		}

		/// <summary>Vanilla yoyos spark falling starlight: modest, clearly visible.</summary>
		private static void VanillaStarfall(TechProjBase shell, Vector2 target)
		{
			Vector2 dir = (target - shell.Projectile.Center).SafeNormalize(Vector2.Zero);
			int dmg = System.Math.Max(1, shell.Projectile.damage * 25 / 100);
			Vector2 spawn = target + new Vector2(-dir.X * 60f, -180f);
			int idx = Projectile.NewProjectile(shell.Projectile.GetSource_FromThis(), spawn,
				new Vector2(dir.X * 4f, 12f), ProjectileID.Starfury, dmg, 1f, shell.Projectile.owner);
			Tame(idx);
			SoundEngine.PlaySound(SoundID.Item11, shell.Projectile.Center);
		}

		private static int ScoreEchoCandidate(string name)
		{
			// Shooty/miniony names first; anything else still usable, just last.
			foreach (string key in new[] { "Fire", "Bolt", "Laser", "Missile", "Seed", "Shard", "Bullet", "Beam", "Blast", "Ball", "Bubble", "Spit", "Needle", "Star", "Spark", "Bone", "Honey", "Clone", "Mini", "Companion", "Eater", "Orb", "Wisp", "Skull", "Flame" }) {
				if (name.Contains(key))
					return 3;
			}
			if (name.EndsWith("Proj"))
				return 1;
			return 0;
		}

		private static int[] AutoSubsFor(int mainProjType)
		{
			EnsureAuto();
			if (autoSubs != null && autoSubs.TryGetValue(mainProjType, out int[] subs))
				return subs;
			return null;
		}

		private enum EchoKind { Volley, Companion, Drop, Beam }

		private class EchoDef
		{
			public int Main;
			public int HitSub; public EchoKind HitKind; public int HitCount = 1; public int HitPct = 30; public int HitChance = 6; public float HitSpeed = 12f;
			public int PassiveSub; public EchoKind PassiveKind; public int PassiveCount = 1; public int PassivePct = 30; public int PassiveEvery = 300; public int PassiveCap = 1;
		}

		private static System.Collections.Generic.List<EchoDef> handTable;
		private static bool handBuilt;

		/// <summary>Hand-tuned kits: the iconic yoyos and their signature subs.</summary>
		private static void EnsureHand()
		{
			if (handBuilt)
				return;
			handBuilt = true;
			handTable = new System.Collections.Generic.List<EchoDef>();
			void Add<TMain, TSub>(EchoKind hitKind, int hitCount, int hitPct, int hitChance, float hitSpeed,
				EchoKind passiveKind, int passiveCount, int passivePct, int passiveEvery, int passiveCap)
				where TMain : ModProjectile where TSub : ModProjectile
			{
				try {
					handTable.Add(new EchoDef {
						Main = ModContent.ProjectileType<TMain>(),
						HitSub = ModContent.ProjectileType<TSub>(), HitKind = hitKind, HitCount = hitCount, HitPct = hitPct, HitChance = hitChance, HitSpeed = hitSpeed,
						PassiveSub = ModContent.ProjectileType<TSub>(), PassiveKind = passiveKind, PassiveCount = passiveCount, PassivePct = passivePct, PassiveEvery = passiveEvery, PassiveCap = passiveCap,
					});
				}
				catch { }
			}
			Add<global::BiltonsYoyoMod.Projectiles.EyeYoyoProj, global::BiltonsYoyoMod.Projectiles.EyeYoyoRedProj>(
				EchoKind.Volley, 1, 30, 5, 12f, EchoKind.Volley, 2, 30, 360, 4);
			Add<global::BiltonsYoyoMod.Projectiles.CrimsonYoyoProj, global::BiltonsYoyoMod.Projectiles.CrimsonYoyoProjClone>(
				EchoKind.Companion, 1, 30, 7, 6f, EchoKind.Companion, 1, 30, 240, 1);
			Add<global::BiltonsYoyoMod.Projectiles.BeeYoyoProj, global::BiltonsYoyoMod.Projectiles.BeeYoyoHProj>(
				EchoKind.Drop, 1, 25, 7, 0f, EchoKind.Drop, 1, 30, 150, 4);
			Add<global::BiltonsYoyoMod.Projectiles.SkeletronYoyoProj, global::BiltonsYoyoMod.Projectiles.SkeletronYoyoBoneProj>(
				EchoKind.Volley, 1, 30, 5, 13f, EchoKind.Companion, 1, 30, 240, 1);
			Add<global::BiltonsYoyoMod.Projectiles.DukeYoyoProj, global::BiltonsYoyoMod.Projectiles.DukeYoyoProjBubble>(
				EchoKind.Volley, 1, 30, 5, 10f, EchoKind.Volley, 2, 30, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.BetsyYoyoProj, global::BiltonsYoyoMod.Projectiles.BetsyYoyoFireProj>(
				EchoKind.Volley, 1, 30, 5, 13f, EchoKind.Volley, 2, 30, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.GolemYoyoProj, global::BiltonsYoyoMod.Projectiles.GolemFistYoyoProj>(
				EchoKind.Companion, 1, 30, 7, 6f, EchoKind.Companion, 1, 30, 240, 1);
			Add<global::BiltonsYoyoMod.Projectiles.HallowYoyoProj, global::BiltonsYoyoMod.Projectiles.HallowYoyoProjS>(
				EchoKind.Volley, 2, 25, 6, 14f, EchoKind.Volley, 2, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.PlanteraYoyoProj, global::BiltonsYoyoMod.Projectiles.PlanteraYoyoProjSeed>(
				EchoKind.Volley, 2, 25, 6, 11f, EchoKind.Volley, 3, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.FleshYoyoProj, global::BiltonsYoyoMod.Projectiles.FleshYoyoBeamProj>(
				EchoKind.Volley, 1, 30, 6, 14f, EchoKind.Volley, 2, 30, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.DestroyerYoyoProj, global::BiltonsYoyoMod.Projectiles.DestroyerYoyoProjLaser>(
				EchoKind.Volley, 1, 30, 6, 14f, EchoKind.Volley, 2, 30, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.CorruptionYoyoProj, global::BiltonsYoyoMod.Projectiles.CorruptionYoyoSpikesProj>(
				EchoKind.Volley, 2, 25, 5, 12f, EchoKind.Volley, 3, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.RoyalYoyoProj, global::BiltonsYoyoMod.Projectiles.RoyalYoyoSpikesProj>(
				EchoKind.Volley, 2, 25, 5, 12f, EchoKind.Volley, 3, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.IceQueenYoyoProj, global::BiltonsYoyoMod.Projectiles.IceQueenYoyoShardProj>(
				EchoKind.Volley, 2, 25, 6, 13f, EchoKind.Volley, 3, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.MartianYoyoProj, global::BiltonsYoyoMod.Projectiles.MartianYoyoDeathRayProj>(
				EchoKind.Beam, 1, 25, 6, 0f, EchoKind.Volley, 2, 30, 240, 4);
			Add<global::BiltonsYoyoMod.Projectiles.SantaYoyoProj, global::BiltonsYoyoMod.Projectiles.SantaYoyoBulletProj>(
				EchoKind.Volley, 2, 25, 6, 13f, EchoKind.Volley, 3, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.PumpkingYoyoProj, global::BiltonsYoyoMod.Projectiles.PumpkingYoyoScytheProj>(
				EchoKind.Volley, 1, 30, 6, 13f, EchoKind.Volley, 2, 30, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.MourningWoodYoyoProj, global::BiltonsYoyoMod.Projectiles.MourningWoodYoyoFireProj>(
				EchoKind.Volley, 2, 25, 6, 12f, EchoKind.Volley, 3, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.EverscreamYoyoProj, global::BiltonsYoyoMod.Projectiles.EverscreamYoyoNeedleProj>(
				EchoKind.Volley, 2, 25, 6, 13f, EchoKind.Volley, 3, 25, 300, 4);
			Add<global::BiltonsYoyoMod.Projectiles.OgreYoyoProj, global::BiltonsYoyoMod.Projectiles.OgreYoyoSpitProj>(
				EchoKind.Volley, 1, 30, 6, 12f, EchoKind.Volley, 2, 30, 300, 4);
		}

		private static EchoDef FindDef(int mainProjType)
		{
			EnsureHand();
			if (handTable == null)
				return null;
			foreach (EchoDef def in handTable) {
				if (def.Main == mainProjType)
					return def;
			}
			return null;
		}

		private static void RunHit(EchoDef def, TechProjBase shell, NPC target)
		{
			switch (def.HitKind) {
				case EchoKind.Volley:
					if (Main.rand.NextBool(def.HitChance)) {
						for (int i = 0; i < def.HitCount; i++)
							FireAt(shell, def.HitSub, target.Center, def.HitPct, def.HitSpeed);
					}
					break;
				case EchoKind.Companion:
					if (Main.rand.NextBool(def.HitChance) && CountOwned(shell, def.HitSub) < def.PassiveCap)
						SpawnMinion(shell, def.HitSub, def.HitPct);
					break;
				case EchoKind.Drop:
					if (Main.rand.NextBool(def.HitChance))
						DropAt(shell, def.HitSub, target.Center, def.HitPct);
					break;
				case EchoKind.Beam:
					// A signature BEAM on the bite itself (the Martian saucer's
					// death ray): anchored to the shell so it sweeps with it.
					// One at a time - a second ray is not "more of the same
					// weapon", it is a mistake.
					if (Main.rand.NextBool(def.HitChance) && CountOwned(shell, def.HitSub) < 1)
						FireBeam(shell, def.HitSub, target.Center, def.HitPct);
					break;
			}
		}

		private static void RunPassive(EchoDef def, TechProjBase shell, int shellTicks)
		{
			if (shellTicks % def.PassiveEvery != 0)
				return;
			switch (def.PassiveKind) {
				case EchoKind.Volley: {
					NPC target = YoyoTechCommon.FindTarget(shell.Projectile.Center, 700f);
					if (target == null)
						return;
					for (int i = 0; i < def.PassiveCount; i++)
						FireAt(shell, def.PassiveSub, target.Center, def.PassivePct, 12f);
					break;
				}
				case EchoKind.Companion:
					if (CountOwned(shell, def.PassiveSub) < def.PassiveCap)
						SpawnMinion(shell, def.PassiveSub, def.PassivePct);
					break;
				case EchoKind.Drop:
					DropAt(shell, def.PassiveSub, shell.Projectile.Center, def.PassivePct);
					break;
				case EchoKind.Beam: {
					NPC target = YoyoTechCommon.FindTarget(shell.Projectile.Center, 700f);
					if (target == null)
						return;
					if (CountOwned(shell, def.PassiveSub) < def.PassiveCap)
						FireBeam(shell, def.PassiveSub, target.Center, def.PassivePct);
					break;
				}
			}
		}
		public static void OnHit(TechProjBase shell, NPC target)
		{
			if (shell == null || target == null || !target.active)
				return;
			if (shell.Projectile.owner != Main.myPlayer)
				return;
			int src = shell.SourceProjType;
			EchoDef hitDef = FindDef(src);
			if (hitDef != null) {
				RunHit(hitDef, shell, target);
				return;
			}
			// Vanilla: falling starlight on hits.
			if (IsVanillaYoyoProj(src)) {
				if (Main.rand.NextBool(7))
					VanillaStarfall(shell, target.Center);
				return;
			}
			// Auto: every other modded yoyo fires its own sub at 30%.
			int[] hitSubs = AutoSubsFor(src);
			if (hitSubs != null && hitSubs.Length > 0 && Main.rand.NextBool(5) && CountOwned(shell, hitSubs[0]) < 4)
				FireAt(shell, hitSubs[0], target.Center, 30, 12f);
		}

		public static void Passive(TechProjBase shell, int shellTicks)
		{
			if (shell == null || shell.Projectile.owner != Main.myPlayer)
				return;
			int src = shell.SourceProjType;
			EchoDef passiveDef = FindDef(src);
			if (passiveDef != null) {
				RunPassive(passiveDef, shell, shellTicks);
				return;
			}
			// Vanilla: starlight volley every 6s.
			if (IsVanillaYoyoProj(src)) {
				if (shellTicks % 360 == 0) {
					NPC autoTarget = YoyoTechCommon.FindTarget(shell.Projectile.Center, 700f);
					if (autoTarget != null) {
						VanillaStarfall(shell, autoTarget.Center);
						VanillaStarfall(shell, autoTarget.Center);
					}
				}
				return;
			}
			// Auto: every other modded yoyo cycles its own sub every 5s.
			int[] passiveSubs = AutoSubsFor(src);
			if (passiveSubs != null && passiveSubs.Length > 0 && shellTicks % 300 == 0 && CountOwned(shell, passiveSubs[0]) < 2) {
				NPC autoTarget = YoyoTechCommon.FindTarget(shell.Projectile.Center, 700f);
				if (autoTarget != null)
					FireAt(shell, passiveSubs[0], autoTarget.Center, 30, 12f);
			}
		}

		private static int CountOwned(TechProjBase shell, int type)
		{
			int n = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == shell.Projectile.owner && p.type == type)
					n++;
			}
			return n;
		}

		/// <summary>All echo spawns are forced player-friendly: never hostile.</summary>
		private static void Tame(int idx)
		{
			if (idx < 0 || idx >= Main.maxProjectiles)
				return;
			Main.projectile[idx].hostile = false;
			Main.projectile[idx].friendly = true;
			Main.projectile[idx].netUpdate = true;
		}

		private static void FireAt(TechProjBase shell, int projType, Vector2 target, int percent, float speed)
		{
			Vector2 dir = (target - shell.Projectile.Center).SafeNormalize(Vector2.UnitX);
			int dmg = System.Math.Max(1, shell.Projectile.damage * percent / 100);
			int idx = Projectile.NewProjectile(shell.Projectile.GetSource_FromThis(), shell.Projectile.Center,
				dir * speed, projType, dmg, 1f, shell.Projectile.owner);
			Tame(idx);
			SoundEngine.PlaySound(SoundID.Item11, shell.Projectile.Center);
		}

		/// <summary>
		/// Anchor one of the yoyo's own BEAM weapons to a tech shell. The ray
		/// rides the shell exactly as it rides its real yoyo: it only asks for
		/// "a yoyo of my own owner" to hold on to, and a shell is one. This is
		/// what keeps a signature beam alive when the tech layer has replaced the
		/// yoyo that used to fire it - the Martian saucer's death ray included.
		/// Damage is a percent of the shell's, like every other echo.
		/// </summary>
		private static void FireBeam(TechProjBase shell, int projType, Vector2 target, int percent)
		{
			Vector2 dir = (target - shell.Projectile.Center).SafeNormalize(Vector2.UnitX);
			int dmg = System.Math.Max(1, shell.Projectile.damage * percent / 100);
			int idx = Projectile.NewProjectile(shell.Projectile.GetSource_FromThis(), shell.Projectile.Center,
				Vector2.Zero, projType, dmg, 1f, shell.Projectile.owner,
				ai0: shell.Projectile.whoAmI, ai1: dir.ToRotation());
			Tame(idx);
			SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.45f }, shell.Projectile.Center);
		}

		private static void DropAt(TechProjBase shell, int projType, Vector2 at, int percent)
		{
			int dmg = System.Math.Max(1, shell.Projectile.damage * percent / 100);
			int idx = Projectile.NewProjectile(shell.Projectile.GetSource_FromThis(), at,
				new Vector2(Main.rand.NextFloat(-1f, 1f), 2f), projType, dmg, 1f, shell.Projectile.owner);
			Tame(idx);
		}

		private static void SpawnMinion(TechProjBase shell, int projType, int percent)
		{
			int dmg = System.Math.Max(1, shell.Projectile.damage * percent / 100);
			int idx = Projectile.NewProjectile(shell.Projectile.GetSource_FromThis(), shell.Projectile.Center,
				shell.Projectile.velocity.SafeNormalize(Vector2.UnitX) * 6f, projType, dmg, 1f, shell.Projectile.owner);
			Tame(idx);
		}
	}
}
