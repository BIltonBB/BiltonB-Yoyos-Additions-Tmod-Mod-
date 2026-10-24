using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Bosses;

namespace BiltonsYoyoMod.Content.Items
{
    public class TrialOfTheGuardian : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.LightRed;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useTurn = true;
            Item.UseSound = SoundID.Roar;
            Item.consumable = false;
            Item.maxStack = 1;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void AddRecipes()
        {
            // Difficult post-Plantera trial construction (shop alternative:
            // Yoyo Master 25g). Dungeon spirits, chlorophyte and all three
            // souls - no Guardian drops, so it never gates itself.
            CreateRecipe()
                .AddIngredient(ItemID.Ectoplasm, 10)
                .AddIngredient(ItemID.ChlorophyteBar, 10)
                .AddIngredient(ItemID.SoulofFright, 5)
                .AddIngredient(ItemID.SoulofMight, 5)
                .AddIngredient(ItemID.SoulofSight, 5)
                .AddTile(TileID.AdamantiteForge)
                .AddCondition(Condition.DownedPlantera)
                .Register();
        }

        public override bool CanUseItem(Player player)
        {
            if (!player.ZoneDirtLayerHeight && !player.ZoneRockLayerHeight && !player.ZoneUnderworldHeight)
            {
                Main.NewText("The Trial of the Guardian only reacts deep underground.", 200, 180, 100);
                return false;
            }

            // A Guardian already raging: no silent double-summon, no
            // self-deactivating guardian and no missing arena.
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == ModContent.NPCType<NPCs.Bosses.YoyoGuardian>()
                    && npc.ModNPC is NPCs.Bosses.YoyoGuardian guardian && guardian.IsRealBoss) {
                    Main.NewText("A Guardian already rages elsewhere.", 255, 150, 100);
                    return false;
                }
            }

            // fix #3: refuse to summon a 3rd simultaneous real Guardian — this is what used to
            // crash the game. See YoyoGuardian.ActiveRealGuardianCount / MaxSimultaneousRealGuardians.
            if (YoyoGuardian.ActiveRealGuardianCount >= YoyoGuardian.MaxSimultaneousRealGuardians)
            {
                Main.NewText("The trial cannot handle more Guardians right now.", 200, 180, 100);
                return false;
            }

            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Fresh centered arena per fight: clear any leftover boxes
                // before the Guardian (and its EnsureExists) arrives.
                Content.Projectiles.GuardianArenaBox.ForgetHome();
                int boxType = ModContent.ProjectileType<Content.Projectiles.GuardianArenaBox>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile q = Main.projectile[i];
                    if (q.active && q.type == boxType) {
                        q.active = false;
                        q.netUpdate = true;
                    }
                }
                NPC.SpawnOnPlayer(player.whoAmI, ModContent.NPCType<NPCs.Bosses.YoyoGuardian>());
            }
            return true;
        }
    }
}
