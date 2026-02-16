using Microsoft.AspNetCore.Mvc.Rendering;

namespace SysPres.ViewModels;

public class ReporteResumenClientesViewModel
{
    public int? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public List<SelectListItem> Clientes { get; set; } = new();
    public List<ReporteResumenClienteItemViewModel> Items { get; set; } = new();
}

public class ReporteResumenClienteItemViewModel
{
    public int ClienteId { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Documento { get; set; } = "-";
    public int Prestamos { get; set; }
    public decimal CapitalPrestado { get; set; }
    public decimal InteresGenerado { get; set; }
    public decimal InteresCobrado { get; set; }
    public int PagosSoloInteres { get; set; }
    public decimal TotalAPagar { get; set; }
}
