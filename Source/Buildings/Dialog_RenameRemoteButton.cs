using Verse;

namespace DoorsExpanded
{
    // Minimal concrete rename dialog for remote buttons/levers. Unlike
    // Dialog_RenameBuildingStorage, it does not reject names already used by zones or
    // storage groups — those namespaces are unrelated to door controls.
    public class Dialog_RenameRemoteButton : Dialog_Rename<IRenameable>
    {
        public Dialog_RenameRemoteButton(IRenameable button)
            : base(button)
        {
        }
    }
}
