# PT2-Architektur

## 1. IST-Architektur (PT1, Stand Branch `nwos-pt2-core`)

```
Install-NWOS.ps1 ──dotnet publish──> %LOCALAPPDATA%\Programs\NWOS
        │                                │
        │ (startet)                      │
        └─> Start-NWOS.bat ──> NWOS.Bootstrap.Windows.exe (STUB: startet nichts)
                                 NWOS.DesktopShell.Ui.exe (WPF-Fenster, inkonsistent)

Komposition existiert, wird aber nicht aufgerufen:
HostComposition.RunAsync
  ├─ ConfigStore / FirstRunWizard ─── nwos.config.json (Lokal)
  ├─ CardinalService ─┬─ ModuleRegistry (+ Scopes)
  │                   ├─ TaskRouter ── PermissionManager ── MessageScopePolicy
  │                   └─ HealthMonitor (Heartbeat 30 s → In-Process-Restart)
  ├─ IpcHostedService → IpcServer (Pipe "NWOS.Cardinal", FRAMING-MISMATCH)
  └─ Module: DesktopShell, Widgets, Alice, Marc, Alya, SystemCenter
             (alle im selben Prozess, je lokale JSON-Persistenz)

Parallele Alt-/Fake-Protokolle (unbenutzt bzw. Fake):
  "NWOS.Cardinal.Pipe" (Bootstrap CardinalIpcServer + Ui CardinalClient)
  "NWOS_Cardinal_Pipe" (DesktopShell CardinalIpcClient – kein Server)
```

Stärken (bleichen): saubere Modulgrenzen (`IModule`), zwingende
Berechtigungsprüfung pro Nachricht, lokale JSON-Persistenz, 9 Test-Suiten,
Fake-freie Fachlogik (echte Fallbacks statt erfundener Daten).
Schwächen: siehe MIGRATION_PT1_PT2.md §1/§4.

## 2. Ziel-Architektur (PT2)

```
Windows
  │
  ├─ NWOS-Installer (nativ, self-contained, kein Admin) ── rollback-fähig
  │
  ├─ NWOS.Cardinal.exe (Kern-Prozess, Headless, überlebt alles andere)
  │    ├─ Cardinal Core: Registry/Router/Permissions/EventBus/Health/Recovery
  │    ├─ Modul-Hosts: Alya, Marc, Alice, Widgets, Boxes, SystemCenter, Settings
  │    ├─ IPC v2 (Pipe "NWOS.Cardinal", framed, versioniert)
  │    ├─ PendingAction-Queue (Nutzer-Bestätigungen)
  │    └─ Audit-Log (persistent, für Alice/Nutzer einsehbar)
  │
  ├─ NWOS.Overlay.exe (eigener Prozess, WPF)
  │    ├─ Desktop-Overlay (borderless, DPI/Multi-Monitor)
  │    ├─ NWOS Widget Window (oben rechts, ein-/ausblendbar)
  │    ├─ Taskbar-Integration: NWOS-Einstiegspunkt im Windows-Tray-Bereich
  │    ├─ NWOS AI-Fenster (Alya/Marc/Alice/Cardinal, Agentenwechsel)
  │    └─ NWOS Settings (15 Bereiche über SettingsStore)
  │    ↕ nur über IPC v2 (Identity: "NWOS.ExternalCaller")
  │
  └─ explorer.exe bleibt Windows-Shell (Shell-Override bleibt bewusste,
     reversible Ausnahme-Option über BootstrapCommands register/restore)
```

### Prozessgrenzen (bewusste Entscheidung)
- **Cardinal-Kern + Fachmodule** bleiben in **einem** Prozess: Module sind
  verwaltete, getestete Klassen; In-Process-Routing + Health-Recovery decken
  den Absturzfall für Logikfehler ab, und Gewinne durch Prozess-Isolation
  jedes Einzelmoduls stünden Ressourcen- und IPC-Kosten gegenüber (§19).
- **Overlay läuft in eigenem Prozess**: WPF/Native-Crashes (Grafiktreiber,
  Hook-Konflikte) dürfen den Kern nicht mitreißen und umgekehrt (§20).
  Cardinal supervisiet den Overlay-Prozess (Start, Heartbeat, Neustart,
  Sitzungs-Restore aus Persistenz).
- Später optional: Agenten-Prozesse hinter `nwos.alya/marc/alice` als
  Erweiterungspunkt, ohne Änderung am Protokoll (gleiche Pipe, gleiche Identität).

### Kommunikationsregeln (unverändert aus PT1, in PT2 erzwungen)
1. Kein Modul ruft ein anderes direkt auf – nur `IModuleMessenger` über Cardinal.
2. Jede Nachricht: Permission-Check (deklarierter Scope ODER
   `MessageScopePolicy.MinimumScopeFor`), dann Zustellung.
3. Externe Prozesse erscheinen immer als `NWOS.ExternalCaller` (Server erzwingt
   die Identität – Lücke aus PT1 wird geschlossen).
4. Kritische Scopes (SystemChange, ProcessControl) → PendingAction statt
   stiller Verweigerung.

## 3. Projektstruktur PT2 (Erweiterung des Bestands)

```
NWOS.sln
src/
├── NWOS.Contracts          (besteht; + Event-Verträge, PendingAction-DTO)
├── NWOS.Cardinal           (besteht; + EventBus, RecoverySupervisor, AuditLog,
│                            PendingActionStore; Fake-Dispatcher entfernt)
├── NWOS.Ipc                (besteht; IPC v2: Framing, Lifecycle, Identity)
├── NWOS.ConfigSetup        (besteht; + GUI-IUserPrompt)
├── NWOS.Settings           (besteht; + Settings-Schema/Bereiche)
├── NWOS.DesktopShell       (besteht; Alt-Clients entfernt; + Box-Messages)
├── NWOS.Widgets            (besteht; + Provider-Push, weitere Typen)
├── NWOS.Alya / NWOS.Marc / NWOS.Alice / NWOS.SystemCenter (bestehen)
├── NWOS.SystemCenter.Windows / NWOS.WindowsIntegration (bestehen)
├── NWOS.Host               (besteht; Program.Main wird echter Einstiegspunkt)
├── NWOS.Bootstrap.Windows  (besteht; Program.Main → BootstrapCommands;
│                            Fake-Server/Welcome entfernt)
├── NWOS.Overlay.Ui         (NEU, net8.0-windows: Overlay, Widget-Window,
│                            Taskbar-Eintrittspunkt, AI-Fenster, Settings-UI)
└── NWOS.Installer          (NEU, ersetzt NWOS.Setup + PowerShell-Installer)
```

PT1-Projekte werden also **nicht umbenannt oder verschoben** – das hält Diffs,
Tests und Migrationsrisiko klein. Die im Auftrag §4 skizzierte Struktur
(Cardinal/Core, Overlay/DesktopShell, AI, Settings, Installer, Security,
Shared) ist über die bestehende Projekt-/Namespace-Aufteilung bereits abgedeckt;
`Overlay/*` bzw. `Installer` kommen als neue Projekte hinzu.

## 4. Laufzeit-Ablauf First Launch (§18)

```
Installer fertig → Autostart/Verknüpfung → NWOS.Cardinal.exe
  → Konfiguration vorhanden? nein → FirstRunWizard (GUI, IUserPrompt)
  → Cardinal init (Registry/Router/Permissions/EventBus/Audit)
  → Module laut Konfiguration starten
  → IPC v2 öffnen, Overlay-Prozess starten (supervised)
  → Overlay: Desktop-Overlay + Widget-Window + Tray-Einstiegspunkt
  → Alya-Begrüßung + Lagebericht (echte Daten via Messenger)
```

Der Boot-Ablauf ist **synchron nur dort, wo nötig** (Konfiguration, IPC bereit);
alles Weitere ereignisgetrieben.
