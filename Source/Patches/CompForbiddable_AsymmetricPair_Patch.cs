using HarmonyLib;
using RimWorld;

namespace DoorsExpanded
{
    // Mirrors vanilla forbid/unforbid across opted-in asymmetric door pairs. This stays
    // narrow: the helper rejects non-DECO doors, mixed defs, chains, and unbracketed layouts.
    [HarmonyPatch(typeof(CompForbiddable), nameof(CompForbiddable.Forbidden), MethodType.Setter)]
    public static class CompForbiddable_AsymmetricPair_Patch
    {
        public static void Postfix(CompForbiddable __instance, bool value)
        {
            Building_DoorExpanded.NotifyForbiddenChanged(__instance.parent, value);
        }
    }
}
