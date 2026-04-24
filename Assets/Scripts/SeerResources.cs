using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 赛尔号资源入口配置。
/// 优先读取本地 Unity 客户端，其次回退到 GitHub 镜像。
/// </summary>
public static class SeerResources
{
    public const string ConfigMirrorBase =
        "https://raw.githubusercontent.com/oldml/SeerUnityConfig/main/config/";

    public const string AssetsMirrorBase =
        "https://raw.githubusercontent.com/SeerAPI/seer-unity-assets/main/newseer/assets/art/ui/assets/";

    public const string ConfigRepoOwner = "oldml";
    public const string ConfigRepoName = "SeerUnityConfig";
    public const string ConfigRepoMonstersPath = "config/monsters.json";

    public const string MonstersFile = "monsters.json";
    public const string PetSkinFile = "pet_skin.json";

    public const string LocalInstallPrefKey = "SeerLocalInstallPath";
    public const string LocalConfigVersionPrefKey = "SeerLocalConfigVersion";
    public const string LocalConfigInstallPrefKey = "SeerLocalConfigInstallPath";
    public const string LocalDefaultManifestVersionPrefKey = "SeerLocalDefaultManifestVersion";

    static readonly string[] CandidateLocalInstalls =
    {
        @"D:\SeerLauncher\games\NewSeer",
        @"C:\SeerLauncher\games\NewSeer",
        @"E:\SeerLauncher\games\NewSeer",
    };

    public static string MonstersJsonUrl => ConfigMirrorBase + MonstersFile;
    public static string PetSkinJsonUrl => ConfigMirrorBase + PetSkinFile;

    public static string PetHeadUrl(int monsterId)
    {
        var cachePath = MonsterHeadCacheFile(monsterId);
        EnsureMonsterHeadCached(monsterId, cachePath);
        return File.Exists(cachePath)
            ? ToFileUri(cachePath)
            : $"{AssetsMirrorBase}pet/head/{monsterId}.png";
    }

    public static string CountermarkIconUrl(string countermarkId)
    {
        var cachePath = CountermarkCacheFile(countermarkId);
        EnsureCountermarkCached(countermarkId, cachePath);
        return File.Exists(cachePath)
            ? ToFileUri(cachePath)
            : $"{AssetsMirrorBase}countermark/icon/{countermarkId}.png";
    }

    public static string UserDataRoot => Application.persistentDataPath;

    public static string LocalInstallPath
    {
        get
        {
            var saved = NormalizePath(PlayerPrefs.GetString(LocalInstallPrefKey, ""));
            if (LooksLikeLocalInstall(saved))
            {
                return saved;
            }

            var detected = FindBestLocalInstall();
            if (!string.IsNullOrEmpty(detected))
            {
                return detected;
            }

            return "";
        }
        set
        {
            var normalized = NormalizePath(value);
            PlayerPrefs.SetString(LocalInstallPrefKey, normalized);
            PlayerPrefs.Save();
        }
    }

    public static bool HasLocalInstall => !string.IsNullOrEmpty(LocalInstallPath);

    public static string FindBestLocalInstall()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer &&
            Application.platform != RuntimePlatform.WindowsEditor)
        {
            return "";
        }

        for (var i = 0; i < CandidateLocalInstalls.Length; i++)
        {
            var candidate = NormalizePath(CandidateLocalInstalls[i]);
            if (LooksLikeLocalInstall(candidate))
            {
                return candidate;
            }
        }

        return "";
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch
        {
            return path.Trim().Trim('"');
        }
    }

    public static bool LooksLikeLocalInstall(string path)
    {
        path = NormalizePath(path);
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
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

        var hasPackageState =
            File.Exists(configVersionPath)
            && File.Exists(defaultVersionPath);

        if (!hasPackageState)
        {
            return false;
        }

        // Allow either a real client install or a locally synced mirror root.
        return File.Exists(exePath)
               || File.Exists(gameAssemblyPath)
               || Directory.Exists(Path.Combine(path, "Seer_Data", "yoo", "ConfigPackage", "CacheBundleFiles"))
               || Directory.Exists(Path.Combine(path, "Seer_Data", "yoo", "DefaultPackage", "CacheBundleFiles"));
    }

    public static void EnsureUserDataRootExists()
    {
        if (!Directory.Exists(UserDataRoot))
        {
            Directory.CreateDirectory(UserDataRoot);
        }
    }

    public static string UserDataFile(string fileName)
    {
        EnsureUserDataRootExists();
        return Path.Combine(UserDataRoot, fileName);
    }

    public static string MonsterHeadCacheFile(int monsterId) =>
        UserDataFile($"{monsterId}.png");

    public static string CountermarkCacheFile(string countermarkId) =>
        UserDataFile($"countermark_{countermarkId}.png");

    static void EnsureMonsterHeadCached(int monsterId, string cachePath)
    {
        if (File.Exists(cachePath))
        {
            return;
        }

        if (SeerLocalImageResolver.Instance.TryLoadMonsterHeadTexture(monsterId, out var texture))
        {
            SeerLocalImageResolver.TrySaveTextureToCache(cachePath, texture);
        }
    }

    static void EnsureCountermarkCached(string countermarkId, string cachePath)
    {
        if (File.Exists(cachePath))
        {
            return;
        }

        if (SeerLocalImageResolver.Instance.TryLoadCountermarkTexture(countermarkId, out var texture))
        {
            SeerLocalImageResolver.TrySaveTextureToCache(cachePath, texture);
        }
    }

    static string ToFileUri(string path)
    {
        return new Uri(path).AbsoluteUri;
    }
}
