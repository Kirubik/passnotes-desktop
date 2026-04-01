[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$DistributionOutputDir,
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

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function New-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "PassNotes.csproj"
$installerBuildScriptPath = Join-Path $repoRoot "build\build-installer.ps1"
$instructionTemplatePath = Join-Path $repoRoot "distribution\INSTALL_RU.template.txt"
$boostyPostTemplatePath = Join-Path $repoRoot "distribution\BOOSTY_POST_RU.template.txt"
$boostyHandoffTemplatePath = Join-Path $repoRoot "distribution\BOOSTY_HANDOFF_RU.template.txt"

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $installerBuildScriptPath)) {
    throw "Installer build script not found: $installerBuildScriptPath"
}

if (-not (Test-Path -LiteralPath $instructionTemplatePath)) {
    throw "Distribution instruction template not found: $instructionTemplatePath"
}

if (-not (Test-Path -LiteralPath $boostyPostTemplatePath)) {
    throw "Boosty post template not found: $boostyPostTemplatePath"
}

if (-not (Test-Path -LiteralPath $boostyHandoffTemplatePath)) {
    throw "Boosty publication handoff template not found: $boostyHandoffTemplatePath"
}

$version = Get-ProjectVersion -ProjectPath $projectPath

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
}

if ([string]::IsNullOrWhiteSpace($InstallerOutputDir)) {
    $InstallerOutputDir = Join-Path $repoRoot "artifacts\installer"
}

if ([string]::IsNullOrWhiteSpace($DistributionOutputDir)) {
    $DistributionOutputDir = Join-Path $repoRoot "artifacts\distribution"
}

Assert-PathInsideRepo -RepoRoot $repoRoot -PathToCheck $PublishDir
Assert-PathInsideRepo -RepoRoot $repoRoot -PathToCheck $InstallerOutputDir
Assert-PathInsideRepo -RepoRoot $repoRoot -PathToCheck $DistributionOutputDir

if (Test-Path -LiteralPath $DistributionOutputDir) {
    Remove-Item -LiteralPath $DistributionOutputDir -Recurse -Force -ErrorAction Stop
}

New-Directory -Path $DistributionOutputDir

Write-Host "Building installer and publish artifacts..." -ForegroundColor Cyan
& $installerBuildScriptPath `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -SelfContained $SelfContained `
    -PublishDir $PublishDir `
    -InstallerOutputDir $InstallerOutputDir `
    -InnoSetupCompilerPath $InnoSetupCompilerPath

if ($LASTEXITCODE -ne 0) {
    throw "build-installer.ps1 failed with exit code $LASTEXITCODE"
}

$installerFileName = "PassNotesDesktopSetup_{0}.exe" -f $version
$installerSourcePath = Join-Path $InstallerOutputDir $installerFileName
if (-not (Test-Path -LiteralPath $installerSourcePath)) {
    throw "Installer output not found: $installerSourcePath"
}

$portableRootName = "PassNotesDesktop_{0}" -f $version
$portableZipName = "{0}_portable.zip" -f $portableRootName
$portableStageRoot = Join-Path $DistributionOutputDir "_portable_stage"
$portableStageDir = Join-Path $portableStageRoot $portableRootName
$portableZipPath = Join-Path $DistributionOutputDir $portableZipName

New-Directory -Path $portableStageDir

Get-ChildItem -LiteralPath $PublishDir -Recurse -File | ForEach-Object {
    if ($_.Extension -ieq ".pdb") {
        return
    }

    $relativePath = [System.IO.Path]::GetRelativePath($PublishDir, $_.FullName)
    $targetPath = Join-Path $portableStageDir $relativePath
    $targetDirectory = Split-Path -Path $targetPath -Parent
    New-Directory -Path $targetDirectory
    Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
}

Compress-Archive -Path $portableStageDir -DestinationPath $portableZipPath -CompressionLevel Optimal
Remove-Item -LiteralPath $portableStageRoot -Recurse -Force -ErrorAction Stop

$installerDestinationPath = Join-Path $DistributionOutputDir $installerFileName
Copy-Item -LiteralPath $installerSourcePath -Destination $installerDestinationPath -Force

$instructionFileName = "INSTALL_RU.txt"
$instructionDestinationPath = Join-Path $DistributionOutputDir $instructionFileName
$instructionTemplate = Get-Content -LiteralPath $instructionTemplatePath -Raw
$instructionContent = $instructionTemplate.Replace("{{VERSION}}", $version)
$instructionContent = $instructionContent.Replace("{{INSTALLER_NAME}}", $installerFileName)
$instructionContent = $instructionContent.Replace("{{PORTABLE_NAME}}", $portableZipName)
Write-Utf8NoBom -Path $instructionDestinationPath -Content $instructionContent

$boostyPostDestinationPath = Join-Path $DistributionOutputDir "BOOSTY_POST_RU.txt"
$boostyPostTemplate = Get-Content -LiteralPath $boostyPostTemplatePath -Raw
$boostyPostContent = $boostyPostTemplate.Replace("{{VERSION}}", $version)
$boostyPostContent = $boostyPostContent.Replace("{{INSTALLER_NAME}}", $installerFileName)
$boostyPostContent = $boostyPostContent.Replace("{{PORTABLE_NAME}}", $portableZipName)
Write-Utf8NoBom -Path $boostyPostDestinationPath -Content $boostyPostContent

$boostyHandoffDestinationPath = Join-Path $DistributionOutputDir "BOOSTY_HANDOFF_RU.txt"
$boostyHandoffTemplate = Get-Content -LiteralPath $boostyHandoffTemplatePath -Raw
$boostyHandoffContent = $boostyHandoffTemplate.Replace("{{VERSION}}", $version)
$boostyHandoffContent = $boostyHandoffContent.Replace("{{INSTALLER_NAME}}", $installerFileName)
$boostyHandoffContent = $boostyHandoffContent.Replace("{{PORTABLE_NAME}}", $portableZipName)
Write-Utf8NoBom -Path $boostyHandoffDestinationPath -Content $boostyHandoffContent

$checksumsPath = Join-Path $DistributionOutputDir "SHA256SUMS.txt"
$checksumLines = @(
    "{0} *{1}" -f (Get-FileHash -LiteralPath $installerDestinationPath -Algorithm SHA256).Hash.ToLowerInvariant(), $installerFileName
    "{0} *{1}" -f (Get-FileHash -LiteralPath $portableZipPath -Algorithm SHA256).Hash.ToLowerInvariant(), $portableZipName
)
Write-Utf8NoBom -Path $checksumsPath -Content (($checksumLines -join [Environment]::NewLine) + [Environment]::NewLine)

Write-Host ""
Write-Host "Distribution output:" -ForegroundColor Green
Get-ChildItem -LiteralPath $DistributionOutputDir -Force | Select-Object Name, Length, LastWriteTime
