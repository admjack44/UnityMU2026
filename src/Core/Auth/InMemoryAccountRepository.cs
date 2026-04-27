namespace MUServer.Core.Auth;

public sealed class InMemoryAccountRepository
{
    private readonly Dictionary<string, AccountDto> _accounts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["test"] = new AccountDto(
                AccountId: 1,
                Username: "test",
                PasswordHash: "1234",
                IsBanned: false
            ),

            ["admin"] = new AccountDto(
                AccountId: 2,
                Username: "admin",
                PasswordHash: "admin123",
                IsBanned: false
            )
        };

    public Task<AccountDto?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    )
    {
        username = username.Trim();

        _accounts.TryGetValue(username, out AccountDto? account);

        return Task.FromResult(account);
    }
}