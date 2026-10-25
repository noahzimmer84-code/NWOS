# NWOS PT2 – Technische Grundlage

Diese Dokumentation ist das Ergebnis der PT2-Analyseaufgabe (Auftrag Abschnitt 26/27):
vollständige Analyse des PT1-Codes, dann die technische Grundlage für PT2 – **bevor**
mit der Implementierung begonnen wird.

## Ablagestruktur (Auftrag Abschnitt 24)

Implementierungsartefakte werden in den späteren Phasen in die zugehörigen Ordner
gelegt; die Analyse- und Architektur-Dokumentation liegt hier gesammelt in
`PT2/Documentation/`:

```
PT2/
├── Documentation/          <- diese Analyse (Phase 0 / "PT2.0")
│   ├── MIGRATION_PT1_PT2.md <- Inventur, Wiederverwendung, Problemkomponenten, Migrationsplan
│   ├── ARCHITECTURE.md     <- Ist- und Ziel-Architektur
│   ├── CARDINAL.md         <- Cardinal-Kernarchitektur für PT2
│   ├── IPC.md              <- IPC-Architektur für PT2 (inkl. verifizierter PT1-Befunde)
│   ├── OVERLAY.md          <- Overlay-/Desktop-Shell-Architektur
│   ├── AI_SYSTEM.md        <- Alya/Marc/Alice/Cardinal-Agentenarchitektur + KI-Fenster
│   ├── INSTALLATION.md     <- Installer-Architektur (ersetzt PowerShell-Installer)
│   ├── SECURITY.md         <- Berechtigungs-/Sicherheitssystem für PT2
│   ├── TESTING.md          <- Teststrategie inkl. bestehender Suiten
│   └── PT2_ROADMAP.md      <- Umsetzungsplan PT2.1 – PT2.7 mit Definition of Done
├── Architecture/           <- (später) Architektur-Feinentwürfe pro Phase
├── Core/                   <- (später) Cardinal/IPC-Implementierungsnotizen
├── Overlay/                <- (später) Overlay-Implementierungsnotizen
├── AI/                     <- (später) Agenten-Implementierungsnotizen
├── Installer/              <- (später) Installer-Implementierungsnotizen
├── Security/               <- (später) Sicherheitsnotizen
└── Tests/                  <- (später) Testpläne/Ergebnisse pro Phase
```

## Abdeckung der 12 Analyseergebnisse (Auftrag Abschnitt 27)

| # | Gefordertes Ergebnis | Dokument |
|---|---|---|
| 1 | Aktuelle Architektur | ARCHITECTURE.md §1, MIGRATION_PT1_PT2.md §1 |
| 2 | Vorhandene Komponenten | MIGRATION_PT1_PT2.md §2 |
| 3 | Wiederverwendbare Komponenten | MIGRATION_PT1_PT2.md §3 |
| 4 | Problematische Komponenten | MIGRATION_PT1_PT2.md §4 |
| 5 | Notwendige Umbauten | MIGRATION_PT1_PT2.md §5 |
| 6 | Abhängigkeiten | MIGRATION_PT1_PT2.md §6 |
| 7 | Cardinal-Architektur für PT2 | CARDINAL.md |
| 8 | IPC-Architektur für PT2 | IPC.md |
| 9 | Overlay-Architektur | OVERLAY.md |
| 10 | Installer-Architektur | INSTALLATION.md |
| 11 | Migrationsplan | MIGRATION_PT1_PT2.md §7 (OLD → NEW → WHY → MIGRATION) |
| 12 | Teststrategie | TESTING.md |

## Wichtigste verifizierte Kernbefunde (Kurzfassung)

1. **PT1 besitzt einen soliden, getesteten Cardinal-Kern** (`NWOS.Contracts`,
   `NWOS.Cardinal`, `NWOS.Settings`, Fachmodule Alice/Marc/Alya/Widgets/
   DesktopShell/SystemCenter) – dieser wird **beibehalten und erweitert**, nicht
   neuentwickelt.
2. **NWOS startet aktuell nicht**: beide Einstiegspunkte (`NWOS.Host/Program.cs`,
   `NWOS.Bootstrap.Windows/Program.cs`) sind Platzhalter-Stubs, die die reale
   Komposition (`HostComposition.RunAsync` / `BootstrapCommands.RunAsync`) nie
   aufrufen. Das README beschreibt einen Zustand, der im Code nicht mehr existiert.
3. **Das reale IPC ist kaputt** (verifiziert per Testlauf: 4 von 5
   `NWOS.Ipc.Tests` schlagen fehl): Client schreibt längen-präfixierte Frames,
   der Server liest rohes JSON; die Absender-Identität wird nicht überschrieben.
4. **Drei konkurrierende IPC-Protokolle/Pipe-Namen** existieren parallel
   (`NWOS.Cardinal`, `NWOS.Cardinal.Pipe`, `NWOS_Cardinal_Pipe`), zwei davon
   sind Fake-Implementierungen mit hartcodierten Antworten.
5. **`NWOS.Setup` kompiliert nicht** (Syntaxfehler `-catch`), und der
   PowerShell-Installer installiert die falschen (Stub-)Binaries.
6. **Die WPF-UI ist inkonsistent** (XAML mit 5-Zonen-Taskleiste vs. Code-Behind
   einer älteren Desktop-Ansicht; zentrale Steuerelemente auf 0×0/Collapsed) –
   sie ist kein funktionierendes Overlay und wird durch das PT2-Overlay ersetzt.

Details, Belege und Folgeschritte: siehe die einzelnen Dokumente.
