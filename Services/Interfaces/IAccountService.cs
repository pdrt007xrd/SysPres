using SysPres.Services.Results;

namespace SysPres.Services.Interfaces;

public interface IAccountService
{
    Task<LoginResult> LoginAsync(string userName, string password);
    Task<RegisterResult> RegisterAsync(string userName, string password);
}
