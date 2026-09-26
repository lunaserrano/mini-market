namespace MiniMarket.Application.Interfaces.Repositories;

/// <summary>market.Parametro: configuración por empresa/sucursal (llave EmpresaId+SucursalId).</summary>
public interface IParametroRepository
{
    /// <summary>
    /// Null si no hay fila ni para la sucursal ni para la empresa (SucursalId = 0): el llamador debe
    /// asumir "habilitada" (fail-safe, ver AuditoriaEstadoProvider).
    /// </summary>
    Task<bool?> ObtenerAuditoriaHabilitadaAsync(int empresaId, int? sucursalId);

    Task ActualizarAuditoriaHabilitadaAsync(int empresaId, int sucursalId, bool habilitada, int? modificadoPorUsuarioId);
}
