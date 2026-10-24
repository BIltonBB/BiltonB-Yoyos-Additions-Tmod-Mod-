using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class SantaYoyoPresentsProj : ModProjectile, IYoyoInstantHit
    {
        private int swayTimer;
        private bool spawnFxDone;
        private readonly List<Vector2> streamer = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 600;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;

            Projectile.scale = 0.7f;
        }

        public override void AI()
        {
            // Parade variety, deterministic per slot: staggered sway phase,
            // 3 fall speeds, 3 wrapper-glow hues. Same lifetime/pierce/damage.
            int lane = Projectile.whoAmI % 3;
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                // Deterministic stagger (whoAmI syncs): no MP divergence.
                swayTimer = (Projectile.whoAmI % 5) * 12;
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    int pick = (i + lane) % 4;
                    int spawnType = pick == 0 ? DustID.RedTorch : pick == 1 ? DustID.GreenTorch : pick == 2 ? DustID.BlueTorch : DustID.YellowTorch;
                    Dust fx = Dust.NewDustPerfect(Projectile.Center, spawnType, angle.ToRotationVector2() * 2.2f, 0, default, 0.9f);
                    fx.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.9f, 0.6f, 0.7f);
            }

            swayTimer++;

            float fallSpeed = 1.2f + lane * 0.3f;
            float swayAmp = 1.7f + lane * 0.3f;
            Projectile.velocity.Y += (fallSpeed - Projectile.velocity.Y) * 0.03f;
            Projectile.velocity.X = (float)System.Math.Sin(swayTimer * 0.05f) * swayAmp;
            Projectile.rotation = (float)System.Math.Sin(swayTimer * 0.05f) * (0.15f + lane * 0.05f);
            // Dud gifts fall straight: no sway, no spin, pure menace.
            if (Projectile.ai[0] == 1f)
            {
                Projectile.velocity.X = 0f;
                Projectile.rotation = 0f;
            }

            if (Projectile.timeLeft < 60)
            {
                Projectile.alpha += 4;
                if (Projectile.alpha > 255)
                    Projectile.alpha = 255;
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.4f, 0.5f);

            // Lean festive trail: one mote every 4th tick with sway drift (was every tick).
            if (Projectile.timeLeft % 4 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, new Vector2(Projectile.velocity.X * 0.2f, -0.5f), 0, default, 0.8f);
                dust.noGravity = true;
            }

            // M14: canopy twinkle winking above the wrapper.
            if (Main.rand.NextBool(16))
            {
                int twinkleType = Main.rand.NextBool() ? DustID.Confetti : DustID.GoldFlame;
                Dust twinkle = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), -10f), twinkleType, new Vector2(0f, -0.6f), 0, default, 0.8f);
                twinkle.noGravity = true;
            }

            // MAJOR C (streamer half): short lane-hued streamer following the sway.
            MiniYoyoFx.RibbonPush(streamer, Projectile.Center, 10f, 5);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Gift glow + lane streamer + drop shadow (wrapper hue by slot).
            int lane = Projectile.whoAmI % 3;
            Color wrap = lane == 0 ? new Color(255, 170, 190) : lane == 1 ? new Color(170, 255, 190) : new Color(255, 220, 160);
            Color edge = lane == 0 ? new Color(220, 80, 100) : lane == 1 ? new Color(80, 200, 110) : new Color(220, 170, 80);
            MiniYoyoFx.RibbonDrawSoft(streamer, edge, wrap, 3f);
            // Single centered glow pinned to the draw point (canopy + shadow
            // orbs removed: they read as detached random circles while swaying).
            Vector2 drawPoint = Projectile.Center + new Vector2(0f, Projectile.gfxOffY);
            MiniYoyoFx.DrawGlow(drawPoint, 20f, wrap, 0.3f);
            // Landing shadow grows as the gift descends (reads the touchdown point).
            float descent = 1f - MathHelper.Clamp(Projectile.timeLeft / 600f, 0f, 1f);
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, 14f), 12f + 14f * descent, new Color(60, 30, 40), 0.08f + 0.12f * descent);
            // Art-center sprite draw (measured art centroid 22.9,26.1 of the 46x56
            // canvas sits ~2px high — vanilla pivots canvas-middle, so the swaying
            // wrapper read as offset from its own glow; this pins art to glow).
            Microsoft.Xna.Framework.Graphics.Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                lightColor * Projectile.Opacity, Projectile.rotation, new Vector2(22.9f, 26.1f), Projectile.scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Dud apology: gray puff and a confession (harmless by design).
            if (Projectile.ai[0] == 1f)
            {
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Dust puff = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, angle.ToRotationVector2() * 2f, 140, new Color(130, 130, 130), 1.1f);
                    puff.noGravity = true;
                }
                CombatText.NewText(new Rectangle((int)Projectile.Center.X - 20, (int)Projectile.Center.Y - 20, 40, 40), new Color(180, 180, 180), "sorry.");
                return;
            }
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust confetti = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, angle.ToRotationVector2() * 3f, 0, default, 1f);
                confetti.noGravity = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust festive = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, angle.ToRotationVector2() * 2.4f, 0, default, 0.9f);
                festive.noGravity = true;
            }
            // MAJOR C (pop half): wrapper-hued burst beads over the confetti.
            int lane = Projectile.whoAmI % 3;
            int laneDust = lane == 0 ? DustID.RedTorch : lane == 1 ? DustID.GreenTorch : DustID.BlueTorch;
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust bead = Dust.NewDustPerfect(Projectile.Center, laneDust, angle.ToRotationVector2() * 3.4f, 0, default, 1f);
                bead.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.9f, 0.6f, 0.7f);
        }
    }
}
