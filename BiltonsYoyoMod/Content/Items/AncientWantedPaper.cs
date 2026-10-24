using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.SubBosses;

namespace BiltonsYoyoMod.Content.Items
{
	// Summons the Ancient Duelist. Single non-consumable summons, like the
	// Old Wanted Paper: it never runs out, it just calls him again.
	public class AncientWantedPaper : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Content/Items/AncientWantedPaper";

		public override void SetDefaults()
		{
			Item.width = 22;
			Item.height = 26;
			Item.rare = ItemRarityID.LightPurple;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.UseSound = SoundID.Item6;
            Item.consumable = false;
            Item.maxStack = 1;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void AddRecipes()
        {
            // Difficult post-Golem writ (shop alternative: Cultist 10g).
            // Ancient scraps flow pre-Duelist via the Cultist's shop and the
            // parts-to-scraps conversion, so this never gates itself.
            CreateRecipe()
                .AddIngredient<global::BiltonsYoyoMod.Items.Materials.AncientScraps>(12)
                .AddIngredient(ItemID.Ectoplasm, 6)
                .AddIngredient(ItemID.SoulofNight, 4)
                .AddIngredient(ItemID.SoulofLight, 4)
                .AddTile(TileID.MythrilAnvil)
                .AddCondition(Condition.DownedGolem)
                .Register();
        }

		public override bool CanUseItem(Player player)
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<AncientDuelist>()) {
					Main.NewText("The Duelist is already here.", 200, 150, 255);
					return false;
				}
			}
			return true;
		}

		public override bool? UseItem(Player player)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				Vector2 pos = player.Center + new Vector2(0, -220f);
				NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem), (int)pos.X, (int)pos.Y, ModContent.NPCType<AncientDuelist>());
			}
			return true;
		}
	}
}
