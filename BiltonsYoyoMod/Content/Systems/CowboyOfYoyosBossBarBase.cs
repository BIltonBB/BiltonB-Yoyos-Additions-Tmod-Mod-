using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.SubBosses;
using System;

namespace BiltonsYoyoMod.Content.Systems
{
    public class CowboyOfYoyosBossBar : ModBossBar
    {
        public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame)
        {
            return ModContent.Request<Texture2D>(
                "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY1_head"
            );
        }

        public override bool? ModifyInfo(
            ref BigProgressBarInfo info,
            ref float life,
            ref float lifeMax,
            ref float shield,
            ref float shieldMax)
        {
            int type1 = ModContent.NPCType<CowboyOfYoyos1>();
            int type2 = ModContent.NPCType<CowboyOfYoyos2>();
            int type3 = ModContent.NPCType<CowboyOfYoyos3>();

            float totalLife = 0f;
            float totalLifeMax = 0f;
            bool anyAlive = false;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active)
                    continue;

                if (npc.type != type1 &&
                    npc.type != type2 &&
                    npc.type != type3)
                    continue;

                anyAlive = true;

                totalLife += Math.Max(0, npc.life);
                totalLifeMax += npc.lifeMax;
            }

            if (!anyAlive || totalLifeMax <= 0f)
                return false;

            life = totalLife;
            lifeMax = totalLifeMax;
            shield = 0f;
            shieldMax = 0f;

            return true;
        }
    }
}