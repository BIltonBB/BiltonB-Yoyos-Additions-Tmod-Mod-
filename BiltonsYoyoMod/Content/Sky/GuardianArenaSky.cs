using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using BiltonsYoyoMod.Content.Projectiles;

namespace BiltonsYoyoMod.Content.Sky
{
    // Arena backdrop: opaque black fill drawn in the background/sky pass, so it
    // is guaranteed behind tiles, walls, NPCs (dummies included), liquids and
    // players, while covering the Terraria background inside the arena rect.
    // Managed by GuardianArenaSkySystem (activate/deactivate); the projectile
    // itself no longer draws any fill.
    public class GuardianArenaSky : CustomSky
    {
        public const string Key = "BiltonsYoyoMod:GuardianArena";

        public override void Update(GameTime gameTime) { }

        public override void Activate(Vector2 position, params object[] args) => Opacity = 1f;

        public override void Deactivate(params object[] args) => Opacity = 0f;

        public override void Reset() { }

        public override bool IsActive() => GuardianArenaBox.AnyLiveBox() || SkyFresh();

        public override bool IsVisible() => GuardianArenaBox.AnyLiveBox() || SkyFresh();

        // Grace period: a box killed seconds ago still counts, so rapid
        // kill/restore cycles can't make the sky flicker or starve its fade.
        private static bool SkyFresh() => Main.GameUpdateCount - GuardianArenaBox.LastBoxSeenTick < 180;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (GuardianArenaBox.TryGetArenaScreenRect(out Rectangle rect))
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, rect, Color.Black);
        }
    }
}
