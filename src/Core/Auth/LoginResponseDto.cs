namespace MUServer.Core.Auth;

public sealed record LoginResponseDto
{
    public bool Success { get; init; }
    public int AccountId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string SessionToken { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public static LoginResponseDto Ok(int accountId, string username, string sessionToken)
    {
        return new LoginResponseDto
        {
            Success = true,
            AccountId = accountId,
            Username = username,
            SessionToken = sessionToken,
            Message = "Login correcto"
        };
    }

    public static LoginResponseDto Fail(string errorCode, string message)
    {
        return new LoginResponseDto
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}