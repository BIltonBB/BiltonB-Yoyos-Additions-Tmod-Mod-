using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content.NPCs.Bosses
{
    // Fix #11: turns out the custom floating bar was never necessary. Star Guardian shows a
    // normal HP bar because it's a plain, undamaged-by-default NPC with boss = false — Terraria
    // draws the standard mouse-over/just-hit life bar above ANY active NPC automatically, boss
    // or not. The shield already has NPC.boss = false (so it doesn't fight for the single
    // top-of-screen boss bar slot), which means it was always eligible for the same standard bar
    // Star Guardian uses; the custom GuardianShieldBarSystem was drawing an extra, wrongly
    // positioned bar on top of it. That system has been removed — the shield now just uses the
    // normal per-NPC bar, positioned by the game itself, same as every other non-boss NPC.
    // Fix #12: orbit radius pushed out further (was a fixed 55px, now 90px).
    // Fix #13: prints a defeat line when a shield is destroyed.
    public class GuardianOrbitShield : ModNPC
    {
        private const float OrbitRadius = 90f; // fix #12

        public override string Texture => "BiltonsYoyoMod/Content/NPCs/Bosses/GuardianOrbitShield";

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MPAllowedEnemies[NPC.type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 40;
            NPC.height = 40;
            NPC.lifeMax = 12000;
            NPC.life = 12000;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.boss = false; // doesn't fight the Guardian for the top boss bar slot
            NPC.netAlways = true;
            NPC.HitSound = SoundID.NPCHit4; // fix #3: same mechanical clang the Guardian itself uses
        }

        public override void AI()
        {
            int master = (int)NPC.ai[0];
            if (master < 0 || master >= Main.maxNPCs || !Main.npc[master].active)
            {
                NPC.active = false;
                return;
            }

            NPC.ai[1] += 0.1f;

            // fix #11 (part 2): NPC.Center now IS the orb position, not the master's own center.
            // The old code kept the shield's hitbox pinned to the boss and only ever drew the
            // orb visual offset from it — which is exactly why any bar Terraria (or the old
            // custom system) drew above "the NPC" showed up centered on the boss instead of on
            // the orb. Since the orb's own position is now the NPC's actual position, the
            // standard built-in life bar follows it correctly, same as it would for any NPC.
            NPC.Center = GetOrbPosition(Main.npc[master].Center);

            LightingHelper.SafeAddLight(NPC.Center, 0.5f, 0.15f, 0.85f); // fix #1

            if (Main.rand.NextBool(4))
                Dust.NewDust(NPC.Center, 4, 4, DustID.PurpleTorch, Scale: 1f);
        }

        public Vector2 GetOrbPosition(Vector2 masterCenter)
        {
            float a = NPC.ai[1] + NPC.ai[2];
            return masterCenter + a.ToRotationVector2() * OrbitRadius;
        }

        public override void OnKill()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                Main.NewText("Guardian Orbit Shield defeated!", 190, 100, 255); // fix #13
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            int master = (int)NPC.ai[0];
            if (master < 0 || master >= Main.maxNPCs || !Main.npc[master].active)
                return false;

            Vector2 pos = NPC.Center; // already the orb position, see AI()
            YoyoDrawing.DrawString(Main.npc[master].Center, pos);

            Texture2D tex = TextureAssets.Projectile[ProjectileID.WoodYoyo].Value;
            spriteBatch.Draw(tex, pos - screenPos, null, new Color(200, 140, 255) * 0.9f,
                NPC.ai[1] + NPC.ai[2], tex.Size() / 2f, 1f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
