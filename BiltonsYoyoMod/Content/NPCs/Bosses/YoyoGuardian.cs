using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles;
using BiltonsYoyoMod.Content.NPCs.Mobs;
using BiltonsYoyoMod.Configuration;
using BiltonsYoyoMod.Content.Utils;
using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Content.NPCs.Bosses
{
    public enum YoyoGuardianPhase
    {
        Intro,
        Phase1,
        Transition1,
        Phase2,
        Transition2,
        Desperation,
        // everything below is Expert+-only content (additions #1-#9)
        DesperationPre,
        TransitionToPhase3,
        Phase3,
        Phase3DeathSequence
    }

    public class YoyoGuardian : ModNPC
    {
        private const float OrbitRadius = 300f;
        private const float AggroRadius = 500f;

        private const float A3RowSpacing = 200f;
        private const float A3RowDistance = 1000f;
        private const float A3RowSpeed = 6.75f;

        private const int Phase1SlotDuration = 600; // 10 seconds
        private const int Phase1SlotCount = 3;
        private const int Phase2SlotDuration = 900; // 15 seconds
        private const int Phase2SlotCount = 9;

        private const int A4LineLength = 2000;   // fix #7 (original pass): doubled from 1000
        private const int A4OffsetStep = 200;
        private const int A4DustTime = 80;
        private const int A4SpawnTime = 95;
        private const int A4Interval = 180;   // every 3s
        private const int A4MaxFires = 5;

        private YoyoGuardianPhase phase = YoyoGuardianPhase.Intro;
        public YoyoGuardianPhase Phase => phase;
        public bool IsRealBoss { get; private set; } = false;

        // instance registry (fix #3, original pass)
        // Fix #6 (this pass): capped down from 2 to 1 simultaneous "real" (non-clone) Guardians.
        // Clones (A6 mirrors, desperation dashers) are never counted and this whole registry is
        // completely untouched for them — see RunA6Clone / RunDesperationClone, which both
        // return before reaching this code.
        //
        // Arena fix: this used to be a static HashSet<int> of NPC.whoAmI indices, updated by
        // hand (added when a Guardian registered itself, removed in CheckDead/OnKill). That
        // only ever got cleaned up on a normal boss kill — any OTHER way a Guardian stopped
        // existing (EncourageDespawn firing when it lost its target, the world being exited
        // mid-fight, or just moving between worlds while the mod stayed loaded) left its index
        // sitting in the set forever, since nothing else was left alive to run the cleanup
        // pass. A single stale entry was enough to permanently convince the mod a real
        // Guardian already existed, so the NEXT one summoned deactivated itself on its very
        // first AI() tick — before ever reaching the arena-spawn code below. That's the "item
        // never spawns the arena" bug. Recomputing this live, by scanning currently-active
        // NPCs, removes the stale state entirely: there's nothing left to go stale.
        public const int MaxSimultaneousRealGuardians = 1;

        public static int ActiveRealGuardianCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC n = Main.npc[i];
                    if (n.active && n.type == ModContent.NPCType<YoyoGuardian>() &&
                        n.ModNPC is YoyoGuardian g && g.IsRealBoss)
                        count++;
                }
                return count;
            }
        }

        // Fix #7/#9: the player-count HP scaling factor Terraria hands us in
        // ApplyDifficultyAndPlayerScaling, stashed so Phase 3's HP (recomputed on entry, in
        // EnterPhase3) also scales with player count instead of only using its own fixed figure.
        private float playerHPBalance = 1f;

        private YoyoServerConfig Cfg => ModContent.GetInstance<YoyoServerConfig>();

        // intro speech (fix #9, original pass)
        private int introTimer;
        private int introLineIndex = -1;
        private static readonly int[] IntroLineTicks = { 0, 240, 300 };

        private int slotIndex;
        private int slotTimer;
        private int subTimer;
        private int movementTimer;
        private float orbitAngle;

        private readonly bool[] phase1Variant = new bool[Phase1SlotCount];
        private readonly bool[] phase2Variant = new bool[Phase2SlotCount];

        private int[] a3cDelays;
        private bool[] a3cFired;

        private int a4FireCount;
        private class A4Line { public Vector2 Center; public float Angle; public int Timer; public int YoyoIndex = -1; }
        private readonly List<A4Line> activeA4Lines = new List<A4Line>();

        private bool a5Active;
        private int a5Timer;
        private float a5Angle;

        private bool a6Done;

        private int transitionTicks;
        private int desperationTimer;
        private bool desperationClonesSpawned;
        private int trackingYoyoIndex = -1;
        private int arenaBoxIndex = -1;

        // hostile worm add-on
        private bool wormSpawned;
        private int wormHeadIndex = -1;
        public bool WormActiveDamageHalved { get; private set; }

        // AB1 telegraph line state (Phase 1/2 legacy telegraphed bearing bursts — untouched)
        private float[] abLineAngles;
        private int abLineTimer;

        private readonly List<(int ticksLeft, Vector2 pos, Vector2 vel)> pendingRowSpawns = new List<(int, Vector2, Vector2)>();

        private void QueueRowSpawn(int delayTicks, Vector2 pos, Vector2 vel) => pendingRowSpawns.Add((delayTicks, pos, vel));

        private void UpdatePendingSpawns()
        {
            if (pendingRowSpawns.Count == 0) return;
            for (int i = pendingRowSpawns.Count - 1; i >= 0; i--)
            {
                var entry = pendingRowSpawns[i];
                if (entry.ticksLeft <= 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), entry.pos, entry.vel,
                            ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
                    pendingRowSpawns.RemoveAt(i);
                }
                else
                {
                    pendingRowSpawns[i] = (entry.ticksLeft - 1, entry.pos, entry.vel);
                }
            }
        }

        public static int CameraFocusNPCIndex = -1;

        private readonly List<TrailPoint> trailHistory = new List<TrailPoint>();
        private const int MaxTrailPoints = 80;
        private const float TrailSpacing = 24f;
        private const float TrailMaxDistance = 360f;

        private struct TrailPoint { public Vector2 Center; public float Rotation; }

        // Addition #1-#9 (Expert+ Phase 3) state
        private int desperationPreTimer;
        private const int DesperationPreDuration = 120; // 2 seconds

        private static readonly int[] P3LineTicks = { 1, 300, 600, 900, 1200, 1500, 1800 };
        private static readonly string[] P3TransitionLines =
        {
            "Interesting.",
            "You thought I would give up?",
            "I have more to offer.",
            "This trial is the test of endurance.",
            "This will truly be my final test.",
            "The final time.",
            "We will fight!",
        };
        private int transitionToP3Timer;
        private int p3SpeechIndex = -1;

        private const int Phase3SlotDuration = 600; // 10 seconds per B1/B2/B3
        private int p3SlotIndex;
        private int p3SlotTimer;

        private int p3B1ShotTimer;
        private int p3B1StarTimer;

        private int p3B2CycleTimer;
        private Vector2 p3B2Position;
        private Vector2 p3B2DestinationPreview; // fix #4: telegraphed ahead of the teleport

        private int p3B3Timer;

        private int[] ap1YoyoIndices; // addition #7: AP1's 10 permanent orbiters

        private class P3Telegraph { public Vector2 Origin; public float Angle; public int Timer; public Action OnFire; }
        private readonly List<P3Telegraph> p3Telegraphs = new List<P3Telegraph>();

        private void QueueP3Telegraph(Vector2 origin, float angle, Action onFire) =>
            p3Telegraphs.Add(new P3Telegraph { Origin = origin, Angle = angle, Timer = 60, OnFire = onFire });

        private void UpdateP3Telegraphs()
        {
            for (int i = p3Telegraphs.Count - 1; i >= 0; i--)
            {
                var t = p3Telegraphs[i];
                t.Timer--;
                if (t.Timer <= 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient) t.OnFire?.Invoke();
                    p3Telegraphs.RemoveAt(i);
                }
            }
        }

        private int phase3DeathTimer;
        private const int Phase3DeathDuration = 180; // 3 seconds

        public override string Texture => phase >= YoyoGuardianPhase.DesperationPre
            ? "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardianDP"
            : "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardian";

        public override string BossHeadTexture => phase >= YoyoGuardianPhase.DesperationPre
            ? "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardian_Head_BossDP"
            : "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardian_Head_Boss";

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MPAllowedEnemies[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);
        }

        public override void SetDefaults()
        {
            NPC.width = 90;
            NPC.height = 90;
            NPC.damage = 0;
            NPC.defense = 40;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.npcSlots = 10f;
            NPC.lifeMax = 200000; // classic baseline for the bestiary preview
            NPC.life = NPC.lifeMax;
            NPC.knockBackResist = 0f; // fix #3: fully knockback resistant
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(gold: 50);
            NPC.netAlways = true;
            NPC.aiStyle = -1;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            // Fix #7/#9: fixed base HP per vanilla difficulty (see YoyoGuardianBalance), scaled
            // by both the player-count "balance" factor Terraria provides here — same as
            // vanilla bosses, so HP goes up automatically with more players without any manual
            // per-player-count table — and the Standard/Hell multiplier from the config.
            int baseHp = Main.masterMode ? YoyoGuardianBalance.MasterHPBase
                : (Main.expertMode ? YoyoGuardianBalance.ExpertHPBase : YoyoGuardianBalance.ClassicHPBase);

            playerHPBalance = balance;

            int hp = (int)(baseHp * balance * Cfg.HPMultiplier);
            NPC.lifeMax = hp;
            NPC.life = hp;
        }

        private bool InUnderworld => NPC.Center.Y > (Main.maxTilesY - 200) * 16f;
        private float AttackFreqMult => InUnderworld ? 0.5f : 1f;

        private int Dmg(int baseDamage)
        {
            // Fix #9: Standard/Hell difficulty multiplier folded in here, so every call site
            // that already routes through Dmg() picks it up automatically.
            float mult = Cfg.DamageMultiplier;
            if (InUnderworld) mult *= YoyoGuardianBalance.UnderworldDamageMultiplier;
            return (int)(baseDamage * mult);
        }

        private int ScaleTime(int ticks) => Math.Max(1, (int)(ticks * AttackFreqMult));

        public override void AI()
        {
            int targetIdx = NPC.target;
            if (targetIdx < 0 || targetIdx >= Main.maxPlayers || !Main.player[targetIdx].active || Main.player[targetIdx].dead)
            {
                NPC.TargetClosest();
                targetIdx = NPC.target;
            }
            if (targetIdx < 0 || targetIdx >= Main.maxPlayers || !Main.player[targetIdx].active || Main.player[targetIdx].dead)
            {
                // No valid target: drop the arena now so a targetless
                // despawn can't leak a box for the next fight to adopt.
                KillArenaBox();
                NPC.velocity.Y -= 0.04f;
                NPC.EncourageDespawn(300);
                return;
            }
            Player target = Main.player[targetIdx];

            var cloneTag = NPC.GetGlobalNPC<YoyoGuardianCloneTag>();
            // Clone AI is untouched below (RunA6Clone unchanged; RunDesperationClone gained a
            // phase check for fix #3 — see below).
            if (cloneTag.isA6Clone) { IsRealBoss = false; RunA6Clone(cloneTag, target); return; }
            if (cloneTag.isDesperationClone) { IsRealBoss = false; RunDesperationClone(cloneTag, target); return; }
            IsRealBoss = true;

            // Arena fix: live leader-election instead of the old persisted registry (see the
            // comment above ActiveRealGuardianCount). If more than MaxSimultaneousRealGuardians
            // non-clone Guardians are simultaneously active, every one except the lowest
            // whoAmI steps aside — same pattern CowboyOfYoyos already uses for its own leader
            // election. Nothing here is stored between ticks, so nothing can go stale between
            // fights, deaths, or world sessions.
            int lowerIndexRealGuardians = 0;
            for (int i = 0; i < NPC.whoAmI; i++)
            {
                NPC other = Main.npc[i];
                if (!other.active || other.type != NPC.type) continue;
                if (other.ModNPC is not YoyoGuardian otherGuardian || !otherGuardian.IsRealBoss) continue;
                lowerIndexRealGuardians++;
            }

            if (lowerIndexRealGuardians >= MaxSimultaneousRealGuardians)
            {
                NPC.active = false;
                return;
            }

            NPC.boss = true;

            // SPEC: one stationary arena box for the whole fight, including the
            // Desperation dash (same home throughout; purple border at P3).
			{
				arenaBoxIndex = GuardianArenaBox.EnsureExists(NPC, ref arenaBoxIndex);

				// On multiplayer clients the arena is owned by the server: no box
				// here is normal, so just skip arena handling instead of warning
				// and aborting the whole AI tick (which froze the boss clientside).
				bool boxSlotOk = arenaBoxIndex >= 0 && arenaBoxIndex < Main.maxProjectiles
					&& Main.projectile[arenaBoxIndex].active;
				if (boxSlotOk && Main.projectile[arenaBoxIndex].ModProjectile is Content.Projectiles.GuardianArenaBox arenabox)
				{
					// SPEC: same stationary box all fight, purple border only in P3.
					bool wantPurple = phase == YoyoGuardianPhase.Phase3 || phase == YoyoGuardianPhase.Phase3DeathSequence;
					if (arenabox.PurpleBorder != wantPurple)
					{
						arenabox.PurpleBorder = wantPurple;
						Main.projectile[arenaBoxIndex].netUpdate = true;
					}
				}
				else if (Main.netMode != NetmodeID.MultiplayerClient)
				{
					arenaBoxIndex = -1; // Force a clean retry next tick.
				}
			}

            if (phase == YoyoGuardianPhase.Intro)
            {
                RunIntro();
                UpdateSpin();
                RecordTrailPoint();
                return;
            }

            float hpPercent = (float)NPC.life / NPC.lifeMax;

            if (phase == YoyoGuardianPhase.Phase1 && hpPercent <= 0.60f)
            {
                phase = YoyoGuardianPhase.Transition1;
                transitionTicks = 0;
                SpawnShield();
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }
            else if (phase == YoyoGuardianPhase.Phase2 && Main.expertMode && hpPercent <= 0.05f)
            {
                phase = YoyoGuardianPhase.Transition2;
                transitionTicks = 0;
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }

            if (!wormSpawned && hpPercent <= 0.50f && phase != YoyoGuardianPhase.Intro)
            {
                wormSpawned = true;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    wormHeadIndex = MagicDestroyerSegment.SpawnWorm(NPC.Center + new Vector2(400f, 0f), NPC.whoAmI, NPC.GetSource_FromAI());
                    ShowSpeechLine("Face the magic of yoyos!", new Color(190, 100, 255), forceImmediate: true);
                }
            }

            bool shieldProtects = CountAliveShields() > 0;
            WormActiveDamageHalved = IsWormAlive();

            if (phase == YoyoGuardianPhase.Transition1 || phase == YoyoGuardianPhase.Transition2 ||
                phase == YoyoGuardianPhase.Desperation || shieldProtects)
            {
                NPC.dontTakeDamage = true;
            }
            else if (phase == YoyoGuardianPhase.Phase1 || phase == YoyoGuardianPhase.Phase2)
            {
                NPC.dontTakeDamage = false;
            }
            // DesperationPre / TransitionToPhase3 / Phase3 / Phase3DeathSequence manage
            // NPC.dontTakeDamage themselves inside their own Run* methods below.

            switch (phase)
            {
                case YoyoGuardianPhase.Phase1:
                    OrbitFollowMovement(target, InUnderworld ? 2f : 1f);
                    RunPhase1();
                    break;
                case YoyoGuardianPhase.Transition1:
                    RunTransition1();
                    break;
                case YoyoGuardianPhase.Phase2:
                    if (slotIndex == 7 && a5Active) RunA5();
                    else { OrbitFollowMovement(target, InUnderworld ? 2f : 1f); RunPhase2(); }
                    break;
                case YoyoGuardianPhase.Transition2:
                    RunTransition2();
                    break;
                case YoyoGuardianPhase.Desperation:
                    RunDesperation(target);
                    break;
                case YoyoGuardianPhase.DesperationPre:
                    RunDesperationPre();
                    break;
                case YoyoGuardianPhase.TransitionToPhase3:
                    RunTransitionToPhase3();
                    break;
                case YoyoGuardianPhase.Phase3:
                    RunPhase3(target);
                    break;
                case YoyoGuardianPhase.Phase3DeathSequence:
                    RunPhase3DeathSequence();
                    break;
            }

            UpdateTelegraph();
            UpdateP3Telegraphs();
            UpdatePendingSpawns();
            UpdateSpin();
            RecordTrailPoint();
        }

        // Intro

        private void RunIntro()
        {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.9f;

            if (introLineIndex == -1)
            {
                introLineIndex = 0;
                introTimer = 0;
                ShowSpeechLine(IntroLine(0), IntroColor(0), forceImmediate: true);
            }

            introTimer++;
            int gap = SpeechGapTicks(IntroLine(introLineIndex));

            if (introLineIndex < 2 && introTimer >= gap)
            {
                introLineIndex++;
                introTimer = 0;
                ShowSpeechLine(IntroLine(introLineIndex), IntroColor(introLineIndex), forceImmediate: true);
            }
            else if (introLineIndex == 2 && introTimer >= gap)
            {
                phase = YoyoGuardianPhase.Phase1;
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }
        }

        private string IntroLine(int i)
        {
            if (!InUnderworld)
            {
                return i switch
                {
                    0 => "This is the trial to determine if you actually are capable of using yoyos.",
                    1 => "Well enough talking, lets start this trial",
                    _ => "Good luck",
                };
            }
            return i switch
            {
                0 => "This is the trial to determine if you actually are capable of using yoyos.",
                1 => "and you dare to fight me in the underworld where i feed off the most?",
                _ => "Good luck trying to survive",
            };
        }

        private Color IntroColor(int i) => InUnderworld ? new Color(255, 90, 60) : new Color(190, 100, 255);

        // Fix #2: added an optional per-line scale (defaults to normal size); everything but
        // the "outer shell cracked" line still calls this with the default.
        private void ShowSpeechLine(string text, Color color, bool forceImmediate, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Main.NewText(text, color.R, color.G, color.B);
            YoyoGuardianDialogueSystem.Enqueue(text, color, scale);
        }

        private int SpeechGapTicks(string text) => Math.Max(120, 90 + text.Length * 3);

        // Shared helpers

        private void RecordTrailPoint()
        {
            trailHistory.Add(new TrailPoint { Center = NPC.Center, Rotation = NPC.rotation });
            while (trailHistory.Count > MaxTrailPoints) trailHistory.RemoveAt(0);
        }

        private int CountAliveShields()
        {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
                if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<GuardianOrbitShield>() && (int)Main.npc[i].ai[0] == NPC.whoAmI)
                    count++;
            return count;
        }

        private bool IsWormAlive()
        {
            if (wormHeadIndex < 0 || wormHeadIndex >= Main.maxNPCs) return false;
            return Main.npc[wormHeadIndex].active && Main.npc[wormHeadIndex].type == ModContent.NPCType<MagicDestroyerHead>();
        }

        private void OrbitFollowMovement(Player target, float speedMult = 1f, float accelLerp = 0.05f)
        {
            float dist = Vector2.Distance(NPC.Center, target.Center);
            if (dist > AggroRadius)
            {
                Vector2 dir = target.Center - NPC.Center;
                dir.Normalize();
                NPC.velocity = Vector2.Lerp(NPC.velocity, dir * 6f * speedMult, accelLerp * 0.4f * speedMult);
                return;
            }

            movementTimer++;
            if (movementTimer >= 90 || Vector2.Distance(NPC.Center, target.Center + orbitAngle.ToRotationVector2() * OrbitRadius) < 24f)
            {
                orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                movementTimer = 0;
            }

            Vector2 destination = target.Center + orbitAngle.ToRotationVector2() * OrbitRadius;
            Vector2 toDest = destination - NPC.Center;
            float distToDest = toDest.Length();
            Vector2 desiredVelocity = distToDest > 2f
                ? toDest / distToDest * MathHelper.Clamp(distToDest * 0.08f * speedMult, 0f, 9f * speedMult)
                : Vector2.Zero;

            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, accelLerp * speedMult);
        }

        private void UpdateSpin()
        {
            float targetSpinSpeed = MathHelper.Lerp(0.02f, 0.35f, MathHelper.Clamp(NPC.velocity.Length() / 10f, 0f, 1f));
            if (phase == YoyoGuardianPhase.Transition1 || phase == YoyoGuardianPhase.Transition2 ||
                phase == YoyoGuardianPhase.TransitionToPhase3 || a5Active)
                targetSpinSpeed = 0.5f;
            NPC.rotation += targetSpinSpeed;
            LightingHelper.SafeAddLight(NPC.Center, 0.5f, 0.15f, 0.85f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Rectangle frame = NPC.frame.Width > 0 && NPC.frame.Height > 0 ? NPC.frame : new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 origin = frame.Size() / 2f;
            float alpha = 1f - NPC.alpha / 255f;
            Color afterimageBase = Color.Lerp(new Color(180, 120, 255), drawColor, 0.5f) * alpha;

            Vector2? lastDrawn = null;
            for (int i = 0; i < trailHistory.Count; i++)
            {
                float dist = Vector2.Distance(trailHistory[i].Center, NPC.Center);
                if (dist > TrailMaxDistance) continue;
                if (lastDrawn.HasValue && Vector2.Distance(trailHistory[i].Center, lastDrawn.Value) < TrailSpacing) continue;
                lastDrawn = trailHistory[i].Center;

                float fade = 1f - dist / TrailMaxDistance;
                Color afterimageColor = afterimageBase * (fade * 0.35f);
                Vector2 drawPos = trailHistory[i].Center - screenPos;
                spriteBatch.Draw(texture, drawPos, frame, afterimageColor, trailHistory[i].Rotation, origin, NPC.scale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(texture, NPC.Center - screenPos, frame, drawColor * alpha, NPC.rotation, origin, NPC.scale, SpriteEffects.None, 0f);

            DrawTelegraphLines(spriteBatch, screenPos);
            return false;
        }

        private void DrawTelegraphLines(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            if (abLineAngles != null && abLineTimer > 0)
            {
                float progress = abLineTimer > 30 ? (60 - abLineTimer) / 30f : abLineTimer / 30f;
                float alpha = MathHelper.Clamp(progress, 0f, 1f);

                foreach (float angle in abLineAngles)
                {
                    Vector2 start = NPC.Center - screenPos;
                    Vector2 end = start + angle.ToRotationVector2() * 2000f;
                    Vector2 diff = end - start;
                    spriteBatch.Draw(pixel, (start + end) * 0.5f, new Rectangle(0, 0, 1, 1),
                        new Color(160, 60, 230) * (alpha * 0.6f), diff.ToRotation(), new Vector2(0.5f, 0.5f),
                        new Vector2(diff.Length(), 2f), SpriteEffects.None, 0f);
                }
            }

            // Addition #7: Phase 3's independent B1/B2/B3 telegraphs, drawn the same way.
            // Fix #6: each also gets a small pulsing origin dot, so the exact firing point
            // reads clearly instead of just an abstract line across the screen.
            foreach (var t in p3Telegraphs)
            {
                float progress = t.Timer > 30 ? (60 - t.Timer) / 30f : t.Timer / 30f;
                float alpha = MathHelper.Clamp(progress, 0f, 1f);

                Vector2 start = t.Origin - screenPos;
                Vector2 end = start + t.Angle.ToRotationVector2() * 2000f;
                Vector2 diff = end - start;
                spriteBatch.Draw(pixel, (start + end) * 0.5f, new Rectangle(0, 0, 1, 1),
                    new Color(160, 60, 230) * (alpha * 0.6f), diff.ToRotation(), new Vector2(0.5f, 0.5f),
                    new Vector2(diff.Length(), 2f), SpriteEffects.None, 0f);

                float dotPulse = 8f + (float)Math.Sin(Main.GameUpdateCount * 0.3f) * 2f;
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1),
                    new Color(220, 150, 255) * alpha, 0f, new Vector2(0.5f, 0.5f),
                    new Vector2(dotPulse, dotPulse), SpriteEffects.None, 0f);
            }
        }

        // Arena / guardian yoyo spawner

        private void KillArenaBox()
        {
            // Clean fight end (despawn/Desperation/death): the next summon
            // centers fresh on the boss instead of restoring this home.
            GuardianArenaBox.ForgetHome();
            // SPEC: despawns with the boss. Kill the cached slot AND any
            // orphan bound to this guardian, so fled/despawned fights can't
            // leak a box for the next fight to adopt.
            if (arenaBoxIndex >= 0 && arenaBoxIndex < Main.maxProjectiles && Main.projectile[arenaBoxIndex].active)
            {
                Main.projectile[arenaBoxIndex].Kill();
                Main.projectile[arenaBoxIndex].netUpdate = true;
            }
            int boxType = ModContent.ProjectileType<Content.Projectiles.GuardianArenaBox>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile q = Main.projectile[i];
                if (q.active && q.type == boxType && (int)q.ai[0] == NPC.whoAmI)
                {
                    q.Kill();
                    q.netUpdate = true;
                }
            }
            arenaBoxIndex = -1;
        }

        private int SpawnGuardianYoyo(Vector2 velocity, int damage, int lifetime, float offset, int mode, int targetPlayer = -1)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return -1;

            Vector2 spawnPos = NPC.Center;
            if (mode == 1 && velocity != Vector2.Zero)
            {
                Vector2 dir = velocity.SafeNormalize(Vector2.Zero);
                spawnPos += dir.RotatedBy(MathHelper.PiOver2) * offset;
            }

            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, velocity,
                ModContent.ProjectileType<StoneGuardianYoyo>(), Dmg(damage), 2f, 255,
                targetPlayer >= 0 ? targetPlayer : NPC.target, NPC.whoAmI);

            if (p < 0 || p >= Main.maxProjectiles) return -1;

            Projectile proj = Main.projectile[p];
            proj.timeLeft = lifetime;
            proj.ai[2] = mode;
            proj.netUpdate = true;
            return p;
        }

        private void SpawnStar(Vector2 spawnPos, float spinUpTicks = 60f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<StarGuardianNPC>());
            if (idx < 0 || idx >= Main.maxNPCs) return;
            Main.npc[idx].ai[0] = NPC.target;
            Main.npc[idx].ai[2] = spinUpTicks;
            Main.npc[idx].netUpdate = true;
        }

        private void SpawnDirectedStar(Vector2 origin, Vector2 dir)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)origin.X, (int)origin.Y, ModContent.NPCType<StarGuardianNPC>());
            if (idx < 0 || idx >= Main.maxNPCs) return;
            Main.npc[idx].ai[0] = NPC.target;
            Main.npc[idx].ai[2] = 6f;
            Main.npc[idx].velocity = dir.RotatedByRandom(MathHelper.ToRadians(10f)) * 10f;
            Main.npc[idx].netUpdate = true;
        }

        private void UpdateRepeatingAttack(int interval, int maxCount, Action fire)
        {
            int scaledInterval = ScaleTime(interval);
            int firedCount = (slotTimer - 1) / scaledInterval + 1;
            if (slotTimer == 1 || (slotTimer - 1) % scaledInterval == 0)
                if (firedCount <= maxCount) fire();
        }

        private Vector2 GetYoyoOrGuardianCenter()
        {
            int idx = GetTrackingYoyoIndex();
            return idx != -1 ? Main.projectile[idx].Center : NPC.Center;
        }

        private int GetTrackingYoyoIndex()
        {
            if (trackingYoyoIndex >= 0 && trackingYoyoIndex < Main.maxProjectiles)
            {
                Projectile p = Main.projectile[trackingYoyoIndex];
                if (p.active && p.type == ModContent.ProjectileType<StoneGuardianYoyo>() && (int)p.ai[1] == NPC.whoAmI)
                    return trackingYoyoIndex;
            }
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<StoneGuardianYoyo>() && (int)p.ai[1] == NPC.whoAmI)
                    return i;
            }
            return -1;
        }

        // AB1 telegraph (telegraphed bearing burst) — Phase 1/2 only, untouched

        private static readonly int[] AB1Counts = { 3, 5, 8, 12, 24 };

        private void StartTelegraphedBearings()
        {
            int count = AB1Counts[Main.rand.Next(AB1Counts.Length)];
            abLineAngles = new float[count];
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < count; i++)
                abLineAngles[i] = baseAngle + MathHelper.TwoPi / count * i;

            abLineTimer = 60;
        }

        private void UpdateTelegraph()
        {
            if (abLineAngles == null) return;
            abLineTimer--;
            if (abLineTimer <= 0)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    foreach (float angle in abLineAngles)
                    {
                        Vector2 vel = angle.ToRotationVector2() * 11f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                            ModContent.ProjectileType<Content.Projectiles.LooseStoneBearingsYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
                    }
                }
                abLineAngles = null;
            }
        }

        // Phase 1

        private void RunPhase1()
        {
            int duration = ScaleTime(Phase1SlotDuration);
            slotTimer++;

            if (slotTimer <= duration)
            {
                bool variant = phase1Variant[slotIndex];
                switch (slotIndex)
                {
                    case 0:
                        if (variant) UpdateRepeatingAttack(120, 5, StartTelegraphedBearings);
                        else UpdateRepeatingAttack(120, 5, SpawnA1a);
                        break;

                    case 1:
                        if (variant)
                        {
                            if (slotTimer == 1) SpawnAB2a();
                            UpdateRepeatingAttack(120, 5, SpawnAB2b);
                        }
                        else
                        {
                            if (slotTimer == 1) SpawnA2a();
                            UpdateRepeatingAttack(120, 5, SpawnA2b);
                        }
                        break;

                    case 2:
                    {
                        int staggerGap = ScaleTime(30);
                        bool waveA = slotTimer == 1 || slotTimer == ScaleTime(300) + 1;
                        bool waveB = slotTimer == 1 + staggerGap || slotTimer == ScaleTime(300) + 1 + staggerGap;

                        if (waveA) { if (variant) SpawnAB3a(); else SpawnA3a(); }
                        if (waveB) { if (variant) SpawnAB3b(); else SpawnA3b(); }
                        break;
                    }
                }
            }
            else
            {
                phase1Variant[slotIndex] = !phase1Variant[slotIndex];
                slotIndex = (slotIndex + 1) % Phase1SlotCount;
                slotTimer = 0;
            }
        }

        private void SpawnA1a()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < 18; i++)
            {
                float angle = MathHelper.TwoPi / 18f * i;
                Vector2 vel = angle.ToRotationVector2() * 11f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<Content.Projectiles.LooseStoneBearingsYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
            }
        }

        private void SpawnA2a() => trackingYoyoIndex = SpawnGuardianYoyo(Vector2.Zero, YoyoGuardianBalance.GuardianYoyoDamage, Main.rand.Next(600, 1201), 0f, mode: 0);

        private void SpawnAB2a()
        {
            trackingYoyoIndex = SpawnGuardianYoyo(Vector2.Zero, YoyoGuardianBalance.GuardianYoyoDamage, Main.rand.Next(600, 1201), 0f, mode: 0);
            SpawnGuardianYoyo(Vector2.Zero, YoyoGuardianBalance.GuardianYoyoDamage, Main.rand.Next(600, 1201), 0f, mode: 2);
        }

        private void SpawnA2b()
        {
            Vector2 aimDir = (Main.player[NPC.target].Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            SpawnGuardianYoyo(aimDir * 8f, YoyoGuardianBalance.GuardianYoyoDamage, 180, -50f, mode: 1);
            SpawnGuardianYoyo(aimDir * 8f, YoyoGuardianBalance.GuardianYoyoDamage, 180, 0f, mode: 1);
            SpawnGuardianYoyo(aimDir * 8f, YoyoGuardianBalance.GuardianYoyoDamage, 180, 50f, mode: 1);
        }

        private void SpawnAB2b()
        {
            Vector2 aimDir = (Main.player[NPC.target].Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            SpawnGuardianYoyo(aimDir * 8f, YoyoGuardianBalance.GuardianYoyoDamage, 180, 0f, mode: 1);
        }

        private void SpawnA3a()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            FireA3Row(true, staggered: false, horizontalMirror: false);
            FireA3Row(false, staggered: false, horizontalMirror: false);
        }

        private void SpawnAB3a()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            FireA3Row(true, staggered: true, horizontalMirror: false);
            FireA3Row(false, staggered: true, horizontalMirror: false);
        }

        private void FireA3Row(bool above, bool staggered, bool horizontalMirror)
        {
            float rowY = NPC.Center.Y + (above ? -A3RowDistance : A3RowDistance);
            float rowOffsetX = above ? 25f : -25f;
            float startX = NPC.Center.X + rowOffsetX - (20f * A3RowSpacing / 2f) + A3RowSpacing / 2f;
            Vector2 vel = new Vector2(0f, above ? A3RowSpeed : -A3RowSpeed);
            int mid = 10;

            for (int i = 0; i < 20; i++)
            {
                Vector2 spawnPos = new Vector2(startX + i * A3RowSpacing, rowY);
                if (!staggered)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel,
                        ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
                }
                else
                {
                    int outward = Math.Abs(i - mid);
                    QueueRowSpawn(outward * 6, spawnPos, vel);
                }
            }

            if (horizontalMirror)
            {
                float colX = NPC.Center.X + (above ? -A3RowDistance : A3RowDistance);
                Vector2 hvel = new Vector2(above ? A3RowSpeed : -A3RowSpeed, 0f);
                for (int i = 0; i < 20; i++)
                {
                    Vector2 spawnPos = new Vector2(colX, NPC.Center.Y - (20f * A3RowSpacing / 2f) + i * A3RowSpacing);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, hvel,
                        ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
                }
            }
        }

        private void SpawnA3b()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 origin = GetYoyoOrGuardianCenter();
            for (int i = 0; i < 5; i++)
                SpawnStar(origin, 60f);
        }

        private void SpawnAB3b()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 origin = GetYoyoOrGuardianCenter();
            for (int i = 0; i < 5; i++)
            {
                float spreadDeg = Main.rand.NextFloat(-15f, 15f);
                Vector2 dir = (-Vector2.UnitY).RotatedBy(MathHelper.ToRadians(spreadDeg));
                SpawnDirectedStar(origin, dir);
            }
        }

        // Transitions

        private void SpawnShield()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < 3; i++)
                NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                    ModContent.NPCType<GuardianOrbitShield>(), 0, NPC.whoAmI, 0f, i * (MathHelper.TwoPi / 3f));
        }

        private void SpawnOutwardDust(bool purple = true)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 10f);
            Dust dust = Dust.NewDustPerfect(NPC.Center, purple ? DustID.PurpleTorch : DustID.Torch, vel, 0, Color.White, 1.4f);
            dust.noGravity = true;
        }

        // Fix #6/#7: an extra layer of bright, inward-converging sparks over the existing
        // outward dust + helix, so the transition buildups read with more energy.
        private void SpawnTransitionSpark(int radius)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = NPC.Center + angle.ToRotationVector2() * radius;
            Vector2 vel = (NPC.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
            Dust dust = Dust.NewDustPerfect(pos, DustID.PurpleTorch, vel, 0, new Color(255, 230, 255), 1.3f);
            dust.noGravity = true;
        }

        private void SpawnTransitionRingPulse(int radius)
        {
            for (int i = 0; i < 40; i++)
            {
                float angle = MathHelper.TwoPi / 40f * i;
                Vector2 pos = NPC.Center + angle.ToRotationVector2() * radius;
                Dust dust = Dust.NewDustPerfect(pos, DustID.PurpleTorch, Vector2.Zero, 0, Color.White, 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 0.6f;
            }
        }

        private void SpawnTransitionHelix(int ticks, float baseRadius)
        {
            float radius = MathHelper.Clamp(baseRadius - ticks * 0.6f, 30f, baseRadius);
            float angle = ticks * 0.12f;
            Vector2 armA = NPC.Center + angle.ToRotationVector2() * radius;
            Vector2 armB = NPC.Center + (angle + MathHelper.Pi).ToRotationVector2() * radius;

            Dust a = Dust.NewDustPerfect(armA, DustID.PurpleTorch, Vector2.Zero, 0, Color.White, 1.6f);
            a.noGravity = true;
            Dust b = Dust.NewDustPerfect(armB, DustID.PurpleTorch, Vector2.Zero, 0, new Color(255, 200, 255), 1.6f);
            b.noGravity = true;

            LightingHelper.SafeAddLight(NPC.Center, 0.6f, 0.2f, 0.95f);
        }

        private void SpawnTransitionRelease()
        {
            for (int i = 0; i < 60; i++)
            {
                float angle = MathHelper.TwoPi / 60f * i;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 16f);
                Dust dust = Dust.NewDustPerfect(NPC.Center, DustID.PurpleTorch, vel, 0, Color.White, 2f);
                dust.noGravity = true;
            }
            LightingHelper.SafeAddLight(NPC.Center, 1f, 0.6f, 1f); // fix #4: brighter flash on release
        }

        private void RunTransition1()
        {
            NPC.velocity *= 0.9f;
            transitionTicks++;
            for (int i = 0; i < 4; i++) SpawnOutwardDust();
            SpawnTransitionHelix(transitionTicks, 220f);
            for (int i = 0; i < 2; i++) SpawnTransitionSpark(200 + transitionTicks); // fix #6

            if (transitionTicks == 1)
            {
                ShowSpeechLine("Enough!", InUnderworld ? new Color(255, 70, 50) : new Color(190, 100, 255), forceImmediate: true);
                CameraFocusNPCIndex = NPC.whoAmI;
            }
            if (transitionTicks == 25) CameraFocusNPCIndex = -1;

            // fix #4: pulses quicken as the transition builds toward its release.
            int pulseInterval = transitionTicks > 90 ? 10 : 30;
            if (transitionTicks % pulseInterval == 0)
                SpawnTransitionRingPulse(60 + transitionTicks);

            if (transitionTicks == 110) SpawnTransitionRelease();

            if (transitionTicks >= 120)
            {
                phase = YoyoGuardianPhase.Phase2;
                transitionTicks = 0;
                ResetAttackState();
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }
        }

        private void RunTransition2()
        {
            NPC.velocity *= 0.9f;
            transitionTicks++;
            for (int i = 0; i < 10; i++) SpawnOutwardDust();
            SpawnTransitionHelix(transitionTicks, 320f);
            for (int i = 0; i < 3; i++) SpawnTransitionSpark(160 + transitionTicks); // fix #6

            if (transitionTicks == 1) { DespawnGuardianSummons(); CameraFocusNPCIndex = NPC.whoAmI; }

            int pulseInterval = transitionTicks > 90 ? 10 : 20; // fix #4
            if (transitionTicks % pulseInterval == 0)
                SpawnTransitionRingPulse(40 + transitionTicks * 2);

            if (transitionTicks == 110) SpawnTransitionRelease();

            if (transitionTicks >= 120)
            {
                CameraFocusNPCIndex = -1;
                phase = YoyoGuardianPhase.Desperation;
                transitionTicks = 0;
                ResetAttackState();
                desperationTimer = 0;
                desperationClonesSpawned = false;
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }
        }

        private void ResetAttackState()
        {
            slotIndex = 0; slotTimer = 0; subTimer = 0;
            a3cDelays = null; a3cFired = null;
            activeA4Lines.Clear(); a4FireCount = 0;
            a5Active = false; a5Timer = 0;
            abLineAngles = null; abLineTimer = 0;
        }

        // Phase 2

        private void RunPhase2()
        {
            int duration = ScaleTime(Phase2SlotDuration);
            slotTimer++;
            bool variant = phase2Variant[slotIndex];

            if (slotTimer == 1)
            {
                switch (slotIndex)
                {
                    case 2: if (variant) SpawnAB2c(); else SpawnA2c(); break;
                    case 4: StartA3c(variant); break;
                    case 6: a4FireCount = 0; break;
                    case 7: StartA5(variant); break;
                    case 8: if (!a6Done) StartA6(); break;
                }
            }

            if (slotIndex == 8 && a6Done && slotTimer > 1) slotTimer = duration + 1;

            if (slotTimer <= duration)
            {
                switch (slotIndex)
                {
                    case 0:
                        if (variant) UpdateRepeatingAttack(90, 10, StartTelegraphedBearings);
                        else UpdateRepeatingAttack(90, 10, SpawnA1a);
                        break;

                    case 1:
                        if (variant) UpdateRepeatingAttack(120, 3, SpawnAB1b);
                        else if (slotTimer == 1) SpawnA1b();
                        UpdateRepeatingAttack(150, 2, SpawnPhase2FillerBearings);
                        break;

                    case 2: break;

                    case 3:
                        if (variant) UpdateRepeatingAttack(90, 10, SpawnAB2d);
                        else UpdateRepeatingAttack(90, 10, SpawnA2d);
                        break;

                    case 4:
                        if (slotTimer == 1 || (slotTimer - 1) % ScaleTime(150) == 0)
                        {
                            int wave = (slotTimer - 1) / ScaleTime(150) + 1;
                            if (wave <= 6) StartA3c(variant);
                        }
                        UpdateA3c();
                        break;

                    case 5:
                        if (slotTimer == 1 || slotTimer == ScaleTime(450) + 1)
                        {
                            if (variant) SpawnAB3d(); else SpawnA3d();
                        }
                        UpdateRepeatingAttack(180, 2, StartTelegraphedBearings);
                        break;

                    case 6:
                        if (variant) UpdateRepeatingAttack(A4Interval, A4MaxFires, StartAB4);
                        else UpdateRepeatingAttack(A4Interval, A4MaxFires, StartA4);
                        UpdateA4();
                        break;

                    case 7:
                        if (a5Active) { if (variant) RunAB5(); else RunA5(); }
                        else slotTimer = duration;
                        break;

                    case 8: break;
                }
            }
            else
            {
                FinishPhase2Slot();
            }
        }

        private void FinishPhase2Slot()
        {
            if (slotIndex == 4) { a3cDelays = null; a3cFired = null; }
            if (slotIndex == 6) { activeA4Lines.Clear(); a4FireCount = 0; }
            if (slotIndex == 7) { a5Active = false; a5Timer = 0; }

            phase2Variant[slotIndex] = !phase2Variant[slotIndex];
            slotIndex = (slotIndex + 1) % Phase2SlotCount;
            slotTimer = 0;
        }

        private void SpawnA1b()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 origin = GetYoyoOrGuardianCenter();
            for (int i = 0; i < 3; i++) SpawnStar(origin, 60f);
        }

        private void SpawnAB1b()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 origin = GetYoyoOrGuardianCenter();
            for (int i = 0; i < 3; i++)
            {
                float spreadDeg = Main.rand.NextFloat(-15f, 15f);
                Vector2 dir = (-Vector2.UnitY).RotatedBy(MathHelper.ToRadians(spreadDeg));
                SpawnDirectedStar(origin, dir);
            }
        }

        private void SpawnPhase2FillerBearings()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 aimDir = (Main.player[NPC.target].Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            for (int i = -1; i <= 1; i++)
            {
                Vector2 vel = aimDir.RotatedBy(MathHelper.ToRadians(15f * i)) * 9f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                    ModContent.ProjectileType<Content.Projectiles.LooseStoneBearingsYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
            }
        }

        private void SpawnA2c() => trackingYoyoIndex = SpawnGuardianYoyo(Vector2.Zero, YoyoGuardianBalance.GuardianYoyoDamage, Main.rand.Next(1800, 2401), 0f, mode: 0);

        private void SpawnAB2c()
        {
            trackingYoyoIndex = SpawnGuardianYoyo(Vector2.Zero, YoyoGuardianBalance.GuardianYoyoDamage, Main.rand.Next(1800, 2401), 0f, mode: 0);
            SpawnGuardianYoyo(Vector2.Zero, YoyoGuardianBalance.GuardianYoyoDamage, Main.rand.Next(1800, 2401), 0f, mode: 2);
        }

        private void SpawnA2d()
        {
            Vector2 aimDir = (Main.player[NPC.target].Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            float[] offsets = { -100f, -50f, 0f, 50f, 100f };
            foreach (float offset in offsets) SpawnGuardianYoyo(aimDir * 8f, YoyoGuardianBalance.GuardianYoyoDamage, 180, offset, mode: 1);
        }

        private void SpawnAB2d()
        {
            Vector2 aimDir = (Main.player[NPC.target].Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            SpawnGuardianYoyo(aimDir * 8f, YoyoGuardianBalance.GuardianYoyoDamage, 180, 0f, mode: 1);
        }

        private void StartA3c(bool variant)
        {
            int totalRows = variant ? 4 : 2;
            a3cDelays = new int[20 * totalRows];
            a3cFired = new bool[20 * totalRows];
            subTimer = 0;
            for (int i = 0; i < a3cDelays.Length; i++)
                a3cDelays[i] = variant ? (i % 20) * 6 : (Main.rand.NextBool() ? 0 : Main.rand.Next(0, 61));
        }

        private void UpdateA3c()
        {
            if (a3cDelays == null) return;

            bool horizontal = a3cDelays.Length > 40;
            for (int i = 0; i < 20; i++)
            {
                if (!a3cFired[i] && subTimer >= a3cDelays[i]) { FireSingleA3(i, above: true, horizontal: false); a3cFired[i] = true; }
                if (!a3cFired[i + 20] && subTimer >= a3cDelays[i + 20]) { FireSingleA3(i, above: false, horizontal: false); a3cFired[i + 20] = true; }

                if (horizontal)
                {
                    if (!a3cFired[i + 40] && subTimer >= a3cDelays[i + 40]) { FireSingleA3(i, above: true, horizontal: true); a3cFired[i + 40] = true; }
                    if (!a3cFired[i + 60] && subTimer >= a3cDelays[i + 60]) { FireSingleA3(i, above: false, horizontal: true); a3cFired[i + 60] = true; }
                }
            }

            subTimer++;
            if (subTimer >= 60) { a3cDelays = null; a3cFired = null; subTimer = 0; }
        }

        private void FireSingleA3(int index, bool above, bool horizontal)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            if (!horizontal)
            {
                float rowY = NPC.Center.Y + (above ? -A3RowDistance : A3RowDistance);
                float rowOffsetX = above ? 25f : -25f;
                float startX = NPC.Center.X + rowOffsetX - (20f * A3RowSpacing / 2f) + A3RowSpacing / 2f;
                Vector2 spawnPos = new Vector2(startX + index * A3RowSpacing, rowY);
                Vector2 vel = new Vector2(0f, above ? A3RowSpeed : -A3RowSpeed);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
            }
            else
            {
                float colX = NPC.Center.X + (above ? -A3RowDistance : A3RowDistance);
                float startY = NPC.Center.Y - (20f * A3RowSpacing / 2f) + A3RowSpacing / 2f;
                Vector2 spawnPos = new Vector2(colX, startY + index * A3RowSpacing);
                Vector2 vel = new Vector2(above ? A3RowSpeed : -A3RowSpeed, 0f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
            }
        }

        private void SpawnA3d()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 origin = GetYoyoOrGuardianCenter();
            for (int i = 0; i < 10; i++) SpawnStar(origin, 60f);
        }

        private void SpawnAB3d()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            Vector2 origin = GetYoyoOrGuardianCenter();
            for (int i = 0; i < 5; i++)
            {
                SpawnDirectedStar(origin, -Vector2.UnitY);
                SpawnDirectedStar(origin, Vector2.UnitY);
            }
        }

        // A4 / AB4

        private void StartA4()
        {
            a4FireCount++;
            int trackIdx = GetTrackingYoyoIndex();
            activeA4Lines.Add(new A4Line
            {
                Angle = Main.rand.NextFloat(MathHelper.TwoPi),
                Center = trackIdx != -1 ? Main.projectile[trackIdx].Center : NPC.Center,
                Timer = 0,
                YoyoIndex = trackIdx,
            });
        }

        // Fix #2 (original pass): the old AB4 spawned 10 lines that were all PARALLEL to one
        // another (offset sideways from a shared base angle), which reads as a solid "wall" —
        // every other AB variant in this fight (AB1's ring, AB3b/AB3d's star cones, etc.) is
        // instead several copies of the base attack fanning OUT from one point, and AB4 wasn't
        // doing that at all, which is what actually made it feel broken/off compared to its
        // sibling attacks. Rewritten as a genuine 5-line fan, aimed at the player and spread
        // across it, spawned from the SAME origin (and tracking the yoyo/Guardian exactly like
        // plain A4 does, which the old version also never did).
        private static readonly float[] AB4FanOffsetsDeg = { -40f, -20f, 0f, 20f, 40f };

        private void StartAB4()
        {
            a4FireCount++;
            int trackIdx = GetTrackingYoyoIndex();
            Vector2 origin = trackIdx != -1 ? Main.projectile[trackIdx].Center : NPC.Center;

            Vector2 toTarget = Main.player[NPC.target].Center - origin;
            float aimAngle = toTarget.SafeNormalize(Vector2.UnitY).ToRotation();

            foreach (float deg in AB4FanOffsetsDeg)
            {
                activeA4Lines.Add(new A4Line
                {
                    Angle = aimAngle + MathHelper.ToRadians(deg),
                    Center = origin,
                    Timer = 0,
                    YoyoIndex = trackIdx,
                });
            }
        }

        private void UpdateA4()
        {
            if (activeA4Lines.Count == 0) return;

            float half = A4LineLength / 2f;

            for (int li = activeA4Lines.Count - 1; li >= 0; li--)
            {
                A4Line line = activeA4Lines[li];

                if (line.YoyoIndex != -1 && (line.YoyoIndex < 0 || line.YoyoIndex >= Main.maxProjectiles || !Main.projectile[line.YoyoIndex].active))
                {
                    line.YoyoIndex = -1;
                    if (line.Timer < A4DustTime) line.Center = NPC.Center;
                }
                else if (line.YoyoIndex != -1)
                {
                    // Fix #2: a tracking AB4 line now actually follows the yoyo/Guardian each
                    // tick while it's still telegraphing, same as plain A4 already did.
                    line.Center = Main.projectile[line.YoyoIndex].Center;
                }

                line.Timer++;
                Vector2 dir = line.Angle.ToRotationVector2();
                Vector2 trueStart = line.Center - dir * half;
                Vector2 trueEnd = line.Center + dir * half;

                if (line.Timer <= A4DustTime)
                {
                    for (int d = 0; d <= A4LineLength; d += 10)
                    {
                        Vector2 point = trueStart + dir * d;
                        Dust dust = Dust.NewDustPerfect(point, DustID.PurpleTorch, Vector2.Zero, 0, Color.White, 1.7f);
                        dust.noGravity = true;
                        LightingHelper.SafeAddLight(point, 0.5f, 0.15f, 0.85f);
                    }
                }

                if (line.Timer == A4SpawnTime)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float angle1 = line.Angle + MathHelper.ToRadians(90f);
                        float angle2 = line.Angle + MathHelper.ToRadians(270f);
                        int steps = A4LineLength / A4OffsetStep;
                        for (int offsetStep = 0; offsetStep <= steps; offsetStep++)
                        {
                            Vector2 spawnPos = trueStart + dir * (offsetStep * A4OffsetStep);
                            if (offsetStep == steps) spawnPos = trueEnd;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, angle1.ToRotationVector2() * 9f, ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, angle2.ToRotationVector2() * 9f, ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
                        }
                    }
                    activeA4Lines.RemoveAt(li);
                }
            }
        }

        // A5 / AB5

        private void StartA5(bool variant)
        {
            a5Active = true;
            a5Timer = 0;
            a5Angle = MathHelper.PiOver2;
            if (variant && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 origin = NPC.Center + Main.rand.NextVector2Unit() * 2000f;
                for (int i = 0; i < 5; i++)
                {
                    float orbitStart = MathHelper.TwoPi / 5f * i;
                    int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)origin.X, (int)origin.Y, ModContent.NPCType<StarGuardianNPC>());
                    if (idx < 0 || idx >= Main.maxNPCs) continue;
                    Main.npc[idx].ai[0] = NPC.target;
                    Main.npc[idx].ai[2] = 0f;
                    Main.npc[idx].velocity = orbitStart.ToRotationVector2() * 22f;
                    Main.npc[idx].netUpdate = true;
                }
            }
        }

        private void RunAB5()
        {
            NPC.velocity *= 0.7f;
            a5Timer++;
            if (a5Timer >= ScaleTime(900)) { a5Active = false; a5Timer = 0; }
        }

        private void RunA5()
        {
            NPC.velocity *= 0.7f;
            if (NPC.velocity.Length() < 0.2f) NPC.velocity = Vector2.Zero;
            a5Timer++;

            int interval = Math.Max(1, (int)(3 * AttackFreqMult));
            int starInterval = Math.Max(1, (int)(60 * AttackFreqMult));
            int maxDuration = ScaleTime(900);

            if (a5Timer <= maxDuration)
            {
                if (a5Timer % interval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 spawnPos = NPC.Center + a5Angle.ToRotationVector2() * 1000f;
                    Vector2 vel = (NPC.Center - spawnPos).SafeNormalize(Vector2.UnitY) * 10f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
                    a5Angle += MathHelper.ToRadians(4f);
                }
                if (a5Timer % starInterval == 0) SpawnStar(NPC.Center, 6f);
            }

            if (a5Timer >= maxDuration) { a5Active = false; a5Timer = 0; }
        }

        // A6 clones (UNCHANGED)

        private void StartA6()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) SpawnA6Clones();
            a6Done = true;
        }

        private void SpawnA6Clones()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector2 randomSpot = NPC.Center + Main.rand.NextVector2Circular(900f, 900f);

                int cloneIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)randomSpot.X, (int)randomSpot.Y, ModContent.NPCType<YoyoGuardian>());
                if (cloneIndex < 0 || cloneIndex >= Main.maxNPCs) continue;

                var tag = Main.npc[cloneIndex].GetGlobalNPC<YoyoGuardianCloneTag>();
                tag.isA6Clone = true;
                tag.masterIndex = NPC.whoAmI;
                tag.accelLerp = Main.rand.NextFloat(0.025f, 0.09f);
                tag.speedMultiplier = Main.rand.NextFloat(0.85f, 1.25f);

                Main.npc[cloneIndex].boss = false;
                Main.npc[cloneIndex].dontTakeDamage = true;
                Main.npc[cloneIndex].damage = 0;
                Main.npc[cloneIndex].alpha = 64;
            }
        }

        private void RunA6Clone(YoyoGuardianCloneTag tag, Player target)
        {
            NPC.boss = false;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            NPC.alpha = 64;

            int masterIndex = tag.masterIndex;
            if (masterIndex < 0 || masterIndex >= Main.maxNPCs || !Main.npc[masterIndex].active) { NPC.active = false; return; }

            OrbitFollowMovement(target, tag.speedMultiplier, tag.accelLerp);
            NPC.rotation += MathHelper.Lerp(0.02f, 0.35f, MathHelper.Clamp(NPC.velocity.Length() / 10f, 0f, 1f));

            LightingHelper.SafeAddLight(NPC.Center, 0.5f, 0.15f, 0.85f);
        }

        // Desperation

        private void RunDesperation(Player target)
        {
            NPC.dontTakeDamage = true;
            NPC.damage = (int)(YoyoGuardianBalance.DesperationDashDamage * Cfg.DamageMultiplier);
            desperationTimer++;

            bool expertPlus = Main.expertMode; // addition #1: DesperationPre/Phase3 content is expert+ only

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (desperationTimer % 6 == 0)
                {
                    int dmgTick = (int)(NPC.lifeMax * 0.00025f);

                    if (expertPlus)
                    {
                        // Addition #1: stop EXACTLY at 1 HP instead of letting the drain kill it.
                        if (NPC.life - dmgTick <= 1)
                        {
                            NPC.life = 1;
                            NPC.netUpdate = true;
                            BeginDesperationPre();
                            return;
                        }
                        NPC.life -= dmgTick;
                    }
                    else
                    {
                        NPC.life -= dmgTick;
                        if (NPC.life <= 0) { NPC.life = 0; NPC.HitEffect(); NPC.checkDead(); return; }
                    }

                    NPC.netUpdate = true;
                }
            }

            if (desperationTimer >= 1200)
            {
                if (expertPlus)
                {
                    // Safety net so expert+ always reaches the 1 HP transition even if the tick
                    // drain above never quite lined up.
                    NPC.life = 1;
                    NPC.netUpdate = true;
                    BeginDesperationPre();
                    return;
                }
                NPC.life = 0; NPC.HitEffect(); NPC.checkDead(); return;
            }

            if (!desperationClonesSpawned)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 offset = Main.rand.NextVector2CircularEdge(300f, 300f);
                        int cloneIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)(target.Center.X + offset.X), (int)(target.Center.Y + offset.Y), ModContent.NPCType<YoyoGuardian>());
                        if (cloneIndex < 0 || cloneIndex >= Main.maxNPCs) continue;

                        var tag = Main.npc[cloneIndex].GetGlobalNPC<YoyoGuardianCloneTag>();
                        tag.isDesperationClone = true;
                        tag.masterIndex = NPC.whoAmI;
                        tag.offset = offset;

                        Main.npc[cloneIndex].boss = false;
                        Main.npc[cloneIndex].dontTakeDamage = true;
                        Main.npc[cloneIndex].damage = (int)(YoyoGuardianBalance.DesperationDashDamage * 0.6f * Cfg.DamageMultiplier);
                    }
                }
                desperationClonesSpawned = true;
            }

            RunDesperationDash(target, false, desperationTimer);
        }

        // Fix #3: a leftover dash clone must never keep dashing once the master has left the
        // classic Desperation phase (DesperationPre, TransitionToPhase3, Phase3, its death
        // sequence, or the master is simply gone). Checked every tick, not just at the moment
        // of the transition, so it's also safe against any ordering/sync edge case.
        private void RunDesperationClone(YoyoGuardianCloneTag tag, Player target)
        {
            int masterIndex = tag.masterIndex;
            if (masterIndex < 0 || masterIndex >= Main.maxNPCs || !Main.npc[masterIndex].active ||
                Main.npc[masterIndex].ModNPC is not YoyoGuardian master || master.Phase != YoyoGuardianPhase.Desperation)
            {
                NPC.active = false;
                return;
            }

            NPC.boss = false;
            NPC.dontTakeDamage = true;
            NPC.damage = (int)(YoyoGuardianBalance.DesperationDashDamage * 0.6f * Cfg.DamageMultiplier);
            LightingHelper.SafeAddLight(NPC.Center, 0.5f, 0.15f, 0.85f);
            NPC.ai[0] += 1f;
            RunDesperationDash(target, true, (int)NPC.ai[0]);
        }

        private void RunDesperationDash(Player target, bool isClone, int timer)
        {
            int cyclePos = timer % 60;
            if (cyclePos < 15) NPC.alpha = (int)MathHelper.Lerp(255, 0, cyclePos / 15f);
            else if (cyclePos == 15)
            {
                Vector2 dashDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                NPC.velocity = dashDir * (isClone ? 18f : 22f) * (InUnderworld ? 2f : 1f);
            }
            else if (cyclePos > 45) { NPC.alpha = (int)MathHelper.Lerp(0, 255, (cyclePos - 45) / 15f); NPC.velocity *= 0.9f; }

            if (cyclePos == 59)
            {
                Vector2 offset = Main.rand.NextVector2CircularEdge(300f, 300f);
                NPC.Center = target.Center + offset;
                NPC.velocity = Vector2.Zero;
            }
        }

        private void DespawnGuardianSummons()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active) continue;
                if (p.type == ModContent.ProjectileType<Content.Projectiles.CounterweightYG>() ||
                    p.type == ModContent.ProjectileType<Content.Projectiles.StoneGuardianYoyo>())
                {
                    // Note: the GuardianArenaBox is deliberately NOT cleared
                    // here. Killing it mid-fight forced a kill/respawn flicker
                    // every Transition2 (and could desync it on laggy hosts):
                    // it stays up through transitions, drops only for the
                    // Desperation dash (per-tick kill above) and on death
                    // (KillArenaBox in CheckDead/OnKill).
                    p.netUpdate = true;
                    p.Kill();
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active) continue;

                if (n.type == ModContent.NPCType<GuardianOrbitShield>() && (int)n.ai[0] == NPC.whoAmI)
                { n.active = false; n.netUpdate = true; continue; }

                if (n.type == ModContent.NPCType<StarGuardianNPC>())
                { n.active = false; n.netUpdate = true; continue; }

                if ((n.type == ModContent.NPCType<MagicDestroyerHead>() ||
                     n.type == ModContent.NPCType<MagicDestroyerBody>() ||
                     n.type == ModContent.NPCType<MagicDestroyerTail>()) &&
                    n.ModNPC is MagicDestroyerSegment seg && seg.GuardianOwner == NPC.whoAmI)
                { n.active = false; n.netUpdate = true; continue; }
            }
        }

        // Fix #3: belt-and-suspenders — despawns any leftover Desperation dash clones the
        // instant the master itself leaves Desperation, instead of waiting up to one extra
        // tick for each clone's own check (above) to catch it.
        private void DespawnDesperationClones()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.type != NPC.type) continue;

                var tag = n.GetGlobalNPC<YoyoGuardianCloneTag>();
                if (tag.isDesperationClone && tag.masterIndex == NPC.whoAmI)
                {
                    n.active = false;
                    n.netUpdate = true;
                }
            }
        }

        // Addition #1: DesperationPre (invincible at 1 HP, cracked-shell sprite, random spray)

        private void BeginDesperationPre()
        {
            phase = YoyoGuardianPhase.DesperationPre;
            desperationPreTimer = 0;
            NPC.dontTakeDamage = true;
            DespawnDesperationClones(); // fix #3: dash never survives past Desperation
            ShowSpeechLine("The outer shell is cracked!", new Color(255, 220, 120), forceImmediate: true, scale: 0.5f); // fix #2: 2x smaller
            if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
        }

        private void RunDesperationPre()
        {
            NPC.dontTakeDamage = true;
            NPC.life = 1;
            NPC.velocity *= 0.9f;
            desperationPreTimer++;

            if (Main.netMode != NetmodeID.MultiplayerClient && desperationPreTimer % 6 == 0)
            {
                Vector2 dir = Main.rand.NextVector2Unit();
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 9f,
                    ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255);
            }

            for (int i = 0; i < 3; i++) SpawnOutwardDust();

            if (desperationPreTimer >= DesperationPreDuration)
            {
                phase = YoyoGuardianPhase.TransitionToPhase3;
                transitionToP3Timer = 0;
                p3SpeechIndex = -1;
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
            }
        }

        // Addition #2 / #3: TransitionToPhase3 (37s YGTP3 track, 7 lines 5s apart)

        private void RunTransitionToPhase3()
        {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.9f;
            transitionToP3Timer++;

            if (p3SpeechIndex + 1 < P3LineTicks.Length && transitionToP3Timer >= P3LineTicks[p3SpeechIndex + 1])
            {
                p3SpeechIndex++;
                ShowSpeechLine(P3TransitionLines[p3SpeechIndex], new Color(190, 100, 255), forceImmediate: true);
            }

            for (int i = 0; i < 6; i++) SpawnOutwardDust();
            SpawnTransitionHelix(transitionToP3Timer, 400f);
            for (int i = 0; i < 2; i++) SpawnTransitionSpark(260 + (transitionToP3Timer % 400)); // fix #6
            if (transitionToP3Timer % 40 == 0) SpawnTransitionRingPulse(80 + transitionToP3Timer / 2);

            // Addition #3: Phase 3 begins right after the last line ("We will fight!") lands —
            // ~1s after it's shown, so it reads as "immediate" without cutting the line off.
            // The YGTP3 track keeps playing its remaining ~6s tail into Phase 3.
            if (p3SpeechIndex == P3TransitionLines.Length - 1 && transitionToP3Timer >= P3LineTicks[^1] + 60)
            {
                SpawnTransitionRelease();
                EnterPhase3();
            }
        }

        // Addition #5-#7: Phase 3

        private void EnterPhase3()
        {
            phase = YoyoGuardianPhase.Phase3;
            CameraFocusNPCIndex = -1;

            // Fix #7/#9: fixed per-difficulty base HP, scaled by both the player-count balance
            // factor recorded in ApplyDifficultyAndPlayerScaling and the Standard/Hell
            // multiplier, same treatment as the fight's main HP pool.
            int baseHp = Main.masterMode ? YoyoGuardianBalance.Phase3MasterHPBase
                : (Main.expertMode ? YoyoGuardianBalance.Phase3ExpertHPBase : YoyoGuardianBalance.Phase3ClassicHPBase);
            int hp = (int)(baseHp * playerHPBalance * Cfg.HPMultiplier);
            NPC.lifeMax = hp;
            NPC.life = hp;
            NPC.dontTakeDamage = false;
            NPC.alpha = 0;

            ResetAttackState();
            p3SlotIndex = 0; p3SlotTimer = 0;
            p3B1ShotTimer = 0; p3B1StarTimer = 0;
            p3B2CycleTimer = 0;
            p3B3Timer = 0;
            p3Telegraphs.Clear();
            ap1YoyoIndices = null;

            if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
        }

        private void RunPhase3(Player target)
        {
            NPC.dontTakeDamage = false;
            MaintainAP1Orbiters(); // addition #7: permanent orbit attack

            // Fix #6: subtle ambient embers give Phase 3 a more "powered up" feel.
            if (Main.rand.NextBool(3))
            {
                Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(70f, 70f);
                Dust ember = Dust.NewDustPerfect(pos, DustID.PurpleTorch, new Vector2(0f, -1.5f), 0, Color.White, 1f);
                ember.noGravity = true;
            }

            p3SlotTimer++;
            switch (p3SlotIndex)
            {
                case 0: OrbitFollowMovement(target, 1.1f); RunPhase3B1(target); break;
                case 1: RunPhase3B2(target); break; // B2 controls its own movement
                case 2: OrbitFollowMovement(target, 1.1f); RunPhase3B3(target); break;
            }

            if (p3SlotTimer >= Phase3SlotDuration)
            {
                p3SlotTimer = 0;
                p3SlotIndex = (p3SlotIndex + 1) % 3;
                p3B1ShotTimer = 0; p3B1StarTimer = 0;
                p3B2CycleTimer = 0;
                NPC.alpha = 0; // fix #4 safety net: never leave the boss mid-fade if the slot changes
                p3B3Timer = 0;
            }
        }

        // B1: aimed Pointed YG + two 5-degree-offset copies every 1s, plus a star flung in a
        // random direction every 2s.
        private void RunPhase3B1(Player target)
        {
            p3B1ShotTimer++;
            if (p3B1ShotTimer == 1 || (p3B1ShotTimer - 1) % 60 == 0)
            {
                Vector2 aimDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                float baseAngle = aimDir.ToRotation();
                float[] offsets = { 0f, 5f, -5f };
                Vector2 origin = NPC.Center;

                foreach (float offsetDeg in offsets)
                {
                    float shotAngle = baseAngle + MathHelper.ToRadians(offsetDeg);
                    QueueP3Telegraph(origin, shotAngle, () =>
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, shotAngle.ToRotationVector2() * 11f,
                            ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255));
                }
            }

            p3B1StarTimer++;
            if (p3B1StarTimer >= 120)
            {
                p3B1StarTimer = 0;
                SpawnDirectedStar(NPC.Center, Main.rand.NextVector2Unit());
            }
        }

        // Fix #4: rewritten from a flat "0.5s stop -> instant snap -> 3s idle" sequence into a
        // proper telegraphed teleport: a growing warning ring appears at the actual destination
        // during the 0.5s pre-delay (so the attack reads BEFORE it happens instead of being a
        // silent stop), the Guardian smoothly fades out at the old spot and fades back in at
        // the new one instead of instant-snapping (no more "no smoothness"), and the explosion
        // fires exactly when it lands rather than being disconnected from the visual. Also
        // keeps firing a random-direction telegraphed Pointed YG every 1s throughout, unchanged.
        private void RunPhase3B2(Player target)
        {
            p3B2CycleTimer++;

            const int preDelayEnd = 30;                 // 0.5s: stopped, ring telegraphs the destination
            const int teleportOutEnd = preDelayEnd + 6;  // fade-out at the old spot
            const int teleportLandTick = teleportOutEnd + 1; // the actual teleport, mid-fade (invisible)
            const int teleportInEnd = teleportOutEnd + 6;    // fade back in at the new spot
            const int holdEnd = teleportInEnd + 180;         // 3s held at the destination
            // anything past holdEnd, up to the 300-tick (5s) cycle, is "rest" — resumes orbiting.

            if (p3B2CycleTimer == 1)
            {
                NPC.velocity = Vector2.Zero;
                float orbitDeg = Main.rand.NextFloat(MathHelper.TwoPi);
                p3B2DestinationPreview = target.Center + orbitDeg.ToRotationVector2() * 128f;
            }

            if (p3B2CycleTimer <= preDelayEnd)
            {
                NPC.velocity *= 0.8f;
                float ringProgress = p3B2CycleTimer / (float)preDelayEnd;
                int ringPoints = 20;
                float ringRadius = MathHelper.Lerp(6f, 46f, ringProgress);
                for (int i = 0; i < ringPoints; i++)
                {
                    float a = MathHelper.TwoPi / ringPoints * i;
                    Vector2 pos = p3B2DestinationPreview + a.ToRotationVector2() * ringRadius;
                    Dust dust = Dust.NewDustPerfect(pos, DustID.PurpleTorch, Vector2.Zero, 0, Color.White, 1f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.4f;
                }
                LightingHelper.SafeAddLight(p3B2DestinationPreview, 0.4f * ringProgress, 0.1f, 0.7f * ringProgress);
            }
            else if (p3B2CycleTimer <= teleportOutEnd)
            {
                float t = (p3B2CycleTimer - preDelayEnd) / 6f;
                NPC.alpha = (int)(255 * t);
                for (int i = 0; i < 3; i++) SpawnOutwardDust();
            }
            else if (p3B2CycleTimer == teleportLandTick)
            {
                p3B2Position = p3B2DestinationPreview;
                NPC.Center = p3B2Position;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
                DoB2Explosion(p3B2Position);
            }
            else if (p3B2CycleTimer <= teleportInEnd)
            {
                NPC.Center = p3B2Position;
                float t = (p3B2CycleTimer - teleportLandTick) / 6f;
                NPC.alpha = (int)(255 * (1f - t));
                for (int i = 0; i < 3; i++) SpawnOutwardDust();
            }
            else if (p3B2CycleTimer <= holdEnd)
            {
                NPC.alpha = 0;
                NPC.Center = p3B2Position;
                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(40f, 40f), DustID.PurpleTorch, Vector2.Zero, 0, Color.White, 1.5f);
                    dust.noGravity = true;
                }
            }
            else
            {
                OrbitFollowMovement(target, 1.1f); // resumes orbiting for the rest of the 5s window
            }

            if (p3B2CycleTimer == 1 || (p3B2CycleTimer - 1) % 60 == 0)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 origin = NPC.Center;
                QueueP3Telegraph(origin, angle, () =>
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, angle.ToRotationVector2() * 11f,
                        ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255));
            }

            if (p3B2CycleTimer >= 300)
            {
                p3B2CycleTimer = 0;
                NPC.alpha = 0; // safety: always end the cycle fully visible
            }
        }

        // Fix #4/#6: a proper multi-ring burst (three expanding rings) plus a bright flash,
        // instead of one flat single ring of dust.
        private void DoB2Explosion(Vector2 center)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int ring = 0; ring < 3; ring++)
            {
                float radius = 20f + ring * 18f;
                int points = 16 + ring * 6;
                for (int i = 0; i < points; i++)
                {
                    float angle = MathHelper.TwoPi / points * i;
                    Vector2 vel = angle.ToRotationVector2() * (5f + ring * 2f);
                    Dust dust = Dust.NewDustPerfect(center + angle.ToRotationVector2() * radius, DustID.PurpleTorch, vel, 0, Color.White, 1.6f - ring * 0.2f);
                    dust.noGravity = true;
                }
            }
            LightingHelper.SafeAddLight(center, 1f, 0.5f, 1f);

            int explosionDamage = Dmg(YoyoGuardianBalance.Phase3B2ExplosionDamage);
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                if (Vector2.Distance(p.Center, center) <= 134f)
                    p.Hurt(PlayerDeathReason.LegacyDefault(), explosionDamage, 0);
            }
        }

        // B3: Pointed YG from random positions/directions within the arena (at least 128px from
        // the player), with line indicators, every 0.1s.
        private void RunPhase3B3(Player target)
        {
            p3B3Timer++;
            if (p3B3Timer % 6 == 0)
            {
                Vector2 pos;
                int guard = 0;
                do { pos = NPC.Center + Main.rand.NextVector2Circular(1400f, 1400f); guard++; }
                while (Vector2.Distance(pos, target.Center) < 128f && guard < 10);

                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                QueueP3Telegraph(pos, angle, () =>
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, angle.ToRotationVector2() * 11f,
                        ModContent.ProjectileType<Content.Projectiles.PointedYG>(), Dmg(YoyoGuardianBalance.BaseProjectileDamage), 2f, 255));
            }
        }

        // AP1: 10 permanently orbiting Stone Guardian yoyos at 256px, spinning fast. Maintained
        // (not just spawned once) so it self-heals if a yoyo somehow gets removed.
        private void MaintainAP1Orbiters()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            if (ap1YoyoIndices == null)
            {
                ap1YoyoIndices = new int[10];
                for (int i = 0; i < 10; i++) ap1YoyoIndices[i] = -1;
            }

            for (int i = 0; i < 10; i++)
            {
                int idx = ap1YoyoIndices[i];
                bool alive = idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active &&
                             Main.projectile[idx].type == ModContent.ProjectileType<Content.Projectiles.StoneGuardianYoyo>();
                if (alive) continue;

                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<Content.Projectiles.StoneGuardianYoyo>(), Dmg(YoyoGuardianBalance.GuardianYoyoDamage), 2f, 255,
                    0f, NPC.whoAmI);

                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].ai[2] = 3f; // orbit mode
                    Main.projectile[p].ai[0] = MathHelper.TwoPi / 10f * i; // fixed orbit slot
                    Main.projectile[p].timeLeft = 36000;
                    Main.projectile[p].netUpdate = true;
                    ap1YoyoIndices[i] = p;
                }
            }
        }

        // Addition #8/#9: Phase 3 death sequence

        private void RunPhase3DeathSequence()
        {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.9f;
            phase3DeathTimer++;

            if (Main.netMode != NetmodeID.MultiplayerClient && phase3DeathTimer % 3 == 0)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 14f);
                    Dust dust = Dust.NewDustPerfect(NPC.Center, DustID.PurpleTorch, vel, 0, Color.White, Main.rand.NextFloat(1.2f, 2.2f));
                    dust.noGravity = true;
                }
            }
            LightingHelper.SafeAddLight(NPC.Center, 0.8f, 0.3f, 1f);

            if (phase3DeathTimer >= Phase3DeathDuration)
            {
                NPC.life = 0;
                NPC.dontTakeDamage = false;
                NPC.HitEffect();
                NPC.checkDead(); // re-enters CheckDead; phase is Phase3DeathSequence now, so this time it actually dies
            }
        }

        // Death / drops

        public override bool CheckDead()
        {
            // Addition #8: Phase 3 doesn't die on the spot — it explodes for 3s with the camera
            // locked on, then dies for real.
            if (phase == YoyoGuardianPhase.Phase3 && IsRealBoss)
            {
                phase = YoyoGuardianPhase.Phase3DeathSequence;
                phase3DeathTimer = 0;
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                CameraFocusNPCIndex = NPC.whoAmI;
                if (Main.netMode != NetmodeID.MultiplayerClient) NPC.netUpdate = true;
                return false;
            }

            CameraFocusNPCIndex = -1;
            KillArenaBox();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (!Main.npc[i].active) continue;

                if (Main.npc[i].type == ModContent.NPCType<GuardianOrbitShield>() && (int)Main.npc[i].ai[0] == NPC.whoAmI)
                { Main.npc[i].active = false; continue; }

                if (Main.npc[i].type != Type) continue;

                var tag = Main.npc[i].GetGlobalNPC<YoyoGuardianCloneTag>();
                if ((tag.isDesperationClone || tag.isA6Clone) && tag.masterIndex == NPC.whoAmI)
                    Main.npc[i].active = false;
            }

            return true;
        }

        public override void OnKill()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            YoyoGuardianWorld.downedYoyoGuardian = true; // for Boss Checklist support
            KillArenaBox();
            DespawnGuardianSummons(); // also mops up AP1's orbiters (they're plain StoneGuardianYoyo)

            if (phase == YoyoGuardianPhase.Phase3DeathSequence) // addition #9
            {
                ShowSpeechLine("You have proven yourself.", new Color(220, 200, 255), forceImmediate: true);
            }

            bool underworld = InUnderworld;
            TryDrop(underworld ? "EnrangedGuardianYoyo" : "GuardianYoyo", 0.5);
            TryDrop("ChargedCore", 0.5, underworld ? 5 : 1, underworld ? 25 : 20);
            TryDrop("GuardianEssence", underworld ? 0.3 : 0.25);
        }

        private void TryDrop(string itemName, double chance, int minStack = 1, int maxStack = 1)
        {
            if (Mod.TryFind<ModItem>(itemName, out var modItem) && Main.rand.NextDouble() < chance)
            {
                int stack = Main.rand.Next(minStack, maxStack + 1);
                Item.NewItem(NPC.GetSource_Loot(), NPC.Hitbox, modItem.Type, stack);
            }
        }
    }

    public class YoyoGuardianCloneTag : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public bool isDesperationClone;
        public bool isA6Clone;
        public int masterIndex = -1;
        public Vector2 offset;
        public float accelLerp = 0.05f;
        public float speedMultiplier = 1f;
    }
}