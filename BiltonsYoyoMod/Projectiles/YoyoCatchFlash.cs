using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Return-catch flash + click when a mini-boss yoyo re-enters hand range
    // after flying far. Distance-tracked per projectile, visual + audio only.
    public class YoyoCatchFlash : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private float lastDist = -1f;
        private bool stretched;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.aiStyle == ProjAIStyleID.Yoyo
                && YoyoStringHookSystem.TryGetStyle(entity.type, out _, out _, out _);
        }

        public override void AI(Projectile projectile)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;
            Player owner = Main.player[projectile.owner];
            if (owner == null || !owner.active)
                return;
            float d = Vector2.Distance(owner.Center, projectile.Center);
            // Rubber-band note: first crossing past ~260px twangs (resets under 200).
            if (!stretched && lastDist > 0f && d > 260f)
            {
                stretched = true;
                if (Main.myPlayer == projectile.owner)
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item37 with { Pitch = -0.5f }, projectile.Center);
            }
            else if (stretched && d < 200f)
            {
                stretched = false;
            }
            if (lastDist > 200f && d < 48f)
            {
                // Catch flash wears the yoyo's own string tint, not generic white.
                Color tint = Color.White;
                if (YoyoStringHookSystem.TryGetStyle(projectile.type, out _, out Color styleTint, out _))
                    tint = styleTint;
                MiniYoyoFx.FlashFX(projectile.Center, tint, 0.7f);
                YoyoParticles.SpawnRing(projectile.Center, 8f, 40f, 20, tint, 2f);
                Lighting.AddLight(owner.Center, tint.R / 255f * 0.7f, tint.G / 255f * 0.7f, tint.B / 255f * 0.7f);
                if (Main.myPlayer == projectile.owner)
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Grab, projectile.Center);
            }
            lastDist = d;
        }
    }
}
