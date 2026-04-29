namespace SeerAssetExporter;

internal sealed class ExportOptions
{
    public string InstallRoot { get; private set; } = string.Empty;
    public string OutputDirectory { get; private set; } = string.Empty;
    public string Mode { get; private set; } = "export";
    public int? Limit { get; private set; }
    public bool SkipMonsters { get; private set; }
    public bool SkipHeads { get; private set; }
    public bool SkipCountermarks { get; private set; }
    public bool Verbose { get; private set; }
    public bool ShowHelp { get; private set; }

    public static ExportOptions Parse(IReadOnlyList<string> args)
    {
        var options = new ExportOptions();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                case "/?":
                    options.ShowHelp = true;
                    break;
                case "--install-root":
                    options.InstallRoot = RequireValue(args, ref i, arg);
                    break;
                case "--output":
                    options.OutputDirectory = RequireValue(args, ref i, arg);
                    break;
                case "--mode":
                    options.Mode = RequireValue(args, ref i, arg).Trim().ToLowerInvariant();
                    break;
                case "--limit":
                    if (!int.TryParse(RequireValue(args, ref i, arg), out var limit) || limit < 1)
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
                case "--verbose":
                    options.Verbose = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (!options.ShowHelp && string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new ArgumentException("--output is required.");
        }

        if (options.Mode is not ("export" or "probe"))
        {
            throw new ArgumentException("--mode must be 'export' or 'probe'.");
        }

        return options;
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }
}
