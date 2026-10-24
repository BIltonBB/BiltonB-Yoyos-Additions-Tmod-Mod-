using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content.Projectiles
{
    public class PointedYG : ModProjectile
    {
        private readonly List<Vector2> trail = new List<Vector2>();

        public override string Texture => "BiltonsYoyoMod/Content/Projectiles/PointedYG";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
            Projectile.extraUpdates = 1;
            Projectile.light = 1f;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            LightingHelper.SafeAddLight(Projectile.Center, 1f, 1f, 1f); // fix #1
            PurpleTrailHelper.Cache(trail, Projectile.Center); // fix #2

            if (Projectile.ai[0] > 0f)
            {
                Projectile.ai[0] -= 1f;

                if (Projectile.ai[0] > 0f)
                {
                    Projectile.velocity = Vector2.Zero;
                    return;
                }

                int ownerIndex = (int)Projectile.ai[1];

                if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs && Main.npc[ownerIndex].active)
                {
                    Vector2 toOwner = Main.npc[ownerIndex].Center - Projectile.Center;
                    Projectile.velocity = toOwner.SafeNormalize(Vector2.UnitY) * 10f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            PurpleTrailHelper.Draw(trail);
            return true;
        }
    }
}
