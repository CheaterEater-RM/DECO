using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // Multi-tile door with a custom draw. Vanilla Building_MultiTileDoor draws each leaf at
    // half width and splits two copies from center, which only suits art authored as a
    // half-leaf. DECO's reused art is authored at FULL door width (a single leaf on the left
    // half of a full-width canvas) plus optional static frame layers, so we draw it ourselves.
    // All door behaviour (state machine, pathing, regions, power, forbidding, temperature) is
    // inherited from vanilla.
    //
    // The draw math (Draw / DrawStandardParams / DrawStretchParams / DrawDoubleSwingParams /
    // DrawFrameParams) is ported near-verbatim from Doors Expanded (jecrell, MIT) — it is
    // settled, allocation-light geometry and is reused as an asset, not reinvented.
    public class Building_DoorExpanded : Building_MultiTileDoor
    {
        private CompProperties_DoorExpanded propsInt;

        // Tolerant of a missing comp: save-swapping between mods can briefly pair this class
        // with a def that lacks it (e.g. a def edited mid-save). Fall back to defaults
        // (Standard two-leaf slide) rather than throwing every draw frame.
        private static readonly CompProperties_DoorExpanded DefaultProps = new();

        public CompProperties_DoorExpanded Props =>
            propsInt ??= def.GetCompProperties<CompProperties_DoorExpanded>() ?? DefaultProps;

        // FreePassage doors are permanently open (vanilla hook).
        protected override bool AlwaysOpen => Props.doorType == DoorType.FreePassage;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            // Non-1x1 rotations change the footprint, which base.SpawnSetup caches in
            // various ways, so the rotation must be corrected before calling base.
            Rotation = DoorRotationAt(def, Props, Position, Rotation, map);
            base.SpawnSetup(map, respawningAfterLoad);
        }

        // Ported from the original: auto-rotate non-rotatable 1x1 and stretch doors to face
        // adjacent walls, and force south to north for art with no authored south facing.
        public static Rot4 DoorRotationAt(ThingDef def, CompProperties_DoorExpanded props,
            IntVec3 loc, Rot4 rot, Map map)
        {
            if (!def.rotatable)
            {
                var size = def.Size;
                if ((size.x == 1 && size.z == 1)
                    || props.doorType is DoorType.Stretch or DoorType.StretchVertical)
                {
                    rot = DoorUtility.DoorRotationAt(loc, map, preferFences: false);
                }
            }
            if (!props.rotatesSouth && rot == Rot4.South)
                rot = Rot4.North;
            return rot;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // We deliberately do NOT call base.DrawAt: Building_MultiTileDoor.DrawAt would
            // redraw the vanilla half-width movers (the squished-art bug we exist to fix).
            // We replicate the original's DrawAt: leaves (optionally async on leaf 0), then
            // the optional frame overlays, then comp post-draw. DECO doors don't use
            // doorSupportGraphic/doorTopGraphic, so the Building_SupportedDoor tail is skipped.
            var props = Props;
            var rotation = DoorRotationAt(def, props, Position, Rotation, Map);
            Rotation = rotation;
            drawLoc.y = AltitudeLayer.DoorMoveable.AltitudeFor();
            var openPct = OpenPct;

            for (var i = 0; i < 2; i++)
            {
                var flipped = i != 0;
                var graphic = (!flipped && props.doorAsync != null)
                    ? props.doorAsync.GraphicColoredFor(this)
                    : Graphic;
                Draw(def, props, graphic, drawLoc, rotation, openPct, flipped);
                graphic.ShadowGraphic?.DrawWorker(drawLoc, rotation, def, this, 0f);
                if (props.singleDoor)
                    break;
            }

            if (props.doorFrame != null)
            {
                DrawFrameParams(def, props, props.doorFrame, props.doorFrameOffset,
                    drawLoc, rotation, false, out var fMesh, out var fMatrix);
                Graphics.DrawMesh(fMesh, fMatrix, props.doorFrame.GraphicColoredFor(this).MatAt(rotation, null), 0);

                if (props.doorFrameSplit != null)
                {
                    DrawFrameParams(def, props, props.doorFrameSplit, props.doorFrameSplitOffset,
                        drawLoc, rotation, true, out fMesh, out fMatrix);
                    Graphics.DrawMesh(fMesh, fMatrix, props.doorFrameSplit.GraphicColoredFor(this).MatAt(rotation, null), 0);
                }
            }

            Comps_PostDraw();
        }

        // Mirrors the original Draw(): pick per-type params, then draw one leaf.
        private void Draw(ThingDef def, CompProperties_DoorExpanded props, Graphic graphic,
            Vector3 drawPos, Rot4 rotation, float openPct, bool flipped)
        {
            Mesh mesh;
            Quaternion rotQuat;
            Vector3 offsetVector, scaleVector;
            switch (props.doorType)
            {
                // Stretch and StretchVertical differ only in stretchOpenSize's default.
                case DoorType.Stretch:
                case DoorType.StretchVertical:
                    DrawStretchParams(def, props, rotation, openPct, flipped,
                        out mesh, out rotQuat, out offsetVector, out scaleVector);
                    break;
                case DoorType.DoubleSwing:
                    DrawDoubleSwingParams(def, props, drawPos, rotation, openPct, flipped,
                        out mesh, out rotQuat, out offsetVector, out scaleVector);
                    break;
                default:
                    DrawStandardParams(def, props, rotation, openPct, flipped,
                        out mesh, out rotQuat, out offsetVector, out scaleVector);
                    break;
            }
            var graphicVector = drawPos + offsetVector;
            var matrix = Matrix4x4.TRS(graphicVector, rotQuat, scaleVector);
            Graphics.DrawMesh(mesh, matrix, graphic.MatAt(rotation, null), 0);
        }

        // Ported DrawStretchParams: a single sheet that shrinks along one axis as it opens,
        // offset so the anchored edge appears not to move (curtains, garage doors).
        private static void DrawStretchParams(ThingDef def, CompProperties_DoorExpanded props,
            Rot4 rotation, float openPct, bool flipped, out Mesh mesh, out Quaternion rotQuat,
            out Vector3 offsetVector, out Vector3 scaleVector)
        {
            var drawSize = def.graphicData.drawSize;
            var closeSize = props.stretchCloseSize;
            var openSize = props.stretchOpenSize;
            var offset = props.stretchOffset.Value;

            var verticalRotation = rotation.IsHorizontal;
            var persMod = (verticalRotation && props.fixedPerspective) ? 2f : 1f;

            offsetVector = new Vector3(offset.x * openPct * persMod, 0f, offset.y * openPct * persMod);

            var scaleX = Mathf.LerpUnclamped(openSize.x, closeSize.x, 1 - openPct) / closeSize.x * drawSize.x * persMod;
            var scaleZ = Mathf.LerpUnclamped(openSize.y, closeSize.y, 1 - openPct) / closeSize.y * drawSize.y * persMod;
            scaleVector = new Vector3(scaleX, 1f, scaleZ);

            // South-facing stretch animation should have same vertical direction as north-facing one.
            if (rotation == Rot4.South)
                offsetVector.z = -offsetVector.z;

            if (!flipped)
            {
                mesh = MeshPool.plane10;
            }
            else
            {
                offsetVector.x = -offsetVector.x;
                mesh = MeshPool.plane10Flip;
            }

            rotQuat = rotation.AsQuat;
            offsetVector = rotQuat * offsetVector;
        }

        private static void DrawStandardParams(ThingDef def, CompProperties_DoorExpanded props,
            Rot4 rotation, float openPct, bool flipped, out Mesh mesh, out Quaternion rotQuat,
            out Vector3 offsetVector, out Vector3 scaleVector)
        {
            var verticalRotation = rotation.IsHorizontal;
            if (!flipped)
            {
                offsetVector = new Vector3(-1f, 0f, 0f);
                mesh = MeshPool.plane10;
            }
            else
            {
                offsetVector = new Vector3(1f, 0f, 0f);
                mesh = MeshPool.plane10Flip;
            }

            rotQuat = rotation.AsQuat;
            offsetVector = rotQuat * offsetVector;

            var offsetMod = (CompProperties_DoorExpanded.VisualDoorOffsetStart
                + props.doorOpenMultiplier * openPct) * def.Size.x;
            offsetVector *= offsetMod;

            var drawSize = def.graphicData.drawSize;
            var persMod = (verticalRotation && props.fixedPerspective) ? 2f : 1f;
            scaleVector = new Vector3(drawSize.x * persMod, 1f, drawSize.y * persMod);
        }

        private static void DrawDoubleSwingParams(ThingDef def, CompProperties_DoorExpanded props,
            Vector3 drawPos, Rot4 rotation, float openPct, bool flipped, out Mesh mesh, out Quaternion rotQuat,
            out Vector3 offsetVector, out Vector3 scaleVector)
        {
            var verticalRotation = rotation.IsHorizontal;
            if (!flipped)
            {
                offsetVector = new Vector3(-1f, 0f, 0f);
                if (verticalRotation)
                    offsetVector = new Vector3(1.4f, 0f, 1.1f);
                mesh = MeshPool.plane10;
            }
            else
            {
                offsetVector = new Vector3(1f, 0f, 0f);
                if (verticalRotation)
                    offsetVector = new Vector3(-1.4f, 0f, 1.1f);
                mesh = MeshPool.plane10Flip;
            }

            if (verticalRotation)
                rotQuat = Quaternion.AngleAxis(rotation.AsAngle + (openPct * (flipped ? 90f : -90f)), Vector3.up);
            else
                rotQuat = rotation.AsQuat;
            offsetVector = rotQuat * offsetVector;

            var offsetMod = (CompProperties_DoorExpanded.VisualDoorOffsetStart
                + props.doorOpenMultiplier * openPct) * def.Size.x;
            offsetVector *= offsetMod;

            if (verticalRotation)
            {
                if ((!flipped && rotation == Rot4.East) || (flipped && rotation == Rot4.West))
                    offsetVector.y = Mathf.Max(0f, AltitudeLayer.BuildingOnTop.AltitudeFor() - drawPos.y);
            }

            var drawSize = def.graphicData.drawSize;
            var persMod = (verticalRotation && props.fixedPerspective) ? 2f : 1f;
            scaleVector = new Vector3(drawSize.x * persMod, 1f, drawSize.y * persMod);
        }

        private static void DrawFrameParams(ThingDef def, CompProperties_DoorExpanded props,
            GraphicData frameData, Vector3 frameOffset, Vector3 drawPos, Rot4 rotation, bool split,
            out Mesh mesh, out Matrix4x4 matrix)
        {
            var verticalRotation = rotation.IsHorizontal;
            var offsetVector = new Vector3(-1f, 0f, 0f);
            mesh = MeshPool.plane10;

            if (props.doorFrameSplit != null && rotation == Rot4.West)
                offsetVector.x = 1f;

            var rotQuat = rotation.AsQuat;
            offsetVector = rotQuat * offsetVector;

            var offsetMod = (CompProperties_DoorExpanded.VisualDoorOffsetStart
                + props.doorOpenMultiplier * 1f) * def.Size.x;
            offsetVector *= offsetMod;

            var drawSize = frameData.drawSize;
            var persMod = (verticalRotation && props.fixedPerspective) ? 2f : 1f;
            var scaleVector = new Vector3(drawSize.x * persMod, 1f, drawSize.y * persMod);

            var graphicVector = drawPos;
            graphicVector.y = AltitudeLayer.Blueprint.AltitudeFor();
            if (rotation == Rot4.North || rotation == Rot4.South)
                graphicVector.y = AltitudeLayer.PawnState.AltitudeFor();
            if (!verticalRotation)
                graphicVector.x += offsetMod;
            if (rotation == Rot4.East)
            {
                graphicVector.z -= offsetMod;
                if (split)
                    graphicVector.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            }
            else if (rotation == Rot4.West)
            {
                graphicVector.z += offsetMod;
                if (split)
                    graphicVector.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
            }
            graphicVector += offsetVector;

            if (props.doorFrameSplit != null && rotation == Rot4.West)
            {
                rotQuat = Quaternion.Euler(0f, 270f, 0f);
                graphicVector.z -= 2.7f;
                mesh = MeshPool.plane10Flip;
            }
            graphicVector += frameOffset;

            matrix = Matrix4x4.TRS(graphicVector, rotQuat, scaleVector);
        }
    }
}
