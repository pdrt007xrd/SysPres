using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
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
                text = $"Préstamo #{p.Id} - Saldo {p.SaldoPendiente:C0}"
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
                page.DefaultTextStyle(x => x.FontSize(8));

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
                        c.ConstantColumn(78); // Fecha
                        c.ConstantColumn(95); // Cliente
                        c.ConstantColumn(44); // Prestamo
                        c.ConstantColumn(120); // Detalle
                        c.ConstantColumn(50); // Tipo
                        c.ConstantColumn(55); // Metodo
                        c.ConstantColumn(55); // Balance
                        c.ConstantColumn(55); // Capital
                        c.ConstantColumn(55); // Interes
                        c.ConstantColumn(55); // Pagado
                        c.ConstantColumn(55); // Recibido
                        c.ConstantColumn(55); // Devuelta
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
                        table.Cell().Element(CellBody).Text(Limit(p.Cliente?.Nombre ?? "-", 22));
                        table.Cell().Element(CellBody).AlignCenter().Text($"#{p.PrestamoId}");
                        table.Cell().Element(CellBody).Text(Limit(string.IsNullOrWhiteSpace(detalle) ? "-" : detalle, 30));
                        table.Cell().Element(CellBody).AlignCenter().Text(Limit(p.TipoPago, 10));
                        table.Cell().Element(CellBody).AlignCenter().Text(Limit(p.MetodoPago, 11));
                        table.Cell().Element(CellBody).AlignRight().Text(p.BalancePendiente.ToString("C0"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.CapitalAbonado.ToString("C0"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.InteresAbonado.ToString("C0"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.TotalPagado.ToString("C0"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.MontoRecibido.ToString("C0"));
                        table.Cell().Element(CellBody).AlignRight().Text(p.CambioDevuelto.ToString("C0"));
                    }
                });
            });
        }).GeneratePdf();

        Response.Headers.ContentDisposition = "inline; filename=historial-pagos.pdf";
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

        static string Limit(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Length <= max ? value : value[..max];
        }
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
                montoRecibidoSoloInteres = Math.Round(model.MontoRecibido, 0);
                if (montoRecibidoSoloInteres < interesPendiente)
                {
                    ModelState.AddModelError(nameof(model.MontoRecibido), $"El efectivo recibido no puede ser menor al interés pendiente ({interesPendiente:C0}).");
                    return View("Index", model);
                }

                cambioDevueltoSoloInteres = Math.Round(montoRecibidoSoloInteres - interesPendiente, 0);
            }

            var saldoAntesSoloInteres = prestamo.SaldoPendiente;
            var capitalPendiente = Math.Max(0, Math.Round(prestamo.SaldoPendiente - interesPendiente, 0));
            var nuevaFechaInicio = DateTime.Today;

            prestamo.FechaInicio = nuevaFechaInicio;
            prestamo.Monto = capitalPendiente;
            prestamo.MontoInteres = Math.Round(capitalPendiente * (prestamo.TasaInteresAnual / 100m), 0);
            prestamo.TotalAPagar = Math.Round(prestamo.Monto + prestamo.MontoInteres, 0);
            prestamo.ValorCuota = prestamo.NumeroPagos > 0 ? Math.Round(prestamo.TotalAPagar / prestamo.NumeroPagos, 0) : 0;
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
                $"Pago solo interés - Cliente {prestamo.Cliente?.Nombre}, préstamo #{prestamo.Id}, interés {interesPendiente:C0}, saldo {saldoAntesSoloInteres:C0} -> {prestamo.SaldoPendiente:C0}");

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

        var montoAplicar = Math.Round(model.MontoAplicar, 0);
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

            var saldoAnterior = Math.Round(cuota.MontoCuota - cuota.MontoPagado, 0);
            if (saldoAnterior <= 0)
            {
                continue;
            }

            var montoAplicadoCuota = Math.Min(restantePorAplicar, saldoAnterior);
            cuota.MontoPagado = Math.Round(cuota.MontoPagado + montoAplicadoCuota, 0);
            var saldoRestante = Math.Round(cuota.MontoCuota - cuota.MontoPagado, 0);
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

            restantePorAplicar = Math.Round(restantePorAplicar - montoAplicadoCuota, 0);
        }

        if (!detalles.Any())
        {
            ModelState.AddModelError(string.Empty, "No se pudo aplicar el pago con los datos enviados.");
            return View("Index", model);
        }

        var totalPagado = Math.Round(detalles.Sum(x => x.MontoAplicado), 0);
        var capitalAbonado = Math.Round(prestamo.TotalAPagar > 0 ? totalPagado * (prestamo.Monto / prestamo.TotalAPagar) : 0m, 0);
        var interesAbonado = Math.Round(totalPagado - capitalAbonado, 0);
        var metodoPago = string.IsNullOrWhiteSpace(model.MetodoPago) ? "Efectivo" : model.MetodoPago;
        decimal montoRecibido = 0;
        decimal cambioDevuelto = 0;

        if (string.Equals(metodoPago, "Efectivo", StringComparison.OrdinalIgnoreCase))
        {
            montoRecibido = Math.Round(model.MontoRecibido, 0);
            if (montoRecibido < montoAplicar)
            {
                ModelState.AddModelError(nameof(model.MontoRecibido), $"El monto recibido en efectivo no puede ser menor al monto indicado ({montoAplicar:C0}).");
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
            cambioDevuelto = Math.Round(montoRecibido - totalPagado, 0);
        }

        var saldoAntesPrestamo = prestamo.SaldoPendiente;
        var saldoDespuesPrestamo = Math.Round(prestamo.Cuotas.Sum(c => Math.Max(0, c.MontoCuota - c.MontoPagado)), 0);
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
            $"Cliente {prestamo.Cliente?.Nombre}, préstamo #{prestamo.Id}, pagó {pago.TotalPagado:C0}, saldo {saldoAntesPrestamo:C0} -> {saldoDespuesPrestamo:C0}");

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
        var empresaRnc = $"RNC: {(string.IsNullOrWhiteSpace(empresa?.Rnc) ? "-" : empresa.Rnc)}";
        var culturaMoneda = CultureInfo.GetCultureInfo("es-DO");
        var detallesOrdenados = pago.Detalles.OrderBy(x => x.NumeroCuota).ThenBy(x => x.Id).ToList();
        var filasTabla = Math.Max(detallesOrdenados.Count, 1);
        var cuotasIds = pago.Detalles.Select(d => d.PrestamoCuotaId).Distinct().ToList();
        var fechasVencimiento = cuotasIds.Count == 0
            ? new Dictionary<int, DateTime>()
            : await _context.PrestamoCuotas
                .AsNoTracking()
                .Where(c => cuotasIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.FechaVencimiento);
        var contratoNumero = string.IsNullOrWhiteSpace(pago.Cliente?.Documento) ? "-" : pago.Cliente.Documento;
        var reciboNumero = $"PP-{fechaLocal:yyyyMMdd}{pago.Id:0000}";
        var formaPagoEtiqueta = pago.MetodoPago.Equals("Transferencia", StringComparison.OrdinalIgnoreCase)
            ? "DEPOSITO"
            : pago.MetodoPago.ToUpperInvariant();

        string Dinero(decimal monto) => $"RD${Math.Round(monto, 0, MidpointRounding.AwayFromZero).ToString("N0", culturaMoneda)}";
        string MontoTabla(decimal monto) => $"$ {monto:0}";
        string TipoFila(string? tipoAplicacion) =>
            !string.IsNullOrWhiteSpace(tipoAplicacion) && tipoAplicacion.Contains("abono", StringComparison.OrdinalIgnoreCase)
                ? "ABONO"
                : "PAGO";
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                var altoTermico = 320f + (filasTabla * 36f) + 180f;
                page.Size(esTermico ? new PageSize(226.77f, altoTermico) : PageSizes.A4);
                page.Margin(esTermico ? 14 : 24);
                page.MarginTop(esTermico ? 24 : 32);
                page.DefaultTextStyle(x => x.FontFamily("Verdana").FontSize(9));

                if (esTermico)
                {
                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text(empresaNombre).FontSize(13).SemiBold();
                        header.Item().AlignCenter().Text(empresaDireccion);
                        header.Item().AlignCenter().Text($"{empresaCiudad}");
                        header.Item().AlignCenter().Text(empresaRnc);
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

                page.Content().PaddingTop(esTermico ? 14 : 10).PaddingBottom(10).Column(column =>
                {
                    if (esTermico)
                    {
                        column.Item().PaddingTop(4).Column(info =>
                        {
                            info.Item().AlignCenter().Text($"Fecha:   {fechaLocal:dd/MM/yyyy}");
                            info.Item().AlignCenter().Text($"Contrato:   {contratoNumero}");
                            info.Item().AlignCenter().Text($"Recibo No:   {reciboNumero}");
                            info.Item().AlignCenter().Text($"Cliente:   {(pago.Cliente?.Nombre ?? "-").ToUpperInvariant()}");
                            info.Item().AlignCenter().Text($"Atendido Por:   {pago.Usuario}");
                            info.Item().AlignCenter().Text($"Forma Pago:   {formaPagoEtiqueta}");
                        });

                        column.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(0.9f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.4f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(CellHeader).Text("Cuota");
                                h.Cell().Element(CellHeader).Text("Fecha");
                                h.Cell().Element(CellHeader).Text("Tipo");
                                h.Cell().Element(CellHeaderMoney).Text("Total");
                            });

                            foreach (var d in detallesOrdenados)
                            {
                                var fechaCuota = fechasVencimiento.TryGetValue(d.PrestamoCuotaId, out var fv)
                                    ? fv.ToString("dd/MM/yy")
                                    : "-";
                                table.Cell().Element(CellBody).Text(d.NumeroCuota.ToString());
                                table.Cell().Element(CellBody).Text(fechaCuota);
                                table.Cell().Element(CellBody).Text(TipoFila(d.TipoAplicacion));
                                table.Cell().Element(CellBodyMoney).Text(MontoTabla(d.MontoAplicado));
                            }

                            if (!detallesOrdenados.Any())
                            {
                                table.Cell().Element(CellBody).Text("-");
                                table.Cell().Element(CellBody).Text(fechaLocal.ToString("dd/MM/yy"));
                                table.Cell().Element(CellBody).Text("PAGO");
                                table.Cell().Element(CellBodyMoney).Text(MontoTabla(pago.TotalPagado));
                            }
                        });

                        column.Item().PaddingTop(6).AlignCenter().Text($"Pagos aplicados: {filasTabla}");
                        column.Item().PaddingTop(8).Element(CellSeparator).Text(string.Empty);
                        column.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text("Totales:");
                            r.ConstantItem(95).AlignRight().Text(MontoTabla(pago.TotalPagado));
                        });

                        column.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Balance pendiente:");
                            r.ConstantItem(95).AlignRight().Text(MontoTabla(pago.BalancePendiente));
                        });

                        column.Item().PaddingTop(10).AlignCenter().Text("GRACIAS POR SU PAGO!").Bold();
                    }
                    else
                    {
                        column.Item().Text($"Cliente: {pago.Cliente?.Nombre ?? "-"}");
                        column.Item().Text($"Documento: {pago.Cliente?.Documento ?? "-"}");
                        column.Item().Text($"Préstamo: #{pago.PrestamoId}");
                        column.Item().Text($"Usuario: {pago.Usuario}");
                        column.Item().Text($"Tipo de pago: {pago.TipoPago}");
                        column.Item().Text($"Método de pago: {pago.MetodoPago}");

                        column.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1);
                                c.RelativeColumn(1.6f);
                                c.RelativeColumn(1.8f);
                                c.RelativeColumn(1.6f);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(CellHeader).Text("Cuota");
                                h.Cell().Element(CellHeader).Text("Fecha");
                                h.Cell().Element(CellHeaderMoney).Text("Tipo");
                                h.Cell().Element(CellHeaderMoney).AlignRight().Text("Total");
                            });

                            var detallesOrdenados = pago.Detalles.OrderBy(x => x.NumeroCuota).ThenBy(x => x.Id).ToList();
                            foreach (var d in detallesOrdenados)
                            {
                                var fechaCuota = fechasVencimiento.TryGetValue(d.PrestamoCuotaId, out var fv)
                                    ? fv.ToString("dd/MM/yyyy")
                                    : "-";
                                table.Cell().Element(CellBody).Text(d.NumeroCuota.ToString());
                                table.Cell().Element(CellBody).Text(fechaCuota);
                                table.Cell().Element(CellBody).Text(pago.MetodoPago);
                                table.Cell().Element(CellBodyMoney).AlignRight().Text(Dinero(d.MontoAplicado));
                            }

                            if (!detallesOrdenados.Any())
                            {
                                table.Cell().Element(CellBody).Text("-");
                                table.Cell().Element(CellBody).Text(fechaLocal.ToString("dd/MM/yyyy"));
                                table.Cell().Element(CellBody).Text(pago.MetodoPago);
                                table.Cell().Element(CellBodyMoney).AlignRight().Text(Dinero(pago.TotalPagado));
                            }
                        });

                        column.Item().PaddingTop(10).LineHorizontal(1);
                        column.Item().PaddingTop(6).AlignRight().Text($"Total: {Dinero(pago.TotalPagado)}").Bold();
                        column.Item().AlignRight().Text("Formato: A4");
                        column.Item().PaddingTop(8).AlignCenter().Text("Gracias por Preferirnos.").Bold();
                        column.Item().PaddingTop(18).AlignCenter().Text("SysPres - Comprobante generado para impresión o guardado");
                    }
                });
            });
        }).GeneratePdf();

        Response.Headers.ContentDisposition = $"inline; filename=recibo-pago-{pago.Id}.pdf";
        return File(pdf, "application/pdf");

        IContainer CellHeader(IContainer container)
        {
            var paddingHorizontal = esTermico ? 2 : 5;
            return container
                .Background(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .PaddingHorizontal(paddingHorizontal)
                .DefaultTextStyle(x => x.SemiBold());
        }

        IContainer CellBody(IContainer container)
        {
            var paddingHorizontal = esTermico ? 2 : 5;
            return container
                .PaddingVertical(5)
                .PaddingHorizontal(paddingHorizontal);
        }

        IContainer CellHeaderMoney(IContainer container)
        {
            var paddingHorizontal = esTermico ? 0 : 5;
            return container
                .Background(Colors.Grey.Lighten3)
                .PaddingVertical(5)
                .PaddingHorizontal(paddingHorizontal)
                .DefaultTextStyle(x => x.SemiBold());
        }

        IContainer CellBodyMoney(IContainer container)
        {
            var paddingHorizontal = esTermico ? 0 : 5;
            return container
                .PaddingVertical(5)
                .PaddingHorizontal(paddingHorizontal);
        }

        IContainer CellSeparator(IContainer container)
        {
            return container
                .PaddingVertical(2)
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Darken1);
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
                Text = $"Préstamo #{p.Id} - Saldo {p.SaldoPendiente:C0}"
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

        return Math.Max(0, Math.Round(prestamo.MontoInteres - interesPagadoCiclo, 0));
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
}
