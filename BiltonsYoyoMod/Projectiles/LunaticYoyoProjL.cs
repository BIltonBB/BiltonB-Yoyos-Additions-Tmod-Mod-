using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;
using System.Collections.Generic;
using BiltonsYoyoMod.Configuration;
using BiltonsYoyoMod.Content.Projectiles.Tech;

namespace BiltonsYoyoMod.Projectiles
{
    public class LunaticYoyoProjL : ModProjectile
    {
        // SETTINGS

        // 1.5 seconds.
        private const int Lifetime = 90;

        // Final 0.5 seconds fade out.
        private const int FadeTime = 30;

        // Move every 0.1 seconds.
        private const int HopInterval = 6;


        // LIGHTNING STEP

        private const float StepMin = 55f;
        private const float StepMax = 110f;

        private const float SideOffsetMin = 10f;
        private const float SideOffsetMax = 24f;


        // LIGHTNING WIDTH

        private const float OuterWidth = 12f;
        private const float InnerWidth = 4f;


        // COLLISION

        private const float HitRadius = 8f;


        // STATE

        private int hopTimer;
        private int hitCooldown;

        private bool initialized;

        /*
         * Prevents the L from trying to perform anything when
         * it is being removed because it destroyed its parent LP.
         *
         * This is important for preventing recursive Kill() calls.
         */
        private bool destroyingParent;


        // LOCKED DIRECTION

        private Vector2 lockedDirection = Vector2.UnitX;


        // PARENT LP

        /*
         * ai[2] contains the exact projectile index of the LP
         * that created this lightning.
         */
        private int parentLPIndex = -1;


        // LIGHTNING PATH

        private readonly List<Vector2> lightningPath =
            new List<Vector2>();


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            Projectile.alpha = 0;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = -1;

            Projectile.timeLeft = Lifetime;

            Projectile.DamageType = DamageClass.Melee;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;
        }


        // AI

        public override void AI()
        {
            // INITIALIZE

            if (!initialized)
            {
                initialized = true;

                lockedDirection =
                    Projectile.ai[1]
                    .ToRotationVector2()
                    .SafeNormalize(Vector2.UnitX);

                parentLPIndex =
                    (int)Projectile.ai[2];

                Projectile.velocity = Vector2.Zero;

                lightningPath.Clear();

                lightningPath.Add(
                    Projectile.Center
                );
            }


            // PARENT CHECK

            /*
             * If the LP disappears before the 1.5 second lifetime
             * finishes, remove this lightning immediately.
             */
            if (!ParentLPExists())
            {
                Projectile.Kill();
                return;
            }


            // HIT COOLDOWN

            if (hitCooldown > 0)
            {
                hitCooldown--;
            }


            // L LIFETIME ENDS

            /*
             * At the final tick, destroy the parent LP.
             *
             * destroyingParent prevents the LP's OnKill() from
             * recursively trying to destroy this L again.
             */
            if (
                Projectile.timeLeft <= 1 &&
                Main.myPlayer == Projectile.owner
            )
            {
                destroyingParent = true;

                KillParentLP();

                Projectile.Kill();

                return;
            }


            // MOVE EVERY 0.1 SECOND

            hopTimer++;

            if (hopTimer >= HopInterval)
            {
                hopTimer = 0;

                MoveLightning();
            }


            // LIGHTING

            Lighting.AddLight(
                Projectile.Center,
                0.35f,
                0.10f,
                0.75f
            );
        }


        // PARENT LP CHECK

        private bool ParentLPExists()
        {
            if (
                parentLPIndex < 0 ||
                parentLPIndex >= Main.maxProjectiles
            )
            {
                return false;
            }

            Projectile parent =
                Main.projectile[parentLPIndex];

            int lpType =
                ModContent.ProjectileType<LunaticYoyoProjLP>();

            return
                parent.active &&
                parent.type == lpType &&
                parent.owner == Projectile.owner;
        }


        // KILL PARENT LP

        private void KillParentLP()
        {
            if (
                parentLPIndex < 0 ||
                parentLPIndex >= Main.maxProjectiles
            )
            {
                return;
            }

            Projectile parent =
                Main.projectile[parentLPIndex];

            int lpType =
                ModContent.ProjectileType<LunaticYoyoProjLP>();

            if (
                !parent.active ||
                parent.type != lpType ||
                parent.owner != Projectile.owner
            )
            {
                return;
            }

            /*
             * The L is already marked as destroyingParent.
             *
             * The LP's OnKill() may find this L, but this L's
             * OnKill() will simply return because of the flag.
             */
            parent.Kill();
        }


        // MOVE LIGHTNING

        private void MoveLightning()
        {
            Vector2 direction =
                lockedDirection;

            Vector2 perpendicular =
                new Vector2(
                    -direction.Y,
                    direction.X
                );

            float travelDistance =
                Main.rand.NextFloat(
                    StepMin,
                    StepMax
                );

            float sideOffset =
                Main.rand.NextFloat(
                    SideOffsetMin,
                    SideOffsetMax
                );

            if (Main.rand.NextBool())
            {
                sideOffset *= -1f;
            }

            Vector2 nextPoint =
                Projectile.Center +
                direction * travelDistance +
                perpendicular * sideOffset;

            Projectile.Center =
                nextPoint;

            lightningPath.Add(
                Projectile.Center
            );

            while (lightningPath.Count > 32)
            {
                lightningPath.RemoveAt(0);
            }
        }


        // COLLISION

        public override bool? Colliding(
            Rectangle projHitbox,
            Rectangle targetHitbox)
        {
            if (hitCooldown > 0)
            {
                return false;
            }


            // LIGHTNING PATH

            for (
                int i = 0;
                i < lightningPath.Count - 1;
                i++
            )
            {
                Vector2 start =
                    lightningPath[i];

                Vector2 end =
                    lightningPath[i + 1];

                if (
                    LineIntersectsExpandedRectangle(
                        start,
                        end,
                        targetHitbox,
                        HitRadius
                    )
                )
                {
                    return true;
                }
            }


            // CURRENT L POSITION

            Rectangle lHitbox =
                new Rectangle(
                    (int)(
                        Projectile.Center.X -
                        Projectile.width / 2f
                    ),
                    (int)(
                        Projectile.Center.Y -
                        Projectile.height / 2f
                    ),
                    Projectile.width,
                    Projectile.height
                );

            return lHitbox.Intersects(
                targetHitbox
            );
        }


        // LINE / RECTANGLE COLLISION

        private bool LineIntersectsExpandedRectangle(
            Vector2 start,
            Vector2 end,
            Rectangle rectangle,
            float padding)
        {
            Rectangle expanded =
                rectangle;

            expanded.Inflate(
                (int)padding,
                (int)padding
            );

            if (
                expanded.Contains(start.ToPoint()) ||
                expanded.Contains(end.ToPoint())
            )
            {
                return true;
            }

            Vector2 topLeft =
                new Vector2(
                    expanded.Left,
                    expanded.Top
                );

            Vector2 topRight =
                new Vector2(
                    expanded.Right,
                    expanded.Top
                );

            Vector2 bottomLeft =
                new Vector2(
                    expanded.Left,
                    expanded.Bottom
                );

            Vector2 bottomRight =
                new Vector2(
                    expanded.Right,
                    expanded.Bottom
                );

            return
                SegmentsIntersect(
                    start,
                    end,
                    topLeft,
                    topRight
                ) ||
                SegmentsIntersect(
                    start,
                    end,
                    topRight,
                    bottomRight
                ) ||
                SegmentsIntersect(
                    start,
                    end,
                    bottomRight,
                    bottomLeft
                ) ||
                SegmentsIntersect(
                    start,
                    end,
                    bottomLeft,
                    topLeft
                );
        }


        // SEGMENT INTERSECTION

        private bool SegmentsIntersect(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d)
        {
            float denominator =
                (b.X - a.X) *
                (d.Y - c.Y) -
                (b.Y - a.Y) *
                (d.X - c.X);

            if (
                Math.Abs(denominator) <
                0.0001f
            )
            {
                return false;
            }

            float numerator1 =
                (a.Y - c.Y) *
                (d.X - c.X) -
                (a.X - c.X) *
                (d.Y - c.Y);

            float numerator2 =
                (a.Y - c.Y) *
                (b.X - a.X) -
                (a.X - c.X) *
                (b.Y - a.Y);

            float t =
                numerator1 /
                denominator;

            float u =
                numerator2 /
                denominator;

            return
                t >= 0f &&
                t <= 1f &&
                u >= 0f &&
                u <= 1f;
        }


        // NPC HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            hitCooldown = 60;

            Projectile.localNPCHitCooldown = 60;

            CreateLightningHitEffect(
                target.Center
            );
        }


        // ON KILL

        public override void OnKill(
            int timeLeft)
        {
            /*
             * If the L is being killed because it reached its own
             * lifetime and already destroyed the LP, do nothing.
             *
             * This prevents the LP -> L -> LP recursive Kill chain.
             */
            if (destroyingParent)
            {
                return;
            }
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                TextureAssets.Projectile[
                    Projectile.type
                ].Value;


            // 1.5 SECOND LIFETIME / FINAL 0.5 SECOND FADE

            float fade;

            if (
                Projectile.timeLeft <= FadeTime
            )
            {
                fade =
                    Projectile.timeLeft /
                    (float)FadeTime;
            }
            else
            {
                fade = 1f;
            }

            fade =
                MathHelper.Clamp(
                    fade,
                    0f,
                    1f
                );

            // Opt-in soft discharge art. OFF by default: the canonical look is
            // the original pair of hard rectangles per hop (see else branch).
            bool softBeam = false;

            try
            {
                YoyoConfig cfg =
                    ModContent.GetInstance<YoyoConfig>();

                softBeam =
                    cfg != null &&
                    cfg.LunaticSoftLightningBeam;
            }
            catch
            {
                // Config not loaded (e.g. during unload): keep the default.
            }


            // LIGHTNING PATH

            if (softBeam)
            {
                // Cubic fade, same as MagicalLightningProj: the bolt holds full
                // brightness for the whole flight and then snaps out in its last
                // beats, instead of being linearly dimmed down from the moment it
                // stops growing.
                fade =
                    1f -
                    (1f - fade) *
                    (1f - fade) *
                    (1f - fade);

                /*
                 * Soft discharge: a stable seeded channel built from overlapping
                 * soft light - aura, braid, hot core, needles, barbs - so every
                 * edge is a gradient and the silhouette is jagged by
                 * construction. The recorded hop polyline is used as-is, so the
                 * shape still bends at every re-commit exactly like before.
                 */

                if (
                    lightningPath.Count >= 2 &&
                    fade > 0.01f
                )
                {
                    Span<Vector2> buffer =
                        stackalloc Vector2[32];

                    int n =
                        Math.Min(
                            lightningPath.Count,
                            32
                        );

                    int start =
                        lightningPath.Count - n;

                    for (int i = 0; i < n; i++)
                    {
                        buffer[i] =
                            lightningPath[start + i];
                    }

                    // The route ends exactly on the bolt, so the drawn head never
                    // lags a tick behind where the damage actually is.
                    buffer[n - 1] =
                        Projectile.Center;

                    Color accent =
                        new Color(150, 120, 255);

                    float width =
                        4.2f *
                        (0.55f + 0.45f * fade);

                    int seed =
                        Projectile.whoAmI * 613 + 29;

                    MagicalFx.SkyfangPath(
                        buffer.Slice(0, n),
                        accent,
                        0.95f * fade,
                        width,
                        seed,
                        2
                    );

                    // Strike head: the eye tracks the point the damage is at.
                    MagicalFx.Bloom(
                        Projectile.Center,
                        34f,
                        accent,
                        0.26f * fade
                    );

                    MagicalFx.Bloom(
                        Projectile.Center,
                        13f,
                        Color.White,
                        0.40f * fade
                    );

                    MagicalFx.StarFlare(
                        Projectile.Center,
                        Color.Lerp(
                            accent,
                            Color.White,
                            0.72f
                        ),
                        15f,
                        Projectile.rotation,
                        0.4f * fade
                    );
                }
            }
            else
            {
                // ORIGINAL LOOK (default): one soft sheath rectangle and one hot
                // core rectangle per recorded hop segment. The recorded path is
                // unchanged, so the bolt bends exactly where it always did.
                Texture2D pixel =
                    TextureAssets.MagicPixel.Value;

                for (
                    int i = 0;
                    i < lightningPath.Count - 1;
                    i++
                )
                {
                    Vector2 start =
                        lightningPath[i] -
                        Main.screenPosition;

                    Vector2 end =
                        lightningPath[i + 1] -
                        Main.screenPosition;

                    Vector2 difference =
                        end - start;

                    float length =
                        difference.Length();

                    if (length <= 1f)
                    {
                        continue;
                    }

                    float segmentProgress =
                        i /
                        (float)(
                            lightningPath.Count - 1
                        );

                    float backFade =
                        MathHelper.Lerp(
                            0.55f,
                            1f,
                            segmentProgress
                        );

                    // OUTER GLOW
                    byte outerAlpha =
                        (byte)MathHelper.Clamp(
                            105f *
                            backFade *
                            fade,
                            0f,
                            255f
                        );

                    Main.spriteBatch.Draw(
                        pixel,
                        (start + end) * 0.5f,
                        new Rectangle(
                            0,
                            0,
                            1,
                            1
                        ),
                        new Color(
                            105,
                            80,
                            175,
                            outerAlpha
                        ),
                        difference.ToRotation(),
                        new Vector2(
                            0.5f,
                            0.5f
                        ),
                        new Vector2(
                            length,
                            OuterWidth
                        ),
                        SpriteEffects.None,
                        0f
                    );

                    // INNER CORE
                    byte innerAlpha =
                        (byte)MathHelper.Clamp(
                            250f *
                            backFade *
                            fade,
                            0f,
                            255f
                        );

                    Main.spriteBatch.Draw(
                        pixel,
                        (start + end) * 0.5f,
                        new Rectangle(
                            0,
                            0,
                            1,
                            1
                        ),
                        new Color(
                            225,
                            215,
                            255,
                            innerAlpha
                        ),
                        difference.ToRotation(),
                        new Vector2(
                            0.5f,
                            0.5f
                        ),
                        new Vector2(
                            length,
                            InnerWidth
                        ),
                        SpriteEffects.None,
                        0f
                    );
                }
            }


            // CURRENT L SPRITE

            Color tileLight =
                Lighting.GetColor(
                    (int)(
                        Projectile.Center.X / 16f
                    ),
                    (int)(
                        Projectile.Center.Y / 16f
                    )
                );

            Color spriteColor =
                Color.Lerp(
                    tileLight,
                    new Color(
                        215,
                        200,
                        255
                    ),
                    0.45f
                );

            spriteColor.A =
                (byte)(
                    255f * fade
                );

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                spriteColor,
                Projectile.rotation,
                texture.Size() * 0.5f,
                0.50f,
                SpriteEffects.None,
                0f
            );

            return false;
        }


        // HIT EFFECT

        private void CreateLightningHitEffect(
            Vector2 position)
        {
            for (
                int i = 0;
                i < 14;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        80,
                        default,
                        Main.rand.NextFloat(
                            0.4f,
                            0.8f
                        )
                    );

                dust.noGravity = true;
            }
        }
    }
}
