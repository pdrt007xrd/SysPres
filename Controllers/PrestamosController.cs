using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SysPres.Models;
using SysPres.ViewModels;

namespace SysPres.Controllers;

[Authorize(Policy = "CanPrestamos")]
public class PrestamosController : Controller
{
    private readonly ApplicationDbContext _context;

    public PrestamosController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = new PrestamoIndexViewModel
        {
            Prestamos = await _context.Prestamos
                .Include(p => p.Cliente)
                .Include(p => p.Cuotas)
                .OrderByDescending(p => p.Id)
                .ToListAsync()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ReportePdf()
    {
        var prestamos = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        var pagosPorPrestamo = await _context.Pagos
            .AsNoTracking()
            .GroupBy(p => p.PrestamoId)
            .Select(g => new
            {
                PrestamoId = g.Key,
                InteresCobrado = g.Sum(x => x.InteresAbonado),
                PagosSoloInteres = g.Count(x => x.TipoPago == "SoloInteres")
            })
            .ToDictionaryAsync(x => x.PrestamoId, x => new { x.InteresCobrado, x.PagosSoloInteres });

        var fechaGeneracion = DateTime.Now;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(header =>
                {
                    header.Item().Text("Reporte de Préstamos").FontSize(18).Bold();
                    header.Item().Text($"Generado: {fechaGeneracion:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(12).Column(column =>
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40); // ID
                            columns.ConstantColumn(125); // Cliente
                            columns.ConstantColumn(72); // Monto
                            columns.ConstantColumn(54); // Tasa
                            columns.ConstantColumn(78); // Frecuencia
                            columns.ConstantColumn(78); // Interes cobrado
                            columns.ConstantColumn(78); // Saldo
                            columns.ConstantColumn(58); // Solo interes
                            columns.ConstantColumn(60); // Estado
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("ID");
                            header.Cell().Element(CellHeader).Text("Cliente");
                            header.Cell().Element(CellHeader).AlignRight().Text("Monto");
                            header.Cell().Element(CellHeader).AlignRight().Text("Tasa");
                            header.Cell().Element(CellHeader).Text("Frecuencia");
                            header.Cell().Element(CellHeader).AlignRight().Text("Int. cobrado");
                            header.Cell().Element(CellHeader).AlignRight().Text("Saldo");
                            header.Cell().Element(CellHeader).AlignRight().Text("Solo interés");
                            header.Cell().Element(CellHeader).Text("Estado");
                        });

                        foreach (var prestamo in prestamos)
                        {
                            var frecuencia = $"{prestamo.FrecuenciaPago} ({prestamo.NumeroPagos})";
                            var pagosData = pagosPorPrestamo.TryGetValue(prestamo.Id, out var value) ? value : null;

                            table.Cell().Element(CellBody).Text($"#{prestamo.Id}");
                            table.Cell().Element(CellBody).Text(Limit(prestamo.Cliente?.Nombre ?? "-", 24));
                            table.Cell().Element(CellBody).AlignRight().Text(prestamo.Monto.ToString("C0"));
                            table.Cell().Element(CellBody).AlignRight().Text($"{prestamo.TasaInteresAnual:0.##}%");
                            table.Cell().Element(CellBody).Text(Limit(frecuencia, 16));
                            table.Cell().Element(CellBody).AlignRight().Text((pagosData?.InteresCobrado ?? 0m).ToString("C0"));
                            table.Cell().Element(CellBody).AlignRight().Text(prestamo.SaldoPendiente.ToString("C0"));
                            table.Cell().Element(CellBody).AlignRight().Text((pagosData?.PagosSoloInteres ?? 0).ToString());
                            table.Cell().Element(CellBody).Text(prestamo.Estado);
                        }
                    });

                    column.Item().PaddingTop(12).Text($"Total de préstamos: {prestamos.Count}").Bold();
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();

        Response.Headers.ContentDisposition = "inline; filename=reporte-prestamos.pdf";
        return File(pdfBytes, "application/pdf");

        static IContainer CellHeader(IContainer container) => container
            .Background(Colors.Grey.Lighten3)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(4)
            .PaddingHorizontal(4)
            .DefaultTextStyle(x => x.SemiBold());

        static IContainer CellBody(IContainer container) => container
            .BorderBottom(1)
            .BorderLeft(1)
            .BorderRight(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .PaddingHorizontal(4);

        static string Limit(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Length <= max ? value : value[..max];
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new PrestamoCreateViewModel();
        await LoadClientes(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PrestamoCreateViewModel model)
    {
        await LoadClientes(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var cliente = await _context.Clientes.FindAsync(model.ClienteId);
        if (cliente == null)
        {
            ModelState.AddModelError(nameof(model.ClienteId), "Debe seleccionar un cliente válido.");
            return View(model);
        }

        var montoInteres = CalcularInteresPorTasaMensual(model.Monto, model.TasaInteresAnual, model.NumeroPagos, model.FrecuenciaPago);
        var totalPagar = Math.Round(model.Monto + montoInteres, 0);
        var valorCuota = Math.Round(totalPagar / model.NumeroPagos, 0);

        var prestamo = new Prestamo
        {
            ClienteId = model.ClienteId,
            Monto = model.Monto,
            TasaInteresAnual = model.TasaInteresAnual,
            NumeroPagos = model.NumeroPagos,
            FrecuenciaPago = model.FrecuenciaPago,
            FechaInicio = model.FechaInicio,
            Observaciones = model.Observaciones,
            MontoInteres = montoInteres,
            TotalAPagar = totalPagar,
            ValorCuota = valorCuota,
            SaldoPendiente = totalPagar,
            Estado = "Activo"
        };

        _context.Prestamos.Add(prestamo);
        await _context.SaveChangesAsync();

        var cuotas = BuildCuotas(prestamo);
        _context.PrestamoCuotas.AddRange(cuotas);
        await _context.SaveChangesAsync();

        await RegistrarActividadAsync("Creó", "Préstamo", $"Prestamo #{prestamo.Id}", $"Cliente {cliente.Nombre}, monto {prestamo.Monto:C0}");
        SetToast("Préstamo creado correctamente.", "success", "Éxito");

        return RedirectToAction(nameof(Details), new { id = prestamo.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas.OrderBy(c => c.NumeroCuota))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
        {
            return NotFound();
        }

        return View(prestamo);
    }

    [HttpGet]
    public async Task<IActionResult> CronogramaPdf(int id)
    {
        var prestamo = await _context.Prestamos
            .AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas.OrderBy(c => c.NumeroCuota))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
        {
            return NotFound();
        }

        var generado = DateTime.Now;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(header =>
                {
                    header.Item().Text($"Cronograma de Préstamo #{prestamo.Id}").FontSize(18).Bold();
                    header.Item().Text($"Cliente: {prestamo.Cliente?.Nombre ?? "-"}");
                    header.Item().Text($"Frecuencia: {prestamo.FrecuenciaPago} | Pagos: {prestamo.NumeroPagos}");
                    header.Item().Text($"Generado: {generado:dd/MM/yyyy HH:mm}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(44);
                        c.ConstantColumn(64);
                        c.ConstantColumn(120);
                        c.ConstantColumn(100);
                        c.RelativeColumn(1);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(CellHeader).AlignCenter().Text("✓");
                        h.Cell().Element(CellHeader).Text("Cuota");
                        h.Cell().Element(CellHeader).Text("Vencimiento");
                        h.Cell().Element(CellHeader).AlignRight().Text("Monto");
                        h.Cell().Element(CellHeader).Text("Estado");
                    });

                    foreach (var cuota in prestamo.Cuotas.OrderBy(c => c.NumeroCuota))
                    {
                        table.Cell().Element(CellBody).AlignCenter().Text("□");
                        table.Cell().Element(CellBody).Text(cuota.NumeroCuota.ToString());
                        table.Cell().Element(CellBody).Text(cuota.FechaVencimiento.ToString("dd/MM/yyyy"));
                        table.Cell().Element(CellBody).AlignRight().Text(cuota.MontoCuota.ToString("C0"));
                        table.Cell().Element(CellBody).Text(cuota.Estado);
                    }
                });
            });
        }).GeneratePdf();

        Response.Headers.ContentDisposition = $"inline; filename=cronograma-prestamo-{prestamo.Id}.pdf";
        return File(pdf, "application/pdf");

        static IContainer CellHeader(IContainer c) => c
            .Background(Colors.Grey.Lighten3)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten1)
            .PaddingVertical(4)
            .PaddingHorizontal(4)
            .DefaultTextStyle(x => x.SemiBold());

        static IContainer CellBody(IContainer c) => c
            .BorderBottom(1)
            .BorderLeft(1)
            .BorderRight(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3)
            .PaddingHorizontal(4);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarCuotaPagada(int prestamoId, int cuotaId)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == prestamoId);

        if (prestamo == null)
        {
            return NotFound();
        }

        var cuota = prestamo.Cuotas.FirstOrDefault(c => c.Id == cuotaId);
        if (cuota == null)
        {
            return NotFound();
        }

        if (cuota.Estado == "Pagado")
        {
            SetToast("La cuota ya estaba marcada como pagada.", "warning", "Aviso");
            return RedirectToAction(nameof(Details), new { id = prestamoId });
        }

        cuota.Estado = "Pagado";
        cuota.MontoPagado = cuota.MontoCuota;
        cuota.FechaPago = DateTime.Now;

        prestamo.SaldoPendiente = Math.Round(prestamo.Cuotas.Sum(c => Math.Max(0, c.MontoCuota - c.MontoPagado)), 0);
        prestamo.Estado = prestamo.SaldoPendiente <= 0 ? "Saldado" : "Activo";

        await _context.SaveChangesAsync();

        await RegistrarActividadAsync("Modificó", "Préstamo", $"Prestamo #{prestamo.Id}", $"Cuota {cuota.NumeroCuota} pagada - Cliente {prestamo.Cliente?.Nombre}");
        SetToast("Cuota marcada como pagada.", "success", "Éxito");

        return RedirectToAction(nameof(Details), new { id = prestamoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReestructurarSoloInteres(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
        {
            return NotFound();
        }

        if (prestamo.Estado == "Saldado")
        {
            SetToast("No se puede reestructurar un préstamo saldado.", "warning", "Aviso");
            return RedirectToAction(nameof(Details), new { id });
        }

        var fechaInicioUtc = prestamo.FechaInicio.Date;
        var interesesPagados = await _context.Pagos
            .Where(p => p.PrestamoId == id && p.FechaPagoUtc >= fechaInicioUtc)
            .SumAsync(p => p.InteresAbonado);

        var capitalPagado = await _context.Pagos
            .Where(p => p.PrestamoId == id && p.FechaPagoUtc >= fechaInicioUtc)
            .SumAsync(p => p.CapitalAbonado);

        var interesPendiente = Math.Max(0, Math.Round(prestamo.MontoInteres - interesesPagados, 0));
        var capitalPendiente = Math.Max(0, Math.Round(prestamo.Monto - capitalPagado, 0));

        if (interesPendiente <= 0)
        {
            SetToast("El interés de este ciclo ya fue pagado.", "warning", "Aviso");
            return RedirectToAction(nameof(Details), new { id });
        }

        var pagoInteres = new Pago
        {
            ClienteId = prestamo.ClienteId,
            PrestamoId = prestamo.Id,
            FechaPagoUtc = DateTime.UtcNow,
            TipoPago = "SoloInteres",
            MetodoPago = "Reestructuración",
            InteresAbonado = interesPendiente,
            CapitalAbonado = 0,
            TotalPagado = interesPendiente,
            MontoRecibido = interesPendiente,
            CambioDevuelto = 0,
            BalancePendiente = capitalPendiente,
            FormatoComprobante = "A4",
            Usuario = User.Identity?.Name ?? "Sistema",
            Detalles = new List<PagoDetalle>()
        };

        var nuevaFechaInicio = DateTime.Today;
        var proximaFechaVencimiento = AddByFrecuencia(nuevaFechaInicio, prestamo.FrecuenciaPago, 1);
        prestamo.FechaInicio = nuevaFechaInicio;
        prestamo.Monto = capitalPendiente;
        prestamo.MontoInteres = Math.Round(capitalPendiente * (prestamo.TasaInteresAnual / 100m), 0);
        prestamo.TotalAPagar = Math.Round(prestamo.Monto + prestamo.MontoInteres, 0);
        prestamo.ValorCuota = Math.Round(prestamo.TotalAPagar / prestamo.NumeroPagos, 0);
        prestamo.SaldoPendiente = prestamo.TotalAPagar;
        prestamo.Estado = "Activo";

        _context.PrestamoCuotas.RemoveRange(prestamo.Cuotas);
        _context.PrestamoCuotas.AddRange(BuildCuotas(prestamo));
        _context.Pagos.Add(pagoInteres);

        await _context.SaveChangesAsync();

        await RegistrarActividadAsync(
            "Modificó",
            "Préstamo",
            $"Prestamo #{prestamo.Id}",
            $"Reestructurado por pago solo interés ({interesPendiente:C0}). Próximo vencimiento: {proximaFechaVencimiento:dd/MM/yyyy}");

        SetToast("Préstamo reestructurado. Se registró pago solo interés y se reinició al próximo ciclo.", "success", "Éxito");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
        {
            return NotFound();
        }

        if (prestamo.Cuotas.Any(c => c.Estado == "Pagado"))
        {
            SetToast("No se puede editar un préstamo con cuotas pagadas.", "warning", "Aviso");
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new PrestamoCreateViewModel
        {
            ClienteId = prestamo.ClienteId,
            Monto = prestamo.Monto,
            TasaInteresAnual = prestamo.TasaInteresAnual,
            NumeroPagos = prestamo.NumeroPagos,
            FrecuenciaPago = prestamo.FrecuenciaPago,
            FechaInicio = prestamo.FechaInicio,
            Observaciones = prestamo.Observaciones,
            MontoInteres = prestamo.MontoInteres,
            TotalAPagar = prestamo.TotalAPagar,
            ValorCuota = prestamo.ValorCuota
        };

        await LoadClientes(vm);
        ViewData["PrestamoId"] = id;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PrestamoCreateViewModel model)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
        {
            return NotFound();
        }

        if (prestamo.Cuotas.Any(c => c.Estado == "Pagado"))
        {
            SetToast("No se puede editar un préstamo con cuotas pagadas.", "warning", "Aviso");
            return RedirectToAction(nameof(Details), new { id });
        }

        await LoadClientes(model);
        ViewData["PrestamoId"] = id;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var montoInteres = CalcularInteresPorTasaMensual(model.Monto, model.TasaInteresAnual, model.NumeroPagos, model.FrecuenciaPago);
        var totalPagar = Math.Round(model.Monto + montoInteres, 0);
        var valorCuota = Math.Round(totalPagar / model.NumeroPagos, 0);

        prestamo.ClienteId = model.ClienteId;
        prestamo.Monto = model.Monto;
        prestamo.TasaInteresAnual = model.TasaInteresAnual;
        prestamo.NumeroPagos = model.NumeroPagos;
        prestamo.FrecuenciaPago = model.FrecuenciaPago;
        prestamo.FechaInicio = model.FechaInicio;
        prestamo.Observaciones = model.Observaciones;
        prestamo.MontoInteres = montoInteres;
        prestamo.TotalAPagar = totalPagar;
        prestamo.ValorCuota = valorCuota;
        prestamo.SaldoPendiente = totalPagar;
        prestamo.Estado = "Activo";

        _context.PrestamoCuotas.RemoveRange(prestamo.Cuotas);
        _context.PrestamoCuotas.AddRange(BuildCuotas(prestamo));

        await _context.SaveChangesAsync();

        await RegistrarActividadAsync("Modificó", "Préstamo", $"Prestamo #{prestamo.Id}", $"Préstamo actualizado - Cliente {prestamo.Cliente?.Nombre}");
        SetToast("Préstamo actualizado correctamente.", "success", "Éxito");

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
        {
            return NotFound();
        }

        if (prestamo.Estado != "Saldado")
        {
            SetToast("Solo se puede eliminar un préstamo saldado.", "warning", "Aviso");
            return RedirectToAction(nameof(Details), new { id });
        }

        _context.Prestamos.Remove(prestamo);
        await _context.SaveChangesAsync();

        await RegistrarActividadAsync("Eliminó", "Préstamo", $"Prestamo #{id}", $"Cliente {prestamo.Cliente?.Nombre}");
        SetToast("Préstamo eliminado correctamente.", "success", "Éxito");

        return RedirectToAction(nameof(Index));
    }

    private static List<PrestamoCuota> BuildCuotas(Prestamo prestamo)
    {
        var cuotas = new List<PrestamoCuota>();

        for (var i = 1; i <= prestamo.NumeroPagos; i++)
        {
            cuotas.Add(new PrestamoCuota
            {
                PrestamoId = prestamo.Id,
                NumeroCuota = i,
                FechaVencimiento = AddByFrecuencia(prestamo.FechaInicio, prestamo.FrecuenciaPago, i),
                MontoCuota = prestamo.ValorCuota,
                MontoPagado = 0,
                Estado = "Pendiente"
            });
        }

        return cuotas;
    }

    private static DateTime AddByFrecuencia(DateTime baseDate, string frecuencia, int salto)
    {
        return frecuencia switch
        {
            "Diario" => baseDate.AddDays(salto),
            "Semanal" => baseDate.AddDays(7 * salto),
            "Quincenal" => baseDate.AddDays(15 * salto),
            _ => baseDate.AddMonths(salto)
        };
    }

    private static decimal CalcularInteresPorTasaMensual(decimal monto, decimal tasaMensual, int numeroPagos, string frecuencia)
    {
        var factorMes = frecuencia switch
        {
            "Diario" => 1m / 30m,
            "Semanal" => 7m / 30m,
            "Quincenal" => 15m / 30m,
            _ => 1m
        };

        var mesesEquivalentes = numeroPagos * factorMes;
        return Math.Round(monto * (tasaMensual / 100m) * mesesEquivalentes, 0);
    }

    private async Task LoadClientes(PrestamoCreateViewModel vm)
    {
        vm.Clientes = await _context.Clientes
            .OrderBy(c => c.Nombre)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Nombre} ({c.Documento})"
            })
            .ToListAsync();
    }

    private async Task RegistrarActividadAsync(string accion, string entidad, string referencia, string detalle)
    {
        var usuario = User.Identity?.Name ?? "Sistema";
        _context.ActivityLogs.Add(new ActivityLog
        {
            Usuario = usuario,
            Accion = accion,
            Entidad = entidad,
            EntidadReferencia = referencia,
            Detalle = detalle,
            FechaUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    private void SetToast(string message, string type, string title)
    {
        TempData["ToastMessage"] = message;
        TempData["ToastType"] = type;
        TempData["ToastTitle"] = title;
    }
}
