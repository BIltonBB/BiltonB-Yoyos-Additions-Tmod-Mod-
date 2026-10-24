using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // G: damage history made visible. Marks are draw-only, per-NPC (no sync,
    // no damage, no AI): the family that last hit leaves its signature on the
    // victim's body for a few seconds; the killing family decides the death
    // flourish (G4). Each client records the hits it witnesses.
    public class VictimMarkSystem : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private const int MarkLife = 240;     // 4s signature
        private const int MaxMarks = 3;

        private readonly List<(int Family, int Timer)> marks = new List<(int, int)>();
        private Vector2 lastHitDir;
        private int lastHitFamily = -1;
        private int wedgeTimer;

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (cfg != null && !cfg.Visuals.VictimMarks)
                return;
            if (!YoyoStringHookSystem.TryGetFamilyIndex(projectile.type, out int family))
                return;

            // G3: freshest hit direction (±4 tick chevron lives in the draw).
            Vector2 vel = projectile.oldVelocity.LengthSquared() > 0.01f ? projectile.oldVelocity : projectile.velocity;
            lastHitDir = vel.SafeNormalize(Vector2.UnitX);
            lastHitFamily = family;
            wedgeTimer = 4;

            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i].Family == family)
                {
                    marks[i] = (family, MarkLife);   // same family refreshes
                    return;
                }
            }
            if (marks.Count >= MaxMarks)
                marks.RemoveAt(0);
            marks.Add((family, MarkLife));
        }

        public override void PostAI(NPC npc)
        {
            if (wedgeTimer > 0)
                wedgeTimer--;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                m.Timer -= 1;
                if (m.Timer <= 0)
                    marks.RemoveAt(i);
                else
                    marks[i] = m;
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (marks.Count == 0 && wedgeTimer <= 0)
                return;
            if (npc.IsABestiaryIconDummy)
                return;
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (cfg != null && !cfg.Visuals.VictimMarks)
                return;

            foreach (var m in marks)
            {
                float a = m.Timer / (float)MarkLife;
                float fade = MiniYoyoFx.Ease(MiniYoyoFx.FxEase.Smooth, MathHelper.Clamp(a * 1.6f, 0f, 1f));
                DrawMark(npc, m.Family, fade);
            }
            if (wedgeTimer > 0 && lastHitFamily >= 0)
            {
                // G3: one chevron pressed into the wound point, then gone.
                float w = wedgeTimer / 4f;
                Vector2 at = npc.Center + lastHitDir * (npc.width * 0.22f);
                Vector2 side = new Vector2(-lastHitDir.Y, lastHitDir.X);
                Color c = YoyoStringHookSystem.FamilyColor(lastHitFamily) * (0.8f * w);
                MiniYoyoFx.DrawLine(at - side * 6f - lastHitDir * 2f - Main.screenPosition, at + lastHitDir * 4f - Main.screenPosition, 2f, c);
                MiniYoyoFx.DrawLine(at + side * 6f - lastHitDir * 2f - Main.screenPosition, at + lastHitDir * 4f - Main.screenPosition, 2f, c);
            }
        }

        // One small procedural glyph per family, deterministic placement on the
        // body so repeated hits do not jitter the mark around.
        private static void DrawMark(NPC npc, int family, float fade)
        {
            Color c = YoyoStringHookSystem.FamilyColor(family);
            float s = MathHelper.Clamp(npc.width / 40f, 0.6f, 2.2f) * (0.55f + 0.45f * fade);
            Vector2 basePos = npc.Center + (family * 2.399f + 1.1f).ToRotationVector2() * MathHelper.Min(npc.width, npc.height) * 0.22f;
            Vector2 screen = basePos - Main.screenPosition;
            float rot = family * 1.7f;

            switch (family)
            {
                case 0: // DarkMage: rune score
                    MiniYoyoFx.DrawLine(screen - new Vector2(5f, -4f) * s, screen + new Vector2(4f, 5f) * s, 1.6f, c * fade);
                    MiniYoyoFx.DrawLine(screen + new Vector2(4f, -5f) * s, screen - new Vector2(3f, 4f) * s, 1.2f, c * (0.8f * fade));
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(2f, -2f) * s, 5f, c, 0.5f * fade);
                    break;
                case 1: // TorchGod: soot crust + embers
                    MiniYoyoFx.DrawSolidEllipse(basePos, 10f * s, 7f * s, rot, new Color(24, 18, 14), 0.4f * fade);
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(3f, -1f) * s, 3.5f, new Color(255, 150, 60), 0.7f * fade);
                    break;
                case 2: // Dutchman: powder smear + faint smoke puff
                    MiniYoyoFx.DrawLine(screen - new Vector2(7f, 1f) * s, screen + new Vector2(6f, -2f) * s, 3.4f * s, new Color(210, 225, 230) * (0.35f * fade));
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(5f, -3f) * s, 6f, new Color(150, 200, 190), 0.35f * fade);
                    break;
                case 3: // Ogre: grease flare
                    MiniYoyoFx.DrawGlow(basePos, 12f * s, new Color(120, 220, 120), 0.4f * fade);
                    MiniYoyoFx.DrawRing(basePos, 7f * s, 1.5f, c * (0.6f * fade), 14);
                    break;
                case 4: // Dreadnautilus: bleeding drag + drip
                    MiniYoyoFx.DrawLine(screen, screen + new Vector2(0f, 7f * s), 2.4f, new Color(200, 30, 45) * (0.7f * fade));
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(0f, 8f * s), 4f, new Color(230, 90, 100), 0.6f * fade);
                    break;
                case 5: // MourningWood: char patch
                    MiniYoyoFx.DrawSolidEllipse(basePos, 11f * s, 8f * s, rot, new Color(30, 20, 12), 0.42f * fade);
                    MiniYoyoFx.DrawGlow(basePos, 4f, new Color(255, 160, 70), 0.5f * fade);
                    break;
                case 6: // Santa: gift-wrap cross + bow pip
                    MiniYoyoFx.DrawLine(screen - new Vector2(6f, 0f) * s, screen + new Vector2(6f, 0f) * s, 1.6f, new Color(255, 90, 90) * (0.6f * fade));
                    MiniYoyoFx.DrawLine(screen - new Vector2(0f, 5f) * s, screen + new Vector2(0f, 5f) * s, 1.6f, new Color(255, 90, 90) * (0.6f * fade));
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(0f, -6f) * s, 4f, new Color(255, 230, 190), 0.6f * fade);
                    break;
                case 7: // Pumpking: lantern scorch with two eye pips
                    MiniYoyoFx.DrawRing(basePos, 8f * s, 1.6f, new Color(255, 170, 80) * (0.6f * fade), 16);
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(-3f, -1f) * s, 3f, new Color(255, 220, 140), 0.8f * fade);
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(3f, -1f) * s, 3f, new Color(255, 220, 140), 0.8f * fade);
                    break;
                case 8: // Everscream: accumulating snow cap
                    for (int i = 0; i < 3; i++)
                        MiniYoyoFx.DrawGlow(basePos + new Vector2((i - 1) * 4f, -4f - (i % 2) * 2f) * s, 4.5f, new Color(235, 245, 255), 0.55f * fade);
                    break;
                case 9: // IceQueen: frozen glint
                    MiniYoyoFx.DrawStarSparkle(basePos, new Color(190, 235, 255), 0.7f * s, rot, 0.8f * fade);
                    break;
                case 10: // Martian: static arcs
                    MiniYoyoFx.DrawLine(screen - new Vector2(6f, 2f) * s, screen + new Vector2(-1f, -3f) * s, 1.4f, new Color(150, 255, 190) * (0.8f * fade));
                    MiniYoyoFx.DrawLine(screen + new Vector2(-1f, -3f) * s, screen + new Vector2(5f, 1f) * s, 1.4f, new Color(150, 255, 190) * (0.8f * fade));
                    break;
                case 11: // Betsy: igniting char
                    MiniYoyoFx.DrawRing(basePos, 7f * s, 1.4f, new Color(255, 140, 60) * (0.55f * fade), 14);
                    MiniYoyoFx.DrawGlow(basePos + new Vector2(2f, -3f) * s, 4f, new Color(255, 120, 40), 0.6f * fade);
                    break;
                default: // Developer: star stamp
                    MiniYoyoFx.DrawStarSparkle(basePos, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 0.8f * s, rot, 0.85f * fade);
                    break;
            }
        }

        // G4: the killing family's mark decides the death flourish.
        internal static MiniYoyoFx.FadeShape KillFlourishFor(int family)
        {
            switch (family)
            {
                case 1: return MiniYoyoFx.FadeShape.Pop;       // TorchGod: ignites
                case 2: return MiniYoyoFx.FadeShape.Collapse;  // Dutchman: dragged under
                case 3: return MiniYoyoFx.FadeShape.Shatter;   // Ogre: crushed flat
                case 4: return MiniYoyoFx.FadeShape.Dissolve;  // Dreadnautilus: bleeds out
                case 8: return MiniYoyoFx.FadeShape.Shatter;   // Everscream: splintered
                case 9: return MiniYoyoFx.FadeShape.Shatter;   // IceQueen: frozen shatter
                case 10: return MiniYoyoFx.FadeShape.Pop;      // Martian: disintegrate
                case 11: return MiniYoyoFx.FadeShape.Pop;      // Betsy: incinerated
                default: return MiniYoyoFx.FadeShape.Pop;
            }
        }
    }
}
