<#
.SYNOPSIS
  Desinstala MiniMarket POS de este equipo: accesos directos y archivos del programa.
  NO toca la base de datos. La conexión guardada (%ProgramData%\MiniMarket) se conserva para una
  reinstalación, salvo que se indique -BorrarConfiguracion.
  Ejecutar como ADMINISTRADOR.
#>
param(
    [string]$Destino = "$env:ProgramFiles\MiniMarket",
    [switch]$BorrarConfiguracion
)

$ErrorActionPreference = "Stop"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecute este script como Administrador."
}

Get-Process -Name "MiniMarket.Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force

# Servicio de versiones anteriores, si quedó alguno.
if (Get-Service -Name "MiniMarketApi" -ErrorAction SilentlyContinue) {
    Stop-Service "MiniMarketApi" -Force -ErrorAction SilentlyContinue
    sc.exe delete "MiniMarketApi" | Out-Null
}
Get-NetFirewallRule -DisplayName "MiniMarket Api" -ErrorAction SilentlyContinue | Remove-NetFirewallRule

foreach ($ruta in @(
    (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "MiniMarket POS.lnk"),
    (Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "MiniMarket POS.lnk"))) {
    Remove-Item $ruta -ErrorAction SilentlyContinue
}

if (Test-Path $Destino) { Remove-Item $Destino -Recurse -Force }

$datos = Join-Path $env:ProgramData "MiniMarket"
if ($BorrarConfiguracion -and (Test-Path $datos)) {
    Remove-Item $datos -Recurse -Force
    Write-Host "Configuración de conexión eliminada." -ForegroundColor Yellow
}

Write-Host "MiniMarket desinstalado. La base de datos no se modificó." -ForegroundColor Green
