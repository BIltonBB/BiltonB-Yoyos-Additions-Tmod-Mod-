using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Content.Customization
{
	public partial class YoyoCustomizationGlobalProjectile : GlobalProjectile
	{
		partial void RunCalibration(Projectile projectile, Player owner, bool isOwner)
		{
			if (CalibrationId < 0)
				return;
			// Miss detector: 5 hitless seconds opens recovery for Reacquisition /
			// Fault Recovery. Any hit resets the clock (see OnHitCalibration).
			if (CalibrationId == 5 || CalibrationId == 107) {
				ticksSinceHit++;
				if (ticksSinceHit >= 300) {
					ticksSinceHit = 0;
					if (CalibrationId == 5) missTimer = 240;
					else faultTimer = 240;
					if (isOwner) DustBurst(projectile.Center, DustID.HealingPlus, 6);
				}
			}
			// Rhythm windows.
			if (rhythmTimer > 0) rhythmTimer--;
			else if (CalibrationId == 2 && rhythmStage > 0) {
				rhythmStage = 0;
				rhythmWindow = 45;
			}
			// Orbit angle accumulation for Precession.
			Vector2 toYoyo = projectile.Center - owner.Center;
			if (toYoyo.Length() > 40f) {
				Vector2 dir = toYoyo.SafeNormalize(Vector2.Zero);
				if (orbitTracking) {
					float d = MathHelper.WrapAngle(dir.ToRotation() - orbitLastDir.ToRotation());
					orbitAngleAccum += d;
					if (System.Math.Abs(orbitAngleAccum) > MathHelper.TwoPi) {
						// Completed a loop: tilt next attack.
						orbitAngleAccum = 0f;
						if (CalibrationId == 3 && isOwner) {
							projectile.velocity = projectile.velocity.RotatedBy(d > 0 ? 0.35f : -0.35f);
							DustBurst(projectile.Center, DustID.GoldCoin, 8);
						}
						if (CalibrationId == 101 && isOwner && !closedMarking) {
							closedMarking = true;
							closedStart = projectile.Center;
						}
					}
				}
				orbitLastDir = dir;
				orbitTracking = true;
			}
			// Parallax geometry.
			if (CalibrationId == 4) {
				if (parallaxTimer > 0) parallaxTimer--;
				else {
					parallaxBase = owner.Center;
					parallaxTimer = 240;
				}
			}
			// Capacitor charge state (id 6): building while banked but not yet full.
			if (CalibrationId == 6)
				capacitorCharging = capacitor > 0f && capacitor < 60f;
			// Deadband windows.
			if (CalibrationId == 8) {
				deadbandTimer++;
				if (deadbandTimer > 300) deadbandTimer = 0;
			}
			// Redline pressure.
			if (CalibrationId == 100) {
				if (projectile.velocity.Length() > 5f)
					redline = System.Math.Min(100f, redline + 0.25f);
				if (redlineActive) {
					redlineTimer--;
					redline += 0.4f;
					if (redline > 140f && isOwner) {
						// Overheld: brief instability.
						projectile.velocity *= 0.9f;
						redline = 0f;
						redlineActive = false;
					}
					if (redlineTimer <= 0) {
						redlineActive = false;
						redline = 0f;
					}
				}
				else if (redline >= 60f && isOwner && deployTicks % 600 == 0) {
					redlineActive = true;
					redlineTimer = 300;
					YoyoStaticBoosts.BoostTopSpeed(projectile.type, 1.2f);
				}
			}
			// Overpressure.
			if (CalibrationId == 106) {
				if (overActive) {
					overTimer--;
					overpressure += 0.5f;
					YoyoStaticBoosts.BoostTopSpeed(projectile.type, 1.25f);
					if (overpressure > 120f && isOwner) {
						projectile.velocity *= 0.75f;
						overActive = false;
						overpressure = 0f;
						overTimer = 0;
					}
					else if (overTimer <= 0) {
						overActive = false;
						overpressure = System.Math.Max(0f, overpressure - 60f);
					}
				}
				else overpressure = System.Math.Max(0f, overpressure - 0.2f);
			}
			if (faultTimer > 0) faultTimer--;
			if (escapeTimer > 0) escapeTimer--;
			if (harmonicTimer > 0) {
				harmonicTimer--;
				if (harmonicTimer == 0) harmonicStage = 0;
			}
		}

		partial void OnReboundCalibration(Projectile projectile, Player owner, bool isOwner)
		{
			if (CalibrationId == 5) {
				// Corrective rebound after a miss.
				if (missTimer >= 0) {
					missTimer = -1;
					capacitor = System.Math.Min(100f, capacitor + 40f);
					if (isOwner) DustBurst(projectile.Center, DustID.HealingPlus, 8);
				}
			}
			if (CalibrationId == 104) {
				// Well-spaced rebound continues harmony.
				if (harmonicTimer > 0) harmonicStage++;
				harmonicTimer = 90;
			}
			if (CalibrationId == 105) escapeTimer = 180;
			if (CalibrationId == 107 && faultTimer > 0) {
				faultTimer = 0;
				capacitor = System.Math.Min(100f, capacitor + 30f);
			}
		}

		partial void ModifyHitCalibration(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (CalibrationId < 0)
				return;
			Player owner = Main.player[projectile.owner];
			Vector2 incoming = projectile.velocity.SafeNormalize(Vector2.Zero);
			if (CalibrationId == 0) {
				// Direct (head-on) vs glancing.
				Vector2 toTarget = (target.Center - projectile.Center).SafeNormalize(Vector2.Zero);
				float dot = Vector2.Dot(incoming, toTarget);
				if (dot > 0.7f) modifiers.SourceDamage *= 1.25f;
				else if (dot < 0.2f) modifiers.SourceDamage *= 1.10f;
			}
			if (CalibrationId == 1 && crosscutTimer > 0) {
				float ang = System.Math.Abs(MathHelper.WrapAngle(incoming.ToRotation() - lastHitDir.ToRotation()));
				if (ang > 0.9f) modifiers.SourceDamage *= 1.45f;
			}
			if (CalibrationId == 2 && rhythmStage >= 3) modifiers.SourceDamage *= 1f + rhythmStage * 0.05f;
			if (CalibrationId == 4) {
				float moved = Vector2.Distance(owner.Center, parallaxBase);
				if (moved > 160f) modifiers.SourceDamage *= 1.3f;
			}
			if (CalibrationId == 6 && capacitorCharging) modifiers.SourceDamage *= 1.1f;
			if (CalibrationId == 5 && missTimer < 0 && capacitor > 20f) modifiers.SourceDamage *= 1.15f;
			if (CalibrationId == 8) {
				// Deadband: 40-tick window every 300 ticks.
				if (deadbandTimer % 300 < 40) modifiers.SourceDamage *= 1.5f;
			}
			if (CalibrationId == 100 && redlineActive) modifiers.SourceDamage *= 1.3f;
			if (CalibrationId == 102 && triangAngles.Count >= 2) modifiers.SourceDamage *= 1.4f;
			if (CalibrationId == 103 && impulse > 30f) modifiers.SourceDamage *= 1f + impulse / 300f;
			if (CalibrationId == 104 && harmonicStage >= 4) modifiers.SourceDamage *= 1.5f;
			if (CalibrationId == 105 && escapeTimer > 0) modifiers.SourceDamage *= 1.25f;
			if (CalibrationId == 106 && overActive) modifiers.SourceDamage *= 1.4f;
			if (CalibrationId == 107 && faultTimer > 0) modifiers.SourceDamage *= 1.2f;
			if (CalibrationId == 9 && diffCurves.Count >= 3) {
				float lo = diffCurves[0], hi = diffCurves[0];
				foreach (float q in diffCurves) {
					if (q < lo) lo = q;
					if (q > hi) hi = q;
				}
				if (hi - lo > 0.25f) modifiers.SourceDamage *= 1.25f;
			}
		}

		partial void OnHitCalibration(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (CalibrationId < 0)
				return;
			ticksSinceHit = 0;
			Vector2 incoming = projectile.velocity.SafeNormalize(Vector2.Zero);
			if (CalibrationId == 1) {
				if (!hasLastHitDir) {
					lastHitDir = incoming;
					hasLastHitDir = true;
					crosscutTimer = 240;
				}
				else {
					float ang = System.Math.Abs(MathHelper.WrapAngle(incoming.ToRotation() - lastHitDir.ToRotation()));
					if (ang > 0.9f && projectile.owner == Main.myPlayer) {
						FollowUpHit(projectile, target, 0.8f);
						hasLastHitDir = false;
						crosscutTimer = 0;
					}
					else {
						lastHitDir = incoming;
						crosscutTimer = 240;
					}
				}
			}
			if (CalibrationId == 2) {
				if (rhythmTimer <= 0 || deployTicks - rhythmTimer < rhythmWindow + 60) {
					rhythmStage++;
					rhythmWindow = System.Math.Max(18, 45 - rhythmStage * 3);
					rhythmTimer = rhythmWindow;
				}
				else {
					rhythmStage = 1;
					rhythmWindow = 45;
					rhythmTimer = rhythmWindow;
				}
			}
			if (CalibrationId == 6) {
				capacitor = System.Math.Min(100f, capacitor + 12f);
				if (capacitor >= 60f && projectile.owner == Main.myPlayer) {
					// The charging path shapes the release: a long straight run discharges
					// hard, a short curled run discharges quick (burst of speed + dust).
					float pathLen = Vector2.Distance(projectile.Center, capacitorStart);
					if (pathLen > 300f)
						FollowUpHit(projectile, target, 0.9f);
					else {
						FollowUpHit(projectile, target, 0.5f);
						projectile.velocity *= 1.15f;
					}
					capacitor = 0f;
					capacitorStart = projectile.Center;
				}
			}
			if (CalibrationId == 7) {
				threadPts.Add(target.Center);
				if (threadPts.Count > 6) threadPts.RemoveAt(0);
				if (threadPts.Count >= 4 && projectile.owner == Main.myPlayer) {
					FollowUpHit(projectile, target, 1.0f);
					threadPts.Clear();
				}
			}
			if (CalibrationId == 9) {
				float curve = System.Math.Abs(MathHelper.WrapAngle(projectile.velocity.ToRotation() - lastVel.ToRotation()));
				diffCurves.Add(curve);
				if (diffCurves.Count > 5) diffCurves.RemoveAt(0);
			}
			if (CalibrationId == 101 && closedMarking) {
				if (Vector2.Distance(projectile.Center, closedStart) < 50f) {
					closedMarking = false;
					if (projectile.owner == Main.myPlayer) {
						FollowUpHit(projectile, target, 1.2f);
						DustBurst(projectile.Center, DustID.GoldCoin, 14);
					}
				}
			}
			if (CalibrationId == 102) {
				float a = incoming.ToRotation();
				bool fresh = true;
				foreach (float q in triangAngles) {
					if (System.Math.Abs(MathHelper.WrapAngle(a - q)) < 0.5f) { fresh = false; break; }
				}
				if (fresh) {
					triangAngles.Add(a);
					if (triangAngles.Count >= 3) {
						triangAngles.Clear();
						if (projectile.owner == Main.myPlayer) FollowUpHit(projectile, target, 1.3f);
					}
				}
			}
			if (CalibrationId == 103) impulse = System.Math.Min(100f, impulse + 20f);
			if (CalibrationId == 104) {
				harmonicStage++;
				harmonicTimer = 120;
				if (harmonicStage >= 5 && projectile.owner == Main.myPlayer) {
					FollowUpHit(projectile, target, 1.1f);
					harmonicStage = 0;
				}
			}
		}
	}
}
