using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Projectiles.TownNPC
{
	// Bearings fired by Billy the Bearing (town pet) once upgraded.
	// BTBBearing: standard bearing shot. BTBUBearing: faster, half gravity, more damage.
	public class BTBBearing : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.damage = 20;
			Projectile.penetrate = 2;
			Projectile.tileCollide = true;
			Projectile.timeLeft = 300;
			Projectile.aiStyle = ProjAIStyleID.ThrownProjectile;
			AIType = ProjectileID.Shuriken;
		}

		public override void AI()
		{
			Lighting.AddLight(Projectile.Center, 0.15f, 0.15f, 0.18f);
			Projectile.rotation += 0.4f;
		}
	}

	public class BTBUBearing : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.damage = 35;
			Projectile.penetrate = 3;
			Projectile.tileCollide = true;
			Projectile.timeLeft = 300;
		}

		public override void AI()
		{
			Projectile.rotation += 0.5f;
			Lighting.AddLight(Projectile.Center, 0.2f, 0.2f, 0.25f);
			Projectile.velocity.Y += 0.08f; // Half gravity.
			if (Projectile.velocity.Y > 8f)
				Projectile.velocity.Y = 8f;
		}
	}
}
