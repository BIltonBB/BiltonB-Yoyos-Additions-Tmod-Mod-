using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Players
{
    public class RevampPlayer : ModPlayer
    {
        public bool Insight;
        public bool Tangled;
        public bool Barbed;
        public bool WisdomConsumed;

        public int ExtraYoyoRange
        {
            get
            {
                if (Barbed)
                    return 100;

                if (Tangled)
                    return 50;

                return 0;
            }
        }

        public override void ResetEffects()
        {
            Insight = false;
            Tangled = false;
            Barbed = false;
        }

        public override void ModifyLuck(ref float luck)
        {
            if (Insight)
                luck += 0.3f;
        }

        public override void PostUpdateEquips()
        {
            if (WisdomConsumed)
                Player.GetCritChance(
                    DamageClass.MeleeNoSpeed
                ) += 5f;

            if (Barbed)
            {
                Player.yoyoString = true;
                Player.stringColor = Content.NPCs.Town.YoyoTownShared.BlackStringShader;
            }
            else if (Tangled)
            {
                Player.yoyoString = true;
                Player.stringColor = Content.NPCs.Town.YoyoTownShared.SilverStringShader;
            }
        }

        public override void SaveData(TagCompound tag)
        {
            tag["WisdomConsumed"] = WisdomConsumed;
        }

        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("WisdomConsumed"))
                WisdomConsumed = tag.GetBool("WisdomConsumed");
        }
    }
}