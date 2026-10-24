using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.SubBosses;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content.Projectiles
{
    // Fix #2/#14 rewrite. ai[0] = returning flag (0/1), ai[1] = owner cowboy NPC whoAmI.
    public class CowboyYoyoProjectile : ModProjectile
    {
        private const int ReturnWindow = 60; // fix #2: starts flying home for the last 1s of its 5s life

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.aiStyle = -1;
        }

        public override void AI()
        {
            Projectile.rotation += 0.1f;

            int ownerIndex = (int)Projectile.ai[1];
            if (ownerIndex < 0 || ownerIndex >= Main.maxNPCs || !Main.npc[ownerIndex].active)
            {
                Projectile.Kill();
                return;
            }

            bool returning = Projectile.ai[0] == 1f;

            if (!returning && Projectile.timeLeft <= ReturnWindow)
            {
                returning = true;
                Projectile.ai[0] = 1f;
            }

            if (returning)
            {
                Projectile.timeLeft = 2; // fix #2: stay alive manually while flying home
                Vector2 toOwner = Main.npc[ownerIndex].Center - Projectile.Center;
                float dist = toOwner.Length();
                if (dist < 24f)
                {
                    Projectile.Kill();
                    return;
                }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner.SafeNormalize(Vector2.UnitY) * 9f, 0.15f);
            }
            else
            {
                Player closest = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
                Vector2 toPlayer = closest.Center - Projectile.Center;
                if (toPlayer != Vector2.Zero)
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, toPlayer.SafeNormalize(Vector2.UnitX) * 3.5f, 0.02f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int ownerIndex = (int)Projectile.ai[1];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs && Main.npc[ownerIndex].active)
                YoyoDrawing.DrawString(Main.npc[ownerIndex].Center, Projectile.Center);
            return true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int ownerIndex = (int)Projectile.ai[1];
            if (ownerIndex < 0 || ownerIndex >= Main.maxNPCs || !Main.npc[ownerIndex].active)
            {
                Projectile.Kill();
                return;
            }

			// Every yoyo hit slings a fresh counterweight: random orbit length
			// and speed each time, like a true vanilla counterweight.
			float orbitRadius = Main.rand.NextFloat(50f, 110f);
			float orbitSpeed = Main.rand.NextFloat(0.06f, 0.15f);
			if (Main.rand.NextBool())
				orbitSpeed = -orbitSpeed;

			int c = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
				ModContent.ProjectileType<HostileCounterweight>(), 20, 1f, Main.myPlayer);

			Main.projectile[c].ai[0] = ownerIndex;
			var counterweight = Main.projectile[c].ModProjectile as HostileCounterweight;
			counterweight?.SetOrbit(orbitRadius, orbitSpeed);

            Projectile.Kill(); // fix #2: the yoyo is consumed on a hit, same as a natural return
        }
    }

    // Fix #14: draws a connecting string back to its owner cowboy, same technique the yoyo
    // itself and the Guardian's own summons already use (Utils/YoyoDrawing.cs).
    public class HostileCounterweight : ModProjectile
    {
        private float orbitRadius = 50f;
        private float orbitAngle;
        private float orbitDirection = 1f;
        private float orbitSpeed = 0.05f;

        public void SetOrbit(float radius, float speed)
        {
            orbitRadius = radius;
            orbitSpeed = speed;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.tileCollide = true;
            Projectile.aiStyle = -1;
        }

        public override void AI()
        {
            int ownerNPC = (int)Projectile.ai[0];
            if (ownerNPC < 0 || ownerNPC >= Main.maxNPCs || !Main.npc[ownerNPC].active)
            {
                Projectile.Kill();
                return;
            }

            orbitAngle += orbitSpeed * orbitDirection;
            Projectile.Center = Main.npc[ownerNPC].Center + orbitAngle.ToRotationVector2() * orbitRadius;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            orbitDirection *= -1f;
            return false;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int ownerNPC = (int)Projectile.ai[0];
            if (ownerNPC >= 0 && ownerNPC < Main.maxNPCs && Main.npc[ownerNPC].active)
                YoyoDrawing.DrawString(Main.npc[ownerNPC].Center, Projectile.Center); // fix #14
            return true;
        }
    }
}