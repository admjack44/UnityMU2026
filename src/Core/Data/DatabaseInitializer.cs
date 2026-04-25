using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace MUServer.Core.Data;

public sealed class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public DatabaseInitializer(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public void Initialize()
    {
        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS accounts
        (
            id INT NOT NULL AUTO_INCREMENT,
            username VARCHAR(32) NOT NULL,
            created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY (id),
            UNIQUE KEY uq_accounts_username (username)
        );

        CREATE TABLE IF NOT EXISTS characters
        (
            id INT NOT NULL AUTO_INCREMENT,
            account_id INT NOT NULL,
            name VARCHAR(16) NOT NULL,
            class TINYINT UNSIGNED NOT NULL,
            level SMALLINT UNSIGNED NOT NULL DEFAULT 1,
            experience INT UNSIGNED NOT NULL DEFAULT 0,
            map_id TINYINT UNSIGNED NOT NULL DEFAULT 0,
            pos_x TINYINT UNSIGNED NOT NULL DEFAULT 125,
            pos_y TINYINT UNSIGNED NOT NULL DEFAULT 125,
            created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY (id),
            UNIQUE KEY uq_characters_name (name),
            KEY ix_characters_account_id (account_id),
            CONSTRAINT fk_characters_account
                FOREIGN KEY (account_id) REFERENCES accounts(id)
                ON DELETE CASCADE
        );
        """;

        command.ExecuteNonQuery();

        _logger.LogInformation("Base de datos MySQL inicializada.");
    }
}