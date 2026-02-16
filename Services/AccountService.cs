using Microsoft.EntityFrameworkCore;
using SysPres.Models;
using SysPres.Services.Interfaces;
using SysPres.Services.Results;

namespace SysPres.Services;

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _db;

    public AccountService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<LoginResult> LoginAsync(string userName, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        if (user == null)
        {
            return LoginResult.Failure("Usuario o contraseña incorrectos.");
        }

        var valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!valid)
        {
            return LoginResult.Failure("Usuario o contraseña incorrectos.");
        }

        return LoginResult.Success(user);
    }

    public async Task<RegisterResult> RegisterAsync(string userName, string password)
    {
        var exists = await _db.Users.AnyAsync(u => u.UserName == userName);
        if (exists)
        {
            return RegisterResult.Failure("Ese usuario ya existe.");
        }

        _db.Users.Add(new ApplicationUser
        {
            UserName = userName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "User"
        });

        await _db.SaveChangesAsync();
        return RegisterResult.Success();
    }
}
