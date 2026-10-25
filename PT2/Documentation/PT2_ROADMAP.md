# PT2-Roadmap (PT2.1 – PT2.7)

Reihenfolge und Priorität folgen dem Auftrag (§2: Stabilität > Sicherheit >
Benutzerkontrolle > Cardinal > Windows-Integration > Performance > Modularität >
UI > Extras). Jede Phase hat ein hartes Eintrittskriterium: **alle Suiten
grün, keine gelockerte Assertion, keine neue Fake-Funktion.**

## PT2.1 – Cardinal Core stabil (erste Implementierungsphase)

| Aufgabe | Detail |
|---|---|
| Einstiegspunkte verdrahten | `NWOS.Host/Program.cs` und `NWOS.Bootstrap.Windows/Program.cs` rufen die reale Komposition auf (`HostComposition.RunAsync` / `BootstrapCommands.RunAsync`); Fake-Boot-Animation entfernt |
| Fakes entfernen | `CardinalIpcDispatcher` (Cardinal), `CardinalIpcServer` + `WelcomeProtocol` (Bootstrap), `CardinalIpcClient` + `TaskbarAssistantController` + `Programm.cs` (DesktopShell), `NWOS.Setup` |
| IPC v2 | Framing beidseitig, Identitäts-Erzwingung (`ExternalCallerIdentity`), parallele Verbindungen, Timeouts, Fehlerrückkanal (IPC.md §3–5) → `NWOS.Ipc.Tests` 5/5 grün |
| Cardinal-Erweiterung | EventBus, PendingActionStore (TTL), RecoverySupervisor (Backoff/Isolation), AuditLog (JSONL) (CARDINAL.md §3) |
| Tests | Neue Cardinal-/IPC-Tests (TESTING.md §2); Crash-Test auf Suitebene |

**DoD PT2.1:** NWOS startet real (Kern + Module + IPC), IPC-Round-trip inkl.
Deny/Timeout/Parallelität getestet, PendingAction persistiert, Recovery
isoliert ohne Crash-Loop, alle 9+ Suiten grün, NIX neu gefakt.

## PT2.2 – Windows Integration

- `ProcessSupervisor` startet/überwacht den Overlay-Prozess (später PT2.3).
- Autostart nativ (HKCU Run direkt auf die EXE, ohne cmd.exe).
- `ShellIntegration`-TODOs abarbeiten: Rechte-Fehlerbehandlung, End-to-End-
  Verifikation auf Windows (register/restore-Notausgang).
- Systemdatenquellen (WMI/Registry) unverändert wiederverwenden; Fallback-
  Verhalten bleibt getestet.

**DoD:** kontrollierter Prozessstart/-stopp/-neustart von Cardinal aus; Autostart
ohne Skript-Umwege; Shell-Override auf Windows geprüft und reversibel.

## PT2.3 – Overlay (neues Projekt `NWOS.Overlay.Ui`)

- Desktop-Overlay: borderless, `WS_EX_LAYERED|TOOLWINDOW`, Per-Monitor-V2-DPI,
  Multi-Monitor-Vorbereitung, Themes via SettingsStore (OVERLAY.md §3).
- Overlay-Prozess-Supervision über Heartbeat; Crash → Cardinal startet neu,
  Sitzung wird restauriert.
- Kein Dauer-Rendering; IPC-Event-Push statt Timer-Polling.

**DoD:** Overlay läuft als eigener Prozess, überlebt Cardinal-Ausfall bedienbar
(„NICHT verbunden"), Cardinal überlebt Overlay-Crash; DPI/Monitor-Wechsel
erprobt.

## PT2.4 – Widgets & Boxes

- Box-Messages vervollständigen: `box.rename/delete/move/resize/setDisplayMode`
  (+ `MessageScopePolicy`-Einträge); UI-Aktionen im Widget-Window.
- Widget-Framework: Event-Push (`event.widgetUpdated`), Box-/Widget-Ansicht im
  NWOS Widget Window (oben rechts, einblendbar), Drag & Drop (PT1-Muster),
  neue echte Widget-Typen (nur mit echtem Provider).
- Globale Suche an Overlay anbinden (`SearchIndex` existiert).

**DoD:** Boxes/Widgets vollständig per UI bedienbar und persistiert; kein
Polling; Widget-Typ ohne echten Provider ist nicht wählbar.

## PT2.5 – Settings

- Settings-UI (Overlay-Fenster) über `SettingsStore` mit den 15 Bereichen
  (General, Appearance, Desktop, Widgets, Boxes, AI, Cardinal, Security,
  Privacy, Windows Integration, Performance, Maintenance, Multi Device,
  Updates, Permissions, Advanced).
- Bereiche ohne implementierte Funktion zeigen ehrlichen Status („in PT2.x")
  statt Fake-Optionen; Modul-Aktivierung bleibt an `NwosConfiguration` gebunden.
- GUI-`IUserPrompt` für den FirstRunWizard.

**DoD:** Einstellungen persistent, bereichsgetrennt, ohne Fake-Optionen;
First Launch ohne Konsole lauffähig.

## PT2.6 – AI Interface

- AI-Fenster: Chat pro Agent, Statuskarten, Aktionsergebnisse, verständliche
  Fehler, Agentenwechsel, `[Erlauben]/[Ablehnen]`-PendingAction-Karten
  (AI_SYSTEM.md §3).
- Agenten-Statussicht aus HealthMonitor/EventBus; Alice-Ereignisse einblenden.

**DoD:** Nutzer kann mit jedem Agenten interagieren, kritische Aktionen laufen
nur über Bestätigung, nichts simuliert.

## PT2.7 – Installer

- `NWOS.Installer` nativ + self-contained Publish (SDK/Runtime-frei beim
  Nutzer), Rollback-Transaktionslog, Update/Deinstall gemäß INSTALLATION.md.
- Alte PS1-Installer bleiben bis Abnahme als Fallback, danach Archivierung.

**DoD:** saubene-Windows-Installation ohne SDK, Rollback nachgewiesen,
Update + Deinstallation geprüft (§23 Installer Test).

## Gesamt-Definition of Done (Auftrag §25)

- [ ] Cardinal stabil läuft (Start, Health, Recovery, kontrollierter Shutdown)
- [ ] IPC stabil funktioniert (Round-trip, Parallelität, Timeout, Reconnect)
- [ ] Module können isoliert werden (Crash-Test grün)
- [ ] Permission System funktioniert (inkl. IPC-Identität, PendingAction)
- [ ] Windows Integration funktioniert (Autostart, Tray, optionaler Shell-Override)
- [ ] Overlay stabil funktioniert (eigener Prozess, DPI, Multi-Monitor)
- [ ] Widgets funktionieren (echte Provider, Event-Push)
- [ ] Boxes funktionieren (vollständige Messages, Persistenz)
- [ ] Settings funktionieren (15 Bereiche, persistent)
- [ ] Taskbar-Integration funktioniert (Tray-Einstiegspunkt, reversibel)
- [ ] AI Interface funktioniert (4 Agenten, Bestätigungen, Status)
- [ ] Installer funktioniert (self-contained, Rollback)
- [ ] Rollback funktioniert (Installer-Transaktion + Shell-Notausgang)
- [ ] Grundlegende Tests erfolgreich (alle Suiten grün, keine gelockerte Assertion)
- [ ] Keine bekannten kritischen Stabilitätsfehler
- [ ] Ressourcenverbrauch überprüft (nur Health-Takt; Render-/IPC-Last nur bei sichtbaren Flächen)
- [ ] PT1-Funktionen übernommen oder bewusst ersetzt und dokumentiert (MIGRATION_PT1_PT2.md)
