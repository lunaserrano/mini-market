<#
.SYNOPSIS
  Quita el servicio MiniMarketApi, los archivos instalados y el acceso directo.
  NO borra la base de datos (los datos de la tienda quedan intactos en SQL Server).
  Ejecutar como ADMINISTRADOR.
#>
param(
    [string]$Destino = "$env:ProgramFiles\MiniMarket",
    [string]$ServiceName = "MiniMarketApi",
    [switch]$ConservarArchivos
)

$ErrorActionPreference = "Stop"

$servicio = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($servicio) {
    if ($servicio.Status -ne "Stopped") { Stop-Service $ServiceName -Force }
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Servicio $ServiceName eliminado."
}

Get-NetFirewallRule -DisplayName "MiniMarket Api" -ErrorAction SilentlyContinue | Remove-NetFirewallRule

$lnk = Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "MiniMarket POS.lnk"
if (Test-Path $lnk) { Remove-Item $lnk -Force }

if (-not $ConservarArchivos -and (Test-Path $Destino)) {
    Remove-Item $Destino -Recurse -Force
    Write-Host "Archivos eliminados de $Destino."
}

Write-Host "Desinstalación completa. La base de datos no se modificó." -ForegroundColor Green
