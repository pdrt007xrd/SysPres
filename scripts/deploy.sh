#!/usr/bin/env bash
set -euo pipefail

PROJECT="./SysPres.csproj"
CONFIGURATION="Release"
PUBLISH_DIR="./publish"
MIGRATE="false"
SKIP_BUILD="false"
SKIP_PUBLISH="false"

usage() {
  cat <<EOF
Uso:
  ./scripts/deploy.sh [opciones]

Opciones:
  --project <ruta>         Ruta al archivo .csproj (default: ./SysPres.csproj)
  --configuration <cfg>    Release o Debug (default: Release)
  --publish-dir <ruta>     Carpeta de salida de publish (default: ./publish)
  --migrate                Ejecuta 'dotnet ef database update'
  --skip-build             Omite 'dotnet build'
  --skip-publish           Omite 'dotnet publish'
  -h, --help               Mostrar ayuda
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --project)
      PROJECT="$2"
      shift 2
      ;;
    --configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --publish-dir)
      PUBLISH_DIR="$2"
      shift 2
      ;;
    --migrate)
      MIGRATE="true"
      shift
      ;;
    --skip-build)
      SKIP_BUILD="true"
      shift
      ;;
    --skip-publish)
      SKIP_PUBLISH="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Opcion no reconocida: $1"
      usage
      exit 1
      ;;
  esac
done

echo "==> Proyecto: $PROJECT"
echo "==> Configuracion: $CONFIGURATION"
echo "==> Publish dir: $PUBLISH_DIR"
echo "==> Migrar DB: $MIGRATE"

if [[ ! -f "$PROJECT" ]]; then
  echo "ERROR: No se encontro el proyecto '$PROJECT'."
  exit 1
fi

echo "==> Restore"
dotnet restore "$PROJECT"

if [[ "$SKIP_BUILD" != "true" ]]; then
  echo "==> Build"
  dotnet build "$PROJECT" -c "$CONFIGURATION" --no-restore
fi

if [[ "$MIGRATE" == "true" ]]; then
  echo "==> Ejecutando migraciones"
  dotnet ef database update --project "$PROJECT"
fi

if [[ "$SKIP_PUBLISH" != "true" ]]; then
  echo "==> Publish"
  dotnet publish "$PROJECT" -c "$CONFIGURATION" -o "$PUBLISH_DIR" --no-restore
fi

echo "==> Despliegue finalizado correctamente."
