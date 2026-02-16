using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPres.Models;
using SysPres.Security;
using SysPres.ViewModels;

namespace SysPres.Controllers;

[Authorize(Policy = "CanConfiguracion")]
public class ConfiguracionController : Controller
{
    private readonly ApplicationDbContext _context;

    public ConfiguracionController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildViewModelAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarEmpresa([Bind(Prefix = "Empresa")] EmpresaFormViewModel empresaForm)
    {
        if (!ModelState.IsValid)
        {
            SetToastError("No se pudo guardar la empresa. Revisa los campos requeridos.");
            var vm = await BuildViewModelAsync();
            vm.Empresa = empresaForm;
            return View("Index", vm);
        }

        var entity = await _context.CompanySettings.FirstOrDefaultAsync();
        if (entity == null)
        {
            entity = new CompanySettings();
            _context.CompanySettings.Add(entity);
        }

        entity.Nombre = empresaForm.Nombre;
        entity.Direccion = empresaForm.Direccion;
        entity.Telefono = empresaForm.Telefono;
        entity.Ciudad = empresaForm.Ciudad;

        await _context.SaveChangesAsync();
        SetToastSuccess("Datos de empresa actualizados.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearEmpleado([Bind(Prefix = "NuevoEmpleado")] NuevoEmpleadoViewModel newUser)
    {
        if (!ModelState.IsValid)
        {
            SetToastError("Debes completar correctamente los datos del nuevo usuario.");
            var vm = await BuildViewModelAsync();
            vm.NuevoEmpleado = newUser;
            return View("Index", vm);
        }

        var exists = await _context.Users.AnyAsync(u => u.UserName == newUser.UserName);
        if (exists)
        {
            ModelState.AddModelError(string.Empty, "Ese usuario ya existe.");
            SetToastWarning("Ese usuario ya existe.");
            var vm = await BuildViewModelAsync();
            vm.NuevoEmpleado = newUser;
            return View("Index", vm);
        }

        var permissions = NormalizePermissions(newUser.Permissions, newUser.Role);

        _context.Users.Add(new ApplicationUser
        {
            UserName = newUser.UserName.Trim(),
            FullName = newUser.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(newUser.Password),
            Role = newUser.Role,
            Permissions = string.Join(",", permissions),
            IsActive = newUser.IsActive
        });

        await _context.SaveChangesAsync();
        SetToastSuccess("Usuario creado correctamente.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPermisos(int userId, List<string> permissions, bool isActive = true)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            SetToastError("No se encontró el usuario a actualizar.");
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            SetToastWarning("El usuario Admin mantiene acceso total.");
            return RedirectToAction(nameof(Index));
        }

        var normalized = NormalizePermissions(permissions, user.Role);
        user.Permissions = string.Join(",", normalized);
        user.IsActive = isActive;
        await _context.SaveChangesAsync();

        SetToastSuccess("Permisos actualizados correctamente.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<ConfiguracionViewModel> BuildViewModelAsync(ConfiguracionViewModel? source = null)
    {
        var empresa = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync() ?? new CompanySettings();
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var vm = source ?? new ConfiguracionViewModel();
        vm.Empresa = new EmpresaFormViewModel
        {
            Id = empresa.Id,
            Nombre = string.IsNullOrWhiteSpace(source?.Empresa.Nombre) ? empresa.Nombre : source.Empresa.Nombre,
            Direccion = source?.Empresa.Direccion ?? empresa.Direccion,
            Telefono = source?.Empresa.Telefono ?? empresa.Telefono,
            Ciudad = source?.Empresa.Ciudad ?? empresa.Ciudad
        };

        vm.Usuarios = users.Select(u => new UsuarioPermisoViewModel
        {
            Id = u.Id,
            UserName = u.UserName,
            FullName = u.FullName,
            Role = u.Role ?? "User",
            IsActive = u.IsActive,
            Permissions = (u.Permissions ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        }).ToList();

        return vm;
    }

    private static List<string> NormalizePermissions(IEnumerable<string>? permissions, string? role)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return AppPermissions.All.ToList();
        }

        var requested = (permissions ?? [])
            .Where(p => AppPermissions.All.Contains(p, StringComparer.OrdinalIgnoreCase) && p != AppPermissions.Configuracion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!requested.Contains(AppPermissions.Dashboard, StringComparer.OrdinalIgnoreCase))
        {
            requested.Add(AppPermissions.Dashboard);
        }

        return requested;
    }

    private void SetToastSuccess(string message)
    {
        TempData["ToastMessage"] = message;
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Configuración";
    }

    private void SetToastWarning(string message)
    {
        TempData["ToastMessage"] = message;
        TempData["ToastType"] = "warning";
        TempData["ToastTitle"] = "Configuración";
    }

    private void SetToastError(string message)
    {
        TempData["ToastMessage"] = message;
        TempData["ToastType"] = "danger";
        TempData["ToastTitle"] = "Configuración";
    }
}
