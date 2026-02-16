using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SysPres.ViewModels;

public class PrestamoCreateViewModel
{
    [Required]
    public int ClienteId { get; set; }

    [Range(1, 999999999)]
    public decimal Monto { get; set; }

    [Range(0, 100)]
    public decimal TasaInteresAnual { get; set; }

    [Range(1, 1000)]
    public int NumeroPagos { get; set; } = 4;

    [Required]
    public string FrecuenciaPago { get; set; } = "Semanal";

    [DataType(DataType.Date)]
    public DateTime FechaInicio { get; set; } = DateTime.Today;

    [MaxLength(250)]
    public string? Observaciones { get; set; }

    public List<SelectListItem> Clientes { get; set; } = new();

    public decimal MontoInteres { get; set; }
    public decimal TotalAPagar { get; set; }
    public decimal ValorCuota { get; set; }
}
