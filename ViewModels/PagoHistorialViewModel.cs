using Microsoft.AspNetCore.Mvc.Rendering;

namespace SysPres.ViewModels;

public class PagoHistorialViewModel
{
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public List<SelectListItem> Clientes { get; set; } = new();
    public List<PagoHistorialItemViewModel> Pagos { get; set; } = new();
}

public class PagoHistorialItemViewModel
{
    public int PagoId { get; set; }
    public DateTime FechaLocal { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public int PrestamoId { get; set; }
    public string AplicadoPor { get; set; } = string.Empty;
    public string TipoPago { get; set; } = "Normal";
    public string MetodoPago { get; set; } = string.Empty;
    public decimal TotalPagado { get; set; }
    public decimal BalancePendiente { get; set; }
    public decimal CapitalAbonado { get; set; }
    public decimal InteresAbonado { get; set; }
    public decimal MontoRecibido { get; set; }
    public decimal CambioDevuelto { get; set; }
    public string DetalleAplicacion { get; set; } = string.Empty;
}
