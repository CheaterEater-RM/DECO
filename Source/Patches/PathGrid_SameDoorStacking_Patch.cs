using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private const int CacheRefreshTicks = 60;

        private static readonly ConditionalWeakTable<Map, SameDoorStackingCellCache> Caches = new();

        private static void Postfix(ref int __result, IntVec3 c, IntVec3 prevCell, PathGrid __instance)
        {
            if (__result >= PathGrid.ImpassableCost || !prevCell.IsValid)
                return;

            var map = __instance.map;
            if (map == null)
                return;

            var cache = Caches.GetValue(map, static currentMap => new SameDoorStackingCellCache(currentMap));
            if (!cache.ContainsBoth(c, prevCell))
                return;

            if (c.GetEdifice(map) is Building_Door door
                && ReferenceEquals(prevCell.GetEdifice(map), door)
                && !door.FreePassage)
            {
                __result -= DoorStackingCost;
            }
        }

        private sealed class SameDoorStackingCellCache
        {
            private readonly Map map;
            private readonly HashSet<IntVec3> cells = new();
            private int nextRefreshTick = -1;

            public SameDoorStackingCellCache(Map map)
            {
                this.map = map;
            }

            public bool ContainsBoth(IntVec3 first, IntVec3 second)
            {
                RefreshIfNeeded();
                return cells.Contains(first) && cells.Contains(second);
            }

            private void RefreshIfNeeded()
            {
                var ticks = Find.TickManager?.TicksGame ?? 0;
                if (ticks < nextRefreshTick)
                    return;

                nextRefreshTick = ticks + CacheRefreshTicks;
                cells.Clear();
                AddDoorCells(map.listerBuildings.allBuildingsColonist);
                AddDoorCells(map.listerBuildings.allBuildingsNonColonist);
            }

            private void AddDoorCells(List<Building> buildings)
            {
                for (var i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i] is not Building_Door door
                        || !door.Spawned
                        || door.FreePassage
                        || door.def.Size.x * door.def.Size.z <= 1)
                    {
                        continue;
                    }

                    foreach (var cell in GenAdj.CellsOccupiedBy(door))
                        cells.Add(cell);
                }
            }
        }
    }
}
