using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Thin horizontal string wrapped around one Polyester target.
    /// The visual scales with the target's size.
    /// </summary>
    public class WildStringRingProj : ModProjectile
    {
        public override string Texture => "BiltonsYoyoMod/Items/WildStringsAP";

        private NPC Target => GetTarget((int)Projectile.ai[0]);

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10 * 60;
        }

        private static NPC GetTarget(int index)
        {
            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[index];
            if (!npc.active || npc.friendly || npc.boss || npc.dontTakeDamage)
                return null;

            return npc;
        }

        public override void AI()
        {
            NPC npc = Target;
            if (npc == null)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = npc.Center;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            NPC npc = Target;
            if (npc == null)
                return false;

            Vector2 center = npc.Center - Main.screenPosition;

            // Thin horizontal wrap; scale with the enemy's size.
            float horizontalHalf = MathHelper.Clamp(npc.width * 0.72f, 14f, 110f);
            float thickness = MathHelper.Clamp(npc.height * 0.045f, 2.0f, 4.0f);

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Color color = new Color(245, 200, 80, 220);
            Rectangle source = texture.Bounds;

            Main.EntitySpriteDraw(
                texture,
                center,
                source,
                color,
                0f,
                source.Size() * 0.5f,
                new Vector2((horizontalHalf * 2f) / source.Width, thickness / source.Height),
                SpriteEffects.None,
                0f);

            return false;
        }
    }
}
