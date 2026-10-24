using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    // Animated inventory icons for the specials roster only: a breathing
    // glow behind the icon. Vanilla still draws the item itself (return true).
    public class YoyoSpecialIconGlobalItem : GlobalItem
    {
        public override bool InstancePerEntity => false;

        private static HashSet<int> specials;

        private static bool IsSpecialIcon(int type)
        {
            if (specials == null)
            {
                specials = new HashSet<int>();
                try
                {
                    specials.Add(ModContent.ItemType<DeveloperYoyo>());
                    specials.Add(ModContent.ItemType<BetsyYoyo>());
                    specials.Add(ModContent.ItemType<MartianYoyo>());
                    specials.Add(ModContent.ItemType<DarkMageYoyo>());
                    specials.Add(ModContent.ItemType<DreadnautilusYoyo>());
                }
                catch
                {
                }
            }
            return specials.Contains(type);
        }

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return IsSpecialIcon(entity.type);
        }

        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            // Shape-following outline: the item's own silhouette redrawn 6%
            // larger behind itself, tinted and pulsing per item (phase/speed
            // keyed by type so no two specials breathe alike). Strictly inside
            // the slot: nothing leaves the UI box. If this still reads bad,
            // delete this whole method (vanilla draws the icon regardless).
            try
            {
                Texture2D icon = Terraria.GameContent.TextureAssets.Item[item.type].Value;
                if (icon != null && !icon.IsDisposed)
                {
                    float phase = (item.type % 7) * 1.1f;
                    float speed = 0.06f + (item.type % 5) * 0.02f;
                    float pulse = 0.22f + 0.13f * (float)System.Math.Sin(Main.GameUpdateCount * speed + phase);
                    Color tint = item.rare == Terraria.ID.ItemRarityID.Expert
                        ? new Color(255, 170, 255)
                        : item.rare == Terraria.ID.ItemRarityID.Master ? new Color(255, 210, 140) : new Color(255, 225, 190);
                    spriteBatch.Draw(icon, position, frame, tint * pulse, 0f, origin, scale * 1.06f, SpriteEffects.None, 0f);
                }
            }
            catch
            {
            }
            return true;
        }
    }
}
