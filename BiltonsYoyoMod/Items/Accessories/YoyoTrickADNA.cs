using Terraria;
using Terraria.ID;
using BiltonsYoyoMod.Players;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class YoyoTrickADNA : YoyoTrickItem
    {
        public override YoyoTrickRank Rank =>
            YoyoTrickRank.HighT3;

        public override int CooldownSeconds =>
            50;

        public override bool UsesTimingBox =>
            true;

        // DNA requires 6 successful timing prompts.
        public override int MinigameDifficulty =>
            6;

        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.rare =
                ItemRarityID.Cyan;
        }

        public override void StartTrick(
            Player player
        )
        {
            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(player);

            if (yoyo == null)
                return;

            player
                .GetModPlayer<DNAPlayer>()
                .StartDNA(yoyo);
        }
    }
}