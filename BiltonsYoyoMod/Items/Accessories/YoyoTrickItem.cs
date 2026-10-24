using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Items.Accessories
{
    public abstract class YoyoTrickItem : ModItem
    {
        public abstract YoyoTrickRank Rank { get; }

        public abstract int CooldownSeconds { get; }

        public override void SetDefaults()
        {
            Item.accessory = true;
            Item.maxStack = 1;

            Item.width = 32;
            Item.height = 32;
        }

        // MINIGAME TYPE

        public virtual bool UsesTimingBox => false;

        public virtual bool UsesMovementMinigame => false;

        public virtual bool UsesPositionMinigame => false;

        public virtual int MinigameDifficulty => 0;

        // TRICK

        public virtual void StartTrick(Player player)
        {
        }

        public virtual void TrickFinished(Player player)
        {
        }

        public virtual void TrickFailed(Player player)
        {
        }

        // Kept for compatibility with older YoyoTrickSystem code.
        public virtual void UpdateTrick(Player player)
        {
        }

        // TOOLTIP

        public override void ModifyTooltips(
            List<TooltipLine> tooltips
        )
        {
            tooltips.Add(
                new TooltipLine(
                    Mod,
                    "YoyoTrick",
                    "Yoyo Trick"
                )
            );

            tooltips.Add(
                new TooltipLine(
                    Mod,
                    "YoyoTrickRank",
                    $"Rank: {Rank}"
                )
            );

            tooltips.Add(
                new TooltipLine(
                    Mod,
                    "YoyoTrickCooldown",
                    $"Cooldown: {CooldownSeconds}s"
                )
            );

            tooltips.Add(
                new TooltipLine(
                    Mod,
                    "YoyoTrickUsage",
                    "Equip this accessory, then press your Yoyo Trick keybind."
                )
            );
        }

        // SAVE / LOAD

        public override void SaveData(TagCompound tag)
        {
        }

        public override void LoadData(TagCompound tag)
        {
        }
    }

    public enum YoyoTrickRank
    {
        T0 = 0,

        LowT1 = 10,
        MediumT1 = 20,
        HighT1 = 30,

        LowT2 = 40,
        MediumT2 = 50,
        HighT2 = 60,

        LowT3 = 70,
        MediumT3 = 80,
        HighT3 = 90
    }
}