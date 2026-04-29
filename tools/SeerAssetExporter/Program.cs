using System.Text.Json;

namespace SeerAssetExporter;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static int Main(string[] args)
    {
        try
        {
            var options = ExportOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var exporter = new SeerExporter(options);
            var result = exporter.Run();

            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return result.Success ? 0 : 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintHelp();
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Usage:
              SeerAssetExporter --output <dir> [--install-root <dir>] [--mode export|probe]

            Options:
              --install-root <dir>   Local NewSeer install root. If omitted, auto-detect common paths.
              --output <dir>         Output directory for exported assets.
              --mode <name>          export (default) or probe.
              --limit <n>            Optional image export limit per category for smoke testing.
              --skip-monsters        Skip monsters.json export.
              --skip-heads           Skip pet/head export.
              --skip-countermarks    Skip countermark/icon export.
              --verbose              Print extra diagnostics.
              --help                 Show this help.
            """);
    }
}
