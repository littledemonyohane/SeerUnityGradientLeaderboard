namespace SeerAssetExporter;

internal sealed class ExportResult
{
    public bool Success { get; set; }
    public string InstallRoot { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? MonstersJsonPath { get; set; }
    public AssetExportStats Heads { get; set; } = new();
    public AssetExportStats Countermarks { get; set; } = new();
    public ProbeResult? Probe { get; set; }
    public List<string> Notes { get; set; } = [];

    public static ExportResult Failure(string error) => new()
    {
        Error = error,
        Success = false,
    };
}

internal sealed class AssetExportStats
{
    public int Indexed { get; set; }
    public int Attempted { get; set; }
    public int Exported { get; set; }
    public int Failed { get; set; }
    public string? OutputDirectory { get; set; }
    public List<string> SampleFailures { get; set; } = [];
}

internal sealed class ProbeResult
{
    public string? ManifestPath { get; set; }
    public int HeadAssetCount { get; set; }
    public int CountermarkAssetCount { get; set; }
    public List<TextureProbe> SampleTextures { get; set; } = [];
}

internal sealed class TextureProbe
{
    public string AssetPath { get; set; } = string.Empty;
    public string BundleHash { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int TextureFormat { get; set; }
    public int DataSize { get; set; }
    public bool Streamed { get; set; }
    public string? StreamPath { get; set; }
}
