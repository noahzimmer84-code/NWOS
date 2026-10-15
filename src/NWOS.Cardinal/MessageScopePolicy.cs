using NWOS.Contracts;

namespace NWOS.Cardinal;

/// <summary>
/// Legt pro MessageType den mindestens nötigen <see cref="PermissionScope"/>
/// fest - unabhängig davon, was der Absender deklariert (Abschnitt 9/31).
/// Unbekannte MessageTypes haben kein Minimum; die Ziel-Module lehnen sie
/// ohnehin mit NotSupportedException ab.
/// </summary>
public static class MessageScopePolicy
{
    public static PermissionScope MinimumScopeFor(string messageType) => messageType switch
    {
        "icon.toggleVisibility" or "box.create" or "box.addItem" or "search.rebuildIndex"
            => PermissionScope.FileSystemWrite,
        "icon.listStates" or "box.list" or "box.search" or "search.global"
            => PermissionScope.FileSystemRead,

        "widget.create" or "widget.move" or "widget.resize" or "widget.setEnabled" or "widget.remove"
            => PermissionScope.FileSystemWrite,
        "widget.list" or "widget.listTypes" or "widget.render"
            => PermissionScope.FileSystemRead,

        "alice.quarantine" or "alice.restore" => PermissionScope.SystemChange,

        "marc.createFolder" or "marc.renameFile" or "marc.organizeFolder" => PermissionScope.FileSystemWrite,
        "marc.launchApp" or "marc.closeApp" => PermissionScope.ProcessControl,

        "systemcenter.closeProcess" => PermissionScope.ProcessControl,
        "systemcenter.setAutostartEnabled" => PermissionScope.SystemChange,
        "systemcenter.cleanTemp" or "systemcenter.optimize" => PermissionScope.FileSystemWrite,

        _ => PermissionScope.None,
    };
}
