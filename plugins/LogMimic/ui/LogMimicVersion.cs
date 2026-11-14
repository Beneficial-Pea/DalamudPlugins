namespace Oracle.Services;

internal static class LogMimicVersion
{
    public static string Display { get; } = ReadVersion();
    private static string ReadVersion()
    {
        var version = typeof(LogMimicVersion).Assembly.GetName().Version ?? new Version(0,0,0);
        return $"v{version.Major}.{version.Minor}.{Math.Max(0,version.Build)}";
    }
}
