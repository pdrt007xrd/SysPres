using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class CompanySettings
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Nombre { get; set; } = "SYS PRES";

    [MaxLength(200)]
    public string? Direccion { get; set; }

    [MaxLength(50)]
    public string? Telefono { get; set; }

    [MaxLength(100)]
    public string? Ciudad { get; set; }
}
