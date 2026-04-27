namespace MUServer.Core.Auth;

public sealed record LoginRequestDto
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Version { get; init; } = "0.1.0";
    public string DeviceId { get; init; } = string.Empty;
    public string Platform { get; init; } = "Unity";
}