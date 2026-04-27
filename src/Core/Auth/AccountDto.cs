namespace MUServer.Core.Auth;

public sealed record AccountDto(
    int AccountId,
    string Username,
    string PasswordHash,
    bool IsBanned
);