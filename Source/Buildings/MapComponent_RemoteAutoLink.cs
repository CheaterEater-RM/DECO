using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // Transient, session-only convenience tracker for the "build button from door" QoL
    // feature. When the player clicks a build-button gizmo on a remote-capable door, that
    // door is "armed" here. The next matching remote-control blueprint placed by vanilla's
    // designator is then recorded here and carried through blueprint -> frame -> finished
    // building so the final button links to the door that started the build.
    //
    // The temporary arm is deliberately not saved, but pending blueprint/frame links are
    // scribed by reference so saving after placement and before construction completes keeps
    // the auto-link intent.
    public class MapComponent_RemoteAutoLink : MapComponent
    {
        // Long enough to place a blueprint after opening the door gizmo, short enough that a
        // cancelled placement doesn't quietly capture an unrelated later build command.
        private const int ArmExpiryTicks = 2500;

        private Building_Door armedDoor;
        private ThingDef armedControlDef;
        private int armedTick;
        private List<Thing> pendingThings = new();
        private List<Building_Door> pendingDoors = new();
        private readonly List<Frame> completingFrames = new();

        public MapComponent_RemoteAutoLink(Map map) : base(map)
        {
        }

        public static bool IsRemoteControlDef(ThingDef def)
        {
            return def != null
                && !def.defName.NullOrEmpty()
                && def.category == ThingCategory.Building
                && def.BuildableByPlayer
                && !def.IsBlueprint
                && !def.IsFrame
                && def.thingClass != null
                && typeof(Building_DoorRemoteButton).IsAssignableFrom(def.thingClass);
        }

        public void Arm(Building_Door door, ThingDef controlDef)
        {
            // Overwrite any previous unplaced arm: only the most recent door-side build
            // command should be waiting for the next matching blueprint.
            armedDoor = door;
            armedControlDef = controlDef;
            armedTick = Find.TickManager.TicksGame;
        }

        public void RegisterBlueprintFromArm(Blueprint_Build blueprint, ThingDef controlDef)
        {
            if (blueprint == null || controlDef != armedControlDef || !IsValidArmedDoor())
                return;

            RegisterPendingThing(blueprint, armedDoor);
            ClearArm();
        }

        public void TransferPendingThing(Thing oldThing, Thing newThing)
        {
            if (oldThing == null || newThing == null)
                return;

            for (var i = 0; i < pendingThings.Count; i++)
            {
                if (pendingThings[i] == oldThing)
                {
                    pendingThings[i] = newThing;
                    return;
                }
            }
        }

        public void NotifyFrameCompleting(Frame frame)
        {
            if (frame != null && pendingThings.Contains(frame) && !completingFrames.Contains(frame))
                completingFrames.Add(frame);
        }

        public Building_Door ClaimDoorFor(Building_DoorRemoteButton button)
        {
            if (button == null)
                return null;

            for (var i = pendingThings.Count - 1; i >= 0; i--)
            {
                var pendingThing = pendingThings[i];
                var door = pendingDoors[i];

                if (!IsValidDoor(door))
                {
                    RemovePendingAt(i);
                    continue;
                }

                if (PendingThingMatchesButton(pendingThing, button))
                {
                    RemovePendingAt(i);
                    if (pendingThing is Frame frame)
                        completingFrames.Remove(frame);
                    return door;
                }
            }

            if (button.def == armedControlDef && IsValidArmedDoor())
            {
                var door = armedDoor;
                ClearArm();
                return door;
            }

            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingThings, "pendingThings", LookMode.Reference);
            Scribe_Collections.Look(ref pendingDoors, "pendingDoors", LookMode.Reference);
            pendingThings ??= new List<Thing>();
            pendingDoors ??= new List<Building_Door>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                TrimMismatchedPendingLists();
        }

        private void RegisterPendingThing(Thing thing, Building_Door door)
        {
            if (thing == null || !IsValidDoor(door))
                return;

            var existingIndex = pendingThings.IndexOf(thing);
            if (existingIndex >= 0)
            {
                pendingDoors[existingIndex] = door;
                return;
            }

            pendingThings.Add(thing);
            pendingDoors.Add(door);
        }

        private bool IsValidArmedDoor()
        {
            if (Find.TickManager.TicksGame - armedTick > ArmExpiryTicks)
            {
                ClearArm();
                return false;
            }
            if (!IsValidDoor(armedDoor))
            {
                ClearArm();
                return false;
            }
            return armedControlDef != null && IsRemoteControlDef(armedControlDef);
        }

        private bool IsValidDoor(Building_Door door)
        {
            return door != null
                && door.Spawned
                && door.Map == map
                && RemoteDoorUtility.CanHaveRemoteControls(door);
        }

        private bool PendingThingMatchesButton(Thing pendingThing, Building_DoorRemoteButton button)
        {
            if (pendingThing == null || pendingThing.Position != button.Position || pendingThing.Rotation != button.Rotation)
                return false;

            if (pendingThing.Destroyed && (pendingThing is not Frame frame || !completingFrames.Contains(frame)))
                return false;

            if (pendingThing == button)
                return true;

            if (pendingThing is Blueprint_Build blueprint)
                return blueprint.BuildDef == button.def;

            if (pendingThing is Frame pendingFrame)
                return pendingFrame.BuildDef == button.def;

            return false;
        }

        private void ClearArm()
        {
            armedDoor = null;
            armedControlDef = null;
            armedTick = 0;
        }

        private void RemovePendingAt(int index)
        {
            pendingThings.RemoveAt(index);
            pendingDoors.RemoveAt(index);
        }

        private void TrimMismatchedPendingLists()
        {
            var count = Mathf.Min(pendingThings.Count, pendingDoors.Count);
            if (pendingThings.Count > count)
                pendingThings.RemoveRange(count, pendingThings.Count - count);
            if (pendingDoors.Count > count)
                pendingDoors.RemoveRange(count, pendingDoors.Count - count);
        }
    }
}
