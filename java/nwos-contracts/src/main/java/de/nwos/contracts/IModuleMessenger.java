package de.nwos.contracts;

import java.util.concurrent.CompletableFuture;

/**
 * Erlaubt einem Modul, selbst eine Nachricht an ein anderes Modul
 * anzustoßen (Architekturdokument Abschnitt 6). Bewusst als schmale
 * Schnittstelle statt einer direkten Referenz auf den CardinalService: ein
 * Modul, das nur IModuleMessenger kennt, kann technisch nicht mehr tun als
 * eine Nachricht einreichen - sie durchläuft trotzdem immer die reguläre
 * Berechtigungsprüfung (Abschnitt 9/31).
 *
 * <p>Port von NWOS.Contracts.IModuleMessenger.</p>
 */
public interface IModuleMessenger {

    CompletableFuture<ModuleMessageResult> send(ModuleMessage message);
}
