<#
.SYNOPSIS
  Genera el instalable del modo Desktop (offline) en .\dist:
    dist\app      -> MiniMarket.Desktop con la Api EMBEBIDA (un solo ejecutable, sin servicio Windows),
                     self-contained win-x64 (no requiere .NET instalado), ofuscado
    dist\tools    -> MiniMarket.ConfigTool (opcional: configurar la conexión por script)
    dist\install  -> install.ps1 / uninstall.ps1 / backup-db.ps1 + SQL inicial + guía

  Lo único que el equipo destino necesita además de esto es SQL Server.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File desktop\install\publish.ps1
  powershell -ExecutionPolicy Bypass -File desktop\install\publish.ps1 -SinOfuscar
#>
param(
    [switch]$SinOfuscar,
    [string]$Configuracion = "Release"
)

$ErrorActionPreference = "Stop"
$raiz = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$dist = Join-Path $raiz "dist"
$ofuscar = if ($SinOfuscar) { "false" } else { "true" }

function Publicar([string]$proyecto, [string]$destino, [string]$obf) {
    Write-Host "`n==> Publicando $proyecto -> $destino (ofuscar=$obf)" -ForegroundColor Cyan
    dotnet publish (Join-Path $raiz $proyecto) -c $Configuracion -r win-x64 --self-contained true `
        -p:Obfuscate=$obf -p:DebugType=None -p:DebugSymbols=false -o $destino
    if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $proyecto" }
}

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }

$app = Join-Path $dist "app"
Publicar "desktop\MiniMarket.Desktop\MiniMarket.Desktop.csproj" $app $ofuscar
Publicar "backend\tools\MiniMarket.ConfigTool\MiniMarket.ConfigTool.csproj" (Join-Path $dist "tools") "false"

# --- Limpiar y verificar la carpeta de la app ----------------------------------------------------
# MiniMarket.Api se usa como biblioteca: su lanzador (.exe) y sus archivos de runtime sobran y
# confundirían al usuario ("¿cuál abro?").
Get-ChildItem $app -Filter "MiniMarket.Api.*" |
    Where-Object { $_.Extension -ne ".dll" } |
    Remove-Item -Force

# El instalable NUNCA debe llevar credenciales: los secretos de cada equipo se generan en el destino
# (DPAPI, %ProgramData%\MiniMarket). Si algo se coló, se aborta.
$prohibidos = Get-ChildItem $app -Filter "appsettings.*.json"
if ($prohibidos) { throw "El instalable contiene archivos de configuración no permitidos: $($prohibidos.Name -join ', ')" }
$config = Get-Content (Join-Path $app "appsettings.json") -Raw -Encoding UTF8 | ConvertFrom-Json
if ($config.ConnectionStrings.DefaultConnection -or $config.Jwt.Key) {
    throw "appsettings.json del Desktop no debe tener connection string ni Jwt:Key."
}
Write-Host "Configuración verificada: el instalable no contiene secretos." -ForegroundColor Green

# --- Scripts de instalación ----------------------------------------------------------------------
$install = Join-Path $dist "install"
New-Item -ItemType Directory -Force $install | Out-Null
Copy-Item (Join-Path $PSScriptRoot "*.ps1") $install -Exclude "publish.ps1"
Copy-Item (Join-Path $raiz "database\desktop\00_setup_local.sql") $install
Copy-Item (Join-Path $raiz "docs\desktop-instalacion.md") $install -ErrorAction SilentlyContinue

Write-Host "`nListo. Instalable en $dist" -ForegroundColor Green
Write-Host "Siguiente paso (en el equipo destino, como administrador): dist\install\install.ps1"
