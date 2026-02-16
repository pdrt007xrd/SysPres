using SysPres.Models;

namespace SysPres.Services.Results;

public sealed record LoginResult(bool Succeeded, string? ErrorMessage, ApplicationUser? User)
{
    public static LoginResult Success(ApplicationUser user) => new(true, null, user);
    public static LoginResult Failure(string message) => new(false, message, null);
}
