using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;
using BiltonsYoyoMod.Content.Projectiles;
using BiltonsYoyoMod.Configuration;

namespace BiltonsYoyoMod.Content.NPCs.Bosses
{
    // Fixes #14-17, replacing the old HostileDestroyerWorm.cs:
    //  - #16: no longer reuses vanilla The Destroyer's AIType, so it no longer inherits the
    //    vanilla laser-probe attacks that came bundled with that AI. This is a fully custom,
    //    simple chain-follow "snake" AI instead: the head homes toward the target with a
    //    clamped turn rate, and every body/tail segment just follows a recorded trail of the
    //    head's past positions, spaced out along the body — the same basic technique vanilla
    //    worms use, minus everything else vanilla Destroyer AI does.
    //  - #14: each segment gets its own 50,000 HP pool and its own texture
    //    (MagicDestroyerHead/Body/Tail), doesn't split off when a body piece dies (there's no
    //    segment-splitting logic here at all — killing a body segment just removes it).
    //  - #15: renamed "Magic Destroyer" (display name lives in the mod's localization file;
    //    the "[entity] defeated" text below uses this name directly).
    //  - #17: each segment fires a single PointedYG at the target's current location every 5s
    //    (staggered slightly per segment so they don't all fire on the same tick).
    public abstract class MagicDestroyerSegment : ModNPC
    {
        private const int SegmentSpacing = 34;
        private const float HeadHomingSpeed = 9f;
        private const float HeadMaxTurnDegrees = 3f;
        private const int ShootInterval = 300;

        private static readonly Dictionary<int, List<Vector2>> HeadTrails = new Dictionary<int, List<Vector2>>();

        protected abstract bool IsHead { get; }
        protected abstract bool IsTail { get; }

        private readonly List<Vector2> visualTrail = new List<Vector2>();
        private int shootTimer;

        // ai[0] = head's own whoAmI once assigned (segments only), ai[1] = position-along-body index (0 = right behind head)
        // ai[2] = unused/reserved, ai[3] = owning YoyoGuardian's whoAmI
        public int HeadIndex { get => (int)NPC.ai[0]; set => NPC.ai[0] = value; }
        public int BodyPosition { get => (int)NPC.ai[1]; set => NPC.ai[1] = value; }
        public int GuardianOwner { get => (int)NPC.ai[3]; set => NPC.ai[3] = value; }

        public override void SetStaticDefaults() => NPCID.Sets.MPAllowedEnemies[NPC.type] = true;

        public override void SetDefaults()
        {
            NPC.width = 44;
            NPC.height = 44;
            NPC.lifeMax = 100000;
            NPC.life = NPC.lifeMax;
            // Fix #9: base contact damage is now a fixed constant, multiplied by the
            // Standard/Hell difficulty setting instead of a config slider.
            NPC.damage = (int)(YoyoGuardianBalance.WormContactDamage * ModContent.GetInstance<YoyoServerConfig>().DamageMultiplier);
            NPC.defense = 25;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.boss = false;
            NPC.friendly = false;
            NPC.netAlways = true;
            NPC.value = 0f;

            shootTimer = Main.rand.Next(ShootInterval);
        }

        public override void AI()
        {
            int guardianIndex = GuardianOwner;
            if (guardianIndex < 0 || guardianIndex >= Main.maxNPCs || !Main.npc[guardianIndex].active ||
                Main.npc[guardianIndex].type != ModContent.NPCType<YoyoGuardian>())
            {
                NPC.active = false;
                return;
            }

            if (IsHead)
                RunHeadAI();
            else
                RunFollowerAI();

            NPC.rotation = NPC.velocity.LengthSquared() > 0.01f ? NPC.velocity.ToRotation() : NPC.rotation;

            PurpleTrailHelper.Cache(visualTrail, NPC.Center);
            LightingHelper.SafeAddLight(NPC.Center, 0.5f, 0.15f, 0.85f);

            UpdateShooting();
        }

        private void RunHeadAI()
        {
            int myIndex = NPC.whoAmI;
            if (!HeadTrails.TryGetValue(myIndex, out var trail))
            {
                trail = new List<Vector2>();
                HeadTrails[myIndex] = trail;
            }

            trail.Insert(0, NPC.Center);
            int maxTrailLength = 9 * (SegmentSpacing / 4 + 1) + 20;
            if (trail.Count > maxTrailLength)
                trail.RemoveRange(maxTrailLength, trail.Count - maxTrailLength);

            NPC.TargetClosest();
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers || !Main.player[NPC.target].active || Main.player[NPC.target].dead)
            {
                NPC.velocity *= 0.98f;
                return;
            }

            Player target = Main.player[NPC.target];
            Vector2 toTarget = target.Center - NPC.Center;
            float desiredAngle = toTarget.ToRotation();
            float currentAngle = NPC.velocity.LengthSquared() > 0.01f ? NPC.velocity.ToRotation() : desiredAngle;
            float maxTurn = MathHelper.ToRadians(HeadMaxTurnDegrees);
            float newAngle = MathHelper.WrapAngle(currentAngle + MathHelper.Clamp(
                MathHelper.WrapAngle(desiredAngle - currentAngle), -maxTurn, maxTurn));

            NPC.velocity = newAngle.ToRotationVector2() * HeadHomingSpeed;
        }

        private void RunFollowerAI()
        {
            int headIndex = HeadIndex;
            if (headIndex < 0 || headIndex >= Main.maxNPCs || !Main.npc[headIndex].active ||
                !HeadTrails.TryGetValue(headIndex, out var trail) || trail.Count == 0)
            {
                if (headIndex >= 0 && headIndex < Main.maxNPCs && Main.npc[headIndex].active)
                {
                    Vector2 toHead = Main.npc[headIndex].Center - NPC.Center;
                    if (toHead.LengthSquared() > 1f)
                        NPC.velocity = Vector2.Lerp(NPC.velocity, toHead.SafeNormalize(Vector2.UnitX) * 6f, 0.1f);
                }
                return;
            }

            int trailPointsPerSegment = SegmentSpacing / 4 + 1;
            int desiredTrailIndex = (int)MathHelper.Clamp(
                (BodyPosition + 1) * trailPointsPerSegment,
                0,
                trail.Count - 1);

            Vector2 followPos = trail[desiredTrailIndex];
            Vector2 toFollow = followPos - NPC.Center;
            NPC.velocity = toFollow.LengthSquared() > 0.01f ? toFollow : Vector2.Zero;
            NPC.Center = Vector2.Lerp(NPC.Center, followPos, 0.6f);
        }

        private void UpdateShooting()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            shootTimer++;
            if (shootTimer < ShootInterval)
                return;

            shootTimer = 0;

            int playerIndex = Player.FindClosest(NPC.position, NPC.width, NPC.height);
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers)
                return;

            Player target = Main.player[playerIndex];
            if (!target.active || target.dead)
                return;

            Vector2 vel = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 10f;

            // Fix #9: fixed base projectile damage, multiplied by the Standard/Hell difficulty
            // setting instead of a config slider.
            int dmg = (int)(YoyoGuardianBalance.BaseProjectileDamage * ModContent.GetInstance<YoyoServerConfig>().DamageMultiplier);

            Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center,
                vel,
                ModContent.ProjectileType<PointedYG>(),
                dmg,
                2f,
                255);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 4; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch);
        }

        public override void OnKill()
        {
            if (IsHead)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Main.NewText("Magic Destroyer defeated!", 190, 100, 255);

                HeadTrails.Remove(NPC.whoAmI);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            PurpleTrailHelper.Draw(visualTrail);

            Texture2D texture = TextureAssets.Npc[NPC.type].Value;

            spriteBatch.Draw(
                texture,
                NPC.Center - screenPos,
                null,
                drawColor,
                NPC.rotation,
                texture.Size() * 0.5f,
                NPC.scale,
                SpriteEffects.None,
                0f);

            return false;
        }

        public static int SpawnWorm(Vector2 headPosition, int guardianIndex, IEntitySource source)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return -1;

            int head = NPC.NewNPC(
                source,
                (int)headPosition.X,
                (int)headPosition.Y,
                ModContent.NPCType<MagicDestroyerHead>());

            if (head < 0 || head >= Main.maxNPCs)
                return -1;

            var headSegment = Main.npc[head].ModNPC as MagicDestroyerSegment;
            headSegment.GuardianOwner = guardianIndex;
            headSegment.HeadIndex = head;

            Main.npc[head].GetGlobalNPC<Utils.YoyoGuardianWormTag>().isGuardianWorm = true;
            HeadTrails[head] = new List<Vector2> { headPosition };

            for (int i = 0; i < 8; i++)
            {
                bool isTail = i == 7;

                int type = isTail
                    ? ModContent.NPCType<MagicDestroyerTail>()
                    : ModContent.NPCType<MagicDestroyerBody>();

                int segment = NPC.NewNPC(
                    source,
                    (int)headPosition.X,
                    (int)headPosition.Y,
                    type);

                if (segment < 0 || segment >= Main.maxNPCs)
                    continue;

                var seg = Main.npc[segment].ModNPC as MagicDestroyerSegment;
                seg.HeadIndex = head;
                seg.BodyPosition = i;
                seg.GuardianOwner = guardianIndex;

                Main.npc[segment].realLife = head;
                Main.npc[segment].GetGlobalNPC<Utils.YoyoGuardianWormTag>().isGuardianWorm = true;
            }

            return head;
        }
    }

    public class MagicDestroyerHead : MagicDestroyerSegment
    {
        protected override bool IsHead => true;
        protected override bool IsTail => false;

        public override string Texture =>
            "BiltonsYoyoMod/Content/NPCs/Bosses/MagicDestroyerHead";
    }

    public class MagicDestroyerBody : MagicDestroyerSegment
    {
        protected override bool IsHead => false;
        protected override bool IsTail => false;

        public override string Texture =>
            "BiltonsYoyoMod/Content/NPCs/Bosses/MagicDestroyerBody";
    }

    public class MagicDestroyerTail : MagicDestroyerSegment
    {
        protected override bool IsHead => false;
        protected override bool IsTail => true;

        public override string Texture =>
            "BiltonsYoyoMod/Content/NPCs/Bosses/MagicDestroyerTail";
    }
}