# Regenerates docs\*.png, assets\logo.png, assets\Khorshid.ico and packaging\Assets\*.png.
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$root = Split-Path $PSScriptRoot
$exe = Join-Path $env:TEMP 'Khorshid-AssetGen.exe'
& $csc /nologo /target:exe /codepage:65001 /main:Khorshid.Tools.AssetGen /out:$exe `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    (Join-Path $root 'Khorshid.cs') (Join-Path $PSScriptRoot 'AssetGen.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $exe $root
Remove-Item $exe
