using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPres.Models;
using SysPres.Security;
using SysPres.Services.Interfaces;
using SysPres.ViewModels;

namespace SysPres.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly IAccountService _accountService;
    private readonly ApplicationDbContext _context;

    public AccountController(IAccountService accountService, ApplicationDbContext context)
    {
        _accountService = accountService;
        _context = context;
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

        var isAdmin = string.Equals(result.User.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        var permissions = isAdmin
            ? AppPermissions.All
            : (result.User.Permissions ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

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
    [Authorize(Policy = "CanDashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var prestamos = await _context.Prestamos.AsNoTracking().ToListAsync();
        var prestamosActivos = prestamos.Where(p => p.Estado == "Activo").ToList();
        var hoy = DateTime.Today;

        var cuotasAtrasadas = await _context.PrestamoCuotas
            .AsNoTracking()
            .Where(c => c.Estado != "Pagado" && c.FechaVencimiento < hoy)
            .ToListAsync();

        var prestamosEnAtraso = cuotasAtrasadas
            .Select(c => c.PrestamoId)
            .Distinct()
            .Count();
        var totalAtrasado = cuotasAtrasadas.Sum(c => Math.Max(0, c.MontoCuota - c.MontoPagado));

        var capitalPrestado = prestamosActivos.Sum(p => p.Monto);
        var montoGlobalCobrar = prestamosActivos.Sum(p => p.SaldoPendiente);
        var interesRecolectado = await _context.Pagos
            .AsNoTracking()
            .Include(p => p.Prestamo)
            .SumAsync(p => p.Prestamo != null && p.Prestamo.TotalAPagar > 0
                ? p.TotalPagado * (p.Prestamo.MontoInteres / p.Prestamo.TotalAPagar)
                : 0m);

        var actividad = await _context.ActivityLogs
            .AsNoTracking()
            .OrderByDescending(a => a.FechaUtc)
            .Take(10)
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            PrestamosActivos = prestamosActivos.Count,
            PrestamosEnAtraso = prestamosEnAtraso,
            TotalAtrasado = totalAtrasado,
            CapitalPrestado = capitalPrestado,
            InteresRecolectado = Math.Round(interesRecolectado, 2),
            MontoGlobalCobrar = montoGlobalCobrar,
            ActividadReciente = actividad
        };

        return View(vm);
    }
}
