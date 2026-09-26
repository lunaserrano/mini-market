namespace MiniMarket.Application.Interfaces;

/// <summary>
/// Resuelve si la auditoría está habilitada para una empresa/sucursal, combinando el interruptor
/// global de appsettings (sección "Auditoria") con el override por empresa/sucursal guardado en
/// market.Parametro. Ver AuditoriaEstadoProvider.
/// </summary>
public interface IAuditoriaEstadoProvider
{
    Task<bool> EstaHabilitadaAsync(int? empresaId, int? sucursalId);
}
