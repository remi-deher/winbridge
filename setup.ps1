<#
.SYNOPSIS
    WinBridge Setup & Bootstrap Script
    Initialise la solution, répare les cibles et restaure les dépendances dans le bon ordre.
#>

$ErrorActionPreference = "Stop"
Write-Host "--- WinBridge Framework Setup ---" -ForegroundColor Cyan

# 1. Nettoyage des résidus de build
Write-Host "[1/4] Cleaning build artifacts..."
$itemsToClean = "bin", "obj", ".vs", "project.assets.json"
Get-ChildItem -Path . -Recurse -Include $itemsToClean | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# 2. Restauration & Build du Core (Any CPU)
Write-Host "[2/4] Bootstrapping WinBridge.Core..." -ForegroundColor Yellow
dotnet restore "WinBridge.Core/WinBridge.Core.csproj"
dotnet build "WinBridge.Core/WinBridge.Core.csproj" -c Release

# 3. Restauration & Build du SDK (Any CPU)
Write-Host "[3/4] Bootstrapping WinBridge.SDK..." -ForegroundColor Yellow
dotnet restore "WinBridge.SDK/WinBridge.SDK.csproj"
dotnet build "WinBridge.SDK/WinBridge.SDK.csproj" -c Release

# 4. Restauration de l'App (Architecture spécifique x64)
# On force le RID win-x64 pour éviter les erreurs de compatibilité WinUI 3
Write-Host "[4/4] Bootstrapping WinBridge.App (x64)..." -ForegroundColor Yellow
dotnet restore "WinBridge.App/WinBridge.App.csproj" -r win-x64
dotnet build "WinBridge.App/WinBridge.App.csproj" -c Debug -r win-x64 --no-self-contained

Write-Host "SETUP TERMINE avec succès !" -ForegroundColor Green
Write-Host "Vous pouvez maintenant ouvrir WinBridge.slnx dans Visual Studio."