namespace SysPres.Services.Results;

public sealed record RegisterResult(bool Succeeded, string? ErrorMessage)
{
    public static RegisterResult Success() => new(true, null);
    public static RegisterResult Failure(string message) => new(false, message);
}
