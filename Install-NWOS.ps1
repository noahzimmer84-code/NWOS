<#
.SYNOPSIS
    Installiert und aktiviert NWOS (NightWing Operating System) fuer den aktuellen Windows-Benutzer.

.DESCRIPTION
    Baut NWOS aus dem Quellcode neben diesem Skript (NWOS.sln), installiert es nach
    %LOCALAPPDATA%\Programs\NWOS, legt eine Konfiguration an, erstellt Verknuepfungen
    (Desktop + Startmenue), richtet optional den Autostart ein und startet NWOS.

    Bewusst NICHT automatisch: die Registrierung als Windows-Shell (ersetzt beim Login
    den normalen Desktop). Das passiert nur auf ausdruecklichen Wunsch (-OfferShellRegistration)
    und mit getippter Bestaetigung. Es sind keine Administratorrechte noetig.

.PARAMETER InstallDir
    Zielordner (Standard: %LOCALAPPDATA%\Programs\NWOS).
.PARAMETER NoAutostart
    Kein Autostart-Eintrag.
.PARAMETER NoLaunch
    NWOS nach der Installation nicht starten.
.PARAMETER Quiet
    Keine Rueckfragen, alle Standardwerte verwenden (alle Module aktiv, Autostart an).
.PARAMETER RunTests
    Vor der Installation 'dotnet test' ausfuehren und bei Fehlern abbrechen.
.PARAMETER OfferShellRegistration
    Am Ende anbieten, NWOS als Windows-Shell zu registrieren (mit getippter Bestaetigung).
#>
[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\NWOS'),
    [switch]$NoAutostart,
    [switch]$NoLaunch,
    [switch]$Quiet,
    [switch]$RunTests,
    [switch]$OfferShellRegistration
)

$ErrorActionPreference = 'Stop'
$SourceRoot = $PSScriptRoot
$DataDir = Join-Path $env:LOCALAPPDATA 'NWOS'

function Write-Step([string]$Text) { Write-Host ""; Write-Host "==> $Text" -ForegroundColor Cyan }

function Ask-YesNo([string]$Question, [bool]$Default = $true) {
    if ($Quiet) { return $Default }
    $suffix = if ($Default) { '[J/n]' } else { '[j/N]' }
    $answer = Read-Host "$Question $suffix"
    if ([string]::IsNullOrWhiteSpace($answer)) { return $Default }
    return ($answer.Trim().ToLower() -in @('j', 'ja', 'y', 'yes'))
}

function Test-DotnetSdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { return $false }
    $majors = & dotnet --list-sdks 2>$null | ForEach-Object { [int](($_ -split '\.')[0]) }
    return [bool]($majors | Where-Object { $_ -ge 8 })
}

function Publish-Project([string]$Project, [string]$OutputDir) {
    Write-Host "  Baue $Project ..."
    if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
    & dotnet publish (Join-Path $SourceRoot $Project) -c Release -o $OutputDir --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fuer '$Project' fehlgeschlagen (Exit-Code $LASTEXITCODE)." }
}

function New-Shortcut([string]$Path, [string]$Target, [string]$WorkDir, [string]$Description) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $Target
    $shortcut.WorkingDirectory = $WorkDir
    $shortcut.Description = $Description
    $shortcut.WindowStyle = 7   # minimiert (das Konsolenfenster des Kerns soll nicht stoeren)
    $shortcut.Save()
}

Write-Host "NWOS Installer" -ForegroundColor Green
Write-Host "Quelle : $SourceRoot"
Write-Host "Ziel   : $InstallDir"
Write-Host "Daten  : $DataDir"

# --- 1. Voraussetzungen ----------------------------------------------------------
Write-Step 'Pruefe Voraussetzungen'
if ($env:OS -ne 'Windows_NT') { throw 'NWOS ist ein Windows-Programm - dieser Installer laeuft nur unter Windows.' }
if (-not (Test-Path (Join-Path $SourceRoot 'NWOS.sln'))) {
    throw "NWOS.sln wurde neben dem Installer nicht gefunden. Bitte das komplette entpackte Projekt verwenden (Ordner: $SourceRoot)."
}

if (-not (Test-DotnetSdk)) {
    Write-Host '.NET SDK 8 (oder neuer) wurde nicht gefunden.' -ForegroundColor Yellow
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        if (Ask-YesNo 'Jetzt per winget installieren (Microsoft.DotNet.SDK.8)?') {
            & winget install --id Microsoft.DotNet.SDK.8 -e --accept-source-agreements --accept-package-agreements
            # PATH der laufenden Sitzung auffrischen
            $env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
        }
    }
    if (-not (Test-DotnetSdk)) {
        throw 'Ohne .NET SDK 8 kann NWOS nicht gebaut werden. Bitte von https://dotnet.microsoft.com/download/dotnet/8.0 installieren und den Installer erneut starten.'
    }
}
Write-Host '  .NET SDK gefunden.'

# --- 2. Optional: Tests ------------------------------------------------------------
if ($RunTests) {
    Write-Step 'Fuehre Tests aus'
    & dotnet test (Join-Path $SourceRoot 'NWOS.sln') --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Tests sind fehlgeschlagen - Installation abgebrochen.' }
}

# --- 3. Laufende Instanz beenden (Update) -------------------------------------------
Write-Step 'Beende ggf. laufende NWOS-Prozesse'
Get-Process -Name 'NWOS.Bootstrap.Windows', 'NWOS.DesktopShell.Ui' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# --- 4. Bauen und installieren -------------------------------------------------------
Write-Step 'Baue und installiere NWOS (kann einige Minuten dauern)'
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Publish-Project 'src\NWOS.Bootstrap.Windows\NWOS.Bootstrap.Windows.csproj' (Join-Path $InstallDir 'Cardinal')
Publish-Project 'src\NWOS.DesktopShell.Ui\NWOS.DesktopShell.Ui.csproj' (Join-Path $InstallDir 'Ui')

# --- 5. Konfiguration ------------------------------------------------------------------
Write-Step 'Konfiguration'
$configPath = Join-Path $DataDir 'nwos.config.json'
if (Test-Path $configPath) {
    Write-Host "  Bestehende Konfiguration bleibt erhalten: $configPath"
} else {
    $cfg = [ordered]@{
        EnableDesktopShell     = $true
        EnableSmartBoxes       = $true
        EnableWidgets          = $true
        EnableAlya             = $true
        EnableMarc             = $true
        EnableAlice            = $true
        EnableMultiDevice      = $false   # noch nicht implementiert
        EnableMaintenance      = $false   # noch nicht implementiert
        EnableSystemCenter     = $true
        MaintenanceWindowStart = $null
        MaintenanceWindowEnd   = $null
    }

    if (-not (Ask-YesNo 'Alle Module aktivieren (Desktop-Shell, Widgets, Alice, Marc, Alya, System Center)?')) {
        $cfg.EnableWidgets      = Ask-YesNo '  Widgets aktivieren?'
        $cfg.EnableAlice        = Ask-YesNo '  Alice (Sicherheit) aktivieren? (empfohlen)'
        $cfg.EnableMarc         = Ask-YesNo '  Marc (Arbeits-KI) aktivieren?'
        $cfg.EnableAlya         = Ask-YesNo '  Alya (Benutzer-KI) aktivieren?'
        $cfg.EnableSystemCenter = Ask-YesNo '  System Center aktivieren?'
    }

    New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
    $json = $cfg | ConvertTo-Json
    [System.IO.File]::WriteAllText($configPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "  Konfiguration geschrieben: $configPath"
}

# --- 6. Starter und Verknuepfungen -------------------------------------------------------
Write-Step 'Erstelle Starter und Verknuepfungen'
$launcher = Join-Path $InstallDir 'Start-NWOS.bat'
@(
    '@echo off',
    'rem Startet Cardinal (minimiert, falls noch nicht aktiv) und danach die Oberflaeche.',
    'tasklist /fi "imagename eq NWOS.Bootstrap.Windows.exe" | find /i "NWOS.Bootstrap.Windows" >nul || start "" /min "%~dp0Cardinal\NWOS.Bootstrap.Windows.exe"',
    'timeout /t 3 /nobreak >nul',
    'start "" "%~dp0Ui\NWOS.DesktopShell.Ui.exe"'
) | Set-Content -Path $launcher -Encoding ASCII

$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'NWOS.lnk'
$startMenuDir = Join-Path ([Environment]::GetFolderPath('Programs')) 'NWOS'
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null
New-Shortcut $desktopLink $launcher $InstallDir 'NWOS starten'
New-Shortcut (Join-Path $startMenuDir 'NWOS.lnk') $launcher $InstallDir 'NWOS starten'
Write-Host '  Verknuepfungen: Desktop und Startmenue.'

# --- 7. Autostart ------------------------------------------------------------------------
if (-not $NoAutostart -and (Ask-YesNo 'NWOS-Kern (Cardinal, inkl. Alice-Schutz) bei der Anmeldung automatisch starten?')) {
    $exe = Join-Path $InstallDir 'Cardinal\NWOS.Bootstrap.Windows.exe'
    $runValue = 'cmd.exe /c start "" /min "' + $exe + '"'
    Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'NWOS' -Value $runValue
    Write-Host '  Autostart eingerichtet (nur fuer diesen Benutzer).'
}

# --- 8. Deinstallations-Eintrag ------------------------------------------------------------
Write-Step 'Registriere Deinstallation (Windows-Einstellungen > Apps)'
Copy-Item (Join-Path $SourceRoot 'Uninstall-NWOS.ps1') (Join-Path $InstallDir 'Uninstall-NWOS.ps1') -Force
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NWOS'
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name 'DisplayName' -Value 'NWOS - NightWing Operating System'
Set-ItemProperty -Path $uninstallKey -Name 'DisplayVersion' -Value '0.1.0'
Set-ItemProperty -Path $uninstallKey -Name 'Publisher' -Value 'NWOS'
Set-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $InstallDir
Set-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $InstallDir 'Uninstall-NWOS.ps1') + '"')
New-ItemProperty -Path $uninstallKey -Name 'NoModify' -Value 1 -PropertyType DWord -Force | Out-Null

# --- 9. Optional: Shell-Registrierung ---------------------------------------------------------
if ($OfferShellRegistration) {
    Write-Step 'Optional: NWOS als Windows-Shell registrieren'
    Write-Host 'ACHTUNG: Ersetzt nach dem naechsten Login den normalen Windows-Desktop. Vorher in einer VM testen!' -ForegroundColor Yellow
    & (Join-Path $InstallDir 'Cardinal\NWOS.Bootstrap.Windows.exe') register-shell
}

# --- 10. Start ------------------------------------------------------------------------------------
Write-Step 'Fertig'
Write-Host "NWOS ist installiert in: $InstallDir" -ForegroundColor Green
Write-Host 'Start: Desktop-Verknuepfung "NWOS" oder Startmenue.'
Write-Host 'Deinstallieren: Windows-Einstellungen > Apps, oder NWOS-Uninstall.bat.'
Write-Host 'Als Windows-Shell registrieren (optional, nur nach Test in einer VM):'
Write-Host "  `"$InstallDir\Cardinal\NWOS.Bootstrap.Windows.exe`" register-shell"
Write-Host 'Zurueck zu explorer.exe (Notausgang):'
Write-Host "  `"$InstallDir\Cardinal\NWOS.Bootstrap.Windows.exe`" restore-shell"

if (-not $NoLaunch) {
    Write-Step 'Starte NWOS'
    Start-Process -FilePath $launcher -WorkingDirectory $InstallDir -WindowStyle Minimized
}
