using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;
using BiltonsYoyoMod.Settings;

namespace BiltonsYoyoMod.Players
{
	// Dimensional Quantum Spool warp: press the Quantum Warp keybind while your
	// phased yoyo is deployed to teleport to it.
	public class QuantumWarpPlayer : ModPlayer
	{
		public int WarpCooldown;

		public override void ResetEffects()
		{
		}

		public override void PostUpdate()
		{
			if (WarpCooldown > 0)
				WarpCooldown--;
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (ModalInput.GameplayHotkeysBlocked(Player))
				return;
			if (YoyoWarpKeybind.QuantumWarpKey == null || !YoyoWarpKeybind.QuantumWarpKey.JustPressed)
				return;
			if (WarpCooldown > 0 || Player.dead || !Player.active)
				return;
			Projectile yoyo = FindSpooledYoyo();
			if (yoyo == null) {
				Main.NewText("No phased yoyo deployed.", 150, 200, 255);
				SoundEngine.PlaySound(SoundID.MenuTick, Player.Center);
				return;
			}
			Vector2 dest = yoyo.Center - new Vector2(Player.width / 2f, Player.height / 2f);
			if (Collision.SolidCollision(dest, Player.width, Player.height)) {
				Main.NewText("No room to materialize there!", 255, 150, 150);
				SoundEngine.PlaySound(SoundID.MenuTick, Player.Center);
				return;
			}
			for (int i = 0; i < 20; i++) {
				Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.PurpleTorch, 0f, 0f, 120, default, 1.1f);
				dust.noGravity = true;
			}
			Player.Teleport(dest, 0);
			for (int i = 0; i < 20; i++) {
				Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.PurpleTorch, 0f, 0f, 120, default, 1.1f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.Item8, Player.Center);
			WarpCooldown = 900; // 15 seconds.
		}

		private Projectile FindSpooledYoyo()
		{
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj == null || !proj.active || proj.owner != Player.whoAmI || proj.aiStyle != ProjAIStyleID.Yoyo)
					continue;
				if (proj.TryGetGlobalProjectile<YoyoEnhancementGlobalProjectile>(out var enh) && enh.ModId == 9)
					return proj;
			}
			return null;
		}
	}
}
