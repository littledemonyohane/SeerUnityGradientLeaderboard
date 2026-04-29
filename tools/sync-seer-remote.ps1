param(
    [string]$InstallRoot,
    [Parameter(Mandatory = $true)]
    [string]$MirrorRoot,
    [string]$RemoteBaseUrl,
    [string[]]$HeadIds,
    [string[]]$CountermarkIds,
    [switch]$AllHeads,
    [switch]$AllCountermarks,
    [switch]$SkipConfig,
    [switch]$NoSummaryOutput
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

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

function Resolve-InstallRoot {
    param([string]$RequestedPath)

    $candidates = @()
    if ($RequestedPath) {
        $candidates += $RequestedPath
    }

    $candidates += @(
        'D:\SeerLauncher\games\NewSeer',
        'C:\SeerLauncher\games\NewSeer',
        'E:\SeerLauncher\games\NewSeer'
    )

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not $candidate) {
            continue
        }

        $fullPath = Resolve-FullPath -PathValue $candidate
        if (Test-Path -LiteralPath (Join-Path $fullPath 'Seer_Data\yoo')) {
            return $fullPath
        }
    }

    return ''
}

function Get-AppConfigCdn {
    param([string]$ResolvedInstallRoot)

    if (-not $ResolvedInstallRoot) {
        return ''
    }

    $resourcesPath = Join-Path $ResolvedInstallRoot 'Seer_Data\resources.assets'
    if (-not (Test-Path -LiteralPath $resourcesPath)) {
        return ''
    }

    $bytes = [System.IO.File]::ReadAllBytes($resourcesPath)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    $match = [regex]::Match($text, '"cdn"\s*:\s*"([^"]+)"')
    if ($match.Success) {
        return $match.Groups[1].Value.Trim()
    }

    return ''
}

function Resolve-RemoteBaseUrl {
    param(
        [string]$RequestedBaseUrl,
        [string]$ResolvedInstallRoot
    )

    if ($RequestedBaseUrl) {
        return $RequestedBaseUrl.TrimEnd('/')
    }

    $cdn = Get-AppConfigCdn -ResolvedInstallRoot $ResolvedInstallRoot
    if ($cdn) {
        return ($cdn.TrimEnd('/') + '/StandaloneWindows64')
    }

    return 'https://newseer.61.com/Assets/StandaloneWindows64'
}

function Invoke-TextRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    $bytes = Invoke-BytesRequest -Url $Url
    return [System.Text.Encoding]::UTF8.GetString($bytes).Trim([char]0).Trim()
}

function Invoke-BytesRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    $client = New-Object System.Net.WebClient
    try {
        return $client.DownloadData($Url)
    }
    finally {
        $client.Dispose()
    }
}

function Get-RemotePackageState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,
        [Parameter(Mandatory = $true)]
        [string]$Package
    )

    $normalizedBaseUrl = $BaseUrl.TrimEnd('/')
    $packageBaseUrl = '{0}/{1}' -f $normalizedBaseUrl, $Package
    $versionUrl = '{0}/PackageManifest_{1}.version' -f $packageBaseUrl, $Package
    $version = Invoke-TextRequest -Url $versionUrl
    $hashUrl = '{0}/PackageManifest_{1}_{2}.hash' -f $packageBaseUrl, $Package, $version
    $manifestUrl = '{0}/PackageManifest_{1}_{2}.bytes' -f $packageBaseUrl, $Package, $version
    $hash = Invoke-TextRequest -Url $hashUrl
    $manifestBytes = Invoke-BytesRequest -Url $manifestUrl

    return [pscustomobject]@{
        Package     = $Package
        BaseUrl     = $packageBaseUrl
        Version     = $version
        Hash        = $hash
        VersionUrl  = $versionUrl
        HashUrl     = $hashUrl
        ManifestUrl = $manifestUrl
        ManifestBytes = $manifestBytes
    }
}

function Save-PackageStateToMirror {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedMirrorRoot,
        [Parameter(Mandatory = $true)]
        [object]$PackageState
    )

    $manifestDir = Join-Path $ResolvedMirrorRoot ("Seer_Data\yoo\{0}\ManifestFiles" -f $PackageState.Package)
    [System.IO.Directory]::CreateDirectory($manifestDir) | Out-Null

    $versionPath = Join-Path $manifestDir ("PackageManifest_{0}.version" -f $PackageState.Package)
    $hashPath = Join-Path $manifestDir ("PackageManifest_{0}_{1}.hash" -f $PackageState.Package, $PackageState.Version)
    $bytesPath = Join-Path $manifestDir ("PackageManifest_{0}_{1}.bytes" -f $PackageState.Package, $PackageState.Version)

    [System.IO.File]::WriteAllText($versionPath, [string]$PackageState.Version, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($hashPath, [string]$PackageState.Hash, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllBytes($bytesPath, $PackageState.ManifestBytes)
}

function Get-OrderedBundleHashes {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$ManifestBytes
    )

    $manifestText = [System.Text.Encoding]::ASCII.GetString($ManifestBytes)
    $ordered = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($match in [regex]::Matches($manifestText, '\b[0-9a-f]{32}\b')) {
        if ($seen.Add($match.Value)) {
            [void]$ordered.Add($match.Value)
        }
    }

    return [pscustomobject]@{
        Text = $manifestText
        Hashes = $ordered
    }
}

function Get-BundleHashForAssetPath {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$ManifestBytes,
        [Parameter(Mandatory = $true)]
        [string]$ManifestText,
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[string]]$OrderedHashes,
        [Parameter(Mandatory = $true)]
        [string]$AssetPath
    )

    $assetOffset = $ManifestText.IndexOf($AssetPath, [System.StringComparison]::Ordinal)
    if ($assetOffset -lt 0) {
        return ''
    }

    $bundleIndexOffset = $assetOffset + $AssetPath.Length
    if ($bundleIndexOffset + 1 -ge $ManifestBytes.Length) {
        return ''
    }

    $bundleIndex = $ManifestBytes[$bundleIndexOffset] + ($ManifestBytes[$bundleIndexOffset + 1] * 256)
    if ($bundleIndex -lt 0 -or $bundleIndex -ge $OrderedHashes.Count) {
        return ''
    }

    return $OrderedHashes[$bundleIndex]
}

function Get-AssetPathsByPrefix {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestText,
        [Parameter(Mandatory = $true)]
        [string]$Prefix,
        [Parameter(Mandatory = $true)]
        [string]$Suffix
    )

    $paths = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $searchOffset = 0

    while ($searchOffset -lt $ManifestText.Length) {
        $start = $ManifestText.IndexOf($Prefix, $searchOffset, [System.StringComparison]::Ordinal)
        if ($start -lt 0) {
            break
        }

        $end = $ManifestText.IndexOf($Suffix, $start, [System.StringComparison]::Ordinal)
        if ($end -lt 0) {
            break
        }

        $end += $Suffix.Length
        $assetPath = $ManifestText.Substring($start, $end - $start)
        if ($seen.Add($assetPath)) {
            [void]$paths.Add($assetPath)
        }

        $searchOffset = $end
    }

    return $paths
}

function Get-AssetBundleMapByPrefix {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$ManifestBytes,
        [Parameter(Mandatory = $true)]
        [string]$ManifestText,
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[string]]$OrderedHashes,
        [Parameter(Mandatory = $true)]
        [string]$Prefix,
        [Parameter(Mandatory = $true)]
        [string]$Suffix
    )

    $assetToHash = @{}
    $searchOffset = 0

    while ($searchOffset -lt $ManifestText.Length) {
        $start = $ManifestText.IndexOf($Prefix, $searchOffset, [System.StringComparison]::Ordinal)
        if ($start -lt 0) {
            break
        }

        $end = $ManifestText.IndexOf($Suffix, $start, [System.StringComparison]::Ordinal)
        if ($end -lt 0) {
            break
        }

        $end += $Suffix.Length
        if ($end + 1 -ge $ManifestBytes.Length) {
            break
        }

        $assetPath = $ManifestText.Substring($start, $end - $start)
        $bundleIndex = $ManifestBytes[$end] + ($ManifestBytes[$end + 1] * 256)
        if ($bundleIndex -ge 0 -and $bundleIndex -lt $OrderedHashes.Count) {
            $assetToHash[$assetPath] = $OrderedHashes[$bundleIndex]
        }

        $searchOffset = $end
    }

    return $assetToHash
}

function Add-RequestedBundle {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Downloads,
        [Parameter(Mandatory = $true)]
        [string]$Package,
        [Parameter(Mandatory = $true)]
        [string]$BundleHash,
        [Parameter(Mandatory = $true)]
        [string]$AssetPath
    )

    if (-not $BundleHash) {
        return
    }

    $key = '{0}:{1}' -f $Package, $BundleHash
    if (-not $Downloads.ContainsKey($key)) {
        $Downloads[$key] = [pscustomobject]@{
            Package = $Package
            BundleHash = $BundleHash
            AssetPaths = New-Object System.Collections.Generic.List[string]
        }
    }

    [void]$Downloads[$key].AssetPaths.Add($AssetPath)
}

function Save-BundleToMirror {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedMirrorRoot,
        [Parameter(Mandatory = $true)]
        [string]$Package,
        [Parameter(Mandatory = $true)]
        [string]$BundleHash,
        [Parameter(Mandatory = $true)]
        [byte[]]$BundleBytes
    )

    $bundleDir = Join-Path $ResolvedMirrorRoot ("Seer_Data\yoo\{0}\CacheBundleFiles\{1}\{2}" -f $Package, $BundleHash.Substring(0, 2), $BundleHash)
    [System.IO.Directory]::CreateDirectory($bundleDir) | Out-Null
    $bundlePath = Join-Path $bundleDir '__data'
    [System.IO.File]::WriteAllBytes($bundlePath, $BundleBytes)
    return $bundlePath
}

function Get-MirrorBundlePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedMirrorRoot,
        [Parameter(Mandatory = $true)]
        [string]$Package,
        [Parameter(Mandatory = $true)]
        [string]$BundleHash
    )

    return (Join-Path $ResolvedMirrorRoot ("Seer_Data\yoo\{0}\CacheBundleFiles\{1}\{2}\__data" -f $Package, $BundleHash.Substring(0, 2), $BundleHash))
}

function Get-InstallBundlePath {
    param(
        [string]$ResolvedInstallRoot,
        [Parameter(Mandatory = $true)]
        [string]$Package,
        [Parameter(Mandatory = $true)]
        [string]$BundleHash
    )

    if (-not $ResolvedInstallRoot) {
        return ''
    }

    $candidate = Join-Path $ResolvedInstallRoot ("Seer_Data\yoo\{0}\CacheBundleFiles\{1}\{2}\__data" -f $Package, $BundleHash.Substring(0, 2), $BundleHash)
    if (Test-Path -LiteralPath $candidate) {
        return $candidate
    }

    return ''
}

function Copy-BundleToMirror {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [string]$ResolvedMirrorRoot,
        [Parameter(Mandatory = $true)]
        [string]$Package,
        [Parameter(Mandatory = $true)]
        [string]$BundleHash
    )

    $destinationPath = Get-MirrorBundlePath -ResolvedMirrorRoot $ResolvedMirrorRoot -Package $Package -BundleHash $BundleHash
    $destinationDir = Split-Path -Parent $destinationPath
    [System.IO.Directory]::CreateDirectory($destinationDir) | Out-Null
    [System.IO.File]::Copy($SourcePath, $destinationPath, $true)
    return $destinationPath
}

$resolvedInstallRoot = Resolve-InstallRoot -RequestedPath $InstallRoot
$resolvedMirrorRoot = Resolve-FullPath -PathValue $MirrorRoot
$resolvedRemoteBaseUrl = Resolve-RemoteBaseUrl -RequestedBaseUrl $RemoteBaseUrl -ResolvedInstallRoot $resolvedInstallRoot
$normalizedHeadIds = @(Normalize-AssetIdList -Values $HeadIds)
$normalizedCountermarkIds = @(Normalize-AssetIdList -Values $CountermarkIds)

[System.IO.Directory]::CreateDirectory($resolvedMirrorRoot) | Out-Null

$summary = [ordered]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString('o')
    InstallRoot = $resolvedInstallRoot
    MirrorRoot = $resolvedMirrorRoot
    RemoteBaseUrl = $resolvedRemoteBaseUrl
    Packages = @()
    DownloadedBundles = @()
}

$downloads = @{}

if (-not $SkipConfig) {
    $configState = Get-RemotePackageState -BaseUrl $resolvedRemoteBaseUrl -Package 'ConfigPackage'
    Save-PackageStateToMirror -ResolvedMirrorRoot $resolvedMirrorRoot -PackageState $configState

    $configIndex = Get-OrderedBundleHashes -ManifestBytes $configState.ManifestBytes
    foreach ($assetPath in @(
        'Assets/Game/Configs/bytes/monsters.bytes',
        'Assets/Game/Configs/bytes/pet_skin.bytes'
    )) {
        $bundleHash = Get-BundleHashForAssetPath `
            -ManifestBytes $configState.ManifestBytes `
            -ManifestText $configIndex.Text `
            -OrderedHashes $configIndex.Hashes `
            -AssetPath $assetPath
        Add-RequestedBundle -Downloads $downloads -Package 'ConfigPackage' -BundleHash $bundleHash -AssetPath $assetPath
    }

    $summary.Packages += [ordered]@{
        Package = 'ConfigPackage'
        Version = $configState.Version
        Hash = $configState.Hash
    }
}

$needsDefaultPackage = $AllHeads -or $AllCountermarks -or ($normalizedHeadIds.Count -gt 0) -or ($normalizedCountermarkIds.Count -gt 0)
if ($needsDefaultPackage) {
    $defaultState = Get-RemotePackageState -BaseUrl $resolvedRemoteBaseUrl -Package 'DefaultPackage'
    Save-PackageStateToMirror -ResolvedMirrorRoot $resolvedMirrorRoot -PackageState $defaultState

    $defaultIndex = Get-OrderedBundleHashes -ManifestBytes $defaultState.ManifestBytes
    $headAssetToHash = @{}
    $countermarkAssetToHash = @{}

    if ($AllHeads -or $normalizedHeadIds.Count -gt 0) {
        $headAssetToHash = Get-AssetBundleMapByPrefix `
            -ManifestBytes $defaultState.ManifestBytes `
            -ManifestText $defaultIndex.Text `
            -OrderedHashes $defaultIndex.Hashes `
            -Prefix 'Assets/Art/Ui/assets/pet/head/' `
            -Suffix '.png'
    }

    if ($AllCountermarks -or $normalizedCountermarkIds.Count -gt 0) {
        $countermarkAssetToHash = Get-AssetBundleMapByPrefix `
            -ManifestBytes $defaultState.ManifestBytes `
            -ManifestText $defaultIndex.Text `
            -OrderedHashes $defaultIndex.Hashes `
            -Prefix 'Assets/Art/Ui/assets/countermark/icon/' `
            -Suffix '.png'
    }

    foreach ($headId in $normalizedHeadIds) {
        $assetPath = 'Assets/Art/Ui/assets/pet/head/{0}.png' -f $headId
        $bundleHash = if ($headAssetToHash.ContainsKey($assetPath)) { $headAssetToHash[$assetPath] } else { '' }
        Add-RequestedBundle -Downloads $downloads -Package 'DefaultPackage' -BundleHash $bundleHash -AssetPath $assetPath
    }

    foreach ($countermarkId in $normalizedCountermarkIds) {
        $assetPath = 'Assets/Art/Ui/assets/countermark/icon/{0}.png' -f $countermarkId
        $bundleHash = if ($countermarkAssetToHash.ContainsKey($assetPath)) { $countermarkAssetToHash[$assetPath] } else { '' }
        Add-RequestedBundle -Downloads $downloads -Package 'DefaultPackage' -BundleHash $bundleHash -AssetPath $assetPath
    }

    if ($AllHeads) {
        foreach ($assetPath in ($headAssetToHash.Keys | Sort-Object)) {
            Add-RequestedBundle -Downloads $downloads -Package 'DefaultPackage' -BundleHash $headAssetToHash[$assetPath] -AssetPath $assetPath
        }
    }

    if ($AllCountermarks) {
        foreach ($assetPath in ($countermarkAssetToHash.Keys | Sort-Object)) {
            Add-RequestedBundle -Downloads $downloads -Package 'DefaultPackage' -BundleHash $countermarkAssetToHash[$assetPath] -AssetPath $assetPath
        }
    }

    $summary.Packages += [ordered]@{
        Package = 'DefaultPackage'
        Version = $defaultState.Version
        Hash = $defaultState.Hash
    }
}

$downloadList = @($downloads.Values | Sort-Object Package, BundleHash)
$summary.RequestedBundleCount = $downloadList.Count
for ($downloadIndex = 0; $downloadIndex -lt $downloadList.Count; $downloadIndex++) {
    $download = $downloadList[$downloadIndex]
    $bundleUrl = '{0}/{1}/{2}' -f $resolvedRemoteBaseUrl.TrimEnd('/'), $download.Package, $download.BundleHash
    $bundlePath = Get-MirrorBundlePath `
        -ResolvedMirrorRoot $resolvedMirrorRoot `
        -Package $download.Package `
        -BundleHash $download.BundleHash
    $bundleLength = 0
    $bundleSource = ''

    Write-Host ("[{0}/{1}] Syncing {2}/{3}" -f ($downloadIndex + 1), $downloadList.Count, $download.Package, $download.BundleHash)

    if (Test-Path -LiteralPath $bundlePath) {
        $bundleItem = Get-Item -LiteralPath $bundlePath
        if ($bundleItem.Length -gt 0) {
            $bundleLength = $bundleItem.Length
            $bundleSource = 'mirror-cache'
        }
    }

    if (-not $bundleSource) {
        $installBundlePath = Get-InstallBundlePath `
            -ResolvedInstallRoot $resolvedInstallRoot `
            -Package $download.Package `
            -BundleHash $download.BundleHash

        if ($installBundlePath) {
            $bundlePath = Copy-BundleToMirror `
                -SourcePath $installBundlePath `
                -ResolvedMirrorRoot $resolvedMirrorRoot `
                -Package $download.Package `
                -BundleHash $download.BundleHash
            $bundleLength = (Get-Item -LiteralPath $bundlePath).Length
            $bundleSource = 'install-cache'
        }
    }

    if (-not $bundleSource) {
        $bundleBytes = Invoke-BytesRequest -Url $bundleUrl
        $bundlePath = Save-BundleToMirror `
            -ResolvedMirrorRoot $resolvedMirrorRoot `
            -Package $download.Package `
            -BundleHash $download.BundleHash `
            -BundleBytes $bundleBytes
        $bundleLength = $bundleBytes.Length
        $bundleSource = 'remote-download'
    }

    $summary.DownloadedBundles += [ordered]@{
        Package = $download.Package
        BundleHash = $download.BundleHash
        AssetPaths = @($download.AssetPaths)
        BundleUrl = $bundleUrl
        BundlePath = $bundlePath
        Length = $bundleLength
        Source = $bundleSource
    }
}

$summaryPath = Join-Path $resolvedMirrorRoot 'seer-remote-sync-summary.json'
$summaryJson = $summary | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($summaryPath, $summaryJson, [System.Text.Encoding]::UTF8)
if (-not $NoSummaryOutput) {
    Write-Output $summaryJson
}
