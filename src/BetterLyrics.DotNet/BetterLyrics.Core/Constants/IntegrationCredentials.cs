namespace BetterLyrics.Core.Constants;

internal static class IntegrationCredentials
{
    public static string? Get(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value) && OperatingSystem.IsWindows())
            value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
