using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Content.Customization
{
	public partial class YoyoCustomizationGlobalProjectile : GlobalProjectile
	{
		partial void RunCurses(Projectile projectile, Player owner, bool isOwner)
		{
			if (CurseId < 0)
				return;
			// Low curses.
			if (CurseId == 4) {
				// Magnetic Obsession: bend toward nearby foes, wobbling aim.
				NPC best = null;
				float bestD2 = 220f * 220f;
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC n = Main.npc[i];
					if (!n.active || n.friendly || n.dontTakeDamage) continue;
					float d2 = Vector2.DistanceSquared(projectile.Center, n.Center);
					if (d2 < bestD2) { bestD2 = d2; best = n; }
				}
				if (best != null)
					projectile.velocity += (best.Center - projectile.Center).SafeNormalize(Vector2.Zero) * 0.35f;
			}
			if (CurseId == 5 && deployTicks % 120 == 0 && isOwner) {
				// Echo Chamber: replay a slice of the last route.
				Vector2 echo = projectile.velocity.RotatedBy(0.6f);
				Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, echo * 0.7f,
					ModContent.ProjectileType<CustomizationEchoProj>(), (int)(projectile.damage * 0.4f), 1f, projectile.owner, ai0: 60f, ai1: projectile.type);
			}
			if (CurseId == 6) {
				doubtTimer++;
				if (doubtTimer > 300) { doubtTimer = 0; doubtHeavy = !doubtHeavy; }
				if (doubtHeavy) projectile.velocity *= 0.995f;
				else projectile.velocity *= 1.002f;
			}
			if (CurseId == 8) {
				pendulumTimer++;
				if (pendulumTimer > 420) { pendulumTimer = 0; pendulumOutward *= -1; }
			}
			if (CurseId == 2) {
				// Holding flight drains the orbit reserve; rebounds refund it.
				if (deployTicks % 30 == 0)
					orbitReserve = System.Math.Max(0, orbitReserve - 15);
				if (orbitReserve <= 0) {
					orbitReserve = 600;
					if (isOwner) {
						projectile.velocity = (owner.Center - projectile.Center).SafeNormalize(Vector2.Zero) * 6f;
						DustBurst(projectile.Center, DustID.Smoke, 10);
					}
				}
			}
			if (CurseId == 9) {
				// The orbit loosens the longer it flies: widen gradually, and
				// correct it with a sharp rebound to collapse the drift.
				emptyWiden++;
				YoyoStaticBoosts.BoostRange(projectile.type, -System.Math.Min(48f, emptyWiden / 20f));
				YoyoStaticBoosts.BoostTopSpeed(projectile.type, 1f - System.Math.Min(0.1f, emptyWiden / 12000f));
			}
			if (borrowedTimer > 0) {
				borrowedTimer--;
				if (borrowedTimer == 0) borrowedDebt = 0;
			}
			// Rebound Debt recovery: a burned bank drags briefly, then clears.
			if (debtTimer > 0) {
				debtTimer--;
				projectile.velocity *= 0.999f;
				if (debtTimer == 0) debtBank = 0f;
			}
			if (brittleBroken) {
				brittle += 0.5f;
				if (brittle > 300f) { brittleBroken = false; brittle = 0f; }
			}
			// High curses.
			if (CurseId == 101) {
				shearTimer++;
				if (shearTimer > 600) { shearTimer = 0; shearState = !shearState; }
				if (shearState) {
					projectile.tileCollide = false;
					projectile.velocity = projectile.velocity.RotatedBy(0.01f);
				}
				else projectile.tileCollide = true;
			}
			if (CurseId == 102) {
				bankruptcy += projectile.velocity.Length() * 0.04f;
				if (bankruptcy > 1500f && isOwner) {
					// Must discharge via a qualifying hit soon or lose control.
					bankruptcyTimer = 300;
					bankruptcy = 0f;
					projectile.velocity *= 0.6f;
					DustBurst(projectile.Center, DustID.Smoke, 14);
				}
				if (bankruptcyTimer > 0) {
					bankruptcyTimer--;
					projectile.velocity *= 0.99f;
				}
			}
			if (CurseId == 103) {
				if (overCurseActive) {
					overCurseTimer--;
					overpressureCurse += 0.6f;
					YoyoStaticBoosts.BoostTopSpeed(projectile.type, 1.3f);
					if (overpressureCurse > 130f && isOwner) {
						projectile.velocity *= 0.6f;
						overCurseActive = false;
						overpressureCurse = 0f;
					}
					else if (overCurseTimer <= 0) {
						overCurseActive = false;
						overpressureCurse *= 0.4f;
					}
				}
			}
			if (CurseId == 104 && gyreNpc >= 0) {
				gyreTimer--;
				NPC n = gyreNpc < Main.maxNPCs ? Main.npc[gyreNpc] : null;
				float maxRange = ProjectileID.Sets.YoyosMaximumRange[projectile.type];
				if (n == null || !n.active || gyreTimer <= 0 || n.type != gyreNpcType
					|| Vector2.Distance(owner.Center, n.Center) > maxRange * 1.1f) {
					gyreNpc = -1;
					if (isOwner) {
						// Destabilized: heavy recovery.
						projectile.velocity *= 0.5f;
					}
				}
				else {
					gyreTight = System.Math.Min(100, gyreTight + 1);
					if (deployTicks % 15 == 0) MarkFx(gyreNpc, Color.OrangeRed);
					float radius = System.Math.Max(30f, 120f - gyreTight);
					float step = 0.07f + projectile.velocity.Length() * 0.004f;
					Vector2 toProj = (projectile.Center - n.Center).SafeNormalize(Vector2.UnitX);
					projectile.Center = Vector2.Lerp(projectile.Center, n.Center + toProj.RotatedBy(step) * radius, 0.4f);
				}
			}
			if (CurseId == 105) {
				if (crownTimer > 0) {
					crownTimer--;
					if (crownTimer == 0) crownReso = 0f;
				}
			}
			if (CurseId == 106) {
				soulsTimer++;
				if (soulsTimer > 360) { soulsTimer = 0; soulsPhase ^= 1; }
			}
			if (CurseId == 107) {
				if (inheritTimer > 0) {
					inheritTimer--;
					projectile.velocity = projectile.velocity.RotatedBy(inheritSkew * 0.002f);
					if (inheritTimer == 0) inheritSkew = 0f;
				}
			}
			if (CurseId == 108 && !throneHeld && throneTimer > 0) {
				throneTimer--;
				projectile.velocity *= 0.99f;
			}
			if (CurseId == 109) {
				if (contractTimer > 0) {
					contractTimer--;
					if (contractTimer == 0) {
						contractBank = 0f;
						contractPrimed = false;
					}
				}
			}
		}

		partial void OnReboundCurse(Projectile projectile, Player owner, bool isOwner)
		{
			if (CurseId == 0) {
				recoil++;
				if (recoil >= 4) {
					recoil = 0;
					if (isOwner) projectile.velocity *= 0.7f;
				}
			}
			else if (CurseId != 0 && recoil > 0) recoil = System.Math.Max(0, recoil - 1);
			if (CurseId == 1) {
				float turn = System.Math.Abs(MathHelper.WrapAngle(projectile.velocity.ToRotation() - lastVel.ToRotation()));
				if (turn > 1.4f) {
					brittleBroken = true;
					brittle = 0f;
					if (isOwner) projectile.velocity *= 0.75f;
				}
			}
			if (CurseId == 2) {
				// Redirecting refunds orbit reserve.
				orbitReserve = System.Math.Min(600, orbitReserve + 60);
			}
			if (CurseId == 9) {
				// A sharp rebound collapses the widening drift.
				emptyWiden = 0;
			}
			if (CurseId == 100) {
				float ang = System.Math.Abs(MathHelper.WrapAngle(projectile.velocity.ToRotation() - lastVel.ToRotation()));
				if (ang > 0.5f) {
					debtBank = System.Math.Min(1000f, debtBank + 120f);
					debtTimer = 300;
				}
				else {
					// Invalid rebound burns the bank into recovery.
					if (debtBank > 200f && isOwner) projectile.velocity *= 0.6f;
					debtBank = 0f;
					debtTimer = 240;
				}
			}
			if (CurseId == 107) {
				inheritSkew += 12f;
				inheritTimer = 300;
			}
		}

		partial void ModifyHitCurse(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (CurseId < 0)
				return;
			if (CurseId == 0 && recoil >= 3) modifiers.SourceDamage *= 0.7f;
			if (CurseId == 1 && brittleBroken) modifiers.SourceDamage *= 0.65f;
			if (CurseId == 1 && !brittleBroken) modifiers.SourceDamage *= 1.15f;
			if (CurseId == 3 && borrowedDebt <= 0) modifiers.SourceDamage *= 1.35f;
			else if (CurseId == 3 && borrowedDebt > 0) modifiers.SourceDamage *= 0.75f;
			if (CurseId == 6) modifiers.SourceDamage *= doubtHeavy ? 1.25f : 0.85f;
			if (CurseId == 7) {
				float ang = projectile.velocity.ToRotation();
				if (fractureAngle >= 0 && System.Math.Abs(MathHelper.WrapAngle(ang - fractureAngle)) < 0.3f && fractureSpam >= 3)
					modifiers.SourceDamage *= 0.7f;
			}
			if (CurseId == 8) {
				bool outward = Vector2.Dot(projectile.velocity.SafeNormalize(Vector2.Zero), (projectile.Center - Main.player[projectile.owner].Center).SafeNormalize(Vector2.Zero)) > 0;
				if ((pendulumOutward == 1) != outward) modifiers.SourceDamage *= 0.75f;
			}
			if (CurseId == 100 && debtBank > 300f) modifiers.SourceDamage *= 1f + debtBank / 800f;
			if (CurseId == 100 && debtTimer > 0) modifiers.SourceDamage *= 0.85f;
			if (CurseId == 102 && bankruptcyTimer > 0) modifiers.SourceDamage *= 0.7f;
			if (CurseId == 103 && overCurseActive) modifiers.SourceDamage *= 1.5f;
			if (CurseId == 104 && gyreTight > 40) modifiers.SourceDamage *= 1.35f;
			if (CurseId == 105 && crownReso > 50f) modifiers.SourceDamage *= 1.5f;
			if (CurseId == 106) modifiers.SourceDamage *= soulsPhase == 0 ? 1.3f : 0.7f;
			if (CurseId == 108 && !throneHeld) modifiers.SourceDamage *= 0.6f;
			if (CurseId == 109 && contractPrimed) modifiers.SourceDamage *= 2.2f;
		}

		partial void OnHitCurse(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (CurseId < 0)
				return;
			if (CurseId == 3 && borrowedDebt <= 0 && projectile.owner == Main.myPlayer) {
				borrowedDebt = 1;
				borrowedTimer = 300;
			}
			if (CurseId == 7) {
				float ang = projectile.velocity.ToRotation();
				if (fractureAngle < 0) fractureAngle = (int)ang;
				if (System.Math.Abs(MathHelper.WrapAngle(ang - fractureAngle)) < 0.3f) fractureSpam++;
				else { fractureAngle = (int)ang; fractureSpam = 1; }
			}
			if (CurseId == 102 && bankruptcyTimer > 0) {
				bankruptcyTimer = 0;
				if (projectile.owner == Main.myPlayer) DustBurst(projectile.Center, DustID.GoldCoin, 12);
			}
			if (CurseId == 103 && !overCurseActive && overpressureCurse < 60f) {
				overCurseActive = true;
				overCurseTimer = 240;
			}
			if (CurseId == 104 && gyreNpc < 0) {
				gyreNpc = target.whoAmI;
				gyreNpcType = target.type;
				gyreTimer = 420;
				gyreTight = 0;
			}
			if (CurseId == 105) {
				crownReso = System.Math.Min(100f, crownReso + 25f);
				crownTimer = 180;
			}
			if (CurseId == 108) {
				// Strict conduct: vary angles. Same-angle spam breaks the throne.
				float ang = projectile.velocity.ToRotation();
				if (fractureAngle >= 0 && System.Math.Abs(MathHelper.WrapAngle(ang - fractureAngle)) < 0.25f) {
					if (throneHeld)
						CombatText.NewText(new Rectangle((int)projectile.Center.X - 50, (int)projectile.Center.Y - 40, 100, 30), Color.Red, "THRONE FALLS!");
					throneHeld = false;
					throneTimer = 600;
				}
				else throneHeld = true;
				fractureAngle = (int)ang;
			}
			if (CurseId == 109) {
				if (!contractPrimed) {
					contractBank += damageDone * 0.5f;
					if (contractBank > 800f) {
						contractPrimed = true;
						contractTimer = 300;
						CombatText.NewText(new Rectangle((int)projectile.Center.X - 50, (int)projectile.Center.Y - 40, 100, 30), Color.Gold, "TITHE TAKEN!");
					}
				}
				else {
					contractPrimed = false;
					contractBank = 0f;
					contractTimer = 0;
					CombatText.NewText(new Rectangle((int)projectile.Center.X - 50, (int)projectile.Center.Y - 40, 100, 30), Color.OrangeRed, "CONTRACT!");
					if (projectile.owner == Main.myPlayer) {
						// Severe frailty after the contract strike.
						borrowedDebt = 1;
						borrowedTimer = 420;
					}
				}
			}
		}
	}
}
