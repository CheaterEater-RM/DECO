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

        // Prisoner-lock / shoot-through behavior is no longer keyed off defNames here — doors opt in
        // via DoorSecurityExtension (see PawnCanOpen_PrisonerLock_Patch and CombatExtendedCompat).

        static HeronDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HeronDefOf));
        }
    }
}
