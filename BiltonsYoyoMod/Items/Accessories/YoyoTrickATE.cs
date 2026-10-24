using Terraria;
using Terraria.ID;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class YoyoTrickATE : YoyoTrickItem
    {
        public override YoyoTrickRank Rank =>
            YoyoTrickRank.MediumT2;

        public override int CooldownSeconds => 40;

        public override bool UsesTimingBox =>
            true;

        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.rare =
                ItemRarityID.LightPurple;
        }

        public override void StartTrick(
            Player player
        )
        {
            player.GetModPlayer<
                ElevatorPlayer>()
                .StartElevator();
        }
    }
}