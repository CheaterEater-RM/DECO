using Verse;

namespace DoorsExpanded
{
    // Persistent mod settings. Defaults preserve the new intended behavior (prisoner locks
    // on by default) while the escape/slave/entity toggles stay off so the feature is
    // conservative out of the box.
    public class DecoSettings : ModSettings
    {
        // Jail doors won't open for prisoners (they don't have keys).
        public bool jailBlocksPrisoners = true;
        // Blast doors won't open for prisoners (assume a keycard system they lack).
        public bool blastBlocksPrisoners = true;
        // A prisoner who is actively breaking out may open the door of their own cell.
        public bool escapingPawnsOpenOwnDoor = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref jailBlocksPrisoners, "jailBlocksPrisoners", defaultValue: true);
            Scribe_Values.Look(ref blastBlocksPrisoners, "blastBlocksPrisoners", defaultValue: true);
            Scribe_Values.Look(ref escapingPawnsOpenOwnDoor, "escapingPawnsOpenOwnDoor", defaultValue: false);
        }
    }
}
