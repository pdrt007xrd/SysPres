using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class Prestamo
{
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    [Range(1, 999999999, ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    [Range(0, 100, ErrorMessage = "La tasa debe estar entre 0 y 100.")]
    public decimal TasaInteresAnual { get; set; }

    [Range(1, 1000, ErrorMessage = "La cantidad de pagos debe ser mayor a 0.")]
    public int NumeroPagos { get; set; }

    [Required]
    [MaxLength(20)]
    public string FrecuenciaPago { get; set; } = "Mensual";

    [DataType(DataType.Date)]
    public DateTime FechaInicio { get; set; } = DateTime.Today;

    public decimal MontoInteres { get; set; }
    public decimal TotalAPagar { get; set; }
    public decimal ValorCuota { get; set; }
    public decimal SaldoPendiente { get; set; }

    [MaxLength(30)]
    public string Estado { get; set; } = "Activo";

    [MaxLength(250)]
    public string? Observaciones { get; set; }

    public Cliente? Cliente { get; set; }
    public List<PrestamoCuota> Cuotas { get; set; } = new();
}
