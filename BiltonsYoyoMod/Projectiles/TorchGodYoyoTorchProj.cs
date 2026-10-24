using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class TorchGodYoyoTorchProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        private bool hasBurst;
        private readonly List<Vector2> trail = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;

            Projectile.timeLeft = 180;

            // Dust-only visual: the sprite is hidden, the flame is pure dust + light.
            Projectile.hide = true;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(trail, Projectile.Center, 8f, 12);

            // Spawn flash: compact ignition in the throw's identity.
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                bool oathSpawn = Projectile.ai[0] == 1f;
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Dust flash = Dust.NewDustPerfect(Projectile.Center, oathSpawn ? DustID.BlueTorch : DustID.Torch, angle.ToRotationVector2() * 2.2f, 100, default, 1.1f);
                    flash.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust push = Dust.NewDustPerfect(Projectile.Center, oathSpawn ? DustID.MagicMirror : DustID.GoldFlame, Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.5f, 0.5f), 100, default, 1f);
                    push.noGravity = true;
                }
                // M10: launch nose streak along the throw direction.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, oathSpawn ? DustID.MagicMirror : DustID.GoldFlame, Projectile.velocity * 0.5f, 100, default, 1.1f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 0.9f, 0.6f, 0.3f);
            }

            // Gravity
            Projectile.velocity.Y += 0.12f;

            // Two identities, one lifecycle: oathflames (ai[0] == 1, targeted) burn
            // blue-white and tight; wild torches burn gold-orange and loose.
            // Both cool as they age: white-hot birth -> ember red death.
            bool oath = Projectile.ai[0] == 1f;
            float age = 1f - Projectile.timeLeft / 180f;
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);

            // Lean flame body: 1 directional ember per tick, tinted by identity.
            // M11: perpendicular wobble so the tail feels alive, not laser-straight.
            Vector2 flightDir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 side = new Vector2(-flightDir.Y, flightDir.X);
            float wobble = (float)Math.Sin(Main.GameUpdateCount * 0.4f + Projectile.whoAmI) * 1.5f;
            int cycle = (int)(Main.GameUpdateCount % 3);
            int bodyType = oath
                ? (cycle == 2 ? DustID.MagicMirror : DustID.BlueTorch)
                : (cycle == 2 ? DustID.GoldFlame : DustID.Torch);
            float spread = oath ? 0.2f : 0.35f;
            Dust body = Dust.NewDustPerfect(Projectile.Center + side * wobble, bodyType, -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(spread, spread), 100, default, (0.85f + 0.3f * (1f - age)) * lifeFade + 0.15f);
            body.noGravity = true;

            if (Projectile.timeLeft % 3 == 0)
            {
                Dust core = Dust.NewDustPerfect(Projectile.Center, oath ? DustID.MagicMirror : DustID.GoldFlame, -Projectile.velocity * 0.05f, 100, default, 1.1f * lifeFade + 0.1f);
                core.noGravity = true;
            }
            if (Projectile.timeLeft % 4 == 0)
            {
                Dust wisp = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), oath ? DustID.BlueTorch : DustID.Torch, new Vector2(0f, -1.2f), 120, default, 0.8f * lifeFade + 0.1f);
                wisp.noGravity = true;
            }
            // M12: fast throws shed an extra stretched spark behind the head.
            if (Projectile.velocity.LengthSquared() > 64f && Main.rand.NextBool(10))
            {
                Dust streak = Dust.NewDustPerfect(Projectile.Center - flightDir * 6f, bodyType, -flightDir * 1.5f, 100, default, 0.8f);
                streak.noGravity = true;
            }
            // Old flames drip more as they cool (young ones almost never do).
            if (Main.rand.NextBool((int)MathHelper.Lerp(28f, 8f, age)))
            {
                Dust drip = Dust.NewDustPerfect(Projectile.Center, oath ? DustID.BlueTorch : DustID.Torch, new Vector2(0f, 1f), 100, default, 0.75f);
                drip.noGravity = false;
            }

            // Temperature light: blue-white birth cooling to deep ember red.
            float breath = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.2f) * 0.07f;
            // Torch tribute (config, default OFF: it eats real placed torches).
            // A wild torch passing the yoyo offers itself: flash + light surge.
            bool tributeOn = false;
            try
            {
                var tcfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                tributeOn = tcfg != null && tcfg.Visuals.TorchTribute;
            }
            catch
            {
            }
            if (tributeOn && !oath && Projectile.ai[1] == 0f)
            {
                int bestTX = -1, bestTY = -1;
                int pcx = (int)(Projectile.Center.X / 16f);
                int pcy = (int)(Projectile.Center.Y / 16f);
                for (int x = pcx - 3; x <= pcx + 3 && bestTX < 0; x++)
                {
                    for (int y = pcy - 3; y <= pcy + 3; y++)
                    {
                        if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                            continue;
                        Tile t = Main.tile[x, y];
                        if (t != null && t.HasTile && t.HasUnactuatedTile && t.TileType == Terraria.ID.TileID.Torches)
                        {
                            bestTX = x;
                            bestTY = y;
                            break;
                        }
                    }
                }
                if (bestTX >= 0 && Main.myPlayer == Projectile.owner)
                {
                    WorldGen.KillTile(bestTX, bestTY, false, false, false);
                    NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, bestTX, bestTY);
                    Projectile.ai[1] = 2f;
                    for (int k = 0; k < 6; k++)
                    {
                        float ra = MathHelper.TwoPi * k / 6f;
                        Dust offer = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, ra.ToRotationVector2() * 3.4f, 100, default, 1.2f);
                        offer.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 1.4f, 1f, 0.5f);
                    Projectile.netUpdate = true;
                }
            }
            // Torch relay: a wild torch passing the yoyo reignites (once each).
            if (!oath && Projectile.ai[1] == 0f)
            {
                int torchGodType = ModContent.ProjectileType<TorchGodYoyoProj>();
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.owner == Projectile.owner && p.type == torchGodType
                        && Vector2.DistanceSquared(p.Center, Projectile.Center) < 50f * 50f)
                    {
                        Projectile.ai[1] = 1f;
                        for (int k = 0; k < 4; k++)
                        {
                            float ra = MathHelper.TwoPi * k / 4f;
                            Dust relay = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, ra.ToRotationVector2() * 3f, 100, default, 1.1f);
                            relay.noGravity = true;
                        }
                        Lighting.AddLight(Projectile.Center, 1.2f, 0.8f, 0.4f);
                        // Flight changes stay owner-side (visuals run everywhere).
                        if (Main.myPlayer == Projectile.owner)
                        {
                            Projectile.velocity *= 1.3f;
                            Projectile.timeLeft = Math.Min(Projectile.timeLeft + 40, 180);
                            Projectile.netUpdate = true;
                        }
                        break;
                    }
                }
            }
            float lr = oath ? MathHelper.Lerp(0.55f, 0.5f, age) : MathHelper.Lerp(1f, 0.8f, age);
            float lg = oath ? MathHelper.Lerp(0.85f, 0.4f, age) : MathHelper.Lerp(0.75f, 0.25f, age);
            float lb = oath ? MathHelper.Lerp(1f, 0.9f, age) : MathHelper.Lerp(0.4f, 0.1f, age);
            Lighting.AddLight(Projectile.Center, lr * breath, lg * breath, lb * breath);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 180);
            // M13: identity-aware contact sparks + light before the burst consumes the torch.
            bool oath = Projectile.ai[0] == 1f;
            for (int i = 0; i < 2; i++)
            {
                Dust kiss = Dust.NewDustPerfect(target.Center, oath ? DustID.MagicMirror : DustID.GoldFlame, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1f);
                kiss.noGravity = true;
            }
            if (oath)
                Lighting.AddLight(target.Center, 0.5f, 0.7f, 1f);
            else
                Lighting.AddLight(target.Center, 0.9f, 0.55f, 0.25f);
            FlameBurst();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            FlameBurst();
            return false;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR B: living-flame comet — identity ribbon + layered glow anatomy.
            // Ribbon follows the real flight path (teleport-safe); head/highlight/tail
            // knots stretch with speed; hue cools with age; everything shrinks out
            // over the last 30 ticks (was a hard pop). hide=true so return false.
            bool oath = Projectile.ai[0] == 1f;
            float age = 1f - Projectile.timeLeft / 180f;
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed / 10f, 0.4f, 1.4f);
            float flicker = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.35f + Projectile.whoAmI) * 0.08f;

            Color ribbonEdge = oath ? new Color(60, 120, 220) : new Color(190, 90, 30);
            Color ribbonCore = oath ? new Color(200, 230, 255) : new Color(255, 215, 140);
            MiniYoyoFx.RibbonDrawSoft(trail, ribbonEdge, ribbonCore, MathHelper.Clamp(4f + speed * 0.15f, 4f, 7f));

            Color head = oath ? Color.Lerp(new Color(170, 220, 255), new Color(120, 120, 255), age) : Color.Lerp(new Color(255, 170, 80), new Color(200, 80, 40), age);
            Color tail = oath ? Color.Lerp(new Color(120, 170, 255), new Color(90, 90, 220), age) : Color.Lerp(new Color(230, 110, 40), new Color(160, 50, 30), age);
            MiniYoyoFx.DrawGlow(Projectile.Center, 24f * flicker * lifeFade + 2f, head, 0.55f * lifeFade);
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -4f), 14f * lifeFade + 1f, new Color(255, 225, 160), 0.6f * lifeFade);
            if (speed * speed > 1f)
            {
                Vector2 tailDir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - tailDir * 8f * stretch, (16f * stretch) * lifeFade + 1f, tail, 0.35f * lifeFade);
                MiniYoyoFx.DrawGlow(Projectile.Center - tailDir * 16f * stretch, (10f * stretch) * lifeFade + 1f, tail, 0.25f * lifeFade);

                // Cross-flare ticks through the head (Calamity HolyLight-inspired, 2 lines).
                Vector2 screen = Projectile.Center - Main.screenPosition;
                Vector2 side = new Vector2(-tailDir.Y, tailDir.X);
                Color flare = new Color(255, 235, 200, 140) * (0.5f * lifeFade);
                MiniYoyoFx.DrawLine(screen - tailDir * 6f, screen + tailDir * 6f, 1.5f, flare);
                MiniYoyoFx.DrawLine(screen - side * 6f, screen + side * 6f, 1.5f, flare);
            }
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            FlameBurst();
        }

        private void FlameBurst()
        {
            if (hasBurst)
                return;

            hasBurst = true;

            // Tight torch pop in the throw's identity (oath blue / wild gold).
            // (No DrawGlow here: drawing is only safe in PreDraw/PostDraw;
            // the flame head glow in PreDraw already covers the flash frame.)
            bool oath = Projectile.ai[0] == 1f;
            for (int i = 0; i < 12; i++)
            {
                float angle = MathHelper.TwoPi * i / 12f;
                int dustType = oath
                    ? (i % 3 == 0 ? DustID.MagicMirror : DustID.BlueTorch)
                    : (i % 3 == 0 ? DustID.GoldFlame : DustID.Torch);
                Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, angle.ToRotationVector2() * 2.6f, 100, default, 1.1f);
                dust.noGravity = true;
            }
            // M14: cooling smoke + identity-aware light (was a fixed orange flash).
            for (int i = 0; i < 2; i++)
            {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), DustID.Smoke, new Vector2(0f, -1f - i * 0.5f), 140, default, 0.9f);
                smoke.noGravity = true;
            }
            if (oath)
                Lighting.AddLight(Projectile.Center, 0.5f, 0.7f, 1f);
            else
            {
                Lighting.AddLight(Projectile.Center, 0.9f, 0.6f, 0.3f);
            }

            Projectile.Kill();
        }
    }
}
