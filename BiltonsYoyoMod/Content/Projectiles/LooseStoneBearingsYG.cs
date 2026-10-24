using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content.Projectiles
{
    public class LooseStoneBearingsYG : ModProjectile
    {
        private readonly List<Vector2> trail = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.aiStyle = -1;
            Projectile.light = 1f;
        }

        public override void AI()
        {
            Projectile.velocity.Y += 0.125f; // fix #19: 50% of the previous 0.25f gravity
            Projectile.rotation += 0.15f * System.Math.Sign(Projectile.velocity.X == 0 ? 1 : Projectile.velocity.X);
            LightingHelper.SafeAddLight(Projectile.Center, 1f, 1f, 1f); // fix #1
            PurpleTrailHelper.Cache(trail, Projectile.Center); // fix #2
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.Kill();
            return false;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            PurpleTrailHelper.Draw(trail);
            return true;
        }
    }
}
