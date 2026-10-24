using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;
using BiltonsYoyoMod.Items.Accessories;
using BiltonsYoyoMod.Settings;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Players
{
    public class YoyoTrickPlayer : ModPlayer
    {
        // DIAGNOSTICS

        private static readonly bool DiagnosticsEnabled = false;

        private void Diagnostic(string message, Color color)
        {
            if (!DiagnosticsEnabled)
                return;

            Main.NewText(message, color);
        }

        // CURRENT STATE

        public YoyoTrickItem ActiveTrick;

        public YoyoTrickMinigame CurrentMinigame;

        public bool MinigameRunning;

        public bool TrickRunning;

        // COOLDOWNS

        private readonly Dictionary<int, int> cooldowns =
            new Dictionary<int, int>();

        // UPDATE

        public override void PostUpdate()
        {
            UpdateCooldowns();

            if (!MinigameRunning)
                return;

            if (CurrentMinigame == null)
            {
                CancelCurrentTrick();
                return;
            }

            CurrentMinigame.Update(Player);

            if (CurrentMinigame.Finished)
            {
                FinishMinigame();
            }
        }

        private void UpdateCooldowns()
        {
            if (cooldowns.Count == 0)
                return;

            List<int> keys =
                new List<int>(cooldowns.Keys);

            foreach (int key in keys)
            {
                int remaining =
                    cooldowns[key] - 1;

                if (remaining <= 0)
                {
                    cooldowns.Remove(key);
                }
                else
                {
                    cooldowns[key] = remaining;
                }
            }
        }

        // KEYBIND

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!Player.active || Player.dead)
                return;

            if (ModalInput.GameplayHotkeysBlocked(Player))
                return;

            ModKeybind keybind =
                YoyoTrickKeybind.YoyoTrickKey;

            if (keybind == null)
                return;

            if (!keybind.JustPressed)
                return;

            Diagnostic(
                "YOYO TRICK KEY PRESSED",
                Color.Lime
            );

            // CURRENT MINIGAME

            if (MinigameRunning &&
                CurrentMinigame != null)
            {
                Diagnostic(
                    "SENDING INPUT TO CURRENT MINIGAME: " +
                    CurrentMinigame.GetType().Name,
                    Color.Yellow
                );

                CurrentMinigame.ReceiveKeyPress(Player);

                return;
            }

            // START NEW TRICK

            TryStartTrick();
        }

        // START TRICK

        private void TryStartTrick()
        {
            if (MinigameRunning)
            {
                Diagnostic(
                    "TRICK START BLOCKED: MINIGAME ALREADY RUNNING",
                    Color.Orange
                );

                return;
            }

            if (TrickRunning)
            {
                Diagnostic(
                    "TRICK START BLOCKED: TRICK ALREADY RUNNING",
                    Color.Orange
                );

                return;
            }

            // FIND EQUIPPED TRICK

            YoyoTrickItem trick =
                FindBestAvailableTrick();

            if (trick == null)
            {
                Diagnostic(
                    "NO AVAILABLE YOYO TRICK FOUND",
                    Color.Red
                );

                return;
            }

            // COOLDOWN CHECK

            if (IsOnCooldown(trick))
            {
                int remaining =
                    GetCooldown(trick);

                float seconds =
                    remaining / 60f;

                Diagnostic(
                    "TRICK ON COOLDOWN: " +
                    trick.GetType().Name +
                    " | " +
                    seconds.ToString("0.0") +
                    "s REMAINING",
                    Color.Orange
                );

                return;
            }

            Diagnostic(
                "FOUND AVAILABLE TRICK: " +
                trick.GetType().Name +
                " | " +
                trick.Rank,
                Color.Cyan
            );

            // FIND MAIN YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetMainYoyo(Player);

            if (yoyo == null)
            {
                Diagnostic(
                    "TRICK FOUND, BUT NO MAIN YOYO PROJECTILE FOUND",
                    Color.Red
                );

                return;
            }

            Diagnostic(
                "MAIN YOYO FOUND: PROJECTILE " +
                yoyo.whoAmI,
                Color.Lime
            );

            // STORE ACTIVE TRICK

            ActiveTrick = trick;

            // CREATE MINIGAME

            CurrentMinigame =
                CreateMinigame(trick);

            if (CurrentMinigame == null)
            {
                Diagnostic(
                    "TRICK FOUND, BUT NO MINIGAME EXISTS FOR: " +
                    trick.GetType().Name,
                    Color.Red
                );

                ActiveTrick = null;

                return;
            }

            // START MINIGAME

            MinigameRunning = true;

            Diagnostic(
                "STARTING MINIGAME: " +
                CurrentMinigame.GetType().Name,
                Color.White
            );

            CurrentMinigame.Start(
                Player,
                trick
            );

            Diagnostic(
                "MINIGAME STARTED: " +
                CurrentMinigame.GetType().Name,
                Color.Lime
            );
        }

        // MINIGAME SELECTION

        private YoyoTrickMinigame CreateMinigame(
            YoyoTrickItem trick)
        {
            if (trick is YoyoTrickTS)
            {
                Diagnostic(
                    "TS DETECTED -> SLEEPER MINIGAME",
                    Color.LightBlue
                );

                return new SleeperMinigame();
            }

            if (trick is YoyoTrickAWTD)
                return new WalkTheDogMinigame();

            if (trick is YoyoTrickAET)
                return new EiffelTowerMinigame();

            if (trick is YoyoTrickATE)
                return new TimingBoxMinigame(3);

            if (trick is YoyoTrickABB)
                return new BoingyBoingMinigame();

            if (trick is YoyoTrickADNA)
                return new TimingBoxMinigame(6);

            return null;
        }

        // FIND BEST AVAILABLE TRICK

        public YoyoTrickItem FindBestAvailableTrick()
        {
            YoyoTrickItem best = null;

            bool foundEquippedTrick = false;
            bool foundAvailableTrick = false;

            // VANILLA ACCESSORY SLOTS

            Diagnostic(
                "SCANNING VANILLA ACCESSORY SLOTS",
                Color.Gray
            );

            for (int i = 3;
                 i < Player.armor.Length && i < 10;
                 i++)
            {
                if (!Player.IsItemSlotUnlockedAndUsable(i))
                    continue;

                Item item = Player.armor[i];

                YoyoTrickItem trick =
                    GetTrickFromItem(item);

                if (trick == null)
                    continue;

                foundEquippedTrick = true;

                Diagnostic(
                    "FOUND VANILLA SLOT TRICK: " +
                    trick.GetType().Name +
                    " | RANK: " +
                    trick.Rank,
                    Color.Cyan
                );

                if (IsOnCooldown(trick))
                {
                    int remaining =
                        GetCooldown(trick);

                    Diagnostic(
                        "VANILLA TRICK ON COOLDOWN: " +
                        trick.GetType().Name +
                        " | " +
                        (remaining / 60f).ToString("0.0") +
                        "s REMAINING",
                        Color.Orange
                    );

                    continue;
                }

                foundAvailableTrick = true;

                best =
                    GetHigherRankedTrick(
                        best,
                        trick
                    );
            }

            // MODDED ACCESSORY SLOTS

            Terraria.ModLoader.Default.ModAccessorySlotPlayer modSlots =
                Player.GetModPlayer<
                    Terraria.ModLoader.Default.ModAccessorySlotPlayer
                >();

            Terraria.ModLoader.AccessorySlotLoader slotLoader =
                Terraria.ModLoader.LoaderManager.Get<
                    Terraria.ModLoader.AccessorySlotLoader
                >();

            int slotCount =
                modSlots.SlotCount;

            Diagnostic(
                "MODDED ACCESSORY SLOT COUNT: " +
                slotCount,
                Color.Gray
            );

            for (int i = 0;
                 i < slotCount;
                 i++)
            {
                Terraria.ModLoader.ModAccessorySlot slot =
                    slotLoader.Get(i, Player);

                if (slot == null)
                    continue;

                if (!slot.IsEnabled())
                    continue;

                Item item =
                    slot.FunctionalItem;

                YoyoTrickItem trick =
                    GetTrickFromItem(item);

                if (trick == null)
                    continue;

                foundEquippedTrick = true;

                Diagnostic(
                    "FOUND MODDED SLOT TRICK: " +
                    trick.GetType().Name +
                    " | RANK: " +
                    trick.Rank,
                    Color.Cyan
                );

                if (IsOnCooldown(trick))
                {
                    int remaining =
                        GetCooldown(trick);

                    Diagnostic(
                        "MODDED TRICK ON COOLDOWN: " +
                        trick.GetType().Name +
                        " | " +
                        (remaining / 60f).ToString("0.0") +
                        "s REMAINING",
                        Color.Orange
                    );

                    continue;
                }

                foundAvailableTrick = true;

                best =
                    GetHigherRankedTrick(
                        best,
                        trick
                    );
            }

            // YOYO STORAGE SLOTS

            YoyoStoragePlayer storage =
                Player.GetModPlayer<YoyoStoragePlayer>();

            Diagnostic(
                "SCANNING YOYO STORAGE SLOTS",
                Color.Gray
            );

            foreach (int storageIndex in storage.VisibleSlots)
            {
                if (storageIndex < 0 ||
                    storageIndex >= storage.Storage.Length)
                {
                    continue;
                }

                Item item =
                    storage.Storage[storageIndex];

                if (item == null || item.IsAir)
                    continue;

                if (!YoyoStoragePlayer.MatchesSlot(
                        item,
                        YoyoStoragePlayer.SlotTypes[storageIndex]))
                {
                    continue;
                }

                YoyoTrickItem trick =
                    GetTrickFromItem(item);

                if (trick == null)
                    continue;

                foundEquippedTrick = true;

                Diagnostic(
                    "FOUND YOYO STORAGE TRICK: " +
                    trick.GetType().Name +
                    " | SLOT: " +
                    storageIndex +
                    " | RANK: " +
                    trick.Rank,
                    Color.Cyan
                );

                if (IsOnCooldown(trick))
                {
                    int remaining =
                        GetCooldown(trick);

                    Diagnostic(
                        "YOYO STORAGE TRICK ON COOLDOWN: " +
                        trick.GetType().Name +
                        " | " +
                        (remaining / 60f).ToString("0.0") +
                        "s REMAINING",
                        Color.Orange
                    );

                    continue;
                }

                foundAvailableTrick = true;

                best =
                    GetHigherRankedTrick(
                        best,
                        trick
                    );
            }

            // FINAL SCAN RESULT

            if (!foundEquippedTrick)
            {
                Diagnostic(
                    "ACCESSORY SCAN: NO YOYO TRICKS EQUIPPED",
                    Color.Red
                );

                return null;
            }

            if (!foundAvailableTrick)
            {
                Diagnostic(
                    "ACCESSORY SCAN: YOYO TRICKS FOUND, BUT ALL ARE ON COOLDOWN",
                    Color.Orange
                );

                return null;
            }

            if (best != null)
            {
                Diagnostic(
                    "BEST AVAILABLE TRICK: " +
                    best.GetType().Name +
                    " | RANK: " +
                    best.Rank,
                    Color.Lime
                );
            }

            return best;
        }

        // ITEM -> TRICK

        private YoyoTrickItem GetTrickFromItem(Item item)
        {
            if (item == null)
                return null;

            if (item.IsAir)
                return null;

            if (item.social)
                return null;

            return item.ModItem as YoyoTrickItem;
        }

        // RANK

        private YoyoTrickItem GetHigherRankedTrick(
            YoyoTrickItem current,
            YoyoTrickItem candidate)
        {
            if (candidate == null)
                return current;

            if (current == null)
                return candidate;

            if (candidate.Rank > current.Rank)
                return candidate;

            return current;
        }

        // FINISH MINIGAME

        private void FinishMinigame()
        {
            YoyoTrickMinigame minigame =
                CurrentMinigame;

            YoyoTrickItem trick =
                ActiveTrick;

            CurrentMinigame = null;
            MinigameRunning = false;

            if (trick == null || minigame == null)
            {
                ActiveTrick = null;

                Diagnostic(
                    "MINIGAME FINISHED BUT ACTIVE TRICK WAS NULL",
                    Color.Red
                );

                return;
            }

            // FAILED

            if (!minigame.Success)
            {
                Diagnostic(
                    "YOYO TRICK MINIGAME FAILED: " +
                    trick.GetType().Name,
                    Color.Red
                );

                cooldowns[trick.Type] =
                    trick.CooldownSeconds * 60;

                Diagnostic(
                    "TRICK FAILED -> COOLDOWN STARTED: " +
                    trick.CooldownSeconds +
                    "s",
                    Color.Orange
                );

                trick.TrickFailed(Player);

                ActiveTrick = null;

                return;
            }

            // SUCCESS

            Diagnostic(
                "YOYO TRICK MINIGAME SUCCEEDED: " +
                trick.GetType().Name,
                Color.Lime
            );

            ActivateTrick();
        }

        // ACTUAL TRICK

        private void ActivateTrick()
        {
            if (ActiveTrick == null)
                return;

            YoyoTrickItem trick =
                ActiveTrick;

            Projectile yoyo =
                YoyoTrickSystem.GetMainYoyo(Player);

            if (yoyo == null)
            {
                Diagnostic(
                    "YOYO DISAPPEARED BEFORE TRICK ACTIVATION",
                    Color.Red
                );

                cooldowns[trick.Type] =
                    trick.CooldownSeconds * 60;

                Diagnostic(
                    "TRICK NOT ACTIVATED -> COOLDOWN STARTED: " +
                    trick.CooldownSeconds +
                    "s",
                    Color.Orange
                );

                ActiveTrick = null;

                return;
            }

            Diagnostic(
                "ACTIVATING TRICK: " +
                trick.GetType().Name,
                Color.Lime
            );

            TrickRunning = true;

            trick.StartTrick(Player);

            // SUCCESS COOLDOWN

            cooldowns[trick.Type] =
                trick.CooldownSeconds * 60;

            trick.TrickFinished(Player);

            TrickRunning = false;
            ActiveTrick = null;

            Diagnostic(
                "TRICK ACTIVATED - COOLDOWN STARTED: " +
                trick.CooldownSeconds +
                "s",
                Color.Lime
            );
        }

        // CANCEL

        private void CancelCurrentTrick()
        {
            Diagnostic(
                "CURRENT YOYO TRICK CANCELLED",
                Color.Orange
            );

            ActiveTrick = null;
            CurrentMinigame = null;

            MinigameRunning = false;
            TrickRunning = false;
        }

        // COOLDOWN

        private bool IsOnCooldown(YoyoTrickItem trick)
        {
            if (trick == null)
                return false;

            return cooldowns.TryGetValue(
                trick.Type,
                out int remaining
            ) && remaining > 0;
        }

        public int GetCooldown(YoyoTrickItem trick)
        {
            if (trick == null)
                return 0;

            if (cooldowns.TryGetValue(
                    trick.Type,
                    out int remaining))
            {
                return remaining;
            }

            return 0;
        }

        // COMPATIBILITY

        public void FinishTrick()
        {
            TrickRunning = false;
            ActiveTrick = null;
        }

        // RESPAWN

        public override void OnRespawn()
        {
            ActiveTrick = null;
            CurrentMinigame = null;

            MinigameRunning = false;
            TrickRunning = false;

            cooldowns.Clear();
        }
    }
}