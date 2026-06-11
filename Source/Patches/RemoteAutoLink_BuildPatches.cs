using HarmonyLib;
using RimWorld;
using Verse;

namespace DoorsExpanded
{
    // Scoped build-lifecycle hooks for the door-side "build remote" command. These run only
    // when vanilla places a blueprint or converts one into a frame; no draw/path hot path.
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.PlaceBlueprintForBuild))]
    internal static class GenConstruct_PlaceBlueprintForBuild_RemoteAutoLink_Patch
    {
        private static void Postfix(BuildableDef sourceDef, Map map, Blueprint_Build __result)
        {
            if (sourceDef is not ThingDef controlDef || !MapComponent_RemoteAutoLink.IsRemoteControlDef(controlDef))
                return;

            map.GetComponent<MapComponent_RemoteAutoLink>()?.RegisterBlueprintFromArm(__result, controlDef);
        }
    }

    [HarmonyPatch(typeof(Blueprint), nameof(Blueprint.TryReplaceWithSolidThing))]
    internal static class Blueprint_TryReplaceWithSolidThing_RemoteAutoLink_Patch
    {
        private static void Postfix(Blueprint __instance, bool __result, Thing createdThing)
        {
            if (!__result || __instance is not Blueprint_Build blueprint || createdThing is not Frame frame)
                return;

            frame.Map.GetComponent<MapComponent_RemoteAutoLink>()?.TransferPendingThing(blueprint, frame);
        }
    }

    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    internal static class Frame_CompleteConstruction_RemoteAutoLink_Patch
    {
        private static void Prefix(Frame __instance)
        {
            __instance.Map.GetComponent<MapComponent_RemoteAutoLink>()?.NotifyFrameCompleting(__instance);
        }
    }
}
