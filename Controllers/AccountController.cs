using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SysPres.Services.Interfaces;
using SysPres.ViewModels;

namespace SysPres.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["LoginError"] = "Complete usuario y contraseña.";
            return RedirectToAction("Index", "Home");
        }

        var result = await _accountService.LoginAsync(model.UserName, model.Password);
        if (!result.Succeeded || result.User == null)
        {
            TempData["LoginError"] = result.ErrorMessage ?? "No fue posible iniciar sesión.";
            return RedirectToAction("Index", "Home");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, result.User.UserName),
            new(ClaimTypes.Role, result.User.Role ?? "User")
        };

        var identity = new ClaimsIdentity(claims, "SysPresCookie");
        var authProperties = new AuthenticationProperties { IsPersistent = true };

        await HttpContext.SignInAsync("SysPresCookie", new ClaimsPrincipal(identity), authProperties);
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _accountService.RegisterAsync(model.UserName, model.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.UserName), result.ErrorMessage ?? "No fue posible registrar el usuario.");
            return View(model);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("SysPresCookie");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        return View();
    }
}
