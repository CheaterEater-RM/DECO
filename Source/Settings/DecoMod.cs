using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // The Mod subclass RimWorld discovers automatically (no modClass in About.xml needed).
    // Holds the single DecoSettings instance and draws the options window. Note: the Mod
    // ctor fires during assembly load, before Defs exist — do not touch Defs here.
    public class DecoMod : Mod
    {
        private static DecoSettings settings;

        public static DecoSettings Settings => settings;

        public DecoMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<DecoSettings>();
        }

        public override string SettingsCategory() => "DECO";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled(
                "DECO_Settings_JailBlocksPrisoners".Translate(),
                ref settings.jailBlocksPrisoners,
                "DECO_Settings_JailBlocksPrisoners_Desc".Translate());
            listing.CheckboxLabeled(
                "DECO_Settings_BlastBlocksPrisoners".Translate(),
                ref settings.blastBlocksPrisoners,
                "DECO_Settings_BlastBlocksPrisoners_Desc".Translate());

            listing.GapLine();

            listing.CheckboxLabeled(
                "DECO_Settings_EscapingPawnsOpenOwnDoor".Translate(),
                ref settings.escapingPawnsOpenOwnDoor,
                "DECO_Settings_EscapingPawnsOpenOwnDoor_Desc".Translate());

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
