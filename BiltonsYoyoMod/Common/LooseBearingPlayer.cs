using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Common
{
    public class LooseBearingPlayer : ModPlayer
    {
        public bool looseBearingsEquipped;

        // RETURN / RELEASE STATE

        private bool wasChannelingLastTick;
        private bool bearingReleasedThisThrow;

        // RESET

        public override void ResetEffects()
        {
            looseBearingsEquipped = false;
        }

        // UPDATE

        public override void PostUpdate()
        {
            Projectile yoyo =
                FindActiveYoyo();

            bool hasYoyo =
                yoyo != null &&
                yoyo.active;

            // RESET RELEASE LOCK

            if (Player.channel)
            {
                bearingReleasedThisThrow = false;
            }

            // DETECT YOYO RELEASE

            bool justReleased =
                wasChannelingLastTick &&
                !Player.channel;

            if (
                looseBearingsEquipped &&
                hasYoyo &&
                justReleased &&
                !bearingReleasedThisThrow
            )
            {
                SpawnBearings(yoyo);

                bearingReleasedThisThrow = true;
            }

            // STORE CHANNEL STATE

            wasChannelingLastTick =
                Player.channel;
        }

        // FIND ACTIVE YOYO

        private Projectile FindActiveYoyo()
        {
            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile projectile =
                    Main.projectile[i];

                if (!projectile.active)
                    continue;

                if (
                    projectile.owner !=
                    Player.whoAmI
                )
                {
                    continue;
                }

                if (
                    projectile.aiStyle != ProjAIStyleID.Yoyo
                )
                {
                    continue;
                }

                return projectile;
            }

            return null;
        }

        // NORMAL YOYO BEARINGS

        private void SpawnBearings(
            Projectile yoyo)
        {
            if (
                yoyo == null ||
                !yoyo.active
            )
            {
                return;
            }

            // YOYO DUPLICATOR

            YoyoDuplicatorPlayer duplicator =
                Player.GetModPlayer<
                    YoyoDuplicatorPlayer>();

            int shots =
                duplicator.GetCopies();

            // SPAWN FROM YOYO
            // Every equipped Loose Bearing variant is handled
            // independently.

            SpawnBearingVariant(
                yoyo,
                LooseBearingData.Wooden,
                GetEquippedVariant(
                    LooseBearingData.Wooden),
                shots);

            SpawnBearingVariant(
                yoyo,
                LooseBearingData.Hellstone,
                GetEquippedVariant(
                    LooseBearingData.Hellstone),
                shots);

            SpawnBearingVariant(
                yoyo,
                LooseBearingData.Mythril,
                GetEquippedVariant(
                    LooseBearingData.Mythril),
                shots);

            SpawnBearingVariant(
                yoyo,
                LooseBearingData.Hallowed,
                GetEquippedVariant(
                    LooseBearingData.Hallowed),
                shots);

            SpawnBearingVariant(
                yoyo,
                LooseBearingData.Luminite,
                GetEquippedVariant(
                    LooseBearingData.Luminite),
                shots);

            SpawnBearingVariant(
                yoyo,
                LooseBearingData.Meteorite,
                GetEquippedVariant(
                    LooseBearingData.Meteorite),
                shots);

            SpawnBearingVariant(
                yoyo,
                LooseBearingData.Chlorophyte,
                GetEquippedVariant(
                    LooseBearingData.Chlorophyte),
                shots);
        }

        // SPAWN ONE EQUIPPED VARIANT

        private void SpawnBearingVariant(
            Projectile yoyo,
            int variant,
            bool equipped,
            int shots)
        {
            if (!equipped)
                return;

            float chance =
                LooseBearingData.GetChance(
                    variant);

            Vector2 spawnPosition =
                yoyo.Center;

            for (
                int i = 0;
                i < shots;
                i++
            )
            {
                if (
                    Main.rand.NextFloat() >=
                    chance
                )
                {
                    continue;
                }

                SpawnBearingProjectile(
                    spawnPosition,
                    variant,
                    "LooseBearings");
            }
        }

        // KEVLAR

        public void TriggerBearingFromPosition(
            Vector2 position)
        {
            SpawnSpecialBearing(
                position,
                "KevlarLooseBearing");
        }

        // FINGER LOOP

        public void TriggerBearingFromFingerLoop(
            Vector2 position)
        {
            // Finger Loop remains exactly ONE bearing.
            // No normal bearing chance.
            // No Yoyo Duplicator.

            SpawnSpecialBearing(
                position,
                "FingerLoopExtraBearing");
        }

        // SPECIAL BEARING

        private void SpawnSpecialBearing(
            Vector2 position,
            string sourceName)
        {
            // Every equipped Loose Bearing variant can be used.
            // The special trigger still creates only ONE bearing
            // per equipped variant.

            SpawnSpecialVariant(
                position,
                LooseBearingData.Wooden,
                GetEquippedVariant(
                    LooseBearingData.Wooden),
                sourceName);

            SpawnSpecialVariant(
                position,
                LooseBearingData.Hellstone,
                GetEquippedVariant(
                    LooseBearingData.Hellstone),
                sourceName);

            SpawnSpecialVariant(
                position,
                LooseBearingData.Mythril,
                GetEquippedVariant(
                    LooseBearingData.Mythril),
                sourceName);

            SpawnSpecialVariant(
                position,
                LooseBearingData.Hallowed,
                GetEquippedVariant(
                    LooseBearingData.Hallowed),
                sourceName);

            SpawnSpecialVariant(
                position,
                LooseBearingData.Luminite,
                GetEquippedVariant(
                    LooseBearingData.Luminite),
                sourceName);

            SpawnSpecialVariant(
                position,
                LooseBearingData.Meteorite,
                GetEquippedVariant(
                    LooseBearingData.Meteorite),
                sourceName);

            SpawnSpecialVariant(
                position,
                LooseBearingData.Chlorophyte,
                GetEquippedVariant(
                    LooseBearingData.Chlorophyte),
                sourceName);
        }

        // SPAWN SPECIAL VARIANT

        private void SpawnSpecialVariant(
            Vector2 position,
            int variant,
            bool equipped,
            string sourceName)
        {
            if (!equipped)
                return;

            SpawnBearingProjectile(
                position,
                variant,
                sourceName);
        }

        // SPAWN BEARING PROJECTILE

        private void SpawnBearingProjectile(
            Vector2 position,
            int variant,
            string sourceName)
        {
            Vector2 direction =
                Main.rand.NextVector2CircularEdge(
                    1f,
                    1f);

            if (
                direction.LengthSquared() <
                0.001f
            )
            {
                direction =
                    Vector2.UnitX;
            }

            direction.Normalize();

            float speed =
                LooseBearingData.GetSpeed(
                    variant);

            int damage =
                LooseBearingData.GetDamage(
                    variant);

            int projectileType =
                LooseBearingData.GetProjectileType(
                    variant);

            Projectile.NewProjectile(
                Player.GetSource_Misc(
                    sourceName),
                position,
                direction * speed,
                projectileType,
                damage,
                1f,
                Player.whoAmI);
        }

        // CHECK EQUIPPED VARIANT

        private bool GetEquippedVariant(
            int variant)
        {
            LooseBearingLoadoutPlayer loadout =
                Player.GetModPlayer<
                    LooseBearingLoadoutPlayer>();

            return variant switch
            {
                LooseBearingData.Wooden =>
                    loadout.wooden,

                LooseBearingData.Hellstone =>
                    loadout.hellstone,

                LooseBearingData.Mythril =>
                    loadout.mythril,

                LooseBearingData.Hallowed =>
                    loadout.hallowed,

                LooseBearingData.Luminite =>
                    loadout.luminite,

                LooseBearingData.Meteorite =>
                    loadout.meteorite,

                LooseBearingData.Chlorophyte =>
                    loadout.chlorophyte,

                _ => false
            };
        }
    }

    // LOADOUT

    public class LooseBearingLoadoutPlayer
        : ModPlayer
    {
        public bool wooden;
        public bool meteorite;
        public bool hellstone;
        public bool mythril;
        public bool hallowed;
        public bool chlorophyte;
        public bool luminite;

        public override void ResetEffects()
        {
            wooden = false;
            meteorite = false;
            hellstone = false;
            mythril = false;
            hallowed = false;
            chlorophyte = false;
            luminite = false;
        }
    }

    // DATA

    public static class LooseBearingData
    {
        public const int Wooden = 0;
        public const int Hellstone = 1;
        public const int Mythril = 2;
        public const int Hallowed = 3;
        public const int Luminite = 4;

        public const int Meteorite = 5;
        public const int Chlorophyte = 6;

        public static float GetChance(int variant)
        {
            return variant switch
            {
                Wooden => 0.25f,
                Hellstone => 0.45f,
                Mythril => 0.60f,
                Hallowed => 0.75f,
                Luminite => 0.80f,
                Meteorite => 0.35f,
                Chlorophyte => 0.70f,
                _ => 0f
            };
        }

        public static int GetDamage(int variant)
        {
            return variant switch
            {
                Wooden => 15,
                Hellstone => 30,
                Mythril => 50,
                Hallowed => 80,
                Luminite => 200,
                Meteorite => 22,
                Chlorophyte => 110,
                _ => 0
            };
        }

        public static float GetSpeed(int variant)
        {
            return variant switch
            {
                Wooden => 5f,
                Hellstone => 6.5f,
                Mythril => 8f,
                Hallowed => 11f,
                Luminite => 15f,
                Meteorite => 7f,
                Chlorophyte => 12.5f,
                _ => 5f
            };
        }

        public static int GetProjectileType(int variant)
        {
            return variant switch
            {
                Wooden =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsAW>(),

                Hellstone =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsAHE>(),

                Mythril =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsAM>(),

                Hallowed =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsAHA>(),

                Luminite =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsAL>(),

                Meteorite =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsAMI>(),

                Chlorophyte =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsACH>(),

                _ =>
                    ModContent.ProjectileType<
                        Projectiles.LooseBearingsAW>()
            };
        }
    }
}