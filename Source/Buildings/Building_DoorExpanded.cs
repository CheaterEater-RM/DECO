using System.Collections.Generic;
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
        private Graphic doorAsyncGraphicInt;
        private Graphic doorFrameGraphicInt;
        private Graphic doorFrameSplitGraphicInt;
        private static bool suppressPairDoorOpen;
        private static bool suppressPairForbid;

        // Tolerant of a missing comp: save-swapping between mods can briefly pair this class
        // with a def that lacks it (e.g. a def edited mid-save). Fall back to defaults
        // (Standard two-leaf slide) rather than throwing every draw frame.
        private static readonly CompProperties_DoorExpanded DefaultProps = new();

        public CompProperties_DoorExpanded Props =>
            propsInt ??= def.GetCompProperties<CompProperties_DoorExpanded>() ?? DefaultProps;

        // FreePassage doors are permanently open (vanilla hook).
        protected override bool AlwaysOpen => Props.doorType == DoorType.FreePassage;

        private static bool SyncPairedAsymmetricDoors =>
            DecoMod.Settings?.syncPairedAsymmetricDoors ?? true;

        private Graphic DoorAsyncGraphic =>
            doorAsyncGraphicInt ??= Props.doorAsync?.GraphicColoredFor(this);

        private Graphic DoorFrameGraphic =>
            doorFrameGraphicInt ??= Props.doorFrame?.GraphicColoredFor(this);

        private Graphic DoorFrameSplitGraphic =>
            doorFrameSplitGraphicInt ??= Props.doorFrameSplit?.GraphicColoredFor(this);

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            // Non-1x1 rotations change the footprint, which base.SpawnSetup caches in
            // various ways, so the rotation must be corrected before calling base.
            Rotation = DoorRotationAt(def, Props, Position, Rotation, map);
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void Notify_ColorChanged()
        {
            doorAsyncGraphicInt = null;
            doorFrameGraphicInt = null;
            doorFrameSplitGraphicInt = null;
            base.Notify_ColorChanged();
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

        private static Rot4 CanonicalParallelRotation(Rot4 rot)
        {
            if (rot == Rot4.South)
                return Rot4.North;
            if (rot == Rot4.West)
                return Rot4.East;
            return rot;
        }

        internal static bool UsesOneSidedWallSupport(ThingDef def) =>
            def?.GetCompProperties<CompProperties_DoorExpanded>()?.oneSidedWallSupport == true;

        internal static bool HasOneSidedWallSupport(ThingDef def, IntVec3 loc, Rot4 rot,
            Map map, bool includeUnbuilt)
        {
            var props = def?.GetCompProperties<CompProperties_DoorExpanded>();
            if (def == null || props == null || !props.oneSidedWallSupport || map == null)
                return false;

            var rect = GenAdj.OccupiedRect(loc, rot, def.Size);
            GetLocalSideDirections(CanonicalParallelRotation(rot), out var negativeSide, out var positiveSide);
            return HasWallSide(rect, map, negativeSide, includeUnbuilt)
                   || HasWallSide(rect, map, positiveSide, includeUnbuilt);
        }

        internal bool StuckOpenBySupport()
        {
            var props = Props;
            if (props.doorType == DoorType.FreePassage)
                return true;
            if (!Spawned || def.size == IntVec2.One)
                return false;
            if (props.oneSidedWallSupport)
                return !HasOneSidedWallSupport(def, Position, Rotation, Map, includeUnbuilt: false);

            return !HasFullWallSupport(def, Position, Rotation, Map, includeUnbuilt: false);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // We deliberately do NOT call base.DrawAt: Building_MultiTileDoor.DrawAt would
            // redraw the vanilla half-width movers (the squished-art bug we exist to fix).
            // We replicate the original's DrawAt: leaves (optionally async on leaf 0), then
            // the optional frame overlays, then comp post-draw. DECO doors don't use
            // doorSupportGraphic/doorTopGraphic, so the Building_SupportedDoor tail is skipped.
            var props = Props;
            var rotation = Rotation;
            if (!def.rotatable || !props.rotatesSouth)
            {
                rotation = DoorRotationAt(def, props, Position, rotation, Map);
                Rotation = rotation;
            }
            if (props.oneSidedWallSupport)
                rotation = CanonicalParallelRotation(rotation);
            drawLoc.y = AltitudeLayer.DoorMoveable.AltitudeFor();
            var openPct = OpenPct;
            var baseGraphic = Graphic;
            var asymmetricFlipped = props.asymmetric
                && props.singleDoor
                && ShouldDrawAsymmetricFlipped(rotation);

            for (var i = 0; i < 2; i++)
            {
                var flipped = props.singleDoor ? asymmetricFlipped : i != 0;
                var graphic = (!flipped && props.doorAsync != null)
                    ? DoorAsyncGraphic
                    : baseGraphic;
                Draw(def, props, graphic, drawLoc, rotation, openPct, flipped);
                graphic.ShadowGraphic?.DrawWorker(drawLoc, rotation, def, this, 0f);
                if (props.singleDoor)
                    break;
            }

            if (props.doorFrame != null)
            {
                DrawFrameParams(def, props, props.doorFrame, props.doorFrameOffset,
                    drawLoc, rotation, false, out var fMesh, out var fMatrix);
                Graphics.DrawMesh(fMesh, fMatrix, DoorFrameGraphic.MatAt(rotation, null), 0);

                if (props.doorFrameSplit != null)
                {
                    DrawFrameParams(def, props, props.doorFrameSplit, props.doorFrameSplitOffset,
                        drawLoc, rotation, true, out fMesh, out fMatrix);
                    Graphics.DrawMesh(fMesh, fMatrix, DoorFrameSplitGraphic.MatAt(rotation, null), 0);
                }
            }

            Comps_PostDraw();
        }

        protected override void DoorOpen(int ticksToClose = 110)
        {
            base.DoorOpen(ticksToClose);
            if (suppressPairDoorOpen || !SyncPairedAsymmetricDoors)
                return;

            if (TryGetAdjacentAsymmetricPair(this, out var partner))
            {
                try
                {
                    suppressPairDoorOpen = true;
                    partner.DoorOpen(ticksToClose);
                }
                finally
                {
                    suppressPairDoorOpen = false;
                }
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (ShouldReconcileAsymmetricPair())
                ReconcileAsymmetricPair();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            Building_DoorExpanded partner = null;
            var replaceHoldOpen = SyncPairedAsymmetricDoors
                && TryGetAdjacentAsymmetricPair(this, out partner);
            var holdOpenLabel = "CommandToggleDoorHoldOpen".Translate().ToString();

            foreach (var gizmo in base.GetGizmos())
            {
                if (replaceHoldOpen
                    && gizmo is Command_Toggle command
                    && command.defaultLabel == holdOpenLabel)
                {
                    yield return PairedHoldOpenGizmo(partner);
                }
                else
                {
                    yield return gizmo;
                }
            }
        }

        internal static void NotifyForbiddenChanged(Thing thing, bool forbidden)
        {
            if (suppressPairForbid
                || !SyncPairedAsymmetricDoors
                || thing is not Building_DoorExpanded door
                || !TryGetAdjacentAsymmetricPair(door, out var partner))
            {
                return;
            }

            try
            {
                suppressPairForbid = true;
                partner.SetForbidden(forbidden, warnOnFail: false);
            }
            finally
            {
                suppressPairForbid = false;
            }
        }

        private Command_Toggle PairedHoldOpenGizmo(Building_DoorExpanded partner)
        {
            return new Command_Toggle
            {
                defaultLabel = "CommandToggleDoorHoldOpen".Translate(),
                defaultDesc = "CommandToggleDoorHoldOpenDesc".Translate(),
                hotKey = KeyBindingDefOf.Misc3,
                icon = TexCommand.HoldOpen,
                isActive = () => holdOpenInt || partner.holdOpenInt,
                toggleAction = () =>
                {
                    var next = !(holdOpenInt || partner.holdOpenInt);
                    holdOpenInt = next;
                    partner.holdOpenInt = next;
                }
            };
        }

        private bool ShouldReconcileAsymmetricPair()
        {
            var props = Props;
            return SyncPairedAsymmetricDoors
                   && props.asymmetric
                   && props.syncAdjacentAsymmetricPair
                   && (Open || holdOpenInt || ticksUntilClose > 0);
        }

        private void ReconcileAsymmetricPair()
        {
            if (!SyncPairedAsymmetricDoors || !TryGetAdjacentAsymmetricPair(this, out var partner))
                return;

            if (holdOpenInt || partner.holdOpenInt)
            {
                holdOpenInt = true;
                partner.holdOpenInt = true;
                if (Open || partner.Open)
                    KeepPairOpen(partner);
                return;
            }

            if (Open && BlockedOpenMomentary)
                KeepDoorOpen(partner, Mathf.Max(110, ticksUntilClose));
            if (partner.Open && partner.BlockedOpenMomentary)
                KeepDoorOpen(this, Mathf.Max(110, partner.ticksUntilClose));

            var forbidden = this.TryGetComp<CompForbiddable>()?.Forbidden ?? false;
            var partnerForbidden = partner.TryGetComp<CompForbiddable>()?.Forbidden ?? false;
            if (forbidden != partnerForbidden && (forbidden || partnerForbidden))
            {
                try
                {
                    suppressPairForbid = true;
                    this.SetForbidden(true, warnOnFail: false);
                    partner.SetForbidden(true, warnOnFail: false);
                }
                finally
                {
                    suppressPairForbid = false;
                }
            }
        }

        private void KeepPairOpen(Building_DoorExpanded partner)
        {
            KeepDoorOpen(this, Mathf.Max(110, ticksUntilClose));
            KeepDoorOpen(partner, Mathf.Max(110, partner.ticksUntilClose));
        }

        private static void KeepDoorOpen(Building_DoorExpanded door, int ticksToClose)
        {
            try
            {
                suppressPairDoorOpen = true;
                door.DoorOpen(ticksToClose);
            }
            finally
            {
                suppressPairDoorOpen = false;
            }
        }

        private bool ShouldDrawAsymmetricFlipped(Rot4 rotation)
        {
            var props = Props;
            if (!props.asymmetric || !props.singleDoor || !Spawned)
                return false;

            GetLocalSideDirections(rotation, out _, out var positiveSide);
            if (!TryGetUniqueWallSide(this, rotation, out var wallSide))
                return false;

            return wallSide == positiveSide;
        }

        internal static bool TryGetAdjacentAsymmetricPair(Building_DoorExpanded door,
            out Building_DoorExpanded partner)
        {
            partner = null;
            var props = door?.Props;
            if (door == null
                || !door.Spawned
                || props == null
                || !props.asymmetric
                || !props.syncAdjacentAsymmetricPair)
            {
                return false;
            }

            var rotation = DoorRotationAt(door.def, props, door.Position, door.Rotation, door.Map);
            if (!TryGetUniqueWallSide(door, rotation, out var wallSide))
                return false;

            var searchSide = Opposite(wallSide);
            partner = DoorOnSide(door, searchSide);
            if (partner == null
                || partner == door
                || partner.def != door.def
                || !partner.Props.asymmetric
                || !partner.Props.syncAdjacentAsymmetricPair)
            {
                partner = null;
                return false;
            }

            var partnerProps = partner.Props;
            var partnerRotation = DoorRotationAt(partner.def, partnerProps,
                partner.Position, partner.Rotation, partner.Map);
            if (CanonicalParallelRotation(partnerRotation) != CanonicalParallelRotation(rotation)
                || !AreRectsAdjacentOnSide(door.OccupiedRect(), partner.OccupiedRect(), searchSide)
                || !HasWallSide(partner, searchSide))
            {
                partner = null;
                return false;
            }

            return true;
        }

        private static bool TryGetUniqueWallSide(Building_DoorExpanded door, Rot4 rotation,
            out IntVec3 wallSide)
        {
            GetLocalSideDirections(rotation, out var negativeSide, out var positiveSide);
            var negativeWall = HasWallSide(door, negativeSide);
            var positiveWall = HasWallSide(door, positiveSide);

            if (negativeWall == positiveWall)
            {
                wallSide = IntVec3.Invalid;
                return false;
            }

            wallSide = positiveWall ? positiveSide : negativeSide;
            return true;
        }

        private static void GetLocalSideDirections(Rot4 rotation, out IntVec3 negativeSide,
            out IntVec3 positiveSide)
        {
            if (rotation == Rot4.East)
                negativeSide = IntVec3.North;
            else if (rotation == Rot4.South)
                negativeSide = IntVec3.East;
            else if (rotation == Rot4.West)
                negativeSide = IntVec3.South;
            else
                negativeSide = IntVec3.West;

            positiveSide = Opposite(negativeSide);
        }

        private static IntVec3 Opposite(IntVec3 direction) =>
            new(-direction.x, 0, -direction.z);

        private static bool HasWallSide(Building_DoorExpanded door, IntVec3 direction)
        {
            return HasWallSide(door.OccupiedRect(), door.Map, direction, includeUnbuilt: false);
        }

        private static bool HasWallSide(CellRect rect, Map map, IntVec3 direction,
            bool includeUnbuilt)
        {
            if (direction == IntVec3.East)
            {
                var x = rect.maxX + 1;
                for (var z = rect.minZ; z <= rect.maxZ; z++)
                {
                    if (!HasWallCell(new IntVec3(x, 0, z), map, includeUnbuilt))
                        return false;
                }
                return true;
            }

            if (direction == IntVec3.West)
            {
                var x = rect.minX - 1;
                for (var z = rect.minZ; z <= rect.maxZ; z++)
                {
                    if (!HasWallCell(new IntVec3(x, 0, z), map, includeUnbuilt))
                        return false;
                }
                return true;
            }

            if (direction == IntVec3.North)
            {
                var z = rect.maxZ + 1;
                for (var x = rect.minX; x <= rect.maxX; x++)
                {
                    if (!HasWallCell(new IntVec3(x, 0, z), map, includeUnbuilt))
                        return false;
                }
                return true;
            }

            var southZ = rect.minZ - 1;
            for (var x = rect.minX; x <= rect.maxX; x++)
            {
                if (!HasWallCell(new IntVec3(x, 0, southZ), map, includeUnbuilt))
                    return false;
            }

            return true;
        }

        private static bool HasWallCell(IntVec3 cell, Map map, bool includeUnbuilt)
        {
            return cell.InBounds(map) && DoorUtility.EncapsulatingWallAt(cell, map, includeUnbuilt);
        }

        private static bool HasFullWallSupport(ThingDef def, IntVec3 loc, Rot4 rot, Map map,
            bool includeUnbuilt)
        {
            var rect = GenAdj.OccupiedRect(IntVec3.Zero, def.defaultPlacingRot, def.size);
            var max = def.defaultPlacingRot.IsHorizontal ? rect.Width : rect.Height;

            for (var i = 0; i < max; i++)
            {
                var first = loc + new IntVec3(rect.minX - 1, 0, rect.minZ + i).RotatedBy(rot);
                if (!HasWallCell(first, map, includeUnbuilt))
                    return false;

                var second = loc + new IntVec3(rect.maxX + 1, 0, rect.minZ + i).RotatedBy(rot);
                if (!HasWallCell(second, map, includeUnbuilt))
                    return false;
            }

            return true;
        }

        private static Building_DoorExpanded DoorOnSide(Building_DoorExpanded door, IntVec3 direction)
        {
            Building_DoorExpanded found = null;
            var rect = door.OccupiedRect();
            var map = door.Map;

            if (direction == IntVec3.East)
            {
                var x = rect.maxX + 1;
                for (var z = rect.minZ; z <= rect.maxZ; z++)
                {
                    if (!TryAddDoorOnSide(new IntVec3(x, 0, z), map, ref found))
                        return null;
                }
                return found;
            }

            if (direction == IntVec3.West)
            {
                var x = rect.minX - 1;
                for (var z = rect.minZ; z <= rect.maxZ; z++)
                {
                    if (!TryAddDoorOnSide(new IntVec3(x, 0, z), map, ref found))
                        return null;
                }
                return found;
            }

            if (direction == IntVec3.North)
            {
                var z = rect.maxZ + 1;
                for (var x = rect.minX; x <= rect.maxX; x++)
                {
                    if (!TryAddDoorOnSide(new IntVec3(x, 0, z), map, ref found))
                        return null;
                }
                return found;
            }

            var southZ = rect.minZ - 1;
            for (var x = rect.minX; x <= rect.maxX; x++)
            {
                if (!TryAddDoorOnSide(new IntVec3(x, 0, southZ), map, ref found))
                    return null;
            }

            return found;
        }

        private static bool TryAddDoorOnSide(IntVec3 cell, Map map, ref Building_DoorExpanded found)
        {
            if (!cell.InBounds(map)
                || cell.GetEdifice(map) is not Building_DoorExpanded candidate
                || found != null && found != candidate)
            {
                return false;
            }

            found = candidate;
            return true;
        }

        private static bool AreRectsAdjacentOnSide(CellRect source, CellRect other, IntVec3 direction)
        {
            if (direction == IntVec3.East)
                return source.maxX + 1 == other.minX
                       && source.minZ == other.minZ
                       && source.maxZ == other.maxZ;
            if (direction == IntVec3.West)
                return source.minX - 1 == other.maxX
                       && source.minZ == other.minZ
                       && source.maxZ == other.maxZ;
            if (direction == IntVec3.North)
                return source.maxZ + 1 == other.minZ
                       && source.minX == other.minX
                       && source.maxX == other.maxX;

            return source.minZ - 1 == other.maxZ
                   && source.minX == other.minX
                   && source.maxX == other.maxX;
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
