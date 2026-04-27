using System.Security.Cryptography;

namespace MUServer.Core.Auth;

public sealed class DevAuthService : IAuthService
{
    private const string RequiredVersion = "0.1.0";

    public Task<AuthResult> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            return Task.FromResult(AuthResult.Fail("INVALID_REQUEST", "Solicitud inválida."));

        if (string.IsNullOrWhiteSpace(request.Username))
            return Task.FromResult(AuthResult.Fail("USERNAME_REQUIRED", "El usuario es obligatorio."));

        if (string.IsNullOrWhiteSpace(request.Password))
            return Task.FromResult(AuthResult.Fail("PASSWORD_REQUIRED", "La contraseña es obligatoria."));

        if (!string.Equals(request.Version, RequiredVersion, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthResult.Fail("INVALID_VERSION", $"Versión inválida. Requerida: {RequiredVersion}."));

        if (!string.Equals(request.Username, "test", StringComparison.OrdinalIgnoreCase) ||
            request.Password != "1234")
        {
            return Task.FromResult(AuthResult.Fail("INVALID_CREDENTIALS", "Usuario o contraseña incorrectos."));
        }

        string token = GenerateToken();

        return Task.FromResult(AuthResult.Ok(
            accountId: 1,
            username: request.Username,
            sessionToken: token
        ));
    }

    private static string GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}