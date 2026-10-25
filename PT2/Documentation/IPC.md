# PT2-IPC-Architektur

## 1. Verifizierter IST-Zustand (PT1)

Laufzeitbeleg (SDK-Container, `dotnet test tests/NWOS.Ipc.Tests`): **4/5 Tests
schlagen fehl.** Code-Ursachen:

| Defekt | Ort | Wirkung |
|---|---|---|
| Framing-Mismatch | `IpcClient` schreibt `PipeFraming` (4-Byte-Länge+JSON), `IpcServer.HandleClientAsync` liest roh `DeserializeAsync` | Round-trip kaputt: Server liest Längen-Bytes als JSON → Exception → **keine Antwort** |
| Keine Identitäts-Erzwingung | Server übernimmt die client-behauptete `SourceModuleId` unverändert | Spoofing-Lücke: externer Client kann sich als `nwos.alice` o. ä. ausgeben; `IpcIdentityTests` erwartet Überschreibung auf `ExternalCallerIdentity` |
| Max. 1 Verbindung | Pipe mit `maxNumberOfServerInstances: 1`, Accept-Loop blockiert in `HandleClientAsync` | Ein hängender Client blockiert **alle** externen Aufrufe |
| Keine Timeouts/Reconnect/Lifecycle | Client: nur Connect-Timeout (5 s); Server: kein Lese-Timeout | Stall forever; „Verbindung trennen und wiederherstellen" (Auftrag §23) nicht erfüllt |
| Fehler ohne Rückkanal | Server loggt Exception, schreibt nichts | Client wartet bis Stream-Ende statt Fehlerergebnis |

Dazu drei konkurrierende Fake-/Alt-Protokolle im Repo (Pipes
`NWOS.Cardinal.Pipe`, `NWOS_Cardinal_Pipe`), die in PT2 ersatzlos entfallen
(MIGRATION_PT1_PT2.md §4.2–4.4).

## 2. Ziele (aus Auftrag §5)

Stabile Verbindungen, Connection Lifecycle, Timeout-Handling, Reconnect,
Exception-Handling, Message-Validation, Thread-Safety, keine Race-Conditions,
kontrollierter Shutdown, Recovery nach Modul-/Client-Absturz, Logging, keine
Endlosschleifen, keine unkontrollierten Leaks. Ein abgestürztes KI-Modul darf
Cardinal und Overlay nicht mitreißen.

## 3. Protokoll v2

- **Transport:** Named Pipe `NWOS.Cardinal` (Name unverändert), Byte-Modus,
  `PipeFraming` **auf beiden Seiten** (4-Byte-Länge, 16-MB-Grenze, Read-Exact).
  Byte- statt Message-Modus: frame-unabhängig und auf Tests auf Nicht-Windows
  (Container/CI) identisch verhalten.
- **Envelope:** `ModuleMessage` wird um nichts Neues erweitert müssen; es trägt
  bereits `SchemaVersion`. Ergänzt wird nur die **Antwort**-Struktur
  `IpcResponse { Outcome, Reason, ResponseMessage, ServerProtocolVersion }`.
- **Identität (Sicherheitsanker):** der Server setzt beim Empfang **immer**
  `SourceModuleId = IpcDefaults.ExternalCallerIdentity` – der Claim des Clients
  wird verworfen (Test `IpcIdentityTests` wird damit grün, ohne gelockert zu
  werden). Scopes für `ExternalCaller` vergibt weiterhin ausschließlich
  `HostComposition` (heute: FileSystemRead|Write).
- **Validation:** Länge > 0 und ≤ 16 MB, JSON-Parse, Pflichtfelder, bekannte
  `SchemaVersion`; ungültige Nachrichten → `IpcResponse{Outcome=Denied,
  Reason="…"}` statt Exception-only.

## 4. Server-Lebenszyklus

```
Accept-Loop (paranoid, pro Verbindung ein Task)
 ├─ AcceptAsync (mehrere Pipe-Instances, z. B. 4; Queue statt Serie)
 ├─ pro Verbindung: ReadFrame (Timeout z. B. 30 s Inaktivität)
 │   → Identity-Overwrite → TaskRouter.RouteAsync → WriteFrame(Antwort)
 ├─ Read-Timeout/Stream-Ende → Verbindung verwerfen, Ressourcen freigeben
 └─ CancellationToken → laufende Verbindungen ordentlich beenden (controlled
    shutdown: keine halben Frames, keine verwaisten Handles)
```

- **Thread-Safety:** `TaskRouter` bleibt der einzige Zustellungspfad; parallele
  Verbindungen sind ok, weil Router/Registry bereits thread-sicher sind
  (ConcurrentDictionary, zustellungslose Prüfung). Pro Verbindung ein
  Async-Task, kein blockierender Code im Accept-Loop.
- **Fehler-Rückkanal:** jede Ausnahme wird als `IpcResponse{ModuleFailure/Denied}`
  beantwortet (Frame erhalten, Aufrufer nie im Dunkeln).
- **Heartbeat (nur für Overlay):** Overlay-Prozess meldet sich periodisch per
  `overlay.heartbeat`; Cardinal misst damit Supervision (siehe OVERLAY.md),
  ohne dass der IPC-Server selbst Polling betreibt.

## 5. Client (`IpcClient`) & Reconnect

- Connect-Timeout (bleibt konfigurierbar), **Read-Timeout** für die Antwort,
  geordnetes Dispose bei Abbruch.
- Reconnect-Policy für langlaufende Clients (Overlay): Verbindungsaufbau mit
  **exponentiellem Backoff** (z. B. 0,5 s → 1 s → 2 s → 5 s, max 5 s) **ohne**
  Endlos-Busyloop; pro Anfrage bleibt es bei Einmal-Verbindungen
  (Request/Response), Heartbeat ist der einzige periodische Verkehr.
- UI-Muster aus PT1 (Titel „NICHT verbunden") bleibt: UI bleibt bedienbar,
  zeigt Verbindungsstatus, verwirft veraltete Antworten (bereits in
  `MainWindow` vorgezeichnet).

## 6. Events (Push statt Polling)

Neue, schlanke Push-MessageTypes vom Kern an verbundene Clients
(z. B. `event.widgetUpdated`, `event.healthChanged`, `event.pendingAction`,
`event.boxUpdated`) über einen **separaten Notify-Kanal** pro Client
(Event-Subscription bei Verbindungsregistrierung). Damit entfällt das 1-s-
Widget-Polling (§19 Performance); unbestätigte Ereignisse sind idempotent.

## 7. Teststrategie (Auszug, Details TESTING.md)

- Bestehende `NWOS.Ipc.Tests` **werden grün gemacht, nicht gelockert**:
  Round-trip, Denied-Roundtrip, Sequenz, „Server weg → ModuleFailure",
  Identity-Overwrite.
- Neu: Parallelitäts-Test (N Clients gleichzeitig), Timeout-Test (stalled
  Client blockiert keine anderen), Shutdown-Test (keine Leak-Meldungen),
  Reconnect-Test (Verbindung trennen/wiederherstellen → automatische
  Erholung, Auftrag §23), Validation-Tests (Riesen-/Müll-Frames).
