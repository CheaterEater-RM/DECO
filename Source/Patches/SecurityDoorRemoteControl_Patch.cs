using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DoorsExpanded
{
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.GetGizmos))]
    internal static class Building_Door_GetGizmos_VanillaDoorRemoteControl_Patch
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Door __instance)
        {
            if (!RemoteDoorUtility.IsVanillaRemoteControllableDoor(__instance))
            {
                foreach (var gizmo in __result)
                    yield return gizmo;
                yield break;
            }

            var insertedRemoteGizmos = false;
            var holdOpenLabel = "CommandToggleDoorHoldOpen".Translate().ToString();

            foreach (var gizmo in __result)
            {
                if (gizmo is Command_Toggle command && command.defaultLabel == holdOpenLabel)
                {
                    if (RemoteDoorUtility.SecuredRemotely(__instance))
                        gizmo.Disable("PH_RemoteDoorSecuredRemotely".Translate());

                    yield return gizmo;
                    foreach (var remoteGizmo in RemoteDoorUtility.RemoteGizmos(__instance))
                        yield return remoteGizmo;
                    insertedRemoteGizmos = true;
                }
                else
                {
                    yield return gizmo;
                }
            }

            if (!insertedRemoteGizmos)
            {
                foreach (var remoteGizmo in RemoteDoorUtility.RemoteGizmos(__instance))
                    yield return remoteGizmo;
            }
        }
    }

    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    internal static class Building_Door_PawnCanOpen_RemoteVanillaDoor_Patch
    {
        private static void Postfix(Building_Door __instance, ref bool __result)
        {
            if (__result && RemoteDoorUtility.RemoteForcesClosed(__instance))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawExtraSelectionOverlays))]
    internal static class Thing_DrawExtraSelectionOverlays_RemoteVanillaDoor_Patch
    {
        private static void Postfix(Thing __instance)
        {
            if (__instance is Building_Door door && RemoteDoorUtility.IsVanillaRemoteControllableDoor(door))
                RemoteDoorUtility.DrawLinkOverlay(door);
        }
    }
}
