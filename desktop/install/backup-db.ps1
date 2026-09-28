<#
.SYNOPSIS
  Respaldo completo de la base local (sin internet, el respaldo es la única protección ante una falla
  del disco). Programarlo a diario con el Programador de tareas, idealmente hacia un USB/NAS:

    schtasks /Create /SC DAILY /ST 22:00 /RU SYSTEM /TN "MiniMarket Backup" ^
      /TR "powershell -ExecutionPolicy Bypass -File \"C:\Program Files\MiniMarket\Tools\backup-db.ps1\" -Carpeta D:\Respaldos"

  Conserva los últimos -Dias respaldos.
#>
param(
    [string]$SqlServer = ".\SQLEXPRESS",
    [string]$Database = "MiniMarket",
    [string]$Carpeta = "C:\MiniMarketBackups",
    [int]$Dias = 14
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force $Carpeta | Out-Null
$archivo = Join-Path $Carpeta ("{0}_{1:yyyyMMdd_HHmm}.bak" -f $Database, (Get-Date))

sqlcmd -S $SqlServer -E -C -b -Q "BACKUP DATABASE [$Database] TO DISK = N'$archivo' WITH INIT, COMPRESSION, CHECKSUM, STATS = 25"
if ($LASTEXITCODE -ne 0) {
    # SQL Server Express no soporta COMPRESSION en versiones antiguas: reintento sin compresión.
    sqlcmd -S $SqlServer -E -C -b -Q "BACKUP DATABASE [$Database] TO DISK = N'$archivo' WITH INIT, CHECKSUM"
    if ($LASTEXITCODE -ne 0) { throw "Falló el respaldo de $Database." }
}

Get-ChildItem $Carpeta -Filter "$($Database)_*.bak" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$Dias) } |
    Remove-Item -Force

Write-Host "Respaldo creado: $archivo" -ForegroundColor Green
