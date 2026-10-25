# PT2-KI-System (Alya, Marc, Alice, Cardinal) + AI-Fenster

## 1. Verantwortungsbereiche (Auftrag §14, unverändert aus PT1)

| Agent | Verantwortung | PT1-Umsetzung | PT2 |
|---|---|---|---|
| **Alya** | Benutzerinteraktion, Kommunikation, Statusberichte, persönliche Oberfläche | `AlyaModule` (greet/statusReport/setTone) – **keine** Systemrechte (PermissionScope.None) | unverändert + Chat-UI |
| **Marc** | vom Nutzer angeforderte lokale PC-Aufgaben | `MarcModule` (createFolder/renameFile/organizeFolder/launchApp/closeApp); Netzwerk auf Projektebene verboten | unverändert + echte Aktivitätsdarstellung |
| **Alice** | Sicherheit, Überwachung, sicherheitsbezogene Funktionen | `AliceModule` (Watcher, Heuristiken, Burst, Audit, Quarantäne) | unverändert + Audit-Einblick über `audit.query` |
| **Cardinal** | zentrale Koordination, Systemverwaltung, Orchestrierung | Kern-Klassen, kein Chat-Agieren | Verwaltungssicht im AI-Fenster (Systemzustand, Module, Recovery) |

Trennung bleibt **technisch erzwungen**: Alya ohne Datei-/Prozess-/Netzwerk-Referenz,
Marc ohne Netzwerk-Referenz (projektseitig), alles Routing durch Permission-Check.

## 2. Befehlskette (Auftrag §15)

```
User → Overlay (AI-Fenster)
     → Alya (Dialog-Aufnahme)          Scope: None
     → Cardinal TaskRouter             Permission-Check zwingend
     → [kritisch? → PendingAction → Nutzer: Erlauben/Ablehnen]
     → Marc (Ausführung)                Scope: FileSystem/Process
     → Windows APIs
```
Keine KI erweitert eigene Berechtigungen: Scopes kommen ausschließlich aus
`NwosConfiguration.ScopesFor` + `ModuleRegistry`. Die harte Marc-Netzwerk-
Verweigerung bleibt in `PermissionManager` kodiert.

## 3. NWOS AI-Fenster (Auftrag §13)

Ersetzt das „einfache Textfeld" (PT1-`TaskbarAssistantController` war Fake).
Komponenten im Overlay-Prozess:

- **Chat-Verlauf** pro Agent (Nachrichten, Statusmeldungen, Aktionsergebnisse,
  verständliche Fehler) – lokal persistiert pro Nutzer (JSON im
  Konfigurationsverzeichnis, kein Netzwerk).
- **Eingabe + Senden** → `IModuleMessenger`-Nachricht an den gewählten Agenten
  (`ai.chat` als neuer MessageType, Scope None; die Antwort des Agenten ist
  Dialog, keine Systemaktion).
- **Agentenauswahl** `[ Alya ▼ ]` (Alya/Marc/Alice/Cardinal) mit sichtbarem
  aktiven Agenten; Wechsel nur im Frontend, Backend-Verantwortlichkeiten bleiben.
- **Aktionen & Bestätigungen:** fordert eine Nachricht SystemChange/ProcessControl,
  erscheint die PendingAction als Karte mit `[Erlauben] [Ablehnen]`
  (Anzeige: Absender, Ziel, Aktion, Grund – aus der Audit-Entscheidung).
  `Alice`-Meldungen (Threat-Events, Quarantäne) erscheinen als Statuskarten.
- **Fehlerdarstellung:** Ergebnisse aus `ModuleMessageOutcome` (Denied →
  „Berechtigung fehlt/abgelehnt", PendingUserConfirmation → Bestätigungskarte,
  ModuleFailure → Agent offline/Recovery läuft).

## 4. Was NICHT simuliert wird

- Kein Fake-„Antwort-Generator" (der PT1-`CardinalIpcDispatcher` mit
  „[Alya AI] Antwort auf …" entfällt ersatzlos).
- Eine echte Konversations-„Intelligenz" jenseits der implementierten Agenten-
  Fähigkeiten gibt es in PT2 **nicht** – Chat-Antworten bestehen aus echten
  Agentenfunktionen (Begrüßung, Lagebericht, Task-Ausführung, Alice-Ereignisse).
  Sobald ein Sprach-/LLM-Backend gewünscht ist, wird es als eigenes, vom Nutzer
  aktivierbares Modul mit eigenem Scope konfiguriert (netzwerkfähig, Opt-in,
  dokumentiert) – nicht als vorgetäuschte Integration.

## 5. Sichtbarkeit des Systemzustands

- Agenten-Status (Healthy/Degraded/Isolated) kommt aus `HealthMonitor`/EventBus
  und wird im AI-Fenster pro Agent angezeigt („Alya – aktiv", „Marc – Neustart",
  „Alice – isoliert").
- Cardinal-Verwaltungssicht: Modulliste, Recovery-Verlauf (Audit-Log),
  Ressourcen-Angaben aus `SystemCenterModule` (nur echte WMI-Daten; ohne
  Windows-Datenquelle: explizit „nicht verfügbar" – PT1-Fallback bleibt).
