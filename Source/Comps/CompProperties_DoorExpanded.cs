using UnityEngine;
using Verse;

namespace DECO
{
    // Animation type for a DECO door. Explicit integer values; never reorder after
    // first release (saved/referenced by ordinal). New values are appended only.
    public enum DoorAnimationType
    {
        Standard = 0,       // slide aside
        Stretch = 1,        // single sheet shrinks horizontally (curtains)
        DoubleSwing = 2,    // leaves swing on hinges when facing east/west (gates)
        StretchVertical = 3 // single sheet shrinks vertically (garage-style remote doors)
    }

    // Data-only config carried by DECO door defs. Does NOT rewrite parentDef.thingClass
    // (DECO defs set thingClass explicitly). The trivial CompDoorExpanded exists only so
    // the props can hang off a ThingDef; all reads go through the def's comp properties.
    // Field names and defaults mirror the original Doors Expanded (jecrell, MIT) comp.
    public class CompProperties_DoorExpanded : CompProperties
    {
        public const float DefaultStretchPercent = 0.2f;

        public DoorAnimationType doorType = DoorAnimationType.Standard;

        // If true, only one leaf is drawn (single full-width panel).
        public bool singleDoor = false;

        // Doubles the draw scale for horizontal rotations (used by gates whose art is
        // authored taller than the footprint).
        public bool fixedPerspective = false;

        // If false, a south rotation is forced to north (art has no authored south facing).
        public bool rotatesSouth = true;

        // How far the leaf travels when fully open, in tiles of door width.
        public float doorOpenMultiplier = VisualDoorOffsetEnd;

        // Optional second-leaf graphic, drawn slightly offset from the first leaf for a
        // layered look (blast doors).
        public GraphicData doorAsync = null;

        // Optional static decorative frame layers drawn around/behind the moving leaves
        // (blast doors: hazard pillars + housing). doorFrameSplit is the mirrored half.
        public GraphicData doorFrame = null;
        public Vector3 doorFrameOffset = Vector3.zero;
        public GraphicData doorFrameSplit = null;
        public Vector3 doorFrameSplitOffset = Vector3.zero;

        // Stretch/StretchVertical only. The size of the closed door (typically the "actual"
        // size ignoring transparent sections of the texture), the size to shrink to when
        // open, and the offset from the closed center to the open center (defaulted so the
        // anchored edge appears not to move). Defaults computed in ResolveReferences.
        public Vector2 stretchCloseSize;
        public Vector2 stretchOpenSize;
        public Vector2? stretchOffset;

        // The original's draw offset constants (Building_DoorExpanded).
        public const float VisualDoorOffsetStart = 0f;
        public const float VisualDoorOffsetEnd = 0.45f;

        public CompProperties_DoorExpanded()
        {
            compClass = typeof(CompDoorExpanded);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);

            // Stretch property defaults, mirroring the original comp: closed size defaults
            // to the authored drawSize; open size shrinks one axis to 20%; the offset keeps
            // the non-shrinking edge visually anchored.
            if (parentDef.graphicData is { } graphicData
                && doorType is DoorAnimationType.Stretch or DoorAnimationType.StretchVertical)
            {
                if (stretchCloseSize == Vector2.zero)
                    stretchCloseSize = graphicData.drawSize;
                if (stretchOpenSize == Vector2.zero)
                {
                    stretchOpenSize = doorType is DoorAnimationType.Stretch
                        ? new Vector2(stretchCloseSize.x * DefaultStretchPercent, stretchCloseSize.y)
                        : new Vector2(stretchCloseSize.x, stretchCloseSize.y * DefaultStretchPercent);
                }
                stretchOffset ??= new Vector2(
                    (stretchOpenSize.x - stretchCloseSize.x) / 2,
                    (stretchCloseSize.y - stretchOpenSize.y) / 2);
            }
        }
    }

    public class CompDoorExpanded : ThingComp
    {
    }
}
