using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // First-throw marquee per yoyo per session (CombatText shout).
    // Owner-side only, visual only, announced once each.
    public class YoyoThrowMarquee : GlobalProjectile
    {
        private static readonly HashSet<string> announced = new HashSet<string>();

        public override bool InstancePerEntity => false;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.aiStyle == ProjAIStyleID.Yoyo && YoyoMarqueeNames(entity.type, out _, out _);
        }

        public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source)
        {
            if (Main.myPlayer != projectile.owner)
                return;
            if (!YoyoMarqueeNames(projectile.type, out string name, out Color color))
                return;
            // Family throw sting: one pitched whoosh per yoyo (index ladder).
            if (YoyoStringHookSystem.TryGetStringIndex(projectile.type, out int stingIdx))
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f + stingIdx * 0.05f }, projectile.Center);
            // Throw snap: hand flash on EVERY throw, marquee shout on the first.
            Player owner = Main.player[projectile.owner];
            if (owner != null && owner.active)
            {
                MiniYoyoFx.FlashFX(owner.Center, color, 0.6f);
                Lighting.AddLight(owner.Center, color.R / 255f * 0.6f, color.G / 255f * 0.6f, color.B / 255f * 0.6f);
            }
            if (announced.Contains(name))
                return;
            announced.Add(name);
            CombatText.NewText(projectile.getRect(), color, name + "!", false);
        }

        public static void Clear()
        {
            announced.Clear();
        }

        internal static bool YoyoMarqueeNames(int type, out string name, out Color color)
        {
            if (type == ModContent.ProjectileType<DarkMageYoyoProj>()) { name = "DARK MAGE"; color = new Color(190, 130, 255); return true; }
            if (type == ModContent.ProjectileType<TorchGodYoyoProj>()) { name = "TORCH GOD"; color = new Color(255, 190, 120); return true; }
            if (type == ModContent.ProjectileType<DutchmanYoyoProj>()) { name = "DUTCHMAN"; color = new Color(150, 200, 190); return true; }
            if (type == ModContent.ProjectileType<OgreYoyoProj>()) { name = "OGRE"; color = new Color(150, 230, 150); return true; }
            if (type == ModContent.ProjectileType<DreadnautilusYoyoProj>()) { name = "DREADNAUTILUS"; color = new Color(220, 80, 90); return true; }
            if (type == ModContent.ProjectileType<MourningWoodYoyoProj>()) { name = "MOURNING WOOD"; color = new Color(255, 180, 110); return true; }
            if (type == ModContent.ProjectileType<SantaYoyoProj>()) { name = "SANTA-NK1"; color = new Color(255, 110, 110); return true; }
            if (type == ModContent.ProjectileType<PumpkingYoyoProj>()) { name = "PUMPKING"; color = new Color(255, 190, 120); return true; }
            if (type == ModContent.ProjectileType<EverscreamYoyoProj>()) { name = "EVERSCREAM"; color = new Color(150, 230, 150); return true; }
            if (type == ModContent.ProjectileType<IceQueenYoyoProj>()) { name = "ICE QUEEN"; color = new Color(170, 225, 255); return true; }
            if (type == ModContent.ProjectileType<MartianYoyoProj>()) { name = "MARTIAN SAUCER"; color = new Color(150, 255, 190); return true; }
            if (type == ModContent.ProjectileType<BetsyYoyoProj>()) { name = "BETSY"; color = new Color(255, 170, 90); return true; }
            if (type == ModContent.ProjectileType<DeveloperYoyoProj>()) { name = "DEVELOPER"; color = new Color(200, 150, 255); return true; }
            name = null;
            color = Color.White;
            return false;
        }
    }

    public class YoyoMarqueeSystem : ModSystem
    {
        public override void OnWorldUnload()
        {
            YoyoThrowMarquee.Clear();
            MiniYoyoFx.ResetMilestones();
        }
    }
}
