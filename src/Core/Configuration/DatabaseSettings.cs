namespace MUServer.Core.Configuration;

public sealed class DatabaseSettings
{
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "muonline";
    public string User { get; set; } = "root";
    public string Password { get; set; } = "MaxJhasper2024";

    public string ConnectionString =>
        $"Server={Server};Port={Port};Database={Database};User ID={User};Password={Password};Allow User Variables=true;";
}