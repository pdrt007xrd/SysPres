# SysPres

Sistema web para gestión de clientes, préstamos, pagos, reportes y configuración.

## Stack y paquetes usados
- .NET SDK `10.0`
- ASP.NET Core MVC
- Entity Framework Core SQL Server `10.0.3`
- `BCrypt.Net-Next` `4.0.3` (hash de contraseñas)
- `QuestPDF` `2026.2.0` (reportes PDF)

## Requisitos
- Linux/Windows con .NET SDK 10 instalado
- SQL Server accesible desde la aplicación
- Herramientas EF Core:
  - `dotnet tool install --global dotnet-ef`

## Configuración
1. Edita `appsettings.json`:
   - `ConnectionStrings:DefaultConnection`
   - `Empresa:Nombre`, `Empresa:Direccion`, `Empresa:Telefono`, `Empresa:Ciudad`
2. Para producción, recomienda usar variables de entorno o secretos en vez de credenciales en texto plano.

## Ejecución local
```bash
dotnet restore
dotnet build
dotnet ef database update
dotnet run --project SysPres.csproj
```

## Despliegue automatizado (Linux)
Se incluye el script `scripts/deploy.sh`.

Ejemplo:
```bash
chmod +x scripts/deploy.sh
./scripts/deploy.sh \
  --project /home/usuario/SysPres/SysPres.csproj \
  --configuration Release \
  --publish-dir /var/www/syspres \
  --migrate
```

Parámetros:
- `--project`: ruta al `.csproj` (por defecto `./SysPres.csproj`)
- `--configuration`: `Release` o `Debug` (por defecto `Release`)
- `--publish-dir`: carpeta de salida publish (por defecto `./publish`)
- `--migrate`: aplica migraciones EF antes de publicar
- `--skip-build`: omite `dotnet build`
- `--skip-publish`: omite `dotnet publish`

## Publicación manual
```bash
dotnet restore
dotnet build -c Release
dotnet ef database update
dotnet publish -c Release -o ./publish
```

Luego sirve la carpeta publicada con:
- `systemd` + Kestrel (+ Nginx recomendado), o
- contenedor Docker.

## Script de servicio systemd (referencia rápida)
Archivo ejemplo: `/etc/systemd/system/syspres.service`
```ini
[Unit]
Description=SysPres
After=network.target

[Service]
WorkingDirectory=/var/www/syspres
ExecStart=/usr/bin/dotnet /var/www/syspres/SysPres.dll
Restart=always
RestartSec=5
SyslogIdentifier=syspres
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Comandos:
```bash
sudo systemctl daemon-reload
sudo systemctl enable syspres
sudo systemctl restart syspres
sudo systemctl status syspres
```

## Notas
- Los reportes PDF y seguridad de rutas/cabeceras ya están integrados en la app.
- Verifica puertos/firewall y reverse proxy para exposición pública.

---
Desarrollado por Pedro Peguero, 829-966-1111 (WhatsApp).
