using Verse;

namespace DoorsExpanded
{
    // Transient, session-only convenience tracker for the "build button from door" QoL
    // feature. When the player clicks a build-button gizmo on a Building_DoorRemote, that
    // door is "armed" here. The next remote button to finish construction (see
    // Building_DoorRemoteButton.SpawnSetup) claims the armed door and auto-links to it.
    //
    // Deliberately NOT saved: the blueprint->frame->building chain can't carry an instance
    // reference, and persisting a half-finished intent across saves adds save-compat risk for
    // a pure convenience. If a save/load happens mid-placement the arm is simply lost and the
    // player falls back to the manual "Connect remote" gizmo. The arm also expires after a
    // generous window so a forgotten or cancelled build never silently captures an unrelated
    // future button.
    public class MapComponent_RemoteAutoLink : MapComponent
    {
        // ~half an in-game day; long enough for a build to be queued and completed, short
        // enough that a stale arm doesn't linger for the rest of the game.
        private const int ArmExpiryTicks = 30000;

        private Building_DoorRemote armedDoor;
        private int armedTick;

        public MapComponent_RemoteAutoLink(Map map) : base(map)
        {
        }

        public void Arm(Building_DoorRemote door)
        {
            // Overwrite any previous arm: only the most recently requested door matters, which
            // gives the "link to the last built one" behaviour.
            armedDoor = door;
            armedTick = Find.TickManager.TicksGame;
        }

        // Returns the still-valid armed door and disarms, or null if none/expired/invalid.
        public Building_DoorRemote ClaimArmedDoor()
        {
            var door = armedDoor;
            armedDoor = null;

            if (door == null || !door.Spawned || door.Map != map)
                return null;
            if (Find.TickManager.TicksGame - armedTick > ArmExpiryTicks)
                return null;

            return door;
        }
    }
}
