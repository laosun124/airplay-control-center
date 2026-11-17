param([string]$Version = '1.0.0', [string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$original = Join-Path $projectRoot 'releases\java-airplay-mse-1.1.1.jar'
if (-not (Test-Path -LiteralPath $original)) {
    New-Item -ItemType Directory -Force (Split-Path $original) | Out-Null
    Invoke-WebRequest 'https://github.com/serezhka/java-airplay/releases/download/v1.1.1/java-airplay-mse-1.1.1.jar' -OutFile $original
}
python "$PSScriptRoot\build-windowed.py"
if ($LASTEXITCODE -ne 0) { throw 'Receiver build failed' }
& "$PSScriptRoot\build.ps1"
$packageName = "AirPlay-Control-Center-$Version-windows-x64"
$staging = Join-Path $projectRoot ("work\packages\$packageName-" + [Guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $staging) { throw 'Package staging directory already exists. Use a new version or remove the previous staging directory.' }
New-Item -ItemType Directory -Force "$staging\desktop-app\assets", "$staging\releases" | Out-Null
foreach ($name in @('AirPlayControl.exe', 'README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'Download Java.html')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $staging
}
Copy-Item -LiteralPath "$PSScriptRoot\MainWindow.xaml" -Destination "$staging\desktop-app"
Copy-Item -LiteralPath "$PSScriptRoot\assets\airplay.ico" -Destination "$staging\desktop-app\assets"
Copy-Item -LiteralPath "$PSScriptRoot\LICENSE.upstream" -Destination "$staging\desktop-app"
Copy-Item -LiteralPath "$projectRoot\releases\java-airplay-mse-1.1.1-windowed.jar" -Destination "$staging\releases"
$dist = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Force $dist | Out-Null
$archive = Join-Path $dist "$packageName.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($staging, $archive)
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $packageName.zip" } | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Write-Output $archive
