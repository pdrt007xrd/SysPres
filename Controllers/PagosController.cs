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

[Authorize(Policy = "CanPagos")]
public class PagosController : Controller
{
    private readonly ApplicationDbContext _context;

    public PagosController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? clienteId = null, int? prestamoId = null)
    {
        var vm = new PagoIndexViewModel
        {
            ClienteId = clienteId,
            PrestamoId = prestamoId
        };

        await LoadClientes(vm);
        await LoadPrestamos(vm, clienteId);
        await LoadCuotas(vm, prestamoId);

        if (clienteId.HasValue)
        {
            vm.ClienteNombre = await _context.Clientes
                .Where(c => c.Id == clienteId.Value)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync();
        }

        if (prestamoId.HasValue)
        {
            var prestamo = await _context.Prestamos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == prestamoId.Value);
            if (prestamo != null)
            {
                vm.InteresPendienteCiclo = await CalcularInteresPendienteCiclo(prestamo);
            }
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> PrestamosPorCliente(int clienteId)
    {
        var prestamos = await _context.Prestamos
            .AsNoTracking()
            .Where(p => p.ClienteId == clienteId && p.Estado != "Saldado")
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                id = p.Id,
                text = $"Préstamo #{p.Id} - Saldo {p.SaldoPendiente:C}"
            })
            .ToListAsync();

        return Json(new
        {
            prestamos,
            ultimoPrestamoId = prestamos.FirstOrDefault()?.id
        });
    }

    [HttpGet]
    public async Task<IActionResult> Historial(int? clienteId = null)
    {
        var vm = new PagoHistorialViewModel
        {
            ClienteId = clienteId
        };

        vm.Clientes = await _context.Clientes
            .OrderBy(c => c.Nombre)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Nombre} ({c.Documento})"
            })
            .ToListAsync();

        var pagosQuery = _context.Pagos
            .AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Detalles)
            .OrderByDescending(p => p.FechaPagoUtc)
            .ThenByDescending(p => p.Id)
            .AsQueryable();

        if (clienteId.HasValue)
        {
            pagosQuery = pagosQuery.Where(p => p.ClienteId == clienteId.Value);
            vm.ClienteNombre = await _context.Clientes
                .Where(c => c.Id == clienteId.Value)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync();
        }

        var pagos = await pagosQuery.ToListAsync();
        vm.Pagos = pagos
            .Select(p => new PagoHistorialItemViewModel
            {
                PagoId = p.Id,
                FechaLocal = p.FechaPagoUtc.ToLocalTime(),
                ClienteNombre = p.Cliente != null ? p.Cliente.Nombre : "-",
                PrestamoId = p.PrestamoId,
                AplicadoPor = p.Usuario,
                TipoPago = p.TipoPago,
                MetodoPago = p.MetodoPago,
                TotalPagado = p.TotalPagado,
                BalancePendiente = p.BalancePendiente,
                CapitalAbonado = p.CapitalAbonado,
                InteresAbonado = p.InteresAbonado,
                MontoRecibido = p.MontoRecibido,
                CambioDevuelto = p.CambioDevuelto,
                DetalleAplicacion = string.Join(", ", p.Detalles
                    .OrderBy(d => d.NumeroCuota)
                    .Select(d => d.TipoAplicacion))
            })
            .ToList();

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> HistorialPdf(int? clienteId = null)
    {
        var pagosQuery = _context.Pagos
            .AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Detalles)
            .OrderByDescending(p => p.FechaPagoUtc)
            .ThenByDescending(p => p.Id)
            .AsQueryable();

        var clienteNombre = "Todos los clientes";
        if (clienteId.HasValue)
        {
            pagosQuery = pagosQuery.Where(p => p.ClienteId == clienteId.Value);
            clienteNombre = await _context.Clientes
                .Where(c => c.Id == clienteId.Value)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync() ?? "Cliente no encontrado";
        }

        var pagos = await pagosQuery.ToListAsync();
        var generado = DateTime.Now;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text("Histórico de Pagos").FontSize(18).Bold();
                    header.Item().Text($"Filtro: {clienteNombre}");
                    header.Item().Text($"Generado: {generado:dd/MM/yyyy HH:mm}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(95);
                        c.RelativeColumn(2);
                        c.ConstantColumn(60);
                        c.RelativeColumn(2);
                        c.ConstantColumn(80);
                        c.ConstantColumn(80);
                        c.ConstantColumn(85);
                        c.ConstantColumn(85);
                        c.ConstantColumn(85);
                        c.ConstantColumn(85);
                        c.ConstantColumn(85);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Element(CellHeader).Text("Fecha");
                        h.Cell().Element(CellHeader).Text("Cliente");
                        h.Cell().Element(CellHeader).Text("Préstamo");
                        h.Cell().Element(CellHeader).Text("Detalle");
                        h.Cell().Element(CellHeader).Text("Tipo");
                        h.Cell().Element(CellHeader).Text("Método");
                        h.Cell().Element(CellHeader).AlignRight().Text("Balance");
                        h.Cell().Element(CellHeader).AlignRight().Text("Capital");
                        h.Cell().Element(CellHeader).AlignRight().Text("Interés");
                        h.Cell().Element(CellHeader).AlignRight().Text("Pagado");
                        h.Cell().Element(CellHeader).AlignRight().Text("Recibido");
                        h.Cell().Element(CellHeader).AlignRight().Text("Devuelta");
                    });

                    foreach (var p in pagos)
                    {
                        var detalle = string.Join(", ", p.Detalles
                            .OrderBy(d => d.NumeroCuota)
                            .Select(d => d.TipoAplicacion));

                        table.Cell().Element(CellBody).Text(p.FechaPagoUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
                        table.Cell().Element(CellBody).Text(p.Cliente?.Nombre ?? "-");
                        table.Cell().Element(CellBody).Text($"#{p.PrestamoId}");
                        table.Cell().Element(CellBody).Text(string.IsNullOrWhiteSpace(detalle) ? "-" : detalle);
                        table.Cell().Element(CellBody).Text(p.TipoPago);
                        table.Cell().Element(CellBody).Text(p.MetodoPago);
                        table.Cell().Element(CellBody).AlignRight().Text(p.BalancePendiente.ToString("C"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.CapitalAbonado.ToString("C"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.InteresAbonado.ToString("C"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.TotalPagado.ToString("C"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.MontoRecibido.ToString("C"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.CambioDevuelto.ToString("C"));
                    }
                });
            });
        }).GeneratePdf();

        Response.Headers.ContentDisposition = "inline; filename=historial-pagos.pdf";
        return File(pdf, "application/pdf");

        static IContainer CellHeader(IContainer c) => c.Background(Colors.Grey.Lighten3).Padding(5).DefaultTextStyle(x => x.SemiBold());
        static IContainer CellBody(IContainer c) => c.Padding(5);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(PagoIndexViewModel model)
    {
        await LoadClientes(model);
        await LoadPrestamos(model, model.ClienteId);
        await LoadCuotas(model, model.PrestamoId);

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas.OrderBy(c => c.NumeroCuota))
            .FirstOrDefaultAsync(p => p.Id == model.PrestamoId);

        if (prestamo == null || prestamo.ClienteId != model.ClienteId)
        {
            ModelState.AddModelError(string.Empty, "Préstamo no válido para el cliente seleccionado.");
            return View("Index", model);
        }

        model.InteresPendienteCiclo = await CalcularInteresPendienteCiclo(prestamo);
        var tipoPago = string.IsNullOrWhiteSpace(model.TipoPago) ? "Normal" : model.TipoPago;

        if (string.Equals(tipoPago, "SoloInteres", StringComparison.OrdinalIgnoreCase))
        {
            var interesPendiente = model.InteresPendienteCiclo;
            if (interesPendiente <= 0)
            {
                ModelState.AddModelError(nameof(model.TipoPago), "El interés del ciclo actual ya fue cubierto.");
                return View("Index", model);
            }

            var metodoPagoSoloInteres = string.IsNullOrWhiteSpace(model.MetodoPago) ? "Efectivo" : model.MetodoPago;
            decimal montoRecibidoSoloInteres = 0;
            decimal cambioDevueltoSoloInteres = 0;

            if (string.Equals(metodoPagoSoloInteres, "Efectivo", StringComparison.OrdinalIgnoreCase))
            {
                montoRecibidoSoloInteres = Math.Round(model.MontoRecibido, 2);
                if (montoRecibidoSoloInteres < interesPendiente)
                {
                    ModelState.AddModelError(nameof(model.MontoRecibido), $"El efectivo recibido no puede ser menor al interés pendiente ({interesPendiente:C}).");
                    return View("Index", model);
                }

                cambioDevueltoSoloInteres = Math.Round(montoRecibidoSoloInteres - interesPendiente, 2);
            }

            var saldoAntesSoloInteres = prestamo.SaldoPendiente;
            var capitalPendiente = Math.Max(0, Math.Round(prestamo.SaldoPendiente - interesPendiente, 2));
            var nuevaFechaInicio = DateTime.Today;

            prestamo.FechaInicio = nuevaFechaInicio;
            prestamo.Monto = capitalPendiente;
            prestamo.MontoInteres = Math.Round(capitalPendiente * (prestamo.TasaInteresAnual / 100m), 2);
            prestamo.TotalAPagar = Math.Round(prestamo.Monto + prestamo.MontoInteres, 2);
            prestamo.ValorCuota = prestamo.NumeroPagos > 0 ? Math.Round(prestamo.TotalAPagar / prestamo.NumeroPagos, 2) : 0;
            prestamo.SaldoPendiente = prestamo.TotalAPagar;
            prestamo.Estado = prestamo.SaldoPendiente <= 0 ? "Saldado" : "Activo";

            _context.PrestamoCuotas.RemoveRange(prestamo.Cuotas);
            _context.PrestamoCuotas.AddRange(BuildCuotas(prestamo));

            var pagoSoloInteres = new Pago
            {
                ClienteId = prestamo.ClienteId,
                PrestamoId = prestamo.Id,
                FechaPagoUtc = DateTime.UtcNow,
                TipoPago = "SoloInteres",
                MetodoPago = metodoPagoSoloInteres,
                InteresAbonado = interesPendiente,
                CapitalAbonado = 0,
                TotalPagado = interesPendiente,
                MontoRecibido = montoRecibidoSoloInteres,
                CambioDevuelto = cambioDevueltoSoloInteres,
                BalancePendiente = prestamo.SaldoPendiente,
                FormatoComprobante = model.FormatoPdf,
                Usuario = User.Identity?.Name ?? "Sistema",
                Detalles = new List<PagoDetalle>()
            };

            _context.Pagos.Add(pagoSoloInteres);
            await _context.SaveChangesAsync();

            await RegistrarActividadAsync(
                "Creó",
                "Pago",
                $"Pago #{pagoSoloInteres.Id}",
                $"Pago solo interés - Cliente {prestamo.Cliente?.Nombre}, préstamo #{prestamo.Id}, interés {interesPendiente:C}, saldo {saldoAntesSoloInteres:C} -> {prestamo.SaldoPendiente:C}");

            return RedirectToAction(nameof(ReciboPdf), new { id = pagoSoloInteres.Id, formato = model.FormatoPdf });
        }

        var cuotasPendientes = prestamo.Cuotas
            .Where(c => c.MontoCuota - c.MontoPagado > 0)
            .OrderBy(c => c.NumeroCuota)
            .ToList();

        if (!cuotasPendientes.Any())
        {
            ModelState.AddModelError(string.Empty, "Este préstamo no tiene cuotas pendientes.");
            return View("Index", model);
        }

        var montoAplicar = Math.Round(model.MontoAplicar, 2);
        if (montoAplicar <= 0)
        {
            ModelState.AddModelError(nameof(model.MontoAplicar), "Indica un monto válido para aplicar al préstamo.");
            return View("Index", model);
        }

        var detalles = new List<PagoDetalle>();
        var ahora = DateTime.UtcNow;
        var restantePorAplicar = montoAplicar;
        foreach (var cuota in cuotasPendientes)
        {
            if (restantePorAplicar <= 0)
            {
                break;
            }

            var saldoAnterior = Math.Round(cuota.MontoCuota - cuota.MontoPagado, 2);
            if (saldoAnterior <= 0)
            {
                continue;
            }

            var montoAplicadoCuota = Math.Min(restantePorAplicar, saldoAnterior);
            cuota.MontoPagado = Math.Round(cuota.MontoPagado + montoAplicadoCuota, 2);
            var saldoRestante = Math.Round(cuota.MontoCuota - cuota.MontoPagado, 2);
            cuota.Estado = saldoRestante <= 0 ? "Pagado" : "Parcial";
            cuota.FechaPago = saldoRestante <= 0 ? ahora : cuota.FechaPago;

            detalles.Add(new PagoDetalle
            {
                PrestamoCuotaId = cuota.Id,
                NumeroCuota = cuota.NumeroCuota,
                TipoAplicacion = saldoRestante <= 0
                    ? $"Saldo cuota {cuota.NumeroCuota}"
                    : $"Abono cuota {cuota.NumeroCuota}",
                MontoAplicado = montoAplicadoCuota,
                SaldoCuotaAnterior = saldoAnterior,
                SaldoCuotaRestante = saldoRestante
            });

            restantePorAplicar = Math.Round(restantePorAplicar - montoAplicadoCuota, 2);
        }

        if (!detalles.Any())
        {
            ModelState.AddModelError(string.Empty, "No se pudo aplicar el pago con los datos enviados.");
            return View("Index", model);
        }

        var totalPagado = Math.Round(detalles.Sum(x => x.MontoAplicado), 2);
        var capitalAbonado = Math.Round(prestamo.TotalAPagar > 0 ? totalPagado * (prestamo.Monto / prestamo.TotalAPagar) : 0m, 2);
        var interesAbonado = Math.Round(totalPagado - capitalAbonado, 2);
        var metodoPago = string.IsNullOrWhiteSpace(model.MetodoPago) ? "Efectivo" : model.MetodoPago;
        decimal montoRecibido = 0;
        decimal cambioDevuelto = 0;

        if (string.Equals(metodoPago, "Efectivo", StringComparison.OrdinalIgnoreCase))
        {
            montoRecibido = Math.Round(model.MontoRecibido, 2);
            if (montoRecibido < montoAplicar)
            {
                ModelState.AddModelError(nameof(model.MontoRecibido), $"El monto recibido en efectivo no puede ser menor al monto indicado ({montoAplicar:C}).");
                return View("Index", model);
            }
        }
        else if (restantePorAplicar > 0)
        {
            ModelState.AddModelError(nameof(model.MontoAplicar), "El monto excede el saldo pendiente del préstamo.");
            return View("Index", model);
        }

        if (string.Equals(metodoPago, "Efectivo", StringComparison.OrdinalIgnoreCase))
        {
            cambioDevuelto = Math.Round(montoRecibido - totalPagado, 2);
        }

        var saldoAntesPrestamo = prestamo.SaldoPendiente;
        var saldoDespuesPrestamo = Math.Round(prestamo.Cuotas.Sum(c => Math.Max(0, c.MontoCuota - c.MontoPagado)), 2);
        prestamo.SaldoPendiente = saldoDespuesPrestamo;
        prestamo.Estado = saldoDespuesPrestamo <= 0 ? "Saldado" : "Activo";

        var pago = new Pago
        {
            ClienteId = prestamo.ClienteId,
            PrestamoId = prestamo.Id,
            FechaPagoUtc = ahora,
            TotalPagado = totalPagado,
            BalancePendiente = saldoDespuesPrestamo,
            CapitalAbonado = capitalAbonado,
            InteresAbonado = interesAbonado,
            TipoPago = tipoPago,
            MetodoPago = metodoPago,
            MontoRecibido = montoRecibido,
            CambioDevuelto = cambioDevuelto,
            FormatoComprobante = model.FormatoPdf,
            Usuario = User.Identity?.Name ?? "Sistema",
            Detalles = detalles
        };

        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync();

        await RegistrarActividadAsync(
            "Creó",
            "Pago",
            $"Pago #{pago.Id}",
            $"Cliente {prestamo.Cliente?.Nombre}, préstamo #{prestamo.Id}, pagó {pago.TotalPagado:C}, saldo {saldoAntesPrestamo:C} -> {saldoDespuesPrestamo:C}");

        return RedirectToAction(nameof(ReciboPdf), new { id = pago.Id, formato = model.FormatoPdf });
    }

    [HttpGet]
    public async Task<IActionResult> ReciboPdf(int id, string? formato = null)
    {
        var pago = await _context.Pagos
            .Include(p => p.Cliente)
            .Include(p => p.Prestamo)
            .Include(p => p.Detalles.OrderBy(d => d.NumeroCuota))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pago == null)
        {
            return NotFound();
        }

        var formatoFinal = string.IsNullOrWhiteSpace(formato) ? pago.FormatoComprobante : formato;
        var esTermico = string.Equals(formatoFinal, "80mm", StringComparison.OrdinalIgnoreCase);
        var fechaLocal = pago.FechaPagoUtc.ToLocalTime();
        var empresa = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        var empresaNombre = (empresa?.Nombre ?? "SYS PRES").ToUpperInvariant();
        var empresaDireccion = empresa?.Direccion ?? "Dirección pendiente";
        var empresaTelefono = empresa?.Telefono ?? "Teléfono pendiente";
        var empresaCiudad = empresa?.Ciudad ?? "Ciudad pendiente";

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(esTermico ? new PageSize(226.77f, 850f) : PageSizes.A4);
                page.Margin(esTermico ? 14 : 24);
                page.DefaultTextStyle(x => x.FontSize(esTermico ? 9 : 10));

                if (esTermico)
                {
                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text(empresaNombre).FontSize(12).Bold();
                        header.Item().AlignCenter().Text(empresaDireccion);
                        header.Item().AlignCenter().Text($"{empresaTelefono} - {empresaCiudad}");
                        header.Item().PaddingTop(5).LineHorizontal(1);
                    });
                }
                else
                {
                    page.Header().Column(header =>
                    {
                        header.Item().Text("Recibo de Pago").FontSize(18).Bold();
                        header.Item().Text($"Pago #{pago.Id} - {fechaLocal:dd/MM/yyyy HH:mm}").FontColor(Colors.Grey.Darken2);
                    });
                }

                page.Content().PaddingVertical(10).Column(column =>
                {
                    if (esTermico)
                    {
                        column.Item().Text($"Fecha: {fechaLocal:dd/MM/yyyy HH:mm}");
                        column.Item().PaddingTop(8);
                        column.Item().Text($"Cliente: {pago.Cliente?.Nombre ?? "-"}");
                        column.Item().Text($"Cobrado por: {pago.Usuario}");
                        column.Item().Text($"Préstamo: #{pago.PrestamoId}");
                        column.Item().Text($"Tipo: {pago.TipoPago}");
                        column.Item().Text($"Método: {pago.MetodoPago}");
                    }
                    else
                    {
                        column.Item().Text($"Cliente: {pago.Cliente?.Nombre ?? "-"}");
                        column.Item().Text($"Documento: {pago.Cliente?.Documento ?? "-"}");
                        column.Item().Text($"Préstamo: #{pago.PrestamoId}");
                        column.Item().Text($"Usuario: {pago.Usuario}");
                        column.Item().Text($"Tipo de pago: {pago.TipoPago}");
                        column.Item().Text($"Método de pago: {pago.MetodoPago}");
                    }

                    column.Item().PaddingTop(esTermico ? 16 : 8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1.5f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("#");
                            h.Cell().Element(CellHeader).Text("Detalle");
                            h.Cell().Element(CellHeader).AlignRight().Text("Cobrado");
                            h.Cell().Element(CellHeader).AlignRight().Text("Saldo");
                        });

                        var detallesOrdenados = pago.Detalles.OrderBy(x => x.NumeroCuota).ThenBy(x => x.Id).ToList();
                        foreach (var d in detallesOrdenados)
                        {
                            table.Cell().Element(CellBody).Text(d.NumeroCuota.ToString());
                            table.Cell().Element(CellBody).Text(d.TipoAplicacion);
                            table.Cell().Element(CellBody).AlignRight().Text(d.MontoAplicado.ToString("C"));
                            table.Cell().Element(CellBody).AlignRight().Text(d.SaldoCuotaRestante.ToString("C"));
                        }

                        if (!detallesOrdenados.Any())
                        {
                            table.Cell().Element(CellBody).Text("-");
                            table.Cell().Element(CellBody).Text("Pago solo interés");
                            table.Cell().Element(CellBody).AlignRight().Text(pago.InteresAbonado.ToString("C"));
                            table.Cell().Element(CellBody).AlignRight().Text(pago.BalancePendiente.ToString("C"));
                        }
                    });

                    column.Item().PaddingTop(10).LineHorizontal(1);
                    column.Item().PaddingTop(6).AlignRight().Text($"Balance pendiente: {pago.BalancePendiente:C}");
                    column.Item().AlignRight().Text($"Capital abonado: {pago.CapitalAbonado:C}");
                    column.Item().AlignRight().Text($"Interés abonado: {pago.InteresAbonado:C}");
                    column.Item().AlignRight().Text($"Total pagado: {pago.TotalPagado:C}").Bold();
                    column.Item().AlignRight().Text($"Efectivo recibido: {pago.MontoRecibido:C}");
                    column.Item().AlignRight().Text($"Devuelta: {pago.CambioDevuelto:C}");
                    if (!esTermico)
                    {
                        column.Item().AlignRight().Text("Formato: A4");
                    }

                    column.Item().PaddingTop(8).AlignCenter().Text("Gracias por Preferirnos.").Bold();
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("SysPres - ");
                    x.Span("Comprobante generado para impresión o guardado");
                });
            });
        }).GeneratePdf();

        Response.Headers.ContentDisposition = $"inline; filename=recibo-pago-{pago.Id}.pdf";
        return File(pdf, "application/pdf");

        static IContainer CellHeader(IContainer container)
        {
            return container
                .Background(Colors.Grey.Lighten3)
                .Padding(5)
                .DefaultTextStyle(x => x.SemiBold());
        }

        static IContainer CellBody(IContainer container)
        {
            return container.Padding(5);
        }
    }

    private async Task LoadClientes(PagoIndexViewModel vm)
    {
        vm.Clientes = await _context.Clientes
            .OrderBy(c => c.Nombre)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Nombre
            })
            .ToListAsync();
    }

    private async Task LoadPrestamos(PagoIndexViewModel vm, int? clienteId)
    {
        if (!clienteId.HasValue)
        {
            vm.Prestamos = new List<SelectListItem>();
            return;
        }

        vm.Prestamos = await _context.Prestamos
            .Where(p => p.ClienteId == clienteId.Value && p.Estado != "Saldado")
            .OrderByDescending(p => p.Id)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = $"Préstamo #{p.Id} - Saldo {p.SaldoPendiente:C}"
            })
            .ToListAsync();
    }

    private async Task LoadCuotas(PagoIndexViewModel vm, int? prestamoId)
    {
        if (!prestamoId.HasValue)
        {
            vm.CuotasPendientes = new List<PagoCuotaPendienteViewModel>();
            return;
        }

        vm.CuotasPendientes = await _context.PrestamoCuotas
            .Where(c => c.PrestamoId == prestamoId.Value)
            .OrderBy(c => c.NumeroCuota)
            .Select(c => new PagoCuotaPendienteViewModel
            {
                Id = c.Id,
                NumeroCuota = c.NumeroCuota,
                FechaVencimiento = c.FechaVencimiento,
                MontoCuota = c.MontoCuota,
                MontoPagado = c.MontoPagado,
                Estado = c.Estado
            })
            .ToListAsync();

        vm.CuotasPendientes = vm.CuotasPendientes
            .Where(c => c.SaldoPendiente > 0)
            .ToList();
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

    private async Task<decimal> CalcularInteresPendienteCiclo(Prestamo prestamo)
    {
        var fechaInicioUtc = prestamo.FechaInicio.Date;
        var interesPagadoCiclo = await _context.Pagos
            .AsNoTracking()
            .Where(p => p.PrestamoId == prestamo.Id && p.FechaPagoUtc >= fechaInicioUtc)
            .SumAsync(p => p.InteresAbonado);

        return Math.Max(0, Math.Round(prestamo.MontoInteres - interesPagadoCiclo, 2));
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
            "Semanal" => baseDate.AddDays(7 * salto),
            "Quincenal" => baseDate.AddDays(15 * salto),
            _ => baseDate.AddMonths(salto)
        };
    }
}
