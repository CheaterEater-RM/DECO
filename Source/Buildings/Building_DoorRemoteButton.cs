using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace DoorsExpanded
{
    public class Building_DoorRemoteButton : Building, IRenameable
    {
        private List<Building_Door> linkedDoors = new();
        private CompPowerTrader powerComp;
        private bool buttonOn;
        private bool needsToBeSwitched;
        private string customLabel;

        public string RenamableLabel
        {
            get => customLabel.NullOrEmpty() ? BaseLabel : customLabel;
            set => customLabel = value == BaseLabel ? null : value;
        }

        public string BaseLabel => base.LabelNoCount.CapitalizeFirst();

        public string InspectLabel => LabelCap;

        public override string LabelNoCount =>
            customLabel.NullOrEmpty() ? base.LabelNoCount : customLabel;

        public List<Building_Door> LinkedDoors
        {
            get
            {
                ScrubLinkedDoors();
                return linkedDoors;
            }
        }

        public bool ButtonOn
        {
            get => buttonOn;
            private set
            {
                if (buttonOn == value)
                    return;

                buttonOn = value;
                ApplyStateToLinkedDoors();
            }
        }

        public bool NeedsToBeSwitched
        {
            get => needsToBeSwitched;
            set => needsToBeSwitched = value;
        }

        public override Graphic Graphic =>
            buttonOn && def.building.fullGraveGraphicData != null
                ? def.building.fullGraveGraphicData.Graphic
                : base.Graphic;

        private bool NeedsPower => powerComp is { PowerOn: false };

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();

            // QoL auto-link: if this button was built from a door-side build gizmo, claim the
            // pending blueprint/frame link that was registered during placement.
            if (!respawningAfterLoad)
            {
                var pendingDoor = map.GetComponent<MapComponent_RemoteAutoLink>()?.ClaimDoorFor(this);
                if (pendingDoor != null)
                    RemoteDoorUtility.SetButton(pendingDoor, this);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
                ScrubLinkedDoors();
            Scribe_Collections.Look(ref linkedDoors, "linkedDoors", LookMode.Reference);
            Scribe_Values.Look(ref buttonOn, "buttonOn", false);
            Scribe_Values.Look(ref needsToBeSwitched, "needsToBeSwitched", false);
            Scribe_Values.Look(ref customLabel, "customLabel");
            linkedDoors ??= new List<Building_Door>();
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            foreach (var linkedDoor in LinkedDoors)
                RemoteLinkDraw.DrawLink(DrawPos, linkedDoor.DrawPos);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            var toggle = new Command_Toggle
            {
                defaultLabel = "PH_UseButtonOrLever".Translate(def.label),
                defaultDesc = "PH_UseButtonOrLeverDesc".Translate(def.label),
                icon = RemoteControlTex.UseButtonOrLever,
                isActive = () => NeedsToBeSwitched,
                toggleAction = () => NeedsToBeSwitched = !NeedsToBeSwitched
            };
            if (IsDisabled(out var reason))
                toggle.Disable(reason);
            yield return toggle;

            yield return new Command_Action
            {
                defaultLabel = "PH_RenameButton".Translate(),
                defaultDesc = "PH_RenameButtonDesc".Translate(),
                icon = RemoteControlTex.Rename,
                action = () => Find.WindowStack.Add(new Dialog_RenameRemoteButton(this))
            };
        }

        public void Notify_Linked(Building_Door door)
        {
            if (door == null)
                return;

            ScrubLinkedDoors();
            if (!linkedDoors.Contains(door))
                linkedDoors.Add(door);
            RemoteDoorUtility.NotifyRemoteStateChanged(door);
        }

        public void Notify_Unlinked(Building_Door door)
        {
            if (linkedDoors == null)
                return;

            linkedDoors.Remove(door);
        }

        public bool IsDisabled(out string reason)
        {
            if (LinkedDoors.Count == 0)
            {
                reason = "PH_UseButtonOrLeverNoConnection".Translate();
                return true;
            }
            if (NeedsPower)
            {
                reason = "PH_UseButtonOrLeverNoPower".Translate();
                return true;
            }

            reason = null;
            return false;
        }

        // Called by JobDriver_UseRemoteButton once a pawn reaches the button — the actual
        // state change happens here, not in the gizmo, so it goes through the work system.
        public void PushButton()
        {
            needsToBeSwitched = false;
            SoundDefOf.Tick_Tiny.PlayOneShot(this);
            ButtonOn = !buttonOn;
        }

        // Gizmo-level availability (no connection / no power).
        public bool IsDisabledForJob(bool forced, out string reason)
        {
            if (!(forced || NeedsToBeSwitched))
            {
                reason = null;
                return true;
            }
            return IsDisabled(out reason);
        }

        private void ApplyStateToLinkedDoors()
        {
            foreach (var linkedDoor in LinkedDoors)
                RemoteDoorUtility.NotifyRemoteStateChanged(linkedDoor, buttonEdge: true);
        }

        private void ScrubLinkedDoors()
        {
            linkedDoors ??= new List<Building_Door>();
            linkedDoors.RemoveAll(door => door == null
                || !door.Spawned
                || !RemoteDoorUtility.CanHaveRemoteControls(door)
                || RemoteDoorUtility.GetButton(door) != this);
        }
    }
}
