using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DoorsExpanded
{
    // Anomaly containment: count multi-tile doors that are more than one cell deep.
    //
    // Vanilla StatWorker_ContainmentStrength.CalculateDoorStats discovers a room's doors by
    // walking each room region's portal links to the door's one-cell Portal regions, then
    // confirming that portal region links the room to some *other* (non-room, non-same-door)
    // region. That holds for a door only one cell deep (1x1, 2x1): its front portal links the
    // room directly to the outside region. But DECO's 3x2 blast door (PH_DoorThickBlastDoor) is
    // two cells deep — its front-row portal cells link the room only to the back-row portal
    // cells of the *same* door, never directly to an outside region. So the confirmation test
    // never passes and the whole door is silently dropped from the containment calculation,
    // leaving the room counted as if that wall had no door (and never registering a breach when
    // it is held open). 1x1 and 2x1 doors are unaffected.
    //
    // Fix: a postfix that independently rebuilds the bordering-door set from the room's border
    // cells (matching vanilla's once-per-door HashSet semantics) and, if it finds a door vanilla
    // missed, recomputes doorCount / avgDoorHp / doorBreach from the complete set. When the sets
    // agree (the common all-shallow-door case) vanilla's result is left untouched, so this never
    // alters behaviour for non-deep doors and stays out of other mods' way.
    [HarmonyPatch(typeof(StatWorker_ContainmentStrength), "CalculateDoorStats")]
    public static class ContainmentStrength_DeepDoor_Patch
    {
        public static void Postfix(Room room, ref int doorCount, ref float avgDoorHp, ref bool doorBreach)
        {
            if (room == null)
                return;

            var map = room.Map;
            if (map == null)
                return;

            // Distinct doors that actually border the room (a door cell cardinally adjacent to a
            // room cell). HashSet de-dupes multi-cell doors to one entry, as vanilla does.
            var borderingDoors = new HashSet<Building_Door>();
            foreach (var borderCell in room.BorderCellsCached)
            {
                if (!borderCell.InBounds(map))
                    continue;
                if (borderCell.GetEdifice(map) is Building_Door door && BordersRoom(door, room, map))
                    borderingDoors.Add(door);
            }

            // Only intervene when vanilla under-counted (it found fewer doors than truly border
            // the room — the deep-door miss). If counts match, leave vanilla's result alone.
            if (borderingDoors.Count <= doorCount)
                return;

            doorCount = borderingDoors.Count;
            avgDoorHp = 0f;
            doorBreach = false;
            foreach (var door in borderingDoors)
            {
                if (door.ContainmentBreached)
                {
                    doorBreach = true;
                    avgDoorHp = 0f;
                }
                else if (!doorBreach)
                {
                    avgDoorHp += door.HitPoints;
                }
            }
        }

        private static bool BordersRoom(Building_Door door, Room room, Map map)
        {
            foreach (var cell in door.OccupiedRect())
            {
                foreach (var direction in GenAdj.CardinalDirections)
                {
                    var adjacent = cell + direction;
                    if (adjacent.InBounds(map) && adjacent.GetRoom(map) == room)
                        return true;
                }
            }

            return false;
        }
    }
}
