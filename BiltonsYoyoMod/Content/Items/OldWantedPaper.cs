using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Items
{
    // Fix #12: was consumable with maxStack 20 — now a non-consumable, stack-1 summon item.
    // Fix #1: spawns one of each distinct cowboy type instead of three copies of the same NPC.
    public class OldWantedPaper : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 26;
            Item.rare = ItemRarityID.Blue;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.UseSound = SoundID.Item6;
            Item.consumable = false; // fix #12
            Item.maxStack = 1;       // fix #12
            Item.value = Item.sellPrice(silver: 50);
        }

        public override void AddRecipes()
        {
            // Was completely unobtainable (blocked the Cowboys -> Six
            // Stringer path): desert materials are the biome gate, matching
            // the Master's "underground desert chests or crafting" hint.
            CreateRecipe()
                .AddIngredient(ItemID.SandBlock, 50)
                .AddIngredient(ItemID.Cactus, 15)
                .AddIngredient(ItemID.Cobweb, 10)
                .AddTile(TileID.WorkBenches)
                .Register();
        }

        public override bool CanUseItem(Player player)
        {
            // The cowboys only roam the desert.
            if (!player.ZoneDesert) {
                Main.NewText("The cowboys only roam the desert.", 220, 180, 100);
                return false;
            }
            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2[] offsets =
                {
                    new Vector2(-300, -40),
                    new Vector2(300, -40),
                    new Vector2(0, -220),
                };

                NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem), (int)(player.Center.X + offsets[0].X),
                    (int)(player.Center.Y + offsets[0].Y), ModContent.NPCType<NPCs.SubBosses.CowboyOfYoyos1>());
                NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem), (int)(player.Center.X + offsets[1].X),
                    (int)(player.Center.Y + offsets[1].Y), ModContent.NPCType<NPCs.SubBosses.CowboyOfYoyos2>());
                NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem), (int)(player.Center.X + offsets[2].X),
                    (int)(player.Center.Y + offsets[2].Y), ModContent.NPCType<NPCs.SubBosses.CowboyOfYoyos3>());
            }
            return true;
        }
    }
}