using Microsoft.EntityFrameworkCore;

namespace SysPres.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<CompanySettings> CompanySettings { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Prestamo> Prestamos { get; set; }
    public DbSet<PrestamoCuota> PrestamoCuotas { get; set; }
    public DbSet<Pago> Pagos { get; set; }
    public DbSet<PagoDetalle> PagoDetalles { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Prestamo>(entity =>
        {
            entity.Property(p => p.Monto).HasPrecision(18, 2);
            entity.Property(p => p.TasaInteresAnual).HasPrecision(5, 2);
            entity.Property(p => p.MontoInteres).HasPrecision(18, 2);
            entity.Property(p => p.TotalAPagar).HasPrecision(18, 2);
            entity.Property(p => p.ValorCuota).HasPrecision(18, 2);
            entity.Property(p => p.SaldoPendiente).HasPrecision(18, 2);

            entity.HasOne(p => p.Cliente)
                .WithMany(c => c.Prestamos)
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PrestamoCuota>(entity =>
        {
            entity.Property(c => c.MontoCuota).HasPrecision(18, 2);
            entity.Property(c => c.MontoPagado).HasPrecision(18, 2);

            entity.HasOne(c => c.Prestamo)
                .WithMany(p => p.Cuotas)
                .HasForeignKey(c => c.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Pago>(entity =>
        {
            entity.Property(p => p.TotalPagado).HasPrecision(18, 2);
            entity.Property(p => p.BalancePendiente).HasPrecision(18, 2);
            entity.Property(p => p.CapitalAbonado).HasPrecision(18, 2);
            entity.Property(p => p.InteresAbonado).HasPrecision(18, 2);
            entity.Property(p => p.MontoRecibido).HasPrecision(18, 2);
            entity.Property(p => p.CambioDevuelto).HasPrecision(18, 2);

            entity.HasOne(p => p.Cliente)
                .WithMany()
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Prestamo)
                .WithMany()
                .HasForeignKey(p => p.PrestamoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PagoDetalle>(entity =>
        {
            entity.Property(d => d.MontoAplicado).HasPrecision(18, 2);
            entity.Property(d => d.SaldoCuotaAnterior).HasPrecision(18, 2);
            entity.Property(d => d.SaldoCuotaRestante).HasPrecision(18, 2);

            entity.HasOne(d => d.Pago)
                .WithMany(p => p.Detalles)
                .HasForeignKey(d => d.PagoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.PrestamoCuota)
                .WithMany()
                .HasForeignKey(d => d.PrestamoCuotaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.Documento)
            .IsUnique();

        modelBuilder.Entity<Cliente>()
            .Property(c => c.IngresoMensual)
            .HasPrecision(18, 2);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.IsActive).HasDefaultValue(true);
        });
    }
}
