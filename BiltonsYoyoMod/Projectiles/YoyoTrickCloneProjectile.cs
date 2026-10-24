using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class YoyoTrickCloneProjectile : ModProjectile
    {
        // STORED YOYO DATA

        // Exact projectile type of the original yoyo.
        public int StoredYoyoType;

        // Exact damage of the original yoyo.
        public int StoredDamage;

        // TARGET

        // Selected once when the clone is created.
        // The clone never changes targets.
        private int TargetNPC = -1;

        // BOUNCE STATE

        private bool Bouncing;

        private int BounceCooldown;

        // Time between bounce attempts.
        private const int BounceInterval =
            60;

        // NORMAL FOLLOWING

        // How far above the NPC the clone normally sits.
        private const float HoverHeight =
            35f;

        // Maximum normal movement speed.
        private const float MaximumSpeed =
            7f;

        // Smooth acceleration/deceleration.
        private const float MovementSmoothing =
            0.12f;

        // BOUNCE

        private const float BounceUpSpeed =
            8f;

        private const float BounceGravity =
            0.30f;

        private const float MaximumFallSpeed =
            10f;

        // LIFETIME

        private const int Lifetime =
            10 * 60;

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width =
                22;

            Projectile.height =
                22;

            // Damage is manually dealt on contact.
            Projectile.friendly =
                false;

            Projectile.hostile =
                false;

            Projectile.penetrate =
                -1;

            Projectile.timeLeft =
                Lifetime;

            Projectile.tileCollide =
                false;

            Projectile.ignoreWater =
                true;

            Projectile.DamageType =
                DamageClass.Melee;
        }

        // INITIALIZE

        public void Initialize(
            int yoyoType,
            int damage
        )
        {
            StoredYoyoType =
                yoyoType;

            StoredDamage =
                damage;

            // Pick the target once.
            TargetNPC =
                FindTarget();

            Bouncing =
                false;

            // Give the clone time to reach the target first.
            BounceCooldown =
                BounceInterval;
        }

        // FIND TARGET
        //
        // Any active NPC can be selected.
        //
        // CanBeChasedBy() is intentionally NOT used so Target Dummies
        // can be targeted.

        private int FindTarget()
        {
            int closest =
                -1;

            float closestDistanceSquared =
                float.MaxValue;

            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];

                if (!npc.active)
                    continue;

                Vector2 difference =
                    npc.Center -
                    Projectile.Center;

                float distanceSquared =
                    difference.LengthSquared();

                if (
                    distanceSquared <
                    closestDistanceSquared
                )
                {
                    closestDistanceSquared =
                        distanceSquared;

                    closest =
                        i;
                }
            }

            return closest;
        }

        // AI

        public override void AI()
        {
            // TARGET CHECK

            if (
                TargetNPC < 0 ||
                TargetNPC >= Main.maxNPCs
            )
            {
                Projectile.Kill();
                return;
            }

            NPC target =
                Main.npc[
                    TargetNPC
                ];

            // Target is invalid -> clone disappears.
            if (
                target == null ||
                !target.active
            )
            {
                Projectile.Kill();
                return;
            }

            // COOLDOWN

            if (BounceCooldown > 0)
            {
                BounceCooldown--;
            }

            // MOVEMENT

            if (Bouncing)
            {
                UpdateBounce(
                    target
                );
            }
            else
            {
                FollowTarget(
                    target
                );

                if (BounceCooldown <= 0)
                {
                    StartBounce();
                }
            }

            // ROTATION

            Projectile.rotation +=
                Projectile.velocity.X *
                0.08f;

            // ICE DUST

            if (Main.rand.NextBool(3))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.IceTorch,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    Main.rand.NextFloat(
                        0.45f,
                        0.75f
                    );
            }
        }

        // FOLLOW TARGET

        private void FollowTarget(
            NPC target
        )
        {
            // Read the target's top.
            //
            // This does NOT change the target.
            float targetTop =
                target.Center.Y -
                target.height / 2f;

            Vector2 destination =
                new Vector2(
                    target.Center.X,
                    targetTop -
                    HoverHeight -
                    Projectile.height / 2f
                );

            SmoothMoveTo(
                destination,
                MaximumSpeed
            );
        }

        // SMOOTH MOVE

        private void SmoothMoveTo(
            Vector2 destination,
            float maximumSpeed
        )
        {
            Vector2 difference =
                destination -
                Projectile.Center;

            float distance =
                difference.Length();

            if (distance <= 1f)
            {
                Projectile.velocity *=
                    0.85f;

                return;
            }

            Vector2 direction =
                difference /
                distance;

            float desiredSpeed =
                maximumSpeed;

            // Decelerate as we approach the destination.
            if (distance < 70f)
            {
                desiredSpeed =
                    MathHelper.Lerp(
                        0.5f,
                        maximumSpeed,
                        distance / 70f
                    );
            }

            Vector2 desiredVelocity =
                direction *
                desiredSpeed;

            // Smooth acceleration AND deceleration.
            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    MovementSmoothing
                );

            if (distance < 12f)
            {
                Projectile.velocity *=
                    0.82f;
            }
        }

        // START BOUNCE

        private void StartBounce()
        {
            Bouncing =
                true;

            // Start by jumping upward.
            Projectile.velocity.Y =
                -BounceUpSpeed;

            // LAUNCH FX (visual + audio only): flash + whoosh off the paws.
            MiniYoyoFx.FlashFX(
                Projectile.Center,
                new Color(
                    255,
                    230,
                    150
                ),
                0.9f
            );

            SoundEngine.PlaySound(
                SoundID.Item1 with { Pitch = 0.2f },
                Projectile.Center
            );

            Main.NewText(
                "WALK THE DOG CLONE: BOUNCE STARTED",
                Color.Yellow
            );
        }

        // UPDATE BOUNCE
        //
        // The clone rises and then falls.
        //
        // It only deals damage when its OWN hitbox contacts the
        // target's hitbox while falling.
        //
        // The target is never moved.

        private void UpdateBounce(
            NPC target
        )
        {
            // GRAVITY

            Projectile.velocity.Y +=
                BounceGravity;

            if (
                Projectile.velocity.Y >
                MaximumFallSpeed
            )
            {
                Projectile.velocity.Y =
                    MaximumFallSpeed;
            }

            // HORIZONTAL TRACKING

            float horizontalDifference =
                target.Center.X -
                Projectile.Center.X;

            float desiredHorizontalVelocity =
                MathHelper.Clamp(
                    horizontalDifference *
                    0.08f,
                    -MaximumSpeed,
                    MaximumSpeed
                );

            Projectile.velocity.X =
                MathHelper.Lerp(
                    Projectile.velocity.X,
                    desiredHorizontalVelocity,
                    0.12f
                );

            // CONTACT

            if (
                Projectile.velocity.Y > 0f &&
                Projectile.Hitbox.Intersects(
                    target.Hitbox
                )
            )
            {
                PerformBounceDamage(
                    target
                );

                CreateImpactDust(
                    target
                );

                // BOUNCE IMPACT FX (visual only): the falling cut reads as a
                // slash + ring over the existing dust (damage above untouched).
                MiniYoyoFx.ImpactFX(
                    target.Center,
                    new Color(
                        255,
                        240,
                        180
                    ),
                    1f,
                    Projectile.owner,
                    0f
                );

                // MOVE ONLY THE CLONE BACK ABOVE TARGET

                float targetTop =
                    target.Center.Y -
                    target.height / 2f;

                Projectile.position.Y =
                    targetTop -
                    HoverHeight -
                    Projectile.height / 2f;

                // RESET BOUNCE

                Bouncing =
                    false;

                BounceCooldown =
                    BounceInterval;

                Projectile.velocity =
                    new Vector2(
                        0f,
                        0f
                    );

                Main.NewText(
                    "WALK THE DOG CLONE: BOUNCE COMPLETE",
                    Color.LightBlue
                );
            }
        }

        // BOUNCE DAMAGE

        private void PerformBounceDamage(
            NPC target
        )
        {
            int damage =
                StoredDamage / 2;

            if (damage < 1)
            {
                damage = 1;
            }

            // IMPORTANT
            //
            // Knockback is ZERO.
            //
            // This means the Target Dummy/NPC does NOT get pushed.
            // Only its health is affected.

            NPC.HitInfo hit =
                new NPC.HitInfo
                {
                    Damage =
                        damage,

                    Knockback =
                        0f,

                    HitDirection =
                        0,

                    Crit =
                        false
                };

            target.StrikeNPC(
                hit
            );

            Main.NewText(
                "WALK THE DOG CLONE: " +
                damage +
                " DAMAGE",
                Color.Yellow
            );
        }

        // IMPACT DUST

        private void CreateImpactDust(
            NPC target
        )
        {
            float targetTop =
                target.Center.Y -
                target.height / 2f;

            Vector2 impactPosition =
                new Vector2(
                    target.Center.X,
                    targetTop
                );

            for (
                int i = 0;
                i < 10;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        impactPosition +
                        Main.rand.NextVector2Circular(
                            12f,
                            4f
                        ),
                        DustID.YellowTorch,
                        Main.rand.NextVector2Circular(
                            2f,
                            2f
                        )
                    );

                dust.noGravity =
                    true;

                dust.scale =
                    Main.rand.NextFloat(
                        0.6f,
                        1f
                    );
            }
        }

        // GET SOURCE YOYO TEXTURE

        private Texture2D GetSourceTexture()
        {
            int type =
                StoredYoyoType;

            if (
                type <= 0 ||
                type >=
                    TextureAssets.Projectile.Length
            )
            {
                return null;
            }

            Main.instance.LoadProjectile(
                type
            );

            return TextureAssets.Projectile[
                type
            ].Value;
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor
        )
        {
            Texture2D texture =
                GetSourceTexture();

            if (texture == null)
            {
                return false;
            }

            Vector2 origin =
                texture.Size() /
                2f;

            // AFTERIMAGES

            for (
                int i = 5;
                i >= 0;
                i--
            )
            {
                if (
                    i >=
                    Projectile.oldPos.Length
                )
                {
                    continue;
                }

                Vector2 position =
                    Projectile.oldPos[i] +
                    Projectile.Size / 2f;

                if (
                    position ==
                    Vector2.Zero
                )
                {
                    continue;
                }

                float alpha =
                    0.4f *
                    (
                        1f -
                        i / 6f
                    );

                if (alpha <= 0.01f)
                {
                    continue;
                }

                // Lighting at the historical position.
                Color lighting =
                    Lighting.GetColor(
                        (int)(
                            position.X / 16f
                        ),
                        (int)(
                            position.Y / 16f
                        )
                    );

                float brightness =
                    (
                        lighting.R +
                        lighting.G +
                        lighting.B
                    ) / 765f;

                brightness =
                    MathHelper.Clamp(
                        brightness,
                        0.4f,
                        1f
                    );

                Main.EntitySpriteDraw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    Color.White *
                    (
                        alpha *
                        brightness
                    ),
                    Projectile.rotation,
                    origin,
                    Projectile.scale,
                    SpriteEffects.None
                );
            }

            // CURRENT CLONE

            Color currentLighting =
                Lighting.GetColor(
                    (int)(
                        Projectile.Center.X / 16f
                    ),
                    (int)(
                        Projectile.Center.Y / 16f
                    )
                );

            float currentBrightness =
                (
                    currentLighting.R +
                    currentLighting.G +
                    currentLighting.B
                ) / 765f;

            currentBrightness =
                MathHelper.Clamp(
                    currentBrightness,
                    0.4f,
                    1f
                );

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                Color.White *
                currentBrightness,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None
            );

            // Never draw the placeholder texture.
            return false;
        }
    }
}