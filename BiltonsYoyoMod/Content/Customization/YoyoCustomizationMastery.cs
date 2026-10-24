using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Customization
{
	public partial class YoyoCustomizationGlobalProjectile : GlobalProjectile
	{
		partial void RunMastery(Projectile projectile, Player owner, bool isOwner)
		{
			if (MasteryId < 0)
				return;
			if (encoreTimer > 0) {
				encoreTimer--;
				if (encoreTimer == 0) encoreStage = 0;
			}
			if (lastWordTimer > 0) {
				lastWordTimer--;
				if (lastWordTimer == 0) lastWordNpc = -1;
			}
			if (farpointTimer > 0) {
				farpointTimer--;
				if (farpointTimer == 0) farpointNpc = -1;
			}
			if (reversalTimer > 0) reversalTimer--;
			// Reversal: an enemy shot whistling past the owner (close but harmless)
			// opens a counter window against the nearest foe.
			if (MasteryId == 6 && isOwner && deployTicks % 12 == 0) {
				bool buzzed = false;
				for (int i = 0; i < Main.maxProjectiles; i++) {
					Projectile q = Main.projectile[i];
					if (!q.active || !q.hostile || q.damage <= 0)
						continue;
					if (Vector2.DistanceSquared(q.Center, owner.Center) < 100f * 100f) {
						buzzed = true;
						break;
					}
				}
				if (buzzed) {
					int best = -1;
					float bestD = 600f * 600f;
					for (int i = 0; i < Main.maxNPCs; i++) {
						NPC n = Main.npc[i];
						if (!n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5)
							continue;
						float d = Vector2.DistanceSquared(owner.Center, n.Center);
						if (d < bestD) { bestD = d; best = i; }
					}
					reversalTimer = 150;
					reversalNpc = best;
					DustBurst(owner.Center, DustID.Electric, 4);
				}
			}
			if (misdirectTimer > 0) {
				misdirectTimer--;
				if (misdirectTimer == 0) { misdirectMix = 0; misdirectMarked = false; }
			}
			if (lockdownNpc >= 0) {
				NPC n = lockdownNpc < Main.maxNPCs ? Main.npc[lockdownNpc] : null;
				if (n == null || !n.active || n.life <= 0) lockdownNpc = -1;
				else if (MasteryId == 4 && deployTicks % 15 == 0) MarkFx(lockdownNpc, Color.Gold);
			}
			if (MasteryId == 1 && lastWordNpc >= 0 && deployTicks % 15 == 0) MarkFx(lastWordNpc, Color.Red);
			if (MasteryId == 8 && farpointNpc >= 0 && deployTicks % 15 == 0) MarkFx(farpointNpc, Color.Cyan);
			if (MasteryId == 5 && misdirectMarked && misdirectNpc >= 0 && deployTicks % 15 == 0) MarkFx(misdirectNpc, new Color(200, 150, 255));
			if (MasteryId == 3 && untouchedTicks > 600 && deployTicks % 30 == 0 && isOwner)
				DustBurst(owner.Center, DustID.GoldCoin, 2, 1f);
			// Untouched charge: taking damage wipes it (immune frames = recent hurt).
			if (MasteryId == 3) {
				if (owner.immune)
					untouchedTicks = 0;
				else if (deployTicks % 2 == 0)
					untouchedTicks = System.Math.Min(3600, untouchedTicks + 1);
			}
			// Waltz Flow upkeep.
			if (MasteryId == 7) {
				waltzTimer++;
				if (waltzTimer > 600) {
					waltzTimer = 0;
					int variety = (waltzRebound > 0 ? 1 : 0) + (waltzOrbit > 0 ? 1 : 0) + (waltzReturn > 0 ? 1 : 0) + (waltzSwap > 0 ? 1 : 0);
					flow = System.Math.Clamp(variety * 25, 0, 100);
					waltzRebound = waltzOrbit = waltzReturn = waltzSwap = 0;
				}
				if (IsReturning(projectile, owner)) waltzReturn = 1;
			}
			// True mastery timers.
			if (secondTimer > 0) {
				secondTimer--;
				if (secondTimer == 0) secondStage = 0;
			}
			// Circuit Breaker events expire if the route stalls.
			if (MasteryId == 100 && circuitTimer > 0) {
				circuitTimer--;
				if (circuitTimer == 0) circuitEvents = 0;
			}
			if (cathedralTimer > 0) {
				cathedralTimer--;
				if (cathedralTimer == 0) cathedralStage = 0;
			}
			if (confTimer > 0) {
				confTimer--;
				if (confTimer == 0) { confCombo = confLoop = confDir = 0; }
			}
			if (resonanceTimer > 0) {
				resonanceTimer--;
				if (resonanceTimer == 0) resonance *= 0.5f;
			}
			if (continuumTimer > 0) {
				continuumTimer--;
				if (continuumTimer == 0) continuumPts.Clear();
			}
			if (ghostCooldown > 0) ghostCooldown--;
		}

		partial void OnReboundMastery(Projectile projectile, Player owner, bool isOwner)
		{
			if (MasteryId == 0) {
				// Varying the line advances encore faster.
				encoreStage = System.Math.Min(4, encoreStage + 1);
				encoreTimer = 300;
			}
			if (MasteryId == 5) {
				misdirectMix++;
				misdirectTimer = 300;
			}
			if (MasteryId == 7) waltzRebound = 1;
			if (MasteryId == 104) {
				// A rebound closes one trajectory loop of the confluence.
				confLoop++;
				confTimer = 300;
			}
			if (MasteryId == 103) {
				cathedralStage++;
				cathedralTimer = 240;
				if (cathedralStage >= 4 && isOwner) {
					cathedralStage = 0;
					CombatText.NewText(new Rectangle((int)projectile.Center.X - 40, (int)projectile.Center.Y - 40, 80, 30), new Color(190, 130, 255), "TOLL!");
					DustBurst(projectile.Center, DustID.Shadowflame, 16, 4f);
					for (int i = 0; i < Main.maxNPCs; i++) {
						NPC n = Main.npc[i];
						if (!n.active || n.friendly || n.dontTakeDamage) continue;
						if (Vector2.DistanceSquared(projectile.Center, n.Center) < 200f * 200f) {
							FollowUpHit(projectile, n, 1.4f);
							break;
						}
					}
				}
			}
		}

		partial void ModifyHitMastery(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (MasteryId < 0)
				return;
			if (MasteryId == 0 && encoreStage > 0) modifiers.SourceDamage *= 1f + encoreStage * 0.08f;
			if (MasteryId == 1 && target.whoAmI == lastWordNpc) {
				bool weak = (float)target.life / target.lifeMax < 0.35f;
				if (weak) modifiers.SourceDamage *= target.boss ? 1.3f : 1.8f;
			}
			if (MasteryId == 3 && untouchedTicks > 600) modifiers.SourceDamage *= 1.25f;
			if (MasteryId == 4 && target.whoAmI == lockdownNpc) modifiers.SourceDamage *= 1f + System.Math.Min(0.5f, lockdownTicks / 1200f);
			if (MasteryId == 5 && misdirectMarked && target.whoAmI == misdirectNpc) modifiers.SourceDamage *= 1.5f;
			if (MasteryId == 6 && reversalTimer > 0 && target.whoAmI == reversalNpc) modifiers.SourceDamage *= 1.7f;
			if (MasteryId == 7 && flow > 50) modifiers.SourceDamage *= 1f + flow / 500f;
			if (MasteryId == 8 && target.whoAmI == farpointNpc) modifiers.SourceDamage *= 1.5f;
			if (MasteryId == 9 && patternStreak > 0) modifiers.SourceDamage *= 1.2f;
			if (MasteryId == 100 && circuitEvents >= 3) modifiers.SourceDamage *= 1.4f;
			if (MasteryId == 104 && confCombo > 0 && confLoop > 0 && confDir > 0) modifiers.SourceDamage *= 1.6f;
			if (MasteryId == 105 && sovereignAngles.Count >= 3) modifiers.SourceDamage *= 1.5f;
			if (MasteryId == 106 && resonance > 50f) modifiers.SourceDamage *= 1f + resonance / 250f;
		}

		partial void OnHitMastery(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (MasteryId < 0)
				return;
			Player owner = Main.player[projectile.owner];
			float maxRange = ProjectileID.Sets.YoyosMaximumRange[projectile.type];
			float dist = Vector2.Distance(owner.Center, projectile.Center);
			if (MasteryId == 0) {
				encoreStage = System.Math.Min(4, encoreStage + 1);
				encoreTimer = 300;
				if (encoreStage >= 3 && projectile.owner == Main.myPlayer)
					DustBurst(projectile.Center, DustID.Confetti, 6);
			}
			if (MasteryId == 1) {
				if ((float)target.life / target.lifeMax < 0.35f) {
					lastWordNpc = target.whoAmI;
					lastWordTimer = 300;
				}
				else if (target.whoAmI == lastWordNpc) {
					lastWordNpc = -1;
					lastWordTimer = 0;
				}
			}
			if (MasteryId == 2) {
				chainMarks[target.whoAmI] = 300;
				// Tag neighbors.
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC n = Main.npc[i];
					if (!n.active || n.friendly || n.dontTakeDamage || i == target.whoAmI) continue;
					if (Vector2.DistanceSquared(n.Center, target.Center) < 180f * 180f)
						chainMarks[i] = 300;
				}
				if (chainMarks.Count > 6 && projectile.owner == Main.myPlayer)
					FollowUpHit(projectile, target, 0.6f);
			}
			if (MasteryId == 4) {
				if (lockdownNpc != target.whoAmI) {
					// Swapping spends focus.
					if (lockdownNpc >= 0 && lockdownTicks > 180 && projectile.owner == Main.myPlayer)
						FollowUpHit(projectile, target, 0.8f);
					lockdownNpc = target.whoAmI;
					lockdownTicks = 0;
				}
				else lockdownTicks += 60;
			}
			if (MasteryId == 5) {
				misdirectMix++;
				misdirectTimer = 300;
				if (misdirectMix >= 3 && !misdirectMarked) {
					misdirectMarked = true;
					misdirectNpc = target.whoAmI;
				}
				else if (misdirectMarked && target.whoAmI == misdirectNpc && projectile.owner == Main.myPlayer) {
					FollowUpHit(projectile, target, 0.9f);
					misdirectMarked = false;
					misdirectMix = 0;
				}
			}
			if (MasteryId == 7) waltzSwap = 1;
			if (MasteryId == 8) {
				if (dist > maxRange * 0.8f) {
					farpointNpc = target.whoAmI;
					farpointTimer = 300;
				}
				else if (target.whoAmI == farpointNpc) {
					farpointNpc = -1;
					farpointTimer = 0;
				}
			}
			if (MasteryId == 9) {
				float ang = projectile.velocity.ToRotation();
				if (lastHitNpc >= 0 && target.whoAmI != lastHitNpc) patternStreak++;
				else if (System.Math.Abs(MathHelper.WrapAngle(ang - lastHitAngle)) > 0.6f) patternStreak++;
				else patternStreak = 0;
				lastHitNpc = target.whoAmI;
				lastHitAngle = ang;
			}
			// True mastery.
			if (MasteryId == 100) {
				circuitEvents++;
				circuitTimer = 300;
				if (circuitEvents >= 4 && projectile.owner == Main.myPlayer) {
					FollowUpHit(projectile, target, 1.2f);
					circuitEvents = 0;
					circuitTimer = 0;
				}
			}
			if (MasteryId == 101) {
				secondStage++;
				// Second Hand alternates long/short timing windows.
				secondExpectLong = !secondExpectLong;
				secondTimer = secondExpectLong ? 150 : 90;
				if (secondStage >= 4 && projectile.owner == Main.myPlayer) {
					secondStage = 0;
					Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, projectile.velocity.RotatedBy(0.5f),
						ModContent.ProjectileType<CustomizationEchoProj>(), (int)(projectile.damage * 0.7f), 2f, projectile.owner, ai0: 70f, ai1: projectile.type);
				}
			}
			if (MasteryId == 102 && ghostCooldown <= 0) {
				ghostRecord.Add(projectile.Center);
				if (ghostRecord.Count > 40) ghostRecord.RemoveAt(0);
				if (ghostRecord.Count >= 30 && projectile.owner == Main.myPlayer) {
					ghostCooldown = 900;
					Projectile.NewProjectile(projectile.GetSource_FromThis(), ghostRecord[0], (ghostRecord[^1] - ghostRecord[0]).SafeNormalize(Vector2.UnitX) * 9f,
						ModContent.ProjectileType<CustomizationEchoProj>(), (int)(projectile.damage * 0.6f), 2f, projectile.owner, ai0: 75f, ai1: projectile.type);
					ghostRecord.Clear();
				}
			}
			if (MasteryId == 104) {
				confCombo++;
				confTimer = 300;
				// A strike from a fresh line feeds the directional strand.
				float ang = projectile.velocity.ToRotation();
				if (System.Math.Abs(MathHelper.WrapAngle(ang - confLastAng)) > 0.6f) {
					confDir++;
					confLastAng = ang;
				}
				if (confCombo >= 2 && confLoop >= 1 && confDir >= 1 && projectile.owner == Main.myPlayer) {
					CombatText.NewText(new Rectangle((int)projectile.Center.X - 50, (int)projectile.Center.Y - 40, 100, 30), Color.Cyan, "CONFLUENCE!");
					FollowUpHit(projectile, target, 1.5f);
					confCombo = confLoop = confDir = 0;
				}
			}
			if (MasteryId == 105) {
				float a = (target.Center - projectile.Center).ToRotation();
				bool fresh = true;
				foreach (float q in sovereignAngles) {
					if (System.Math.Abs(MathHelper.WrapAngle(a - q)) < 0.45f) { fresh = false; break; }
				}
				if (fresh) {
					sovereignAngles.Add(a);
					if (sovereignAngles.Count >= 5) {
						sovereignAngles.Clear();
						if (projectile.owner == Main.myPlayer) FollowUpHit(projectile, target, 1.4f);
					}
				}
			}
			if (MasteryId == 106) {
				resonance = System.Math.Min(100f, resonance + 18f);
				resonanceTimer = 300;
			}
			if (MasteryId == 107) {
				continuumPts.Add(target.Center);
				continuumTimer = 300;
				if (continuumPts.Count >= 5 && projectile.owner == Main.myPlayer) {
					FollowUpHit(projectile, target, 1.3f);
					continuumPts.Clear();
				}
			}
		}
	}
}
