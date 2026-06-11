using HarmonyLib;
using RimWorld;

namespace DoorsExpanded
{
    [HarmonyPatch(typeof(Building_Door), "StuckOpen", MethodType.Getter)]
    public static class Building_Door_OneSidedSupport_Patch
    {
        public static void Postfix(Building_Door __instance, ref bool __result)
        {
            if (!__result
                || __instance is not Building_DoorExpanded door
                || !door.Props.oneSidedWallSupport
                || !door.Spawned)
            {
                return;
            }

            if (Building_DoorExpanded.HasOneSidedWallSupport(door.def, door.Position,
                door.Rotation, door.Map, includeUnbuilt: false))
            {
                __result = false;
            }
        }
    }
}
