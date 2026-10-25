# PT2-Cardinal-Architektur

## 1. Auftrag

Cardinal ist die zentrale Systeminstanz (Auftrag §3): Initialisierung,
Konfiguration, Modulverwaltung, KI-Koordination, Kommunikation, Berechtigungen,
Systemzustand, Fehlerüberwachung, Lifecycle, Plugin-/Widget-Verwaltung,
Präferenzen, Windows-Integration, Logging, Recovery, Update-Vorbereitung,
Sicherheitszustände, Ressourcenüberwachung. Kein Fake, keine Still-Eskalation.

## 2. Bestand (aus PT1, unverändert weiterverwendet)

| Klasse | Aufgabe | Zustand |
|---|---|---|
| `ModuleRegistry` | aktive Module + Scopes (nur Host vergibt) | getestet |
| `TaskRouter` | zwingender Permission-Check → Zustellung → Ergebnis | getestet |
| `PermissionManager` | Scope-Prüfung inkl. fester Marc-Netzwerk-Sperre | getestet |
| `MessageScopePolicy` | minimale Scopes pro MessageType (absenderunabhängig) | getestet |
| `HealthMonitor` | 30-s-Heartbeat, Schwelle 3 → Recovery-Event | getestet |
| `CardinalService` | Start aller Module, Health-Loop, sauberer Shutdown | getestet |

Entfernt wird einzig der Fake-`CardinalIpcDispatcher` (Kern darf keine
Modulprojekte referenzieren, Antworten waren erfunden).

## 3. Erweiterungen PT2.1 (neue Kernbausteine)

### 3.1 EventBus (`NWOS.Cardinal/EventBus.cs` + Verträge in `NWOS.Contracts`)
- Typisiertes, thread-sicheres Publish/Subscribe **im Prozess** (Module) und
  mit Spiegelung auf den IPC-Notify-Kanal (Overlay).
- Events: `ModuleStateChanged`, `HealthChanged`, `PendingActionRaised/Resolved`,
  `WidgetUpdated`, `BoxUpdated`, `ShutdownRequested`.
- Kein Polling: Health bleibt einziges Intervall (30 s), alles andere ereignisgetrieben.

### 3.2 PendingActionStore (`NWOS.Cardinal/PendingActionStore.cs`)
- Schließt die Lücke aus MIGRATION §4.7: `TaskRouter` → `PendingUserConfirmation`
  bedeutet ab sofort **eine persistierte, auditerte Warteschlange**
  (nwos-pending-actions.json im Konfigurationsverzeichnis):
  `{ PendingActionId, Message, RaisedAt, ExpiresAt, Status }`.
- Lebenszyklus: `Raised` → (`Approved`|`Denied`|`Expired`) → Ergebnis wird über
  den EventBus publiziert und im Audit-Log protokolliert. TTL (z. B. 10 Min)
  gegen endlose Warteschlangen; auf Timeout folgt **Verweigerung**, nie
  stilles Durchwinken.
- UI bindet sich über IPC (`pending.list/pending.resolve`); ohne Overlay laufen
  Aktionen kontrolliert in den Timeout – sicherer Grundzustand bleibt erhalten.

### 3.3 RecoverySupervisor (`NWOS.Cardinal/RecoverySupervisor.cs`)
- Ersetzt das direkte `ModuleNeedsRecovery`-Wiring durch eine Zustandsmaschine
  pro Modul: `Healthy → Degraded → Recovering(neu) → Healthy|Isolated`.
- **Backoff** gegen Crash-Loops (z. B. 1 s, 5 s, 30 s, dann `Isolated` mit
  Nutzermeldung statt endlosem Neustart).
- `Isolated` heißt: Modul bleibt registriert, wird aber nicht mehr beliefert;
  andere Module laufen unberührt weiter (Auftrag §5: „Alya crashed → Cardinal
  bleibt aktiv"). Reaktivierung nur durch Nutzerentscheidung (Settings/AI-Fenster).
- Für Prozesse (Overlay): gleiche Maschine mit Prozess-Heartbeat statt
  `CheckHealthAsync` (Details OVERLAY.md).

### 3.4 AuditLog (`NWOS.Cardinal/AuditLog.cs`)
- Persistiertes, append-only JSONL im Konfigurationsverzeichnis
  (nwos-audit.jsonl): Permission-Entscheidungen (aus `PermissionManager`),
  Recovery-Ereignisse, PendingAction-Lebenszyklus, Shutdown/Start.
- Löst das TODO in `PermissionManager` („persistentes Audit-Log") ab; Alice und
  der Nutzer können über `audit.query` lesen (read-only).

### 3.5 ProcessSupervisor (Vorbereitung PT2.2/2.3)
- Startet/überwacht den Overlay-Prozess und künftige Hilfsprozesse
  (Start, Heartbeat-Verlust → geordneter Neustart, Sitzungs-Restore).
- Basis ist `BootstrapCommands`' Single-Instance-Mutex (existiert schon:
  `Local\NWOS.Cardinal.SingleInstance`).

## 4. Lifecycle PT2

```
Program.Main (Bootstrap) → HostComposition.RunAsync
  → Konfiguration laden (oder GUI-FirstRunWizard, IUserPrompt)
  → Cardinal Core (Registry/Router/Permissions/EventBus/Audit/PendingStore)
  → Module starten (konfigurationsgetrieben, Ausnahme isoliert ein Modul)
  → IPC v2 öffnen
  → ProcessSupervisor startet Overlay-Prozess
  → Run: ereignisgetrieben; Health-Heartbeat 30 s als einziger Takt
Shutdown (kontrolliert):
  → ShutdownRequested-Event → Overlay sanft beenden → Module StopAsync
    (Fehler je Modul isoliert loggen) → Settings/Audit flushen → exit 0
```

## 5. Was Cardinal (auch in PT2) NICHT tut

- Keine eigenen Berechtigungserweiterungen; Scopes vergibt nur der Host aus
  der Konfiguration (`ModuleRegistry.Register`/`GrantExternalScope`).
- Keine ungefragten Datei-Löschungen/Systemänderungen; kritische Scopes laufen
  immer über die PendingAction-Bestätigung.
- Keine Fachlogik der KI-Module im Kern; Cardinal orchestriert nur.
