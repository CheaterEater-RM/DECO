using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // One-time save-migration cleanup. The old Doors Expanded made curtains minifiable;
    // DECO's curtains inherit vanilla DoorBase and are NOT minifiable, so a minified
    // curtain carried over from an old save is invalid state. Its inner Building_Door
    // ticks while held inside the MinifiedThing and NREs in
    // GenTemperature.EqualizeTemperaturesThroughBuilding (no map context).
    //
    // We scrub ONLY our three curtain defNames by exact match, refund the build cost
    // (respecting resourcesFractionWhenDeconstructed), then destroy the orphan. This is
    // deliberately scoped to DECO defs so it can never touch another mod's minified things.
    public class MinifiedCurtainScrubber : MapComponent
    {
        private static readonly HashSet<string> CurtainDefNames = new()
        {
            "HeronCurtainTribal",
            "HeronCurtainTribalDouble",
            "HeronCurtainTribalTriple",
        };

        private bool scrubbed;

        public MinifiedCurtainScrubber(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (scrubbed)
                return;

            scrubbed = true;
            ScrubOrphanedMinifiedCurtains();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scrubbed, "DEx_minifiedCurtainsScrubbed", false);
        }

        private void ScrubOrphanedMinifiedCurtains()
        {
            // ToList: we mutate the spawned-things list while iterating (Destroy).
            var orphans = map.listerThings.AllThings
                .OfType<MinifiedThing>()
                .Where(m => m.InnerThing?.def != null && CurtainDefNames.Contains(m.InnerThing.def.defName))
                .ToList();

            foreach (var orphan in orphans)
            {
                var inner = orphan.InnerThing;
                var cell = orphan.Position;
                RefundResources(inner, cell);
                Log.Warning($"[DECO] Removed orphaned minified curtain '{inner.def.defName}' at {cell} " +
                    "(carried over from old Doors Expanded; curtains are no longer minifiable). Build cost refunded.");
                orphan.Destroy(DestroyMode.Vanish);
            }
        }

        private void RefundResources(Thing inner, IntVec3 cell)
        {
            if (!cell.InBounds(map))
                cell = CellFinder.RandomCell(map);

            var fraction = Mathf.Clamp01(inner.def.resourcesFractionWhenDeconstructed);
            if (fraction <= 0f)
                return;

            foreach (var cost in inner.CostListAdjusted())
            {
                var count = GenMath.RoundRandom(cost.count * fraction);
                while (count > 0)
                {
                    var stack = Mathf.Min(count, cost.thingDef.stackLimit);
                    var refund = ThingMaker.MakeThing(cost.thingDef);
                    refund.stackCount = stack;
                    GenPlace.TryPlaceThing(refund, cell, map, ThingPlaceMode.Near);
                    count -= stack;
                }
            }
        }
    }
}
