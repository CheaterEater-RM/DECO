using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    internal static class RemoteDoorUtility
    {
        private const int RemoteOpenRefreshTicks = 120;
        private const string VanillaAutodoorDefName = "Autodoor";

        private static readonly FieldInfo HoldOpenField =
            AccessTools.Field(typeof(Building_Door), "holdOpenInt");

        private static readonly FieldInfo TicksUntilCloseField =
            AccessTools.Field(typeof(Building_Door), "ticksUntilClose");

        private static readonly MethodInfo DoorOpenMethod =
            AccessTools.Method(typeof(Building_Door), "DoorOpen");

        public static bool IsVanillaRemoteControllableDoor(Building_Door door)
        {
            return door != null
                && door is not Building_DoorRemote
                && (door.def == ThingDefOf.SecurityDoor || door.def?.defName == VanillaAutodoorDefName);
        }

        public static bool CanHaveRemoteControls(Building_Door door)
        {
            return door is Building_DoorRemote || IsVanillaRemoteControllableDoor(door);
        }

        public static Building_DoorRemoteButton GetButton(Building_Door door)
        {
            if (door is Building_DoorRemote remoteDoor)
                return remoteDoor.Button;

            return IsVanillaRemoteControllableDoor(door)
                ? door.Map?.GetComponent<MapComponent_RemoteDoorLinks>()?.GetButton(door)
                : null;
        }

        public static void SetButton(Building_Door door, Building_DoorRemoteButton button)
        {
            if (door is Building_DoorRemote remoteDoor)
            {
                remoteDoor.LinkToButton(button);
                return;
            }

            if (IsVanillaRemoteControllableDoor(door))
                door.Map?.GetComponent<MapComponent_RemoteDoorLinks>()?.SetButton(door, button);
        }

        public static bool SecuredRemotely(Building_Door door)
        {
            if (door is Building_DoorRemote remoteDoor)
                return remoteDoor.SecuredRemotely;

            return IsVanillaRemoteControllableDoor(door)
                && door.Map?.GetComponent<MapComponent_RemoteDoorLinks>()?.SecuredRemotely(door) == true;
        }

        public static void SetSecuredRemotely(Building_Door door, bool value)
        {
            if (door is Building_DoorRemote remoteDoor)
            {
                remoteDoor.SecuredRemotely = value;
                return;
            }

            if (IsVanillaRemoteControllableDoor(door))
                door.Map?.GetComponent<MapComponent_RemoteDoorLinks>()?.SetSecuredRemotely(door, value);
        }

        public static bool RemoteForcesClosed(Building_Door door)
        {
            return SecuredRemotely(door) && GetButton(door) is { Spawned: true, ButtonOn: false };
        }

        public static void NotifyRemoteStateChanged(Building_Door door, bool buttonEdge = false)
        {
            if (door is Building_DoorRemote remoteDoor)
            {
                remoteDoor.Notify_RemoteStateChanged(buttonEdge);
                return;
            }

            if (IsVanillaRemoteControllableDoor(door))
                door.Map?.GetComponent<MapComponent_RemoteDoorLinks>()?.NotifyRemoteStateChanged(door, buttonEdge);
        }

        public static IEnumerable<Gizmo> RemoteGizmos(Building_Door door)
        {
            var secure = new Command_Toggle
            {
                defaultLabel = "PH_RemoteDoorSecuredRemotely".Translate(),
                defaultDesc = "PH_RemoteDoorSecuredRemotelyDesc".Translate(),
                icon = RemoteControlTex.SecuredRemotely,
                isActive = () => SecuredRemotely(door),
                toggleAction = () => SetSecuredRemotely(door, !SecuredRemotely(door))
            };
            if (GetButton(door) == null)
                secure.Disable("PH_ButtonNeeded".Translate());
            yield return secure;

            yield return new Command_Action
            {
                defaultLabel = "PH_ButtonConnect".Translate(),
                defaultDesc = "PH_ButtonConnectDesc".Translate(),
                icon = RemoteControlTex.ConnectToButton,
                action = () => BeginButtonConnect(door)
            };

            if (GetButton(door) != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PH_ButtonDisconnect".Translate(),
                    defaultDesc = "PH_ButtonDisconnectDesc".Translate(),
                    icon = RemoteControlTex.DisconnectButton,
                    action = () => SetButton(door, null)
                };
            }

            foreach (var buildGizmo in BuildButtonGizmos(door))
                yield return buildGizmo;
        }

        public static void DrawLinkOverlay(Building_Door door)
        {
            if (GetButton(door) is { Spawned: true } button)
                RemoteLinkDraw.DrawLink(button.DrawPos, door.DrawPos);
        }

        public static void OpenDoor(Building_Door door)
        {
            DoorOpenMethod?.Invoke(door, new object[] { RemoteOpenRefreshTicks });
        }

        public static bool HoldOpen(Building_Door door)
        {
            return HoldOpenField != null && (bool)HoldOpenField.GetValue(door);
        }

        public static void SetHoldOpen(Building_Door door, bool value)
        {
            HoldOpenField?.SetValue(door, value);
        }

        public static void StartClosing(Building_Door door)
        {
            TicksUntilCloseField?.SetValue(door, 1);
        }

        private static IEnumerable<Gizmo> BuildButtonGizmos(Building_Door door)
        {
            foreach (var buttonDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!MapComponent_RemoteAutoLink.IsRemoteControlDef(buttonDef))
                    continue;

                if (BuildCopyCommandUtility.BuildCommand(buttonDef) is not Command_Action vanillaCommand)
                    continue;

                var def = buttonDef;
                var startPlacement = vanillaCommand.action;
                yield return new Command_Action
                {
                    defaultLabel = "PH_BuildButton".Translate(def.label),
                    defaultDesc = "PH_BuildButtonDesc".Translate(def.label),
                    icon = vanillaCommand.icon,
                    iconProportions = vanillaCommand.iconProportions,
                    iconDrawScale = vanillaCommand.iconDrawScale,
                    iconTexCoords = vanillaCommand.iconTexCoords,
                    iconAngle = vanillaCommand.iconAngle,
                    iconOffset = vanillaCommand.iconOffset,
                    defaultIconColor = vanillaCommand.defaultIconColor,
                    action = () =>
                    {
                        door.Map.GetComponent<MapComponent_RemoteAutoLink>()?.Arm(door, def);
                        startPlacement();
                        Messages.Message("PH_BuildButtonArmed".Translate(def.label),
                            door, MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                };
            }
        }

        private static void BeginButtonConnect(Building_Door door)
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
                    SetButton(door, remoteButton);
                }
                else
                {
                    Messages.Message("PH_ButtonConnectFailed".Translate(), door, MessageTypeDefOf.RejectInput);
                }
            });
        }
    }
}
