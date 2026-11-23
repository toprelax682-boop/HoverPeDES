# Local Windows build: publishes the app, fetches libmpv + yt-dlp, builds the Inno Setup installer.
# Requirements: .NET 8 SDK, Inno Setup 6, 7-Zip (7z on PATH) and GitHub CLI (gh).
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet publish src/HoverPeDES/HoverPeDES.csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -o publish

New-Item -ItemType Directory -Force mpv | Out-Null
gh release download --repo shinchiro/mpv-winbuild-cmake --pattern 'mpv-dev-lgpl-x86_64-2*.7z' --dir mpv --clobber
$archive = (Get-ChildItem mpv\*.7z | Select-Object -First 1).FullName
7z e $archive "-ompv\x" libmpv-2.dll -y
Copy-Item mpv\x\libmpv-2.dll publish\libmpv-2.dll -Force

Invoke-WebRequest https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe -OutFile publish\yt-dlp.exe

New-Item -ItemType Directory -Force installer\Languages | Out-Null
if (-not (Test-Path installer\Languages\Vietnamese.isl)) {
    Invoke-WebRequest https://raw.githubusercontent.com/jrsoftware/issrc/main/Files/Languages/Unofficial/Vietnamese.isl -OutFile installer\Languages\Vietnamese.isl
}

& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" "/DAppVersion=$Version" installer\HoverPeDES.iss
Write-Host "Done: dist\HoverPeDES-Setup-$Version.exe"
