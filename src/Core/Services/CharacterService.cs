using MUServer.Core.Models;
using MUServer.Core.Repositories;

namespace MUServer.Core.Services;

/// <summary>
/// Orquesta reglas de negocio relacionadas con personajes:
/// creación, selección, progresión y persistencia de estado.
/// </summary>
public sealed class CharacterService
{
    private const int MaxCharactersPerAccount = 5;
    private const ushort LevelUpBaseMultiplier = 100;

    private readonly CharacterRepository _repository;

    public CharacterService(CharacterRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<Character> GetCharacters(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return Array.Empty<Character>();
        }

        return _repository.GetCharacters(accountName.Trim());
    }

    public Character? CreateCharacter(string accountName, string characterName, byte classId)
    {
        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(characterName))
        {
            return null;
        }

        string normalizedAccountName = accountName.Trim();
        string normalizedCharacterName = characterName.Trim();

        var characters = _repository.GetCharacters(normalizedAccountName);

        if (characters.Count >= MaxCharactersPerAccount)
        {
            return null;
        }

        bool duplicatedName = characters.Any(c =>
            c.Name.Equals(normalizedCharacterName, StringComparison.OrdinalIgnoreCase));

        if (duplicatedName)
        {
            return null;
        }

        return _repository.CreateCharacter(normalizedAccountName, normalizedCharacterName, classId);
    }

    public Character? GetCharacterBySlot(string accountName, int slot)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return null;
        }

        return _repository.GetCharacterBySlot(accountName.Trim(), slot);
    }

    public void EnsureSeedData(string accountName)
    {
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        string normalizedAccountName = accountName.Trim();

        _repository.EnsureAccount(normalizedAccountName);

        var characters = _repository.GetCharacters(normalizedAccountName);
        if (characters.Count > 0)
        {
            return;
        }

        _repository.CreateCharacter(normalizedAccountName, "TestPlayer", 0);
    }

    public void UpdateCharacterPosition(int characterId, byte mapId, byte x, byte y)
    {
        _repository.UpdateCharacterPosition(characterId, mapId, x, y);
    }

    public void UpdateCharacterExperience(int characterId, uint experience)
    {
        _repository.UpdateCharacterExperience(characterId, experience);
    }

    public bool TryLevelUp(Character character)
    {
        if (character is null)
        {
            return false;
        }

        uint requiredExperience = GetRequiredExperienceForNextLevel(character.Level);

        if (character.Experience < requiredExperience)
        {
            return false;
        }

        character.Experience -= requiredExperience;
        character.Level++;

        ApplyLevelUpStatGrowth(character);

        _repository.UpdateCharacterProgress(character);

        return true;
    }

    public uint GetRequiredExperienceForNextLevel(ushort currentLevel)
    {
        return (uint)(currentLevel * LevelUpBaseMultiplier);
    }

    private static void ApplyLevelUpStatGrowth(Character character)
    {
        character.Strength += 5;
        character.Agility += 5;
        character.Vitality += 5;
        character.Energy += 5;

        // Recalcular vida máxima y restaurar vida al subir nivel.
        character.MaxLife += 20;
        character.CurrentLife = character.MaxLife;
    }

    public void UpdateCharacterProgress(Character character)
    {
        if (character is null)
        {
            return;
        }

        _repository.UpdateCharacterProgress(character);
    }
}