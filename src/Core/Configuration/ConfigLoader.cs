using System.Text.Json;

namespace MUServer.Core.Configuration;

public static class ConfigLoader
{
    public static ServerConfig Load(string path = "config/server.json")
    {
        if (!File.Exists(path))
            throw new Exception($"Config file not found: {path}");

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<ServerConfig>(json);

        if (config == null)
            throw new Exception("Invalid config file");

        return config;
    }
}