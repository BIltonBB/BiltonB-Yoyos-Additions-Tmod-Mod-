using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class TorchGodYoyoProj : ModProjectile
    {
        private int randomTimer;
        private int targetedTimer = 60; // Offset so targeted throws stagger between random throws
        private readonly List<Vector2> trail = new List<Vector2>();

        // Cached oath direction for telegraphs (visual only; fire logic re-scans at the fire tick).
        private Vector2 oathDir = Vector2.UnitY;
        private bool oathValid;
        private int oathNPC = -1;
        private int tributeSurge; // lantern-borrow light ticks (visual only)
        private int lanternCooldown;

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the brazier (ExampleMod Basic Projectile pattern).
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.scale = 1f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 10f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 220f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;
        }

        // M1: warm lift so the brazier reads in dark caves without fullbrighting.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(trail, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // M3: deterministic visual spin only (square hitbox; never affects collision).
            Projectile.rotation += 0.16f;

            // Sparse torch embers shed along the flight path (was 75/s cloud).
            if (Main.rand.NextBool(5) && Projectile.velocity.LengthSquared() > 4f)
            {
                Dust ember = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.Torch, -Projectile.velocity * 0.05f + new Vector2(0f, -1f), 100, default, 0.9f);
                ember.noGravity = true;
            }
            if (Main.rand.NextBool(9))
            {
                Dust gold = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f), DustID.GoldFlame, new Vector2(0f, -0.7f), 120, default, 0.75f);
                gold.noGravity = true;
            }

            // M6: fast-move directional sparks snapped behind the flight dir.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spark = Dust.NewDustPerfect(Projectile.Center - back * 6f, DustID.Torch, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 100, default, 0.8f);
                spark.noGravity = true;
            }

            // M5: golden string shimmer drifting along the player->yoyo line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[Projectile.owner];
                if (owner != null && owner.active)
                {
                    Vector2 pos = Vector2.Lerp(owner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.GoldFlame, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            // M8: rare trial incense smoke curling off the brazier.
            if (Main.rand.NextBool(20))
            {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-4f, 4f), -6f), DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.9f), 140, default, 0.9f);
                smoke.noGravity = true;
            }

            // M4: breathing brazier light (flicker + throw swell, was a flat value).
            float k1 = MathHelper.Clamp((randomTimer - 48f) / 12f, 0f, 1f);
            float k2 = MathHelper.Clamp((targetedTimer - 108f) / 12f, 0f, 1f);
            float swell = MathHelper.Max(k1, k2);
            float breath = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.25f) * 0.06f;
            Lighting.AddLight(Projectile.Center, (0.85f + 0.1f * swell) * breath, (0.55f + 0.06f * swell) * breath, 0.25f * breath);
            // Borrowed lantern light surges while the tribute burns (visual only).
            if (tributeSurge > 0)
            {
                tributeSurge--;
                Lighting.AddLight(Projectile.Center, 0.5f, 0.4f, 0.2f);
            }
            // Lantern tribute (config, default OFF): near hanging lanterns, a
            // gold wisp streams lantern->yoyo and the brazier burns brighter.
            // Nothing is consumed (lanterns are furniture, not fuel) - that is
            // the most dimming the engine allows per-tile.
            if (lanternCooldown > 0)
                lanternCooldown--;
            else if (Main.myPlayer == Projectile.owner && TorchTributeOn())
            {
                lanternCooldown = 60;
                int pcx = (int)(Projectile.Center.X / 16f);
                int pcy = (int)(Projectile.Center.Y / 16f);
                for (int x = pcx - 5; x <= pcx + 5; x++)
                {
                    for (int y = pcy - 5; y <= pcy + 5; y++)
                    {
                        if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                            continue;
                        Tile t = Main.tile[x, y];
                        if (t == null || !t.HasTile || !t.HasUnactuatedTile)
                            continue;
                        int tt = t.TileType;
                        if (tt == Terraria.ID.TileID.HangingLanterns || tt == Terraria.ID.TileID.ChineseLanterns
                            || tt == Terraria.ID.TileID.SkullLanterns || tt == Terraria.ID.TileID.Chandeliers)
                        {
                            Vector2 lamp = new Vector2(x * 16f + 8f, y * 16f + 8f);
                            for (int k = 0; k < 3; k++)
                            {
                                Vector2 wispPos = Vector2.Lerp(lamp, Projectile.Center, k / 3f);
                                Dust wisp = Dust.NewDustPerfect(wispPos, DustID.GoldFlame, (Projectile.Center - lamp) * 0.02f, 100, default, 0.9f);
                                wisp.noGravity = true;
                            }
                            tributeSurge = 600;
                            break;
                        }
                    }
                    if (tributeSurge >= 600)
                        break;
                }
            }

            randomTimer++;
            targetedTimer++;

            // Cache the oath direction during the last 12 ticks so PreDraw can
            // telegraph without its own NPC scan (visual only).
            if (targetedTimer >= 108)
            {
                NPC omenCache = FindNearestEnemy(800f);
                if (omenCache != null)
                {
                    oathDir = (omenCache.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    oathValid = true;
                    oathNPC = omenCache.whoAmI;
                }
                else
                {
                    oathValid = false;
                    oathNPC = -1;
                }
            }
            else if (targetedTimer < 8)
            {
                // Just fired: hold the last direction briefly for the snap ring frame.
            }
            else
            {
                oathValid = false;
                oathNPC = -1;
            }

            // Every 1s (60 ticks): throw a wild torch in a random direction (ai[0] = 0).
            if (randomTimer >= 60)
            {
                randomTimer = 0;
                if (Main.myPlayer == Projectile.owner)
                {
                    Vector2 dir = Main.rand.NextVector2Circular(1f, 1f).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 8f,
                        ModContent.ProjectileType<TorchGodYoyoTorchProj>(), Projectile.damage / 2, 1f, Projectile.owner);
                    MuzzleFlash(Projectile.Center, dir, false);
                }
            }

            // Every 2s (120 ticks, offset): throw an oathflame toward nearest enemy (ai[0] = 1).
            if (targetedTimer >= 120)
            {
                targetedTimer = 0;
                if (Main.myPlayer == Projectile.owner)
                {
                    NPC target = FindNearestEnemy(800f);
                    if (target != null)
                    {
                        Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 10f,
                            ModContent.ProjectileType<TorchGodYoyoTorchProj>(), Projectile.damage / 2, 1f, Projectile.owner);
                        if (idx >= 0 && idx < Main.maxProjectiles)
                        {
                            Main.projectile[idx].ai[0] = 1f;
                            Main.projectile[idx].netUpdate = true;
                        }
                        MuzzleFlash(Projectile.Center, dir, true);
                    }
                }
            }

            // Kindle updrafts: twin rising streams while either throw loads.
            if ((randomTimer >= 48 || targetedTimer >= 108) && Main.GameUpdateCount % 4 == 0)
            {
                Dust up = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), -4f), DustID.Torch, new Vector2(0f, -2.6f), 100, default, 0.8f);
                up.noGravity = true;
            }
            // Brazier tongue: flame licks toward the coming throw (visual only, local dust).
            if (randomTimer >= 48)
            {
                if (Main.GameUpdateCount % 2 == 0)
                {
                    Dust tongue = Dust.NewDustPerfect(Projectile.Center + new Vector2(0f, -6f), DustID.Torch, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -2.4f), 100, default, 0.9f);
                    tongue.noGravity = true;
                }
            }
            // Rain steam: torches hiss white in the rain.
            if (Main.raining && Main.GameUpdateCount % 20 == 0)
            {
                Dust steam = Dust.NewDustPerfect(Projectile.Center + new Vector2(0f, -8f), DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -1.6f), 140, new Color(200, 200, 200), 1f);
                steam.noGravity = true;
            }
            if (targetedTimer >= 108 && oathValid)
            {
                if (Main.GameUpdateCount % 2 == 0)
                {
                    Dust lick = Dust.NewDustPerfect(Projectile.Center + oathDir * 8f, DustID.BlueTorch, oathDir * 2.4f, 100, default, 0.9f);
                    lick.noGravity = true;
                }
            }
        }

        private void MuzzleFlash(Vector2 pos, Vector2 dir, bool oathBlue)
        {
            MiniYoyoFx.FlashFX(pos, oathBlue ? new Color(140, 190, 255) : new Color(255, 180, 100));
            // Directional ignition kiss along the throw (was radial puff).
            // Dust + light only: drawing is only safe in PreDraw/PostDraw.
            int sparkType = oathBlue ? DustID.BlueTorch : DustID.Torch;
            int coreType = oathBlue ? DustID.MagicMirror : DustID.GoldFlame;
            for (int i = 0; i < 2; i++)
            {
                Dust spark = Dust.NewDustPerfect(pos, sparkType, dir * (3f + i * 1.5f) + Main.rand.NextVector2Circular(0.6f, 0.6f), 100, default, 1f);
                spark.noGravity = true;
            }
            Dust hot = Dust.NewDustPerfect(pos, coreType, dir * 1.5f, 100, default, 0.9f);
            hot.noGravity = true;
            // M7: lingering hot core at the muzzle.
            Dust core = Dust.NewDustPerfect(pos, coreType, dir * 0.5f, 100, default, 1.2f);
            core.noGravity = true;
            // Cast snap dust ring in the throw's identity (no extra projectiles).
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust ring = Dust.NewDustPerfect(pos, sparkType, angle.ToRotationVector2() * 2f, 100, default, 0.9f);
                ring.noGravity = true;
            }
            if (oathBlue)
                Lighting.AddLight(pos, 0.5f, 0.7f, 1f);
            else
                Lighting.AddLight(pos, 0.9f, 0.6f, 0.3f);
        }

        // M9: impact tick for the brazier itself (dust + light only; damage untouched).
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(255, 180, 110), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.1f;
                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Torch, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                spark.noGravity = true;
            }
            Dust core = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, new Vector2(0f, -1f), 100, default, 1f);
            core.noGravity = true;
            Lighting.AddLight(target.Center, 0.9f, 0.6f, 0.3f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Motion ribbon in torch-orange + warm seat-glow swelling before throws.
            MiniYoyoFx.RibbonDrawSoft(trail, Projectile.type);
            DrawAfterimages(lightColor);

            float k1 = MathHelper.Clamp((randomTimer - 48f) / 12f, 0f, 1f);
            float k2 = MathHelper.Clamp((targetedTimer - 108f) / 12f, 0f, 1f);
            float swell = MathHelper.Max(k1, k2);
            // Seat sheen (kept tight: the big-disc look lives only on specials).
            MiniYoyoFx.DrawGlow(Projectile.Center, 20f + 8f * swell, new Color(255, 150, 60), 0.3f + 0.15f * swell);

            // M15: soft ground shadow so the floating brazier sits in the world.
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, 10f), 26f, new Color(80, 35, 15), 0.18f);

            // MAJOR A: dual cooldown halo — contracting charge rings + orbiting embers.
            // Wild gold ring (60-tick cycle) + oath blue ring (120-tick cycle), drawn only
            // while charging (last 12 ticks) so the yoyo stays clean the rest of the time.
            if (k1 > 0f)
                MiniYoyoFx.DrawDashedRing(Projectile.Center, MathHelper.Lerp(30f, 14f, k1), 2f, new Color(255, 170, 80) * (0.25f + 0.55f * k1), 12, Main.GameUpdateCount * 0.1f);
            if (k2 > 0f && oathValid)
                MiniYoyoFx.DrawRing(Projectile.Center, MathHelper.Lerp(34f, 16f, k2), 2f, new Color(120, 180, 255) * (0.25f + 0.55f * k2), 24);

            // Cast snap rings right after firing (first 8 ticks of each cycle).
            if (randomTimer < 8)
            {
                float t = 1f - randomTimer / 8f;
                MiniYoyoFx.DrawRing(Projectile.Center, MathHelper.Lerp(14f, 26f, t), 2f, new Color(255, 190, 100) * (0.7f * (1f - t) + 0.1f), 24);
            }
            if (targetedTimer < 8)
            {
                float t = 1f - targetedTimer / 8f;
                MiniYoyoFx.DrawRing(Projectile.Center, MathHelper.Lerp(16f, 28f, t), 2f, new Color(140, 190, 255) * (0.7f * (1f - t) + 0.1f), 24);
            }

            // MAJOR C (yoyo half): oath firing tick — a short directional line toward
            // the doomed target while the blue ring charges. Wild throws stay circular
            // (their direction is random and unknowable to remote clients).
            // Victim marker: the doomed enemy wears a tightening blue ring.
            if (k2 > 0f && oathValid && oathNPC >= 0 && oathNPC < Main.maxNPCs)
            {
                NPC marked = Main.npc[oathNPC];
                if (marked != null && marked.active)
                {
                    MiniYoyoFx.DrawDashedRing(marked.Center, MathHelper.Lerp(26f, 14f, k2), 2f, new Color(140, 190, 255) * (0.3f + 0.5f * k2), 10, Main.GameUpdateCount * 0.15f);
                    // Oath bullseye: the dot fills as the throw loads.
                    MiniYoyoFx.DrawGlow(marked.Center, 4f + 8f * k2, new Color(180, 210, 255), 0.3f + 0.4f * k2);
                }
            }
            if (k2 > 0f && oathValid)
            {
                Vector2 start = Projectile.Center - Main.screenPosition;
                Vector2 end = Projectile.Center + oathDir * (10f + 8f * k2) - Main.screenPosition;
                MiniYoyoFx.DrawLine(start, end, 2f, new Color(140, 190, 255, 150) * (0.3f + 0.5f * k2));
            }

            return true;
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow: PreDraw seat + lighting + _Glow file already carry
            // identity. PostDraw keeps only motion/condition reads.
            // Aura budget (C1): fury ring cut - belongs to Ogre/Betsy/DarkMage now.
        }

        private void DrawAfterimages(Color lightColor)
        {
            // Same verified pattern as DarkMageYoyoProj: TrailCacheLength ghosts, oldest->newest.
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2)
                return;
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 halfHitbox = new Vector2(Projectile.width, Projectile.height) * 0.5f;
            Vector2 origin = tex.Size() * 0.5f;
            int ghosts = Math.Min(4, Projectile.oldPos.Length - 1);
            for (int k = ghosts; k >= 1; k--)
            {
                Vector2 oldCenter = Projectile.oldPos[k] + halfHitbox;
                if (oldCenter.LengthSquared() < 1f)
                    continue;
                float age = 1f - k / (float)(ghosts + 1);
                Color color = (GetAlpha(lightColor) ?? lightColor) * (0.26f * age);
                float rot = (Projectile.oldRot != null && k < Projectile.oldRot.Length) ? Projectile.oldRot[k] : Projectile.rotation;
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                    null, color, rot, origin, Projectile.scale, SpriteEffects.None, 0);
            }
        }

        private static bool TorchTributeOn()
        {
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                return cfg != null && cfg.Visuals.TorchTribute;
            }
            catch
            {
                return false;
            }
        }

        private NPC FindNearestEnemy(float maxRange)
        {
            NPC nearestNPC = null;
            float maxRangeSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (MiniYoyoFx.IsAttackable(npc))
                {
                    float distanceSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (distanceSq <= maxRangeSq)
                    {
                        maxRangeSq = distanceSq;
                        nearestNPC = npc;
                    }
                }
            }

            return nearestNPC;
        }
    }
}
