using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class PrestamoCuota
{
    public int Id { get; set; }

    [Required]
    public int PrestamoId { get; set; }

    public int NumeroCuota { get; set; }

    [DataType(DataType.Date)]
    public DateTime FechaVencimiento { get; set; }

    public decimal MontoCuota { get; set; }
    public decimal MontoPagado { get; set; }

    [MaxLength(20)]
    public string Estado { get; set; } = "Pendiente";

    public DateTime? FechaPago { get; set; }

    public Prestamo? Prestamo { get; set; }
}
