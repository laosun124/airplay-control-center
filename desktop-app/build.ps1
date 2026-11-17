$ErrorActionPreference = 'Stop'
$appRoot = Split-Path $PSScriptRoot
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /codepage:65001 "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\assets\airplay.ico" "/out:$appRoot\AirPlayControl.exe" "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" /r:System.Xaml.dll "$PSScriptRoot\AirPlayControl.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
