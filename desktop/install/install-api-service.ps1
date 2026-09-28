<#
.SYNOPSIS
  Instala MiniMarket en el equipo servidor (o en la única PC de la tienda):
    1. Copia la Api a C:\Program Files\MiniMarket\Api (y el cliente a ...\Desktop)
    2. Genera appsettings.Secrets.json con la connection string y la Jwt:Key cifradas (DPAPI)
    3. Registra y arranca el servicio Windows "MiniMarketApi" (inicio automático, reinicio ante fallos)
    4. Verifica GET http://127.0.0.1:5080/api/health
    5. Crea el acceso directo del cliente en el Escritorio público

  Requisitos previos: SQL Server Express instalado y database\desktop\00_setup_local.sql ejecutado.
  Ejecutar como ADMINISTRADOR desde la carpeta dist\install.

.EXAMPLE
  .\install-api-service.ps1 -SqlServer ".\SQLEXPRESS" -Database MiniMarket -SqlUser minimarket_api
  (pide la contraseña SQL de forma segura)
#>
param(
    [string]$SqlServer = ".\SQLEXPRESS",
    [string]$Database = "MiniMarket",
    [string]$SqlUser = "minimarket_api",
    [string]$Destino = "$env:ProgramFiles\MiniMarket",
    [string]$ServiceName = "MiniMarketApi",
    [int]$Puerto = 5080,
    [switch]$AccesoRed
)

$ErrorActionPreference = "Stop"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute este script como Administrador."
}

$dist = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$apiOrigen = Join-Path $dist "api"
$desktopOrigen = Join-Path $dist "desktop"
$toolsOrigen = Join-Path $dist "tools"
foreach ($d in @($apiOrigen, $desktopOrigen, $toolsOrigen)) {
    if (-not (Test-Path $d)) { throw "No se encontró $d. Ejecute primero publish.ps1." }
}

$apiDestino = Join-Path $Destino "Api"
$desktopDestino = Join-Path $Destino "Desktop"
$toolsDestino = Join-Path $Destino "Tools"

# 1. Detener el servicio si ya existe (actualización) y copiar archivos
$servicio = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($servicio -and $servicio.Status -ne "Stopped") {
    Write-Host "Deteniendo servicio $ServiceName..."
    Stop-Service $ServiceName -Force
    Start-Sleep -Seconds 2
}

Write-Host "Copiando archivos a $Destino ..." -ForegroundColor Cyan
foreach ($par in @(@($apiOrigen, $apiDestino), @($desktopOrigen, $desktopDestino), @($toolsOrigen, $toolsDestino))) {
    New-Item -ItemType Directory -Force $par[1] | Out-Null
    # Se preserva appsettings.Secrets.json (secretos ya generados en este equipo) al actualizar.
    Copy-Item (Join-Path $par[0] "*") $par[1] -Recurse -Force -Exclude "appsettings.Secrets.json"
}

Copy-Item (Join-Path $PSScriptRoot "backup-db.ps1") $toolsDestino -Force

# 2. Secretos cifrados (DPAPI de máquina): solo válidos en ESTE equipo
$configTool = Join-Path $toolsDestino "MiniMarket.ConfigTool.exe"
$secretos = Join-Path $apiDestino "appsettings.Secrets.json"
if (-not (Test-Path $secretos)) {
    $pwdSeguro = Read-Host "Contraseña SQL del login '$SqlUser'" -AsSecureString
    $pwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwdSeguro))
    # Por variable de entorno (no por argumento): los argumentos son visibles en la lista de procesos.
    $env:MINIMARKET_SQL_PASSWORD = $pwd
    try {
        & $configTool init --dir $apiDestino --server $SqlServer --database $Database --user $SqlUser
        if ($LASTEXITCODE -ne 0) { throw "ConfigTool no pudo generar los secretos (código $LASTEXITCODE)." }
    } finally {
        Remove-Item Env:\MINIMARKET_SQL_PASSWORD -ErrorAction SilentlyContinue
        $pwd = $null
    }
} else {
    Write-Host "Se conservan los secretos existentes. Verificando..."
    & $configTool verify --dir $apiDestino
    if ($LASTEXITCODE -ne 0) { throw "Los secretos existentes no son válidos. Borre $secretos y vuelva a ejecutar." }
}

# Solo SYSTEM y Administradores pueden leer los secretos cifrados.
# SIDs en vez de nombres: "Administrators" se llama "Administradores" en Windows en español.
icacls $secretos /inheritance:r /grant:r "*S-1-5-18:(R)" "*S-1-5-32-544:(F)" | Out-Null

# Escuchar en la red local (varias cajas) o solo en este equipo.
$url = if ($AccesoRed) { "http://0.0.0.0:$Puerto" } else { "http://127.0.0.1:$Puerto" }

# 3. Servicio Windows
$exe = Join-Path $apiDestino "MiniMarket.Api.exe"
if (-not $servicio) {
    Write-Host "Registrando servicio $ServiceName ..." -ForegroundColor Cyan
    New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName "MiniMarket Api (local)" `
        -Description "Api local de MiniMarket POS (offline). El cliente WinForms se conecta a este servicio." `
        -StartupType Automatic | Out-Null
}
# Entorno Desktop y URL de escucha para el proceso del servicio.
$regKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
Set-ItemProperty -Path $regKey -Name Environment -Type MultiString -Value @(
    "ASPNETCORE_ENVIRONMENT=Desktop",
    "Kestrel__Endpoints__Http__Url=$url"
)
# Reinicio automático ante fallos (5 s, 10 s, 30 s).
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

if ($AccesoRed) {
    if (-not (Get-NetFirewallRule -DisplayName "MiniMarket Api" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName "MiniMarket Api" -Direction Inbound -Protocol TCP -LocalPort $Puerto -Action Allow -Profile Private,Domain | Out-Null
    }
    Write-Host "Acceso en red habilitado (perfil Privado/Dominio). En las demás cajas configure: http://<IP-de-este-equipo>:$Puerto/api/" -ForegroundColor Yellow
}

Write-Host "Iniciando servicio (la primera vez aplica migraciones y crea el usuario admin)..." -ForegroundColor Cyan
Start-Service $ServiceName

# 4. Health check
$ok = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    try {
        $salud = Invoke-RestMethod "http://127.0.0.1:$Puerto/api/health" -TimeoutSec 3
        if ($salud.db) { $ok = $true; break }
    } catch { }
}
if (-not $ok) { throw "El servicio no respondió en /api/health. Revise el Visor de eventos (Aplicación, origen MiniMarketApi)." }
Write-Host "Servicio en línea: $($salud.modo) v$($salud.version), base de datos OK." -ForegroundColor Green

# 5. Acceso directo del cliente
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "MiniMarket POS.lnk"))
$lnk.TargetPath = Join-Path $desktopDestino "MiniMarket.Desktop.exe"
$lnk.WorkingDirectory = $desktopDestino
$lnk.Save()

Write-Host "`nInstalación completa. Abra 'MiniMarket POS' en el Escritorio." -ForegroundColor Green
Write-Host "Primer ingreso: usuario 'admin', contraseña 'Admin123!' (CÁMBIELA de inmediato en Sistema > Cambiar contraseña)." -ForegroundColor Yellow
