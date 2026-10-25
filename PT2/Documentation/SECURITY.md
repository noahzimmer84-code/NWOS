# PT2-Sicherheit & Berechtigungen

## 1. Bestand (aus PT1, wird nicht ersetzt)

- **Scopes:** `PermissionScope` (FileSystemRead/Write, ProcessControl,
  NetworkResearch, NetworkGeneral, SystemChange, DeviceCommunication).
- **Zwingender Check:** jede Nachricht durch `PermissionManager.Evaluate`;
  deklarierter Scope ist Untergrenze, `MessageScopePolicy` legt pro MessageType
  echte Minima fest (Absender kann Prüfung nicht durch `None` umgehen).
- **Scope-Vergabe:** ausschließlich Host/Installer-Pfad aus Konfiguration
  (`NwosConfiguration.ScopesFor`, `GrantExternalScope`); Module/Client können
  nichts selbst erweitern.
- **Harte Regeln:** `nwos.marc` verliert NetworkGeneral **unabhängig von jeder
  Konfiguration** (in `PermissionManager` kodiert); Marc ohne Netzwerk-
  Projekt-Referenz; Alya ohne Datei-/Prozess-/Netzwerk-Fähigkeiten.
- **Bestätigungspflicht:** SystemChange/ProcessControl → niemals still
  durchgewunken (PT1: Rückgabe `PendingUserConfirmation`).
- **Alice:** passives Watcher-Monitoring, transparentes Regelwerk, append-only
  Sicherheits-Log, reversible Quarantäne; **eine** eng gefasste Auto-Regel
  (double-extension-executable), alles andere nutzerbestätigt.

## 2. PT2-Ergänzungen

### 2.1 IPC-Identität schließen (Priorität 1)
Spoofing-Lücke aus MIGRATION §4.5: der Server erzwingt `SourceModuleId =
NWOS.ExternalCaller`; Scopes dieser Identität bleiben minimal
(FileSystemRead/Write – bewusst kein ProcessControl/SystemChange/Netzwerk,
bestehende Konstruktion in `HostComposition` ist korrekt und bleibt).

### 2.2 PendingAction-Workflow statt stiller Verfall
Kritische Anfragen landen in der persistierten, TTL-begrenzten
`PendingActionStore` (CARDINAL.md §3.2); jedes Erlauben/Ablehnen/Timeout wird
im Audit-Log protokolliert. UI-Karte „[Erlauben] [Ablehnen]" zeigt Absender,
Ziel, Aktion, Grund in Klartext (Auftrag §15-Beispiel).

### 2.3 Persistente Audit-Log (Kern)
`PermissionManager` loggt heute nur in den ILogger. PT2: zusätzlich append-only
JSONL (Cardinal-AuditLog) mit Permission-Entscheidungen, Recovery-,
PendingAction- und Lifecycle-Ereignissen; lesbar über `audit.query` (read-only,
Scope None) für Alice-Übersicht und Nutzer (Auftrag §15 „nachvollziehbar").

### 2.4 Box-Passwortschutz (echt oder gar nicht)
`SmartBox.PasswordHashPlaceholder` bleibt ungenutzt, bis der Schutz real
existiert: PBKDF2 (Rfc2898, iteration ≥ 100k, Salt pro Box), Verschlüsselung
der Item-Quellenliste (AES-GCM) im Konfigurationsverzeichnis; kein Klartext-
Passwort, kein Platzhalter-Feld in der UI. Bis dahin: Feature-UI offeriert
keinen Passwortschutz (PT1-Prinzip „keine Fake-Funktionen").

### 2.5 Installer-Sicherheit
Siehe INSTALLATION.md §„Sicherheit": HKCU-only, Schreibgrenzen, transparente
Schritte, Rollback, Shell-Override bleibt explizit + reversibel
(`register-shell`/`restore-shell`-Notausgang mit getippter Bestätigung /
bestätigungsfreier Wiederherstellung).

### 2.6 Datenschutz
Alles bleibt lokal (JSON unter `%LOCALAPPDATA%\NWOS`); keine Telemetrie,
keine Cloud; Chat-Verläufe lokal. Neue Netzwerk-Features wären Opt-in mit
eigenem Scope (NetworkGeneral/NetworkResearch) und transparenter Information.

## 3. Verbotene Aktionen (Kodex, wird in Tests gespiegelt)

Cardinal/Agenten dürfen **nie**: ungefragt kritische Änderungen, ungefragtes
Löschen, Deaktivieren von Sicherheitsfunktionen, Windows beschädigen, Nutzerdaten
ohne Zustimmung übertragen. Entsprechende Negative-Tests:
  Denied-Pfade in `PermissionManagerTests` werden um IPC-Ebene (externer
  Caller) und PendingAction-Verweigerung ergänzt.
