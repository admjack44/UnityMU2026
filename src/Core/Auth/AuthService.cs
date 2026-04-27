using System.Security.Cryptography;

namespace MUServer.Core.Auth;

public sealed class AuthService
{
    private readonly InMemoryAccountRepository _accounts;
    private const string RequiredVersion = "0.1.0";

    public AuthService(InMemoryAccountRepository accounts)
    {
        _accounts = accounts;
    }

    public async Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
            return LoginResponseDto.Fail("INVALID_REQUEST", "Solicitud inválida.");

        string username = request.Username.Trim();
        string password = request.Password.Trim();
        string version = request.Version.Trim();

        if (string.IsNullOrWhiteSpace(username))
            return LoginResponseDto.Fail("USERNAME_REQUIRED", "El usuario es obligatorio.");

        if (string.IsNullOrWhiteSpace(password))
            return LoginResponseDto.Fail("PASSWORD_REQUIRED", "La contraseña es obligatoria.");

        if (!string.Equals(version, RequiredVersion, StringComparison.OrdinalIgnoreCase))
            return LoginResponseDto.Fail("INVALID_VERSION", $"Versión inválida. Requerida: {RequiredVersion}.");

        AccountDto? account = await _accounts.FindByUsernameAsync(
            username,
            cancellationToken
        );

        if (account is null)
            return LoginResponseDto.Fail("INVALID_CREDENTIALS", "Usuario o contraseña incorrectos.");

        if (account.IsBanned)
            return LoginResponseDto.Fail("ACCOUNT_BANNED", "La cuenta está bloqueada.");

        if (account.PasswordHash != password)
            return LoginResponseDto.Fail("INVALID_CREDENTIALS", "Usuario o contraseña incorrectos.");

        string token = GenerateSessionToken();

        return LoginResponseDto.Ok(
            account.AccountId,
            account.Username,
            token
        );
    }

    private static string GenerateSessionToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}