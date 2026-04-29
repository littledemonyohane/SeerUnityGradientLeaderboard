namespace SeerAssetExporter;

internal static class AssetProbe
{
    public static ProbeResult Run(LocalInstallLayout install)
    {
        return new ProbeResult
        {
            ManifestPath = install.DefaultManifestBytesPath,
        };
    }
}
