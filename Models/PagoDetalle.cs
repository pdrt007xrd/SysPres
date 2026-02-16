using System.ComponentModel.DataAnnotations;

namespace SysPres.Models;

public class PagoDetalle
{
    public int Id { get; set; }

    [Required]
    public int PagoId { get; set; }

    [Required]
    public int PrestamoCuotaId { get; set; }

    public int NumeroCuota { get; set; }

    [MaxLength(20)]
    public string TipoAplicacion { get; set; } = "Cuota";

    public decimal MontoAplicado { get; set; }
    public decimal SaldoCuotaAnterior { get; set; }
    public decimal SaldoCuotaRestante { get; set; }

    public Pago? Pago { get; set; }
    public PrestamoCuota? PrestamoCuota { get; set; }
}
