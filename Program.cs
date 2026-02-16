using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Globalization;
using SysPres.Models;
using SysPres.Security;
using SysPres.Services;
using SysPres.Services.Interfaces;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var culturaRd = new CultureInfo("es-DO");
culturaRd.NumberFormat.CurrencySymbol = "RD$";
culturaRd.NumberFormat.CurrencyDecimalDigits = 0;
CultureInfo.DefaultThreadCurrentCulture = culturaRd;
CultureInfo.DefaultThreadCurrentUICulture = culturaRd;

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IAccountService, AccountService>();

builder.Services.AddAuthentication("SysPresCookie")
    .AddCookie("SysPresCookie", options =>
    {
        options.LoginPath = "/Home/Index";
        options.AccessDeniedPath = "/Home/Index";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("CanClientes", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.HasClaim("permission", AppPermissions.Clientes)))
    .AddPolicy("CanPrestamos", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.HasClaim("permission", AppPermissions.Prestamos)))
    .AddPolicy("CanPagos", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.HasClaim("permission", AppPermissions.Pagos)))
    .AddPolicy("CanReportes", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.HasClaim("permission", AppPermissions.Reportes)))
    .AddPolicy("CanDashboard", policy => policy.RequireAssertion(ctx =>
        ctx.User.IsInRole("Admin") || ctx.User.HasClaim("permission", AppPermissions.Dashboard)))
    .AddPolicy("CanConfiguracion", policy => policy.RequireRole("Admin"));

builder.Services.AddControllersWithViews();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(culturaRd);
    options.SupportedCultures = [culturaRd];
    options.SupportedUICultures = [culturaRd];
});

var app = builder.Build();

app.UseRequestLocalization();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();

app.Use(async (context, next) =>
{
    // Cabeceras base para reducir superficie de ataque del navegador.
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; object-src 'none'; frame-ancestors 'none';";

    if (HttpMethods.IsTrace(context.Request.Method) || string.Equals(context.Request.Method, "TRACK", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;
    var blockedSegments = new[]
    {
        "/.git", "/.svn", "/.env", "/appsettings", "/migrations", "/controllers", "/models", "/views"
    };

    if (blockedSegments.Any(segment => path.Contains(segment, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var blockedExtensions = new[]
    {
        ".cs", ".csproj", ".sln", ".sql", ".bak", ".config", ".yml", ".yaml", ".md", ".log"
    };

    if (blockedExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/css") ||
        context.Request.Path.StartsWithSegments("/js") ||
        context.Request.Path.StartsWithSegments("/lib") ||
        context.Request.Path.StartsWithSegments("/favicon"))
    {
        await next();
        return;
    }

    context.Response.OnStarting(() =>
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        return Task.CompletedTask;
    });

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
