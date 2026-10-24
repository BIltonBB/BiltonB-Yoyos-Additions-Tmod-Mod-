using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;
using BiltonsYoyoMod.Content.NPCs.Bosses;

namespace BiltonsYoyoMod.Content.Projectiles
{
    public class StoneGuardianYoyo : ModProjectile
    {
        private const float TrackMaxSpeed = 12f;
        private const float TrackMinSpeed = 3f;
        private const float TrackAccelRange = 300f;
        private const float TrackDecelRange = 200f;
        private const float StraightSpeed = 16f;

        // Fix #8: A2b/AB2b (mode 1, spawned offset from the Guardian and flung straight out at
        // high speed) could end up far enough away by the time they were meant to return that
        // the old 45-tick return window / 75-tick hard timeout wasn't enough time to actually
        // travel back — they'd just get force-killed mid-flight, i.e. "disappear" instead of
        // visibly returning. Both numbers are widened, and the return-lerp is stronger, so a
        // yoyo that's a long way out still reliably makes it home instead of timing out first.
        private const int ReturnWindow = 70;
        private const float ReturnSpeed = 22f;
        private const int ReturnHardTimeout = 220;

        private readonly List<Vector2> trail = new List<Vector2>();
        private bool returning;
        private int hardTimeout;

        private readonly Vector2[] history = new Vector2[31];
        private int historyIndex;
        private bool historyFilled;
        private const float DelayedCardinalSpeed = 9f;

        // Addition #7 (AP1, Phase 3): permanent orbiters never enter the "returning" logic at
        // all — they just orbit until the boss (or death cleanup) kills them.
        private float orbitAngle;
        private bool orbitInit;
        private const float AP1OrbitRadius = 256f;
        private const float AP1OrbitSpeed = 0.09f;

        public override string Texture => "BiltonsYoyoMod/Content/Projectiles/StoneGuardianYoyo";

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
            Projectile.light = 1f;
        }

        private NPC OwningGuardian()
        {
            int ownerIndex = (int)Projectile.ai[1];
            if (ownerIndex < 0 || ownerIndex >= Main.maxNPCs) return null;
            NPC n = Main.npc[ownerIndex];
            return n.active && n.type == ModContent.NPCType<YoyoGuardian>() ? n : null;
        }

        public override void AI()
        {
            Projectile.rotation += 0.25f;
            LightingHelper.SafeAddLight(Projectile.Center, 1f, 1f, 1f); // fix #1

            NPC guardian = OwningGuardian();
            if (guardian == null)
            {
                Projectile.Kill();
                return;
            }

            int mode = (int)Projectile.ai[2];

            // Addition #7: AP1's permanent orbiters skip the return/timeout machinery entirely.
            if (mode == 3)
            {
                RunOrbitMovement(guardian);
                PurpleTrailHelper.Cache(trail, Projectile.Center);
                return;
            }

            if (!returning && Projectile.timeLeft <= ReturnWindow)
            {
                returning = true;
                hardTimeout = ReturnHardTimeout;
            }

            if (returning)
            {
                Projectile.timeLeft = 2;
                hardTimeout--;

                Vector2 toGuardian = guardian.Center - Projectile.Center;
                float dist = toGuardian.Length();

                if (dist < 32f || hardTimeout <= 0)
                {
                    Projectile.Kill();
                    return;
                }

                Vector2 dir = toGuardian / dist;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * ReturnSpeed, 0.18f);
            }
            else
            {
                if (mode == 1) RunStraightMovement();
                else if (mode == 2) RunDelayedCardinalMovement((int)Projectile.ai[0]);
                else RunTrackingMovement((int)Projectile.ai[0]);
            }

            PurpleTrailHelper.Cache(trail, Projectile.Center);
        }

        private void RunOrbitMovement(NPC guardian)
        {
            if (!orbitInit) { orbitAngle = Projectile.ai[0]; orbitInit = true; }
            orbitAngle += AP1OrbitSpeed;
            Projectile.Center = guardian.Center + orbitAngle.ToRotationVector2() * AP1OrbitRadius;
            Projectile.rotation += 0.4f;
            Projectile.timeLeft = 2; // refreshed every tick, same pattern as the arena box
        }

        private void RunStraightMovement()
        {
            if (Projectile.velocity.LengthSquared() < 0.01f) return;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity = dir * StraightSpeed;
        }

        private void RunTrackingMovement(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) return;
            Player target = Main.player[playerIndex];
            if (target == null || !target.active || target.dead) return;

            Vector2 toTarget = target.Center - Projectile.Center;
            float distance = toTarget.Length();
            if (distance < 1f) return;

            Vector2 dir = toTarget / distance;
            float desiredSpeed = TrackMaxSpeed;
            if (distance < TrackDecelRange)
                desiredSpeed = MathHelper.Lerp(TrackMinSpeed, TrackMaxSpeed, distance / TrackDecelRange);

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * desiredSpeed, 0.04f);
        }

        private void RunDelayedCardinalMovement(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers) return;
            Player target = Main.player[playerIndex];
            if (target == null || !target.active || target.dead) return;

            history[historyIndex] = target.Center;
            historyIndex = (historyIndex + 1) % history.Length;
            if (historyIndex == 0) historyFilled = true;

            Vector2 delayedTarget = historyFilled ? history[historyIndex] : target.Center;
            Vector2 diff = delayedTarget - Projectile.Center;

            if (System.Math.Abs(diff.X) > System.Math.Abs(diff.Y))
                Projectile.velocity = new Vector2(System.Math.Sign(diff.X) * DelayedCardinalSpeed, 0f);
            else if (System.Math.Abs(diff.Y) > 4f)
                Projectile.velocity = new Vector2(0f, System.Math.Sign(diff.Y) * DelayedCardinalSpeed);
            else
                Projectile.velocity = Vector2.Zero;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            NPC guardian = OwningGuardian();
            if (guardian != null)
                YoyoDrawing.DrawString(guardian.Center, Projectile.Center);

            PurpleTrailHelper.Draw(trail);

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation, texture.Size() * 0.5f, 1f, SpriteEffects.None, 0f);

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if ((int)Projectile.ai[2] == 3) return; // AP1 orbiters don't spawn counterweights

            NPC guardian = OwningGuardian();
            if (guardian == null) return;

            int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<CounterweightYG>(), 0, 0f, 255);

            if (p < 0 || p >= Main.maxProjectiles) return;

            Main.projectile[p].ai[0] = Main.rand.NextFloat(MathHelper.TwoPi);
            Main.projectile[p].ai[1] = guardian.whoAmI;
            Main.projectile[p].localAI[0] = Main.rand.NextFloat(0.04f, 0.09f) * (Main.rand.NextBool() ? 1f : -1f);
            Main.projectile[p].netUpdate = true;
        }
    }
}
