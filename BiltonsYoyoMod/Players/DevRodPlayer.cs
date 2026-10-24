using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    // Developer rod-mode state + the reason the rod can "smh do damage":
    // while its bobber is out, it zaps nearby enemies with disco sparks.
    public class DevRodPlayer : ModPlayer
    {
        public bool RodMode;
        public int LastToggle;
        private int zapTick;
        private int ectoTick;

        public override void PostUpdate()
        {
            if (!RodMode)
                return;
            if (Player.HeldItem == null || !(Player.HeldItem.ModItem is Items.DeveloperYoyo))
                return;
            bool bobberOut = false;
            Vector2 bobberPos = Player.Center;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && p.type == ProjectileID.FishingBobber)
                {
                    bobberOut = true;
                    bobberPos = p.Center;
                    break;
                }
            }
            if (!bobberOut || Main.myPlayer != Player.whoAmI)
                return;
            // Ecto fishing: a graveyard bobber on a disco line pulls the dead up.
            if (Player.ZoneGraveyard && ++ectoTick >= 5400)
            {
                ectoTick = 0;
                for (int i = 0; i < 6; i++)
                {
                    Dust ghost = Dust.NewDustPerfect(bobberPos, DustID.WhiteTorch, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.1f);
                    ghost.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item29, bobberPos);
                CombatText.NewText(new Rectangle((int)bobberPos.X - 20, (int)bobberPos.Y - 40, 40, 40), new Color(180, 255, 200), "the dead bite...");
                int drop = Item.NewItem(Player.GetSource_ItemUse(Player.HeldItem), (int)bobberPos.X, (int)bobberPos.Y, 8, 8, ItemID.Ectoplasm, 1);
                if (Main.netMode == NetmodeID.MultiplayerClient && drop >= 0)
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, drop);
            }
            if (++zapTick < 90)
                return;
            zapTick = 0;
            NPC target = null;
            float best = 600f * 600f;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!Projectiles.MiniYoyoFx.IsAttackable(npc))
                    continue;
                float dSq = Vector2.DistanceSquared(Player.Center, npc.Center);
                if (dSq < best)
                {
                    best = dSq;
                    target = npc;
                }
            }
            if (target == null)
                return;
            Vector2 dir = (target.Center - Player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(Player.GetSource_ItemUse(Player.HeldItem), Player.Center, dir * 14f,
                ModContent.ProjectileType<Projectiles.DevFishZapperProj>(), 70, 4f, Player.whoAmI);
            SoundEngine.PlaySound(SoundID.Item15, Player.Center);
        }
    }
}
