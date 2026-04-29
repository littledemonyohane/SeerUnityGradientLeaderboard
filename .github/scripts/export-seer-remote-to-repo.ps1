param(
    [string]$InstallRoot,
    [string]$MirrorRoot,
    [string]$ExportRoot,
    [string]$RepoOutputRoot,
    [string]$PythonPath,
    [string[]]$HeadIds,
    [string[]]$CountermarkIds,
    [switch]$AllHeads,
    [switch]$AllCountermarks,
    [switch]$SkipConfig,
    [switch]$BootstrapPythonPackages
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw 'PathValue cannot be empty.'
    }

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $PathValue))
}

function Resolve-RepoRoot {
    return Resolve-FullPath -PathValue (Join-Path $PSScriptRoot '..\..')
}

function Resolve-DefaultPath {
    param(
        [string]$RequestedPath,
        [Parameter(Mandatory = $true)]
        [string]$DefaultPath
    )

    if ($RequestedPath) {
        return Resolve-FullPath -PathValue $RequestedPath
    }

    return Resolve-FullPath -PathValue $DefaultPath
}

function Normalize-AssetIdList {
    param([string[]]$Values)

    $normalized = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($value in $Values) {
        if (-not $value) {
            continue
        }

        foreach ($part in ([string]$value -split ',')) {
            $cleaned = $part.Trim()
            if ($cleaned -and $seen.Add($cleaned)) {
                [void]$normalized.Add($cleaned)
            }
        }
    }

    return $normalized
}

function Assert-SafeDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath,
        [string]$AllowedParent
    )

    $fullPath = Resolve-FullPath -PathValue $DirectoryPath
    $rootPath = [System.IO.Path]::GetPathRoot($fullPath)
    if ([string]::Equals($fullPath.TrimEnd('\'), $rootPath.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate on filesystem root: $fullPath"
    }

    if ($AllowedParent) {
        $resolvedParent = Resolve-FullPath -PathValue $AllowedParent
        $normalizedPath = $fullPath.TrimEnd('\') + '\'
        $normalizedParent = $resolvedParent.TrimEnd('\') + '\'
        if (-not $normalizedPath.StartsWith($normalizedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Path is outside the allowed parent.`nPath: $fullPath`nAllowedParent: $resolvedParent"
        }
    }

    return $fullPath
}

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath
    )

    $resolved = Assert-SafeDirectory -DirectoryPath $DirectoryPath
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }

    [System.IO.Directory]::CreateDirectory($resolved) | Out-Null
    return $resolved
}

function Remove-DirectoryIfExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath,
        [string]$AllowedParent
    )

    $resolved = Assert-SafeDirectory -DirectoryPath $DirectoryPath -AllowedParent $AllowedParent
    if (Test-Path -LiteralPath $resolved) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

function Resolve-PythonPath {
    param([string]$RequestedPythonPath)

    if ($RequestedPythonPath) {
        $trimmedPath = $RequestedPythonPath.Trim()
        $command = Get-Command $trimmedPath -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }

        return Resolve-FullPath -PathValue $trimmedPath
    }

    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($pythonCommand) {
        return $pythonCommand.Source
    }

    $pyCommand = Get-Command py -ErrorAction SilentlyContinue
    if ($pyCommand) {
        return $pyCommand.Source
    }

    throw 'Unable to locate Python. Install Python 3 or pass -PythonPath.'
}

function Invoke-SimpleProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$Arguments
    )

    $effectiveArguments = @()
    if ([System.IO.Path]::GetFileName($FilePath) -ieq 'py.exe') {
        $effectiveArguments += '-3'
    }
    $effectiveArguments += $Arguments

    $rawLines = & $FilePath @effectiveArguments 2>&1
    $exitCode = $LASTEXITCODE
    $rawOutput = ($rawLines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $rawOutput
    }
}

function Test-PythonDependencies {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedPythonPath
    )

    $result = Invoke-SimpleProcess -FilePath $ResolvedPythonPath -Arguments @('-c', 'import UnityPy, PIL')
    return $result.ExitCode -eq 0
}

function Ensure-PythonDependencies {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedPythonPath,
        [switch]$Bootstrap
    )

    if (Test-PythonDependencies -ResolvedPythonPath $ResolvedPythonPath) {
        return
    }

    if (-not $Bootstrap) {
        throw "Python was found at $ResolvedPythonPath but UnityPy/Pillow are missing. Install them or pass -BootstrapPythonPackages."
    }

    Write-Host 'Installing UnityPy and Pillow...'
    $install = Invoke-SimpleProcess -FilePath $ResolvedPythonPath -Arguments @('-m', 'pip', 'install', '--disable-pip-version-check', 'UnityPy', 'pillow')
    if ($install.ExitCode -ne 0) {
        throw "pip install failed.`n$($install.Output)"
    }

    if (-not (Test-PythonDependencies -ResolvedPythonPath $ResolvedPythonPath)) {
        throw 'UnityPy/Pillow are still unavailable after pip install.'
    }
}

function ConvertFrom-JsonText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $trimmed = $Text.Trim()
    if (-not $trimmed) {
        throw 'Expected JSON output but received an empty response.'
    }

    try {
        return $trimmed | ConvertFrom-Json
    }
    catch {
        $start = $trimmed.IndexOf('{')
        $end = $trimmed.LastIndexOf('}')
        if ($start -ge 0 -and $end -gt $start) {
            $candidate = $trimmed.Substring($start, $end - $start + 1)
            return $candidate | ConvertFrom-Json
        }

        throw "Failed to parse JSON output.`n$trimmed"
    }
}

function Invoke-PythonJsonScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedPythonPath,
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [string[]]$Arguments
    )

    $result = Invoke-SimpleProcess -FilePath $ResolvedPythonPath -Arguments (@($ScriptPath) + $Arguments)
    if ($result.ExitCode -ne 0) {
        throw "Python script failed: $ScriptPath`n$($result.Output)"
    }

    return ConvertFrom-JsonText -Text $result.Output
}

function Copy-DirectoryToRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Source directory does not exist: $SourcePath"
    }

    $sourceItem = Get-Item -LiteralPath $SourcePath
    Copy-Item -LiteralPath $sourceItem.FullName -Destination $DestinationRoot -Recurse -Force
}

$repoRoot = Resolve-RepoRoot
$resolvedMirrorRoot = Resolve-DefaultPath -RequestedPath $MirrorRoot -DefaultPath (Join-Path $repoRoot 'SyncedSeerMirror-github')
$resolvedExportRoot = Resolve-DefaultPath -RequestedPath $ExportRoot -DefaultPath (Join-Path $repoRoot 'ExportedSeerMirror-github')
$resolvedRepoOutputRoot = Resolve-DefaultPath -RequestedPath $RepoOutputRoot -DefaultPath $repoRoot
$resolvedPythonPath = Resolve-PythonPath -RequestedPythonPath $PythonPath
$normalizedHeadIds = @(Normalize-AssetIdList -Values $HeadIds)
$normalizedCountermarkIds = @(Normalize-AssetIdList -Values $CountermarkIds)

$syncScriptPath = Join-Path $repoRoot 'tools\sync-seer-remote.ps1'
$configScriptPath = Join-Path $repoRoot 'tools\export-seer-config.py'
$imagesScriptPath = Join-Path $repoRoot 'tools\export-seer-images.py'

foreach ($requiredPath in @($syncScriptPath, $configScriptPath, $imagesScriptPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required helper was not found: $requiredPath"
    }
}

Ensure-PythonDependencies -ResolvedPythonPath $resolvedPythonPath -Bootstrap:$BootstrapPythonPackages

Write-Host "Mirror root: $resolvedMirrorRoot"
Write-Host "Export root: $resolvedExportRoot"
Write-Host "Repo output root: $resolvedRepoOutputRoot"

[System.IO.Directory]::CreateDirectory($resolvedMirrorRoot) | Out-Null
Reset-Directory -DirectoryPath $resolvedExportRoot | Out-Null
[System.IO.Directory]::CreateDirectory($resolvedRepoOutputRoot) | Out-Null

$existingRepoPetSkinPath = Join-Path $resolvedRepoOutputRoot 'config\pet_skin.json'
$stagedExportConfigDir = Join-Path $resolvedExportRoot 'config'
[System.IO.Directory]::CreateDirectory($stagedExportConfigDir) | Out-Null
$preservedExistingPetSkin = $false
if (Test-Path -LiteralPath $existingRepoPetSkinPath) {
    Copy-Item -LiteralPath $existingRepoPetSkinPath -Destination (Join-Path $stagedExportConfigDir 'pet_skin.json') -Force
    $preservedExistingPetSkin = $true
}

$syncParams = @{
    MirrorRoot = $resolvedMirrorRoot
    NoSummaryOutput = $true
}
if ($InstallRoot) {
    $syncParams.InstallRoot = Resolve-FullPath -PathValue $InstallRoot
}
if ($SkipConfig) {
    $syncParams.SkipConfig = $true
}
if ($normalizedHeadIds.Count -gt 0) {
    $syncParams.HeadIds = @($normalizedHeadIds)
}
if ($normalizedCountermarkIds.Count -gt 0) {
    $syncParams.CountermarkIds = @($normalizedCountermarkIds)
}
if ($AllHeads) {
    $syncParams.AllHeads = $true
}
if ($AllCountermarks) {
    $syncParams.AllCountermarks = $true
}

Write-Host 'Running remote sync...'
& $syncScriptPath @syncParams

$syncSummaryPath = Join-Path $resolvedMirrorRoot 'seer-remote-sync-summary.json'
if (-not (Test-Path -LiteralPath $syncSummaryPath)) {
    throw "Remote sync summary was not generated: $syncSummaryPath"
}
$syncSummary = Get-Content -LiteralPath $syncSummaryPath -Raw | ConvertFrom-Json

$needsImageExport = $AllHeads -or $AllCountermarks -or ($normalizedHeadIds.Count -gt 0) -or ($normalizedCountermarkIds.Count -gt 0)
$configSummary = $null
if (-not $SkipConfig) {
    Write-Host 'Exporting config JSON...'
    $configSummary = Invoke-PythonJsonScript `
        -ResolvedPythonPath $resolvedPythonPath `
        -ScriptPath $configScriptPath `
        -Arguments @(
            '--source-root', $resolvedMirrorRoot,
            '--output-dir', $resolvedExportRoot,
            '--layout', 'mirror'
        )
}

$imageSummary = [ordered]@{
    Success = $true
    SourceRoot = $resolvedMirrorRoot
    OutputRoot = $resolvedExportRoot
    DefaultPackageVersion = if ($syncSummary.Packages) {
        ($syncSummary.Packages | Where-Object { $_.Package -eq 'DefaultPackage' } | Select-Object -First 1).Version
    }
    else {
        ''
    }
    Heads = [ordered]@{
        Indexed = 0
        Attempted = 0
        Exported = 0
        Failed = 0
        OutputDirectory = ''
        SampleFailures = @()
        Source = 'skipped'
        Layout = 'mirror'
    }
    Countermarks = [ordered]@{
        Indexed = 0
        Attempted = 0
        Exported = 0
        Failed = 0
        OutputDirectory = ''
        SampleFailures = @()
        Source = 'skipped'
        Layout = 'mirror'
    }
    Error = ''
}

if ($needsImageExport) {
    Write-Host 'Exporting image assets...'
    $imageArgs = @(
        '--source-root', $resolvedMirrorRoot,
        '--output-dir', $resolvedExportRoot,
        '--layout', 'mirror'
    )
    $shouldExportHeads = $AllHeads -or ($normalizedHeadIds.Count -gt 0) -or (-not $AllCountermarks -and $normalizedCountermarkIds.Count -eq 0)
    $shouldExportCountermarks = $AllCountermarks -or ($normalizedCountermarkIds.Count -gt 0)

    if (-not $shouldExportHeads) {
        $imageArgs += '--skip-heads'
    }
    if (-not $shouldExportCountermarks) {
        $imageArgs += '--skip-countermarks'
    }
    if ($normalizedHeadIds.Count -gt 0) {
        $imageArgs += '--head-ids'
        foreach ($headId in $normalizedHeadIds) {
            $imageArgs += [string]$headId
        }
    }
    if ($normalizedCountermarkIds.Count -gt 0) {
        $imageArgs += '--countermark-ids'
        foreach ($countermarkId in $normalizedCountermarkIds) {
            $imageArgs += [string]$countermarkId
        }
    }

    $imageSummary = Invoke-PythonJsonScript `
        -ResolvedPythonPath $resolvedPythonPath `
        -ScriptPath $imagesScriptPath `
        -Arguments $imageArgs
}

$configTargetPath = Join-Path $resolvedRepoOutputRoot 'config'
$newseerTargetPath = Join-Path $resolvedRepoOutputRoot 'newseer'

if (-not $SkipConfig) {
    Remove-DirectoryIfExists -DirectoryPath $configTargetPath -AllowedParent $resolvedRepoOutputRoot
}
if ($needsImageExport) {
    Remove-DirectoryIfExists -DirectoryPath $newseerTargetPath -AllowedParent $resolvedRepoOutputRoot
}

$exportedConfigPath = Join-Path $resolvedExportRoot 'config'
$exportedNewseerPath = Join-Path $resolvedExportRoot 'newseer'

if (-not $SkipConfig) {
    Copy-DirectoryToRoot -SourcePath $exportedConfigPath -DestinationRoot $resolvedRepoOutputRoot
}
if ($needsImageExport) {
    Copy-DirectoryToRoot -SourcePath $exportedNewseerPath -DestinationRoot $resolvedRepoOutputRoot
}

$combinedSummary = [ordered]@{
    Success = $true
    GeneratedAtUtc = [DateTime]::UtcNow.ToString('o')
    RepoRoot = $repoRoot
    RepoOutputRoot = $resolvedRepoOutputRoot
    MirrorRoot = $resolvedMirrorRoot
    ExportRoot = $resolvedExportRoot
    PythonPath = $resolvedPythonPath
    PreservedExistingRepoPetSkin = $preservedExistingPetSkin
    ConfigVersion = if ($configSummary) { $configSummary.ConfigVersion } else { '' }
    DefaultManifestVersion = if ($configSummary) { $configSummary.DefaultManifestVersion } else { $imageSummary.DefaultPackageVersion }
    MonsterCount = if ($configSummary) { $configSummary.MonsterCount } else { 0 }
    PetSkinCount = if ($configSummary) { $configSummary.PetSkinCount } else { 0 }
    PetSkinSource = if ($configSummary) { $configSummary.PetSkinSource } else { '' }
    Config = $configSummary
    Images = $imageSummary
    Sync = $syncSummary
    OutputPaths = [ordered]@{
        Config = if (-not $SkipConfig) { $configTargetPath } else { '' }
        Newseer = if ($needsImageExport) { $newseerTargetPath } else { '' }
    }
}

$combinedSummaryPath = Join-Path $resolvedExportRoot 'seer-github-export-summary.json'
[System.IO.File]::WriteAllText(
    $combinedSummaryPath,
    ($combinedSummary | ConvertTo-Json -Depth 100),
    [System.Text.Encoding]::UTF8)

Write-Output ($combinedSummary | ConvertTo-Json -Depth 100)
