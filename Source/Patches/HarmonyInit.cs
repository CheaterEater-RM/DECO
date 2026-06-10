using System.Reflection;
using HarmonyLib;
using Verse;

namespace DoorsExpanded
{
    // Dedicated Harmony bootstrap. Kept separate from RemoteControlTex (a texture cache that
    // also happens to be [StaticConstructorOnStartup]) per the project rule that the patch
    // initializer must be its own class. Fires after ResolveReferences, which is correct for
    // patching — no Defs are touched here.
    [StaticConstructorOnStartup]
    internal static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("com.cheatereater.deco");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
