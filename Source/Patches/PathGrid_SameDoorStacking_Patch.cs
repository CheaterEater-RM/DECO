using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DoorsExpanded
{
    // Vanilla's anti-door-stacking penalty (PathGrid.CalculatedCostAt) adds +45 move cost
    // when a pawn steps from one closed door cell into another, so chained doors can't form
    // a free airlock. A door 2+ cells deep in the travel direction (the 3x2 blast door)
    // trips it against ITSELF — both cells resolve to the same Building_Door — giving a
    // permanent slow walk through the doorway. This postfix removes the penalty only when
    // both cells belong to the same door instance; two genuinely separate adjacent doors
    // keep the vanilla penalty.
    //
    // The subtraction is exact, not approximate: the only call path with a valid prevCell
    // (Pawn_PathFollower.CostToMoveIntoCell) always passes perceivedStatic=false, so the
    // fire costs never co-occur, and snow/sand contributions are max()'d in before the +45.
    // The in-loop impassable/fence exits return the 10000 sentinel before the +45 is added,
    // which the first guard excludes.
    [HarmonyPatch(typeof(PathGrid), nameof(PathGrid.CalculatedCostAt))]
    internal static class PathGrid_SameDoorStacking_Patch
    {
        private const int DoorStackingCost = 45; // mirrors the literal in CalculatedCostAt

        private static void Postfix(ref int __result, IntVec3 c, IntVec3 prevCell, PathGrid __instance)
        {
            if (__result >= PathGrid.ImpassableCost || !prevCell.IsValid)
                return;

            var map = __instance.map;
            if (c.GetEdifice(map) is Building_Door door
                && ReferenceEquals(prevCell.GetEdifice(map), door)
                && !door.FreePassage)
            {
                __result -= DoorStackingCost;
            }
        }
    }
}
