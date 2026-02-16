using SysPres.Models;

namespace SysPres.ViewModels;

public class DashboardViewModel
{
    public int PrestamosActivos { get; set; }
    public int PrestamosEnAtraso { get; set; }
    public decimal TotalAtrasado { get; set; }
    public decimal CapitalPrestado { get; set; }
    public decimal MontoGlobalCobrar { get; set; }
    public decimal InteresRecolectado { get; set; }
    public List<ActivityLog> ActividadReciente { get; set; } = new();
}
