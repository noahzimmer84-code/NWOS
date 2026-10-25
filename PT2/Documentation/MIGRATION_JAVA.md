# VPT2/VPT3 – Migration .NET/PowerShell → Java

Status: **in Arbeit** (Start der Java-Migration, Ziel: vollständige Entfernung der .NET-Runtime)

## Ziel und Roadmap-Kontext

- **VPT2/3**: PT1-/PT2-Probleme beheben — PowerShell- und .NET-Programme werden nach Java konvertiert, die .NET-Runtime wird entfernt. Das Windows-Overlay (VPT3) wird auf **JavaFX** gebaut.
- **VPT4**: Verbesserungen, Ideen, Fehlerbehebung.
- **VPT5**: Überführung in ein Betriebssystem.
- **Kern-Architektur**: Cardinal (Kernsystem-Instanz) orchestriert Module; Marc (Aufgabenerledigung/Auftragsdurchsetzung) und Alya (User-Kontakt, Priorisierung) entwickeln sich selbstständig weiter.

## Was existiert jetzt (Java-Struktur unter `java/`)

| Java-Modul | Ersetzt (C#) | Status |
|---|---|---|
| `nwos-contracts` | `NWOS.Contracts` | portiert (ModuleMessage, IModule, Scopes, Health, NwosJson) |
| `nwos-cardinal` | `NWOS.Cardinal` | portiert (Registry, TaskRouter, PermissionManager, HealthMonitor, CardinalService) |
| `nwos-ipc` | `NWOS.Ipc` | portiert **+ PT1-Fixes** (siehe unten) |
| `nwos-host` | `NWOS.Host` / `NWOS.Bootstrap.Windows` | echter Bootstrap (kein Stub mehr), eingebauter SystemModule |
| `nwos-overlay` | `NWOS.DesktopShell.Ui` (aufgegebenes WPF-Band) | VPT3-Skeleton: transparente, always-on-top JavaFX-Leiste mit Cardinal-Statusabfrage |

Nicht übernommen (bewusst): `CardinalIpcDispatcher` und weitere PT1-Fakes, das defekte 48px-WPF-Band, PowerShell-Install/-Uninstall-Skripte.

## Mit der Migration behobene PT1-Probleme

1. **IPC-Framing-Mismatch**: PT1 schrieb der Client gerahmt, der Server erwartete ungerahmten JSON-Stream (4/5 fehlschlagende IPC-Tests). Java: es gibt genau ein Codec (`FrameCodec`, 4-Byte-Big-Endian-Längenpräfix, 16-MB-Limit) für alle Teilnehmer — keinen ungerahmten Pfad mehr.
2. **Fehlendes Identity-Enforcement**: Jede Verbindung startet mit einem Identitäts-Handshake (`HandshakeRequest`); unbekannte Identitäten werden abgewiesen, und jede Nachricht muss als Quelle die verifizierte Verbindungs-Identität tragen (`IpcServer.enforceIdentityAndRoute`) — Spoofing ist ausgeschlossen.
3. **Single-Connection-Limit**: Der PT1-Server bediente genau einen Client; der Java-Server bearbeitet parallel beliebig viele Verbindungen (Virtual Threads).
4. **Stub-Einstiegspunkte**: `de.nwos.host.Main` führt den echten Bootstrap aus (Registry → Permissions → Router → Cardinal → IPC-Bus) statt nur die Konsole zu öffnen.

## Getroffene Entscheidungen

- **Build**: Maven, Java 21 (LTS), Jackson für JSON, JUnit 5.
- **Transport**: Loopback-Socket auf `127.0.0.1` (Standardport 45110). Java kennt keine nativen Named Pipes; ein Windows-Named-Pipe-Transport kann später als zusätzliche Implementierung ergängt werden, ohne dass sich Contracts/Router ändern.
- **Threading**: Virtual Threads (Java 21) pro IPC-Verbindung; asynchrone Modul-API über `CompletableFuture` (Äquivalent zu C# `Task`).
- **Scopes**: C#-`[Flags]`-Enum → `Set<PermissionScope>`; leere Menge = `None`.
- **.NET-Runtime entfernen**: Das Endprodukt wird mit `jpackage` gebündelt (eigenes Runtime-Image, kein installiertes .NET, keine installierte Java-VM beim Nutzer nötig). Kein PowerShell-Installer mehr.

## Nächste Schritte

1. Module **Marc**, **Alya**, **Alice** nach Java portieren (Fachlogik aus `NWOS.Marc/Alya/Alice`).
2. Widgets/SystemCenter portieren; Overlay um echte UI-Zonen erweitern.
3. C#-Projekte und `NWOS.sln` entfernen, sobald Parität erreicht ist ( dieser Zweig: schrittweise Migration, C# bleibt bis dahin referenzierbar).
4. Installation: `jpackage`-Installer (MSI/EXE) für Windows ersetzt `Install-NWOS.ps1` / `NWOS-Setup.bat`.
5. Aggregierte PT2-Dokumente (ARCHITECTURE, IPC, SECURITY) um die Java-Sicht ergänzen.

## Bauen & Testen

```bash
cd java
mvn clean package          # kompiliert alle Module + Tests
mvn -pl nwos-host -am package   # nur Host-Build
java -jar nwos-host/target/nwos-host-*.jar   # Cardinal starten
mvn -pl nwos-overlay javafx:run             # Overlay starten (benötigt laufenden Cardinal)
```
