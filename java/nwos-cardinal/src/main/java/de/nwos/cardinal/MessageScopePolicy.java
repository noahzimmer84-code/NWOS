package de.nwos.cardinal;

import de.nwos.contracts.PermissionScope;

import java.util.Map;
import java.util.Set;

/**
 * Legt pro MessageType den mindestens nötigen PermissionScope fest -
 * unabhängig davon, was der Absender deklariert (Abschnitt 9/31). Unbekannte
 * MessageTypes haben kein Minimum; die Ziel-Module lehnen sie ohnehin ab.
 *
 * <p>Port von NWOS.Cardinal.MessageScopePolicy.</p>
 */
public final class MessageScopePolicy {

    private static final Map<String, PermissionScope> MINIMUM_SCOPES = Map.ofEntries(
            Map.entry("icon.toggleVisibility", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("box.create", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("box.addItem", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("search.rebuildIndex", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("icon.listStates", PermissionScope.FILE_SYSTEM_READ),
            Map.entry("box.list", PermissionScope.FILE_SYSTEM_READ),
            Map.entry("box.search", PermissionScope.FILE_SYSTEM_READ),
            Map.entry("search.global", PermissionScope.FILE_SYSTEM_READ),

            Map.entry("widget.create", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("widget.move", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("widget.resize", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("widget.setEnabled", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("widget.remove", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("widget.list", PermissionScope.FILE_SYSTEM_READ),
            Map.entry("widget.listTypes", PermissionScope.FILE_SYSTEM_READ),
            Map.entry("widget.render", PermissionScope.FILE_SYSTEM_READ),

            Map.entry("alice.quarantine", PermissionScope.SYSTEM_CHANGE),
            Map.entry("alice.restore", PermissionScope.SYSTEM_CHANGE),

            Map.entry("marc.createFolder", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("marc.renameFile", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("marc.organizeFolder", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("marc.launchApp", PermissionScope.PROCESS_CONTROL),
            Map.entry("marc.closeApp", PermissionScope.PROCESS_CONTROL),

            Map.entry("systemcenter.closeProcess", PermissionScope.PROCESS_CONTROL),
            Map.entry("systemcenter.setAutostartEnabled", PermissionScope.SYSTEM_CHANGE),
            Map.entry("systemcenter.cleanTemp", PermissionScope.FILE_SYSTEM_WRITE),
            Map.entry("systemcenter.optimize", PermissionScope.FILE_SYSTEM_WRITE));

    private MessageScopePolicy() {
    }

    public static Set<PermissionScope> minimumScopeFor(String messageType) {
        var scope = messageType == null ? null : MINIMUM_SCOPES.get(messageType);
        return scope == null ? Set.of() : Set.of(scope);
    }
}
