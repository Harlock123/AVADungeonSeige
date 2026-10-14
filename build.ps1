# Publishes AVADungeonSeige as a self-contained single-file executable for every release
# platform: Windows, macOS and Linux, each on x64 and ARM64. Output: BUILDS\<PLATFORM>\.
$ErrorActionPreference = "Stop"

$SolutionRoot = $PSScriptRoot
$Project = Join-Path $SolutionRoot "src/DungeonSiege.App/DungeonSiege.App.csproj"
$BuildsDir = Join-Path $SolutionRoot "BUILDS"

# Ordered so the summary lists the platforms the same way every time.
$Targets = [ordered]@{
    "win-x64"     = "WINDOWSx64"
    "win-arm64"   = "WINDOWSarm64"
    "osx-x64"     = "MACOSx64"
    "osx-arm64"   = "MACOSarm64"
    "linux-x64"   = "LINUXx64"
    "linux-arm64" = "LINUXarm64"
}

Write-Host "========================================"
Write-Host "  AVADungeonSeige - Publish Build Script"
Write-Host "========================================"

if (Test-Path $BuildsDir) {
    Write-Host "Cleaning previous builds..."
    Remove-Item -Recurse -Force $BuildsDir
}
New-Item -ItemType Directory -Path $BuildsDir | Out-Null

$Failed = @()

foreach ($RID in $Targets.Keys) {
    $Folder = $Targets[$RID]
    $Output = Join-Path $BuildsDir $Folder

    Write-Host ""
    Write-Host "--- Building $Folder ($RID) ---"

    # Single-file, self-contained and compression settings live in the .csproj and switch on
    # whenever a runtime identifier is given.
    dotnet publish $Project --configuration Release --runtime $RID --output $Output --nologo

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  -> $Folder OK"
    } else {
        Write-Host "  -> $Folder FAILED"
        $Failed += $Folder
    }
}

Write-Host ""
Write-Host "========================================"
Write-Host "  Build Summary"
Write-Host "========================================"

foreach ($RID in $Targets.Keys) {
    $Folder = $Targets[$RID]
    $Output = Join-Path $BuildsDir $Folder
    if ((Test-Path $Output) -and (Get-ChildItem $Output -ErrorAction SilentlyContinue)) {
        $Size = "{0:N1} MB" -f ((Get-ChildItem $Output -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB)
        Write-Host "  $Folder  ->  $Size"
    } else {
        Write-Host "  $Folder  ->  MISSING"
    }
}

if ($Failed.Count -gt 0) {
    Write-Host ""
    Write-Host "FAILED: $($Failed -join ', ')"
    exit 1
}
Write-Host ""
Write-Host "All builds completed successfully!"
Write-Host "Output: $BuildsDir"
