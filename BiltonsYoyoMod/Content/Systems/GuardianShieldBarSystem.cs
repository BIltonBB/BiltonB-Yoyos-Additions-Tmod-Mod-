// Fix #11: this custom floating HP bar has been removed. The shield's hitbox now sits at its
// actual on-screen orb position (see GuardianOrbitShield.cs), so Terraria's own built-in
// per-NPC life bar displays correctly on its own — the same bar Star Guardian already used —
// and this extra draw call is no longer needed. Delete this file from your project; it's kept
// here (empty) only so the old path doesn't dangle if something still references it.
