<#
.SYNOPSIS
    Entfernt NWOS fuer den aktuellen Benutzer.
.DESCRIPTION
    Stellt zuerst - falls noetig - explorer.exe als Windows-Shell wieder her, beendet NWOS,
    entfernt Autostart, Verknuepfungen und den Deinstallations-Eintrag und loescht den
    Programmordner. Die Daten (%LOCALAPPDATA%\NWOS: Konfiguration, Boxen, Quarantaene)
    bleiben standardmaessig erhalten.
.PARAMETER RemoveData
    Auch die Daten loeschen (inkl. Alice-Quarantaene, unwiderruflich).
.PARAMETER Quiet
    Keine Rueckfragen.
#>
[CmdletBinding()]
param(
    [switch]$RemoveData,
    [switch]$Quiet
)

$ErrorActionPreference = 'Continue'
$InstallDir = $PSScriptRoot
$DataDir = Join-Path $env:LOCALAPPDATA 'NWOS'
$exe = Join-Path $InstallDir 'Cardinal\NWOS.Bootstrap.Windows.exe'

function Write-Step([string]$Text) { Write-Host ""; Write-Host "==> $Text" -ForegroundColor Cyan }

Write-Host 'NWOS Deinstallation' -ForegroundColor Green

# 1. Shell zuerst zuruecksetzen, damit der Nutzer beim naechsten Login einen normalen Desktop hat.
Write-Step 'Pruefe Shell-Registrierung'
if (Test-Path $exe) {
    try {
        $status = (& $exe shell-status) -join ' '
        if ($status -match 'registriert' -and $status -notmatch 'NICHT') {
            Write-Host '  NWOS ist als Windows-Shell registriert - stelle explorer.exe wieder her.'
            & $exe restore-shell
        } else {
            Write-Host '  NWOS ist nicht als Shell registriert - nichts zu tun.'
        }
    } catch {
        Write-Host "  Konnte den Shell-Status nicht pruefen: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host '  Sicherheitshalber wird explorer.exe als Shell wiederhergestellt.'
        try { & $exe restore-shell } catch { }
    }
}

Write-Step 'Beende NWOS-Prozesse'
Get-Process -Name 'NWOS.Bootstrap.Windows', 'NWOS.DesktopShell.Ui' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Step 'Entferne Autostart, Verknuepfungen und Deinstallations-Eintrag'
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'NWOS' -ErrorAction SilentlyContinue
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NWOS' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path ([Environment]::GetFolderPath('Desktop')) 'NWOS.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path ([Environment]::GetFolderPath('Programs')) 'NWOS') -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path $DataDir) {
    $delete = $RemoveData
    if (-not $delete -and -not $Quiet) {
        $answer = Read-Host "Auch die NWOS-Daten loeschen ($DataDir - inkl. Alice-Quarantaene, unwiderruflich)? [j/N]"
        $delete = ($answer.Trim().ToLower() -in @('j', 'ja', 'y', 'yes'))
    }
    if ($delete) {
        Remove-Item -Path $DataDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host '  Daten geloescht.'
    } else {
        Write-Host "  Daten bleiben erhalten: $DataDir"
    }
}

Write-Step 'Loesche Programmordner'
# Dieses Skript liegt selbst im Programmordner - Loeschung daher verzoegert ueber einen separaten Prozess.
Set-Location $env:TEMP
Start-Process -FilePath 'cmd.exe' -WindowStyle Hidden `
    -ArgumentList ('/c timeout /t 3 /nobreak >nul & rmdir /s /q "' + $InstallDir + '"')

Write-Host 'NWOS wurde entfernt.' -ForegroundColor Green
