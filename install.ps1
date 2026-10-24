# Installs (or removes) Khorshid for the current user. No admin rights needed.
#
#   Install:    irm https://raw.githubusercontent.com/fr2ad/Khorshid/main/install.ps1 | iex
#   Uninstall:  & ([scriptblock]::Create((irm https://raw.githubusercontent.com/fr2ad/Khorshid/main/install.ps1))) -Uninstall

param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$repo     = 'fr2ad/Khorshid'
$dir      = Join-Path $env:LOCALAPPDATA 'Programs\Khorshid'
$exe      = Join-Path $dir 'Khorshid.exe'
$shortcut = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Khorshid.lnk'
$runKey   = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

Get-Process Khorshid -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if ($Uninstall) {
    Remove-ItemProperty $runKey -Name Khorshid -ErrorAction SilentlyContinue
    Remove-Item $shortcut -ErrorAction SilentlyContinue
    Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Khorshid was uninstalled.'
    return
}

New-Item -ItemType Directory -Force $dir | Out-Null
Write-Host 'Downloading Khorshid...'
Invoke-WebRequest "https://github.com/$repo/releases/latest/download/Khorshid.exe" -OutFile $exe -UseBasicParsing
Unblock-File $exe

$link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $dir
$link.Description = 'Persian calendar in the Windows taskbar'
$link.Save()

Set-ItemProperty $runKey -Name Khorshid -Value "`"$exe`""
Start-Process $exe
Write-Host "Khorshid is installed in $dir and will start with Windows."
