using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SysPres.ViewModels;

public class PagoIndexViewModel
{
    [Required]
    public int? ClienteId { get; set; }

    [Required]
    public int? PrestamoId { get; set; }

    [Range(1, 999999999)]
    public decimal MontoAplicar { get; set; }

    [Required]
    public string TipoPago { get; set; } = "Normal";

    [Required]
    public string FormatoPdf { get; set; } = "A4";

    [Required]
    public string MetodoPago { get; set; } = "Efectivo";

    [Range(0, 999999999)]
    public decimal MontoRecibido { get; set; }

    public string? ClienteNombre { get; set; }
    public decimal InteresPendienteCiclo { get; set; }

    public List<SelectListItem> Clientes { get; set; } = new();
    public List<SelectListItem> Prestamos { get; set; } = new();
    public List<PagoCuotaPendienteViewModel> CuotasPendientes { get; set; } = new();
}

public class PagoCuotaPendienteViewModel
{
    public int Id { get; set; }
    public int NumeroCuota { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal MontoCuota { get; set; }
    public decimal MontoPagado { get; set; }
    public decimal SaldoPendiente => Math.Max(0, MontoCuota - MontoPagado);
    public string Estado { get; set; } = "Pendiente";
}
