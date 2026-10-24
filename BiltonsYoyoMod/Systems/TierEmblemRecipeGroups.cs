using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Systems
{
    public class TierEmblemRecipeGroups : ModSystem
    {
        public override void AddRecipeGroups()
        {
            RecipeGroup.RegisterGroup(
                "BiltonsYoyoMod:AnyEvilBar",
                new RecipeGroup(() => $"{Lang.GetItemNameValue(ItemID.DemoniteBar)} / {Lang.GetItemNameValue(ItemID.CrimtaneBar)}",
                    ItemID.DemoniteBar, ItemID.CrimtaneBar));

            RecipeGroup.RegisterGroup(
                "BiltonsYoyoMod:AnyEvilMaterial",
                new RecipeGroup(() => $"{Lang.GetItemNameValue(ItemID.ShadowScale)} / {Lang.GetItemNameValue(ItemID.TissueSample)}",
                    ItemID.ShadowScale, ItemID.TissueSample));

            RecipeGroup.RegisterGroup(
                "BiltonsYoyoMod:AnyHardmetalBar", // Adamantite / Titanium
                new RecipeGroup(() => Lang.GetItemNameValue(ItemID.AdamantiteBar),
                    ItemID.AdamantiteBar, ItemID.TitaniumBar));

            RecipeGroup.RegisterGroup(
                "BiltonsYoyoMod:AnyMythrilBar", // Mythril / Orichalcum
                new RecipeGroup(() => Lang.GetItemNameValue(ItemID.MythrilBar),
                    ItemID.MythrilBar, ItemID.OrichalcumBar));

            RecipeGroup.RegisterGroup(
                "BiltonsYoyoMod:AnyPalladiumBar", // Palladium / Cobalt
                new RecipeGroup(() => Lang.GetItemNameValue(ItemID.PalladiumBar),
                    ItemID.PalladiumBar, ItemID.CobaltBar));

            RecipeGroup.RegisterGroup(
                "BiltonsYoyoMod:AnyCelestialFragment",
                new RecipeGroup(() => "Any Celestial Fragment",
                    ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentNebula, ItemID.FragmentStardust));
        }
    }
}