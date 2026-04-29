using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace SeerAssetTool.Win;

public sealed class SeerToolService
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public string BaseDirectory => AppContext.BaseDirectory;

    public string DefaultMirrorRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SeerAssetTool",
            "SyncedSeerMirror");

    public string DefaultExportRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SeerAssetTool",
            "ExportedSeerData");

    public string FindBestInstallRoot()
    {
        foreach (var candidate in new[]
                 {
                     @"D:\SeerLauncher\games\NewSeer",
                     @"C:\SeerLauncher\games\NewSeer",
                     @"E:\SeerLauncher\games\NewSeer",
                 })
        {
            if (LooksLikeSourceRoot(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return string.Empty;
    }

    public bool LooksLikeSourceRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            return File.Exists(Path.Combine(fullPath, "Seer_Data", "yoo", "ConfigPackage", "ManifestFiles", "PackageManifest_ConfigPackage.version"))
                   && File.Exists(Path.Combine(fullPath, "Seer_Data", "yoo", "DefaultPackage", "ManifestFiles", "PackageManifest_DefaultPackage.version"));
        }
        catch
        {
            return false;
        }
    }

    public async Task<PythonCommand> EnsurePythonReadyAsync(Action<string>? log, bool bootstrap, CancellationToken cancellationToken = default)
    {
        var python = await ResolvePythonAsync(log, cancellationToken).ConfigureAwait(false);

        var importCheck = new List<string>(python.PrefixArguments)
        {
            "-c",
            "import UnityPy, PIL",
        };

        var checkResult = await ProcessRunner.RunAsync(
            python.FileName,
            importCheck,
            log,
            BaseDirectory,
            cancellationToken).ConfigureAwait(false);

        if (checkResult.ExitCode == 0)
        {
            return python;
        }

        if (!bootstrap)
        {
            throw new InvalidOperationException("Python was found, but UnityPy/Pillow are missing. Enable bootstrap or run: pip install UnityPy pillow");
        }

        log?.Invoke("Python dependencies are missing. Installing UnityPy and Pillow...");

        var installArgs = new List<string>(python.PrefixArguments)
        {
            "-m",
            "pip",
            "install",
            "UnityPy",
            "pillow",
        };

        var installResult = await ProcessRunner.RunAsync(
            python.FileName,
            installArgs,
            log,
            BaseDirectory,
            cancellationToken).ConfigureAwait(false);

        if (installResult.ExitCode != 0)
        {
            throw new InvalidOperationException("Failed to install Python dependencies required by the Seer asset tool.");
        }

        return python;
    }

    public async Task RunSyncAsync(SyncOptions options, Action<string>? log, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.MirrorRoot))
        {
            throw new ArgumentException("MirrorRoot is required.");
        }

        Directory.CreateDirectory(options.MirrorRoot);
        var arguments = new List<string>
        {
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            GetRequiredPath("sync-seer-remote.ps1"),
        };

        if (!string.IsNullOrWhiteSpace(options.InstallRoot))
        {
            arguments.Add("-InstallRoot");
            arguments.Add(Path.GetFullPath(options.InstallRoot));
        }

        arguments.Add("-MirrorRoot");
        arguments.Add(Path.GetFullPath(options.MirrorRoot));
        arguments.Add("-NoSummaryOutput");

        if (options.AllHeads)
        {
            arguments.Add("-AllHeads");
        }

        if (options.AllCountermarks)
        {
            arguments.Add("-AllCountermarks");
        }

        log?.Invoke("Syncing latest remote mirror...");

        var result = await ProcessRunner.RunAsync(
            "powershell.exe",
            arguments,
            log,
            BaseDirectory,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Remote mirror sync failed.");
        }
    }

    public async Task<CombinedExportSummary> RunExportAsync(ExportOptions options, Action<string>? log, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeSourceRoot(options.SourceRoot))
        {
            throw new ArgumentException($"Invalid Seer source root: {options.SourceRoot}");
        }

        if (string.IsNullOrWhiteSpace(options.OutputRoot))
        {
            throw new ArgumentException("OutputRoot is required.");
        }

        Directory.CreateDirectory(options.OutputRoot);
        var python = await EnsurePythonReadyAsync(log, options.BootstrapPython, cancellationToken).ConfigureAwait(false);

        ConfigExportResult? configResult = null;
        if (!options.SkipMonsters)
        {
            log?.Invoke("Exporting config JSON...");
            configResult = await RunConfigExportAsync(python, options, log, cancellationToken).ConfigureAwait(false);
            if (!configResult.Success)
            {
                throw new InvalidOperationException(configResult.Error);
            }
        }

        ImageExportResult? imageResult = null;
        if (!options.SkipHeads || !options.SkipCountermarks)
        {
            log?.Invoke("Exporting images...");
            imageResult = await RunImageExportAsync(python, options, log, cancellationToken).ConfigureAwait(false);
            if (!imageResult.Success)
            {
                throw new InvalidOperationException(imageResult.Error);
            }
        }

        var summary = BuildCombinedSummary(options, configResult, imageResult);
        var summaryPath = Path.Combine(options.OutputRoot, "seer-export-summary.json");
        await File.WriteAllTextAsync(
            summaryPath,
            JsonSerializer.Serialize(summary, JsonOptions),
            cancellationToken).ConfigureAwait(false);

        log?.Invoke($"Combined summary written to {summaryPath}");
        return summary;
    }

    public async Task<CombinedExportSummary> RunSyncAndExportAsync(SyncExportOptions options, Action<string>? log, CancellationToken cancellationToken = default)
    {
        await RunSyncAsync(
            new SyncOptions
            {
                InstallRoot = options.InstallRoot,
                MirrorRoot = options.MirrorRoot,
                AllHeads = options.AllHeads,
                AllCountermarks = options.AllCountermarks,
            },
            log,
            cancellationToken).ConfigureAwait(false);

        return await RunExportAsync(
            new ExportOptions
            {
                SourceRoot = options.MirrorRoot,
                OutputRoot = options.OutputRoot,
                Layout = options.Layout,
                Limit = options.Limit,
                SkipMonsters = options.SkipMonsters,
                SkipHeads = options.SkipHeads,
                SkipCountermarks = options.SkipCountermarks,
                BootstrapPython = options.BootstrapPython,
            },
            log,
            cancellationToken).ConfigureAwait(false);
    }

    public void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { Path.GetFullPath(path) },
            UseShellExecute = true,
        });
    }

    async Task<PythonCommand> ResolvePythonAsync(Action<string>? log, CancellationToken cancellationToken)
    {
        foreach (var candidate in new[]
                 {
                     new PythonCommand("python.exe", Array.Empty<string>()),
                     new PythonCommand("py.exe", new[] { "-3" }),
                 })
        {
            try
            {
                var versionArgs = new List<string>(candidate.PrefixArguments) { "--version" };
                var result = await ProcessRunner.RunAsync(
                    candidate.FileName,
                    versionArgs,
                    null,
                    BaseDirectory,
                    cancellationToken).ConfigureAwait(false);

                if (result.ExitCode == 0)
                {
                    log?.Invoke($"Using Python command: {candidate.FileName} {string.Join(" ", candidate.PrefixArguments)}".Trim());
                    return candidate;
                }
            }
            catch
            {
                // Try the next candidate.
            }
        }

        throw new InvalidOperationException("Python 3 was not found. Install Python 3.12+ and ensure python.exe or py.exe is available.");
    }

    async Task<ConfigExportResult> RunConfigExportAsync(
        PythonCommand python,
        ExportOptions options,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var args = new List<string>(python.PrefixArguments)
        {
            GetRequiredPath("export-seer-config.py"),
            "--source-root",
            Path.GetFullPath(options.SourceRoot),
            "--output-dir",
            Path.GetFullPath(options.OutputRoot),
            "--layout",
            ToLayoutArgument(options.Layout),
        };

        var result = await ProcessRunner.RunAsync(
            python.FileName,
            args,
            log,
            BaseDirectory,
            cancellationToken).ConfigureAwait(false);

        return ParseJsonResult<ConfigExportResult>(result, "config export");
    }

    async Task<ImageExportResult> RunImageExportAsync(
        PythonCommand python,
        ExportOptions options,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var args = new List<string>(python.PrefixArguments)
        {
            GetRequiredPath("export-seer-images.py"),
            "--source-root",
            Path.GetFullPath(options.SourceRoot),
            "--output-dir",
            Path.GetFullPath(options.OutputRoot),
            "--layout",
            ToLayoutArgument(options.Layout),
        };

        if (options.Limit > 0)
        {
            args.Add("--limit");
            args.Add(options.Limit.ToString());
        }

        if (options.SkipHeads)
        {
            args.Add("--skip-heads");
        }

        if (options.SkipCountermarks)
        {
            args.Add("--skip-countermarks");
        }

        var result = await ProcessRunner.RunAsync(
            python.FileName,
            args,
            log,
            BaseDirectory,
            cancellationToken).ConfigureAwait(false);

        return ParseJsonResult<ImageExportResult>(result, "image export");
    }

    T ParseJsonResult<T>(ProcessRunResult processResult, string operationName)
    {
        if (processResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"{operationName} failed with exit code {processResult.ExitCode}.");
        }

        var jsonText = processResult.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            throw new InvalidOperationException($"{operationName} produced no JSON output.");
        }

        var parsed = JsonSerializer.Deserialize<T>(jsonText, JsonOptions);
        if (parsed is null)
        {
            throw new InvalidOperationException($"{operationName} produced invalid JSON output.");
        }

        return parsed;
    }

    CombinedExportSummary BuildCombinedSummary(
        ExportOptions options,
        ConfigExportResult? configResult,
        ImageExportResult? imageResult)
    {
        return new CombinedExportSummary
        {
            Success = true,
            SourceRoot = Path.GetFullPath(options.SourceRoot),
            OutputRoot = Path.GetFullPath(options.OutputRoot),
            Layout = ToLayoutArgument(options.Layout),
            ConfigVersion = configResult?.ConfigVersion ?? string.Empty,
            DefaultManifestVersion = configResult?.DefaultManifestVersion
                                     ?? imageResult?.DefaultPackageVersion
                                     ?? string.Empty,
            MonsterCount = configResult?.MonsterCount ?? 0,
            MonsterSource = configResult?.MonsterSource ?? string.Empty,
            PetSkinCount = configResult?.PetSkinCount ?? 0,
            PetSkinSource = configResult?.PetSkinSource ?? string.Empty,
            PetSkinError = configResult?.PetSkinError ?? string.Empty,
            IndexedHeadCount = imageResult?.Heads?.Indexed ?? 0,
            ExportedHeadCount = imageResult?.Heads?.Exported ?? 0,
            FailedHeadCount = imageResult?.Heads?.Failed ?? 0,
            IndexedCountermarkCount = imageResult?.Countermarks?.Indexed ?? 0,
            ExportedCountermarkCount = imageResult?.Countermarks?.Exported ?? 0,
            FailedCountermarkCount = imageResult?.Countermarks?.Failed ?? 0,
            LimitPerCategory = options.Limit,
            HeadExportSource = imageResult?.Heads?.Source ?? string.Empty,
            CountermarkExportSource = imageResult?.Countermarks?.Source ?? string.Empty,
            HeadOutputDirectory = imageResult?.Heads?.OutputDirectory ?? string.Empty,
            CountermarkOutputDirectory = imageResult?.Countermarks?.OutputDirectory ?? string.Empty,
            Error = string.Empty,
        };
    }

    string GetRequiredPath(string fileName)
    {
        var candidate = Path.Combine(BaseDirectory, fileName);
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException($"Required tool file was not found next to the desktop tool: {candidate}");
        }

        return candidate;
    }

    static string ToLayoutArgument(ExportLayout layout) =>
        layout == ExportLayout.Cache ? "cache" : "mirror";
}
