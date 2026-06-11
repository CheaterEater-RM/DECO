using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    public class PlaceWorker_DoorExpanded : PlaceWorker_MultiCellDoor
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc,
            Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (checkingDef is ThingDef thingDef
                && Building_DoorExpanded.UsesOneSidedWallSupport(thingDef)
                && !Building_DoorExpanded.HasOneSidedWallSupport(thingDef, loc, rot, map,
                    includeUnbuilt: true))
            {
                return "DECO_CurtainRequiresWallSupport".Translate();
            }

            return AcceptanceReport.WasAccepted;
        }

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol,
            Thing thing = null)
        {
            if (!Building_DoorExpanded.UsesOneSidedWallSupport(def))
                base.DrawGhost(def, center, rot, ghostCol, thing);
        }

        public override void PostPlace(Map map, BuildableDef def, IntVec3 loc, Rot4 rot)
        {
            if (def is ThingDef thingDef && Building_DoorExpanded.UsesOneSidedWallSupport(thingDef))
                return;

            base.PostPlace(map, def, loc, rot);
        }
    }
}
