# NWOS – Installation (schnellster Weg)

Voraussetzung: Windows 10/11. Keine Administratorrechte nötig (Installation nur für den aktuellen Benutzer).

1. ZIP vollständig entpacken (der Ordner mit `NWOS.sln` und `NWOS-Setup.bat`).
2. **`NWOS-Setup.bat` doppelklicken.** Der Installer
   - prüft/installiert das .NET-8-SDK (per `winget`, mit Rückfrage),
   - baut NWOS aus dem Quellcode und installiert nach `%LOCALAPPDATA%\Programs\NWOS`,
   - legt die Konfiguration an (alle implementierten Module aktiv; Rückfrage möglich),
   - erstellt Verknüpfungen (Desktop + Startmenü) und – auf Wunsch – den Autostart,
   - trägt die Deinstallation in Windows „Apps“ ein und startet NWOS.
3. Deinstallieren: Windows-Einstellungen → Apps → „NWOS“, oder `NWOS-Uninstall.bat`.

Optionen (PowerShell): `.\Install-NWOS.ps1 -Quiet` (keine Rückfragen), `-RunTests` (erst Tests, Abbruch bei Fehlern),
`-NoAutostart`, `-NoLaunch`, `-InstallDir <Pfad>`, `-OfferShellRegistration`.

**Bewusst nicht automatisch:** die Registrierung als Windows-Shell. Sie ersetzt beim Login den normalen Desktop und
sollte zuerst in einer VM getestet werden (`NWOS.Bootstrap.Windows.exe register-shell` / Notausgang `restore-shell`).

**Ehrlicher Hinweis:** Der Installer ist ein Skript, das auf DEINEM Rechner aus dem Quellcode baut – keine vorkompilierte
Setup-EXE. Der gesamte Code (inkl. Installer) wurde in einer Umgebung ohne .NET/Windows geschrieben und noch nie
kompiliert oder ausgeführt. Erste Build-Fehler sind daher möglich; `-RunTests` zeigt sie früh.

---

# NWOS – Phase 1 + Phase 2 (Grundgerüst)

Dieses Projekt setzt Abschnitt 13 ("Realistischer Entwicklungsplan") und
Abschnitt 44 ("Erste Aufgabe") aus dem Master-Entwicklungsauftrag um: zuerst
den **kleinsten stabilen NWOS-Kern** (Phase 1), jetzt erweitert um das
**UI-Datenmodell aus Phase 2** (Desktop-Icons, Smart Boxes, globale Suche,
zentrale Einstellungen) - bewusst als reine Logik-/Datenschicht, noch ohne
grafische Darstellung (siehe Abschnitt "Was bewusst NICHT enthalten ist").

## Was ist enthalten

| Projekt | Inhalt |
|---|---|
| `NWOS.Contracts` | Modul-Schnittstelle (`IModule`), Nachrichtenformat, Berechtigungs- und Health-Modelle. Plattformneutral. |
| `NWOS.Cardinal` | `ModuleRegistry`, `PermissionManager`, `TaskRouter`, `HealthMonitor`, `CardinalService`. Das eigentliche "Herz" (Abschnitt 5). Plattformneutral, läuft auch ohne Windows. |
| `NWOS.ConfigSetup` | Datenmodell für das Konfigurationsformular (Abschnitt 4), lokale JSON-Persistenz (Abschnitt 34), UI-agnostischer Ersteinrichtungs-Wizard. |
| `NWOS.WindowsIntegration` | Schnittstelle + minimale, **ungetestete** Implementierung für die Windows-Shell-Registrierung (Pro-Benutzer-Override, reversibel). `net8.0-windows`. |
| `NWOS.Host` | Plattformneutrale Komposition: `HostComposition.RunAsync` lädt/erfasst Konfiguration, startet Cardinal, registriert alle Module gemäß Konfiguration. `Program.cs` ruft das ohne Windows-Erweiterungen auf (Standard-Einstiegspunkt); `NWOS.Bootstrap.Windows` ruft dieselbe Methode MIT echten Windows-Datenquellen auf (`WindowsExtensions`) - keine doppelte Registrierungslogik. |
| `NWOS.Cardinal.Tests` | xUnit-Tests für Berechtigungsprüfung, Routing, Absturzisolation/Recovery-Schwelle. |
| `NWOS.Settings` | Zentrales, modulübergreifendes Einstellungszentrum (Abschnitt 33), lokal persistiert, pro Modul isolierte Abschnitte. |
| `NWOS.DesktopShell` | Icon-Sichtbarkeits-Toggle ohne Veränderung von Position/Verknüpfung (Abschnitt 16), Smart Boxes inkl. box-interner Suche (Abschnitt 17/19, noch ohne Passwortschutz), globaler lokaler Such-Index (Abschnitt 20). Implementiert `IModule` und registriert sich als `nwos.desktopshell`. |
| `NWOS.DesktopShell.Tests`, `NWOS.Settings.Tests` | xUnit-Tests für Icon-Toggle-Verhalten, Box-Persistenz, Suchindex-Korrektheit, Settings-Isolation pro Modul. |
| `NWOS.Widgets` | Erweiterbares Widget-System (Abschnitt 21): `IWidgetProvider` als Erweiterungspunkt, `WidgetRegistry` für Typen, `WidgetManager` für platzierte Instanzen (verschiebbar/skalierbar/aktivierbar), eingebauter, echter `ClockWidgetProvider` als Funktionsbeweis. Registriert sich als `nwos.widgets`. |
| `NWOS.Widgets.Tests` | xUnit-Tests inkl. Nachweis, dass Instanzen nicht registrierter Widget-Typen abgelehnt werden. |
| `NWOS.DesktopShell.Ui` | Erstes sichtbares NWOS-Fenster (WPF, `net8.0-windows`). Zeigt echte Desktop-Icons aus `%USERPROFILE%\Desktop` mit Doppelklick-Sichtbarkeits-Toggle (Abschnitt 16), Smart Boxes mit Hover-/Static-Modus und echtem Datei-Drag&Drop (Abschnitt 17/18), eine Widget-Leiste mit dynamischer Typ-Auswahl aus `WidgetRegistry` (Abschnitt 21) und eine Suchleiste über `SearchIndex` (Abschnitt 20). Bindet die Phase-2-Klassen bewusst direkt im selben Prozess ein - siehe Einschränkung unten. |
| `NWOS.Alice` | Sicherheits-KI (Abschnitt 8): `FileSystemWatcher`-basierte, passive Überwachung eines konfigurierten Ordners, transparentes Regelwerk (`HeuristicEngine`: getarnte Doppel-Endungen, neue .exe in Downloads), `BurstDetector` für ungewöhnlich viele Dateiänderungen, append-only `SecurityAuditLog`, reversible `QuarantineService`. Automatische Aktion nur für die eine, eng gefasste "eindeutig sicher automatisierbare" Regel (Abschnitt 8) - alles andere bleibt protokolliert, nicht automatisch gehandelt. |
| `NWOS.Marc` | Arbeits-KI (Abschnitt 9): `FileOrganizer` (Dateien nach Typ einsortieren), `FileTaskExecutor` (Ordner erstellen, Dateien umbenennen), `ProcessController` (Programme starten/geordnet schließen). Besitzt bewusst keinerlei Netzwerk-Referenz im Projekt - die Internet-Regel aus Abschnitt 9 ist damit auch auf Code-Ebene erzwungen, nicht nur über `PermissionManager`. |
| `NWOS.Alya` | Benutzerkontakt-KI (Abschnitt 10): tageszeitabhängige, tonabhängige Begrüßung, personalisierbarer Ton über `NWOS.Settings`, Lagebericht (Abschnitt 12) über `IModuleMessenger` - fragt Alice nach den letzten Sicherheitsereignissen, ausschließlich über Cardinal vermittelt, nie direkt. Besitzt keinerlei Datei-/Prozess-/Netzwerk-Fähigkeiten (kein entsprechendes Projekt referenziert). |
| `NWOS.Alice.Tests`, `NWOS.Marc.Tests`, `NWOS.Alya.Tests` | xUnit-Tests je Modul, inkl. eines Fake-Messengers für Alya, der die reale Kommunikationskette Alya → Cardinal → Alice ohne echten Cardinal-Prozess nachbildet. |
| `NWOS.SystemCenter` | Systemstatus/Optimierung (Abschnitt 24/25), plattformneutraler Kern: `TempCleaner` (sichere Bereinigung, überspringt gesperrte Dateien), `ProtectedProcessList` (Denyliste kritischer Prozessnamen), `ProcessLister`. Schließt nie selbst einen Prozess - delegiert über `IModuleMessenger` an Marc, nach lokaler Schutzlisten-Prüfung. |
| `NWOS.SystemCenter.Windows` | Echte Datenquellen: `WmiSystemStatusProvider` (CPU/RAM/Disk über WMI), `RegistryAutostartManager` (Autostart über den Run-Key, reversibel deaktivierbar). `net8.0-windows`, gleiches Muster wie `NWOS.WindowsIntegration`. |
| `NWOS.SystemCenter.Tests` | xUnit-Tests für `TempCleaner`, `ProtectedProcessList` und `SystemCenterModule` (inkl. Nachweis, dass eine Prozess-Schließen-Anfrage tatsächlich an Marc delegiert wird). |
| `NWOS.Bootstrap.Windows` | **Der tatsächliche Windows-Einstiegspunkt.** Verdrahtet `WmiSystemStatusProvider` und `RegistryAutostartManager` in `HostComposition` (siehe unten) und stellt drei Subcommands bereit: `shell-status`, `register-shell` (nur nach getippter Bestätigung `BESTAETIGEN` - Abschnitt 41) und `restore-shell` (ohne Bestätigung - der Sicherheits-Ausweg). Ohne Argument bzw. mit `run`: normaler NWOS-Start mit echten Windows-Datenquellen. |
| `NWOS.Ipc` | Cross-Process-IPC (Abschnitt 7): `IpcServer` (nimmt Verbindungen auf einer lokalen Named Pipe entgegen, reicht jede Nachricht an den lokalen `IModuleMessenger`/Cardinal weiter) und `IpcClient` (implementiert selbst `IModuleMessenger` - ein externer Prozess sendet Nachrichten genau wie ein In-Process-Modul). Bewusst plattformneutral (`System.IO.Pipes` funktioniert auch außerhalb von Windows), läuft automatisch mit, sobald Cardinal startet (`IpcHostedService` in `NWOS.Host`). |
| `NWOS.Ipc.Tests` | Echte Ende-zu-Ende-Tests über eine reale lokale Pipe (kein Mock) - Server und Client laufen im selben Testprozess, aber über echtes `NamedPipeServerStream`/`NamedPipeClientStream`. |

## `NWOS.DesktopShell.Ui` und Cardinal

Die UI läuft als **eigener Prozess** und spricht mit dem laufenden Cardinal (`NWOS.Bootstrap.Windows`) ausschließlich
über `NWOS.Ipc` (Named Pipe). Sie referenziert weder `NWOS.DesktopShell` noch `NWOS.Widgets`: Icon-Sichtbarkeit, Boxen,
Widgets und Suchindex liegen in Cardinal und werden dort nach jeder Änderung sofort gespeichert. Läuft Cardinal nicht,
zeigt der Fenstertitel „NICHT verbunden“ (kein Absturz). `Start-NWOS.bat` startet beide in der richtigen Reihenfolge.

**Sicherheitsmodell für externe Aufrufer (neu):**
- `IpcServer` überschreibt `SourceModuleId` immer mit `external.ipc` – ein Client kann sich nicht als Alice/Marc ausgeben.
- `external.ipc` hat nur `FileSystemRead|FileSystemWrite` (`HostComposition`) – kein Prozess-/System-/Netzwerkzugriff.
- Der nötige Scope ergibt sich zusätzlich aus dem MessageType (`MessageScopePolicy`); ein Absender kann durch die
  Deklaration `PermissionScope.None` die Prüfung nicht mehr umgehen. Tests: `MessageScopePolicyTests`, `IpcIdentityTests`.
- Restrisiko: Die Pipe ist nicht zusätzlich per ACL auf den Benutzer beschränkt; jeder lokale Prozess desselben Benutzers
  kann sich verbinden (dann aber nur mit den Rechten von `external.ipc`). TODO Phase 7.


## Was bewusst NICHT enthalten ist

Gemäß Abschnitt 13 und Abschnitt 40 (keine Fake-Funktionen) wurde alles
weggelassen, was noch nicht ehrlich funktionsfähig implementiert werden kann:

- **Startsequenz und dynamische Hintergründe** (Abschnitt 11/13/14) – eigene
  Phasen (5), noch nicht begonnen.
- **Passwortschutz für Boxen** – bewusst noch nicht (siehe TODO in `SmartBox.cs`).
- **Verschieben/Skalieren von Icons und Boxen per Maus** – aktuell feste
  Layouts (`WrapPanel`/`StackPanel`); echtes Positionieren mit Speicherung
  ist ein TODO, das die vorhandenen `PositionX`/`PositionY`-Felder in
  `SmartBox`/`WidgetInstance` bereits vorbereiten.
- **Cross-Process-IPC zwischen `NWOS.DesktopShell.Ui` und `NWOS.Cardinal`** – siehe Abschnitt oben.
- **Automatischer Aufruf der Windows-Shell-Registrierung** – existiert als
  Code (`ShellIntegration`), wird aber von `NWOS.Host` nicht automatisch
  aufgerufen. Das ist Absicht (Abschnitt 41: Sicherheit vor Komfort) und weil
  dieser Code in dieser Umgebung nicht getestet werden konnte (siehe unten).

## Wichtige Einschränkung dieser Entwicklungsumgebung

Der Code wurde in einer **Linux-Sandbox ohne .NET-SDK und ohne
Internetzugang** geschrieben – hier konnte **nichts kompiliert oder
ausgeführt werden**. Das betrifft besonders `NWOS.WindowsIntegration`, das
echte Windows-Registry-APIs (`Microsoft.Win32.Registry`) verwendet.

**Vor dem ersten echten Einsatz zwingend nötig:**

```bash
dotnet build NWOS.sln
dotnet test tests/NWOS.Cardinal.Tests/NWOS.Cardinal.Tests.csproj
dotnet run --project src/NWOS.Host/NWOS.Host.csproj
```

auf einer echten Windows-Maschine mit .NET 8 SDK, inklusive:

- Kontrolle, ob alle NuGet-Paketversionen (`Microsoft.Extensions.Hosting 8.0.0`
  usw.) noch aktuell/verfügbar sind.
- End-to-End-Test von `ShellIntegration` **in einer Wegwerf-VM**, nicht auf
  einem produktiv genutzten Windows-System – inklusive Test von
  `RestoreDefaultShellAsync`, bevor `RegisterNwosShellAsync` überhaupt in
  Betracht gezogen wird.

## Status und nächster Schritt

Abgeschlossen: sichtbarer Verifikationsbau, Phase 3 (Alice/Marc/Alya),
Phase 4 (System Center) und jetzt das **Windows-Bootstrap-Projekt**
(`NWOS.Bootstrap.Windows`) - der erste Einstiegspunkt, der tatsächlich alle
bisherigen Windows-spezifischen Fähigkeiten zusammenführt: echter
WMI-Systemstatus, echte Registry-Autostart-Verwaltung, und die
Shell-Registrierung (`ShellIntegration`) hinter einer expliziten,
getippten Bestätigung.

Dafür wurde `NWOS.Host.Program.cs` in eine wiederverwendbare
`HostComposition.RunAsync(args, windowsExtensions)`-Methode extrahiert
(Abschnitt 39: bestehenden Code erweitern statt duplizieren) - der
plattformneutrale `NWOS.Host` ruft sie ohne Windows-Erweiterungen auf,
`NWOS.Bootstrap.Windows` mit echten. Keine der bisherigen Modul-
Registrierungslogik musste dafür kopiert werden.

**Wichtiger Fix beim Review von Phase 4 gefunden und behoben:** alle Module
serialisierten ihre Antwort-Payloads bis dahin mit .NETs Standard-JSON-
Einstellungen (PascalCase-Feldnamen), obwohl jede MessageType-Dokumentation
und mehrere Tests durchgängig camelCase voraussetzen (z. B. `"ruleId"` statt
`"RuleId"`). Das hätte die gesamte Alya-→-Cardinal-→-Alice-Kommunikationskette
(Lagebericht) im echten Betrieb stillschweigend brechen können, obwohl die
Tests dafür (mit direkten `HandleMessageAsync`-Aufrufen ohne Serialisierung)
grün gewesen wären. Behoben durch eine zentrale `NWOS.Contracts.NwosJson`
mit `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, die jetzt jedes
Modul beim Serialisieren seiner Antworten verwendet.

**Jetzt, mit dem Bootstrap-Projekt, ist ein echter End-to-End-Test auf
Windows endlich vollständig möglich.** Empfohlene Reihenfolge:
1. `dotnet build NWOS.sln` und `dotnet test` auf echtem Windows mit .NET 8 SDK.
2. `NWOS.Bootstrap.Windows` OHNE Argument starten (= `run`) - normaler Start,
   NICHT als Shell registriert. Prüfen: startet Cardinal, werden alle
   konfigurierten Module registriert (Log-Ausgabe), liefert
   `systemcenter.getStatus` plausible CPU-/RAM-/Disk-Werte?
3. `shell-status` aufrufen (sollte "nicht registriert" zeigen).
4. **Nur in einer Wegwerf-VM, nie auf einer produktiv genutzten Maschine:**
   `register-shell` mit der getippten Bestätigung testen, abmelden/anmelden,
   prüfen ob NWOS tatsächlich anstelle von explorer.exe startet, dann sofort
   `restore-shell` testen und erneut an-/abmelden, um die Reversibilität zu
   bestätigen, BEVOR irgendetwas davon als verlässlich gilt.
5. Danach erst die übrigen Modul-Funktionen (Alice-Quarantäne, Marc-Datei-
   organisation, Widget-Anzeige über `NWOS.DesktopShell.Ui`, ...) durchklicken.

**Jetzt neu: `NWOS.Ipc`, der Cross-Process-Transport (Abschnitt 7).**
`IpcServer` läuft automatisch mit, sobald Cardinal startet (egal ob über
`NWOS.Host` oder `NWOS.Bootstrap.Windows`) - jeder lokale Prozess kann sich
über `IpcClient` verbinden und Nachrichten genau wie ein In-Process-Modul
senden, inklusive derselben Berechtigungsprüfung. Anders als die Windows-
spezifischen Teile ist das über echte Ende-zu-Ende-Tests mit einer realen
Pipe abgesichert (`NWOS.Ipc.Tests`), weil `System.IO.Pipes` plattformneutral
funktioniert.

**Die UI nutzt `NWOS.Ipc` jetzt tatsächlich:** `NWOS.DesktopShell.Ui` wurde umgebaut und sendet
`icon.*`-, `box.*`-, `widget.*`- und `search.*`-Nachrichten über `IpcClient` an den laufenden Cardinal-Prozess
(siehe Abschnitt "NWOS.DesktopShell.Ui und Cardinal"). Dafür kamen die Nachrichtentypen `icon.listStates`,
`box.list` und `widget.list` hinzu; Module speichern ihren Zustand jetzt sofort nach jeder Änderung.

**Bevor `NWOS.Ipc` als "verifiziert" gilt**, auf echtem Windows:
- Zwei separate Prozesse: `NWOS.Bootstrap.Windows` starten, dann ein kleines
  Testprogramm (oder `dotnet-script`/eine Testkonsole) schreiben, das
  `IpcClient` verwendet, um z. B. `"alice.getRecentEvents"` zu senden -
  kommt eine echte Antwort vom laufenden Cardinal-Prozess zurück?
- Funktioniert das auch, wenn Cardinal währenddessen neu gestartet wird
  (Client sollte beim nächsten Aufruf einfach neu verbinden, nicht dauerhaft fehlschlagen)?

**Offene Punkte:**
- GPU-Auslastung und Temperaturen (Abschnitt 24) fehlen (siehe TODO in `WmiSystemStatusProvider.cs`).
- Kein Background-Maintenance-Scheduler (Abschnitt 26: idle-/zeitfensterabhängige automatische Wartung) - `MaintenanceWindowStart/End` in `NwosConfiguration` sind vorbereitet, aber noch ungenutzt.
- `NWOS.Bootstrap.Windows` hat noch keine automatisierten Tests (siehe Begründung in `BootstrapCommands.cs`) - die manuellen Prüfschritte oben sind bewusst der Ersatz dafür in diesem Entwicklungsstand.
- `IpcServer` bedient Verbindungen sequenziell, nicht parallel (bewusste Vereinfachung, siehe TODO in `IpcServer.cs`).

Nächste sinnvolle Fortsetzung: erster echter Windows-Test (Build, Tests, Installer), danach Phase 5
(Hintergrundsystem) oder der Background-Maintenance-Scheduler (Abschnitt 26).
