using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;
using BiltonsYoyoMod.Configuration;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Content.NPCs.Mobs
{
    // Fix #13 (from the original pass): independent, damageable mob (500 HP) instead of an
    // un-hittable hostile projectile. Turn rate scales down as speed goes up (a slow star can
    // nearly reverse direction; a fast one barely curves).
    // Fix #18 (this pass): now despawns immediately after landing a single hit on a player.
    public class StarGuardianNPC : ModNPC
    {
        private const float HomingSpeed = 11f;
        private const float MaxTurnDegreesSlow = 20f;
        private const float MaxTurnDegreesFast = 4f;
        private const float SpeedForMinTurn = HomingSpeed; // at/above this speed, turning is minimal

        private readonly List<Vector2> trail = new List<Vector2>();

        public override string Texture => "BiltonsYoyoMod/Content/Projectiles/StarYG";

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MPAllowedEnemies[NPC.type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 20;
            NPC.height = 20;
            // Fix #9: fixed base contact damage, multiplied by the Standard/Hell difficulty
            // setting instead of a config slider.
            NPC.damage = (int)(YoyoGuardianBalance.StarGuardianContactDamage * ModContent.GetInstance<YoyoServerConfig>().DamageMultiplier);
            NPC.defense = 0;
            NPC.lifeMax = 500;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath14; // fix #2: NPCDeath2 is vanilla's zombie groan; reuse the Guardian family's own death sound instead
            NPC.value = 0f;
            NPC.netAlways = true;
        }

        // ai[0] = target player index, ai[1] = spin-up timer, ai[2] = spin-up duration
        public override void AI()
        {
            NPC.rotation += 0.2f;
            LightingHelper.SafeAddLight(NPC.Center, 0.5f, 0.15f, 0.85f); // fix #1
            PurpleTrailHelper.Cache(trail, NPC.Center);

            float spinUpDuration = NPC.ai[2] <= 0f ? 60f : NPC.ai[2];

            if (NPC.ai[1] < spinUpDuration)
            {
                NPC.ai[1] += 1f;
                NPC.velocity *= 0.985f;
                return;
            }

            int playerIndex = (int)NPC.ai[0];
            if (playerIndex >= 0 && playerIndex < Main.maxPlayers &&
                Main.player[playerIndex].active && !Main.player[playerIndex].dead)
            {
                Player target = Main.player[playerIndex];
                Vector2 toTarget = target.Center - NPC.Center;

                float currentSpeed = NPC.velocity.Length();
                float speedFraction = MathHelper.Clamp(currentSpeed / SpeedForMinTurn, 0f, 1f);
                float maxTurnDeg = MathHelper.Lerp(MaxTurnDegreesSlow, MaxTurnDegreesFast, speedFraction);
                float maxTurnRad = MathHelper.ToRadians(maxTurnDeg);

                float desiredAngle = toTarget.ToRotation();
                float currentAngle = NPC.velocity.ToRotation();
                float newAngle = MathHelper.WrapAngle(currentAngle + MathHelper.Clamp(
                    MathHelper.WrapAngle(desiredAngle - currentAngle), -maxTurnRad, maxTurnRad));

                NPC.velocity = newAngle.ToRotationVector2() * HomingSpeed;
            }
            else if (NPC.velocity != Vector2.Zero)
            {
                NPC.velocity = Vector2.Normalize(NPC.velocity) * HomingSpeed;
            }
        }

        // fix #18: despawns immediately after landing a hit on a player.
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            NPC.life = 0;
            NPC.HitEffect();
            NPC.checkDead();
            NPC.active = false;
        }

        public override bool CheckActive() => false; // never naturally despawns from distance alone

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 4; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            PurpleTrailHelper.Draw(trail);
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            spriteBatch.Draw(texture, NPC.Center - screenPos, null, drawColor, NPC.rotation,
                texture.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}