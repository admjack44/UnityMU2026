namespace MUServer.Core.Models;

public sealed class Account
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsBanned { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginUtc { get; set; }
}
