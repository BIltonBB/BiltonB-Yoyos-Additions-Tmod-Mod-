using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Content.Customization
{
	// Reads per-yoyo Tuning/Calibration/Mastery/Curse on spawn and runs them while deployed.
	// Each modifier is a real mechanic (rebounds, orbits, routes, patterns, trade-offs),
	// not just a stat line. Multiplayer: ids sync via SendExtraAI so remotes match the owner.
	public partial class YoyoCustomizationGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public int TuningId = -1;
		public int CalibrationId = -1;
		public int MasteryId = -1;
		public int CurseId = -1;

		// Shared trackers.
		private int deployTicks;
		private bool announced;
		private Vector2 lastVel;
		private Vector2 lastPos;
		private float travelDist;
		private int reboundCount;
		private int reboundCooldown;
		private float tension; // Snapback / Tension Break / Flywheel etc.
		private int tensionTimer;
		private int orbitNpc = -1;
		private int orbitNpcType;
		private int orbitTimer;
		private int anchorTimer;
		private bool anchored;
		private int anchorCooldown;
		private int forkCooldown;
		private int slipCooldown;
		private int slipTimer;
		private int needleTicks;
		private Vector2 needleDir;
		private int springFlip;
		private int pinballStage;
		private int afterimageCooldown;
		private readonly List<Vector2> trail = new();
		private float flywheel;
		private int livingCharges = 3;
		private int livingTimer;
		private readonly Dictionary<int, int> hitCooldown = new();
		private readonly Dictionary<int, int> returnMark = new();
		private int preyNpc = -1;
		private int preyWindow;
		private bool preyArmed;
		private readonly System.Collections.Generic.List<int> crossList = new();

		// Calibration state.
		private Vector2 lastHitDir;
		private bool hasLastHitDir;
		private int crosscutTimer;
		private int rhythmStage;
		private int rhythmTimer;
		private int rhythmWindow = 45;
		private float orbitAngleAccum;
		private Vector2 orbitLastDir;
		private bool orbitTracking;
		private Vector2 parallaxBase;
		private int parallaxTimer;
		private int missTimer = -1;
		private int ticksSinceHit;
		private float capacitor;
		private Vector2 capacitorStart;
		private bool capacitorCharging;
		private readonly List<Vector2> threadPts = new();
		private int deadbandTimer;
		private readonly List<float> diffCurves = new();
		private float redline;
		private bool redlineActive;
		private int redlineTimer;
		private Vector2 closedStart;
		private bool closedMarking;
		private readonly List<float> triangAngles = new();
		private float impulse;
		private int harmonicStage;
		private int harmonicTimer;
		private int escapeTimer;
		private float overpressure;
		private bool overActive;
		private int overTimer;
		private int faultTimer;

		// Mastery state.
		private int encoreStage;
		private int encoreTimer;
		private int lastWordNpc = -1;
		private int lastWordTimer;
		private readonly Dictionary<int, int> chainMarks = new();
		private int untouchedTicks;
		private int lockdownNpc = -1;
		private int lockdownTicks;
		private int misdirectMix;
		private int misdirectTimer;
		private bool misdirectMarked;
		private int misdirectNpc = -1;
		private int reversalTimer = -1;
		private int reversalNpc = -1;
		private int waltzRebound;
		private int waltzOrbit;
		private int waltzReturn;
		private int waltzSwap;
		private int waltzTimer;
		private int flow;
		private int farpointNpc = -1;
		private int farpointTimer;
		private int lastHitNpc = -1;
		private float lastHitAngle;
		private int patternStreak;

		// True mastery state.
		private int circuitEvents;
		private int circuitTimer;
		private int secondStage;
		private int secondTimer;
		private bool secondExpectLong;
		private readonly List<Vector2> ghostRecord = new();
		private int ghostCooldown;
		private int cathedralStage;
		private int cathedralTimer;
		private int confCombo;
		private int confLoop;
		private int confDir;
		private float confLastAng;
		private int confTimer;
		private readonly List<float> sovereignAngles = new();
		private float resonance;
		private int resonanceTimer;
		private readonly List<Vector2> continuumPts = new();
		private int continuumTimer;

		// Curse state.
		private int recoil;
		private float brittle;
		private bool brittleBroken;
		private int orbitReserve = 600;
		private int borrowedDebt;
		private int borrowedTimer;
		private int doubtTimer;
		private bool doubtHeavy;
		private int fractureAngle = -1;
		private int fractureSpam;
		private int pendulumOutward = 1;
		private int pendulumTimer;
		private int emptyWiden;
		private float debtBank;
		private int debtTimer;
		private int shearTimer;
		private bool shearState;
		private float bankruptcy;
		private int bankruptcyTimer;
		private float overpressureCurse;
		private bool overCurseActive;
		private int overCurseTimer;
		private int gyreTight;
		private int gyreNpc = -1;
		private int gyreNpcType;
		private int gyreTimer;
		private float crownReso;
		private int crownTimer;
		private int soulsPhase;
		private int soulsTimer;
		private float inheritSkew;
		private int inheritTimer;
		private int throneTimer;
		private bool throneHeld = true;
		private float contractBank;
		private int contractTimer;
		private bool contractPrimed;

		public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
		{
			return projectile.aiStyle == ProjAIStyleID.Yoyo;
		}

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			Item src = null;
			if (source is EntitySource_ItemUse_WithAmmo ammoUse)
				src = ammoUse.Item;
			else if (source is EntitySource_ItemUse itemUse)
				src = itemUse.Item;
			if (src == null || src.IsAir)
				return;
			if (!Content.NPCs.Town.YoyoTownShared.IsYoyoShape(src))
				return;
			if (!src.TryGetGlobalItem<YoyoCustomizationGlobalItem>(out var cust))
				return;
			TuningId = cust.TuningId;
			CalibrationId = cust.CalibrationId;
			MasteryId = cust.MasteryId;
			CurseId = cust.CurseId;
			announced = false;
			lastVel = projectile.velocity;
			lastPos = projectile.Center;
			needleDir = projectile.velocity.SafeNormalize(Vector2.UnitX);
			capacitorStart = projectile.Center;
			closedStart = projectile.Center;
			if (TuningId == 9) slipCooldown = 1200;
		}

		// tModLoader 1.4.4+ extra-AI sync (BitWriter form). Keeps remotes in sync.
		public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			binaryWriter.Write((short)TuningId);
			binaryWriter.Write((short)CalibrationId);
			binaryWriter.Write((short)MasteryId);
			binaryWriter.Write((short)CurseId);
		}

		public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
		{
			TuningId = binaryReader.ReadInt16();
			CalibrationId = binaryReader.ReadInt16();
			MasteryId = binaryReader.ReadInt16();
			CurseId = binaryReader.ReadInt16();
		}

		public override void AI(Projectile projectile)
		{
			if (TuningId < 0 && CalibrationId < 0 && MasteryId < 0 && CurseId < 0)
				return;
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active)
				return;
			bool isOwner = projectile.owner == Main.myPlayer;
			deployTicks++;
			AmbientFx(projectile, owner, isOwner);

			// Rebound detector: sharp direction flip at speed.
			if (reboundCooldown > 0) reboundCooldown--;
			Vector2 v = projectile.velocity;
			if (v.Length() > 3f && lastVel.Length() > 3f && reboundCooldown <= 0) {
				float ang = MathHelper.WrapAngle(v.ToRotation() - lastVel.ToRotation());
				if (System.Math.Abs(ang) > 1.9f) {
					reboundCount++;
					reboundCooldown = 12;
					OnRebound(projectile, owner, isOwner);
				}
			}
			lastVel = v;
			travelDist += v.Length();

			RunTuning(projectile, owner, isOwner);
			RunCalibration(projectile, owner, isOwner);
			RunMastery(projectile, owner, isOwner);
			RunCurses(projectile, owner, isOwner);

			// Tick down per-NPC follow-up cooldowns and return-vector marks.
			if (deployTicks % 5 == 0) {
				if (hitCooldown.Count > 0) {
					var drop = new List<int>();
					foreach (var kv in hitCooldown) {
						if (kv.Value - 5 <= 0) drop.Add(kv.Key);
						else hitCooldown[kv.Key] = kv.Value - 5;
					}
					foreach (int k in drop) hitCooldown.Remove(k);
				}
				if (returnMark.Count > 0) {
					var dropM = new List<int>();
					foreach (var kv in returnMark) {
						if (kv.Value - 5 <= 0) dropM.Add(kv.Key);
						else returnMark[kv.Key] = kv.Value - 5;
					}
					foreach (int k in dropM) returnMark.Remove(k);
				}
			}
			// Return Vector: a return leg passing through a struck foe re-hits it.
			if (TuningId == 5 && IsReturning(projectile, owner) && projectile.owner == Main.myPlayer) {
				foreach (var kv in returnMark) {
					int who = kv.Key;
					if (who < 0 || who >= Main.maxNPCs)
						continue;
					NPC n = Main.npc[who];
					if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5 || n.life <= 0)
						continue;
					if (Vector2.DistanceSquared(projectile.Center, n.Center) < 36f * 36f)
						FollowUpHit(projectile, n, 0.6f);
				}
			}
			// Crossfire Array: a true return re-hits remembered foes in reverse.
			if (TuningId == 106 && IsReturning(projectile, owner) && projectile.owner == Main.myPlayer && crossList.Count > 0) {
				var spent = new List<int>();
				for (int ci = crossList.Count - 1; ci >= 0; ci--) {
					int who = crossList[ci];
					if (who < 0 || who >= Main.maxNPCs) { spent.Add(who); continue; }
					NPC n = Main.npc[who];
					if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5 || n.life <= 0) { spent.Add(who); continue; }
					if (Vector2.DistanceSquared(projectile.Center, n.Center) < 70f * 70f) {
						FollowUpHit(projectile, n, 0.8f);
						spent.Add(who);
					}
				}
				foreach (int who in spent)
					crossList.Remove(who);
			}
			if (preyWindow > 0) preyWindow--;
			lastPos = projectile.Center;
		}

		private void OnRebound(Projectile projectile, Player owner, bool isOwner)
		{
			// Shared rebound hook: each family reacts.
			OnReboundTuning(projectile, owner, isOwner);
			OnReboundCalibration(projectile, owner, isOwner);
			OnReboundMastery(projectile, owner, isOwner);
			OnReboundCurse(projectile, owner, isOwner);
		}

		// Family-tinted ambient drift so active modifiers read at a glance.
		private void AmbientFx(Projectile projectile, Player owner, bool isOwner)
		{
			if (deployTicks % 24 != 0)
				return;
			int dust = -1;
			if (CurseId >= 0) dust = DustID.Blood;
			else if (MasteryId >= 100) dust = DustID.MagicMirror;
			else if (MasteryId >= 0) dust = DustID.GoldCoin;
			else if (CalibrationId >= 100) dust = DustID.Electric;
			else if (CalibrationId >= 0) dust = DustID.Grass;
			else if (TuningId >= 0) dust = DustID.Cloud;
			if (dust < 0)
				return;
			Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, dust, 0f, -0.6f, 120, default, 0.8f);
			d.noGravity = true;
		}

		private static bool IsReturning(Projectile p, Player owner)
		{
			Vector2 toOwner = owner.Center - p.Center;
			if (toOwner.Length() < 60f)
				return false;
			return Vector2.Dot(p.velocity.SafeNormalize(Vector2.Zero), toOwner.SafeNormalize(Vector2.Zero)) > 0.3f;
		}

		private static void DustBurst(Vector2 pos, int dustId, int count, float speed = 2f)
		{
			for (int i = 0; i < count; i++) {
				Dust d = Dust.NewDustDirect(pos - new Vector2(4, 4), 8, 8, dustId, Main.rand.NextFloat(-speed, speed), Main.rand.NextFloat(-speed, speed), 100, default, 0.9f);
				d.noGravity = true;
			}
		}

		private static void MarkFx(int whoAmI, Color color)
		{
			if (whoAmI < 0 || whoAmI >= Main.maxNPCs)
				return;
			NPC n = Main.npc[whoAmI];
			if (!n.active || n.friendly || n.life <= 0)
				return;
			for (int i = 0; i < 2; i++) {
				float ang = Main.GameUpdateCount * 0.08f + i * MathHelper.Pi;
				Vector2 p = n.Center + ang.ToRotationVector2() * (System.Math.Max(n.width, n.height) * 0.7f + 8f);
				Dust d = Dust.NewDustDirect(p - new Vector2(2, 2), 4, 4, DustID.GoldCoin, 0f, 0f, 120, color, 0.8f);
				d.noGravity = true;
				d.velocity = Vector2.Zero;
			}
		}

		private static void PullEnemies(Vector2 center, float radius, float strength)
		{
			float r2 = radius * radius;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5)
					continue;
				if (Vector2.DistanceSquared(center, n.Center) > r2)
					continue;
				if (n.knockBackResist >= 1f)
					continue;
				if (DummySafety.Pin(n))
					continue;
				n.velocity += (center - n.Center).SafeNormalize(Vector2.Zero) * strength;
			}
		}

		private void FollowUpHit(Projectile projectile, NPC target, float mult)
		{
			if (projectile.owner != Main.myPlayer)
				return;
			// Never punch through shield/intro phases or onto the already-dead.
			if (target.dontTakeDamage || target.life <= 0 || !target.active)
				return;
			if (hitCooldown.TryGetValue(target.whoAmI, out int cd) && cd > 0)
				return;
			hitCooldown[target.whoAmI] = 30;
			int dmg = System.Math.Max(1, (int)(projectile.damage * mult));
			NPC.HitInfo hit = new() {
				Damage = dmg,
				Knockback = 2f,
				HitDirection = System.Math.Sign(target.Center.X - projectile.Center.X),
				Crit = false
			};
			target.StrikeNPC(hit, false, false);
			target.netUpdate = true;
			DustBurst(projectile.Center, DustID.GoldCoin, 4, 1.5f);
		}

		// ---- Tuning ----
		private void RunTuning(Projectile projectile, Player owner, bool isOwner)
		{
			// Snapback: rebounds bank tension (diminishing), consumed on next hit.
			if (TuningId == 0) {
				if (tensionTimer > 0) tensionTimer--;
			}
			// Gyre orbit. Speed-scaled circling; releases instead of stretching
			// the string past max range (never stuck).
			if (TuningId == 1 && orbitNpc >= 0) {
				orbitTimer--;
				NPC n = orbitNpc >= 0 && orbitNpc < Main.maxNPCs ? Main.npc[orbitNpc] : null;
				float maxRange = ProjectileID.Sets.YoyosMaximumRange[projectile.type];
				if (n == null || !n.active || n.life <= 0 || orbitTimer <= 0 || n.type != orbitNpcType
					|| Vector2.Distance(owner.Center, n.Center) > maxRange * 1.1f) {
					orbitNpc = -1;
				}
				else {
					float playerDist = Vector2.Distance(owner.Center, projectile.Center);
					float radius = System.Math.Clamp(playerDist * 0.35f, 40f, 160f);
					float step = 0.05f + projectile.velocity.Length() * 0.004f;
					Vector2 toProj = (projectile.Center - n.Center).SafeNormalize(Vector2.UnitX);
					Vector2 want = n.Center + toProj.RotatedBy(step) * radius;
					projectile.Center = Vector2.Lerp(projectile.Center, want, 0.35f);
					if (isOwner && deployTicks % 20 == 0)
						FollowUpHit(projectile, n, 0.35f);
				}
			}
			if (TuningId == 1 && orbitNpc >= 0 && deployTicks % 15 == 0)
				MarkFx(orbitNpc, Color.Orange);
			// Deadweight anchor.
			if (TuningId == 2) {
				if (anchorCooldown > 0) anchorCooldown--;
				if (!anchored && projectile.velocity.Length() < 2.2f && anchorCooldown <= 0) {
					anchorTimer++;
					if (anchorTimer > 30) {
						anchored = true;
						anchorTimer = 180;
						if (isOwner) DustBurst(projectile.Center, DustID.Iron, 12);
					}
				}
				else if (!anchored) anchorTimer = 0;
				if (anchored) {
					anchorTimer--;
					projectile.velocity *= 0.90f;
					// Interrupt weak hostile shots nearby.
					if (isOwner) {
						for (int i = 0; i < Main.maxProjectiles; i++) {
							Projectile q = Main.projectile[i];
							if (!q.active || !q.hostile || q.damage > 50)
								continue;
							if (Vector2.DistanceSquared(q.Center, projectile.Center) < 48f * 48f) {
								q.active = false;
								DustBurst(q.Center, DustID.Electric, 6);
							}
						}
					}
					if (anchorTimer <= 0 || projectile.velocity.Length() > 7f) {
						anchored = false;
						anchorCooldown = 300;
						projectile.velocity += projectile.velocity.SafeNormalize(Vector2.UnitX) * 4f;
						if (isOwner) DustBurst(projectile.Center, DustID.Smoke, 10);
					}
				}
			}
			// Tension Break.
			if (TuningId == 3) {
				float dist = Vector2.Distance(owner.Center, projectile.Center);
				float prevDist = Vector2.Distance(owner.Center, lastPos);
				if (dist > prevDist + 0.5f)
					tension = System.Math.Min(100f, tension + 0.6f);
				else if (dist < prevDist - 0.5f)
					tension = System.Math.Min(100f, tension + 0.3f);
				if (tensionTimer > 0) {
					tensionTimer--;
					if (tensionTimer == 0) tension *= 0.5f;
				}
				else tension = System.Math.Max(0f, tension - 0.05f);
				if (tension >= 60f && dist > 200f && projectile.velocity.Length() > 6f && isOwner) {
					projectile.velocity *= 1.25f;
					// Clamp the launch to the yoyo's own speed class: no runaway.
					float cap = YoyoStaticBoosts.BaseTopSpeed(projectile.type) * 1.6f;
					if (projectile.velocity.Length() > cap)
						projectile.velocity = projectile.velocity.SafeNormalize(Vector2.UnitX) * cap;
					tension = 0f;
					tensionTimer = 600;
					DustBurst(projectile.Center, DustID.Electric, 10);
				}
			}
			// Springwake elasticity flip handled on rebound; apply here.
			if (TuningId == 6) {
				if (springFlip % 2 == 1)
					YoyoStaticBoosts.BoostTopSpeed(projectile.type, 1.12f);
				else if (springFlip > 0)
					YoyoStaticBoosts.BoostRange(projectile.type, 24f);
			}
			// Countermass timer.
			if (TuningId == 7 && isOwner && deployTicks % 300 == 0) {
				Vector2 opp = owner.Center + (owner.Center - projectile.Center).SafeNormalize(Vector2.UnitX) * 120f;
				Vector2 vel = (opp - projectile.Center).SafeNormalize(Vector2.UnitX) * 8f;
				int idx = Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, vel,
					ModContent.ProjectileType<CustomizationEchoProj>(), (int)(projectile.damage * 0.6f), 2f, projectile.owner, ai0: 90f, ai1: projectile.type);
				if (idx >= 0) DustBurst(projectile.Center, DustID.SilverCoin, 8);
			}
			// Needlepoint corridor.
			if (TuningId == 8) {
				Vector2 dir = projectile.velocity.Length() > 1f ? projectile.velocity.SafeNormalize(Vector2.Zero) : needleDir;
				float ang = System.Math.Abs(MathHelper.WrapAngle(dir.ToRotation() - needleDir.ToRotation()));
				if (ang < 0.22f) {
					needleTicks = System.Math.Min(600, needleTicks + 1);
					if (needleTicks % 120 == 0)
						needleDir = dir;
				}
				else {
					needleTicks = System.Math.Max(0, needleTicks - 6);
					if (needleTicks == 0) needleDir = dir;
				}
			}
			// Slipphase.
			if (TuningId == 9) {
				if (slipCooldown > 0) slipCooldown--;
				if (slipTimer > 0) {
					slipTimer--;
					projectile.tileCollide = false;
					if (slipTimer == 0) {
						projectile.tileCollide = true;
						slipCooldown = 1200;
					}
				}
				else if (slipCooldown <= 0 && isOwner) {
					// Auto-phase when wedged inside solid.
					Point pt = projectile.Center.ToTileCoordinates();
					Tile tl = Framing.GetTileSafely(pt.X, pt.Y);
					if (tl.HasTile && Main.tileSolid[tl.TileType]) {
						slipTimer = 60;
						DustBurst(projectile.Center, DustID.MagicMirror, 12);
					}
				}
			}
			// Gravity Well pull.
			if (TuningId == 101 && projectile.velocity.Length() > 4f)
				PullEnemies(projectile.Center, 90f, 0.25f);
			// Outbound Engine: delayed return + control tradeoff.
			if (TuningId == 103) {
				YoyoStaticBoosts.BoostLifetime(projectile.type, 1.6f);
				YoyoStaticBoosts.BoostRange(projectile.type, 40f);
				if (deployTicks > 600)
					projectile.velocity *= 0.995f;
			}
			// Flywheel passive record.
			if (TuningId == 105)
				flywheel = System.Math.Min(2000f, flywheel + projectile.velocity.Length() * 0.05f);
			// Living Mechanism adaptation.
			if (TuningId == 107) {
				livingTimer++;
				if (livingTimer > 300 && livingCharges > 0) {
					livingTimer = 0;
					livingCharges--;
					int pick = Main.rand.Next(3);
					if (pick == 0) YoyoStaticBoosts.BoostTopSpeed(projectile.type, 1.15f);
					else if (pick == 1) YoyoStaticBoosts.BoostRange(projectile.type, 32f);
					else YoyoStaticBoosts.BoostLifetime(projectile.type, 1.3f);
					if (isOwner) DustBurst(projectile.Center, DustID.Grass, 8);
				}
				if (deployTicks % 1800 == 0) livingCharges = System.Math.Min(3, livingCharges + 1);
			}
		}

		private void OnReboundTuning(Projectile projectile, Player owner, bool isOwner)
		{
			if (TuningId == 0) {
				tension = System.Math.Min(100f, tension + 25f / (1f + tension / 40f));
				tensionTimer = 600;
			}
			if (TuningId == 4 && forkCooldown <= 0 && isOwner) {
				forkCooldown = 90;
				Vector2 dir = projectile.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.UnitX);
				Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, dir * 9f,
					ModContent.ProjectileType<CustomizationEchoProj>(), (int)(projectile.damage * 0.5f), 2f, projectile.owner, ai0: 70f, ai1: projectile.type);
				DustBurst(projectile.Center, DustID.Cloud, 8);
			}
			if (TuningId == 4 && forkCooldown > 0) forkCooldown--;
			if (TuningId == 6) springFlip++;
			if (TuningId == 100) {
				// Pinball Heart sequence: each rebound must differ in angle class.
				float ang = System.Math.Abs(MathHelper.WrapAngle(projectile.velocity.ToRotation() - lastVel.ToRotation()));
				int want = pinballStage % 3;
				bool ok = want == 0 ? ang > 1.2f : want == 1 ? ang > 0.6f && ang <= 1.2f : ang <= 0.6f;
				if (ok) {
					pinballStage++;
					if (pinballStage >= 5) {
						pinballStage = 0;
						if (isOwner) {
							for (int i = 0; i < Main.maxNPCs; i++) {
								NPC n = Main.npc[i];
								if (!n.active || n.friendly || n.dontTakeDamage) continue;
								if (Vector2.DistanceSquared(projectile.Center, n.Center) < 160f * 160f) {
									FollowUpHit(projectile, n, 1.5f);
									break;
								}
							}
							DustBurst(projectile.Center, DustID.GoldCoin, 16, 4f);
							CombatText.NewText(new Rectangle((int)projectile.Center.X - 40, (int)projectile.Center.Y - 40, 80, 30), Color.Gold, "PINBALL!");
						}
					}
				}
				else pinballStage = 0;
			}
			if (TuningId == 104) {
				// Prey Circuit transfer marker refresh.
				tension = 60f;
				tensionTimer = 120;
			}
		}

		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (TuningId == 0 && tension > 10f) {
				modifiers.SourceDamage *= 1f + System.Math.Min(0.8f, tension / 150f);
			}
			if (TuningId == 8 && needleTicks > 120) {
				modifiers.SourceDamage *= 1f + System.Math.Min(0.35f, needleTicks / 2000f);
			}
			if (TuningId == 100 && pinballStage >= 4) {
				modifiers.SourceDamage *= 1.6f;
			}
			if (TuningId == 105 && flywheel > 500f) {
				modifiers.SourceDamage *= 1f + System.Math.Min(0.6f, flywheel / 4000f);
			}
			if (TuningId == 104 && preyArmed) {
				preyArmed = false;
				modifiers.SourceDamage *= 1.45f;
			}
			ModifyHitCalibration(projectile, target, ref modifiers);
			ModifyHitMastery(projectile, target, ref modifiers);
			ModifyHitCurse(projectile, target, ref modifiers);
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			// First connect each throw announces the fitted kit once, so a hit
			// always answers "is anything even on?" with names, not silence.
			if (!announced && projectile.owner == Main.myPlayer
				&& (TuningId >= 0 || CalibrationId >= 0 || MasteryId >= 0 || CurseId >= 0)) {
				announced = true;
				string name = TuningId >= 0 ? YoyoTuningDefs.ById(TuningId)?.Name
					: CalibrationId >= 0 ? YoyoCalibrationDefs.ById(CalibrationId)?.Name
					: MasteryId >= 0 ? YoyoMasteryDefs.ById(MasteryId)?.Name
					: YoyoCurseDefs.ById(CurseId)?.Name;
				Color tint = CurseId >= 0 ? Color.OrangeRed
					: MasteryId >= 100 ? new Color(200, 170, 255)
					: MasteryId >= 0 ? Color.Gold
					: CalibrationId >= 100 ? new Color(150, 220, 255)
					: CalibrationId >= 0 ? Color.PaleGreen
					: Color.LightSkyBlue;
				if (!string.IsNullOrEmpty(name))
					CombatText.NewText(new Rectangle((int)projectile.Center.X - 60, (int)projectile.Center.Y - 46, 120, 24), tint, name + "!");
			}
			if (TuningId == 0 && tension > 10f) {
				tension = 0f;
				tensionTimer = 0;
				DustBurst(projectile.Center, DustID.Electric, 10);
			}
			if (TuningId == 1 && target.life > 0) {
				orbitNpc = target.whoAmI;
				orbitNpcType = target.type;
				orbitTimer = 180;
			}
			if (TuningId == 5) {
				// Record struck foe for return-vector follow-up (10s memory).
				hitCooldown[target.whoAmI] = System.Math.Max(hitCooldown.GetValueOrDefault(target.whoAmI), 1);
				returnMark[target.whoAmI] = 600;
			}
			if (TuningId == 102) {
				trail.Add(projectile.Center);
				if (trail.Count > 60) trail.RemoveAt(0);
				if (trail.Count >= 50 && afterimageCooldown <= 0 && projectile.owner == Main.myPlayer) {
					afterimageCooldown = 900;
					Vector2 dir = (trail[^1] - trail[0]).SafeNormalize(Vector2.UnitX);
					Projectile.NewProjectile(projectile.GetSource_FromThis(), trail[0], dir * 10f,
						ModContent.ProjectileType<CustomizationEchoProj>(), (int)(projectile.damage * 0.6f), 2f, projectile.owner, ai0: 80f, ai1: projectile.type);
					trail.Clear();
				}
			}
			if (TuningId == 105) {
				flywheel = System.Math.Max(0f, flywheel - 400f);
			}
			if (TuningId == 106) {
				if (!crossList.Contains(target.whoAmI)) {
					crossList.Add(target.whoAmI);
					if (crossList.Count > 5) crossList.RemoveAt(0);
				}
			}
			if (TuningId == 104) {
				// Prey Circuit: switching targets arms the next link (+45%).
				if (preyWindow > 0 && target.whoAmI != preyNpc)
					preyArmed = true;
				preyNpc = target.whoAmI;
				preyWindow = 150;
			}
			OnHitCalibration(projectile, target, hit, damageDone);
			OnHitMastery(projectile, target, hit, damageDone);
			OnHitCurse(projectile, target, hit, damageDone);
		}

		partial void RunCalibration(Projectile projectile, Player owner, bool isOwner);
		partial void OnReboundCalibration(Projectile projectile, Player owner, bool isOwner);
		partial void ModifyHitCalibration(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers);
		partial void OnHitCalibration(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone);
		partial void RunMastery(Projectile projectile, Player owner, bool isOwner);
		partial void OnReboundMastery(Projectile projectile, Player owner, bool isOwner);
		partial void ModifyHitMastery(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers);
		partial void OnHitMastery(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone);
		partial void RunCurses(Projectile projectile, Player owner, bool isOwner);
		partial void OnReboundCurse(Projectile projectile, Player owner, bool isOwner);
		partial void ModifyHitCurse(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers);
		partial void OnHitCurse(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone);
	}

	// Short-lived echo that replays a recorded segment. Never copies itself.
	public class CustomizationEchoProj : ModProjectile
	{
		// ai0: inherited lifetime hint. ai1: source yoyo projectile type, so the
		// echo wears its yoyo's own sprite under proper tile lighting.
		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 3;
			Projectile.timeLeft = 90;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			Projectile.rotation += 0.3f;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.4f, 0.5f);
			if (Main.rand.NextBool(4)) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, 0f, 0f, 100, default, 0.8f);
				d.noGravity = true;
			}
			Projectile.timeLeft--;
			if (Projectile.timeLeft <= 0)
				Projectile.Kill();
			// ai0 carries lifetime at spawn; enforce hit-count via penetrate (vanilla).
		}

		public override bool PreDraw(ref Color lightColor)
		{
			int src = (int)Projectile.ai[1];
			Texture2D tex = null;
			if (src > ProjectileID.None && src < TextureAssets.Projectile.Length) {
				try { tex = TextureAssets.Projectile[src].Value; }
				catch { tex = null; }
			}
			if (tex == null)
				return true; // fall back to this projectile's own texture
			// Proper lighting: sample the tile light where the echo flies,
			// ghosted translucent so it reads as an afterimage, not a twin.
			Color tileLight = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
			Color ghost = new Color(tileLight.R, tileLight.G, tileLight.B, (byte)140);
			Vector2 origin = new Vector2(tex.Width, tex.Height) / 2f;
			float scale = System.Math.Min(1f, 28f / System.Math.Max(tex.Width, tex.Height));
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, ghost, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
