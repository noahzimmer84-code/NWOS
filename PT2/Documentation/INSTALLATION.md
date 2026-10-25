# PT2-Installer & Installation

## 1. IST (PT1) und Defizite

- `Install-NWOS.ps1` (PowerShell): gut durchdachter Ablauf, aber es **baut auf
  dem Rechner des Nutzers aus Quellcode** (installiert bei Bedarf das .NET SDK
  per winget!), startet cmd-`Start-NWOS.bat`, Autostart via
  `cmd.exe /c start "" /min …`.
- `NWOS.Setup` (C#-Konsole): Syntaxfehler (`-catch`), kompiliert nicht,
  dupliziert die Skript-Logik schlechter, kein Rollback.
- **Folge:** „Installation" = SDK-Voraussetzung + Build + Stub-Binaries.
- README-Doku ist ehrlich („noch nie kompiliert"), aber PT2-Ziel ist: SDK-lose,
  roll back-fähige, native Installation (Auftrag §6/§7/§16/§17).

## 2. Ziel: `NWOS.Installer` (nativ, C#, WPF-Konsole)

Ein eigenes Projekt `NWOS.Installer` (net8.0-windows, **self-contained**
gepublish't, braucht selbst kein .NET SDK/Runtime beim Nutzer) ersetzt
PowerShell-Installer UND `NWOS.Setup`. Der Installationsablauf folgt dem
bereits entworfenen PS1-Ablauf (der bewährte Teil von PT1 wird migriert):

```
System Check (OS-Version, Ressourcen, installierte NWOS-Version)
  → Komponenten prüfen (self-contained Publish ⇒ KEINE Runtime-Abhängigkeit)
  → Dateien kopieren (Versioniert: Programs\NWOS\<version>\…)
  → Konfiguration erzeugen (nur falls keine existiert; GUI-FirstRunWizard
     bleibt bewusst dem First Launch vorbehalten)
  → Verknüpfungen (Desktop/Startmenü, nativ statt COM-freie .bat)
  → Autostart (HKCU Run, direkt auf NWOS.Cardinal.exe, kein cmd-Umweg)
  → Overlay-/Taskbar-Integration (Tray-Icon Teil der App, nichts zu
     registrieren; Shell-Override bleibt bewusst separat & manuell)
  → Deinstallations-Eintrag (HKCU, verweist auf Installer-Uninstall)
  → First Launch starten
  → Protokoll: Installations-Log neben Datenverzeichnis
```

### Sicherheit (Auftrag §17)
- **Keine Administratorrechte** – alles HKCU / LOCALAPPDATA (PT1-Prinzip bleibt).
- **Schreibgrenze:** nur `%LOCALAPPDATA%\Programs\NWOS` und `%LOCALAPPDATA%\NWOS`
  plus explizite, einzeln gemeldete Ausnahmen (Verknüpfungen). Keine
  Systemdateien, keine HKLM, keine Firewall-/Netzwerkregeln (NWOS hat keinen
  Server; neue Netzwerk-Integrationen würden nur bei echtem Bedarf, mit
  transparenter Nutzerinformation, dazukommen).
- **Transparenz vor kritischen Schritten** (Autostart, Shell-Override-Option):
  informative Bestätigungen statt stiller Änderungen (PS1-Vorbild übernehmen).
- **Rollback:** versionierte Zielverzeichnisse + vor jedem Schritt geschriebenes
  Transaktions-Log (Aktion + Rück-Aktion). Fehler → bisher ausgeführte Schritte
  werden in umgekehrter Reihenfolge zurückgerollt (Dateien löschen, Registry-
  Werte entfernen); bereits existierende Konfiguration wird nie überschrieben.
- **Update:** neue Version in neues Verzeichnis, Prozesse beenden, aktiv-Verweis
  (Startmenü/Autostart) umlenken, alte Version erst nach erfolgreichem Start der
  neuen entfernen → Downgrade-Rettung bleibt möglich.

## 3. Deinstallation

Übernimmt die `Uninstall-NWOS.ps1`-Logik nativ:
1. **zuerst** Shell-Override zurücksetzen, falls gesetzt (Notausgang bleibt
   bestätigungsfrei),
2. NWOS-Prozesse beenden,
3. Autostart/Verknüpfungen/Deinstallations-Eintrag entfernen,
4. Programmverzeichnis löschen,
5. **Nutzerdaten** (Konfiguration, Boxen, Quarantäne, Audit-Log) standardmäßig
   erhalten – Löschung nur auf ausdrücklichen Wunsch.

## 4. Verteilung (§7 „.NET-Abhängigkeit reduzieren")

- `dotnet publish -c Release -r win-x64 --self-contained` für Cardinal, Overlay
  und Installer: keine Runtime-Voraussetzung beim Nutzer, kein SDK, keine
  PATH-Abhängigkeit. Größe (~100–150 MB) ist akzeptiert und dokumentiert;
  bewusst **kein** Framework-Dependent-Deployment (das wäre die Runtime-
  Vorbedingung, die PT2 abschaffen soll).
- Abhängigkeiten bleiben minimal (keine neuen NuGet-Pakete außer ggf.
  CsWin32 für Tray-/DPI-APIs); alles Dokumentationspflichtige wird in
  INSTALLATION.md (diesem Dokument) weitergepflegt.

## 5. Teststrategie (Auszug, Details TESTING.md)

- Installer-Logik als testbare, plattformneutrale Kernklasse (Schrittliste,
  Rollback-Log): xUnit für Schrittfolge/Rollback/Idempotenz.
- Auf Windows: saubene-VM-Install/Uninstall/Update/Shell-Status als
  manuelle Prüfliste pro Release (Anforderung §23 „Installer Test").
