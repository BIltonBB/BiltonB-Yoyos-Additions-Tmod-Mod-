using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace BiltonsYoyoMod.Content.NPCs.SubBosses
{
    public class CowboyOfYoyos1 : CowboyOfYoyosBase
    {
        public override string Texture =>
            "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY1";

        protected override int BaseLife => 450;
        protected override int BaseDamage => 18;
        protected override int BaseDefense => 8;
        protected override float PreferredDistance => 220f;

        public override float CounterweightOrbitRadius => 40f;
        public override float CounterweightOrbitSpeed => 0.05f;

        // TCOY1.png = 44x1350 = 25 frames of 44x54.
        protected override int TotalWalkFrames => 25;

        private const float DangerRadius = 220f;
        private const float DodgeRadius = 170f;
        private const float PredictRadius = 280f;
        private int dodgeCooldown;
        private int jumpsUsed;
        private int strafeFlipTimer;
        private float strafeSign = 1f;
        private Vector2 lastDashDir = Vector2.Zero;

        protected override void RunMovement(Player target)
        {
            // Grounded: jumps refresh.
            if (NPC.collideY)
                jumpsUsed = 0;
            // Sidestep dash away from incoming player yoyos, up to 3 jumps
            // before touching ground again.
            if (dodgeCooldown > 0) {
                dodgeCooldown--;
            }
            else if (jumpsUsed < 3) {
                Projectile incoming = FindIncomingPlayerShot(target, out bool closing, out float dist, out bool isYoyo);
                // Yoyos only: fast-closing ones get dodged from further out.
                if (!isYoyo)
                    incoming = null;
                float trigger = closing && dist < PredictRadius ? PredictRadius : DodgeRadius;
                if (incoming != null && dist < trigger) {
                    Vector2 away = (NPC.Center - incoming.Center).SafeNormalize(Vector2.UnitX);
                    // Sidestep: mostly perpendicular so it doesn't back into walls.
                    // Smarter: pick the tangent side with open space ahead.
                    Vector2 tangent = new Vector2(-away.Y, away.X);
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 probe = NPC.Center + tangent * side * 70f - new Vector2(0f, 20f);
                    Tile tile = Framing.GetTileSafely((int)(probe.X / 16f), (int)(probe.Y / 16f));
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated)
                        side = -side;
                    Vector2 dash = (away * 0.7f + tangent * side * 0.9f).SafeNormalize(Vector2.UnitX);
                    // Never dodge the same way twice: flip if aligned with last.
                    if (lastDashDir != Vector2.Zero && Vector2.Dot(dash, lastDashDir) > 0.5f) {
                        side = -side;
                        dash = (away * 0.7f + tangent * side * 0.9f).SafeNormalize(Vector2.UnitX);
                    }
                    lastDashDir = dash;
                    NPC.velocity = dash * 11f + new Vector2(0f, -2f);
                    dodgeCooldown = 20;
                    jumpsUsed++;
                    NPC.netUpdate = true;
                    // First jump kicks up a big dust burst so it reads.
                    int puffs = jumpsUsed == 1 ? 16 : 8;
                    for (int i = 0; i < puffs; i++) {
                        Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke, -away.X * 2f, -1f, 120, default, jumpsUsed == 1 ? 1.3f : 1f);
                        dust.noGravity = true;
                    }
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.DoubleJump, NPC.Center);
                    return;
                }
            }

            Vector2? yoyoPos =
                FindNearestHostileYoyo(
                    out float yoyoDist
                );

            if (yoyoPos.HasValue &&
                yoyoDist < DangerRadius)
            {
                Vector2 away =
                    (NPC.Center - yoyoPos.Value)
                    .SafeNormalize(Vector2.UnitX);

                NPC.velocity.X = MathHelper.Lerp(
                    NPC.velocity.X,
                    away.X * 3.2f,
                    0.18f
                );
            }
            else
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

                Vector2 tangent =
                    new Vector2(
                        -toPlayer.Y,
                        toPlayer.X
                    );

                // Unpredictable strafing: flip orbit direction every ~3s.
                if (++strafeFlipTimer >= 180) {
                    strafeFlipTimer = 0;
                    strafeSign = -strafeSign;
                    NPC.netUpdate = true;
                }

                Vector2 desired =
                    (toPlayer * radialDir +
                     tangent * 0.6f * strafeSign)
                    .SafeNormalize(Vector2.UnitX);

                NPC.velocity.X = MathHelper.Lerp(
                    NPC.velocity.X,
                    desired.X * 3.4f,
                    0.1f
                );
            }

            if (NPC.velocity.Y == 0f &&
                Main.rand.NextFloat() < 0.01f)
            {
                NPC.velocity.Y = -6.5f;
            }
        }

        // Nearest player-thrown danger, plus whether it is closing in fast.
        private Projectile FindIncomingPlayerShot(Player target, out bool closing, out float dist, out bool isYoyo)
        {
            Projectile best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || !p.friendly || p.hostile || p.owner != target.whoAmI)
                    continue;
                if (p.damage <= 0 || p.velocity.LengthSquared() < 4f)
                    continue;
                float d = Vector2.Distance(NPC.Center, p.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = p;
                }
            }
            closing = false;
            dist = bestDist;
            isYoyo = false;
            if (best != null) {
                isYoyo = best.aiStyle == ProjAIStyleID.Yoyo;
                if (best.velocity.LengthSquared() > 25f) {
                    Vector2 toMe = (NPC.Center - best.Center).SafeNormalize(Vector2.UnitX);
                    closing = Vector2.Dot(best.velocity.SafeNormalize(Vector2.UnitX), toMe) > 0.7f;
                }
            }
            return best;
        }
    }
}