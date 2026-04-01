[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$PublishDir,
    [string]$InstallerOutputDir,
    [string]$InnoSetupCompilerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-PathInsideRepo {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$PathToCheck
    )

    $resolvedRepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($PathToCheck)
    if (-not $resolvedPath.StartsWith($resolvedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside repository root: $resolvedPath"
    }
}

function Get-ProjectVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath
    $versionNode = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($versionNode)) {
        throw "Could not read <Version> from $ProjectPath"
    }

    return $versionNode.Trim()
}

function Resolve-InnoSetupCompiler {
    param(
        [string]$RequestedPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path -LiteralPath $RequestedPath)) {
            throw "Inno Setup compiler not found at requested path: $RequestedPath"
        }

        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup 6 compiler (ISCC.exe) was not found in the standard locations."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "PassNotes.csproj"
$installerScriptPath = Join-Path $repoRoot "installer\PassNotesDesktop.iss"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $installerScriptPath)) {
    throw "Installer script not found: $installerScriptPath"
}

$version = Get-ProjectVersion -ProjectPath $projectPath

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
}

if ([string]::IsNullOrWhiteSpace($InstallerOutputDir)) {
    $InstallerOutputDir = Join-Path $repoRoot "artifacts\installer"
}

Assert-PathInsideRepo -RepoRoot $repoRoot -PathToCheck $PublishDir
Assert-PathInsideRepo -RepoRoot $repoRoot -PathToCheck $InstallerOutputDir

if (Test-Path -LiteralPath $PublishDir) {
    Remove-Item -LiteralPath $PublishDir -Recurse -Force -ErrorAction Stop
}

if (-not (Test-Path -LiteralPath $InstallerOutputDir)) {
    New-Item -ItemType Directory -Path $InstallerOutputDir -Force | Out-Null
}

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", ($SelfContained.ToString().ToLowerInvariant()),
    "-o", $PublishDir
)

Write-Host "Publishing PassNotes Desktop $version..." -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$isccPath = Resolve-InnoSetupCompiler -RequestedPath $InnoSetupCompilerPath
$installerDir = Split-Path -Path $installerScriptPath -Parent
$installerScriptName = Split-Path -Path $installerScriptPath -Leaf

Push-Location $installerDir
try {
    Write-Host "Building Inno Setup installer..." -ForegroundColor Cyan
    & $isccPath `
        "/DMyAppVersion=$version" `
        "/DPublishDir=$PublishDir" `
        "/DInstallerOutputDir=$InstallerOutputDir" `
        $installerScriptName

    if ($LASTEXITCODE -ne 0) {
        throw "ISCC.exe failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Publish directory:" -ForegroundColor Green
Write-Host "  $PublishDir"
Write-Host "Installer output:" -ForegroundColor Green
Get-ChildItem -LiteralPath $InstallerOutputDir -Force | Select-Object Name, Length, LastWriteTime
