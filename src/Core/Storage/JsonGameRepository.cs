using System.Text.Json;
using MUServer.Core.Models;

namespace MUServer.Core.Storage;

public sealed class JsonGameRepository
{
    private readonly string _dataPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public JsonGameRepository(string dataPath = "data")
    {
        _dataPath = dataPath;
        Directory.CreateDirectory(_dataPath);
        Directory.CreateDirectory(Path.Combine(_dataPath, "accounts"));
        Directory.CreateDirectory(Path.Combine(_dataPath, "characters"));
    }

    public Account? GetAccount(string username)
    {
        var path = GetAccountPath(username);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Account>(File.ReadAllText(path), _jsonOptions);
    }

    public void SaveAccount(Account account)
    {
        File.WriteAllText(GetAccountPath(account.Username), JsonSerializer.Serialize(account, _jsonOptions));
    }

    public List<Character> GetCharacters(string username)
    {
        var path = GetCharactersPath(username);
        if (!File.Exists(path)) return new List<Character>();
        return JsonSerializer.Deserialize<List<Character>>(File.ReadAllText(path), _jsonOptions) ?? new List<Character>();
    }

    public void SaveCharacters(string username, List<Character> characters)
    {
        File.WriteAllText(GetCharactersPath(username), JsonSerializer.Serialize(characters, _jsonOptions));
    }

    private string GetAccountPath(string username) => Path.Combine(_dataPath, "accounts", Safe(username) + ".json");
    private string GetCharactersPath(string username) => Path.Combine(_dataPath, "characters", Safe(username) + ".json");
    private static string Safe(string value) => value.Trim().ToLowerInvariant().Replace("/", "_").Replace("\\", "_");
}
