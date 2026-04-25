namespace MUServer.Core.Configuration;

public sealed class DatabaseSettings
{
    public string Server { get; init; } =
        Environment.GetEnvironmentVariable("MU_DB_SERVER") ?? "localhost";

    public int Port { get; init; } =
        int.TryParse(Environment.GetEnvironmentVariable("MU_DB_PORT"), out var port)
            ? port
            : 3306;

    public string Database { get; init; } =
        Environment.GetEnvironmentVariable("MU_DB_NAME") ?? "muonline";

    public string User { get; init; } =
        Environment.GetEnvironmentVariable("MU_DB_USER") ?? "root";

    public string Password { get; init; } =
        Environment.GetEnvironmentVariable("MU_DB_PASSWORD")
        ?? throw new InvalidOperationException("MU_DB_PASSWORD is required.");

    public string ConnectionString =>
        $"Server={Server};Port={Port};Database={Database};User ID={User};Password={Password};Allow User Variables=true;";
}