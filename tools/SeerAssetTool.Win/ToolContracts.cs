using System.Text.Json.Serialization;

namespace SeerAssetTool.Win;

public enum ExportLayout
{
    Mirror,
    Cache,
}

public sealed class SyncOptions
{
    public string InstallRoot { get; set; } = string.Empty;
    public string MirrorRoot { get; set; } = string.Empty;
    public bool AllHeads { get; set; } = true;
    public bool AllCountermarks { get; set; } = true;
}

public sealed class ExportOptions
{
    public string SourceRoot { get; set; } = string.Empty;
    public string OutputRoot { get; set; } = string.Empty;
    public ExportLayout Layout { get; set; } = ExportLayout.Cache;
    public int Limit { get; set; }
    public bool SkipMonsters { get; set; }
    public bool SkipHeads { get; set; }
    public bool SkipCountermarks { get; set; }
    public bool BootstrapPython { get; set; } = true;
}

public sealed class SyncExportOptions
{
    public string InstallRoot { get; set; } = string.Empty;
    public string MirrorRoot { get; set; } = string.Empty;
    public string OutputRoot { get; set; } = string.Empty;
    public ExportLayout Layout { get; set; } = ExportLayout.Cache;
    public int Limit { get; set; }
    public bool SkipMonsters { get; set; }
    public bool SkipHeads { get; set; }
    public bool SkipCountermarks { get; set; }
    public bool AllHeads { get; set; } = true;
    public bool AllCountermarks { get; set; } = true;
    public bool BootstrapPython { get; set; } = true;
}

public sealed class ConfigExportResult
{
    public bool Success { get; set; }
    public string SourceRoot { get; set; } = string.Empty;
    public string OutputRoot { get; set; } = string.Empty;
    public string Layout { get; set; } = string.Empty;
    public string ConfigVersion { get; set; } = string.Empty;
    public string DefaultManifestVersion { get; set; } = string.Empty;
    public int MonsterCount { get; set; }
    public string MonsterSource { get; set; } = string.Empty;
    public int PetSkinCount { get; set; }
    public string PetSkinSource { get; set; } = string.Empty;
    public string PetSkinError { get; set; } = string.Empty;
    public string MonstersJsonPath { get; set; } = string.Empty;
    public string PetSkinJsonPath { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public sealed class ImageExportCategoryResult
{
    public int Indexed { get; set; }
    public int Attempted { get; set; }
    public int Exported { get; set; }
    public int Failed { get; set; }
    public string OutputDirectory { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Layout { get; set; } = string.Empty;
    public List<string> SampleFailures { get; set; } = new();
}

public sealed class ImageExportResult
{
    public bool Success { get; set; }
    public string SourceRoot { get; set; } = string.Empty;
    public string OutputRoot { get; set; } = string.Empty;
    public string DefaultPackageVersion { get; set; } = string.Empty;
    public ImageExportCategoryResult Heads { get; set; } = new();
    public ImageExportCategoryResult Countermarks { get; set; } = new();
    public string Error { get; set; } = string.Empty;
}

public sealed class CombinedExportSummary
{
    public bool Success { get; set; }
    public string SourceRoot { get; set; } = string.Empty;
    public string OutputRoot { get; set; } = string.Empty;
    public string Layout { get; set; } = string.Empty;
    public string ConfigVersion { get; set; } = string.Empty;
    public string DefaultManifestVersion { get; set; } = string.Empty;
    public int MonsterCount { get; set; }
    public string MonsterSource { get; set; } = string.Empty;
    public int PetSkinCount { get; set; }
    public string PetSkinSource { get; set; } = string.Empty;
    public string PetSkinError { get; set; } = string.Empty;
    public int IndexedHeadCount { get; set; }
    public int ExportedHeadCount { get; set; }
    public int FailedHeadCount { get; set; }
    public int IndexedCountermarkCount { get; set; }
    public int ExportedCountermarkCount { get; set; }
    public int FailedCountermarkCount { get; set; }
    public int LimitPerCategory { get; set; }
    public string HeadExportSource { get; set; } = string.Empty;
    public string CountermarkExportSource { get; set; } = string.Empty;
    public string HeadOutputDirectory { get; set; } = string.Empty;
    public string CountermarkOutputDirectory { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public sealed record PythonCommand(string FileName, IReadOnlyList<string> PrefixArguments);
public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
