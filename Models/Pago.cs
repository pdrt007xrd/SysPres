using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class Pago
{
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    [Required]
    public int PrestamoId { get; set; }

    public DateTime FechaPagoUtc { get; set; } = DateTime.UtcNow;

    public decimal TotalPagado { get; set; }
    public decimal BalancePendiente { get; set; }
    public decimal CapitalAbonado { get; set; }
    public decimal InteresAbonado { get; set; }
    public decimal MontoRecibido { get; set; }
    public decimal CambioDevuelto { get; set; }

    [MaxLength(30)]
    public string TipoPago { get; set; } = "Normal";

    [MaxLength(20)]
    public string MetodoPago { get; set; } = "Efectivo";

    [MaxLength(20)]
    public string FormatoComprobante { get; set; } = "A4";

    [MaxLength(100)]
    public string Usuario { get; set; } = "Sistema";

    public Cliente? Cliente { get; set; }
    public Prestamo? Prestamo { get; set; }
    public List<PagoDetalle> Detalles { get; set; } = new();
}
