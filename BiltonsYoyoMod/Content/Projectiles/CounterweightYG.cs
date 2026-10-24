using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;
using BiltonsYoyoMod.Content.NPCs.Bosses;

namespace BiltonsYoyoMod.Content.Projectiles
{
    public class CounterweightYG : ModProjectile
    {
        private const float MinOrbitRadius = 140f;
        private const float MaxOrbitRadius = 220f;

        public override string Texture => "BiltonsYoyoMod/Content/Projectiles/CounterweightYG";

        private readonly List<Vector2> trail = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.extraUpdates = 1;
            Projectile.light = 1f;
            Projectile.damage = 50;
        }

        public override void AI()
        {
            int ownerIndex = (int)Projectile.ai[1];

            if (ownerIndex < 0 || ownerIndex >= Main.maxNPCs ||
                !Main.npc[ownerIndex].active ||
                Main.npc[ownerIndex].type != ModContent.NPCType<YoyoGuardian>())
            {
                Projectile.Kill();
                return;
            }

            if (Projectile.localAI[0] == 0f)
                Projectile.localAI[0] = 0.05f;

            if (Projectile.localAI[1] < MinOrbitRadius || Projectile.localAI[1] > MaxOrbitRadius)
                Projectile.localAI[1] = MathHelper.Clamp(
                    Projectile.localAI[1] == 0f ? Main.rand.NextFloat(MinOrbitRadius, MaxOrbitRadius) : Projectile.localAI[1],
                    MinOrbitRadius, MaxOrbitRadius);

            Projectile.ai[0] += Projectile.localAI[0];

            Vector2 orbitOffset = Projectile.ai[0].ToRotationVector2() * Projectile.localAI[1];
            Projectile.Center = Main.npc[ownerIndex].Center + orbitOffset;

            PurpleTrailHelper.Cache(trail, Projectile.Center);
            LightingHelper.SafeAddLight(Projectile.Center, 0.5f, 0.15f, 0.85f); // fix #1
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.localAI[0] *= -1f;
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            Projectile.localAI[0] *= -1f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int ownerIndex = (int)Projectile.ai[1];

            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs &&
                Main.npc[ownerIndex].active &&
                Main.npc[ownerIndex].type == ModContent.NPCType<YoyoGuardian>())
            {
                YoyoDrawing.DrawString(Main.npc[ownerIndex].Center, Projectile.Center);
            }

            PurpleTrailHelper.Draw(trail);
            return true;
        }
    }
}
