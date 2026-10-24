using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common.Systems;
using BiltonsYoyoMod.Common;
using BiltonsYoyoMod.Items.Accessories;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Players
{
    public class StringCutterPlayer : ModPlayer
    {
        // ACCESSORY STATE

        public bool StringCutterEquipped;

        public StringCutterType StringCutterType =
            StringCutterType.Standard;

        // RESET

        public override void ResetEffects()
        {
            StringCutterEquipped = false;
            StringCutterType = StringCutterType.Standard;
        }

        // KEYBIND

        public override void ProcessTriggers(
            TriggersSet triggersSet)
        {
            if (ModalInput.GameplayHotkeysBlocked(Player))
                return;

            if (!StringCutterEquipped)
                return;

            if (StringCutterKeybindSystem.CutStringKeybind == null)
                return;

            if (!StringCutterKeybindSystem.CutStringKeybind.JustPressed)
                return;

            CutActiveYoyo();
        }

        // FIND ACTIVE YOYO

        private void CutActiveYoyo()
        {
            if (!Player.active)
                return;

            // MAXIMUM THREE DETACHED YOYOS

            int ropeType =
                ModContent.ProjectileType<
                    StringCutterRopeProjectile>();

            int detachedCount = 0;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];

                if (!projectile.active)
                    continue;

                if (projectile.owner != Player.whoAmI)
                    continue;

                if (projectile.type == ropeType)
                    detachedCount++;
            }

            if (detachedCount >= 3)
                return;

            // DETERMINE THE PROJECTILE THE PLAYER ACTUALLY FIRED

            int heldYoyoType = -1;

            if (Player.HeldItem != null &&
                !Player.HeldItem.IsAir)
            {
                heldYoyoType =
                    Player.HeldItem.shoot;
            }

            // FIRST PASS:
            // MATCH THE HELD ITEM'S SHOOT PROJECTILE.
            //
            // This prevents a yoyo's auxiliary projectiles from
            // accidentally being selected.

            Projectile activeYoyo = null;

            if (heldYoyoType > 0)
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile projectile =
                        Main.projectile[i];

                    if (!projectile.active)
                        continue;

                    if (projectile.owner != Player.whoAmI)
                        continue;

                    if (projectile.type != heldYoyoType)
                        continue;

                    if (!IsRegisteredYoyo(projectile.type))
                        continue;

                    /*
                     * Keep the newest matching projectile.
                     */
                    activeYoyo = projectile;
                }
            }

            // SECOND PASS:
            // FALLBACK FOR CUSTOM YOYOS.

            if (activeYoyo == null)
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile projectile =
                        Main.projectile[i];

                    if (!projectile.active)
                        continue;

                    if (projectile.owner != Player.whoAmI)
                        continue;

                    if (!IsRegisteredYoyo(projectile.type))
                        continue;

                    /*
                     * Never select the String Cutter's own projectile.
                     */
                    if (projectile.type == ropeType)
                        continue;

                    activeYoyo = projectile;
                }
            }

            if (activeYoyo == null)
                return;

            DetachYoyo(activeYoyo);
        }

        // YOYO CHECK

        private static bool IsRegisteredYoyo(int projectileType)
        {
            if (projectileType < 0)
                return false;

            if (projectileType >=
                ProjectileID.Sets.YoyosMaximumRange.Length)
            {
                return false;
            }

            return ProjectileID.Sets
                .YoyosMaximumRange[projectileType] > 0f;
        }

        // DETACH YOYO

        private void DetachYoyo(Projectile yoyo)
        {
            if (yoyo == null || !yoyo.active)
                return;

            int sourceYoyoType =
                yoyo.type;

            // VERIFY YOYO

            if (!IsRegisteredYoyo(sourceYoyoType))
                return;

            float maximumStringLength =
                ProjectileID.Sets
                    .YoyosMaximumRange[
                        sourceYoyoType
                    ];

            if (maximumStringLength <= 0f)
                return;

            // COPY LIVE STATE

            Vector2 position =
                yoyo.Center;

            Vector2 velocity =
                yoyo.velocity;

            int damage =
                yoyo.damage;

            float knockback =
                yoyo.knockBack;

            DamageClass damageType =
                yoyo.DamageType;

            float rotation =
                yoyo.rotation;

            float scale =
                yoyo.scale;

            int alpha =
                yoyo.alpha;

            int spriteDirection =
                yoyo.spriteDirection;

            // SPAWN DETACHED YOYO

            int ropeType =
                ModContent.ProjectileType<
                    StringCutterRopeProjectile>();

            int index =
                Projectile.NewProjectile(
                    yoyo.GetSource_FromThis(),
                    position,
                    velocity,
                    ropeType,
                    damage,
                    knockback,
                    Player.whoAmI,
                    sourceYoyoType,
                    maximumStringLength,
                    (float)StringCutterType
                );

            if (index < 0 ||
                index >= Main.maxProjectiles)
            {
                return;
            }

            Projectile detached =
                Main.projectile[index];

            // COPY YOYO COMBAT STATE

            detached.DamageType =
                damageType;

            detached.rotation =
                rotation;

            detached.scale =
                scale;

            detached.alpha =
                alpha;

            detached.spriteDirection =
                spriteDirection;

            detached.netUpdate =
                true;

            // CUT FX: white snap line across the cut, snip click, sparks
            // recoiling along both string ends (visual + audio only: the
            // detach above is untouched).
            Vector2 cutDir =
                (position - Player.Center).SafeNormalize(Vector2.UnitX);
            Projectiles.MiniYoyoFx.FlashFX(
                position,
                Color.White,
                0.8f
            );
            Projectiles.YoyoParticles.SpawnSlashLine(
                position,
                cutDir.ToRotation(),
                36f,
                Color.White,
                2.5f,
                8
            );
            for (int i = -1; i <= 1; i += 2)
            {
                Dust spark =
                    Dust.NewDustPerfect(
                        position,
                        DustID.WhiteTorch,
                        cutDir * (i * 4f),
                        100,
                        default,
                        1f
                    );

                spark.noGravity =
                    true;
            }
            SoundEngine.PlaySound(
                SoundID.Item37 with { Pitch = 0.6f },
                position
            );

            // SERRATED

            if (StringCutterType ==
                StringCutterType.Serrated)
            {
                SpawnSerratedFibers(
                    position,
                    damage
                );
            }

            // REMOVE ORIGINAL

            yoyo.Kill();

            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendData(
                    MessageID.SyncProjectile,
                    -1,
                    -1,
                    null,
                    yoyo.whoAmI
                );

                NetMessage.SendData(
                    MessageID.SyncProjectile,
                    -1,
                    -1,
                    null,
                    index
                );
            }
        }

        // SERRATED FIBERS

        private void SpawnSerratedFibers(
            Vector2 position,
            int yoyoDamage)
        {
            int fiberType =
                ModContent.ProjectileType<
                    StringCutterFiberProjectile>();

            const int FiberCount = 16;

            int fiberDamage =
                System.Math.Max(
                    1,
                    yoyoDamage / 2
                );

            for (int i = 0; i < FiberCount; i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    FiberCount;

                angle +=
                    Main.rand.NextFloat(
                        -0.10f,
                        0.10f
                    );

                Vector2 direction =
                    angle.ToRotationVector2();

                Vector2 spawnPosition =
                    position +
                    direction *
                    Main.rand.NextFloat(
                        4f,
                        12f
                    );

                Vector2 velocity =
                    direction *
                    Main.rand.NextFloat(
                        2.5f,
                        5f
                    );

                int index =
                    Projectile.NewProjectile(
                        Player.GetSource_FromThis(),
                        spawnPosition,
                        velocity,
                        fiberType,
                        fiberDamage,
                        1f,
                        Player.whoAmI
                    );

                if (index >= 0 &&
                    index < Main.maxProjectiles)
                {
                    Main.projectile[index].netUpdate =
                        true;
                }
            }

            // BLEEDING

            const float Radius =
                5f * 16f;

            float radiusSquared =
                Radius * Radius;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc =
                    Main.npc[i];

                if (!npc.active ||
                    npc.friendly ||
                    npc.dontTakeDamage)
                {
                    continue;
                }

                if (Vector2.DistanceSquared(
                        npc.Center,
                        position) >
                    radiusSquared)
                {
                    continue;
                }

                npc.AddBuff(
                    BuffID.Bleeding,
                    180
                );
            }
        }
    }
}