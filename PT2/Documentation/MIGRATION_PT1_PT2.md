# PT2-Migrationsanalyse: PT1 → PT2

Status: **Analyse abgeschlossen, vor Implementierungsbeginn** (Auftrag Abschnitt 26/27).
Alle Befunde sind direkt aus dem PT1-Quellcode des Branches `nwos-pt2-core` belegt;
laufzeitverifizierte Befunde sind als solche markiert.

---

## 1. Aktuelle Architektur (IST)

```
Start (geplant)                    Realität
──────────────────────────────     ─────────────────────────────────────────────
NWOS.Bootstrap.Windows (run)  →    Program.Main = Stub (Fake-Boot-Animation,
  HostComposition.RunAsync          wartet endlos; ruft BootstrapCommands nie auf)
  ├─ ConfigStore/FirstRunWizard     NWOS.Host/Program.Main = Stub (wartet endlos)
  ├─ Cardinal (Registry, Router,
  │  PermissionManager,             → Die reale Komposition HostComposition.RunAsync
  │  HealthMonitor)                 existiert vollständig, wird aber von keinem
  ├─ IpcHostedService (IpcServer)   Einstiegspunkt aufgerufen.
  └─ Module laut Konfiguration:
     DesktopShell, Widgets, Alice,
     Marc, Alya, SystemCenter

Windows-UI (separater Prozess)  →   NWOS.DesktopShell.Ui: WPF-Fenster, 48 px
                                     Topmost-Band, 5 Zonen – XAML und
                                     Code-Behind sind voneinander entfernt
                                     (Detail in §4.9)

Installer                      →   Install-NWOS.ps1 / Uninstall-NWOS.ps1
                                     (PowerShell, baut aus Quellcode beim Nutzer)
                                     + NWOS.Setup (kompiliert nicht) als toter
                                     C#-Versatz
```

Kernprinzipien aus PT1 (bleiben gültig):
- Jede Modulkommunikation läuft über Cardinal (`IModuleMessenger`/`TaskRouter`),
  kein Modul greift ein anderes direkt an.
- Jede Nachricht wird vor Zustellung durch `PermissionManager.Evaluate` geprüft;
  der deklarierte Scope ist nur Untergrenze (`MessageScopePolicy.MinimumScopeFor`).
- Module werden über die Konfiguration aktiviert (`NwosConfiguration`), Scopes
  vergibt ausschließlich der Host (`ModuleRegistry.Register/GrantExternalScope`).
- Alles persistiert lokal als JSON (`ConfigStore`, `SettingsStore`, Boxen,
  Widgets, Icon-Sichtbarkeit, Alice-Auditlog/Quarantäne).
- Fallback-ohne-Fake-Muster: fehlt eine Windows-Datenquelle, liefern Module ein
  explizites „nicht verfügbar" (z. B. `SystemCenterModule.GetStatusAsync`) statt
  erfundener Werte.

## 2. Inventur: vorhandene Komponenten

### Plattformneutrale Bibliotheken (net8.0) – Linux-buildbar/-testbar
| Projekt | Komponenten | Zustand |
|---|---|---|
| `NWOS.Contracts` | `IModule`, `IModuleMessenger`, `ModuleMessage`, `ModuleHealth`, `PermissionScope`, `PermissionDecision`, `NwosJson` | **stabil, getestet** |
| `NWOS.Cardinal` | `CardinalService`, `ModuleRegistry`, `TaskRouter`, `PermissionManager`, `HealthMonitor`, `MessageScopePolicy` | **stabil, getestet** |
| `NWOS.Cardinal` | `CardinalIpcDispatcher` | ❌ Fake-Relikt, Architekturbruch (siehe §4.3) |
| `NWOS.Ipc` | `IpcServer`, `IpcClient`, `PipeFraming` | ❌ **kaputt** (verifiziert, §4.5) |
| `NWOS.ConfigSetup` | `ConfigStore`, `NwosConfiguration`, `FirstRunWizard` + `IUserPrompt`-Abstraktion | stabil, getestet |
| `NWOS.Settings` | `SettingsStore` (Abschnitte pro Modul) | stabil, getestet, UI fehlt |
| `NWOS.DesktopShell` | `DesktopShellModule`, `SmartBox`, `BoxManager`, `SearchIndex`, `IconVisibilityStore`, `DesktopIcon` | stabil, getestet |
| `NWOS.DesktopShell` | `CardinalIpcClient`, `TaskbarAssistantController`, `Programm` | ❌ Fake-/Alt-Relikte (§4.6) |
| `NWOS.Widgets` | `WidgetsModule`, `WidgetManager`, `WidgetRegistry`, `IWidgetProvider`, `ClockWidgetProvider` | stabil, getestet, nur 1 echter Provider |
| `NWOS.Alya` | `AlyaModule`, `GreetingBuilder`, `StatusReportBuilder` | stabil, getestet |
| `NWOS.Marc` | `MarcModule`, `FileOrganizer`, `FileTaskExecutor`, `ProcessController` | stabil, getestet; kein Netzwerk (projektseitig erzwungen) |
| `NWOS.Alice` | `AliceModule`, `HeuristicEngine`, `BurstDetector`, `SecurityAuditLog`, `QuarantineService` | stabil, getestet, event-getrieben |
| `NWOS.SystemCenter` | `SystemCenterModule`, `TempCleaner`, `ProcessLister`, `ProtectedProcessList` | stabil, getestet; Schließ-Delegation an Marc |
| `NWOS.Host` | `HostComposition` (reale Komposition), `IpcHostedService`, `WindowsExtensions` | Komposition korrekt; `Program.cs` = Stub |

### Windows-only (net8.0-windows)
| Projekt | Komponenten | Zustand |
|---|---|---|
| `NWOS.SystemCenter.Windows` | `WmiSystemStatusProvider`, `RegistryAutostartManager` | implementiert, nur auf Windows testbar |
| `NWOS.WindowsIntegration` | `IShellIntegration` + `ShellIntegration` (HKCU-Winlogon-Shell-Override, reversibel) | implementiert, laut eigener Doku **auf Windows ungetestet** |
| `NWOS.Bootstrap.Windows` | `BootstrapCommands` (single-instance Mutex, shell-status/register/restore) | korrekt; `Program.cs` = Stub; `CardinalIpcServer` + `WelcomeProtocol` = Fakes (§4.2) |
| `NWOS.DesktopShell.Ui` | WPF `MainWindow` + ViewModels, `CardinalClient` | ❌ inkonsistent/nicht funktional (§4.9) |
| `NWOS.Setup` | C#-Konsolen-Installer | ❌ kompiliert nicht (Syntaxfehler `-catch` in `Program.cs`) |

### Installer / Skripte
- `Install-NWOS.ps1`, `Uninstall-NWOS.ps1`: durchdacht (Rückfragen, Autostart HKCU,
  Deinstallations-Eintrag, Shell-Registrierung nur auf Wunsch) – aber PowerShell-
  Fundament und baut aus Quellcode mit **.NET SDK beim Nutzer**.
- `Start-NWOS.bat`, cmd.exe-Autostart-Umweg (`cmd.exe /c start "" /min ...`).

### Tests (9 xUnit-Suiten, alle plattformneutral lauffähig)
Cardinal (Permission/Router/Health/ScopePolicy), Ipc, Settings, DesktopShell,
Widgets, Alya, Marc, Alice, SystemCenter. Aktueller Stand der `NWOS.Ipc.Tests`:
**4/5 FEHLGESCHLAGEN** (verifiziert im Container-Lauf; Details §4.5).

## 3. Wiederverwendbare Komponenten (KEEP)

1. **`NWOS.Contracts` komplett** – Schnittstellen sind bereits das PT2-Modellmodell.
2. **Cardinal-Kern** (`CardinalService`, `ModuleRegistry`, `TaskRouter`,
   `PermissionManager`, `HealthMonitor`, `MessageScopePolicy`) inkl. Tests –
   Grundlage für PT2.1; wird um Event-System, Recovery-Supervisor, persistente
   Audit-Log und Bestätigungs-Workflow **erweitert**, nicht ersetzt.
3. **`PipeFraming`** – Längen-Präfix + 16-MB-Grenze + `ReadExactAsync` ist das
   richtige Framing; der Server muss es nur auch benutzen (§4.5).
4. **Fachmodule DesktopShell/Widgets/Alice/Marc/Alya/SystemCenter** samt ihrer
   Persistenz- und Domain-Klassen und ihrer Tests – FT-Deckung für PT2.4/2.5/2.6.
5. **`SettingsStore`** – Abschnittsmodell passt zum geplanten NWOS-Settings-Baum.
6. **`FirstRunWizard` + `IUserPrompt`** – PT2-GUI implementiert nur einen zweiten
   `IUserPrompt`, der Wizard selbst bleibt.
7. **`BootstrapCommands`** (Single-Instance-Mutex, shell-status/register/restore
   mit getippter Bestätigung) – wird der neue `Program.Main`-Pfad.
8. **`ShellIntegration`** (reversibler HKCU-Shell-Override) – bleibt, inkl. der
   offenen TODOs (Rechte-Fehlerbehandlung, echte Windows-Tests).
9. **Installer-Konzepte aus `Install-NWOS.ps1`** (Ablauf, Rückfragen, Reversibilität,
   Deinstallations-Eintrag) – werden 1:1 als Anforderungen in den nativen
   PT2-Installer übernommen (INSTALLATION.md).

## 4. Problematische Komponenten (REPLACE/REMOVE) – mit Beleg

### 4.1 Einstiegspunkte sind Stubs → NWOS startet nicht
`NWOS.Host/Program.cs` und `NWOS.Bootstrap.Windows/Program.cs` drucken zwei Zeilen
und warten endlos (`Task.Delay(-1)`); die reale Komposition wird nie aufgerufen.
Das README („Program.cs ruft das ohne Windows-Erweiterungen auf", „Bootstrap =
der tatsächliche Windows-Einstiegspunkt") beschreibt einen älteren Zustand.
**Folge:** Der PT1-Installer installiert und startet Binaries, die NWOS nicht
hochfahren lassen. Priorität-0-Fix in PT2.1.

### 4.2 `NWOS.Bootstrap.Windows`: Fake-Server + Fake-Willkommen
`CardinalIpcServer` (369 Zeilen): in-memory Boxen/Widgets/Tasks mit hartcodierten
Antworten („CPU: 12%"), handgebautem JSON per String-Konkatenation, eigenem
line-basierten Protokoll auf Pipe `NWOS.Cardinal.Pipe`, ohne jede Berechtigungsprüfung.
`WelcomeProtocol` liefert ein erfundenes Update-Log. Beides verstößt direkt gegen
„keine Fake-Implementierung als fertig deklariert" (Auftrag §2/§40-Äquivalent).
**Entscheidung: löschen**, sobald PT2-IPC den Bedarf (UI-Anbindung) echt deckt.

### 4.3 `NWOS.Cardinal/CardinalIpcDispatcher` – Fake + Architekturbruch
Gibt hartcodierte Antworten aus („[Marc] Prozess-Aktion ausgeführt") **und**
referenziert `NWOS.Alya/Marc/Alice` – damit verletzt Cardinal seine eigene
Richtungsregel (Modulbibliotheken dürfen nicht vom Kern abhängen, Kern nie von
Modulen). **Entscheidung: löschen**; ist mit keiner echten Laufzeit verkabelt.

### 4.4 `NWOS.DesktopShell`: Alt-Clients
`CardinalIpcClient` (Pipe `NWOS_Cardinal_Pipe`, drittes Protokoll, JSON per
String-Bau, `"target"/"action"/"payload"`), `TaskbarAssistantController`
(hartcodierte Textantworten), `Programm.cs` (Demo-Stub). Niemand bedient
`NWOS_Cardinal_Pipe`. **Entscheidung: löschen; UI spricht künftig über `NWOS.Ipc.IpcClient`.**

### 4.5 Reales IPC ist nachweislich defekt (verifiziert)
Testlauf `dotnet test tests/NWOS.Ipc.Tests` im SDK-Container: **4 von 5 Tests fehl**:
- `Client_request_reaches_server_side_messenger_unchanged` → `ModuleFailure`
- `Denied_outcome_and_reason_survive_the_round_trip` → `ModuleFailure`
- `Multiple_sequential_requests_are_each_handled_correctly` → `ModuleFailure`
- `Server_overwrites_claimed_source_module_with_external_identity` → NRE, weil
  die Nachricht den Messenger nie erreicht

Ursachen im Code:
- **Framing-Mismatch:** `IpcClient.SendAsync` schreibt via `PipeFraming`
  (4-Byte-Länge + JSON); `IpcServer.HandleClientAsync` liest dagegen roh
  `JsonSerializer.DeserializeAsync<ModuleMessage>(stream)` – das erste, was es
  liest, sind die 4 Längen-Bytes → JsonException → **keine Antwort**, Client
  scheitert/verhungert.
- **Identitäts-Spoofing-Lücke:** der Server **überschreibt** die vom Client
  behauptete `SourceModuleId` nicht mit `IpcDefaults.ExternalCallerIdentity`
  (grep-belegt: Konstante wird nur in `HostComposition` verwendet). Der Host
  vergibt External-Scopes an diese Identität, aber ein Client könnte sich als
  `nwos.alice` o. ä. ausgeben. `IpcIdentityTests` kodiert bereits die richtige
  Anforderung – nur der Server hält sie nicht ein.
- **Keine Verbindungs-Parallelität:** Pipe mit `maxNumberOfServerInstances: 1`,
  `HandleClientAsync` blockiert den Accept-Loop → eine hängende Verbindung
  legt den kompletten externen Zugriff lahm.
- **Keine Timeouts/Reconnects** pro Verbindung; Fehler werden nur geloggt, der
  Client erhält keine Fehlernachricht.
Das ist exakt das vom Auftrag (§5) beschriebene PT1-IPC-Problemfeld.

### 4.6 `NWOS.Setup` kompiliert nicht + Duplikat
Syntaxfehler `-catch (Exception ex)` in `Program.cs`; Inhalt dupliziert die
PowerShell-Installer-Logik schlechter (Autostart via `cmd.exe /c start`, fixed
Version 0.2.0, kein Rollback). **Entscheidung: löschen**; ersetzt durch den
nativen PT2-Installer (INSTALLATION.md).

### 4.7 Bestätigungs-Pfad existiert nicht
`TaskRouter` liefert für SystemChange/ProcessControl korrekt
`PendingUserConfirmation` – **aber es gibt keinen Verbraucher**: keine Queue,
keine UI-Abfrage, keine TTL; die Aktion verfällt kommentarlos. Für PT2 braucht
Cardinal einen persistierten Bestätigungs-Workflow (CARDINAL.md §4).

### 4.8 Health-Recovery nur in-process
Alle Module laufen im Cardinal-Prozess. `HealthMonitor` startet nach 3 Fehlern
das Modulobjekt neu – das hilft bei Logikfehlern, **nicht** bei Prozessabsturz
(WPF-UI, künftig ggf. Agenten-Prozesse). Ein Overlay-Crash reißt heute Cardinal
mit. PT2: Overlay als separater, von Cardinal überwachter Prozess (OVERLAY.md).

### 4.9 `NWOS.DesktopShell.Ui`: XAML ≠ Code-Behind
XAML beschreibt ein 48-px-Topmost-Taskband mit 5 Zonen und Bindings auf
`Zone1ViewModel..Zone5ViewModel`; der Code-Behind füllt dagegen eigene
Collections (`_icons`, `_boxes`, `_widgets`) in ItemsControls, die im XAML
`Visibility="Collapsed"` bzw. `Width=0 Height=0` sind (`WidgetTypeComboBox`,
`SearchResultsList`). `App.xaml.cs` existiert, aber die ViewModels im
`ViewModels/`-Ordner und die MainWindow-Logik stammen aus zwei verschiedenen
UI-Generationen. Das Fenster ist so **nicht bedienbar**. **Entscheidung:** als
PT2-Overlay komplett neu aufgebaut (OVERLAY.md), ViewModels/Domain-Logik aus
`NWOS.DesktopShell`/`NWOS.Widgets` werden weiterverwendet.

### 4.10 Widget-Rendering per Polling
Die UI rendert Widgets per 1-s-`DispatcherTimer` (Polling). Bricht mit
PT2-Leitlinie „event-driven, kein Polling". PT2: Render-Push über IPC-Events
(IPC.md §4), nur sichtbare Widgets.

### 4.11 PowerShell/cmd-Startketten
Autostart via `cmd.exe /c start`, Launcher via `Start-NWOS.bat`,
Install via `winget`-SDK-Installation beim Nutzer. **Entscheidung:** native
Prozesse und Registry-Einträge; PowerShell nur noch optionale
Verwaltungsschnittstelle (Auftrag §6).

## 5. Notwendige Umbauten (Zusammenfassung, priorisiert)

| P | Umbau | Auftrag |
|---|---|---|
| 0 | `Program.Main` von Host & Bootstrap auf die reale Komposition verdrahten | §3, §18 |
| 0 | `NWOS.Setup` entfernen (kompiliert nicht, Duplikat) | §2 |
| 1 | IPC v2: Server nutzt `PipeFraming`, Identitäts-Überschreibung, parallele Verbindungen, Timeouts, Reconnect, Fehlerrückantworten | §5 |
| 1 | Fake-Komplexe entfernen: `CardinalIpcDispatcher`, Bootstrap-`CardinalIpcServer`, `WelcomeProtocol`, DesktopShell-Alt-Clients, `TaskbarAssistantController`, `Programm.cs` | §2, §5 |
| 1 | Event-System + Bestätigungs-Queue + persistente Audit-Log in Cardinal | §3, §15 |
| 2 | Overlay-Prozess neu (borderless, DPI, Multi-Monitor, Themes) mit Supervision über IPC | §8 |
| 2 | Taskbar-Integration (Tray-Eintrittspunkt statt Systemeingriff) | §12 |
| 3 | Widget-Framework: Provider-Push, Box-/Widget-Kopplung, neue Typen | §9, §10 |
| 4 | Settings-UI über `SettingsStore` (15 Bereiche) | §11 |
| 5 | KI-Fenster + Agentenwechsel + Permission-Dialoge | §13, §14 |
| 6 | Nativer Installer + Self-contained Deployment + Rollback | §7, §16, §17 |
| — | Box-Vollständigkeit: rename/delete/position/size als MessageTypes (BoxManager kann es teils schon, Modul nicht) | §10 |

## 6. Abhängigkeiten (IST → PT2)

- **.NET 8** – einzige Laufzeit; PT1 verlangt zusätzlich das **SDK beim Nutzer**
  (Installer baut aus Quellcode). PT2: self-contained Publish pro Binary
  (`dotnet publish -r win-x64 --self-contained`), SDK nur noch für Entwicklung.
- **PowerShell** – nur Installer/Launcher (s. §4.11); Fachcode ist bereits C#.
  PT2 reduziert PowerShell auf „optionale Verwaltungsschnittstelle".
- **WPF** – `net8.0-windows`; für Overlay/Widget-Fenster/KI-Fenster/Settings-UI.
  Keine dritte UI-Engine nötig (Auftrag §7: nichts zwanghaft entfernen).
- **Windows-APIs** – Registry (Autostart, Uninstall, Winlogon-Shell), WMI
  (Systemstatus), Named Pipes, COM (Verknüpfungen). PT2 ergänzt: Tray-API
  (Shell_NotifyIcon), Per-Monitor-V2-DPI, Multi-Monitor (EnumDisplayMonitors),
  Fenster-Layer (WS_EX_LAYERED/TOOLWINDOW/NOACTIVATE) – Win32 via P/Invoke oder
  `Microsoft.Windows.CsWin32`.
- **NuGet-Pakete:** praktisch none (System.Text.Json, Microsoft.Extensions.*,
  xUnit) – bewusst so belassen (geringe Angriffsfläche, Auftrag §19).
- **Externe Dienste/Netzwerk:** keine. Keine Secrets erforderlich.

## 7. Migrationsplan (OLD → NEW → WHY → MIGRATION)

| OLD | NEW | WHY | MIGRATION |
|---|---|---|---|
| Stub `Program.Main` (Host, Bootstrap) | `Program.Main` → `BootstrapCommands.RunAsync` / `HostComposition.RunAsync` | Ohne Verdrahtung startet NWOS gar nicht | 1:1-Verdrahtung + Bootstrapping-Tests; kein Datenumzug |
| `IpcServer` (rohes JSON, 1 Verbindung, keine Identität) | IPC v2: `PipeFraming` beidseitig, Identitäts-Überschreibung auf `ExternalCallerIdentity`, parallele Instances, Lese-/Schreib-Timeouts, Fehlerrückkanal, Heartbeat | Auftrag §5; Round-trip heute nachweislich kaputt; Spoofing-Lücke | Protokoll hat SchemaVersion-Feld; `ModuleMessage.SchemaVersion=1` bleibt lesbar. Bestehende Tests werden grün ergänzt (nicht gelockert). |
| `CardinalIpcServer` (Bootstrap, Fake) / `CardinalIpcClient` (DesktopShell, Fake) / Ui-`CardinalClient` (Alt-Protokoll) | **gelöscht**; einzige UI-Anbindung: `NWOS.Ipc.IpcClient` | Drei konkurrierende Protokolle, zwei davon Fake, alle ohne Permission-Check | UI-Call-Stellen (`MainWindow`, Taskbar-Controller) auf `IpcClient`/`IModuleMessenger` umstellen; es existiert kein Persistenzformat, kein Datenumzug |
| `CardinalIpcDispatcher` (Cardinal→Modul-Fake) | **gelöscht** | Kern darf Module nicht referenzieren; Antworten waren erfunden | Kein Konsument im realen Pfad; Tests existieren nicht dazu |
| `TaskRouter.PendingUserConfirmation` ohne Verbraucher | Persistente `PendingAction`-Queue in Cardinal + UI-Bestätigungsdialog + TTL + Audit-Eintrag | Kritische Aktionen dürfen nicht still verfallen (§15) | Ergebnis-Typ bleibt; Queue neu; `RoutingOutcome.PendingUserConfirmation` bleibt bedeutungsgleich |
| UI-Polling (1-s-Timer) | Event-Push über IPC (widget.updated, box.updated, health.changed) | §19 Performance | `widget.render`-MessageType bleibt für on-demand; Timer entfällt |
| WPF-`MainWindow` (48-px-Band, inkonsistent) | Overlay-Prozess: Desktop-Overlay + Widget-Window + Taskbar-Einstiegspunkt + KI-Fenster + Settings-Fenster | §8/§9/§11/§12/§13 | Neubau in `NWOS.Overlay.Ui`; Wiederverwendung der Domain-Logik (`SmartBox`, `WidgetManager`, `SearchIndex`, `SettingsStore`, `IconVisibilityStore`); Persistenzformate bleiben |
| `Install-NWOS.ps1`/`Start-NWOS.bat`/`NWOS.Setup` | Nativer Installer (C#) + self-contained Publish; Uninstall bleibt App-Eintrag | §6/§7/§16/§17; SDK-los, kein cmd-Umweg, Rollback | Erst Installation unter Windows verifizieren; Deinstallationsskript-Logik wird 1:1 in C# nachgebaut (inkl. Shell-Restore-Notausgang) |
| In-process-Module | Kern bleibt in-process; Overlay (und später ggf. Agenten-HOSTS) als überwachte Prozesse | §20: Overlay-Crash darf Cardinal nicht mitreißen | `IModule` unverändert; Overlay-Prozess erscheint als externer IPC-Client + Health-Heartbeat; Recovery startet den Prozess neu |
| `HealthMonitor` (Neustart ohne Backoff) | Recovery-Supervisor: Backoff, isolierter Zustand (`ModuleStatus.Disabled`), Audit | §20/§5 Crash-Loops | Schwelle (3) bleibt; Zustandsmaschine erweitert |

**Bewusst NICHT ersetzt:** `NWOS.Contracts`, Cardinal-Kernklassen, alle Fachmodule,
Persistenzformate (JSON), Test-Suiten, `ShellIntegration`, `BootstrapCommands`,
Settings-/Config-Store. PT1-Konzepte (Cardinal, Alya, Marc, Alice, Smart Boxes,
Widgets, Search, System Center, Windows Integration, IPC, Permissions) bleiben
vollständig erhalten – nur die Fake-/Alt-Implementierungen werden entfernt und
die genannten Lücken geschlossen.
