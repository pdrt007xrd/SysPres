using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SysPres.Models;

namespace SysPres.Controllers;

[Authorize(Policy = "CanClientes")]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ClientesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var clientes = await _context.Clientes
            .Include(c => c.Prestamos)
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        return View(clientes);
    }

    [HttpGet]
    public async Task<IActionResult> ReportePdf()
    {
        var clientes = await _context.Clientes
            .Include(c => c.Prestamos)
            .OrderBy(c => c.Nombre)
            .ToListAsync();
        var pagosCliente = await _context.Pagos
            .AsNoTracking()
            .GroupBy(p => p.ClienteId)
            .Select(g => new
            {
                ClienteId = g.Key,
                InteresCobrado = g.Sum(x => x.InteresAbonado),
                PagosSoloInteres = g.Count(x => x.TipoPago == "SoloInteres")
            })
            .ToDictionaryAsync(x => x.ClienteId, x => new { x.InteresCobrado, x.PagosSoloInteres });

        var fechaGeneracion = DateTime.Now;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(header =>
                {
                    header.Item().Text("Reporte de Clientes").FontSize(18).Bold();
                    header.Item().Text($"Generado: {fechaGeneracion:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(12).Column(column =>
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Cliente
                            columns.ConstantColumn(90); // Documento
                            columns.ConstantColumn(95); // Contacto
                            columns.RelativeColumn(3); // Direccion
                            columns.ConstantColumn(75); // Prestamos activos
                            columns.ConstantColumn(85); // Interes cobrado
                            columns.ConstantColumn(70); // Solo interes
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("Cliente");
                            header.Cell().Element(CellHeader).Text("Documento");
                            header.Cell().Element(CellHeader).Text("Contacto");
                            header.Cell().Element(CellHeader).Text("Dirección");
                            header.Cell().Element(CellHeader).AlignRight().Text("Préstamos activos");
                            header.Cell().Element(CellHeader).AlignRight().Text("Interés cobrado");
                            header.Cell().Element(CellHeader).AlignRight().Text("Solo interés");
                        });

                        foreach (var cliente in clientes)
                        {
                            var contacto = string.IsNullOrWhiteSpace(cliente.Telefono) ? (cliente.Email ?? "-") : cliente.Telefono;
                            var prestamosActivos = cliente.Prestamos.Count(p => p.Estado == "Activo");
                            var pagoInfo = pagosCliente.TryGetValue(cliente.Id, out var value) ? value : null;

                            table.Cell().Element(CellBody).Text(Limit(cliente.Nombre, 45));
                            table.Cell().Element(CellBody).Text(Limit(cliente.Documento, 13));
                            table.Cell().Element(CellBody).Text(Limit(contacto, 13));
                            table.Cell().Element(CellBody).Text(Limit(cliente.Direccion ?? "-", 120));
                            table.Cell().Element(CellBody).AlignRight().Text(prestamosActivos.ToString());
                            table.Cell().Element(CellBody).AlignRight().Text((pagoInfo?.InteresCobrado ?? 0m).ToString("C"));
                            table.Cell().Element(CellBody).AlignRight().Text((pagoInfo?.PagosSoloInteres ?? 0).ToString());
                        }
                    });

                    column.Item().PaddingTop(12).Text($"Total de clientes: {clientes.Count}").Bold();
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

        Response.Headers.ContentDisposition = "inline; filename=reporte-clientes.pdf";
        return File(pdfBytes, "application/pdf");

        static IContainer CellHeader(IContainer container)
        {
            return container
                .Background(Colors.Grey.Lighten3)
                .Padding(6)
                .DefaultTextStyle(x => x.SemiBold());
        }

        static IContainer CellBody(IContainer container)
        {
            return container.Padding(6);
        }

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
    public IActionResult Create()
    {
        return View(new Cliente());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Cliente model)
    {
        model.Documento = FormatearDocumento(model.Documento);
        model.Telefono = FormatearTelefono(model.Telefono);
        model.GaranteDocumento = FormatearDocumento(model.GaranteDocumento);
        model.GaranteTelefono = FormatearTelefono(model.GaranteTelefono);
        NormalizeGarante(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var exists = await _context.Clientes.AnyAsync(c => c.Documento == model.Documento);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Documento), "Ya existe un cliente con ese documento.");
            return View(model);
        }

        model.FechaRegistro = DateTime.UtcNow;
        _context.Clientes.Add(model);
        await _context.SaveChangesAsync();

        await RegistrarActividadAsync("Creó", "Cliente", model.Documento, $"Cliente {model.Nombre}");
        SetToastSuccess("Cliente registrado correctamente.");

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
        {
            return NotFound();
        }

        return View(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Cliente model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        model.Documento = FormatearDocumento(model.Documento);
        model.Telefono = FormatearTelefono(model.Telefono);
        model.GaranteDocumento = FormatearDocumento(model.GaranteDocumento);
        model.GaranteTelefono = FormatearTelefono(model.GaranteTelefono);
        NormalizeGarante(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
        {
            return NotFound();
        }

        var duplicatedDocumento = await _context.Clientes.AnyAsync(c => c.Documento == model.Documento && c.Id != id);
        if (duplicatedDocumento)
        {
            ModelState.AddModelError(nameof(model.Documento), "Ya existe un cliente con ese documento.");
            return View(model);
        }

        cliente.Nombre = model.Nombre;
        cliente.Documento = model.Documento;
        cliente.Telefono = model.Telefono;
        cliente.Email = model.Email;
        cliente.Direccion = model.Direccion;
        cliente.Empresa = model.Empresa;
        cliente.Puesto = model.Puesto;
        cliente.IngresoMensual = model.IngresoMensual;
        cliente.MesesLaborando = model.MesesLaborando;
        cliente.TieneGarante = model.TieneGarante;
        cliente.GaranteNombre = model.GaranteNombre;
        cliente.GaranteDocumento = model.GaranteDocumento;
        cliente.GaranteTelefono = model.GaranteTelefono;
        cliente.GaranteDireccion = model.GaranteDireccion;

        await _context.SaveChangesAsync();

        await RegistrarActividadAsync("Modificó", "Cliente", cliente.Documento, $"Cliente {cliente.Nombre}");
        SetToastSuccess("Cliente actualizado correctamente.");

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Prestamos.OrderByDescending(p => p.Id))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente == null)
        {
            return NotFound();
        }

        return View(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCliente(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
        {
            return NotFound();
        }

        var documento = cliente.Documento;
        var nombre = cliente.Nombre;

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();

        await RegistrarActividadAsync("Eliminó", "Cliente", documento, $"Cliente {nombre}");
        SetToastSuccess("Cliente eliminado correctamente.");

        return RedirectToAction(nameof(Index));
    }

    private static string FormatearDocumento(string? documento)
    {
        var digitos = Regex.Replace(documento ?? string.Empty, @"\D", string.Empty);
        if (digitos.Length != 11)
        {
            return documento?.Trim() ?? string.Empty;
        }

        return $"{digitos[..3]}-{digitos.Substring(3, 7)}-{digitos[10]}";
    }

    private static void NormalizeGarante(Cliente model)
    {
        if (model.TieneGarante)
        {
            return;
        }

        model.GaranteNombre = null;
        model.GaranteDocumento = null;
        model.GaranteTelefono = null;
        model.GaranteDireccion = null;
    }

    private static string? FormatearTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return null;
        }

        var digitos = Regex.Replace(telefono, @"\D", string.Empty);
        if (digitos.Length != 10)
        {
            return telefono.Trim();
        }

        return $"{digitos[..3]}-{digitos.Substring(3, 3)}-{digitos.Substring(6, 4)}";
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

    private void SetToastSuccess(string message)
    {
        TempData["ToastMessage"] = message;
        TempData["ToastType"] = "success";
        TempData["ToastTitle"] = "Éxito";
    }
}
