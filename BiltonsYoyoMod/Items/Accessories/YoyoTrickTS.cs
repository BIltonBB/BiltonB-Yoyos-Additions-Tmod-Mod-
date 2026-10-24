using Terraria;
using Terraria.ID;
using BiltonsYoyoMod.Players;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class YoyoTrickTS : YoyoTrickItem
    {
        public override YoyoTrickRank Rank =>
            YoyoTrickRank.T0;

        public override int CooldownSeconds => 20;

        public override bool UsesPositionMinigame =>
            true;

        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.rare = ItemRarityID.White;
        }

        public override void StartTrick(
            Player player)
        {
            Projectile yoyo =
                YoyoTrickSystem.GetMainYoyo(
                    player
                );

            if (yoyo == null)
                return;

            player.GetModPlayer<SleeperPlayer>()
                .StartSleeper(yoyo);
        }
    }
}