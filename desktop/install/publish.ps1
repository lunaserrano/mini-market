<#
.SYNOPSIS
  Genera los instalables del modo Desktop (offline) en .\dist:
    dist\api      -> MiniMarket.Api (servicio Windows), self-contained win-x64, ofuscada
    dist\desktop  -> MiniMarket.Desktop (cliente WinForms), self-contained win-x64, ofuscado
    dist\tools    -> MiniMarket.ConfigTool (cifra los secretos en el equipo destino)
    dist\install  -> scripts de instalación + SQL inicial

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

Publicar "backend\src\MiniMarket.Api\MiniMarket.Api.csproj" (Join-Path $dist "api") $ofuscar
Publicar "desktop\MiniMarket.Desktop\MiniMarket.Desktop.csproj" (Join-Path $dist "desktop") $ofuscar
Publicar "backend\tools\MiniMarket.ConfigTool\MiniMarket.ConfigTool.csproj" (Join-Path $dist "tools") "false"

# --- Sanear la configuración de la Api ---------------------------------------------------------
# El instalable NUNCA debe llevar las credenciales de Azure ni la Jwt:Key de appsettings.json: los
# secretos de cada instalación los genera MiniMarket.ConfigTool en el equipo destino (DPAPI).
$api = Join-Path $dist "api"
Remove-Item (Join-Path $api "appsettings.Development.json") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $api "appsettings.Secrets.json") -ErrorAction SilentlyContinue
$appsettings = Join-Path $api "appsettings.json"
$json = Get-Content $appsettings -Raw -Encoding UTF8 | ConvertFrom-Json
$json.ConnectionStrings.DefaultConnection = ""
$json.Jwt.Key = ""
$json.AllowedOrigins = @()
$json | ConvertTo-Json -Depth 10 | Set-Content $appsettings -Encoding UTF8
Write-Host "Configuración de la Api saneada (sin connection string ni Jwt:Key)." -ForegroundColor Green

# --- Scripts de instalación ----------------------------------------------------------------------
$install = Join-Path $dist "install"
New-Item -ItemType Directory -Force $install | Out-Null
Copy-Item (Join-Path $PSScriptRoot "*.ps1") $install -Exclude "publish.ps1"
Copy-Item (Join-Path $raiz "database\desktop\00_setup_local.sql") $install
Copy-Item (Join-Path $raiz "docs\desktop-instalacion.md") $install -ErrorAction SilentlyContinue

Write-Host "`nListo. Instalables en $dist" -ForegroundColor Green
Write-Host "Siguiente paso (en el equipo destino, como administrador): dist\install\install-api-service.ps1"
