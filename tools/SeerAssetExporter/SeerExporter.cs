namespace SeerAssetExporter;

internal sealed class SeerExporter
{
    private readonly ExportOptions _options;

    public SeerExporter(ExportOptions options)
    {
        _options = options;
    }

    public ExportResult Run()
    {
        var install = LocalInstallLayout.Resolve(_options.InstallRoot);
        if (!install.IsAvailable)
        {
            return ExportResult.Failure(
                $"Unable to locate a valid NewSeer install. Checked: {string.Join(", ", LocalInstallLayout.CandidateInstalls)}");
        }

        return _options.Mode == "probe"
            ? Probe(install)
            : Export(install);
    }

    private ExportResult Probe(LocalInstallLayout install)
    {
        return new ExportResult
        {
            Success = true,
            InstallRoot = install.InstallRoot,
            OutputDirectory = Path.GetFullPath(_options.OutputDirectory),
            Probe = AssetProbe.Run(install),
        };
    }

    private ExportResult Export(LocalInstallLayout install)
    {
        var outputDirectory = Path.GetFullPath(_options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var exporter = new ExportPipeline(install, outputDirectory, _options);
        return exporter.Run();
    }
}
