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

[Authorize(Policy = "CanReportes")]
public class ReportesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? clienteId = null)
    {
        var vm = await BuildResumenAsync(clienteId);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ResumenClientesPdf(int? clienteId = null)
    {
        var vm = await BuildResumenAsync(clienteId);
        var generado = DateTime.Now;
        var filtro = string.IsNullOrWhiteSpace(vm.ClienteNombre) ? "Todos los clientes" : vm.ClienteNombre;
        var totalPrestamos = vm.Items.Sum(x => x.Prestamos);
        var totalCapital = vm.Items.Sum(x => x.CapitalPrestado);
        var totalInteres = vm.Items.Sum(x => x.InteresGenerado);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(header =>
                {
                    header.Item().Text("Reporte Total por Cliente").FontSize(18).Bold();
                    header.Item().Text($"Filtro: {filtro}");
                    header.Item().Text($"Generado: {generado:dd/MM/yyyy HH:mm}").FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(130); // Cliente
                            c.ConstantColumn(90); // Documento
                            c.ConstantColumn(58); // Prestamos
                            c.ConstantColumn(80); // Prestado
                            c.ConstantColumn(80); // Interes
                            c.ConstantColumn(85); // Interes cobrado
                            c.ConstantColumn(80); // Total
                        });

                        table.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Cliente");
                            h.Cell().Element(CellHeader).Text("Documento");
                            h.Cell().Element(CellHeader).AlignRight().Text("Préstamos");
                            h.Cell().Element(CellHeader).AlignRight().Text("Prestado");
                            h.Cell().Element(CellHeader).AlignRight().Text("Interés");
                            h.Cell().Element(CellHeader).AlignRight().Text("Interés cobrado");
                            h.Cell().Element(CellHeader).AlignRight().Text("Total");
                        });

                        foreach (var item in vm.Items)
                        {
                            table.Cell().Element(CellBody).Text(Limit(item.Cliente, 26));
                            table.Cell().Element(CellBody).Text(item.Documento);
                            table.Cell().Element(CellBody).AlignRight().Text(item.Prestamos.ToString());
                            table.Cell().Element(CellBody).AlignRight().Text(item.CapitalPrestado.ToString("C0"));
                            table.Cell().Element(CellBody).AlignRight().Text(item.InteresGenerado.ToString("C0"));
                            table.Cell().Element(CellBody).AlignRight().Text(item.InteresCobrado.ToString("C0"));
                            table.Cell().Element(CellBody).AlignRight().Text(item.TotalAPagar.ToString("C0"));
                        }
                    });

                    column.Item().PaddingTop(10).Text($"Total préstamos creados: {totalPrestamos}").Bold();
                    column.Item().Text($"Suma total de capital: {totalCapital:C0}").Bold();
                    column.Item().Text($"Suma total de interés: {totalInteres:C0}").Bold();
                });
            });
        }).GeneratePdf();

        Response.Headers.ContentDisposition = "inline; filename=reporte-total-clientes.pdf";
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

    private async Task<ReporteResumenClientesViewModel> BuildResumenAsync(int? clienteId)
    {
        var vm = new ReporteResumenClientesViewModel
        {
            ClienteId = clienteId
        };

        vm.Clientes = await _context.Clientes
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Nombre
            })
            .ToListAsync();

        if (clienteId.HasValue)
        {
            vm.ClienteNombre = await _context.Clientes
                .AsNoTracking()
                .Where(c => c.Id == clienteId.Value)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync();
        }

        var query = _context.Prestamos
            .AsNoTracking()
            .Include(p => p.Cliente)
            .AsQueryable();

        if (clienteId.HasValue)
        {
            query = query.Where(p => p.ClienteId == clienteId.Value);
        }

        var prestamos = await query.ToListAsync();
        var pagos = await _context.Pagos
            .AsNoTracking()
            .Where(p => !clienteId.HasValue || p.ClienteId == clienteId.Value)
            .ToListAsync();

        vm.Items = prestamos
            .GroupBy(p => new { p.ClienteId, Cliente = p.Cliente != null ? p.Cliente.Nombre : "-", Documento = p.Cliente != null ? p.Cliente.Documento : "-" })
            .Select(g => new ReporteResumenClienteItemViewModel
            {
                ClienteId = g.Key.ClienteId,
                Cliente = g.Key.Cliente,
                Documento = g.Key.Documento,
                Prestamos = g.Count(),
                CapitalPrestado = g.Sum(x => x.Monto),
                InteresGenerado = g.Sum(x => x.MontoInteres),
                InteresCobrado = pagos.Where(x => x.ClienteId == g.Key.ClienteId).Sum(x => x.InteresAbonado),
                PagosSoloInteres = pagos.Count(x => x.ClienteId == g.Key.ClienteId && x.TipoPago == "SoloInteres"),
                TotalAPagar = g.Sum(x => x.TotalAPagar)
            })
            .OrderBy(x => x.Cliente)
            .ToList();

        return vm;
    }
}
