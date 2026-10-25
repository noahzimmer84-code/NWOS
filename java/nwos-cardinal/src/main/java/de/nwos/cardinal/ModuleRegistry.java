package de.nwos.cardinal;

import de.nwos.contracts.IModule;
import de.nwos.contracts.PermissionScope;

import java.util.Collection;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Hält die Menge der aktuell aktivierten Module (Architekturdokument
 * Abschnitt 4: "Nicht jeder Client muss dieselben Module aktiv haben").
 * Die Registry ist bewusst dumm - sie trifft keine Entscheidungen, sondern
 * verwaltet nur den Zustand. Entscheidungen (starten/stoppen/routen) trifft
 * der TaskRouter.
 *
 * <p>Port von NWOS.Cardinal.ModuleRegistry.</p>
 */
public final class ModuleRegistry {

    private final Map<String, IModule> modules = new ConcurrentHashMap<>();
    private final Map<String, Set<PermissionScope>> grantedScopes = new ConcurrentHashMap<>();

    /**
     * Registriert ein Modul mit den ihm laut Konfiguration erlaubten Scopes.
     * Wird vom Host beim Start anhand der Konfiguration aufgerufen - nicht von
     * den Modulen selbst, damit kein Modul sich eigenmächtig mehr Rechte
     * geben kann.
     */
    public void register(IModule module, Set<PermissionScope> scopes) {
        Objects.requireNonNull(module, "module");
        if (modules.putIfAbsent(module.getModuleId(), module) != null) {
            throw new IllegalStateException(
                    "Modul '" + module.getModuleId() + "' ist bereits registriert. Doppelte Registrierung deutet auf einen Konfigurationsfehler hin.");
        }
        grantedScopes.put(module.getModuleId(), scopes == null ? Set.of() : Set.copyOf(scopes));
    }

    /**
     * Vergibt Berechtigungen an eine Identität, die KEIN registriertes Modul
     * ist (z. B. das Overlay, das über IPC zugreift). Bewusst eine eigene
     * Methode statt register(): es gibt kein Objekt, das Cardinal
     * starten/überwachen könnte, und der Aufruf ist im Code leicht auffindbar
     * (Abschnitt 9: Berechtigungen müssen klar definiert sein).
     */
    public void grantExternalScope(String identityId, Set<PermissionScope> scope) {
        Objects.requireNonNull(identityId, "identityId");
        grantedScopes.put(identityId, scope == null ? Set.of() : Set.copyOf(scope));
    }

    /**
     * Wahr, wenn diese Identität Grant-Einträge besitzt - entweder als
     * registriertes Modul oder als externe Identität. Der IPC-Server nutzt
     * das für den Handshake, um unbekannte Aufrufer abzuweisen.
     */
    public boolean hasGrantedIdentity(String identityId) {
        return identityId != null && grantedScopes.containsKey(identityId);
    }

    /** Liefert das Modul oder {@code null}, wenn nicht registriert. */
    public IModule tryGet(String moduleId) {
        return moduleId == null ? null : modules.get(moduleId);
    }

    public Set<PermissionScope> getGrantedScopes(String moduleId) {
        return grantedScopes.getOrDefault(moduleId, Set.of());
    }

    public Collection<IModule> allModules() {
        return List.copyOf(modules.values());
    }

    public void unregister(String moduleId) {
        modules.remove(moduleId);
        grantedScopes.remove(moduleId);
    }
}
