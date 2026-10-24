using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Central kill FX: EVERY damaging projectile of the 13 yoyos (mains AND
    // sub-projectiles) confirms kills through here, so blood and kill bursts
    // fire no matter which part lands the blow. Runs alongside each
    // projectile's own OnHitNPC. Same-tick duplicates are suppressed.
    public class YoyoKillFX : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        private static readonly Dictionary<(int P, int N), int> recentKills = new Dictionary<(int P, int N), int>();

        // Hit combo per owner: consecutive hits within 2s climb, milestones shout.
        private static readonly Dictionary<int, (int Count, int LastTick)> combos = new Dictionary<int, (int, int)>();

        // Combo read for war-paint staging (0 when idle).
        internal static int GetCombo(int owner)
        {
            if (combos.TryGetValue(owner, out var c) && (int)Main.GameUpdateCount - c.LastTick < 120)
                return c.Count;
            return 0;
        }

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return HasStyle(entity.type) || IsCounterweight(entity.type);
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            bool style = MiniYoyoFx.TryGetKillStyle(projectile.type, out Color color, out bool blood);
            bool weights = !style && IsCounterweight(projectile.type);
            if (!style && !weights)
                return;
            if (!style)
            {
                color = Color.White;
                blood = false;
            }
            // Counterweight love: vanilla weights get our sparks + combo too.
            if (weights)
                YoyoParticles.SpawnBurst(target.Center, 5, 2.5f, new Color(255, 225, 160), 0.9f, 20);
            // No blood on hits. Kills only.
            var key = (projectile.whoAmI, target.whoAmI);
            int tick = (int)Main.GameUpdateCount;
            if (recentKills.TryGetValue(key, out int last) && last == tick)
                return;
            recentKills[key] = tick;
            if (recentKills.Count > 64)
                recentKills.Clear();
            // Combo meter: every damaging hit from our 13 families counts while
            // chained inside 2s. Shouts at 10/25/50, owner-side text only.
            if (Main.myPlayer == projectile.owner)
            {
                int now = (int)Main.GameUpdateCount;
                int count = 1;
                if (combos.TryGetValue(projectile.owner, out var c) && now - c.LastTick < 120)
                    count = c.Count + 1;
                combos[projectile.owner] = (count, now);
                if (combos.Count > 16)
                    combos.Clear();
                if (count == 10 || count == 25 || count == 50)
                    CombatText.NewText(target.getRect(), new Color(255, 220, 150), count + " HITS!");
            }
            // Overkill readout: 2x+ HP in one blow pops gold (owner-side text).
            if (Main.myPlayer == projectile.owner && target.life <= 0 && target.lifeMax > 5 && -target.life >= target.lifeMax)
                CombatText.NewText(target.getRect(), new Color(255, 210, 110), "OVERKILL");
            MartianChain(projectile, target, damageDone);
            MiniYoyoFx.KillConfirm(target, color, projectile.Center, blood);
            // Impact taxonomy accent (B1): geometry-aware hit language layered
            // over the family's own ImpactFX (not on kills only - on every hit).
            if (style)
                MiniYoyoFx.ImpactClassAccent(target, projectile, damageDone, color);
            // E4: the hit plucks the family's deployed string (whip overlay).
            if (YoyoStringHookSystem.TryGetFamilyIndex(projectile.type, out int familyIdx))
                YoyoStringHookSystem.PluckFamily(projectile.owner, familyIdx);
            // G4: the killing family signs its death flourish.
            if (target.life <= 0 && familyIdx >= 0)
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                if (cfg == null || cfg.Visuals.FadeShapes)
                    MiniYoyoFx.FadeShapeFX(target.Center, color, VictimMarkSystem.KillFlourishFor(familyIdx), 1.1f);
            }
        }

        private static bool HasStyle(int type)
        {
            return MiniYoyoFx.TryGetKillStyle(type, out _, out _);
        }

        // Martian chain lightning: ray/laser/missile/main kills arc to 3
        // nearby enemies (visual arcs + half-damage bolts). Kill-gated so it
        // never storms on crowds; pooled sparks draw the arcs, real bolts
        // carry synced damage (no client-side StrikeNPC desync).
        private static bool IsMartian(int type)
        {
            return type == ModContent.ProjectileType<MartianYoyoProj>()
                || type == ModContent.ProjectileType<MartianYoyoLaserProj>()
                || type == ModContent.ProjectileType<MartianYoyoMissilesProj>()
                || type == ModContent.ProjectileType<MartianYoyoDeathRayProj>();
        }

        private static void MartianChain(Projectile projectile, NPC victim, int damageDone)
        {
            if (!IsMartian(projectile.type) || victim == null || victim.life > 0)
                return;
            int found = 0;
            float bestBase = 320f * 320f;
            for (int i = 0; i < Main.maxNPCs && found < 3; i++)
            {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active || npc.whoAmI == victim.whoAmI)
                    continue;
                if (!MiniYoyoFx.IsAttackable(npc))
                    continue;
                if (Vector2.DistanceSquared(victim.Center, npc.Center) > bestBase)
                    continue;
                found++;
                // Arc VFX: jittered spark line victim -> chained target.
                Vector2 a = victim.Center;
                Vector2 b = npc.Center;
                Vector2 side = (b - a).SafeNormalize(Vector2.UnitX);
                side = new Vector2(-side.Y, side.X);
                for (int s = 0; s <= 8; s++)
                {
                    float t = s / 8f;
                    Vector2 pos = Vector2.Lerp(a, b, t) + side * Main.rand.NextFloat(-6f, 6f) * (1f - System.Math.Abs(t - 0.5f) * 2f);
                    YoyoParticles.SpawnSpark(pos, Vector2.Zero, s % 3 == 0 ? Color.White : new Color(120, 255, 170), 0.9f, 14, 1f);
                }
                MiniYoyoFx.FlashFX(b, new Color(150, 255, 190), 0.8f);
                if (Main.myPlayer == projectile.owner)
                {
                    Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
                    Projectile.NewProjectile(projectile.GetSource_FromThis(), a, dir * 16f,
                        ModContent.ProjectileType<MartianChainBoltProj>(),
                        System.Math.Max(1, damageDone / 2), projectile.knockBack, projectile.owner);
                }
            }
        }
        private static bool IsCounterweight(int type)
        {
            return type == ProjectileID.RedCounterweight
                || type == ProjectileID.GreenCounterweight
                || type == ProjectileID.PurpleCounterweight
                || type == ProjectileID.YellowCounterweight
                || type == ProjectileID.SolarCounter
                || type == ProjectileID.AbigailCounter;
        }
    }
}
