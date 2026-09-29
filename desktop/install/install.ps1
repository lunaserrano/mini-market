<#
.SYNOPSIS
  Instala (o actualiza) MiniMarket POS en una caja. NO instala ningún servicio: la Api va embebida
  en MiniMarket.Desktop.exe y vive mientras la app está abierta. Lo único externo es SQL Server.

    1. Copia la app a C:\Program Files\MiniMarket\App (y las herramientas a ...\Tools)
    2. Prepara %ProgramData%\MiniMarket, donde la app guarda la conexión cifrada (DPAPI)
    3. Registra el origen "MiniMarket" en el Visor de eventos (errores de la app)
    4. Crea los accesos directos en el Escritorio público y el menú Inicio
    5. (Opcional, con -SqlServer) configura la conexión ahora; si no, la app la pide al abrirse

  Si encuentra una instalación anterior con el servicio "MiniMarketApi", lo elimina y conserva su
  conexión ya configurada.

  Requisitos previos: SQL Server (Express) accesible y database\desktop\00_setup_local.sql ejecutado.
  Ejecutar como ADMINISTRADOR desde la carpeta dist\install.

.EXAMPLE
  .\install.ps1
  (la conexión se configura al abrir MiniMarket por primera vez)

.EXAMPLE
  .\install.ps1 -SqlServer ".\SQLEXPRESS" -Database MiniMarket -SqlUser minimarket_api
  (pide la contraseña SQL de forma segura y deja la conexión lista)

.EXAMPLE
  .\install.ps1 -SqlServer "CAJA1\SQLEXPRESS" -RestringirConfiguracion
  (segunda caja de la red; solo un administrador podrá cambiar la conexión después)
#>
param(
    [string]$Destino = "$env:ProgramFiles\MiniMarket",
    [string]$SqlServer,
    [string]$Database = "MiniMarket",
    [string]$SqlUser = "minimarket_api",
    [switch]$Integrada,
    # Sin este switch, cualquier usuario de Windows del equipo puede configurar la conexión desde la app
    # (necesario si la configura el cajero en el primer arranque).
    [switch]$RestringirConfiguracion
)

$ErrorActionPreference = "Stop"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute este script como Administrador."
}

$dist = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appOrigen = Join-Path $dist "app"
$toolsOrigen = Join-Path $dist "tools"
foreach ($d in @($appOrigen, $toolsOrigen)) {
    if (-not (Test-Path $d)) { throw "No se encontró $d. Ejecute primero publish.ps1." }
}

$appDestino = Join-Path $Destino "App"
$toolsDestino = Join-Path $Destino "Tools"
$datos = Join-Path $env:ProgramData "MiniMarket"
$secretos = Join-Path $datos "appsettings.Secrets.json"

# 0. La app no puede estar abierta mientras se reemplazan sus archivos.
$abiertas = Get-Process -Name "MiniMarket.Desktop" -ErrorAction SilentlyContinue
if ($abiertas) {
    Write-Host "Cerrando MiniMarket abierto en este equipo..." -ForegroundColor Yellow
    $abiertas | Stop-Process -Force
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force $datos | Out-Null

# 1. Migración desde la versión con servicio Windows (Api separada).
$servicioViejo = Get-Service -Name "MiniMarketApi" -ErrorAction SilentlyContinue
if ($servicioViejo) {
    Write-Host "Eliminando el servicio anterior 'MiniMarketApi' (ya no se usa)..." -ForegroundColor Cyan
    if ($servicioViejo.Status -ne "Stopped") { Stop-Service "MiniMarketApi" -Force; Start-Sleep -Seconds 2 }
    sc.exe delete "MiniMarketApi" | Out-Null
}
$secretosViejos = Join-Path $Destino "Api\appsettings.Secrets.json"
if ((Test-Path $secretosViejos) -and -not (Test-Path $secretos)) {
    # DPAPI de máquina: el archivo sigue siendo válido porque es el mismo equipo.
    Copy-Item $secretosViejos $secretos
    Write-Host "Se conservó la conexión configurada en la instalación anterior." -ForegroundColor Green
}
foreach ($viejo in @("Api", "Desktop")) {
    $ruta = Join-Path $Destino $viejo
    if (Test-Path $ruta) { Remove-Item $ruta -Recurse -Force }
}
Get-NetFirewallRule -DisplayName "MiniMarket Api" -ErrorAction SilentlyContinue | Remove-NetFirewallRule

# 2. Archivos del programa (reemplazo completo: no quedan dll de versiones anteriores).
Write-Host "Copiando archivos a $Destino ..." -ForegroundColor Cyan
foreach ($par in @(@($appOrigen, $appDestino), @($toolsOrigen, $toolsDestino))) {
    if (Test-Path $par[1]) { Remove-Item $par[1] -Recurse -Force }
    New-Item -ItemType Directory -Force $par[1] | Out-Null
    Copy-Item (Join-Path $par[0] "*") $par[1] -Recurse -Force
}
Copy-Item (Join-Path $PSScriptRoot "backup-db.ps1") $toolsDestino -Force

# 3. Permisos de %ProgramData%\MiniMarket. SIDs en vez de nombres ("Users" es "Usuarios" en español):
#    S-1-5-18 SYSTEM, S-1-5-32-544 Administradores, S-1-5-32-545 Usuarios.
$permisoUsuarios = if ($RestringirConfiguracion) { "(OI)(CI)(RX)" } else { "(OI)(CI)(M)" }
icacls $datos /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)(F)" "*S-1-5-32-544:(OI)(CI)(F)" "*S-1-5-32-545:$permisoUsuarios" | Out-Null
if (Test-Path $secretos) { icacls $secretos /reset | Out-Null }

# 4. Origen del Visor de eventos (crearlo requiere administrador; la app solo escribe si existe).
if (-not [System.Diagnostics.EventLog]::SourceExists("MiniMarket")) {
    New-EventLog -LogName Application -Source "MiniMarket"
}

# 5. Conexión con SQL Server (opcional aquí; si no, la app la pide al abrirse).
if ($SqlServer) {
    $configTool = Join-Path $toolsDestino "MiniMarket.ConfigTool.exe"
    $argumentos = @("init", "--dir", $datos, "--server", $SqlServer, "--database", $Database)
    if ($Integrada) {
        $argumentos += "--integrated"
    } else {
        $argumentos += @("--user", $SqlUser)
        $pwdSeguro = Read-Host "Contraseña SQL del login '$SqlUser'" -AsSecureString
        # Por variable de entorno (no por argumento): los argumentos son visibles en la lista de procesos.
        $env:MINIMARKET_SQL_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwdSeguro))
    }
    try {
        & $configTool @argumentos
        if ($LASTEXITCODE -ne 0) { throw "ConfigTool no pudo guardar la conexión (código $LASTEXITCODE)." }
    } finally {
        Remove-Item Env:\MINIMARKET_SQL_PASSWORD -ErrorAction SilentlyContinue
    }
} elseif (Test-Path $secretos) {
    Write-Host "Conexión ya configurada en este equipo. Verificando..."
    & (Join-Path $toolsDestino "MiniMarket.ConfigTool.exe") verify --dir $datos
    if ($LASTEXITCODE -ne 0) {
        Write-Host "La conexión guardada no funciona; MiniMarket la pedirá de nuevo al abrirse." -ForegroundColor Yellow
    }
}

# 6. Accesos directos
$exe = Join-Path $appDestino "MiniMarket.Desktop.exe"
$shell = New-Object -ComObject WScript.Shell
$accesos = @(
    (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "MiniMarket POS.lnk"),
    (Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "MiniMarket POS.lnk")
)
foreach ($ruta in $accesos) {
    $lnk = $shell.CreateShortcut($ruta)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $appDestino
    $lnk.Description = "MiniMarket POS"
    $lnk.Save()
}

Write-Host "`nInstalación completa. Abra 'MiniMarket POS' desde el Escritorio." -ForegroundColor Green
if (-not (Test-Path $secretos)) {
    Write-Host "Al abrirlo por primera vez le pedirá los datos de conexión con SQL Server." -ForegroundColor Yellow
}
Write-Host "Primer ingreso: usuario 'admin', contraseña 'Admin123!' (CÁMBIELA de inmediato en Sistema > Cambiar contraseña)." -ForegroundColor Yellow
