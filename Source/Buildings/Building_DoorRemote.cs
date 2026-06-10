namespace DoorsExpanded
{
    // Remote-controllable door. Currently a stub: the class exists so that doors saved under
    // this name (blast doors, autodoors, and the original mod's remote doors) load correctly.
    // Saved remote state from the original mod (button, securedRemotely) is silently ignored
    // until the M2 milestone implements remote behavior here under those same field names.
    public class Building_DoorRemote : Building_DoorExpanded
    {
    }
}
