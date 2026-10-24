using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Betsy fireball (barrage volleys, speed 11 set by the parent). Inflicts OnFire! for
    /// 180 ticks and bursts into an explosion on hit or tile impact.
    /// </summary>
    public class BetsyYoyoFireBallProj : ModProjectile, IYoyoInstantHit
    {
        private bool hasExploded;
        private bool spawnFxDone;
        private readonly List<Vector2> exhaust = new List<Vector2>();

        // M12: bombard lift so shells read against burning skies.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(255, 205, 160), 0.3f) * Projectile.Opacity;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 240;

            Projectile.scale = 0.6f;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            // Spawn flash: tight ignition (was 10 random).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Lighting.AddLight(Projectile.Center, 1.4f, 0.6f, 0.2f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.6f, 100, default, 1f);
                    dust.noGravity = true;
                }
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, 1.0f, 0.5f, 0.15f);

            MiniYoyoFx.RibbonPush(exhaust, Projectile.Center, 8f, 10);

            // Lean barrage trail: every other tick (was 2/tick x ~36 alive).
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, -Projectile.velocity * 0.08f, 120, default, 0.85f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Scroll mode on the exhaust ribbon: energy bands flow down the trail.
            MiniYoyoFx.DrawWithShader(0, Color.White, 0.8f,
                () => MiniYoyoFx.RibbonDrawSoft(exhaust, new Color(180, 80, 30), new Color(255, 210, 140), 5f));
            float flicker = 1f + (float)System.Math.Sin(Main.GameUpdateCount * 0.45f + Projectile.whoAmI) * 0.1f;
            MiniYoyoFx.DrawGlow(Projectile.Center, 20f * flicker, new Color(255, 160, 80), 0.5f);
            // M13: tail knot + head ticks so the 11spd shells streak.
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 9f, 12f, new Color(220, 110, 40), 0.35f);
                Vector2 head = Projectile.Center - Main.screenPosition;
                Vector2 tail = Projectile.Center - back * 12f - Main.screenPosition;
                MiniYoyoFx.DrawLine(head, tail, 1.5f, new Color(255, 215, 160, 140) * 0.55f);
            }
            // Art-center draw (measured bright-mass centroid 16.5,34.7 of the 34x90
            // canvas — same top-heavy flame as the drop variant).
            Microsoft.Xna.Framework.Graphics.Texture2D shellTex = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(shellTex, Projectile.Center - Main.screenPosition, null,
                GetAlpha(lightColor) ?? lightColor, Projectile.rotation, new Vector2(16.5f, 34.7f), Projectile.scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 180);
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Explode();
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            Explode();
        }

        private void Explode()
        {
            if (hasExploded)
                return;

            hasExploded = true;

            // Tight barrage pop: ember core + smoke + gold rise + crisp ring (was 34).
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 3f, 100, default, 1.1f);
                dust.noGravity = true;
            }

            for (int i = 0; i < 3; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1.2f), 120, default, 0.9f);
                dust.noGravity = true;
            }

            // Cinder rise above the blast (was 8 random column).
            for (int i = 0; i < 4; i++)
            {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-18f, 0f));
                Dust dust = Dust.NewDustPerfect(pos, DustID.GoldFlame, new Vector2(0f, -2f), 120, default, 1f);
                dust.noGravity = true;
            }

            // Crisp flash ring (was 8).
            for (int i = 0; i < 3; i++)
            {
                float angle = MathHelper.TwoPi * i / 3f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 3.6f, 100, default, 1f);
                dust.noGravity = true;
            }

            // MAJOR C (crown half): tall gold rise + white-hot core over the pop.
            for (int i = 0; i < 4; i++)
            {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-22f, -4f));
                Dust crown = Dust.NewDustPerfect(pos, DustID.GoldFlame, new Vector2(0f, -2.4f), 120, default, 1f);
                crown.noGravity = true;
            }
            for (int i = 0; i < 2; i++)
            {
                float angle = MathHelper.TwoPi * i / 2f;
                Dust core = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 2.6f, 100, default, 1f);
                core.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 1.2f, 0.5f, 0.15f);
            // Synced vanilla slash sparkles over the pop (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);

            Projectile.Kill();
        }
    }
}
