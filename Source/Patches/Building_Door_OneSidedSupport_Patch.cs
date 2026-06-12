using HarmonyLib;
using RimWorld;

namespace DoorsExpanded
{
    [HarmonyPatch(typeof(Building_Door), "StuckOpen", MethodType.Getter)]
    public static class Building_Door_OneSidedSupport_Patch
    {
        public static bool Prefix(Building_Door __instance, ref bool __result)
        {
            if (__instance is not Building_DoorExpanded door)
            {
                return true;
            }

            __result = door.StuckOpenBySupport();
            return false;
        }
    }
}
