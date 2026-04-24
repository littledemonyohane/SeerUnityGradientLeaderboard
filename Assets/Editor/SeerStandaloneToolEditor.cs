using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SeerStandaloneToolEditor
{
    const string ToolExePathPrefKey = "SeerStandaloneToolExePath";

    static string RepoRoot => Directory.GetParent(Application.dataPath)?.FullName ?? ".";
    static string ToolProjectPath => Path.Combine(RepoRoot, "tools", "SeerAssetTool.Win", "SeerAssetTool.Win.csproj");
    static string ToolProjectDir => Path.GetDirectoryName(ToolProjectPath) ?? RepoRoot;
    static string PublishedExePath => Path.Combine(ToolProjectDir, "bin", "Release", "net8.0-windows", "win-x64", "publish", "SeerAssetTool.Win.exe");
    static string ReleaseExePath => Path.Combine(ToolProjectDir, "bin", "Release", "net8.0-windows", "SeerAssetTool.Win.exe");
    static string DefaultMirrorRoot => Path.Combine(RepoRoot, "StandaloneToolMirror");

    [MenuItem("Seer/Standalone Tool/Publish Windows Tool")]
    public static void PublishWindowsTool()
    {
        RunProcessOrThrow(
            "dotnet",
            new[]
            {
                "publish",
                ToolProjectPath,
                "-c", "Release",
                "-r", "win-x64",
                "--self-contained", "true",
                "-p:PublishSingleFile=true",
            },
            RepoRoot,
            "Publishing the standalone Windows tool");

        EditorPrefs.SetString(ToolExePathPrefKey, PublishedExePath);
        EditorUtility.DisplayDialog(
            "Publish Complete",
            $"Standalone tool published to:\n{PublishedExePath}",
            "OK");
    }

    [MenuItem("Seer/Standalone Tool/Launch Windows Tool")]
    public static void LaunchWindowsTool()
    {
        var toolPath = ResolveToolExePath(requirePublished: false);
        if (string.IsNullOrEmpty(toolPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = toolPath,
            WorkingDirectory = Path.GetDirectoryName(toolPath) ?? RepoRoot,
            UseShellExecute = true,
        });
    }

    [MenuItem("Seer/Standalone Tool/Export Project Cache Via Tool")]
    public static void ExportProjectCacheViaTool()
    {
        var toolPath = ResolveToolExePath(requirePublished: false);
        if (string.IsNullOrEmpty(toolPath))
        {
            return;
        }

        var sourceRoot = EnsureSourceRoot();
        if (string.IsNullOrEmpty(sourceRoot))
        {
            return;
        }

        var outputRoot = Application.persistentDataPath;
        Directory.CreateDirectory(outputRoot);

        RunToolCommandOrThrow(
            toolPath,
            new[]
            {
                "export",
                "--source-root", sourceRoot,
                "--output-root", outputRoot,
                "--layout", "cache",
            },
            "Exporting project cache via the standalone tool");

        EditorUtility.RevealInFinder(outputRoot);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Export Complete",
            $"Project cache updated via standalone tool:\n{outputRoot}",
            "OK");
    }

    [MenuItem("Seer/Standalone Tool/Sync Latest Mirror And Export Cache Via Tool")]
    public static void SyncLatestMirrorAndExportCacheViaTool()
    {
        var toolPath = ResolveToolExePath(requirePublished: false);
        if (string.IsNullOrEmpty(toolPath))
        {
            return;
        }

        var installRoot = EnsureSourceRoot();
        if (string.IsNullOrEmpty(installRoot))
        {
            return;
        }

        var mirrorRoot = DefaultMirrorRoot;
        var outputRoot = Application.persistentDataPath;
        Directory.CreateDirectory(outputRoot);

        RunToolCommandOrThrow(
            toolPath,
            new[]
            {
                "sync-export",
                "--install-root", installRoot,
                "--mirror-root", mirrorRoot,
                "--output-root", outputRoot,
                "--layout", "cache",
            },
            "Syncing the latest mirror and exporting cache via the standalone tool");

        EditorUtility.RevealInFinder(outputRoot);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Sync + Export Complete",
            $"Mirror root:\n{mirrorRoot}\n\nProject cache:\n{outputRoot}",
            "OK");
    }

    [MenuItem("Seer/Standalone Tool/Choose Tool EXE")]
    public static void ChooseToolExe()
    {
        var current = EditorPrefs.GetString(ToolExePathPrefKey, ResolveDefaultToolPath() ?? "");
        var selected = EditorUtility.OpenFilePanel(
            "Choose SeerAssetTool.Win.exe",
            string.IsNullOrEmpty(current) ? ToolProjectDir : Path.GetDirectoryName(current),
            "exe");

        if (string.IsNullOrEmpty(selected))
        {
            return;
        }

        EditorPrefs.SetString(ToolExePathPrefKey, selected);
        EditorUtility.DisplayDialog("Tool Path Saved", selected, "OK");
    }

    static string EnsureSourceRoot()
    {
        var current = SeerResources.LocalInstallPath;
        if (SeerResources.LooksLikeLocalInstall(current))
        {
            return current;
        }

        var picked = EditorUtility.OpenFolderPanel(
            "Choose the NewSeer install or mirror root",
            string.IsNullOrEmpty(current) ? @"D:\" : current,
            "");
        if (string.IsNullOrEmpty(picked))
        {
            return "";
        }

        if (!SeerResources.LooksLikeLocalInstall(picked))
        {
            EditorUtility.DisplayDialog(
                "Invalid Folder",
                "The selected folder does not look like a valid NewSeer install or mirror root.",
                "OK");
            return "";
        }

        SeerResources.LocalInstallPath = picked;
        return picked;
    }

    static string ResolveToolExePath(bool requirePublished)
    {
        var saved = EditorPrefs.GetString(ToolExePathPrefKey, "");
        if (File.Exists(saved))
        {
            return saved;
        }

        var defaultPath = requirePublished ? PublishedExePath : ResolveDefaultToolPath();
        if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
        {
            return defaultPath;
        }

        var message = requirePublished
            ? "Standalone tool EXE was not found. Publish it first."
            : "Standalone tool EXE was not found. Build or publish it first.";
        EditorUtility.DisplayDialog("Tool Not Found", message, "OK");
        return "";
    }

    static string ResolveDefaultToolPath()
    {
        if (File.Exists(PublishedExePath))
        {
            return PublishedExePath;
        }

        if (File.Exists(ReleaseExePath))
        {
            return ReleaseExePath;
        }

        return "";
    }

    static void RunToolCommandOrThrow(string toolPath, string[] args, string title)
    {
        RunProcessOrThrow(toolPath, args, Path.GetDirectoryName(toolPath) ?? RepoRoot, title);
    }

    static void RunProcessOrThrow(string fileName, string[] args, string workingDirectory, string progressTitle)
    {
        try
        {
            EditorUtility.DisplayProgressBar(progressTitle, "Running...", 0.3f);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            for (var i = 0; i < args.Length; i++)
            {
                process.StartInfo.ArgumentList.Add(args[i]);
            }

            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                UnityEngine.Debug.Log(stdout);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                UnityEngine.Debug.LogWarning(stderr);
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Command failed with exit code {process.ExitCode}.\n\nSTDERR:\n{stderr}\n\nSTDOUT:\n{stdout}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
