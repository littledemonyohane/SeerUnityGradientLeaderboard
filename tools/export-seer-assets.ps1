param(
    [string]$InstallRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputDir,
    [ValidateSet('export', 'probe')]
    [string]$Mode = 'export',
    [string]$UnityPath,
    [string]$BatchProjectCopyRoot,
    [int]$Limit = 0,
    [switch]$SkipMonsters,
    [switch]$SkipHeads,
    [switch]$SkipCountermarks,
    [switch]$NoWarmupRetry
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-FullPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $PathValue))
}

function Resolve-UnityEditorPath {
    param(
        [string]$RequestedPath,
        [string]$RepositoryRoot
    )

    if ($RequestedPath) {
        $resolvedRequestedPath = Resolve-FullPath -PathValue $RequestedPath
        if (-not (Test-Path -LiteralPath $resolvedRequestedPath)) {
            throw "Unity.exe was not found at $resolvedRequestedPath"
        }

        return $resolvedRequestedPath
    }

    $projectVersionFile = Join-Path $RepositoryRoot 'ProjectSettings\ProjectVersion.txt'
    $projectVersion = ''
    if (Test-Path -LiteralPath $projectVersionFile) {
        $projectVersionLine = Get-Content -LiteralPath $projectVersionFile |
            Select-String -Pattern '^m_EditorVersion:\s*(.+)$' |
            Select-Object -First 1
        if ($projectVersionLine) {
            $projectVersion = $projectVersionLine.Matches[0].Groups[1].Value.Trim()
        }
    }

    $candidates = @()
    if ($env:UNITY_EDITOR_PATH) {
        $candidates += $env:UNITY_EDITOR_PATH
    }

    if ($projectVersion) {
        $candidates += @(
            "E:\UnityEditor\$projectVersion\Editor\Unity.exe",
            "C:\Program Files\Unity\Hub\Editor\$projectVersion\Editor\Unity.exe",
            "D:\UnityEditor\$projectVersion\Editor\Unity.exe",
            "E:\Program Files\Unity\Hub\Editor\$projectVersion\Editor\Unity.exe"
        )
    }

    $unityCommand = Get-Command 'Unity.exe' -ErrorAction SilentlyContinue
    if ($unityCommand) {
        $candidates += $unityCommand.Source
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    throw "Unable to locate Unity.exe automatically. Pass -UnityPath explicitly."
}

function Resolve-PythonPath {
    $pythonCommand = Get-Command 'python' -ErrorAction SilentlyContinue
    if ($pythonCommand) {
        return $pythonCommand.Source
    }

    $pyLauncher = Get-Command 'py' -ErrorAction SilentlyContinue
    if ($pyLauncher) {
        return $pyLauncher.Source
    }

    throw "Unable to locate Python. Install Python 3 and the UnityPy package."
}

function Test-UnityPyAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedPythonPath
    )

    & $ResolvedPythonPath -c 'import UnityPy'
    return ($LASTEXITCODE -eq 0)
}

function Test-UnityProjectLocked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    return Test-Path -LiteralPath (Join-Path $ProjectRoot 'Temp\UnityLockfile')
}

function Test-UnityProjectInUse {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $unityProcesses = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue
    foreach ($process in $unityProcesses) {
        if ($process.CommandLine -and $process.CommandLine.IndexOf($ProjectRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Reset-BatchProjectLockArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    foreach ($lockPath in @(
        (Join-Path $ProjectRoot 'Temp\UnityLockfile'),
        (Join-Path $ProjectRoot 'Temp\workerlic')
    )) {
        if (Test-Path -LiteralPath $lockPath) {
            Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Sync-BatchProjectCopy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,
        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    [System.IO.Directory]::CreateDirectory($DestinationRoot) | Out-Null

    $excludeDirectories = @(
        (Join-Path $SourceRoot 'Library'),
        (Join-Path $SourceRoot 'Temp'),
        (Join-Path $SourceRoot 'obj'),
        (Join-Path $SourceRoot '.git'),
        (Join-Path $SourceRoot '.vs'),
        (Join-Path $SourceRoot '.claude'),
        (Join-Path $SourceRoot 'Logs'),
        (Join-Path $SourceRoot 'MemoryCaptures'),
        (Join-Path $SourceRoot 'UserSettings'),
        (Join-Path $SourceRoot 'ExportedSeerMirror'),
        (Join-Path $SourceRoot 'ExportedSeerMirror-smoke')
    ) | Where-Object { Test-Path -LiteralPath $_ }

    $arguments = @(
        $SourceRoot,
        $DestinationRoot,
        '/MIR',
        '/FFT',
        '/R:2',
        '/W:1',
        '/NFL',
        '/NDL',
        '/NJH',
        '/NJS',
        '/NP'
    )

    if ($excludeDirectories.Count -gt 0) {
        $arguments += '/XD'
        $arguments += $excludeDirectories
    }

    & robocopy @arguments | Out-Null
    $exitCode = $LASTEXITCODE
    if ($exitCode -gt 7) {
        throw "Failed to prepare the batch project copy. robocopy exit code: $exitCode"
    }
}

function Resolve-BatchProjectPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [string]$RequestedCopyRoot
    )

    if (-not (Test-UnityProjectLocked -ProjectRoot $RepositoryRoot)) {
        return $RepositoryRoot
    }

    $copyRoot = if ($RequestedCopyRoot) {
        Resolve-FullPath -PathValue $RequestedCopyRoot
    }
    else {
        Join-Path $env:TEMP 'SeerUnityGradientLeaderboard-batch'
    }

    Write-Host "Unity project is currently open. Syncing a batch-safe project copy to $copyRoot"
    Sync-BatchProjectCopy -SourceRoot $RepositoryRoot -DestinationRoot $copyRoot

    if (Test-UnityProjectInUse -ProjectRoot $copyRoot) {
        throw "The batch project copy is already being used by another Unity process: $copyRoot"
    }

    Reset-BatchProjectLockArtifacts -ProjectRoot $copyRoot
    return $copyRoot
}

function Get-EffectiveLimit {
    param(
        [string]$SelectedMode,
        [int]$RequestedLimit
    )

    if ($RequestedLimit -gt 0) {
        return $RequestedLimit
    }

    if ($SelectedMode -eq 'probe') {
        return 3
    }

    return 0
}

function Read-ExportSummary {
    param(
        [string]$SummaryPath,
        [datetime]$StartedAt
    )

    if (-not (Test-Path -LiteralPath $SummaryPath)) {
        return $null
    }

    $summaryItem = Get-Item -LiteralPath $SummaryPath
    if ($summaryItem.LastWriteTime -lt $StartedAt.AddSeconds(-1)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Invoke-UnityExportAttempt {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedUnityPath,
        [Parameter(Mandatory = $true)]
        [string]$UnityProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$ResolvedOutputDir,
        [Parameter(Mandatory = $true)]
        [int]$EffectiveLimit,
        [Parameter(Mandatory = $true)]
        [int]$AttemptNumber,
        [Parameter(Mandatory = $true)]
        [bool]$UseExternalHeadExporter,
        [Parameter(Mandatory = $true)]
        [bool]$UseExternalCountermarkExporter
    )

    $logPath = Join-Path $ResolvedOutputDir ("unity-export-attempt-{0}.log" -f $AttemptNumber)
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $UnityProjectPath,
        '-executeMethod', 'SeerAssetDiagnosticsEditor.BatchExportMirrorLayoutFromCommandLine',
        '-logFile', $logPath,
        '-seerOutput', $ResolvedOutputDir
    )

    if ($InstallRoot) { $arguments += @('-seerInstallRoot', $InstallRoot) }
    if ($EffectiveLimit -gt 0) { $arguments += @('-seerLimit', $EffectiveLimit) }
    if ($SkipMonsters) { $arguments += '-seerSkipMonsters' }
    if ($SkipHeads -or $UseExternalHeadExporter) { $arguments += '-seerSkipHeads' }
    if ($SkipCountermarks -or $UseExternalCountermarkExporter) { $arguments += '-seerSkipCountermarks' }

    $startTime = Get-Date
    $process = Start-Process -FilePath $ResolvedUnityPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    $exitCode = $process.ExitCode

    [pscustomobject]@{
        ExitCode  = $exitCode
        LogPath   = $logPath
        StartedAt = $startTime
    }
}

function Invoke-UnityPyImageExport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedPythonPath,
        [Parameter(Mandatory = $true)]
        [string]$ResolvedSourceRoot,
        [Parameter(Mandatory = $true)]
        [string]$ResolvedOutputDir,
        [Parameter(Mandatory = $true)]
        [int]$EffectiveLimit,
        [Parameter(Mandatory = $true)]
        [bool]$SkipHeadsFlag,
        [Parameter(Mandatory = $true)]
        [bool]$SkipCountermarksFlag
    )

    $scriptPath = Join-Path $repoRoot 'tools\export-seer-images.py'
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "UnityPy image export script not found: $scriptPath"
    }

    $output = & $ResolvedPythonPath $scriptPath `
        --source-root $ResolvedSourceRoot `
        --output-dir $ResolvedOutputDir `
        @($(if ($EffectiveLimit -gt 0) { @('--limit', $EffectiveLimit) } else { @() })) `
        @($(if ($SkipHeadsFlag) { @('--skip-heads') } else { @() })) `
        @($(if ($SkipCountermarksFlag) { @('--skip-countermarks') } else { @() })) 2>&1

    $exitCode = $LASTEXITCODE
    $rawOutput = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($rawOutput)) {
        throw "UnityPy image export did not return JSON output."
    }

    try {
        $result = $rawOutput | ConvertFrom-Json
    }
    catch {
        throw "UnityPy image export returned non-JSON output:`n$rawOutput"
    }

    if ($exitCode -ne 0 -or -not $result.Success) {
        $errorText = if ($result.Error) { $result.Error } else { "exit code $exitCode" }
        throw "UnityPy image export failed: $errorText"
    }

    return $result
}

function Set-OrAddSummaryField {
    param(
        [Parameter(Mandatory = $true)]
        [object]$SummaryObject,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        $Value
    )

    $existingProperty = $SummaryObject.PSObject.Properties[$Name]
    if ($existingProperty) {
        $existingProperty.Value = $Value
    }
    else {
        $SummaryObject | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function Merge-ImageExportSummary {
    param(
        [Parameter(Mandatory = $true)]
        [object]$SummaryObject,
        [Parameter(Mandatory = $true)]
        [object]$ImageSummary,
        [Parameter(Mandatory = $true)]
        [bool]$SkipHeadsFlag,
        [Parameter(Mandatory = $true)]
        [bool]$SkipCountermarksFlag,
        [Parameter(Mandatory = $true)]
        [string]$SummaryPath
    )

    if (-not $SkipHeadsFlag -and $ImageSummary.Heads) {
        $SummaryObject.IndexedHeadCount = [int]$ImageSummary.Heads.Indexed
        $SummaryObject.ExportedHeadCount = [int]$ImageSummary.Heads.Exported
        $SummaryObject.FailedHeadCount = [int]$ImageSummary.Heads.Failed
        Set-OrAddSummaryField -SummaryObject $SummaryObject -Name 'HeadExportSource' -Value ([string]$ImageSummary.Heads.Source)
        Set-OrAddSummaryField -SummaryObject $SummaryObject -Name 'HeadOutputDirectory' -Value ([string]$ImageSummary.Heads.OutputDirectory)
    }

    if (-not $SkipCountermarksFlag -and $ImageSummary.Countermarks) {
        $SummaryObject.IndexedCountermarkCount = [int]$ImageSummary.Countermarks.Indexed
        $SummaryObject.ExportedCountermarkCount = [int]$ImageSummary.Countermarks.Exported
        $SummaryObject.FailedCountermarkCount = [int]$ImageSummary.Countermarks.Failed
        Set-OrAddSummaryField -SummaryObject $SummaryObject -Name 'CountermarkExportSource' -Value ([string]$ImageSummary.Countermarks.Source)
        Set-OrAddSummaryField -SummaryObject $SummaryObject -Name 'CountermarkOutputDirectory' -Value ([string]$ImageSummary.Countermarks.OutputDirectory)
    }

    $summaryJson = $SummaryObject | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($SummaryPath, $summaryJson, [System.Text.Encoding]::UTF8)
}

$resolvedOutputDir = Resolve-FullPath -PathValue $OutputDir
$resolvedUnityPath = Resolve-UnityEditorPath -RequestedPath $UnityPath -RepositoryRoot $repoRoot
$resolvedPythonPath = Resolve-PythonPath
$unityProjectPath = Resolve-BatchProjectPath -RepositoryRoot $repoRoot -RequestedCopyRoot $BatchProjectCopyRoot
$effectiveLimit = Get-EffectiveLimit -SelectedMode $Mode -RequestedLimit $Limit
$summaryPath = Join-Path $resolvedOutputDir 'seer-export-summary.json'
$useExternalHeadExporter = -not $SkipHeads
$useExternalCountermarkExporter = -not $SkipCountermarks

if (($useExternalHeadExporter -or $useExternalCountermarkExporter) -and -not (Test-UnityPyAvailable -ResolvedPythonPath $resolvedPythonPath)) {
    throw "Python was found at $resolvedPythonPath but the UnityPy module is unavailable. Install it with: pip install UnityPy pillow"
}

[System.IO.Directory]::CreateDirectory($resolvedOutputDir) | Out-Null

$maxAttempts = 1
if (-not $NoWarmupRetry) {
    $maxAttempts = 2
}

$lastAttempt = $null
$summary = $null

for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    $lastAttempt = Invoke-UnityExportAttempt `
        -ResolvedUnityPath $resolvedUnityPath `
        -UnityProjectPath $unityProjectPath `
        -ResolvedOutputDir $resolvedOutputDir `
        -EffectiveLimit $effectiveLimit `
        -AttemptNumber $attempt `
        -UseExternalHeadExporter $useExternalHeadExporter `
        -UseExternalCountermarkExporter $useExternalCountermarkExporter

    $summary = Read-ExportSummary -SummaryPath $summaryPath -StartedAt $lastAttempt.StartedAt
    if ($summary -and $summary.Success) {
        if ($useExternalHeadExporter -or $useExternalCountermarkExporter) {
            $resolvedSourceRoot = if ($summary.InstallRoot) {
                Resolve-FullPath -PathValue ([string]$summary.InstallRoot)
            }
            elseif ($InstallRoot) {
                Resolve-FullPath -PathValue $InstallRoot
            }
            else {
                throw 'Unity export summary did not report the source root.'
            }

            $imageSummary = Invoke-UnityPyImageExport `
                -ResolvedPythonPath $resolvedPythonPath `
                -ResolvedSourceRoot $resolvedSourceRoot `
                -ResolvedOutputDir $resolvedOutputDir `
                -EffectiveLimit $effectiveLimit `
                -SkipHeadsFlag $SkipHeads `
                -SkipCountermarksFlag $SkipCountermarks

            Merge-ImageExportSummary `
                -SummaryObject $summary `
                -ImageSummary $imageSummary `
                -SkipHeadsFlag $SkipHeads `
                -SkipCountermarksFlag $SkipCountermarks `
                -SummaryPath $summaryPath
        }

        Write-Host "Export succeeded."
        Write-Host "Unity:    $resolvedUnityPath"
        Write-Host "Project:  $unityProjectPath"
        Write-Host "Output:   $resolvedOutputDir"
        Write-Host "Summary:  $summaryPath"
        Write-Host "Log:      $($lastAttempt.LogPath)"
        $summary | ConvertTo-Json -Depth 8
        exit 0
    }

    if ($attempt -lt $maxAttempts) {
        Write-Host "First Unity batch attempt did not complete successfully. Retrying once for import/recompile warmup."
    }
}

if ($summary) {
    $summary | ConvertTo-Json -Depth 8
}

if ($lastAttempt) {
    throw "Unity batch export failed. See log: $($lastAttempt.LogPath)"
}

throw 'Unity batch export failed before Unity could be started.'
