namespace MUServer.Core.Configuration;

public class ServerConfig
{
    public DatabaseConfig Database { get; set; } = new();
}

public class DatabaseConfig
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string Database { get; set; } = "";
}