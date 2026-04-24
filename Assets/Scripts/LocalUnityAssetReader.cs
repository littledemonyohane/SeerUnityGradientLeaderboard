using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Reads config and bundle data from either a real NewSeer install or a synced mirror root.
/// </summary>
public class LocalUnityAssetReader
{
    const string ConfigMonstersAssetPath = "Assets/Game/Configs/bytes/monsters.bytes";
    const string ConfigPetSkinAssetPath = "Assets/Game/Configs/bytes/pet_skin.bytes";

    static readonly Regex BundleHashRegex =
        new Regex(@"\b[0-9a-f]{32}\b", RegexOptions.Compiled);

    readonly Dictionary<string, AssetBundle> _loadedConfigBundles =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    readonly Dictionary<string, string> _configAssetPathToHash =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    readonly List<string> _orderedConfigHashes = new List<string>(1024);

    string _indexedConfigManifestPath = "";
    bool _configManifestReady;

    public string InstallRoot { get; }

    public LocalUnityAssetReader()
        : this(SeerResources.LocalInstallPath)
    {
    }

    public LocalUnityAssetReader(string installRoot)
    {
        InstallRoot = SeerResources.NormalizePath(installRoot);
    }

    public bool IsAvailable => SeerResources.LooksLikeLocalInstall(InstallRoot);

    public string ExecutablePath => Path.Combine(InstallRoot, "Seer.exe");
    public string GameAssemblyPath => Path.Combine(InstallRoot, "GameAssembly.dll");
    public string SeerDataDir => IsAvailable ? Path.Combine(InstallRoot, "Seer_Data") : "";
    public string YooRoot => IsAvailable ? Path.Combine(SeerDataDir, "yoo") : "";
    public string ConfigPackageDir => IsAvailable ? Path.Combine(YooRoot, "ConfigPackage") : "";
    public string DefaultPackageDir => IsAvailable ? Path.Combine(YooRoot, "DefaultPackage") : "";
    public string RawFileTxtDir => IsAvailable ? Path.Combine(ConfigPackageDir, "rawfile_txt") : "";
    public string RawFileBytesDir => IsAvailable ? Path.Combine(ConfigPackageDir, "rawfile", "__data") : "";
    public string ConfigManifestDir => IsAvailable ? Path.Combine(ConfigPackageDir, "ManifestFiles") : "";
    public string DefaultManifestDir => IsAvailable ? Path.Combine(DefaultPackageDir, "ManifestFiles") : "";
    public string ConfigPackageCacheBundleDir => IsAvailable ? Path.Combine(ConfigPackageDir, "CacheBundleFiles") : "";
    public string DefaultPackageCacheBundleDir => IsAvailable ? Path.Combine(DefaultPackageDir, "CacheBundleFiles") : "";
    public string ConfigPackageVersionPath => IsAvailable ? Path.Combine(ConfigManifestDir, "PackageManifest_ConfigPackage.version") : "";
    public string DefaultPackageVersionPath => IsAvailable ? Path.Combine(DefaultManifestDir, "PackageManifest_DefaultPackage.version") : "";
    public string MonstersBytesPath => IsAvailable ? Path.Combine(RawFileBytesDir, "monsters.bytes") : "";
    public string PetSkinBytesPath => IsAvailable ? Path.Combine(RawFileBytesDir, "pet_skin.bytes") : "";
    public string PetSkinTxtPath => IsAvailable ? Path.Combine(RawFileTxtDir, "pet_skin.txt") : "";
    public string GlobalMetadataPath => IsAvailable ? Path.Combine(SeerDataDir, "il2cpp_data", "Metadata", "global-metadata.dat") : "";

    public string GetConfigPackageVersion()
    {
        return ReadTextIfExists(ConfigPackageVersionPath);
    }

    public string GetDefaultPackageVersion()
    {
        return ReadTextIfExists(DefaultPackageVersionPath);
    }

    public string GetBuildInfo()
    {
        return ReadTextIfExists(Path.Combine(SeerDataDir, "StreamingAssets", "build_info"));
    }

    public string ReadRawTxt(string fileName)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var path = Path.Combine(RawFileTxtDir, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public string GetConfigPackageManifestBytesPath()
    {
        return GetPackageManifestBytesPath("ConfigPackage");
    }

    public string GetDefaultPackageManifestBytesPath()
    {
        return GetPackageManifestBytesPath("DefaultPackage");
    }

    public string GetBundleDataPath(string bundleHash)
    {
        return GetBundleDataPath("DefaultPackage", bundleHash);
    }

    public string GetBundleDataPath(string packageName, string bundleHash)
    {
        if (!IsAvailable || string.IsNullOrEmpty(bundleHash) || bundleHash.Length < 2)
        {
            return "";
        }

        var packageDir = GetPackageDirectory(packageName);
        if (string.IsNullOrEmpty(packageDir))
        {
            return "";
        }

        return Path.Combine(
            packageDir,
            "CacheBundleFiles",
            bundleHash.Substring(0, 2),
            bundleHash,
            "__data");
    }

    public bool TryReadMonstersBytes(out byte[] data)
    {
        if (TryReadFileBytes(MonstersBytesPath, out data))
        {
            return true;
        }

        return TryReadConfigAssetBytes(ConfigMonstersAssetPath, out data);
    }

    public bool TryReadPetSkinBytes(out byte[] data)
    {
        if (TryReadFileBytes(PetSkinBytesPath, out data))
        {
            return true;
        }

        return TryReadConfigAssetBytes(ConfigPetSkinAssetPath, out data);
    }

    public bool TryReadConfigAssetBytes(string assetPath, out byte[] data)
    {
        data = null;
        if (!IsAvailable || string.IsNullOrWhiteSpace(assetPath) || !EnsureConfigManifestIndexed())
        {
            return false;
        }

        if (!_configAssetPathToHash.TryGetValue(assetPath, out var bundleHash)
            && !TryResolveConfigBundleHash(assetPath, out bundleHash))
        {
            return false;
        }

        var bundle = GetOrLoadConfigBundle(bundleHash);
        if (bundle == null)
        {
            return false;
        }

        var textAsset = bundle.LoadAsset<TextAsset>(assetPath)
                        ?? bundle.LoadAsset<TextAsset>(assetPath.ToLowerInvariant());
        if (textAsset == null)
        {
            return false;
        }

        data = textAsset.bytes;
        return data != null && data.Length > 0;
    }

    public void UnloadAllLoadedBundles()
    {
        foreach (var loadedBundle in _loadedConfigBundles.Values)
        {
            if (loadedBundle != null)
            {
                loadedBundle.Unload(false);
            }
        }

        _loadedConfigBundles.Clear();
    }

    public string DescribeStatus()
    {
        if (!IsAvailable)
        {
            return "[Local Seer Source] Not found. Select an install root or a synced mirror root.";
        }

        var sourceKind = File.Exists(ExecutablePath) || File.Exists(GameAssemblyPath)
            ? "install"
            : "mirror";

        return
            $"[Local Seer Source] {InstallRoot} ({sourceKind})\n" +
            $"  BuildInfo:        {GetBuildInfo()}\n" +
            $"  ConfigPackage:    {GetConfigPackageVersion()}\n" +
            $"  DefaultPackage:   {GetDefaultPackageVersion()}\n" +
            $"  MonstersRaw:      {MonstersBytesPath}\n" +
            $"  ConfigManifest:   {GetConfigPackageManifestBytesPath()}\n" +
            $"  DefaultManifest:  {GetDefaultPackageManifestBytesPath()}";
    }

    static string ReadTextIfExists(string path)
    {
        return File.Exists(path)
            ? File.ReadAllText(path).Trim()
            : "";
    }

    static bool TryReadFileBytes(string path, out byte[] data)
    {
        data = null;
        if (!File.Exists(path))
        {
            return false;
        }

        data = File.ReadAllBytes(path);
        return data.Length > 0;
    }

    string GetPackageManifestBytesPath(string packageName)
    {
        var manifestDir = GetPackageManifestDirectory(packageName);
        if (string.IsNullOrEmpty(manifestDir) || !Directory.Exists(manifestDir))
        {
            return "";
        }

        var version = GetPackageVersion(packageName);
        if (!string.IsNullOrEmpty(version))
        {
            var exactPath = Path.Combine(
                manifestDir,
                $"PackageManifest_{packageName}_{version}.bytes");
            if (File.Exists(exactPath))
            {
                return exactPath;
            }
        }

        var pattern = $"PackageManifest_{packageName}_*.bytes";
        var fallback = Directory.GetFiles(manifestDir, pattern)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        return fallback ?? "";
    }

    string GetPackageVersion(string packageName)
    {
        var versionPath = Path.Combine(
            GetPackageManifestDirectory(packageName),
            $"PackageManifest_{packageName}.version");
        return ReadTextIfExists(versionPath);
    }

    string GetPackageDirectory(string packageName)
    {
        switch (packageName)
        {
            case "ConfigPackage":
                return ConfigPackageDir;
            case "DefaultPackage":
                return DefaultPackageDir;
            default:
                return IsAvailable ? Path.Combine(YooRoot, packageName) : "";
        }
    }

    string GetPackageManifestDirectory(string packageName)
    {
        var packageDir = GetPackageDirectory(packageName);
        return string.IsNullOrEmpty(packageDir)
            ? ""
            : Path.Combine(packageDir, "ManifestFiles");
    }

    bool EnsureConfigManifestIndexed()
    {
        var manifestPath = GetConfigPackageManifestBytesPath();
        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
        {
            return false;
        }

        if (_configManifestReady
            && string.Equals(_indexedConfigManifestPath, manifestPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        _configAssetPathToHash.Clear();
        _orderedConfigHashes.Clear();
        UnloadAllLoadedBundles();

        var manifestBytes = File.ReadAllBytes(manifestPath);
        var manifestText = Encoding.ASCII.GetString(manifestBytes);
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hashMatches = BundleHashRegex.Matches(manifestText);
        for (var i = 0; i < hashMatches.Count; i++)
        {
            var hash = hashMatches[i].Value;
            if (seenHashes.Add(hash))
            {
                _orderedConfigHashes.Add(hash);
            }
        }

        _indexedConfigManifestPath = manifestPath;
        _configManifestReady = _orderedConfigHashes.Count > 0;
        return _configManifestReady;
    }

    bool TryResolveConfigBundleHash(string assetPath, out string bundleHash)
    {
        bundleHash = "";
        if (!_configManifestReady)
        {
            return false;
        }

        var manifestPath = _indexedConfigManifestPath;
        var manifestBytes = File.ReadAllBytes(manifestPath);
        var manifestText = Encoding.ASCII.GetString(manifestBytes);
        var assetOffset = manifestText.IndexOf(assetPath, StringComparison.Ordinal);
        if (assetOffset < 0)
        {
            return false;
        }

        var bundleIndexOffset = assetOffset + assetPath.Length;
        if (bundleIndexOffset + 1 >= manifestBytes.Length)
        {
            return false;
        }

        var bundleIndex = manifestBytes[bundleIndexOffset] + (manifestBytes[bundleIndexOffset + 1] << 8);
        if (bundleIndex < 0 || bundleIndex >= _orderedConfigHashes.Count)
        {
            return false;
        }

        bundleHash = _orderedConfigHashes[bundleIndex];
        _configAssetPathToHash[assetPath] = bundleHash;
        return true;
    }

    AssetBundle GetOrLoadConfigBundle(string bundleHash)
    {
        if (_loadedConfigBundles.TryGetValue(bundleHash, out var loadedBundle) && loadedBundle != null)
        {
            return loadedBundle;
        }

        var bundlePath = GetBundleDataPath("ConfigPackage", bundleHash);
        if (!File.Exists(bundlePath))
        {
            return null;
        }

        var bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
        {
            Debug.LogWarning($"[LocalUnityAssetReader] AssetBundle.LoadFromFile failed: {bundlePath}");
            return null;
        }

        _loadedConfigBundles[bundleHash] = bundle;
        return bundle;
    }
}
