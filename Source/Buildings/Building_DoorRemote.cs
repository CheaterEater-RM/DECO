using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // Remote-controllable door. The remote button's buttonOn field is the authority:
    // linked doors continually reconcile toward that state instead of storing a stale
    // "remotely held open" value in vanilla holdOpenInt.
    public class Building_DoorRemote : Building_DoorExpanded
    {
        private const int RemoteOpenRefreshTicks = 120;

        private Building_DoorRemoteButton button;
        private bool securedRemotely;
        // Set when a remote close cleared the player's hold-open; restored once the door
        // finishes closing so the setting survives for the next manual open.
        private bool restoreHoldOpenOnClose;

        public Building_DoorRemoteButton Button
        {
            get => button;
            private set
            {
                if (button == value)
                    return;

                var oldButton = button;
                button = value;
                oldButton?.Notify_Unlinked(this);
                button?.Notify_Linked(this);
                if (button == null)
                    securedRemotely = false;
                Notify_RemoteStateChanged();
            }
        }

        public bool SecuredRemotely
        {
            get => securedRemotely && button is { Spawned: true };
            set
            {
                var next = value && button != null;
                if (securedRemotely == next)
                    return;

                // Force "hold open" off before securing remotely. Otherwise the hold-open
                // gizmo gets disabled while still active, leaving the door stuck open with
                // no way to clear it. Securing means the remote is the sole authority.
                if (next)
                    holdOpenInt = false;

                securedRemotely = next;
                Notify_RemoteStateChanged();
            }
        }

        public bool RemoteWantsOpen => button is { Spawned: true, ButtonOn: true };

        public bool RemoteForcesClosed => SecuredRemotely && !RemoteWantsOpen;

        private bool RemoteCanAct => powerComp == null || powerComp.PowerOn;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (button == null)
                securedRemotely = false;
            else
                button.Notify_Linked(this);
            Notify_RemoteStateChanged();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref button, "button");
            Scribe_Values.Look(ref securedRemotely, "securedRemotely", false);
            Scribe_Values.Look(ref restoreHoldOpenOnClose, "restoreHoldOpenOnClose", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (restoreHoldOpenOnClose && !Open)
            {
                holdOpenInt = true;
                restoreHoldOpenOnClose = false;
            }
            if (button != null && this.IsHashIntervalTick(30))
                Notify_RemoteStateChanged();
        }

        public override bool PawnCanOpen(Pawn p)
        {
            if (RemoteForcesClosed)
                return false;
            return base.PawnCanOpen(p);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            if (!RemoteForcesClosed)
                return;

            var overlayLoc = drawLoc;
            overlayLoc.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            Graphics.DrawMesh(MeshPool.plane05, Matrix4x4.TRS(overlayLoc, Quaternion.identity, Vector3.one),
                RemoteControlTex.SecuredOverlayMaterial, 0);
        }

        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            if (button != null)
                RemoteLinkDraw.DrawLink(button.DrawPos, DrawPos);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            var insertedRemoteGizmos = false;
            var holdOpenLabel = "CommandToggleDoorHoldOpen".Translate().ToString();

            foreach (var gizmo in base.GetGizmos())
            {
                if (gizmo is Command_Toggle command && command.defaultLabel == holdOpenLabel)
                {
                    if (SecuredRemotely)
                        gizmo.Disable("PH_RemoteDoorSecuredRemotely".Translate());

                    yield return gizmo;
                    foreach (var remoteGizmo in RemoteGizmos())
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
                foreach (var remoteGizmo in RemoteGizmos())
                    yield return remoteGizmo;
            }
        }

        // buttonEdge is true only when the player just toggled the button (a deliberate
        // action), false for passive reconciles (tick, spawn, (un)link). Passive reconciles
        // must not fight pawn-driven opening on an unsecured door — only a secured door is
        // continuously forced closed.
        public void Notify_RemoteStateChanged(bool buttonEdge = false)
        {
            if (!Spawned || button is not { Spawned: true } || !RemoteCanAct)
                return;

            if (button.ButtonOn)
            {
                DoorOpen(RemoteOpenRefreshTicks);
            }
            else if (Open && (buttonEdge || SecuredRemotely))
            {
                // The remote closing the door must override a manual "hold open" — player
                // input wins. But don't slam it shut on a pawn mid-stride: clear hold-open
                // and drive the close through vanilla's timer, which defers while a pawn
                // occupies the doorway. Tick puts the hold-open setting back once the
                // door has actually closed (see restoreHoldOpenOnClose).
                if (holdOpenInt)
                {
                    holdOpenInt = false;
                    restoreHoldOpenOnClose = true;
                }
                ticksUntilClose = 1;
            }
        }

        private IEnumerable<Gizmo> RemoteGizmos()
        {
            var secure = new Command_Toggle
            {
                defaultLabel = "PH_RemoteDoorSecuredRemotely".Translate(),
                defaultDesc = "PH_RemoteDoorSecuredRemotelyDesc".Translate(),
                icon = RemoteControlTex.SecuredRemotely,
                isActive = () => SecuredRemotely,
                toggleAction = () => SecuredRemotely = !SecuredRemotely
            };
            if (button == null)
                secure.Disable("PH_ButtonNeeded".Translate());
            yield return secure;

            yield return new Command_Action
            {
                defaultLabel = "PH_ButtonConnect".Translate(),
                defaultDesc = "PH_ButtonConnectDesc".Translate(),
                icon = RemoteControlTex.ConnectToButton,
                action = BeginButtonConnect
            };

            if (button != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PH_ButtonDisconnect".Translate(),
                    defaultDesc = "PH_ButtonDisconnectDesc".Translate(),
                    icon = RemoteControlTex.DisconnectButton,
                    action = () => Button = null
                };
            }
        }

        private void BeginButtonConnect()
        {
            var parameters = new TargetingParameters
            {
                canTargetBuildings = true,
                canTargetPawns = false,
                validator = target => target.Thing is Building_DoorRemoteButton
            };
            Find.Targeter.BeginTargeting(parameters, target =>
            {
                if (target.Thing is Building_DoorRemoteButton remoteButton)
                {
                    Button = remoteButton;
                }
                else
                {
                    Messages.Message("PH_ButtonConnectFailed".Translate(), this, MessageTypeDefOf.RejectInput);
                }
            });
        }
    }
}
