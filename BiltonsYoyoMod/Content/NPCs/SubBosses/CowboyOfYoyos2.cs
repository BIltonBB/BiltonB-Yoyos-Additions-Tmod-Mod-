using Microsoft.Xna.Framework;
using Terraria;

namespace BiltonsYoyoMod.Content.NPCs.SubBosses
{
    // Fix #6: somewhat cares about danger — stays the furthest set distance from the player of
    // the three, and jumps over a nearby hostile yoyo rather than actively dodging sideways.
    public class CowboyOfYoyos2 : CowboyOfYoyosBase
    {
        public override string Texture =>
            "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY2";

        protected override int BaseLife => 550;
        protected override int BaseDamage => 20;
        protected override int BaseDefense => 12;
        protected override float PreferredDistance => 340f;

        public override float CounterweightOrbitRadius => 65f;
        public override float CounterweightOrbitSpeed => 0.035f;

        // TCOY2.png = 44x1350 = 25 frames of 44x54.
        protected override int TotalWalkFrames => 25;

        private const float JumpDangerRadius = 150f;

        protected override void RunMovement(Player target)
        {
            float dist =
                Vector2.Distance(
                    NPC.Center,
                    target.Center
                );

            float radialDir =
                dist > PreferredDistance ? 1f : -1f;

            Vector2 toPlayer =
                (target.Center - NPC.Center)
                .SafeNormalize(Vector2.UnitX);

            NPC.velocity.X = MathHelper.Lerp(
                NPC.velocity.X,
                toPlayer.X * radialDir * 2.6f,
                0.08f
            );

            Vector2? yoyoPos =
                FindNearestHostileYoyo(
                    out float yoyoDist
                );

            if (yoyoPos.HasValue &&
                yoyoDist < JumpDangerRadius &&
                NPC.velocity.Y == 0f)
            {
                NPC.velocity.Y = -8f;
            }

            if (NPC.velocity.Y == 0f &&
                Main.rand.NextFloat() < 0.01f)
            {
                NPC.velocity.Y = -6.5f;
            }
        }
    }
}