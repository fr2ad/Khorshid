# Builds Khorshid.exe with the C# compiler included in Windows (.NET Framework 4.x).
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Set-Location $PSScriptRoot
& $csc /nologo /target:winexe /codepage:65001 /optimize+ /out:Khorshid.exe /win32icon:assets\Khorshid.ico `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll Khorshid.cs
