param(
    [string]$InstallRoot,
    [string]$RemoteBaseUrl,
    [string]$OutputPath,
    [string[]]$Packages = @('StartupPackage', 'DefaultPackage', 'ConfigPackage', 'FollowPackage', 'PetAnimPackage')
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

    try {
        $response = Invoke-WebRequest -UseBasicParsing $Url -TimeoutSec 30
        $bytes = if ($response.Content -is [byte[]]) {
            $response.Content
        }
        else {
            [System.Text.Encoding]::UTF8.GetBytes([string]$response.Content)
        }

        return [pscustomobject]@{
            Success = $true
            Url     = $Url
            Content = [System.Text.Encoding]::UTF8.GetString($bytes).Trim([char]0)
            Error   = ''
        }
    }
    catch {
        return [pscustomobject]@{
            Success = $false
            Url     = $Url
            Content = ''
            Error   = $_.Exception.Message
        }
    }
}

function Get-LocalPackageState {
    param(
        [string]$ResolvedInstallRoot,
        [string[]]$PackageNames
    )

    if (-not $ResolvedInstallRoot) {
        return @()
    }

    $results = New-Object System.Collections.Generic.List[object]
    foreach ($package in $PackageNames) {
        $versionPath = Join-Path $ResolvedInstallRoot ("Seer_Data\yoo\{0}\ManifestFiles\PackageManifest_{0}.version" -f $package)
        $manifestDirectory = Join-Path $ResolvedInstallRoot ("Seer_Data\yoo\{0}\ManifestFiles" -f $package)
        $version = ''
        $manifestPath = ''

        if (Test-Path -LiteralPath $versionPath) {
            $version = (Get-Content -LiteralPath $versionPath -Raw).Trim()
        }

        if ($version) {
            $candidateManifestPath = Join-Path $manifestDirectory ("PackageManifest_{0}_{1}.bytes" -f $package, $version)
            if (Test-Path -LiteralPath $candidateManifestPath) {
                $manifestPath = $candidateManifestPath
            }
        }

        $results.Add([pscustomobject]@{
            Package      = $package
            Version      = $version
            VersionPath  = $versionPath
            ManifestPath = $manifestPath
            Exists       = [bool]$version
        })
    }

    return $results
}

function Get-RemotePackageState {
    param(
        [string]$BaseUrl,
        [string]$Package
    )

    if (-not $BaseUrl) {
        return [pscustomobject]@{
            Package        = $Package
            RemoteBaseUrl  = ''
            Version        = ''
            VersionUrl     = ''
            HashUrl        = ''
            ManifestUrl    = ''
            VersionSuccess = $false
            HashSuccess    = $false
            ManifestSuccess = $false
            Hash           = ''
            ManifestLength = 0
            Error          = 'RemoteBaseUrl was not provided.'
        }
    }

    $normalizedBaseUrl = $BaseUrl.TrimEnd('/')
    $versionUrl = '{0}/{1}/PackageManifest_{1}.version' -f $normalizedBaseUrl, $Package
    $versionResponse = Invoke-TextRequest -Url $versionUrl
    if (-not $versionResponse.Success) {
        return [pscustomobject]@{
            Package         = $Package
            RemoteBaseUrl   = $normalizedBaseUrl
            Version         = ''
            VersionUrl      = $versionUrl
            HashUrl         = ''
            ManifestUrl     = ''
            VersionSuccess  = $false
            HashSuccess     = $false
            ManifestSuccess = $false
            Hash            = ''
            ManifestLength  = 0
            Error           = $versionResponse.Error
        }
    }

    $version = $versionResponse.Content.Trim()
    $hashUrl = '{0}/{1}/PackageManifest_{1}_{2}.hash' -f $normalizedBaseUrl, $Package, $version
    $manifestUrl = '{0}/{1}/PackageManifest_{1}_{2}.bytes' -f $normalizedBaseUrl, $Package, $version
    $hashResponse = Invoke-TextRequest -Url $hashUrl

    $manifestLength = 0
    $manifestSuccess = $false
    $manifestError = ''
    try {
        $manifestResponse = Invoke-WebRequest -UseBasicParsing $manifestUrl -TimeoutSec 30
        $manifestLength = $manifestResponse.RawContentLength
        if (-not $manifestLength -and $manifestResponse.Content) {
            $manifestLength = [System.Text.Encoding]::UTF8.GetByteCount([string]$manifestResponse.Content)
        }

        $manifestSuccess = $true
    }
    catch {
        $manifestError = $_.Exception.Message
    }

    return [pscustomobject]@{
        Package         = $Package
        RemoteBaseUrl   = $normalizedBaseUrl
        Version         = $version
        VersionUrl      = $versionUrl
        HashUrl         = $hashUrl
        ManifestUrl     = $manifestUrl
        VersionSuccess  = $true
        HashSuccess     = $hashResponse.Success
        ManifestSuccess = $manifestSuccess
        Hash            = if ($hashResponse.Success) { $hashResponse.Content.Trim() } else { '' }
        ManifestLength  = $manifestLength
        Error           = if ($manifestError) { $manifestError } else { $hashResponse.Error }
    }
}

function Get-NoticePreview {
    $response = Invoke-TextRequest -Url 'http://124.222.192.41:8001/unity_notice/'
    if (-not $response.Success) {
        return [pscustomobject]@{
            Success = $false
            Error   = $response.Error
            Items   = @()
        }
    }

    try {
        $items = $response.Content | ConvertFrom-Json
        return [pscustomobject]@{
            Success = $true
            Error   = ''
            Items   = @($items | Select-Object -First 5 | ForEach-Object {
                [pscustomobject]@{
                    id         = $_.id
                    type       = $_.type
                    main_title = $_.main_title
                    page_title = $_.page_title
                    modify_time = $_.modify_time
                    start      = $_.start
                    end        = $_.end
                }
            })
        }
    }
    catch {
        return [pscustomobject]@{
            Success = $false
            Error   = $_.Exception.Message
            Items   = @()
        }
    }
}

$resolvedInstallRoot = Resolve-InstallRoot -RequestedPath $InstallRoot
$effectiveRemoteBaseUrl = Resolve-RemoteBaseUrl -RequestedBaseUrl $RemoteBaseUrl -ResolvedInstallRoot $resolvedInstallRoot
$checkUnityIp = Invoke-TextRequest -Url 'https://seer-login-ip.61.com/check-unity-ip.txt'
$onlineGate = Invoke-TextRequest -Url 'http://seerh5login.61.com/online_gate'
$userRegionPage = Invoke-TextRequest -Url 'https://newseer.61.com/u_r.html'
$noticePreview = Get-NoticePreview
$localPackages = Get-LocalPackageState -ResolvedInstallRoot $resolvedInstallRoot -PackageNames $Packages
$remotePackages = @()

foreach ($package in $Packages) {
    $remotePackages += Get-RemotePackageState -BaseUrl $effectiveRemoteBaseUrl -Package $package
}

$result = [pscustomobject]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString('o')
    InstallRoot = $resolvedInstallRoot
    RemoteBaseUrl = $effectiveRemoteBaseUrl
    Gateways = [pscustomobject]@{
        CheckUnityIp = $checkUnityIp
        OnlineGate = $onlineGate
        UserRegionPageTitle = if ($userRegionPage.Success -and $userRegionPage.Content -match '<title>([^<]+)</title>') { $matches[1] } else { '' }
    }
    NoticePreview = $noticePreview
    LocalPackages = $localPackages
    RemotePackages = $remotePackages
}

$json = $result | ConvertTo-Json -Depth 8
if ($OutputPath) {
    $resolvedOutputPath = Resolve-FullPath -PathValue $OutputPath
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if ($outputDirectory) {
        [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
    }

    Set-Content -LiteralPath $resolvedOutputPath -Value $json -Encoding UTF8
}

$json
