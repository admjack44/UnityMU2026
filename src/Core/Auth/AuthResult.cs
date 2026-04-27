namespace MUServer.Core.Auth;

public sealed record AuthResult
{
    public bool Success { get; init; }
    public int AccountId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string SessionToken { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public static AuthResult Ok(int accountId, string username, string sessionToken)
    {
        return new AuthResult
        {
            Success = true,
            AccountId = accountId,
            Username = username,
            SessionToken = sessionToken,
            Message = "Login correcto"
        };
    }

    public static AuthResult Fail(string errorCode, string message)
    {
        return new AuthResult
        {
            Success = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}