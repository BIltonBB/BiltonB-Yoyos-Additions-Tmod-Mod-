using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria.ID;
using BiltonsYoyoMod.Players;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace BiltonsYoyoMod.UI
{
    public static class YoyoStorageUI
    {
        private const int SlotSize = 38;
        private const int ColumnGap = 42;
        private const int RowGap = 46;

        private const int NormalTop = 150;

        // How far upward the entire storage UI moves when the
        // minimap is actually closed.
        private const int MinimapClosedOffset = 30;

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu ||
                !Main.playerInventory ||
                Main.LocalPlayer == null)
            {
                return;
            }

            YoyoStoragePlayer storage =
                Main.LocalPlayer.GetModPlayer<YoyoStoragePlayer>();

            if (storage.VisibleSlotCount == 0)
                return;

            Vector2 titlePosition =
                GetSlotPosition(0) +
                new Vector2(-28f, -26f);

            // Clickable title: collapses/expands the panel while the
            // inventory stays open.
            bool fancy = ModContent.GetInstance<global::BiltonsYoyoMod.Configuration.YoyoConfig>().StorageUI
                == global::BiltonsYoyoMod.Configuration.StorageUIStyle.Fancy;

            string title = storage.ShowStorageUI
                ? $"Yoyo Storage T{storage.ActiveTier} ({storage.VisibleSlotCount} slots) [-]"
                : $"Yoyo Storage T{storage.ActiveTier} [+]";

            // Fancy: one dark relic panel behind the whole grid + title,
            // measured from the real extents. Collapsed: chip behind title.
            if (fancy && storage.VisibleSlotCount > 0) {
                Vector2 titleMeasure = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(title) * 0.8f;
                Vector2 titleTopLeft = GetSlotPosition(0) + new Vector2(-28f, -26f);
                Rectangle back;
                if (storage.ShowStorageUI) {
                    float left = titleTopLeft.X - 6f;
                    float top = titleTopLeft.Y - 4f;
                    float right = titleTopLeft.X;
                    float bottom = titleTopLeft.Y;
                    for (int di = 0; di < storage.VisibleSlotCount; di++) {
                        Vector2 p = GetSlotPosition(di);
                        left = System.Math.Min(left, p.X - 6f);
                        top = System.Math.Min(top, p.Y - 6f);
                        right = System.Math.Max(right, p.X + SlotSize + 6f);
                        bottom = System.Math.Max(bottom, p.Y + SlotSize + 6f);
                    }
                    right = System.Math.Max(right, titleTopLeft.X + titleMeasure.X + 14f);
                    bottom = System.Math.Max(bottom, titleTopLeft.Y + titleMeasure.Y + 10f);
                    back = new Rectangle((int)left, (int)top, (int)(right - left), (int)(bottom - top));
                }
                else {
                    back = new Rectangle((int)titleTopLeft.X - 4, (int)titleTopLeft.Y - 2, (int)titleMeasure.X + 16, (int)titleMeasure.Y + 8);
                }
                Fill(spriteBatch, back, new Color(10, 9, 18, 200));
                Color trim = new Color(176, 128, 52);
                Fill(spriteBatch, new Rectangle(back.X, back.Y, back.Width, 1), trim);
                Fill(spriteBatch, new Rectangle(back.X, back.Bottom - 1, back.Width, 1), trim);
                Fill(spriteBatch, new Rectangle(back.X, back.Y, 1, back.Height), trim);
                Fill(spriteBatch, new Rectangle(back.Right - 1, back.Y, 1, back.Height), trim);
            }
            Vector2 titleSize = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(title) * 0.8f;
            Rectangle titleBox = new((int)titlePosition.X, (int)titlePosition.Y, (int)titleSize.X + 8, (int)titleSize.Y + 4);
            bool titleHover = titleBox.Contains(Main.MouseScreen.ToPoint());
            Color titleColor = fancy ? new Color(255, 215, 130) : new Color(115, 190, 255);
            if (titleHover)
                titleColor = Color.White;
            if (fancy)
                Utils.DrawBorderString(spriteBatch, title, titlePosition + new Vector2(1f, 1f), new Color(120, 70, 10, 180), .8f);
            Utils.DrawBorderString(
                spriteBatch,
                title,
                titlePosition,
                titleColor,
                .8f
            );
            if (fancy) {
                Vector2 titleSize2 = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(title) * 0.8f;
                Fill(spriteBatch,
                    new Rectangle((int)titlePosition.X, (int)(titlePosition.Y + titleSize2.Y + 2), (int)titleSize2.X + 8, 1),
                    new Color(176, 128, 52));
            }
            if (titleHover) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    storage.ShowStorageUI = !storage.ShowStorageUI;
                    // Collapsing also closes the overlays.
                    if (!storage.ShowStorageUI) {
                        storage.ShowStorageHelp = false;
                        storage.ShowStorageStats = false;
                    }
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
                }
            }
            // Side button row beside the grid: "?" legend on top, Stats
            // below. Anchored to the grid so any case size fits.
            float gridLeft = titlePosition.X;
            float gridTop = titlePosition.Y;
            if (storage.ShowStorageUI && storage.VisibleSlotCount > 0) {
                gridLeft = float.MaxValue;
                gridTop = float.MaxValue;
                for (int di = 0; di < storage.VisibleSlotCount; di++) {
                    Vector2 p = GetSlotPosition(di);
                    if (p.X < gridLeft)
                        gridLeft = p.X;
                    if (p.Y < gridTop)
                        gridTop = p.Y;
                }
            }
            float colRight = gridLeft - 8f;
            Rectangle helpBox = new((int)(colRight - 24), (int)gridTop, 24, 24);
            bool helpHover = helpBox.Contains(Main.MouseScreen.ToPoint());
            Utils.DrawBorderString(spriteBatch, "?", new Vector2(helpBox.X + 7, helpBox.Y + 3),
                storage.ShowStorageHelp ? Color.Yellow : helpHover ? Color.White : Color.Gray, 0.85f);
            if (helpHover) {
                Main.LocalPlayer.mouseInterface = true;
                Main.hoverItemName = "What goes where";
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    storage.ShowStorageHelp = !storage.ShowStorageHelp;
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
                }
            }
            if (storage.ShowStorageHelp)
                DrawLegend(spriteBatch, colRight - 8f, gridTop);
            // "Stats" button stacked under "?" in the same side row.
            Rectangle statsBox = new((int)(colRight - 52), (int)gridTop + 28, 52, 24);
            bool statsHover = statsBox.Contains(Main.MouseScreen.ToPoint());
            Utils.DrawBorderString(spriteBatch, "Stats", new Vector2(statsBox.X + 7, statsBox.Y + 3),
                storage.ShowStorageStats ? Color.Yellow : statsHover ? Color.White : Color.Gray, 0.85f);
            if (statsHover) {
                Main.LocalPlayer.mouseInterface = true;
                Main.hoverItemName = "Overall yoyo damage";
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    storage.ShowStorageStats = !storage.ShowStorageStats;
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
                }
            }
            if (storage.ShowStorageStats)
                DrawStats(spriteBatch, Main.LocalPlayer, storage, colRight - 8f, gridTop);
            if (!storage.ShowStorageUI)
                return;

            for (int displayIndex = 0;
                 displayIndex < storage.VisibleSlotCount;
                 displayIndex++)
            {
                DrawSlot(
                    spriteBatch,
                    storage,
                    displayIndex,
                    storage.VisibleSlots[displayIndex]
                );
            }
        }

        private static void DrawLegend(SpriteBatch spriteBatch, float panelRight, float top)
        {
            string[,] rows = new string[,] {
                { "Trick Book", "Trick books" },
                { "Wild String", "Wild strings" },
                { "Loose Bearings", "Loose bearings" },
                { "Response Pads", "Response pads" },
                { "Yoyo Axle", "Yoyo axles" },
                { "Tension Bearing", "Tension bearings" },
                { "Fingerloop", "Fingerloops" },
                { "Yoyo Duplicator", "Duplicators" },
                { "String Cutter", "String cutters" },
                { "Strings", "Strings (colors the line)" },
                { "Counterweight", "Counterweights (thrown in fights)" },
                { "Yoyo Glove", "Yoyo Glove family" },
                { "Yoyo Emblem", "Yoyo emblems (Ancient case)" },
                { "Yoyo Accessory", "Any yoyo accessory (Ancient case)" },
                { "Yoyo Potion", "Yoyo tonic: forever buff (Ancient case)" },
            };
            const int rowH = 17;
            int w = 330;
            int h = 30 + rows.GetLength(0) * rowH;
            var box = new Rectangle((int)(panelRight - w), (int)top, w, h);
            if (box.X < 4)
                box.X = 4;
            bool fancy = ModContent.GetInstance<global::BiltonsYoyoMod.Configuration.YoyoConfig>().StorageUI
                == global::BiltonsYoyoMod.Configuration.StorageUIStyle.Fancy;
            Fill(spriteBatch, box,
                fancy ? new Color(10, 9, 18, 220) : new Color(20, 24, 40, 220));
            if (fancy) {
                Color trim = new Color(176, 128, 52);
                Fill(spriteBatch, new Rectangle(box.X, box.Y, box.Width, 1), trim);
                Fill(spriteBatch, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), trim);
                Fill(spriteBatch, new Rectangle(box.X, box.Y, 1, box.Height), trim);
                Fill(spriteBatch, new Rectangle(box.Right - 1, box.Y, 1, box.Height), trim);
            }
            Utils.DrawBorderString(spriteBatch, "What goes where:", new Vector2(box.X + 8, box.Y + 6),
                fancy ? new Color(255, 215, 130) : Color.White, 0.8f);
            for (int i = 0; i < rows.GetLength(0); i++) {
                Utils.DrawBorderString(spriteBatch, rows[i, 0] + ": " + rows[i, 1],
                    new Vector2(box.X + 8, box.Y + 26 + i * rowH), Color.LightGray, 0.7f);
            }
        }

        private static void DrawStats(SpriteBatch spriteBatch, Player player, YoyoStoragePlayer storage, float panelRight, float top)
        {
            float dmgPct = (player.GetDamage(DamageClass.MeleeNoSpeed).ApplyTo(100f) - 100f);
            float crit = player.GetCritChance(DamageClass.MeleeNoSpeed);
            int carried = global::BiltonsYoyoMod.Content.NPCs.Town.YoyoTownShared.CountYoyos(player);
            int tier = storage.ActiveTier;

            // Stats unlock with the BEST equipped case's tier: lowest shows
            // damage and crit only, best (T8+) shows everything.
            var lines = new List<string> {
                $"Yoyo damage: +{dmgPct:0}%",
                $"Yoyo crit: +{crit:0}%",
            };
            if (tier >= 2) {
                lines.Add($"Case damage: +{storage.YoyoDamageBonus * 100f:0}%");
                lines.Add($"Trick damage: +{storage.TrickDamageBonus * 100f:0}%");
            }
            if (tier >= 4) {
                float pen = player.GetArmorPenetration(DamageClass.MeleeNoSpeed);
                float kb = (player.GetKnockback(DamageClass.MeleeNoSpeed).ApplyTo(1f) - 1f) * 100f;
                lines.Add($"Armor pen: +{pen:0}");
                lines.Add($"Knockback: +{kb:0}%");
            }
            if (tier >= 6) {
                float spd = (player.GetAttackSpeed(DamageClass.MeleeNoSpeed) - 1f) * 100f;
                lines.Add($"Attack speed: +{spd:0}%");
                lines.Add($"Counterweights: {player.counterWeight}" + (player.yoyoGlove ? " (glove)" : ""));
            }
            if (tier >= 8) {
                lines.Add($"String: {(player.yoyoString ? "yes" : "no")}");
                lines.Add($"Potion boost: x{storage.PotionBoost:0.00}");
                lines.Add($"Yoyos carried: {carried}");
                int yoyosOut = 0;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    // Deployed yoyo bodies only: yoyo AI, owned, hostile-free,
                    // and able to hit (ignores damage-less visual helpers).
                    if (p.active && p.owner == player.whoAmI && !p.hostile && p.damage > 0 && p.aiStyle == ProjAIStyleID.Yoyo)
                        yoyosOut++;
                }
                lines.Add($"Yoyos out: {yoyosOut}");
                lines.Add($"Yoyo defense: +{storage.YoyoDefenseBonus}");
                lines.Add($"Luck: {player.luck:0.00}");
                bool insight = player.GetModPlayer<RevampPlayer>().Insight;
                lines.Add($"Insight: {(insight ? "on" : "off")}");
                string potionName = "";
                for (int di = 0; di < storage.VisibleSlotCount; di++) {
                    int si = storage.VisibleSlots[di];
                    if (si < 0 || si >= YoyoStoragePlayer.SlotTypes.Length)
                        continue;
                    if (YoyoStoragePlayer.SlotTypes[si] != YoyoStorageSlotType.YoyoPotion)
                        continue;
                    Item potion = storage.Storage[si];
                    if (potion != null && !potion.IsAir && potion.buffType > 0)
                        potionName = Lang.GetBuffName(potion.buffType);
                }
                lines.Add($"Potion: {(potionName == "" ? "none" : potionName + " (perma)")}");
            }
            const int rowH = 17;
            // Box scales to content: wide enough for the longest line.
            var font = Terraria.GameContent.FontAssets.MouseText.Value;
            float maxW = font.MeasureString("Overall yoyo damage:").X * 0.8f;
            foreach (string line in lines)
                maxW = System.Math.Max(maxW, font.MeasureString(line).X * 0.7f);
            int w = (int)maxW + 20;
            if (w < 160)
                w = 160;
            int h = 30 + lines.Count * rowH;
            var box = new Rectangle((int)(panelRight - w), (int)top, w, h);
            if (box.X < 4)
                box.X = 4;
            bool fancy = ModContent.GetInstance<global::BiltonsYoyoMod.Configuration.YoyoConfig>().StorageUI
                == global::BiltonsYoyoMod.Configuration.StorageUIStyle.Fancy;
            Fill(spriteBatch, box,
                fancy ? new Color(10, 9, 18, 220) : new Color(20, 24, 40, 220));
            if (fancy) {
                Color trim = new Color(176, 128, 52);
                Fill(spriteBatch, new Rectangle(box.X, box.Y, box.Width, 1), trim);
                Fill(spriteBatch, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), trim);
                Fill(spriteBatch, new Rectangle(box.X, box.Y, 1, box.Height), trim);
                Fill(spriteBatch, new Rectangle(box.Right - 1, box.Y, 1, box.Height), trim);
            }
            Utils.DrawBorderString(spriteBatch, "Overall yoyo damage:", new Vector2(box.X + 8, box.Y + 6),
                fancy ? new Color(255, 215, 130) : Color.White, 0.8f);
            for (int i = 0; i < lines.Count; i++) {
                Utils.DrawBorderString(spriteBatch, lines[i],
                    new Vector2(box.X + 8, box.Y + 26 + i * rowH), Color.LightGray, 0.7f);
            }
        }

        private static void DrawSlot(
            SpriteBatch spriteBatch,
            YoyoStoragePlayer storage,
            int displayIndex,
            int storageIndex)
        {
            Vector2 position =
                GetSlotPosition(displayIndex);

            Rectangle hitbox =
                new Rectangle(
                    (int)position.X,
                    (int)position.Y,
                    SlotSize,
                    SlotSize
                );

            Item item =
                storage.Storage[storageIndex];

            YoyoStorageSlotType slotType =
                YoyoStoragePlayer.SlotTypes[storageIndex];

            bool canInteract =
                Main.mouseItem.IsAir ||
                YoyoStoragePlayer.MatchesSlot(
                    Main.mouseItem,
                    slotType
                );

            bool isHovered =
                hitbox.Contains(
                    Main.MouseScreen.ToPoint()
                );

            if (isHovered &&
                canInteract &&
                Main.mouseLeft &&
                Main.mouseLeftRelease)
            {
                Item heldItem =
                    Main.mouseItem;

                Main.mouseItem =
                    storage.Storage[storageIndex];

                storage.Storage[storageIndex] =
                    heldItem;

                Main.mouseLeftRelease = false;

                item =
                    storage.Storage[storageIndex];
            }

            // VANILLA-STYLE SLOT BACKGROUND
            // ALWAYS VISIBLE (fancy: gold-trimmed relic plate).
            bool fancySlot = ModContent.GetInstance<global::BiltonsYoyoMod.Configuration.YoyoConfig>().StorageUI
                == global::BiltonsYoyoMod.Configuration.StorageUIStyle.Fancy;
            if (fancySlot) {
                bool evenCol = (displayIndex / 4) % 2 == 0;
                Fill(spriteBatch,
                    new Rectangle((int)position.X, (int)position.Y, SlotSize, SlotSize),
                    evenCol ? new Color(24, 20, 36, 235) : new Color(16, 14, 26, 235));
                Color trim = isHovered ? new Color(255, 225, 150) : new Color(176, 128, 52);
                Fill(spriteBatch,
                    new Rectangle((int)position.X, (int)position.Y, SlotSize, 1), trim);
                Fill(spriteBatch,
                    new Rectangle((int)position.X, (int)position.Y + SlotSize - 1, SlotSize, 1), trim);
                Fill(spriteBatch,
                    new Rectangle((int)position.X, (int)position.Y, 1, SlotSize), trim);
                Fill(spriteBatch,
                    new Rectangle((int)position.X + SlotSize - 1, (int)position.Y, 1, SlotSize), trim);
                Fill(spriteBatch,
                    new Rectangle((int)position.X + 1, (int)position.Y + 1, SlotSize - 2, 1),
                    new Color(255, 240, 200, 60));
            }
            else {
                float backgroundScale =
                    SlotSize /
                    (float)TextureAssets.InventoryBack.Value.Width;

                spriteBatch.Draw(
                    TextureAssets.InventoryBack.Value,
                    position,
                    null,
                    Color.White * 0.75f,
                    0f,
                    Vector2.Zero,
                    backgroundScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // CUSTOM UI ASSET
            // ONLY VISIBLE WHEN THE STORAGE SLOT IS EMPTY.
            if (item == null ||
                item.IsAir)
            {
                DrawPlaceholder(
                    spriteBatch,
                    slotType,
                    position,
                    fancySlot
                );
            }

            // STORED ITEM
            // ALWAYS VISIBLE WHEN PRESENT.
            DrawStoredItem(
                spriteBatch,
                item,
                position
            );

            // HOVER
            if (isHovered)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (item != null &&
                    !item.IsAir)
                {
                    ItemSlot.MouseHover(
                        ref storage.Storage[storageIndex],
                        ItemSlot.Context.EquipAccessory
                    );
                }
                else
                {
                    Main.hoverItemName =
                        GetSlotName(slotType);
                }
            }
        }

        private static Vector2 GetSlotPosition(
            int index)
        {
            int column =
                index / 4;

            int row =
                index % 4;

            int top =
                Main.mapEnabled
                    ? NormalTop
                    : NormalTop - MinimapClosedOffset;

            return new Vector2(
                Main.screenWidth -
                390 -
                column * ColumnGap,

                top +
                row * RowGap
            );
        }

        private static void DrawPlaceholder(
            SpriteBatch spriteBatch,
            YoyoStorageSlotType slotType,
            Vector2 position,
            bool fancy)
        {
            string assetName =
                GetPlaceholderAssetName(slotType);

            string assetPath =
                $"BiltonsYoyoMod/UI/Assets/{assetName}";

            if (!ModContent.RequestIfExists<Texture2D>(
                    assetPath,
                    out Asset<Texture2D> asset))
            {
                return;
            }

            Texture2D texture =
                asset.Value;

            Vector2 center =
                position +
                new Vector2(
                    SlotSize / 2f,
                    SlotSize / 2f
                );

            float scale =
                MathHelper.Min(
                    26f / texture.Width,
                    26f / texture.Height
                );

            Color grayscaleColor = fancy
                ? new Color(212, 175, 105, 170)
                : new Color(
                    128,
                    128,
                    128,
                    128
                );

            spriteBatch.Draw(
                texture,
                center,
                null,
                grayscaleColor,
                0f,
                new Vector2(
                    texture.Width,
                    texture.Height
                ) / 2f,
                scale,
                SpriteEffects.None,
                0f
            );
        }

        private static string GetPlaceholderAssetName(
            YoyoStorageSlotType slotType)
        {
            return slotType switch
            {
                YoyoStorageSlotType.TrickBook =>
                    "TrickBookUIAssets",

                YoyoStorageSlotType.WildString =>
                    "WildStringsUIAssets",

                YoyoStorageSlotType.LooseBearings =>
                    "LooseBearingsUIAssets",

                YoyoStorageSlotType.ResponsePads =>
                    "ResponsePadsUIAssets",

                YoyoStorageSlotType.YoyoAxle =>
                    "YoyoAxleUIAssets",

                YoyoStorageSlotType.TensionBearing =>
                    "TensionBearingUIAssets",

                YoyoStorageSlotType.Fingerloop =>
                    "FingerLoopUIAssets",

                YoyoStorageSlotType.YoyoDuplicator =>
                    "YoyoDuplicatorUIAssets",

                YoyoStorageSlotType.StringCutter =>
                    "StringCutterUIAssets",

                YoyoStorageSlotType.Strings =>
                    "StringsUIAssets",

                YoyoStorageSlotType.Counterweight =>
                    "CounterweightUIAssets",

                YoyoStorageSlotType.YoyoGlove =>
                    "YoyoGloveUIAssets",

                YoyoStorageSlotType.YoyoEmblem =>
                    "YoyoEmblemUIAssets",

                YoyoStorageSlotType.YoyoAccessory =>
                    "YoyoAccessoryUIAssets",

                YoyoStorageSlotType.YoyoPotion =>
                    "YoyoPotionUIAssets",

                _ =>
                    null
            };
        }

        private static void DrawStoredItem(
            SpriteBatch spriteBatch,
            Item item,
            Vector2 position)
        {
            if (item == null ||
                item.IsAir)
            {
                return;
            }

            Texture2D texture = null;
            try {
                if (item.type > Terraria.ID.ItemID.None && item.type < Terraria.ID.ItemID.Count)
                    texture = Main.Assets.Request<Texture2D>(
                        "Images/Item_" + item.type,
                        ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
                else if (item.type < TextureAssets.Item.Length)
                    texture = TextureAssets.Item[item.type].Value;
            }
            catch {
                texture = null;
            }
            if (texture == null || texture.Width <= 0 || texture.Height <= 0)
                return;

            float scale =
                MathHelper.Min(
                    28f / texture.Width,
                    28f / texture.Height
                );

            Vector2 center =
                position +
                new Vector2(
                    SlotSize / 2f,
                    SlotSize / 2f
                );

            spriteBatch.Draw(
                texture,
                center,
                null,
                Color.White,
                0f,
                new Vector2(
                    texture.Width,
                    texture.Height
                ) / 2f,
                scale,
                SpriteEffects.None,
                0f
            );
        }

        private static string GetSlotName(
            YoyoStorageSlotType slotType)
        {
            return slotType switch
            {
                YoyoStorageSlotType.TrickBook =>
                    "Yoyo Trick Book",

                YoyoStorageSlotType.WildString =>
                    "Wild String",

                YoyoStorageSlotType.LooseBearings =>
                    "Loose Bearings",

                YoyoStorageSlotType.ResponsePads =>
                    "Response Pads",

                YoyoStorageSlotType.YoyoAxle =>
                    "Yoyo Axle",

                YoyoStorageSlotType.TensionBearing =>
                    "Tension Bearing",

                YoyoStorageSlotType.Fingerloop =>
                    "Fingerloop",

                YoyoStorageSlotType.YoyoDuplicator =>
                    "Yoyo Duplicator",

                YoyoStorageSlotType.StringCutter =>
                    "String Cutter",

                YoyoStorageSlotType.Strings =>
                    "Strings",

                YoyoStorageSlotType.Counterweight =>
                    "Counterweight",

                YoyoStorageSlotType.YoyoGlove =>
                    "Yoyo Glove",

                YoyoStorageSlotType.YoyoEmblem =>
                    "Yoyo Emblem",

                YoyoStorageSlotType.YoyoAccessory =>
                    "Yoyo Accessory",

                YoyoStorageSlotType.YoyoPotion =>
                    "Yoyo Potion",

                _ =>
                    "Yoyo Storage"
            };
        }

        private static void Fill(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;
            // MagicPixel contains transparent padding; sample only its white
            // texel or wide fills stretch the padding into bright blocks.
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect,
                new Rectangle(0, 0, 1, 1), color);
        }
    }
}