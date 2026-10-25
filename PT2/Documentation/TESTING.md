# PT2-Teststrategie

## 1. Bestand: 9 xUnit-Suiten (alle plattformneutral, laufen in CI/Container)

| Suite | Deckung | IST-Vermerk |
|---|---|---|
| NWOS.Cardinal.Tests | Permission, Router, Health-Recovery-Schwelle, ScopePolicy | grün |
| NWOS.Ipc.Tests | Round-trip, Denied-Roundtrip, Sequenz, „Server weg", Identität | **4/5 rot** (verifiziert; erste PT2.1-Aufgabe: grün machen, ohne Assertions zu lockern) |
| NWOS.Settings.Tests | Store-Isolation pro Modul | grün |
| NWOS.DesktopShell.Tests | Icon-Toggle, Boxen/Persistenz, Suchindex | grün |
| NWOS.Widgets.Tests | Manager/Registry (inkl. unbekannter Typ abgelehnt) | grün |
| NWOS.Alya.Tests | Begrüßung, Lagebericht (Fake-Messenger-Kette) | grün |
| NWOS.Marc.Tests | Organizer, FileTasks, Prozesssteuerung | grün |
| NWOS.Alice.Tests | Modul, Heuristiken, Burst, Audit/Quarantäne | grün |
| NWOS.SystemCenter.Tests | TempCleaner, ProtectedList, Delegation an Marc | grün |

PT2-Regel: **keine bestehende Assertion wird gelockert oder entfernt**; neue
Tests ergänzen, reparierte Fehler müssen den ursprünglich roten Test grün
machen (Regressionsschutz).

## 2. Neue Testziele pro Auftrag §23

### Cardinal (PT2.1)
- EventBus: Publish/Subscribe-Threadsicherheit, keine Handler-Exception reißt Bus.
- PendingActionStore: Lifecycle Approved/Denied/Expired, Persistenz über
  Neustart, TTL.
- RecoverySupervisor: Backoff-Sequenz, Isolation nach Schwellwert, andere
  Module unbeeinflusst (Crash-Test: absichtlich werfendes Modul → Cardinal
  bleibt aktiv – Erweiterung der bestehenden `HealthMonitorTests`).

### IPC (PT2.1)
- Bestehende 5 Tests grün + Neu: Parallelität (N gleichzeitige Clients),
  Stalled-Client blockiert keine andere Verbindung, Lese-/Schreib-Timeouts,
  kontrollierter Shutdown (keine hängenden Tasks), Reconnect-Backoff,
  Frame-Validation (Riesen-/Müll-Frames → Denied statt Crash).

### Overlay / Widgets / Boxes (PT2.3/2.4)
- Overlay-Logik testbar halten: alle Fenster nur Code-Behind-Dünnschicht,
  jede Logik in testbaren ViewModels/Klassen der plattformneutralen Projekte.
- Box-Vollständigkeit: rename/delete/move/resize/DisplayMode-Messages
  (Unit-Tests analog den bestehenden `SmartBoxAndBoxManagerTests`).
- Widget-Push: Event statt Polling (Fake-Notify-Kanal im Test).

### Settings (PT2.5)
- Bereichs-Schema: bekannte Bereiche, Defaults, ungültige Werte abweisen;
  UI liest/schreibt ausschließlich über `SettingsStore`.

### Installer (PT2.7)
- Schrittfolge/Rollback/Idempotenz als plattformneutrale Unit-Tests
  (Transaktions-Log, „existierende Konfiguration bleibt").
- Windows-Prüfliste (manuell, pro Release): saubere VM → Installation →
  First Launch → Autostart → Update → Deinstallation → Shell-Status.

### Crash-Szenarien (§20/§23)
| Test | Erwartung |
|---|---|
| Fachmodul wirft wiederholt | Cardinal aktiv; Modul isoliert; Audit-Eintrag |
| Overlay-Prozess beenden | Cardinal aktiv; Overlay wird neu gestartet; Sitzung restauriert |
| IPC-Verbindung kappen | Client erholt sich (Reconnect); Server resettet Ressourcen |
| Nicht erlaubte Aktion (extern, per IPC mit gefälschter Identität) | **Denied** (Permission-Test auf IPC-Ebene) |

## 3. Ressourcen-Prüfung (§19/§25)

- Kein Dauer-Polling außer Health-Heartbeat (30 s) – Review-Checkliste pro
  Phase.
- Overlay-Render-/IPC-Last nur bei sichtbaren Flächen (Test: verstecktes
  Widget-Window ruft kein `widget.render`).
- Startup-Budget: Module müssen `StartAsync` schnell beenden (bereits
  `IModule`-Vertrag; Test ergänzen).

## 4. Ausführung

- **Linux/Container** (Base44 + CI): `dotnet test` über alle 9 Suiten –
  Deckung der plattformneutralen Kernlogik (der Großteil des Systems).
- **Windows** (Entwickler-VM/Release): zusätzlich Build der `net8.0-windows`-
  Projekte (`EnableWindowsTargeting`), UI-Smoke-Tests, Installer-Prüfliste,
  Shell-Override-End-to-End (Anmeldung/Abmeldung).
