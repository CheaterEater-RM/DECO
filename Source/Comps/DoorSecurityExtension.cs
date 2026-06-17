using Verse;

namespace DoorsExpanded
{
    // Marks a door as a "security" door so DECO's prisoner-lock and Combat Extended shoot-through
    // behaviors apply to it, without hardcoding defNames. DECO's own jail/blast doors carry this
    // extension in their XML; reskin add-ons (e.g. the Star Wars edition) opt in by adding the
    // same <modExtensions> entry to their door defs, so they inherit identical behavior.
    public class DoorSecurityExtension : DefModExtension
    {
        // Which prisoner-lock setting governs this door (None = no prisoner lock):
        //   Jail  -> DecoSettings.jailBlocksPrisoners
        //   Blast -> DecoSettings.blastBlocksPrisoners
        public PrisonerLockKind prisonerLock = PrisonerLockKind.None;

        // Bars: under Combat Extended, projectiles can intercept on this door's cell at any height
        // (mirrors CE's plant behavior). Vanilla shoot-through is just the def's low fillPercent.
        public bool barsShootThrough = false;
    }

    public enum PrisonerLockKind
    {
        None,
        Jail,
        Blast,
    }
}
