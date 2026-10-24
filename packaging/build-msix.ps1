# Builds dist\Khorshid_<version>.msix for the Microsoft Store.
#
#   .\packaging\build-msix.ps1 -IdentityName 12345Farzad.Khorshid -Publisher 'CN=XXXXXXXX-XXXX-...' -Version 1.0.0.0
#
# Take IdentityName, Publisher and PublisherDisplayName from Partner Center
# (your app > Product management > Product identity). Upload the unsigned .msix;
# the Store signs it. Use -Sign only to make a self-signed package for local testing.

param(
    [string]$IdentityName = 'Khorshid.Dev',
    [string]$Publisher = 'CN=Khorshid Dev',
    [string]$PublisherDisplayName = 'Farzad Farazmand',
    [string]$DisplayName = 'Khorshid',
    [string]$Version = '1.0.0.0',
    [switch]$Sign
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'msix'

# Windows SDK tools: the Microsoft.Windows.SDK.BuildTools NuGet package unpacked into
# .tools\buildtools (see packaging\README.md), or an installed Windows SDK.
$sdkBin = Get-ChildItem (Join-Path $root '.tools\buildtools\bin'), 'C:\Program Files (x86)\Windows Kits\10\bin' `
    -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdkBin) { throw 'makeappx.exe not found. See packaging\README.md to get the Windows SDK build tools.' }
$tools = Join-Path $sdkBin.FullName 'x64'

# 1. Build the app
& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# 2. Stage the package layout
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'Assets') | Out-Null
Copy-Item (Join-Path $root 'Khorshid.exe') $stage
Copy-Item (Join-Path $PSScriptRoot 'Assets\*.png') (Join-Path $stage 'Assets')
$manifest = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AppxManifest.xml'))
$values = @{ IdentityName = $IdentityName; Publisher = $Publisher; PublisherDisplayName = $PublisherDisplayName;
             DisplayName = $DisplayName; Version = $Version }
foreach ($k in $values.Keys) { $manifest = $manifest.Replace("`$$k`$", [Security.SecurityElement]::Escape($values[$k])) }
[IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

# 3. Resource index, so Windows picks the right logo size for each scale and target size
$priConfig = Join-Path $dist 'priconfig.xml'
& "$tools\makepri.exe" createconfig /cf $priConfig /dq en-US /o | Out-Null
& "$tools\makepri.exe" new /pr $stage /cf $priConfig /mn (Join-Path $stage 'AppxManifest.xml') /of (Join-Path $stage 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri failed.' }
Remove-Item $priConfig

# 4. Pack
$msix = Join-Path $dist "Khorshid_$Version.msix"
& "$tools\makeappx.exe" pack /d $stage /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed.' }

# 5. Optional self-signed signature for sideloading on this PC
if ($Sign) {
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Publisher -and $_.HasPrivateKey } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature `
            -CertStoreLocation Cert:\CurrentUser\My -FriendlyName 'Khorshid test signing' `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }
    Export-Certificate -Cert $cert -FilePath (Join-Path $dist 'Khorshid-test.cer') | Out-Null
    & "$tools\signtool.exe" sign /fd SHA256 /sha1 $cert.Thumbprint /s My $msix | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'signtool failed.' }
}

Write-Host "Built $msix"
