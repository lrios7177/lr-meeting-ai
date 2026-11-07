param([string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", [switch]$IncludeAI)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if ($IncludeAI) { & (Join-Path $PSScriptRoot 'Prepare-Engines.ps1') }
    dotnet publish src/LRMeeting.App/LRMeeting.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación .NET.' }
    if (!(Test-Path $Iscc)) { throw 'Instala Inno Setup 6 o proporciona -Iscc con la ruta a ISCC.exe.' }
    & $Iscc installer/LRMeetingAI.iss
    if ($LASTEXITCODE -ne 0) { throw 'Falló la creación del instalador.' }
} finally { Pop-Location }
