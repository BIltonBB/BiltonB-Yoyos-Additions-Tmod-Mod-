using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class MourningWoodYoyoProj : ModProjectile
    {
        private int fireTimer;
        private int lingerTimer;
        private int fireCount; // every 5 shots the furnace breathes wide + hot
        private bool hotPhase;
        private int gardenCooldown = 600; // first garden check 10s after deploy
        private static bool gardenHintShown;
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the furnace (ExampleMod Basic Projectile pattern).
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
            Projectile.tileCollide = false;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 270f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 12f;
        }

        // M1: ember lift so the furnace reads in dark pumpkin-moon fields.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        // Breath-phase sync so remote clients see the same hot/cool furnace.
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(hotPhase);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            hotPhase = reader.ReadBoolean();
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // Hot breath runs thicker while the furnace scatters wide (synced phase).
            if (hotPhase && Main.GameUpdateCount % 5 == 0)
            {
                Dust thick = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.GoldFlame, new Vector2(0f, -1.6f), 120, default, 0.85f);
                thick.noGravity = true;
            }
            // Every 15th tick a gold heart-ember joins the bark cinders.
            if (Main.GameUpdateCount % 15 == 0)
            {
                Dust heart = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.GoldFlame, new Vector2(0f, -1.8f), 120, default, 0.9f);
                heart.noGravity = true;
            }
            // Ash snowfall: gray motes sift down around the furnace.
            if (Main.GameUpdateCount % 40 == 0)
            {
                Dust ash = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(-20f, -8f)), DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 0.7f), 140, new Color(120, 110, 105), 0.9f);
                ash.noGravity = false;
            }

            // M3: deterministic furnace spin (square hitbox; never affects collision).
            Projectile.rotation += 0.14f;

            // Muzzle rhythm: furnace light throbs with each 0.1s shot,
            // breathing hot/cool with the wide/narrow breath phases.
            float muzzle = 0.85f + 0.3f * (fireTimer / 6f);
            if (hotPhase)
                Lighting.AddLight(Projectile.Center, 1f * muzzle, 0.5f * muzzle, 0.12f * muzzle);
            else
                Lighting.AddLight(Projectile.Center, 0.75f * muzzle, 0.35f * muzzle, 0.08f * muzzle);

            // Thin bark-cinder accent: one rising ember every 5th tick (was 1/2 cloud).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 5 == 0)
            {
                Dust mote = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Torch, new Vector2(0f, -1.2f), 120, default, 0.8f);
                mote.noGravity = true;
            }

            // M5: fast-move cinder spray snapped behind the furnace when she runs.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spray = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.Torch, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 120, default, 0.8f);
                spray.noGravity = true;
            }

            // M4: ember string shimmer along the player->furnace line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.Torch, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            fireTimer++;
            lingerTimer++;

            // Every 0.1s: fire upward at high speed, breathing narrow column
            // and wide hot scatter every 5 shots (same count, varied shape).
            // Kindling nova: every 50th shot exhales all at once (owner-side).
            if (fireTimer >= 6)
            {
                fireTimer = 0;
                fireCount++;
                bool newPhase = (fireCount / 5) % 2 == 1;
                if (newPhase != hotPhase)
                {
                    hotPhase = newPhase;
                    Projectile.netUpdate = true;
                }
                float fan = hotPhase ? 25f : 8f;
                float speed = (hotPhase ? 10f : 13f) + Main.rand.NextFloat(-1f, 1f);
                Vector2 vel = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(MathHelper.ToRadians(-fan), MathHelper.ToRadians(fan))) * speed;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel,
                    ModContent.ProjectileType<MourningWoodYoyoFireProj>(),
                    Projectile.damage / 2,
                    Projectile.knockBack,
                    Projectile.owner);
                // M6: muzzle-side puff curling off the breath.
                MiniYoyoFx.FlashFX(Projectile.Center + new Vector2(0f, -8f), new Color(255, 160, 70), 0.7f);
                Dust puff = Dust.NewDustPerfect(Projectile.Center + new Vector2(0f, -8f), DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -2f), 120, default, 0.9f);
                puff.noGravity = true;
                if (fireCount % 50 == 0)
                {
                    for (int n = 0; n < 12; n++)
                    {
                        float nangle = MathHelper.TwoPi * n / 12f;
                        Dust nova = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, nangle.ToRotationVector2() * 5f, 100, default, 1.2f);
                        nova.noGravity = true;
                    }
                    MiniYoyoFx.FlashFX(Projectile.Center, new Color(255, 200, 120), 1.4f);
                }
            }

            // Every 0.5s: slow lobbed lingering ember with an arcing variety.
            if (lingerTimer >= 30)
            {
                lingerTimer = 0;
                Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -3f));
                if (vel.Length() > 5f)
                    vel = vel.SafeNormalize(Vector2.UnitY * -1f) * 4.5f;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel,
                    ModContent.ProjectileType<MourningWoodYoyoLingeringProj>(),
                    Projectile.damage / 3,
                    Projectile.knockBack,
                    Projectile.owner);
                // M7: lob ring puff marking the toss.
                for (int i = 0; i < 2; i++)
                {
                    float angle = MathHelper.TwoPi * i / 2f;
                    Dust lob = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 1.6f, 120, default, 0.8f);
                    lob.noGravity = true;
                }
                // Ember garden: a planted-ember world near clay pots grows one
                // fireblossom per minute (first time gets a hint shout).
                gardenCooldown--;
                if (gardenCooldown <= 0 && Main.myPlayer == Projectile.owner)
                {
                    gardenCooldown = 3600;
                    int pcx = (int)(Projectile.Center.X / 16f);
                    int pcy = (int)(Projectile.Center.Y / 16f);
                    for (int x = pcx - 4; x <= pcx + 4; x++)
                    {
                        for (int y = pcy - 4; y <= pcy + 4; y++)
                        {
                            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                                continue;
                            Tile t = Main.tile[x, y];
                            if (t == null || !t.HasTile || !t.HasUnactuatedTile)
                                continue;
                            if (t.TileType == Terraria.ID.TileID.ClayPot || t.TileType == Terraria.ID.TileID.PlanterBox)
                            {
                                Vector2 at = new Vector2(x * 16f + 8f, y * 16f);
                                int drop = Item.NewItem(Projectile.GetSource_FromThis(), (int)at.X, (int)at.Y, 8, 8, Terraria.ID.ItemID.Fireblossom, 1);
                                if (Main.netMode == NetmodeID.MultiplayerClient && drop >= 0)
                                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, drop);
                                if (!gardenHintShown)
                                {
                                    gardenHintShown = true;
                                    CombatText.NewText(new Rectangle((int)at.X - 20, (int)at.Y - 40, 40, 40), new Color(255, 180, 110), "ember garden!");
                                }
                                x = pcx + 5;
                                break;
                            }
                        }
                    }
                }
            }
        }

        // M9: scorch tick for the furnace itself (dust + light only; damage untouched).
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
            Lighting.AddLight(target.Center, 1f, 0.55f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Furnace identity: ember wake + seat glow + muzzle kindle,
            // kindle burning hotter/broader on wide-breath phases.
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type, hotPhase ? 8f : 7f);
            DrawAfterimages(lightColor);
            MiniYoyoFx.DrawGlow(Projectile.Center, hotPhase ? 20f : 18f, hotPhase ? new Color(255, 160, 70) : new Color(255, 140, 60), hotPhase ? 0.35f : 0.3f);

            // M8: the kindle pales toward white-hot on wide-breath phases.
            float kindle = fireTimer / 6f;
            Color kindleColor = hotPhase ? new Color(255, 200, 120) : new Color(255, 180, 100);
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -8f), 12f + 10f * kindle, kindleColor, 0.3f + 0.3f * kindle);

            // MAJOR A (column half): muzzle-flare stack breathing on wall-clock time
            // (not the owner-side fireTimer) so remote clients see it throb too.
            float breathT = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.3f);
            float columnBoost = hotPhase ? 1.3f : 1f;
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -14f), 16f * columnBoost, new Color(255, 170, 80), 0.25f * breathT * columnBoost);
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -26f), 12f * columnBoost, new Color(255, 160, 70), 0.18f * breathT * columnBoost);
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -38f), 9f * columnBoost, new Color(240, 140, 60), 0.12f * breathT * columnBoost);

            // MAJOR A (snap half): breath phase transition double ring.
            if (fireTimer == 0 && fireCount > 0)
            {
                float phaseT = (fireCount % 5) / 5f;
                Color phaseColor = hotPhase ? new Color(255, 180, 80) : new Color(200, 120, 40);
                MiniYoyoFx.DrawRing(Projectile.Center, 20f + 10f * phaseT, 2f, phaseColor * (0.3f + 0.4f * phaseT), 24);
                MiniYoyoFx.DrawRing(Projectile.Center, (20f + 10f * phaseT) * 0.55f, 1.5f, new Color(255, 220, 160) * (0.3f + 0.4f * phaseT), 20);
            }

            return true;
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow. Seat + lights + _Glow carry identity.
            // Aura budget (C1): fury ring cut - belongs to Ogre/Betsy/DarkMage now.
        }

        private void DrawAfterimages(Color lightColor)
        {
            // Same verified pattern as prior yoyos: TrailCacheLength ghosts, oldest->newest.
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
                Color color = (GetAlpha(lightColor) ?? lightColor) * (0.24f * age);
                float rot = (Projectile.oldRot != null && k < Projectile.oldRot.Length) ? Projectile.oldRot[k] : Projectile.rotation;
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                    null, color, rot, origin, Projectile.scale, SpriteEffects.None, 0);
            }
        }
    }
}
