using HarmonyLib;
using RimWorld;
using Verse;

namespace DoorsExpanded
{
    // Prisoner door locks for DECO jail and blast doors.
    //
    // Logic adapted from "Prisoners Don't Have Keys" by Mlie (MIT License, Copyright 2020 Mlie):
    //   https://github.com/emipa606/PrisonersDontHaveKeys
    // The original postfixes Building_Door.PawnCanOpen; we do the same but gate on DECO's own
    // jail/blast defs and our per-door-type settings, and never touch other doors.
    // DECO is stricter than the original mod's OwnDoor setting: an escaping prisoner can only
    // open a DECO jail/blast door that actually borders their current prison cell.
    //
    // Postfix (not a cancelling prefix) per project rule: only narrows __result to false in tight
    // conditions. Patching the base Building_Door.PawnCanOpen covers the jail door
    // (Building_DoorExpanded) and blast doors (Building_DoorRemote), since Building_DoorRemote's
    // override calls base.PawnCanOpen and Building_DoorExpanded doesn't override it.
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    public static class PawnCanOpen_PrisonerLock_Patch
    {
        public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
        {
            // Vanilla already says no, or nothing to lock — leave it.
            if (!__result || p == null)
                return;

            var settings = DecoMod.Settings;
            if (settings == null)
                return;

            var def = __instance.def;
            bool isJail = def == HeronDefOf.PH_DoorJail;
            bool isBlast = def == HeronDefOf.PH_DoorBlastSingle
                           || def == HeronDefOf.PH_DoorBlastDoor
                           || def == HeronDefOf.PH_DoorThickBlastDoor;

            if (!isJail && !isBlast)
                return;

            bool blocksPrisoners = isJail ? settings.jailBlocksPrisoners : settings.blastBlocksPrisoners;
            if (!blocksPrisoners)
                return;

            if (p.IsPrisonerOfColony)
            {
                // Only an active prison break can open a door at all, and then only a
                // door bordering the prisoner's own cell if the player allows it.
                if (!PrisonBreakUtility.IsPrisonBreaking(p))
                {
                    __result = false;
                    return;
                }

                __result = settings.escapingPawnsOpenOwnDoor && BordersPawnPrisonCell(__instance, p);
            }
        }

        private static bool BordersPawnPrisonCell(Building_Door door, Pawn p)
        {
            if (!door.Spawned || !p.Spawned || door.Map != p.Map)
                return false;

            var prisonRoom = p.GetRoom();
            if (prisonRoom == null || !prisonRoom.IsPrisonCell)
                return false;

            var map = door.Map;
            foreach (var doorCell in GenAdj.CellsOccupiedBy(door))
            {
                foreach (var direction in GenAdj.CardinalDirections)
                {
                    var adjacentCell = doorCell + direction;
                    if (adjacentCell.InBounds(map) && adjacentCell.GetRoom(map) == prisonRoom)
                        return true;
                }
            }

            return false;
        }
    }
}
