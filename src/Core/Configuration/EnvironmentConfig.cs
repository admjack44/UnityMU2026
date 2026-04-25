namespace MUServer.Core.Configuration;

public static class EnvironmentConfig
{
    public static string GetRequired(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required environment variable: {key}");

        return value;
    }

    public static string GetOptional(string key, string defaultValue)
    {
        return Environment.GetEnvironmentVariable(key) ?? defaultValue;
    }

    public static int GetOptionalInt(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}