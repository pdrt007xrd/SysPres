using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El documento es obligatorio.")]
    [RegularExpression(@"^\d{3}-\d{7}-\d$", ErrorMessage = "La cédula debe tener formato 001-1811925-4.")]
    [MaxLength(30)]
    public string Documento { get; set; } = string.Empty;

    [RegularExpression(@"^$|^\d{3}-\d{3}-\d{4}$", ErrorMessage = "El teléfono debe tener formato 809-808-8888.")]
    [MaxLength(30)]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(120)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? Direccion { get; set; }

    [MaxLength(120)]
    public string? Empresa { get; set; }

    [MaxLength(100)]
    public string? Puesto { get; set; }

    [Range(0, 999999999, ErrorMessage = "El ingreso mensual no es válido.")]
    public decimal? IngresoMensual { get; set; }

    [Range(0, 1200, ErrorMessage = "Los meses laborando no son válidos.")]
    public int? MesesLaborando { get; set; }

    public bool TieneGarante { get; set; }

    [MaxLength(120)]
    public string? GaranteNombre { get; set; }

    [RegularExpression(@"^$|^\d{3}-\d{7}-\d$", ErrorMessage = "La cédula del garante debe tener formato 001-1811925-4.")]
    [MaxLength(30)]
    public string? GaranteDocumento { get; set; }

    [RegularExpression(@"^$|^\d{3}-\d{3}-\d{4}$", ErrorMessage = "El teléfono del garante debe tener formato 809-808-8888.")]
    [MaxLength(30)]
    public string? GaranteTelefono { get; set; }

    [MaxLength(200)]
    public string? GaranteDireccion { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public List<Prestamo> Prestamos { get; set; } = new();
}
