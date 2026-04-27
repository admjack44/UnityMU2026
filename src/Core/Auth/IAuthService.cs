namespace MUServer.Core.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}