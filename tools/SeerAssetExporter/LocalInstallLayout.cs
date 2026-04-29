using System.Text.RegularExpressions;

namespace SeerAssetExporter;

internal sealed class LocalInstallLayout
{
    public static readonly IReadOnlyList<string> CandidateInstalls =
    [
        @"D:\SeerLauncher\games\NewSeer",
        @"C:\SeerLauncher\games\NewSeer",
        @"E:\SeerLauncher\games\NewSeer",
    ];

    public string InstallRoot { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string SeerDataDir => Path.Combine(InstallRoot, "Seer_Data");
    public string YooRoot => Path.Combine(SeerDataDir, "yoo");
    public string ConfigPackageDir => Path.Combine(YooRoot, "ConfigPackage");
    public string DefaultPackageDir => Path.Combine(YooRoot, "DefaultPackage");
    public string RawFileBytesDir => Path.Combine(ConfigPackageDir, "rawfile", "__data");
    public string MonstersBytesPath => Path.Combine(RawFileBytesDir, "monsters.bytes");
    public string DefaultManifestDir => Path.Combine(DefaultPackageDir, "ManifestFiles");
    public string DefaultPackageVersionPath => Path.Combine(DefaultManifestDir, "PackageManifest_DefaultPackage.version");
    public string DefaultCacheBundleDir => Path.Combine(DefaultPackageDir, "CacheBundleFiles");

    public string DefaultManifestBytesPath
    {
        get
        {
            if (!File.Exists(DefaultPackageVersionPath))
            {
                return string.Empty;
            }

            var version = File.ReadAllText(DefaultPackageVersionPath).Trim();
            if (string.IsNullOrWhiteSpace(version))
            {
                return string.Empty;
            }

            return Path.Combine(DefaultManifestDir, $"PackageManifest_DefaultPackage_{version}.bytes");
        }
    }

    public string BundleDataPath(string bundleHash)
    {
        if (string.IsNullOrWhiteSpace(bundleHash) || bundleHash.Length < 2)
        {
            return string.Empty;
        }

        return Path.Combine(DefaultCacheBundleDir, bundleHash[..2], bundleHash, "__data");
    }

    public static LocalInstallLayout Resolve(string explicitInstallRoot)
    {
        if (LooksLikeInstall(explicitInstallRoot))
        {
            return new LocalInstallLayout
            {
                InstallRoot = NormalizePath(explicitInstallRoot),
                IsAvailable = true,
            };
        }

        foreach (var candidate in CandidateInstalls)
        {
            if (LooksLikeInstall(candidate))
            {
                return new LocalInstallLayout
                {
                    InstallRoot = NormalizePath(candidate),
                    IsAvailable = true,
                };
            }
        }

        return new LocalInstallLayout
        {
            InstallRoot = NormalizePath(explicitInstallRoot),
            IsAvailable = false,
        };
    }

    public static bool LooksLikeInstall(string path)
    {
        path = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        var exePath = Path.Combine(path, "Seer.exe");
        var gameAssemblyPath = Path.Combine(path, "GameAssembly.dll");
        var configVersionPath = Path.Combine(
            path,
            "Seer_Data",
            "yoo",
            "ConfigPackage",
            "ManifestFiles",
            "PackageManifest_ConfigPackage.version");
        var defaultVersionPath = Path.Combine(
            path,
            "Seer_Data",
            "yoo",
            "DefaultPackage",
            "ManifestFiles",
            "PackageManifest_DefaultPackage.version");

        return (File.Exists(exePath) || File.Exists(gameAssemblyPath))
               && File.Exists(configVersionPath)
               && File.Exists(defaultVersionPath);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch
        {
            return Regex.Replace(path.Trim().Trim('"'), @"[\\/]+$", string.Empty);
        }
    }
}
