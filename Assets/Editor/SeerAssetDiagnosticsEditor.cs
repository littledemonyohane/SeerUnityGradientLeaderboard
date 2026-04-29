using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utilities and batch-mode export entry points for local Seer Unity assets.
/// </summary>
public static class SeerAssetDiagnosticsEditor
{
    [Serializable]
    class BatchMirrorExportSummary
    {
        public bool Success;
        public string InstallRoot;
        public string OutputRoot;
        public string ConfigVersion;
        public string DefaultManifestVersion;
        public int MonsterCount;
        public int PetSkinCount;
        public string PetSkinSource;
        public int IndexedHeadCount;
        public int ExportedHeadCount;
        public int FailedHeadCount;
        public int IndexedCountermarkCount;
        public int ExportedCountermarkCount;
        public int FailedCountermarkCount;
        public int LimitPerCategory;
        public string Error;
    }

    static string RepoRoot => Directory.GetParent(Application.dataPath)?.FullName ?? ".";
    static string BatchLogPath => Path.Combine(RepoRoot, "SeerBatch.log");

    [MenuItem("Seer/Asset Diagnostics/Check Local Unity Install")]
    public static void CheckLocalUnityInstall()
    {
        var reader = new LocalUnityAssetReader();
        var message = reader.IsAvailable
            ? reader.DescribeStatus()
            : "No valid local NewSeer source root was found.";

        EditorUtility.DisplayDialog("Local Seer Source", message, "OK");
        Debug.Log(message);
    }

    [MenuItem("Seer/Asset Diagnostics/Set Local Install Path")]
    public static void SetLocalInstallPath()
    {
        var currentPath = SeerResources.LocalInstallPath;
        var pickedPath = EditorUtility.OpenFolderPanel(
            "Choose the NewSeer install or mirror root",
            string.IsNullOrEmpty(currentPath) ? @"D:\" : currentPath,
            "");

        if (string.IsNullOrEmpty(pickedPath))
        {
            return;
        }

        if (!SeerResources.LooksLikeLocalInstall(pickedPath))
        {
            EditorUtility.DisplayDialog(
                "Invalid Folder",
                "The selected folder does not look like a valid NewSeer install or mirror root.",
                "OK");
            return;
        }

        SeerResources.LocalInstallPath = pickedPath;
        Debug.Log($"[SeerResources] Local source root saved: {pickedPath}");
        CheckLocalUnityInstall();
    }

    [MenuItem("Seer/Asset Diagnostics/Clear Local Install Path")]
    public static void ClearLocalInstallPath()
    {
        SeerResources.LocalInstallPath = "";
        Debug.Log("[SeerResources] Cleared the saved local source root.");
    }

    [MenuItem("Seer/Asset Diagnostics/Export Local Config To Persistent Data")]
    public static void ExportLocalConfigToPersistentData()
    {
        var reader = new LocalUnityAssetReader();
        if (!reader.IsAvailable)
        {
            EditorUtility.DisplayDialog("Export Failed", "No valid local Seer source root was found.", "OK");
            return;
        }

        var result = RunConfigExport(reader, SeerResources.UserDataRoot, true);
        if (!result.Success)
        {
            EditorUtility.DisplayDialog("Export Failed", result.Error ?? "Unknown error.", "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "Export Complete",
            $"monsters.json: {result.MonstersJsonPath}\npet_skin.json: {result.PetSkinJsonPath}",
            "OK");
    }

    [MenuItem("Seer/Asset Diagnostics/Export Local Monster Head Sample")]
    public static void ExportMonsterHeadSample()
    {
        var cachePath = SeerResources.MonsterHeadCacheFile(1);
        var success = ExportMonsterHeadToPath(1, cachePath);

        EditorUtility.DisplayDialog(
            "Monster Head Sample",
            success ? $"Exported to:\n{cachePath}" : "Failed to export monster head 1 from the local client.",
            "OK");
    }

    [MenuItem("Seer/Asset Diagnostics/Open Persistent Data Directory")]
    public static void OpenPersistentDataDir()
    {
        if (!Directory.Exists(Application.persistentDataPath))
        {
            Directory.CreateDirectory(Application.persistentDataPath);
        }

        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    [MenuItem("Seer/Asset Diagnostics/Clear JSON Cache And Local Version Marks")]
    public static void ClearJsonCache()
    {
        var removed = 0;
        foreach (var fileName in new[] { SeerResources.MonstersFile, SeerResources.PetSkinFile })
        {
            var path = SeerResources.UserDataFile(fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            File.Delete(path);
            removed++;
        }

        PlayerPrefs.DeleteKey("localSHA");
        PlayerPrefs.DeleteKey(SeerResources.LocalInstallPrefKey);
        PlayerPrefs.DeleteKey(SeerResources.LocalConfigVersionPrefKey);
        PlayerPrefs.DeleteKey(SeerResources.LocalConfigInstallPrefKey);
        PlayerPrefs.DeleteKey(SeerResources.LocalDefaultManifestVersionPrefKey);
        PlayerPrefs.Save();

        EditorUtility.DisplayDialog(
            "Cache Cleared",
            $"Removed {removed} JSON cache file(s) and cleared saved local version markers.",
            "OK");
    }

    [MenuItem("Seer/Asset Diagnostics/Open Config Mirror Repo")]
    public static void OpenConfigMirrorRepo()
    {
        Application.OpenURL("https://github.com/littledemonyohane/SeerUnityGradientLeaderboard/tree/main/config");
    }

    [MenuItem("Seer/Asset Diagnostics/Open Assets Mirror Repo")]
    public static void OpenAssetsMirrorRepo()
    {
        Application.OpenURL("https://github.com/littledemonyohane/SeerUnityGradientLeaderboard/tree/main/newseer/assets/art/ui/assets");
    }

    public static void BatchExportLocalConfigToPersistentData()
    {
        BatchWriteLog("BatchExportLocalConfigToPersistentData:start");
        var reader = ResolveReaderFromCommandLineOrPrefs();
        EnsureLocalInstallAvailable(reader, "BatchExportLocalConfigToPersistentData");

        BatchWriteLog($"installRoot={reader.InstallRoot}");
        BatchWriteLog($"persistentDataPath={Application.persistentDataPath}");

        var result = RunConfigExport(reader, SeerResources.UserDataRoot, true);
        if (!result.Success)
        {
            FailBatch(
                "BatchExportLocalConfigToPersistentData",
                result.Error ?? "Unknown export failure.");
        }

        BatchWriteLog(
            $"BatchExportLocalConfigToPersistentData:success monsters={result.MonsterCount} petSkins={result.PetSkinCount} path={result.MonstersJsonPath}");
    }

    public static void BatchExportMonsterHeadSample()
    {
        BatchWriteLog("BatchExportMonsterHeadSample:start");
        var reader = ResolveReaderFromCommandLineOrPrefs();
        EnsureLocalInstallAvailable(reader, "BatchExportMonsterHeadSample");

        var cachePath = SeerResources.MonsterHeadCacheFile(1);
        if (!ExportMonsterHeadToPath(1, cachePath))
        {
            FailBatch("BatchExportMonsterHeadSample", "Failed to export monster head 1 from local bundles.");
        }

        BatchWriteLog($"BatchExportMonsterHeadSample:success path={cachePath}");
        Debug.Log($"[SeerBatch] Monster head 1 exported to {cachePath}");
    }

    public static void BatchExportMirrorLayoutFromCommandLine()
    {
        BatchWriteLog("BatchExportMirrorLayoutFromCommandLine:start");

        var reader = ResolveReaderFromCommandLineOrPrefs();
        EnsureLocalInstallAvailable(reader, "BatchExportMirrorLayoutFromCommandLine");

        var outputRoot = ResolveBatchOutputRoot();
        var limit = ReadIntArgument("-seerLimit", 0);
        var skipMonsters = HasCommandLineFlag("-seerSkipMonsters");
        var skipHeads = HasCommandLineFlag("-seerSkipHeads");
        var skipCountermarks = HasCommandLineFlag("-seerSkipCountermarks");

        var summary = new BatchMirrorExportSummary
        {
            InstallRoot = reader.InstallRoot,
            OutputRoot = outputRoot,
            ConfigVersion = reader.GetConfigPackageVersion(),
            DefaultManifestVersion = reader.GetDefaultPackageVersion(),
            LimitPerCategory = limit,
        };

        try
        {
            Directory.CreateDirectory(outputRoot);

            if (!skipMonsters)
            {
                var configDir = Path.Combine(outputRoot, "config");
                var configResult = RunConfigExport(reader, configDir, false);
                if (!configResult.Success)
                {
                    throw new InvalidOperationException(configResult.Error ?? "Failed to export config.");
                }

                summary.MonsterCount = configResult.MonsterCount;
                summary.PetSkinCount = configResult.PetSkinCount;
                summary.PetSkinSource = configResult.PetSkinSource ?? "";
            }

            var resolver = SeerLocalImageResolver.Instance;

            if (!skipHeads)
            {
                if (!resolver.TryGetMonsterHeadIds(out var monsterIds))
                {
                    throw new InvalidOperationException("Failed to index local monster head assets.");
                }

                summary.IndexedHeadCount = monsterIds.Count;
                var headOutputDir = Path.Combine(outputRoot, "newseer", "assets", "art", "ui", "assets", "pet", "head");
                ExportMonsterHeads(resolver, monsterIds, headOutputDir, limit, summary);
            }

            if (!skipCountermarks)
            {
                if (!resolver.TryGetCountermarkIds(out var countermarkIds))
                {
                    throw new InvalidOperationException("Failed to index local countermark assets.");
                }

                summary.IndexedCountermarkCount = countermarkIds.Count;
                var countermarkOutputDir = Path.Combine(outputRoot, "newseer", "assets", "art", "ui", "assets", "countermark", "icon");
                ExportCountermarks(resolver, countermarkIds, countermarkOutputDir, limit, summary);
            }

            summary.Success = true;
            WriteBatchSummary(outputRoot, summary);
            BatchWriteLog("BatchExportMirrorLayoutFromCommandLine:success");
            Debug.Log($"[SeerBatch] Mirror export complete: {outputRoot}");
        }
        catch (Exception ex)
        {
            summary.Success = false;
            summary.Error = ex.ToString();
            WriteBatchSummary(outputRoot, summary);
            FailBatch("BatchExportMirrorLayoutFromCommandLine", ex.ToString());
        }
    }

    static SeerLocalConfigExportResult RunConfigExport(LocalUnityAssetReader reader, string outputDir, bool preserveExistingPetSkin)
    {
        SeerResources.LocalInstallPath = reader.InstallRoot;

        var exporter = new SeerLocalConfigExporter(reader);
        var result = exporter.ExportToDirectory(outputDir, preserveExistingPetSkin);
        if (result.Success)
        {
            PersistLocalMetadata(result);
            Debug.Log(
                $"[SeerLocalExport] monsters={result.MonsterCount}, petSkins={result.PetSkinCount}, " +
                $"petSkinSource={result.PetSkinSource}");
        }

        return result;
    }

    static void PersistLocalMetadata(SeerLocalConfigExportResult result)
    {
        SeerResources.LocalInstallPath = result.InstallRoot ?? "";
        PlayerPrefs.SetString(SeerResources.LocalConfigVersionPrefKey, result.ConfigVersion ?? "");
        PlayerPrefs.SetString(SeerResources.LocalConfigInstallPrefKey, result.InstallRoot ?? "");
        PlayerPrefs.SetString(SeerResources.LocalDefaultManifestVersionPrefKey, result.DefaultManifestVersion ?? "");
        PlayerPrefs.Save();
    }

    static LocalUnityAssetReader ResolveReaderFromCommandLineOrPrefs()
    {
        if (TryGetCommandLineArgValue("-seerInstallRoot", out var installRoot)
            && SeerResources.LooksLikeLocalInstall(installRoot))
        {
            SeerResources.LocalInstallPath = installRoot;
            return new LocalUnityAssetReader(installRoot);
        }

        return new LocalUnityAssetReader();
    }

    static void EnsureLocalInstallAvailable(LocalUnityAssetReader reader, string batchAction)
    {
        if (reader.IsAvailable)
        {
            return;
        }

        FailBatch(batchAction, "No valid local Seer source root was found.");
    }

    static string ResolveBatchOutputRoot()
    {
        if (TryGetCommandLineArgValue("-seerOutput", out var outputRoot))
        {
            return Path.GetFullPath(outputRoot);
        }

        return Path.Combine(RepoRoot, "ExportedSeerMirror");
    }

    static int ReadIntArgument(string name, int defaultValue)
    {
        if (!TryGetCommandLineArgValue(name, out var value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsedValue) && parsedValue > 0
            ? parsedValue
            : defaultValue;
    }

    static bool HasCommandLineFlag(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    static bool TryGetCommandLineArgValue(string name, out string value)
    {
        value = "";
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = args[i + 1];
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    static void ExportMonsterHeads(
        SeerLocalImageResolver resolver,
        List<int> monsterIds,
        string outputDir,
        int limit,
        BatchMirrorExportSummary summary)
    {
        Directory.CreateDirectory(outputDir);
        var maxCount = limit > 0 ? Math.Min(limit, monsterIds.Count) : monsterIds.Count;

        for (var i = 0; i < maxCount; i++)
        {
            var outputPath = Path.Combine(outputDir, $"{monsterIds[i]}.png");
            if (ExportMonsterHeadToPath(monsterIds[i], outputPath))
            {
                summary.ExportedHeadCount++;
            }
            else
            {
                summary.FailedHeadCount++;
            }

            if ((i + 1) % 200 == 0)
            {
                resolver.UnloadAllLoadedBundles();
            }
        }

        resolver.UnloadAllLoadedBundles();
    }

    static void ExportCountermarks(
        SeerLocalImageResolver resolver,
        List<string> countermarkIds,
        string outputDir,
        int limit,
        BatchMirrorExportSummary summary)
    {
        Directory.CreateDirectory(outputDir);
        var maxCount = limit > 0 ? Math.Min(limit, countermarkIds.Count) : countermarkIds.Count;

        for (var i = 0; i < maxCount; i++)
        {
            var outputPath = Path.Combine(outputDir, $"{countermarkIds[i]}.png");
            if (resolver.TryLoadCountermarkTexture(countermarkIds[i], out var texture))
            {
                SeerLocalImageResolver.TrySaveTextureToCache(outputPath, texture);
                summary.ExportedCountermarkCount++;
            }
            else
            {
                summary.FailedCountermarkCount++;
            }

            if ((i + 1) % 200 == 0)
            {
                resolver.UnloadAllLoadedBundles();
            }
        }

        resolver.UnloadAllLoadedBundles();
    }

    static bool ExportMonsterHeadToPath(int monsterId, string outputPath)
    {
        if (!SeerLocalImageResolver.Instance.TryLoadMonsterHeadTexture(monsterId, out var texture))
        {
            return false;
        }

        SeerLocalImageResolver.TrySaveTextureToCache(outputPath, texture);
        return File.Exists(outputPath);
    }

    static void WriteBatchSummary(string outputRoot, BatchMirrorExportSummary summary)
    {
        if (string.IsNullOrEmpty(outputRoot))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(outputRoot);
            var summaryPath = Path.Combine(outputRoot, "seer-export-summary.json");
            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SeerBatch] Failed to write export summary: {ex.Message}");
        }
    }

    static void FailBatch(string action, string message)
    {
        BatchWriteLog($"{action}:failed");
        BatchWriteLog(message);
        Debug.LogError($"[SeerBatch] {message}");
        throw new InvalidOperationException(message);
    }

    static void BatchWriteLog(string message)
    {
        try
        {
            File.AppendAllText(BatchLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SeerBatch] Failed to write batch log: {ex.Message}");
        }
    }
}
