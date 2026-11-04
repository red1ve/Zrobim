namespace Zrobim.Api.Infrastructure;

/// <summary>
/// Loads KEY=VALUE pairs from the nearest .env file (searching up from the current
/// directory) into environment variables. Variables that are already set win, so a
/// real environment always overrides the local file.
/// </summary>
public static class DotEnv
{
    public static void Load(string fileName = ".env")
    {
        var path = FindUpwards(Directory.GetCurrentDirectory(), fileName);
        if (path is null)
            return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindUpwards(string directory, string fileName)
    {
        for (var dir = new DirectoryInfo(directory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
