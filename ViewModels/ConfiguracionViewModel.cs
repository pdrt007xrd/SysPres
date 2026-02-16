using System.ComponentModel.DataAnnotations;
using SysPres.Security;

namespace SysPres.ViewModels;

public class ConfiguracionViewModel
{
    public EmpresaFormViewModel Empresa { get; set; } = new();
    public NuevoEmpleadoViewModel NuevoEmpleado { get; set; } = new();
    public List<UsuarioPermisoViewModel> Usuarios { get; set; } = new();
    public string[] PermisosDisponibles { get; set; } = AppPermissions.All.Where(p => p != AppPermissions.Configuracion).ToArray();
}

public class EmpresaFormViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Nombre { get; set; } = "SYS PRES";

    [MaxLength(200)]
    public string? Direccion { get; set; }

    [MaxLength(50)]
    public string? Telefono { get; set; }

    [MaxLength(100)]
    public string? Ciudad { get; set; }
}

public class NuevoEmpleadoViewModel
{
    [Required, MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MinLength(4)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "La confirmación no coincide.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Empleado";

    public bool IsActive { get; set; } = true;
    public List<string> Permissions { get; set; } = new();
}

public class UsuarioPermisoViewModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public List<string> Permissions { get; set; } = new();
}
