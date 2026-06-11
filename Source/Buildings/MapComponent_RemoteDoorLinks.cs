using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // Stores remote-control state for vanilla doors that cannot carry DECO's save fields.
    // DECO remote doors keep their original button/securedRemotely fields for save swapping.
    public class MapComponent_RemoteDoorLinks : MapComponent
    {
        private List<Building_Door> doors = new();
        private List<Building_DoorRemoteButton> buttons = new();
        private List<bool> securedRemotely = new();
        private List<bool> restoreHoldOpenOnClose = new();

        public MapComponent_RemoteDoorLinks(Map map) : base(map)
        {
        }

        public Building_DoorRemoteButton GetButton(Building_Door door)
        {
            ScrubLinks();
            var index = doors.IndexOf(door);
            return index >= 0 ? buttons[index] : null;
        }

        public void SetButton(Building_Door door, Building_DoorRemoteButton button)
        {
            if (!IsValidDoor(door))
                return;

            var index = doors.IndexOf(door);
            if (index < 0 && button == null)
                return;
            if (index < 0)
                index = AddDoor(door);

            var oldButton = buttons[index];
            if (oldButton == button)
                return;

            buttons[index] = button;
            oldButton?.Notify_Unlinked(door);
            if (button == null)
            {
                RemoveAt(index);
                return;
            }

            button.Notify_Linked(door);
            NotifyRemoteStateChanged(door);
        }

        public bool SecuredRemotely(Building_Door door)
        {
            ScrubLinks();
            var index = doors.IndexOf(door);
            return index >= 0 && securedRemotely[index] && buttons[index] is { Spawned: true };
        }

        public void SetSecuredRemotely(Building_Door door, bool value)
        {
            if (!IsValidDoor(door))
                return;

            var index = doors.IndexOf(door);
            if (index < 0)
                index = AddDoor(door);
            var next = value && buttons[index] != null;
            if (securedRemotely[index] == next)
                return;

            if (next)
                RemoteDoorUtility.SetHoldOpen(door, false);

            securedRemotely[index] = next;
            NotifyRemoteStateChanged(door);
        }

        public void NotifyRemoteStateChanged(Building_Door door, bool buttonEdge = false)
        {
            var index = doors.IndexOf(door);
            if (index < 0)
                return;
            NotifyRemoteStateChangedAt(index, buttonEdge);
        }

        // Index-based core so callers that already know the slot (the tick loop) don't
        // re-run IndexOf for the door and again for the SecuredRemotely check.
        private void NotifyRemoteStateChangedAt(int index, bool buttonEdge)
        {
            var door = doors[index];
            if (!IsValidDoor(door))
                return;

            var button = buttons[index];
            if (button is not { Spawned: true } || !RemoteCanAct(door))
                return;

            if (button.ButtonOn)
            {
                RemoteDoorUtility.OpenDoor(door);
            }
            // button is already known spawned (guard above), so securedRemotely[index] is
            // the full secured test here.
            else if (door.Open && (buttonEdge || securedRemotely[index]))
            {
                if (RemoteDoorUtility.HoldOpen(door))
                {
                    RemoteDoorUtility.SetHoldOpen(door, false);
                    restoreHoldOpenOnClose[index] = true;
                }
                RemoteDoorUtility.StartClosing(door);
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (doors.Count == 0)
                return;

            if (Find.TickManager.TicksGame % 30 == 0)
                ScrubLinks();

            for (var i = doors.Count - 1; i >= 0; i--)
            {
                var door = doors[i];
                if (!IsValidDoor(door))
                    continue;

                if (restoreHoldOpenOnClose[i] && !door.Open)
                {
                    RemoteDoorUtility.SetHoldOpen(door, true);
                    restoreHoldOpenOnClose[i] = false;
                }

                if (buttons[i] != null && door.IsHashIntervalTick(30))
                    NotifyRemoteStateChangedAt(i, buttonEdge: false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref doors, "securityRemoteDoors", LookMode.Reference);
            Scribe_Collections.Look(ref buttons, "securityRemoteButtons", LookMode.Reference);
            Scribe_Collections.Look(ref securedRemotely, "securityRemoteSecured", LookMode.Value);
            Scribe_Collections.Look(ref restoreHoldOpenOnClose, "securityRemoteRestoreHoldOpen", LookMode.Value);
            doors ??= new List<Building_Door>();
            buttons ??= new List<Building_DoorRemoteButton>();
            securedRemotely ??= new List<bool>();
            restoreHoldOpenOnClose ??= new List<bool>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                TrimMismatchedLists();
        }

        private int AddDoor(Building_Door door)
        {
            doors.Add(door);
            buttons.Add(null);
            securedRemotely.Add(false);
            restoreHoldOpenOnClose.Add(false);
            return doors.Count - 1;
        }

        private void ScrubLinks()
        {
            TrimMismatchedLists();
            for (var i = doors.Count - 1; i >= 0; i--)
            {
                if (!IsValidDoor(doors[i]) || buttons[i] is not { Spawned: true })
                    RemoveAt(i);
            }
        }

        private bool IsValidDoor(Building_Door door)
        {
            return RemoteDoorUtility.IsVanillaRemoteControllableDoor(door) && door.Spawned && door.Map == map;
        }

        private static bool RemoteCanAct(Building_Door door)
        {
            var powerComp = door.TryGetComp<CompPowerTrader>();
            return powerComp == null || powerComp.PowerOn;
        }

        private void RemoveAt(int index)
        {
            doors.RemoveAt(index);
            buttons.RemoveAt(index);
            securedRemotely.RemoveAt(index);
            restoreHoldOpenOnClose.RemoveAt(index);
        }

        private void TrimMismatchedLists()
        {
            var count = Mathf.Min(Mathf.Min(doors.Count, buttons.Count),
                Mathf.Min(securedRemotely.Count, restoreHoldOpenOnClose.Count));
            if (doors.Count > count)
                doors.RemoveRange(count, doors.Count - count);
            if (buttons.Count > count)
                buttons.RemoveRange(count, buttons.Count - count);
            if (securedRemotely.Count > count)
                securedRemotely.RemoveRange(count, securedRemotely.Count - count);
            if (restoreHoldOpenOnClose.Count > count)
                restoreHoldOpenOnClose.RemoveRange(count, restoreHoldOpenOnClose.Count - count);
        }
    }
}
