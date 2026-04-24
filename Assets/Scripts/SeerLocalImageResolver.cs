using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 从本地 Unity 客户端的 DefaultPackage manifest 中建立资源路径到 bundle 的映射，
/// 并按需读取头像/刻印图。
/// </summary>
public class SeerLocalImageResolver
{
    const string PetHeadPrefix = "Assets/Art/Ui/assets/pet/head/";
    const string CountermarkPrefix = "Assets/Art/Ui/assets/countermark/icon/";
    const string PngSuffix = ".png";

    static readonly Regex HashRegex =
        new Regex(@"\b[0-9a-f]{32}\b", RegexOptions.Compiled);

    static SeerLocalImageResolver _instance;

    readonly Dictionary<string, string> _assetPathToHash =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    readonly Dictionary<string, AssetBundle> _loadedBundles =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    readonly List<string> _orderedHashes = new List<string>(8192);

    string _installRoot = "";
    string _manifestPath = "";
    bool _initialized;

    public static SeerLocalImageResolver Instance => _instance ?? (_instance = new SeerLocalImageResolver());

    public bool TryLoadMonsterHeadTextureCachedOrLocal(int monsterId, out Texture2D texture)
    {
        var cachePath = SeerResources.MonsterHeadCacheFile(monsterId);
        if (TryLoadCachedTexture(cachePath, out texture))
        {
            return true;
        }

        if (!TryLoadMonsterHeadTexture(monsterId, out texture))
        {
            return false;
        }

        TrySaveTextureToCache(cachePath, texture);
        return true;
    }

    public bool TryLoadCountermarkTextureCachedOrLocal(string countermarkId, out Texture2D texture)
    {
        var cachePath = SeerResources.CountermarkCacheFile(countermarkId);
        if (TryLoadCachedTexture(cachePath, out texture))
        {
            return true;
        }

        if (!TryLoadCountermarkTexture(countermarkId, out texture))
        {
            return false;
        }

        TrySaveTextureToCache(cachePath, texture);
        return true;
    }

    public bool TryLoadMonsterHeadTexture(int monsterId, out Texture2D texture)
    {
        return TryLoadTextureForAssetPath($"{PetHeadPrefix}{monsterId}{PngSuffix}", out texture);
    }

    public bool TryLoadCountermarkTexture(string countermarkId, out Texture2D texture)
    {
        return TryLoadTextureForAssetPath($"{CountermarkPrefix}{countermarkId}{PngSuffix}", out texture);
    }

    public bool HasLocalManifest()
    {
        return EnsureInitialized();
    }

    public bool TryGetMonsterHeadIds(out List<int> monsterIds)
    {
        monsterIds = new List<int>();
        if (!EnsureInitialized())
        {
            return false;
        }

        var seen = new HashSet<int>();
        foreach (var assetPath in _assetPathToHash.Keys)
        {
            if (!TryExtractIntAssetId(assetPath, PetHeadPrefix, out var monsterId))
            {
                continue;
            }

            if (seen.Add(monsterId))
            {
                monsterIds.Add(monsterId);
            }
        }

        monsterIds.Sort();
        return monsterIds.Count > 0;
    }

    public bool TryGetCountermarkIds(out List<string> countermarkIds)
    {
        countermarkIds = new List<string>();
        if (!EnsureInitialized())
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assetPath in _assetPathToHash.Keys)
        {
            if (!TryExtractStringAssetId(assetPath, CountermarkPrefix, out var countermarkId))
            {
                continue;
            }

            if (seen.Add(countermarkId))
            {
                countermarkIds.Add(countermarkId);
            }
        }

        countermarkIds.Sort(StringComparer.OrdinalIgnoreCase);
        return countermarkIds.Count > 0;
    }

    public void UnloadAllLoadedBundles()
    {
        foreach (var loadedBundle in _loadedBundles.Values)
        {
            if (loadedBundle != null)
            {
                loadedBundle.Unload(false);
            }
        }

        _loadedBundles.Clear();
    }

    public static bool TryLoadCachedTexture(string cachePath, out Texture2D texture)
    {
        texture = null;
        try
        {
            if (!File.Exists(cachePath))
            {
                return false;
            }

            var fileData = File.ReadAllBytes(cachePath);
            var cachedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!cachedTexture.LoadImage(fileData))
            {
                UnityEngine.Object.Destroy(cachedTexture);
                return false;
            }

            texture = cachedTexture;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SeerLocalImageResolver] 读取缓存图片失败：{cachePath}\n{ex.Message}");
            return false;
        }
    }

    public static void TrySaveTextureToCache(string cachePath, Texture2D texture)
    {
        if (texture == null || string.IsNullOrEmpty(cachePath))
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var pngBytes = EncodeTextureToPng(texture);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return;
            }

            File.WriteAllBytes(cachePath, pngBytes);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SeerLocalImageResolver] 写入缓存图片失败：{cachePath}\n{ex.Message}");
        }
    }

    bool TryLoadTextureForAssetPath(string assetPath, out Texture2D texture)
    {
        texture = null;
        if (!EnsureInitialized())
        {
            return false;
        }

        if (!_assetPathToHash.TryGetValue(assetPath, out var bundleHash))
        {
            return false;
        }

        var bundle = GetOrLoadBundle(bundleHash);
        if (bundle == null)
        {
            return false;
        }

        var lowerAssetPath = assetPath.ToLowerInvariant();
        var sprite = bundle.LoadAsset<Sprite>(lowerAssetPath) ?? bundle.LoadAsset<Sprite>(assetPath);
        if (sprite != null && sprite.texture != null)
        {
            texture = sprite.texture;
            return true;
        }

        texture = bundle.LoadAsset<Texture2D>(lowerAssetPath) ?? bundle.LoadAsset<Texture2D>(assetPath);
        return texture != null;
    }

    AssetBundle GetOrLoadBundle(string bundleHash)
    {
        if (_loadedBundles.TryGetValue(bundleHash, out var loadedBundle) && loadedBundle != null)
        {
            return loadedBundle;
        }

        var reader = new LocalUnityAssetReader(_installRoot);
        if (!reader.IsAvailable)
        {
            return null;
        }

        var bundlePath = reader.GetBundleDataPath(bundleHash);
        if (!File.Exists(bundlePath))
        {
            return null;
        }

        var bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
        {
            Debug.LogWarning($"[SeerLocalImageResolver] AssetBundle.LoadFromFile 失败：{bundlePath}");
            return null;
        }

        _loadedBundles[bundleHash] = bundle;
        return bundle;
    }

    bool EnsureInitialized()
    {
        var reader = new LocalUnityAssetReader();
        if (!reader.IsAvailable)
        {
            return false;
        }

        var manifestPath = reader.GetDefaultPackageManifestBytesPath();
        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
        {
            return false;
        }

        if (_initialized
            && string.Equals(_installRoot, reader.InstallRoot, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_manifestPath, manifestPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            RebuildLookup(reader, manifestPath);
            return true;
        }
        catch (Exception ex)
        {
            _initialized = false;
            Debug.LogWarning($"[SeerLocalImageResolver] 解析 DefaultPackage manifest 失败：\n{ex}");
            return false;
        }
    }

    void RebuildLookup(LocalUnityAssetReader reader, string manifestPath)
    {
        _assetPathToHash.Clear();
        _orderedHashes.Clear();

        UnloadAllLoadedBundles();

        var manifestBytes = File.ReadAllBytes(manifestPath);
        var manifestText = Encoding.ASCII.GetString(manifestBytes);

        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hashMatches = HashRegex.Matches(manifestText);
        for (var i = 0; i < hashMatches.Count; i++)
        {
            var hash = hashMatches[i].Value;
            if (seenHashes.Add(hash))
            {
                _orderedHashes.Add(hash);
            }
        }

        IndexAssetPaths(manifestText, manifestBytes, PetHeadPrefix);
        IndexAssetPaths(manifestText, manifestBytes, CountermarkPrefix);

        _installRoot = reader.InstallRoot;
        _manifestPath = manifestPath;
        _initialized = true;

        Debug.Log(
            $"[SeerLocalImageResolver] 索引完成：{_assetPathToHash.Count} 条资源路径，" +
            $"{_orderedHashes.Count} 个 bundle hash。");
    }

    void IndexAssetPaths(string manifestText, byte[] manifestBytes, string prefix)
    {
        var searchStart = 0;
        while (searchStart >= 0 && searchStart < manifestText.Length)
        {
            var start = manifestText.IndexOf(prefix, searchStart, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var end = manifestText.IndexOf(PngSuffix, start, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            end += PngSuffix.Length;
            if (end + 1 >= manifestBytes.Length)
            {
                break;
            }

            var assetPath = manifestText.Substring(start, end - start);
            var bundleIndex = manifestBytes[end] + (manifestBytes[end + 1] * 256);
            if (bundleIndex >= 0 && bundleIndex < _orderedHashes.Count)
            {
                _assetPathToHash[assetPath] = _orderedHashes[bundleIndex];
            }

            searchStart = end;
        }
    }

    static bool TryExtractIntAssetId(string assetPath, string prefix, out int assetId)
    {
        assetId = 0;
        if (!TryExtractStringAssetId(assetPath, prefix, out var value))
        {
            return false;
        }

        return int.TryParse(value, out assetId);
    }

    static bool TryExtractStringAssetId(string assetPath, string prefix, out string assetId)
    {
        assetId = "";
        if (string.IsNullOrEmpty(assetPath)
            || !assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !assetPath.EndsWith(PngSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        assetId = assetPath.Substring(
            prefix.Length,
            assetPath.Length - prefix.Length - PngSuffix.Length);
        return !string.IsNullOrEmpty(assetId);
    }

    static byte[] EncodeTextureToPng(Texture2D texture)
    {
        try
        {
            return texture.EncodeToPNG();
        }
        catch
        {
            var readableCopy = CreateReadableCopy(texture);
            if (readableCopy == null)
            {
                return null;
            }

            try
            {
                return readableCopy.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.Destroy(readableCopy);
            }
        }
    }

    static Texture2D CreateReadableCopy(Texture2D source)
    {
        RenderTexture temporary = null;
        var previousActive = RenderTexture.active;

        try
        {
            temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            var readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply();
            return readableTexture;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SeerLocalImageResolver] 复制可读贴图失败：{ex.Message}");
            return null;
        }
        finally
        {
            RenderTexture.active = previousActive;
            if (temporary != null)
            {
                RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
