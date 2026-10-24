using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Projectiles
{
    // Friendly fast bolt fired as an opposite pair by GYTelegraphProj.
    public class GYPointedProj : ModProjectile
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/GYPointedProj";

        public const float FlySpeed = 14f;

        private readonly List<Vector2> trail = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180; // ~2500px at full speed, covers the 2000px telegraph
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
            Projectile.extraUpdates = 1;
            Projectile.light = 0.8f;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.9f);
            GYPurpleTrail.Cache(trail, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            GYPurpleTrail.Draw(trail);
            return true;
        }
    }
}
