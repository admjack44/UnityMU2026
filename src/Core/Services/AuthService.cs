using System.Security.Cryptography;
using System.Text;
using MUServer.Core.Models;
using MUServer.Core.Storage;

namespace MUServer.Core.Services;

public sealed class AuthService
{
    private readonly JsonGameRepository _repository;

    public AuthService(JsonGameRepository repository)
    {
        _repository = repository;
        EnsureDefaultAdmin();
    }

    public Account? Login(string username, string password)
    {
        var account = _repository.GetAccount(username);
        if (account is null || account.IsBanned)
            return null;

        if (!FixedTimeEquals(account.PasswordHash, HashPassword(password)))
            return null;

        account.LastLoginUtc = DateTime.UtcNow;
        _repository.SaveAccount(account);
        return account;
    }

    public Account Register(string username, string password)
    {
        username = Normalize(username);
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username invalido.", nameof(username));
        if (password.Length < 6)
            throw new ArgumentException("Password minimo 6 caracteres.", nameof(password));
        if (_repository.GetAccount(username) is not null)
            throw new InvalidOperationException("La cuenta ya existe.");

        var account = new Account
        {
            Username = username,
            PasswordHash = HashPassword(password),
            CreatedAtUtc = DateTime.UtcNow
        };

        _repository.SaveAccount(account);
        return account;
    }

    private void EnsureDefaultAdmin()
    {
        if (_repository.GetAccount("admin") is null)
            Register("admin", "admin123");
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("UnityMU2026:" + password));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
