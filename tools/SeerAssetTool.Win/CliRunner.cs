namespace SeerAssetTool.Win;

public sealed class CliRunner
{
    readonly SeerToolService _service;

    public CliRunner(SeerToolService service)
    {
        _service = service;
    }

    public async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].Trim().ToLowerInvariant();
        try
        {
            switch (command)
            {
                case "sync":
                {
                    var options = ParseSyncOptions(args.Skip(1).ToArray());
                    await _service.RunSyncAsync(options, Console.WriteLine).ConfigureAwait(false);
                    return 0;
                }
                case "export":
                {
                    var options = ParseExportOptions(args.Skip(1).ToArray());
                    var summary = await _service.RunExportAsync(options, Console.WriteLine).ConfigureAwait(false);
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }
                case "sync-export":
                {
                    var options = ParseSyncExportOptions(args.Skip(1).ToArray());
                    var summary = await _service.RunSyncAndExportAsync(options, Console.WriteLine).ConfigureAwait(false);
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }
                case "doctor":
                {
                    await _service.EnsurePythonReadyAsync(Console.WriteLine, bootstrap: true).ConfigureAwait(false);
                    Console.WriteLine("Python and UnityPy are ready.");
                    return 0;
                }
                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    PrintHelp();
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static bool IsHelp(string value) =>
        value is "--help" or "-h" or "/?" or "help";

    static void PrintHelp()
    {
        Console.WriteLine(
            """
            SeerAssetTool.Win

            Commands:
              sync
                --mirror-root <dir>
                [--install-root <dir>]
                [--no-all-heads]
                [--no-all-countermarks]

              export
                --source-root <dir>
                --output-root <dir>
                [--layout mirror|cache]
                [--limit <n>]
                [--skip-monsters]
                [--skip-heads]
                [--skip-countermarks]
                [--no-bootstrap-python]

              sync-export
                --mirror-root <dir>
                --output-root <dir>
                [--install-root <dir>]
                [--layout mirror|cache]
                [--limit <n>]
                [--skip-monsters]
                [--skip-heads]
                [--skip-countermarks]
                [--no-all-heads]
                [--no-all-countermarks]
                [--no-bootstrap-python]

              doctor
            """);
    }

    static SyncOptions ParseSyncOptions(IReadOnlyList<string> args)
    {
        var options = new SyncOptions();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--install-root":
                    options.InstallRoot = RequireValue(args, ref i, args[i]);
                    break;
                case "--mirror-root":
                    options.MirrorRoot = RequireValue(args, ref i, args[i]);
                    break;
                case "--no-all-heads":
                    options.AllHeads = false;
                    break;
                case "--no-all-countermarks":
                    options.AllCountermarks = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.MirrorRoot))
        {
            throw new ArgumentException("--mirror-root is required.");
        }

        return options;
    }

    static ExportOptions ParseExportOptions(IReadOnlyList<string> args)
    {
        var options = new ExportOptions();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--source-root":
                    options.SourceRoot = RequireValue(args, ref i, args[i]);
                    break;
                case "--output-root":
                    options.OutputRoot = RequireValue(args, ref i, args[i]);
                    break;
                case "--layout":
                    options.Layout = ParseLayout(RequireValue(args, ref i, args[i]));
                    break;
                case "--limit":
                    if (!int.TryParse(RequireValue(args, ref i, args[i]), out var limit) || limit < 1)
                    {
                        throw new ArgumentException("--limit must be a positive integer.");
                    }

                    options.Limit = limit;
                    break;
                case "--skip-monsters":
                    options.SkipMonsters = true;
                    break;
                case "--skip-heads":
                    options.SkipHeads = true;
                    break;
                case "--skip-countermarks":
                    options.SkipCountermarks = true;
                    break;
                case "--no-bootstrap-python":
                    options.BootstrapPython = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.SourceRoot))
        {
            throw new ArgumentException("--source-root is required.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputRoot))
        {
            throw new ArgumentException("--output-root is required.");
        }

        return options;
    }

    static SyncExportOptions ParseSyncExportOptions(IReadOnlyList<string> args)
    {
        var options = new SyncExportOptions();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--install-root":
                    options.InstallRoot = RequireValue(args, ref i, args[i]);
                    break;
                case "--mirror-root":
                    options.MirrorRoot = RequireValue(args, ref i, args[i]);
                    break;
                case "--output-root":
                    options.OutputRoot = RequireValue(args, ref i, args[i]);
                    break;
                case "--layout":
                    options.Layout = ParseLayout(RequireValue(args, ref i, args[i]));
                    break;
                case "--limit":
                    if (!int.TryParse(RequireValue(args, ref i, args[i]), out var limit) || limit < 1)
                    {
                        throw new ArgumentException("--limit must be a positive integer.");
                    }

                    options.Limit = limit;
                    break;
                case "--skip-monsters":
                    options.SkipMonsters = true;
                    break;
                case "--skip-heads":
                    options.SkipHeads = true;
                    break;
                case "--skip-countermarks":
                    options.SkipCountermarks = true;
                    break;
                case "--no-all-heads":
                    options.AllHeads = false;
                    break;
                case "--no-all-countermarks":
                    options.AllCountermarks = false;
                    break;
                case "--no-bootstrap-python":
                    options.BootstrapPython = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.MirrorRoot))
        {
            throw new ArgumentException("--mirror-root is required.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputRoot))
        {
            throw new ArgumentException("--output-root is required.");
        }

        return options;
    }

    static ExportLayout ParseLayout(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "cache" => ExportLayout.Cache,
            "mirror" => ExportLayout.Mirror,
            _ => throw new ArgumentException("--layout must be 'mirror' or 'cache'."),
        };

    static string RequireValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }
}
