$ErrorActionPreference = "Stop"

$projectPath = ".\src\GameServerManager\GameServerManager.csproj"
$publishDir = ".\dist\portable\GameServerManager"
$zipPath = ".\dist\GameServerManager-v1.2.0-Portable.zip"

Write-Host "Cleaning dist folder..."
if (Test-Path ".\dist") {
    Remove-Item -Recurse -Force ".\dist"
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing project (Portable)..."
# Publish self-contained or framework-dependent? The spec implies portable. Let's do framework-dependent to keep it small, but self-contained is safer for no-install. We will use self-contained for max portability.
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDir

Write-Host "Creating Portable ZIP archive..."
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

Write-Host "Build and package complete!"
Write-Host "Portable ZIP created at: $zipPath"

$isccPath = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (Test-Path $isccPath) {
    Write-Host "Building Setup EXE..."
    & $isccPath ".\installer.iss"
    Write-Host "Setup EXE created!"
} else {
    Write-Host "Inno Setup 6 (ISCC.exe) not found. Skipping Setup EXE creation."
}
