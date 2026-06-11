using System;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // Combat Extended compatibility for shoot-through jail "bars".
    //
    // CE replaces vanilla cover/LOS: a building's fillPercent is treated as cover HEIGHT
    // (shots fly OVER low cover), so the vanilla fillPercent=0.25 jail door would just be a
    // low wall you shoot over — not bars. CE's "intercept at any height" behavior is its
    // PLANT path, hardcoded to Plant. The supported hook to get the same effect for a
    // building is CombatExtended.Compatibility.BlockerRegistry.RegisterCheckForCollisionCallback,
    // which CE invokes per cell in ProjectileCE.CheckCellForCollision; returning true forces a
    // collision in that cell regardless of projectile height.
    //
    // We mirror CE's own plant formula (ProjectileCE.cs) so bars are directional and
    // distance-scaled — point-blank "gun through the bars" barely blocks, longer shots scale
    // up toward ~25%. All reflection: CE is a soft dependency and is never referenced at build
    // time, so this no-ops cleanly when CE is absent.
    [StaticConstructorOnStartup]
    internal static class CombatExtendedCompat
    {
        private const float BarsBlockChance = 0.25f;

        // Cached reflection handles into the loaded CE assembly.
        private static FieldInfo fiOriginIV3;
        private static FieldInfo fiAccuracyFactor;
        private static FieldInfo fiLastPos;
        private static FieldInfo fiDef;          // ProjectileCE.def (ThingDef)

        static CombatExtendedCompat()
        {
            try
            {
                Install();
            }
            catch (Exception e)
            {
                Log.Warning($"[DECO] Combat Extended compatibility failed to initialize: {e}");
            }
        }

        private static void Install()
        {
            if (!ModsConfig.IsActive("CETeam.CombatExtended") && !ModLister.HasActiveModWithName("Combat Extended"))
            {
                return;
            }

            var registryType = GenTypes.GetTypeInAnyAssembly("CombatExtended.Compatibility.BlockerRegistry");
            var projectileType = GenTypes.GetTypeInAnyAssembly("CombatExtended.ProjectileCE");
            if (registryType == null || projectileType == null)
            {
                Log.Message("[DECO] Combat Extended not detected (BlockerRegistry/ProjectileCE missing); skipping CE bars compat.");
                return;
            }

            var register = registryType.GetMethod(
                "RegisterCheckForCollisionCallback",
                BindingFlags.Public | BindingFlags.Static);
            if (register == null)
            {
                Log.Warning("[DECO] CE BlockerRegistry.RegisterCheckForCollisionCallback not found; skipping CE bars compat.");
                return;
            }

            // Func<ProjectileCE, IntVec3, Thing, bool>
            var funcType = typeof(Func<,,,>).MakeGenericType(projectileType, typeof(IntVec3), typeof(Thing), typeof(bool));

            // Relaxed delegate binding: our method's first parameter is `object`, a base of
            // ProjectileCE, so CreateDelegate accepts it without a compile-time CE reference.
            var callbackMethod = typeof(CombatExtendedCompat).GetMethod(
                nameof(CheckCellForCollisionCallback),
                BindingFlags.NonPublic | BindingFlags.Static);
            var callback = Delegate.CreateDelegate(funcType, callbackMethod);

            // Cache the projectile field handles we read each intercept roll.
            fiOriginIV3 = projectileType.GetField("OriginIV3", BindingFlags.Public | BindingFlags.Instance);
            fiAccuracyFactor = projectileType.GetField("AccuracyFactor", BindingFlags.Public | BindingFlags.Instance);
            fiLastPos = projectileType.GetField("LastPos", BindingFlags.Public | BindingFlags.Instance);
            fiDef = projectileType.GetField("def", BindingFlags.Public | BindingFlags.Instance)
                    ?? projectileType.BaseType?.GetField("def", BindingFlags.Public | BindingFlags.Instance);

            if (fiOriginIV3 == null || fiAccuracyFactor == null || fiLastPos == null || fiDef == null)
            {
                Log.Warning("[DECO] CE ProjectileCE fields not found as expected; skipping CE bars compat.");
                return;
            }

            register.Invoke(null, new object[] { callback });
            Log.Message("[DECO] Combat Extended detected; registered jail-bars shoot-through compat.");
        }

        // Signature must be assignable to Func<ProjectileCE, IntVec3, Thing, bool>; `object`
        // is a base of ProjectileCE so relaxed delegate binding works.
        private static bool CheckCellForCollisionCallback(object projectile, IntVec3 cell, Thing launcher)
        {
            var map = (launcher as Thing)?.Map;
            if (map == null)
            {
                return false;
            }

            var door = cell.GetEdifice(map) as Building_Door;
            if (door == null || door.def != HeronDefOf.PH_DoorJail || door.Open)
            {
                return false;
            }

            // "Gun through the bars": if the door cell is the projectile's previous cell, the
            // shooter is point-blank against the bars — no interception (matches CE plants).
            var lastPos = (Vector3)fiLastPos.GetValue(projectile);
            if (door.Position == lastPos.ToIntVec3())
            {
                return false;
            }

            // chance = 0.25 * accuracyFactor, where accuracyFactor scales with distance from
            // the shot origin exactly as CE does for plants (ProjectileCE.TryCollideWith).
            float accuracyFactor;
            var def = (ThingDef)fiDef.GetValue(projectile);
            // alwaysFreeIntercept lives on the vanilla ProjectileProperties base.
            if (def?.projectile?.alwaysFreeIntercept ?? false)
            {
                accuracyFactor = 1f;
            }
            else
            {
                var origin = (IntVec3)fiOriginIV3.GetValue(projectile);
                float baseAcc = (float)fiAccuracyFactor.GetValue(projectile);
                accuracyFactor = (door.Position - origin).LengthHorizontal / 40f * baseAcc;
            }

            float chance = BarsBlockChance * accuracyFactor;
            return Rand.ChanceSeeded(chance, door.HashOffsetTicks());
        }
    }
}
