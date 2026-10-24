using System;
using System.Collections.Generic;
using BiltonsYoyoMod.Items;
using BiltonsYoyoMod.Items.Accessories;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Players
{
    public enum YoyoStorageSlotType
    {
        TrickBook,
        WildString,
        LooseBearings,
        ResponsePads,
        YoyoAxle,
        TensionBearing,
        Fingerloop,
        YoyoDuplicator,
        StringCutter,
        Strings,
        Counterweight,
        YoyoGlove,
        YoyoEmblem,
        YoyoAccessory,
        YoyoPotion
    }

    public class YoyoStoragePlayer : ModPlayer
    {
        public const int StorageSlotCount = 23;

        public Item[] Storage =
            new Item[StorageSlotCount];

        public int ActiveTier { get; private set; } = -1;

        public float TrickDamageBonus { get; private set; }

        public float YoyoDamageBonus { get; private set; }

        // Defense granted by yoyo gear, worn or slotted: each
        // defense-granting yoyo accessory registers its bonus here
        // inside its own UpdateAccessory, so every path that applies
        // it (vanilla slots, modded slots, storage) counts exactly once.
        public int YoyoDefenseBonus { get; private set; }

        public void AddYoyoDefense(int amount)
        {
            YoyoDefenseBonus += amount;
        }

        public bool YoyosPassThroughBlocks { get; private set; }

        // Yoyo-potion slot boost: 1.2x tonic effectiveness while a potion
        // is slotted, read by the tonic buffs themselves.
        public float PotionBoost { get; private set; } = 1f;

        // Storage panel collapsed/expanded while the inventory is open.
        public bool ShowStorageUI = true;

        // Slot legend overlay on/off.
        public bool ShowStorageHelp = false;

        // Yoyo stats overlay on/off.
        public bool ShowStorageStats = false;

        public int[] VisibleSlots { get; private set; } =
            Array.Empty<int>();

        // Every storage case equipped this tick, in equip order.
        // Reset each tick (see ResetEffects) and rebuilt by Activate.
        private readonly List<YoyoStorageCase> activatedCases = new();

        public static readonly YoyoStorageSlotType[] SlotTypes =
        {
            YoyoStorageSlotType.TrickBook,
            YoyoStorageSlotType.TrickBook,
            YoyoStorageSlotType.TrickBook,

            YoyoStorageSlotType.WildString,
            YoyoStorageSlotType.WildString,
            YoyoStorageSlotType.WildString,

            YoyoStorageSlotType.LooseBearings,
            YoyoStorageSlotType.LooseBearings,
            YoyoStorageSlotType.LooseBearings,

            YoyoStorageSlotType.ResponsePads,
            YoyoStorageSlotType.YoyoAxle,
            YoyoStorageSlotType.TensionBearing,

            YoyoStorageSlotType.Fingerloop,
            YoyoStorageSlotType.YoyoDuplicator,
            YoyoStorageSlotType.StringCutter,

            YoyoStorageSlotType.Strings,

            YoyoStorageSlotType.Counterweight,
            YoyoStorageSlotType.Counterweight,
            YoyoStorageSlotType.Counterweight,

            YoyoStorageSlotType.YoyoGlove,

            YoyoStorageSlotType.YoyoEmblem,
            YoyoStorageSlotType.YoyoAccessory,
            YoyoStorageSlotType.YoyoPotion
        };

        public override void Initialize()
        {
            for (int index = 0;
                 index < StorageSlotCount;
                 index++)
            {
                Storage[index] = new Item();
            }
        }

        public override void ResetEffects()
        {
            ActiveTier = -1;
            TrickDamageBonus = 0f;
            YoyoDamageBonus = 0f;
            YoyoDefenseBonus = 0;
            YoyosPassThroughBlocks = false;
            PotionBoost = 1f;

            VisibleSlots =
                Array.Empty<int>();

            activatedCases.Clear();
        }

        public void Activate(
            YoyoStorageCase storageCase)
        {
            activatedCases.Add(storageCase);

            // Best (highest tier) case takes priority for bonuses.
            // Ties keep last-equipped-wins, matching the old behavior.
            YoyoStorageCase best = null;

            foreach (YoyoStorageCase candidate in activatedCases)
            {
                if (best == null ||
                    candidate.StorageTier >= best.StorageTier)
                {
                    best = candidate;
                }
            }

            ActiveTier =
                best.StorageTier;

            TrickDamageBonus =
                best.TrickDamageBonus;

            YoyoDamageBonus =
                best.YoyoDamageBonus;

            YoyosPassThroughBlocks =
                best.YoyosPassThroughBlocks;

            // Union of slots: the best case's slots first, then any new
            // categories (slot indices) contributed by the other cases.
            // Example: {3 tricks, 1 accessory} + {3 tricks, 1 potion}
            // becomes {3 tricks, 1 potion, 1 accessory}.
            var merged =
                new List<int>();

            var seen =
                new HashSet<int>();

            var ordered =
                new List<YoyoStorageCase> { best };

            foreach (YoyoStorageCase candidate in activatedCases)
            {
                if (!ReferenceEquals(candidate, best))
                {
                    ordered.Add(candidate);
                }
            }

            foreach (YoyoStorageCase candidate in ordered)
            {
                int[] slots =
                    candidate.StorageSlots;

                if (slots == null)
                {
                    continue;
                }

                foreach (int index in slots)
                {
                    if (index < 0 ||
                        index >= StorageSlotCount)
                    {
                        continue;
                    }

                    if (seen.Add(index))
                    {
                        merged.Add(index);
                    }
                }
            }

            VisibleSlots =
                merged.ToArray();
        }

        public override void PostUpdateEquips()
        {
            foreach (int index in VisibleSlots)
            {
                if (index < 0 ||
                    index >= Storage.Length ||
                    index >= SlotTypes.Length)
                {
                    continue;
                }

                Item item =
                    Storage[index];

                if (item == null ||
                    item.IsAir)
                {
                    continue;
                }

                YoyoStorageSlotType slotType =
                    SlotTypes[index];

                if (!MatchesSlot(item, slotType))
                {
                    continue;
                }

                /*
                 * Apply the item exactly like a functional
                 * accessory.
                 *
                 * This is important for modded accessories.
                 * Their normal UpdateAccessory code is allowed
                 * to run, so accessories such as custom strings
                 * that spawn projectiles can perform their normal
                 * behavior.
                 */
                Player.ApplyEquipFunctional(
                    item,
                    false
                );

                /*
                 * Vanilla Strings have a visual component that
                 * depends on Player.yoyoString and
                 * Player.stringColor.
                 *
                 * Because this item is stored in our custom
                 * storage instead of a normal accessory slot,
                 * explicitly copy those visual values - but
                 * never over an explicitly worn string (normal
                 * accessory slot): worn strings win so Golden,
                 * Ancient, Tangled and Barbed keep their color.
                 */
                if (slotType ==
                    YoyoStorageSlotType.Strings &&
                    !HasWornString())
                {
                    Player.yoyoString = true;
                    Player.stringColor =
                        item.stringColor;
                }

                // Yoyo-potion slot: the slotted tonic's buff stays up
                // forever at 1.2x effectiveness until removed.
                if (slotType ==
                    YoyoStorageSlotType.YoyoPotion &&
                    item.buffType > 0)
                {
                    PotionBoost = 1.2f;
                    Player.AddBuff(item.buffType, 60);
                }
            }
        }

        // True if any armor/accessory slot (functional or vanity)
        // holds an item with its own string color.
        private bool HasWornString()
        {
            foreach (Item worn in Player.armor) {
                if (worn != null && !worn.IsAir && worn.stringColor != 0)
                    return true;
            }
            return false;
        }

        public int VisibleSlotCount =>
            VisibleSlots.Length;

        public static bool MatchesSlot(
            Item item,
            YoyoStorageSlotType slotType)
        {
            if (item == null ||
                item.IsAir)
            {
                return true;
            }

            string name =
                item.ModItem?.GetType().Name
                ?? string.Empty;

            return slotType switch
            {
                YoyoStorageSlotType.TrickBook =>
                    item.ModItem is YoyoTrickItem,

                YoyoStorageSlotType.WildString =>
                    name.StartsWith(
                        "WildStrings",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.LooseBearings =>
                    name.StartsWith(
                        "LooseBearings",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.ResponsePads =>
                    name.StartsWith(
                        "ResponsePads",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.YoyoAxle =>
                    name.StartsWith(
                        "YoyoAxle",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.TensionBearing =>
                    name.StartsWith(
                        "TensionBearing",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.Fingerloop =>
                    name.StartsWith(
                        "FingerLoop",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.YoyoDuplicator =>
                    name.StartsWith(
                        "YoyoDuplicator",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.StringCutter =>
                    name.StartsWith(
                        "StringCutter",
                        StringComparison.Ordinal
                    ),

                YoyoStorageSlotType.Strings =>
                    item.accessory &&
                    item.Name.Contains(
                        "String",
                        StringComparison.OrdinalIgnoreCase
                    ),

                YoyoStorageSlotType.Counterweight =>
                    item.Name.Contains(
                        "Counterweight",
                        StringComparison.OrdinalIgnoreCase
                    ),

                YoyoStorageSlotType.YoyoGlove =>
                    item.Name.Contains(
                        "Yoyo Glove",
                        StringComparison.OrdinalIgnoreCase
                    ),

                YoyoStorageSlotType.YoyoEmblem =>
                    item.accessory &&
                    item.Name.Contains(
                        "Emblem",
                        StringComparison.OrdinalIgnoreCase
                    ),

                YoyoStorageSlotType.YoyoAccessory =>
                    item.accessory &&
                    item.ModItem != null &&
                    item.ModItem.Mod == ModContent.GetInstance<BiltonsYoyoMod>() &&
                    item.ModItem is not YoyoStorageCase,

                YoyoStorageSlotType.YoyoPotion =>
                    IsYoyoPotion(item),

                _ => false
            };
        }

        private static bool IsYoyoTonicBuff(int buffType)
        {
            return buffType == ModContent.BuffType<Content.Buffs.BearingGreaseBuff>()
                || buffType == ModContent.BuffType<Content.Buffs.CultistFervorBuff>()
                || buffType == ModContent.BuffType<Content.Buffs.StringWaxBuff>()
                || buffType == ModContent.BuffType<Content.Buffs.MoonlitFocusBuff>();
        }

        private static bool IsYoyoPotion(Item item)
        {
            if (item == null || item.IsAir || item.accessory || item.buffType <= 0)
                return false;
            if (IsYoyoTonicBuff(item.buffType))
                return true;
            // Trail food counts too (vanilla Well Fed at normal strength).
            return item.ModItem is Items.Consumables.HoneySpool;
        }

        public override void SaveData(
            TagCompound tag)
        {
            var items =
                new List<TagCompound>();

            foreach (Item item in Storage)
            {
                items.Add(
                    ItemIO.Save(item)
                );
            }

            tag["YoyoStorage"] =
                items;
        }

        public override void LoadData(
            TagCompound tag)
        {
            if (!tag.ContainsKey(
                    "YoyoStorage"))
            {
                return;
            }

            var items =
                tag.GetList<TagCompound>(
                    "YoyoStorage"
                );

            for (int index = 0;
                 index < Math.Min(
                     items.Count,
                     StorageSlotCount
                 );
                 index++)
            {
                Storage[index] =
                    ItemIO.Load(
                        items[index]
                    );
            }
        }
    }
}