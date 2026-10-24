using Terraria;
using Terraria.ID;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class YoyoTrickAET : YoyoTrickItem
    {
        public override YoyoTrickRank Rank =>
            YoyoTrickRank.HighT1;

        public override int CooldownSeconds => 25;

        public override bool UsesPositionMinigame =>
            true;

        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.rare =
                ItemRarityID.Orange;
        }

        public override void StartTrick(
            Player player
        )
        {
            player.GetModPlayer<
                EiffelTowerPlayer>()
                .StartEiffelTower();
        }
    }
}