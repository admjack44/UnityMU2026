using MUServer.Core.Models;
using MySqlConnector;

namespace MUServer.Core.Repositories;

/// <summary>
/// Acceso a datos de personajes y cuentas asociadas.
/// </summary>
public sealed class CharacterRepository
{
    private const byte DefaultMapId = 0;
    private const byte DefaultPosX = 125;
    private const byte DefaultPosY = 125;
    private const ushort DefaultLevel = 1;
    private const uint DefaultExperience = 0;

    private readonly string _connectionString;

    public CharacterRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void EnsureAccount(string username)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO accounts (username)
        VALUES (@username)
        ON DUPLICATE KEY UPDATE username = username;
        """;
        command.Parameters.AddWithValue("@username", username.Trim());

        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Character> GetCharacters(string username)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            c.id,
            c.name,
            c.class,
            c.level,
            c.experience,
            c.map_id,
            c.pos_x,
            c.pos_y,
            c.strength,
            c.agility,
            c.vitality,
            c.energy,
            c.leadership,
            c.life,
            c.mana
        FROM characters c
        INNER JOIN accounts a ON a.id = c.account_id
        WHERE a.username = @username
        ORDER BY c.id ASC;
        """;
        command.Parameters.AddWithValue("@username", username.Trim());

        using var reader = command.ExecuteReader();

        var characters = new List<Character>();

        while (reader.Read())
        {
            ushort life = reader.GetFieldValue<ushort>(reader.GetOrdinal("life"));

            characters.Add(new Character
            {
                Id = reader.GetInt32("id"),
                Name = reader.GetString("name"),
                Class = reader.GetByte("class"),
                Level = reader.GetFieldValue<ushort>(reader.GetOrdinal("level")),
                Experience = reader.GetFieldValue<uint>(reader.GetOrdinal("experience")),
                MapId = reader.GetByte("map_id"),
                X = reader.GetByte("pos_x"),
                Y = reader.GetByte("pos_y"),
                Strength = reader.GetFieldValue<ushort>(reader.GetOrdinal("strength")),
                Agility = reader.GetFieldValue<ushort>(reader.GetOrdinal("agility")),
                Vitality = reader.GetFieldValue<ushort>(reader.GetOrdinal("vitality")),
                Energy = reader.GetFieldValue<ushort>(reader.GetOrdinal("energy")),
                Leadership = reader.GetFieldValue<ushort>(reader.GetOrdinal("leadership")),
                Life = life,
                Mana = reader.GetFieldValue<ushort>(reader.GetOrdinal("mana")),
                MaxLife = life,
                CurrentLife = life
            });
        }

        return characters;
    }

    public Character? CreateCharacter(string username, string name, byte classId)
    {
        using var connection = CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            int? accountId = GetAccountId(connection, transaction, username.Trim());
            if (!accountId.HasValue)
            {
                transaction.Rollback();
                return null;
            }

            var baseStats = GetBaseStats(classId);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            """
            INSERT INTO characters
            (
                account_id,
                name,
                class,
                level,
                experience,
                map_id,
                pos_x,
                pos_y,
                strength,
                agility,
                vitality,
                energy,
                leadership,
                life,
                mana
            )
            VALUES
            (
                @accountId,
                @name,
                @class,
                @level,
                @experience,
                @mapId,
                @posX,
                @posY,
                @strength,
                @agility,
                @vitality,
                @energy,
                @leadership,
                @life,
                @mana
            );
            """;

            command.Parameters.AddWithValue("@accountId", accountId.Value);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.Parameters.AddWithValue("@class", classId);
            command.Parameters.AddWithValue("@level", DefaultLevel);
            command.Parameters.AddWithValue("@experience", DefaultExperience);
            command.Parameters.AddWithValue("@mapId", DefaultMapId);
            command.Parameters.AddWithValue("@posX", DefaultPosX);
            command.Parameters.AddWithValue("@posY", DefaultPosY);
            command.Parameters.AddWithValue("@strength", baseStats.Strength);
            command.Parameters.AddWithValue("@agility", baseStats.Agility);
            command.Parameters.AddWithValue("@vitality", baseStats.Vitality);
            command.Parameters.AddWithValue("@energy", baseStats.Energy);
            command.Parameters.AddWithValue("@leadership", baseStats.Leadership);
            command.Parameters.AddWithValue("@life", baseStats.Life);
            command.Parameters.AddWithValue("@mana", baseStats.Mana);

            command.ExecuteNonQuery();

            int characterId = (int)command.LastInsertedId;

            transaction.Commit();

            return new Character
            {
                Id = characterId,
                Name = name.Trim(),
                Class = classId,
                Level = DefaultLevel,
                Experience = DefaultExperience,
                MapId = DefaultMapId,
                X = DefaultPosX,
                Y = DefaultPosY,
                Strength = baseStats.Strength,
                Agility = baseStats.Agility,
                Vitality = baseStats.Vitality,
                Energy = baseStats.Energy,
                Leadership = baseStats.Leadership,
                Life = baseStats.Life,
                Mana = baseStats.Mana,
                MaxLife = baseStats.Life,
                CurrentLife = baseStats.Life
            };
        }
        catch
        {
            transaction.Rollback();
            return null;
        }
    }

    public Character? GetCharacterBySlot(string username, int slot)
    {
        var characters = GetCharacters(username);

        if (slot < 0 || slot >= characters.Count)
        {
            return null;
        }

        return characters[slot];
    }

    public void UpdateCharacterPosition(int characterId, byte mapId, byte x, byte y)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE characters
        SET
            map_id = @mapId,
            pos_x = @posX,
            pos_y = @posY
        WHERE id = @characterId;
        """;

        command.Parameters.AddWithValue("@mapId", mapId);
        command.Parameters.AddWithValue("@posX", x);
        command.Parameters.AddWithValue("@posY", y);
        command.Parameters.AddWithValue("@characterId", characterId);

        command.ExecuteNonQuery();
    }

    public void UpdateCharacterExperience(int characterId, uint experience)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE characters
        SET experience = @experience
        WHERE id = @characterId;
        """;

        command.Parameters.AddWithValue("@experience", experience);
        command.Parameters.AddWithValue("@characterId", characterId);

        command.ExecuteNonQuery();
    }

    public void UpdateCharacterLevel(int characterId, ushort level)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE characters
        SET level = @level
        WHERE id = @characterId;
        """;

        command.Parameters.AddWithValue("@level", level);
        command.Parameters.AddWithValue("@characterId", characterId);

        command.ExecuteNonQuery();
    }

    private MySqlConnection CreateOpenConnection()
    {
        var connection = new MySqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static int? GetAccountId(MySqlConnection connection, MySqlTransaction transaction, string username)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
        """
        SELECT id
        FROM accounts
        WHERE username = @username
        LIMIT 1;
        """;
        command.Parameters.AddWithValue("@username", username);

        var result = command.ExecuteScalar();
        return result is null ? null : Convert.ToInt32(result);
    }

    private static Character GetBaseStats(byte classId)
    {
        return classId switch
        {
            0 => new Character
            {
                Strength = 18,
                Agility = 18,
                Vitality = 15,
                Energy = 30,
                Leadership = 0,
                Life = 60,
                Mana = 60
            },
            1 => new Character
            {
                Strength = 28,
                Agility = 20,
                Vitality = 25,
                Energy = 10,
                Leadership = 0,
                Life = 110,
                Mana = 20
            },
            2 => new Character
            {
                Strength = 22,
                Agility = 25,
                Vitality = 20,
                Energy = 15,
                Leadership = 0,
                Life = 80,
                Mana = 30
            },
            _ => new Character
            {
                Strength = 18,
                Agility = 18,
                Vitality = 18,
                Energy = 18,
                Leadership = 0,
                Life = 60,
                Mana = 60
            }
        };
    }

    public void UpdateCharacterProgress(Character character)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText =
        """
    UPDATE characters
    SET
        level = @level,
        experience = @experience,
        strength = @strength,
        agility = @agility,
        vitality = @vitality,
        energy = @energy,
        leadership = @leadership,
        life = @life,
        mana = @mana
    WHERE id = @characterId;
    """;

        command.Parameters.AddWithValue("@level", character.Level);
        command.Parameters.AddWithValue("@experience", character.Experience);
        command.Parameters.AddWithValue("@strength", character.Strength);
        command.Parameters.AddWithValue("@agility", character.Agility);
        command.Parameters.AddWithValue("@vitality", character.Vitality);
        command.Parameters.AddWithValue("@energy", character.Energy);
        command.Parameters.AddWithValue("@leadership", character.Leadership);
        command.Parameters.AddWithValue("@life", character.MaxLife);
        command.Parameters.AddWithValue("@mana", character.Mana);
        command.Parameters.AddWithValue("@characterId", character.Id);

        command.ExecuteNonQuery();
    }
}