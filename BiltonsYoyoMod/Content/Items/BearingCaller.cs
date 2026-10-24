using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Content.Items
{
    // Fix #7: manually starts the Bearing Party event, bypassing the biome/progression check
    // (force: true) — this is an explicit deliberate trigger, not a natural roll.
    public class BearingCaller : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.rare = ItemRarityID.Green;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.UseSound = SoundID.Item6;
            Item.consumable = true;
            Item.maxStack = 20;
            Item.value = Item.sellPrice(silver: 30);
        }

        public override void AddRecipes()
        {
            // Was completely unobtainable (only the rare natural party
            // remained): cavern party-starter for post-Skeletron.
            CreateRecipe()
                .AddIngredient(ItemID.StoneBlock, 30)
                .AddIngredient(ItemID.Wire, 10)
                .AddIngredient(ItemID.IronBar, 5)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedSkeletron)
                .Register();
        }

        public override bool? UseItem(Player player)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (BearingPartyEvent.Active)
                {
                    Main.NewText("A Bearing Party is already underway!", 200, 180, 100);
                    return false;
                }

                BearingPartyEvent.StartEvent(force: true);
            }
            return true;
        }
    }
}