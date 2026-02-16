namespace SysPres.Security;

public static class AppPermissions
{
    public const string Clientes = "Clientes";
    public const string Prestamos = "Prestamos";
    public const string Pagos = "Pagos";
    public const string Reportes = "Reportes";
    public const string Dashboard = "Dashboard";
    public const string Configuracion = "Configuracion";

    public static readonly string[] All = [Dashboard, Clientes, Prestamos, Pagos, Reportes, Configuracion];
}
