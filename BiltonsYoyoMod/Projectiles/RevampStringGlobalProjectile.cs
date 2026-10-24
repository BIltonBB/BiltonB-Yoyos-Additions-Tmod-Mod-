using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class RevampStringGlobalProjectile : GlobalProjectile
    {
        private const float StringWidth = 18f;
        private const float SampleSpacing = 6f;

        private const int StringHitCooldown = 6;

        private const int TangledDamage = 1;
        private const int BarbedDamage = 5;

        private readonly int[] stringHitCooldowns =
            new int[Main.maxNPCs];

        public override bool InstancePerEntity => true;

        public override void PostAI(Projectile projectile)
        {
            for (int i = 0; i < stringHitCooldowns.Length; i++)
            {
                if (stringHitCooldowns[i] > 0)
                    stringHitCooldowns[i]--;
            }

            if (!projectile.active ||
                !projectile.friendly ||
                projectile.aiStyle != ProjAIStyleID.Yoyo)
            {
                return;
            }

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player owner = Main.player[projectile.owner];

            if (!owner.active || owner.dead)
                return;

            RevampPlayer revamp =
                owner.GetModPlayer<RevampPlayer>();

            if (!revamp.Tangled && !revamp.Barbed)
                return;

            Vector2 stringStart = owner.MountedCenter;
            Vector2 stringEnd = projectile.Center;

            Vector2 difference = stringEnd - stringStart;
            float stringLength = difference.Length();

            if (stringLength <= 1f)
                return;

            Vector2 direction =
                difference / stringLength;

            int damage =
                revamp.Barbed
                    ? BarbedDamage
                    : TangledDamage;

            int sampleCount =
                (int)(stringLength / SampleSpacing) + 1;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC target = Main.npc[i];

                if (!target.active ||
                    target.friendly ||
                    target.dontTakeDamage ||
                    target.lifeMax <= 5 ||
                    !target.CanBeChasedBy())
                {
                    continue;
                }

                if (stringHitCooldowns[i] > 0)
                    continue;

                Rectangle hitbox = target.Hitbox;

                int inflate =
                    (int)(StringWidth * 0.5f);

                hitbox.Inflate(inflate, inflate);

                bool collided = false;

                for (int sample = 0; sample <= sampleCount; sample++)
                {
                    float distance =
                        MathHelper.Min(
                            sample * SampleSpacing,
                            stringLength
                        );

                    Vector2 point =
                        stringStart +
                        direction * distance;

                    if (!hitbox.Contains(point.ToPoint()))
                        continue;

                    collided = true;
                    break;
                }

                if (!collided)
                    continue;

                stringHitCooldowns[i] =
                    StringHitCooldown;

                int hitDirection =
                    target.Center.X >= owner.Center.X
                        ? 1
                        : -1;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    owner.ApplyDamageToNPC(
                        target,
                        damage,
                        0f,
                        hitDirection,
                        false,
                        DamageClass.MeleeNoSpeed
                    );

                    target.AddBuff(
                        BuffID.Bleeding,
                        5 * 60
                    );

                    target.netUpdate = true;
                }
            }
        }
    }
}