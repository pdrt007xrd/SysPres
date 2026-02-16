using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class ActivityLog
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Usuario { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Accion { get; set; } = string.Empty;

    [Required]
    [MaxLength(60)]
    public string Entidad { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? EntidadReferencia { get; set; }

    [Required]
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(250)]
    public string? Detalle { get; set; }
}
