using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Players
{
	// Rift Snap trigger: uses the dedicated keybind (Settings/RiftSnapKeybind).
	// Validates active anchor, distance, cooldown and yoyo ownership before
	// redirecting eligible yoyos. Owner-only, no permanent AI override.
	public class RiftSnapPlayer : ModPlayer
	{
		public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet)
		{
			if (ModalInput.GameplayHotkeysBlocked(Player))
				return;
			if (global::BiltonsYoyoMod.Settings.RiftSnapKeybind.RiftSnapKey == null || !global::BiltonsYoyoMod.Settings.RiftSnapKeybind.RiftSnapKey.JustPressed)
				return;
			if (Player.dead || !Player.active)
				return;
			var anchor = Player.GetModPlayer<Content.Placeables.RiftAnchorPlayer>();
			if (!anchor.HasActive) {
				if (Main.myPlayer == Player.whoAmI)
					Main.NewText("No active Rift Anchor. Right-click an anchor to select it.", 180, 160, 255);
				return;
			}
			if (anchor.SnapCooldown > 0) {
				if (Main.myPlayer == Player.whoAmI)
					Main.NewText($"Rift Snap recharging ({anchor.SnapCooldown / 60}s).", 180, 160, 255);
				return;
			}
			if (!Content.Placeables.RiftAnchorHelpers.IsUsable(Player, anchor.ActiveTile, out string why)) {
				if (Main.myPlayer == Player.whoAmI)
					Main.NewText($"Rift Anchor unusable: {why}", 255, 150, 150);
				return;
			}
			bool anyYoyo = false;
			Vector2 target = Content.Placeables.RiftAnchorHelpers.AnchorCenter(anchor.ActiveTile);
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.owner != Player.whoAmI || p.aiStyle != ProjAIStyleID.Yoyo)
					continue;
				string ns = p.ModProjectile?.GetType().Namespace ?? string.Empty;
				if (ns.Contains("TownNPC") || p.hostile)
					continue;
				if (p.TryGetGlobalProjectile<Content.Placeables.RiftSnapGlobalProjectile>(out var snap))
					snap.StartSnap(target);
				p.netUpdate = true;
				anyYoyo = true;
			}
			if (!anyYoyo) {
				if (Main.myPlayer == Player.whoAmI)
					Main.NewText("No deployed yoyo to snap.", 180, 160, 255);
				return;
			}
			anchor.SnapCooldown = (int)(Content.Placeables.RiftAnchorPlayer.CooldownTicks * Content.Placeables.StationNetwork.RiftCooldownMult(anchor.ActiveTile));
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, Player.Center);
			for (int i = 0; i < 10; i++) {
				Dust d = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.MagicMirror, 0f, -2f, 100, default, 1f);
				d.noGravity = true;
			}
		}
	}
}
