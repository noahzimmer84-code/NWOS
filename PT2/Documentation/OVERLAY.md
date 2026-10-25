# PT2-Overlay-Architektur (Desktop Shell, Widget Window, Taskbar)

## 1. IST (PT1) und Bewertung

`NWOS.DesktopShell.Ui` ist ein 48 px hohes, topmostes WPF-Band mit 5 Zonen
(XAML), während der Code-Behind aus einer älteren, vollflächigen Desktop-
Generation stammt (eigene Collections, Steuerelemente im XAML auf 0×0/Collapsed).
Das Fenster ist in dieser Form **nicht bedienbar** und wird nicht repariert,
sondern durch das PT2-Overlay ersetzt. Wiederverwendet werden die dahinter
liegenden Domänen-Klassen (`SmartBox`, `BoxManager`, `WidgetManager`,
`SearchIndex`, `IconVisibilityStore`, `SettingsStore`) und das UI-Muster
„Cardinal weg → Status im Titel, UI bleibt bedienbar".

## 2. Zielbild

`NWOS.Overlay.Ui` (neues Projekt, net8.0-windows, **eigener Prozess**):

| Fenster | Aufgabe (Auftrag) |
|---|---|
| **Desktop-Overlay** | borderless, transparent integrierte NWOS-Oberfläche über dem Windows-Desktop (§8) |
| **NWOS Widget Window** | oben rechts, klein/unaufdringlich, ein-/ausblendbar (kein Dauer-Topmost) (§9) |
| **Taskbar-Einstiegspunkt** | NWOS-Icon im Windows-Tray-Bereich → öffnet NWOS Settings; **kein** Eingriff in die Windows-Taskleiste selbst (§12) |
| **NWOS AI-Fenster** | Chat/Oberfläche für Alya/Marc/Alice/Cardinal (§13/§14) |
| **NWOS Settings** | 15 Bereiche über `SettingsStore` (§11) |

## 3. Desktop-Overlay (technisch)

- **Borderless:** `WindowStyle=None`, `AllowsTransparency`/`WindowChrome`,
  `WS_EX_LAYERED | WS_EX_TOOLWINDOW` (nicht in Taskleiste/Alt-Tab), optional
  `WS_EX_NOACTIVATE` für passive Flächen.
- **Fokus-/Layer-Management:** Voll-Screen-Overlay-Fläche `Topmost=False`
  (deckt nichts ab, solange der Nutzer nichts tut), Bereiche heben gezielt
  ab; Klick-Through für transparente Flächen (`WS_EX_TRANSPARENT`) außerhalb
  interaktiver Zonen.
- **DPI:** Per-Monitor-V2 (`app.manifest` dpiAwareness), keine gerundeten
  Koordinaten in der Persistenz (physische Px speichern + Skalierungsfaktor).
- **Multi-Monitor:** vorbereitet via `EnumDisplayMonitors`/`Screen.AllScreens`:
  Fensterposition pro Monitor-Geräte-ID persistiert; Fenster erscheinen auf
  dem Monitor der letzten Nutzung.
- **Themes:** Dark/Light/Custom über Resource-Dictionaries; konkrete Farbwerte
  kommen aus `SettingsStore` (Abschnitt `nwos.overlay`), kein hartcodiertes
  Farbschema im Code.
- **Input:** Keyboard-Shortcuts (z. B. Global-Hotkey zum Ein-/Ausblenden des
  Widget-Window, `RegisterHotKey`), Mouse/Hover für Boxen (Hover-/Static-Modus
  existieren im Domänenmodell bereits: `BoxDisplayMode`).
- **Ressourcen (§19):** kein Dauer-Rendering-Loop; `CompositionTarget`-Takt nur
  bei sichtbaren, geänderten Flächen; Widget-Werte kommen per IPC-Event-Push,
  nicht per Timer; versteckte Fenster werden (per Visibility.Collapsed) aus
  dem Render- und IPC-Verkehr genommen.

## 4. NWOS Widget Window (§9)

- Standardzustand: klein, am rechten oberen Rand, **nicht** topmost, öffnet
  sich bei Bedarf (Tray-Klick/Hotkey) und zieht sich zurück.
- Funktionen: Widgets erstellen/aktivieren/deaktivieren/verschieben/konfigurieren,
  Boxes erstellen/öffnen/schließen, neue Widget-Typen (aus `WidgetRegistry`).
- Alle Aktionen laufen als normale IPC-Nachrichten (`widget.*`, `box.*`) mit
  Permission-Check; der Overlay-Prozess hat als `NWOS.ExternalCaller` nur
  FileSystemRead/Write – Box-/Widget-Zustand bleibt im Cardinal-Prozess
  Single Source of Truth.

## 5. Smart Boxes (§10)

Domäne existiert und wird erweitert (kein Neubau):
- `SmartBox`/`BoxManager` können bereits: create/delete/rename/position/
  size/hover/static/box-interne Suche/persistente JSON.
- Fehlt: MessageTypes im `DesktopShellModule` für `box.rename`, `box.delete`,
  `box.move`, `box.resize`, `box.setDisplayMode` (+ Einträge in
  `MessageScopePolicy`, alles FileSystemWrite) – und der Passwortschutz
  (siehe SECURITY.md: erst mit echtem PBKDF2, kein Fake-Feld).
- Drag & Drop und interne Suche hängen an der neuen Overlay-UI; das
  Drop-Muster aus PT1 (`BoxBorder_Drop`) wird übernommen.

## 6. Taskbar-Integration (§12)

- **Keine** Manipulation der Windows-Taskleiste. Einstiegspunkt = Tray-Icon
  (Shell_NotifyIcon) mit „NWOS Settings"-Eintrag; Platzierung im Tray-Bereich
  neben den Windows-Indikatoren entspricht der Anforderung, ohne System zu
  verändern.
- Die bestehende, reversible Ausnahme-Option (NWOS **als** Shell, HKCU-Winlogon
  via `BootstrapCommands register-shell`/`restore-shell`) bleibt bewusst
  erhalten – separater, expliziter Schritt, nie automatisch.

## 7. Prozess-/Crash-Verhalten (§20)

```
Overlay-Crash → Cardinal (ProcessSupervisor) überlebt
  → Heartbeat-Verlust erkannt (Timeout > Restart-Backoff)
  → Overlay-Prozess neu starten
  → Sitzungsrestore aus Persistenz (Boxen/Widgets/Icon-Zustände sind
    bereits je Aktion gesichert – PT1-Prinzip bleibt)
```
Umgekehrt bleibt der Overlay bedienbar und zeigt „NICHT verbunden", wenn
Cardinal (z. B. für Updates) kurz weg ist – Reconnect mit Backoff (IPC.md §5).
