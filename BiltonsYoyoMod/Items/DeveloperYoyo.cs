using System.Collections.Generic;
using BiltonsYoyoMod.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class DeveloperYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 55;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.knockBack = 15f;

            Item.width = 30;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.DeveloperYoyoProj>();
            Item.shootSpeed = 16f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Expert; // Rainbow (ItemRarityID.Rainbow does not exist in this tModLoader version)
            Item.value = Item.buyPrice(gold: 20);
        }

        // Rod Mode (the joke): RIGHT-CLICK IN THE INVENTORY toggles the yoyo
        // into a fishing rod. It used to be an alt-use in the world, which stole
        // the Magical Tech's right-click and made the two systems fight over the
        // same button. Inventory right-click is inert for every other item, so
        // the mode switch can never be confused with a cast.
        // Tooltip brags 999999999% power; the field stays 5, so catches are
        // always bottom-tier ("1% bait stuff"). Texture stays the yoyo: held-item
        // art cannot be swapped per-state without a second item (documented limit).
        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            DevRodPlayer rp = player.GetModPlayer<DevRodPlayer>();
            if (System.Math.Abs((int)Main.GameUpdateCount - rp.LastToggle) <= 20)
                return;
            rp.RodMode = !rp.RodMode;
            rp.LastToggle = (int)Main.GameUpdateCount;
            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = rp.RodMode ? 0.3f : -0.3f }, player.Center);
        }

        public override void HoldItem(Player player)
        {
            // Applied every held tick (HoldItem runs before use), restored the
            // same way. Instance fields only ever matter while held/used.
            if (player.GetModPlayer<DevRodPlayer>().RodMode)
            {
                Item.shoot = ProjectileID.FishingBobber;
                Item.shootSpeed = 12f;
                Item.fishingPole = 25;
                Item.channel = false;
                Item.noUseGraphic = false;
            }
            else
            {
                Item.shoot = ModContent.ProjectileType<Projectiles.DeveloperYoyoProj>();
                Item.shootSpeed = 16f;
                Item.fishingPole = 0;
                Item.channel = true;
                Item.noUseGraphic = true;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            bool rod = false;
            if (Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers)
            {
                Player local = Main.player[Main.myPlayer];
                if (local != null && local.active)
                    rod = local.GetModPlayer<DevRodPlayer>().RodMode;
            }
            tooltips.Add(new TooltipLine(Mod, "DevRod", rod
                ? "Rod Mode: ON - 999999999% fishing power* (*results may be junk) - right-click in inventory to switch back"
                : "Right-click in inventory: Rod Mode (lies about 999999999% power)"));
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.HallowedBar, 12)
                .AddIngredient(ItemID.SoulofLight, 5)
                .AddIngredient(ItemID.SoulofNight, 5)
                .AddTile(TileID.MythrilAnvil)
                .AddCondition(Condition.Hardmode)
                .Register();
        }
    }
}
