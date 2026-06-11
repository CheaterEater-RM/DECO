using RimWorld;
using Verse;

namespace DoorsExpanded
{
    [DefOf]
    public static class HeronDefOf
    {
        public static JobDef PH_UseRemoteButton;

        // Asymmetric single-panel doors can mirror toward adjacent walls and optionally
        // sync state with a matching opposite door.
        public static ThingDef HeronCurtainTribal;
        public static ThingDef HeronCurtainTribalDouble;
        public static ThingDef HeronCurtainTribalTriple;

        // Jail and blast doors get prisoner-lock / shoot-through behavior; referenced by
        // PawnCanOpen_PrisonerLock_Patch and CombatExtendedCompat.
        public static ThingDef PH_DoorJail;
        public static ThingDef PH_DoorBlastSingle;
        public static ThingDef PH_DoorBlastDoor;
        public static ThingDef PH_DoorThickBlastDoor;

        static HeronDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HeronDefOf));
        }
    }
}
