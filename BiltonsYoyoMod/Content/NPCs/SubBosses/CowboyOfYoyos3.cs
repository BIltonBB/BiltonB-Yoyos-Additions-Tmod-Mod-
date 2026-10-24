using Microsoft.Xna.Framework;
using Terraria;

namespace BiltonsYoyoMod.Content.NPCs.SubBosses
{
    // Fix #7: doesn't care about danger at all — stays the closest of the three to the player,
    // and every 5s dashes straight at them, dealing contact damage through the dash.
    public class CowboyOfYoyos3 : CowboyOfYoyosBase
    {
        public override string Texture =>
            "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY3";

        protected override int BaseLife => 500;
        protected override int BaseDamage => 24;
        protected override int BaseDefense => 10;
        protected override float PreferredDistance => 140f;

        public override float CounterweightOrbitRadius => 30f;
        public override float CounterweightOrbitSpeed => 0.09f;

        // TCOY3.png = 44x1350 = 25 frames of 44x54.
        protected override int TotalWalkFrames => 25;

        private const int DashInterval = 300;

        private int dashTimer;
        private int dashLockout;

        protected override void RunMovement(Player target)
        {
            if (dashLockout > 0)
            {
                dashLockout--;
            }
            else
            {
                dashTimer++;

                if (dashTimer >= DashInterval)
                {
                    dashTimer = 0;
                    dashLockout = 20;

                    Vector2 dir =
                        (target.Center - NPC.Center)
                        .SafeNormalize(Vector2.UnitX);

                    NPC.velocity = dir * 11f;
                    return;
                }
            }

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
                toPlayer.X * radialDir * 3f,
                0.1f
            );

            if (NPC.velocity.Y == 0f &&
                Main.rand.NextFloat() < 0.01f)
            {
                NPC.velocity.Y = -6.5f;
            }
        }
    }
}