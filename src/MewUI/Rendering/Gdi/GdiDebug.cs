namespace Aprillz.MewUI.Rendering.Gdi;

[Flags]
public enum GdiDumpStages
{
    None = 0,
    AaSurface = 1 << 0,
    AaSurfaceAlpha = 1 << 1,
    DestBeforeBlend = 1 << 2,
    DestAfterBlend = 1 << 3,
}

public static class GdiDebug
{
    public static string? DumpAaDirectory { get; set; }

    public static int DumpAaMaxFiles { get; set; } = 50;

    public static GdiDumpStages DumpStages { get; set; } = GdiDumpStages.None;

    private static int _dumpAaCount;

    internal static bool TryGetNextAaDumpIndex(out int index)
    {
        index = 0;

        if (string.IsNullOrWhiteSpace(DumpAaDirectory))
            return false;

        if (_dumpAaCount >= DumpAaMaxFiles)
            return false;

        index = _dumpAaCount++;
        return true;
    }
}
